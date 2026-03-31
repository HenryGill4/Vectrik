// gantt-viewport.js — Infinite scroll + fluid zoom for the scheduler Gantt chart.
// Loaded as an ES module from Blazor via IJSRuntime.InvokeAsync<IJSObjectReference>.
//
// Lifecycle: Blazor may destroy and recreate the Gantt container DOM at any time
// (e.g. during auto-refresh). The module preserves scroll/zoom state across those
// cycles via reinit() which rebinds to the new DOM without losing position.

let _dotNetRef = null;
let _container = null;
let _inner = null;
let _pixelsPerHour = 6.0;
let _dataStartIso = null;
let _dataStartMs = 0;
let _dataEndMs = 0;
let _debounceId = 0;
let _disposed = false;
let _initialized = false;

// Mouse drag state for desktop panning
let _isDragging = false;
let _dragStartX = 0;
let _dragStartScrollLeft = 0;

// Bar drag state for rescheduling
let _barDragEl = null;        // The bar element being dragged
let _barDragType = null;      // 'exec', 'build', or 'group'
let _barDragEntityId = null;  // The exec/program ID
let _barDragOrigLeft = 0;     // Original CSS left value in px
let _barDragStartX = 0;       // Mouse X at drag start
let _barDragStartY = 0;       // Mouse Y at drag start (for cross-row drag)
let _barDragActive = false;   // True once mouse moves past threshold
let _barDragGhost = null;     // Tooltip showing proposed time
let _barDragSourceMachineId = null;  // Machine ID the bar started on
let _barDragTargetMachineId = null;  // Machine ID the cursor is currently over
let _barDragHighlightedRow = null;   // Currently highlighted row element

// Saved viewport state — survives DOM destruction
let _savedScrollTimeMsFromStart = null; // ms offset from _dataStartMs at left edge of viewport
let _savedPixelsPerHour = null;

const MIN_PX_PER_HOUR = 0.5;
const MAX_PX_PER_HOUR = 120;
const ZOOM_FACTOR = 1.15;
const DEBOUNCE_MS = 150;

// ── Helpers (internal) ──────────────────────────────────────────────────────

function isAlive() {
    return _container != null && _container.isConnected !== false;
}

function detachListeners() {
    if (!_container) return;
    try {
        _container.removeEventListener('scroll', onScroll);
        _container.removeEventListener('wheel', onWheel, { capture: true });
        _container.removeEventListener('touchstart', onTouchStart);
        _container.removeEventListener('touchmove', onTouchMove);
        _container.removeEventListener('touchend', onTouchEnd);
        _container.removeEventListener('touchcancel', onTouchEnd);
        _container.removeEventListener('mousedown', onMouseDown);
        _container.removeEventListener('mousemove', onMouseMove);
        _container.removeEventListener('mouseup', onMouseUp);
        _container.removeEventListener('mouseleave', onMouseUp);
        _container.removeEventListener('contextmenu', onContextMenu);
        _container.removeEventListener('keydown', onKeyDown);
    } catch (e) {
        // Gracefully handle missing references during dispose
    }
}

function attachListeners() {
    if (!_container) return;
    _container.addEventListener('scroll', onScroll, { passive: true });
    _container.addEventListener('wheel', onWheel, { passive: false, capture: true });
    _container.addEventListener('touchstart', onTouchStart, { passive: true });
    _container.addEventListener('touchmove', onTouchMove, { passive: false });
    _container.addEventListener('touchend', onTouchEnd, { passive: true });
    _container.addEventListener('touchcancel', onTouchEnd, { passive: true });
    _container.addEventListener('mousedown', onMouseDown, { passive: false });
    _container.addEventListener('mousemove', onMouseMove, { passive: false });
    _container.addEventListener('mouseup', onMouseUp, { passive: true });
    _container.addEventListener('mouseleave', onMouseUp, { passive: true });
    _container.addEventListener('contextmenu', onContextMenu, { passive: false });
    _container.addEventListener('keydown', onKeyDown);
}

function clampZoom(v) {
    return Math.max(MIN_PX_PER_HOUR, Math.min(MAX_PX_PER_HOUR, v));
}

function updateInnerWidth() {
    if (!_inner) return;
    const totalHours = (_dataEndMs - _dataStartMs) / 3600000;
    _inner.style.width = (totalHours * _pixelsPerHour) + 'px';
}

function notifyViewportChanged() {
    if (_disposed || !_dotNetRef || !isAlive()) return;
    const vp = getViewport();
    _dotNetRef.invokeMethodAsync('OnViewportChanged', vp.startIso, vp.endIso, vp.pixelsPerHour);
}

function debouncedNotify() {
    clearTimeout(_debounceId);
    _debounceId = setTimeout(notifyViewportChanged, DEBOUNCE_MS);
}

// Debounce zoom C# notifications — batches rapid Ctrl+wheel/pinch events
// so only the final zoom level triggers a Blazor re-render (~60ms = ~1 frame at 16fps)
let _zoomNotifyId = 0;
function debouncedZoomNotify(anchorTimeHours, anchorViewportX) {
    clearTimeout(_zoomNotifyId);
    _zoomNotifyId = setTimeout(() => {
        if (_disposed || !_dotNetRef) return;
        // Send the actual accumulated _pixelsPerHour (not direction) so C# gets the
        // correct value even after rapid wheel events during the debounce window.
        _dotNetRef.invokeMethodAsync('OnZoomApplied', _pixelsPerHour, anchorTimeHours, anchorViewportX);
    }, 60);
}

// ── Zoom anchor + MutationObserver ─────────────────────────────────────────
// When JS zooms immediately, Blazor re-renders ~60-100ms later and patches
// the DOM. The browser may reflow and disturb scrollLeft. The MutationObserver
// restores scrollLeft in the same microtask as the DOM mutation (before the
// browser paints), eliminating visible jank.
let _zoomAnchor = null; // { timeHours, viewportX }
let _innerObserver = null;
let _zoomAnchorClearId = 0;

function setZoomAnchor(timeHours, viewportX) {
    _zoomAnchor = { timeHours, viewportX };
    // Clear anchor after the Blazor re-render cycle is certainly complete.
    // This prevents the observer from incorrectly restoring scroll on
    // unrelated DOM mutations (e.g. data refresh).
    clearTimeout(_zoomAnchorClearId);
    _zoomAnchorClearId = setTimeout(() => { _zoomAnchor = null; }, 500);
}

function setupInnerObserver() {
    if (_innerObserver) _innerObserver.disconnect();
    if (!_inner || !_container) return;

    _innerObserver = new MutationObserver(() => {
        if (!_zoomAnchor || !isAlive()) return;
        // Blazor patched the DOM — restore scroll before the browser paints.
        _container.scrollLeft = _zoomAnchor.timeHours * _pixelsPerHour - _zoomAnchor.viewportX;
    });

    _innerObserver.observe(_inner, {
        attributes: true,
        attributeFilter: ['style'],
        childList: true
    });
}

function teardownInnerObserver() {
    if (_innerObserver) {
        _innerObserver.disconnect();
        _innerObserver = null;
    }
    _zoomAnchor = null;
    clearTimeout(_zoomAnchorClearId);
}

function touchDist(t) {
    const dx = t[0].clientX - t[1].clientX;
    const dy = t[0].clientY - t[1].clientY;
    return Math.sqrt(dx * dx + dy * dy);
}

function bindToContainer(containerEl) {
    _container = containerEl;
    _inner = _container.querySelector('.gantt-inner');
    if (!_inner) {
        console.warn('gantt-viewport: .gantt-inner not found inside container');
        return false;
    }
    updateInnerWidth();
    attachListeners();
    setupInnerObserver();
    return true;
}

// ── Internal state save/restore ─────────────────────────────────────────────

function saveStateInternal() {
    if (isAlive() && _pixelsPerHour > 0) {
        _savedScrollTimeMsFromStart = (_container.scrollLeft / _pixelsPerHour) * 3600000;
        _savedPixelsPerHour = _pixelsPerHour;
    } else if (_savedPixelsPerHour == null) {
        _savedPixelsPerHour = _pixelsPerHour;
        _savedScrollTimeMsFromStart = 0;
    }
}

function restoreStateInternal() {
    if (_savedPixelsPerHour != null) {
        _pixelsPerHour = _savedPixelsPerHour;
    }
    if (_inner) {
        updateInnerWidth();
    }
    if (isAlive() && _savedScrollTimeMsFromStart != null) {
        const hoursFromStart = _savedScrollTimeMsFromStart / 3600000;
        _container.scrollLeft = hoursFromStart * _pixelsPerHour;
    }
}

// ── Public API ──────────────────────────────────────────────────────────────

export function initGanttViewport(dotNetRef, containerEl, dataStartIso, dataEndIso, pixelsPerHour) {
    detachListeners();

    // Always store dotNetRef so reinit() can notify C# even if the container
    // isn't in the DOM yet (e.g. during Blazor's first render while loading).
    _dotNetRef = dotNetRef;
    _pixelsPerHour = pixelsPerHour || 6.0;
    _dataStartIso = dataStartIso;
    _dataStartMs = new Date(dataStartIso).getTime();
    _dataEndMs = new Date(dataEndIso).getTime();
    _disposed = false;
    _isDragging = false;

    if (!containerEl) {
        console.warn('gantt-viewport: containerEl is null — will bind on reinit()');
        return;
    }

    if (!bindToContainer(containerEl)) return;

    _initialized = true;

    // Scroll to "now" on first init — but only if we don't have saved state
    // (saved state means the component was destroyed/recreated, not a fresh page load)
    if (_savedScrollTimeMsFromStart != null) {
        restoreStateInternal();
    } else {
        setTimeout(() => scrollToTime(new Date().toISOString()), 50);
    }
}

/**
 * Rebind to a new DOM container after Blazor re-render.
 * Preserves scroll position and zoom level.
 */
export function reinit(containerEl, dataStartIso, dataEndIso, pixelsPerHour) {
    if (!containerEl) {
        console.warn('gantt-viewport reinit: containerEl is null');
        return;
    }

    // Save current viewport position before detaching old container
    saveStateInternal();
    detachListeners();

    // Update data range if provided
    if (dataStartIso && dataEndIso) {
        _dataStartIso = dataStartIso;
        _dataStartMs = new Date(dataStartIso).getTime();
        _dataEndMs = new Date(dataEndIso).getTime();
    }

    // Sync zoom level from C# (C# is authoritative)
    if (pixelsPerHour != null && pixelsPerHour > 0) {
        _pixelsPerHour = clampZoom(pixelsPerHour);
        _savedPixelsPerHour = _pixelsPerHour;
    }

    _disposed = false;
    _isDragging = false;

    if (!bindToContainer(containerEl)) return;

    _initialized = true;

    // Restore saved scroll position at the (possibly updated) zoom level
    restoreStateInternal();

    // Notify C# so viewport bounds stay in sync
    notifyViewportChanged();
}

export function dispose() {
    _disposed = true;
    _initialized = false;
    _isDragging = false;
    teardownInnerObserver();
    detachListeners();
    _dotNetRef = null;
    _container = null;
    _inner = null;
}

export function scrollToTime(isoDateTimeString) {
    if (!isAlive() || !_inner) return;
    const targetMs = new Date(isoDateTimeString).getTime();
    const hoursFromStart = (targetMs - _dataStartMs) / 3600000;
    const px = hoursFromStart * _pixelsPerHour;
    const scrollTarget = px - _container.clientWidth / 2;
    _container.scrollLeft = Math.max(0, scrollTarget);
}

/**
 * Called by C# ZoomInAsync / ZoomOutAsync.
 * Zooms around the viewport center and returns anchor info so C# can
 * restore scroll after the Blazor DOM diff (which may displace scrollLeft).
 */
export function applyZoom(pixelsPerHour) {
    if (!isAlive() || !_inner) {
        _pixelsPerHour = clampZoom(pixelsPerHour);
        return { anchorTimeHours: 0, anchorViewportX: 0 };
    }

    // Save center time using current JS _pixelsPerHour (DOM still at old positions)
    const anchorViewportX = _container.clientWidth / 2;
    const anchorTimeHours = (_container.scrollLeft + anchorViewportX) / _pixelsPerHour;

    _pixelsPerHour = clampZoom(pixelsPerHour);
    updateInnerWidth();

    // Restore center — scroll persists through Blazor re-render
    _container.scrollLeft = anchorTimeHours * _pixelsPerHour - anchorViewportX;

    // Store anchor — MutationObserver will restore scroll if Blazor's DOM patch displaces it
    setZoomAnchor(anchorTimeHours, anchorViewportX);

    return { anchorTimeHours, anchorViewportX };
}

/**
 * Called by C# OnAfterRenderAsync after Ctrl+wheel/pinch zoom.
 * Blazor has re-rendered tick/bar positions. JS already has the correct
 * _pixelsPerHour — this restores scrollLeft as a safety net in case
 * the Blazor DOM diff displaced the scroll position.
 * The MutationObserver usually handles this first (same-frame), so this
 * is a belt-and-suspenders fallback via SignalR.
 */
export function syncScrollToAnchor(anchorTimeHours, anchorViewportX) {
    if (!isAlive() || !_inner) return;
    _container.scrollLeft = anchorTimeHours * _pixelsPerHour - anchorViewportX;
    // Clear anchor — the observer's job is done for this zoom cycle
    _zoomAnchor = null;
    clearTimeout(_zoomAnchorClearId);
}

export function getViewport() {
    if (!isAlive()) {
        return { startIso: _dataStartIso, endIso: _dataStartIso, pixelsPerHour: _pixelsPerHour };
    }
    const scrollLeft = _container.scrollLeft;
    const clientWidth = _container.clientWidth;
    const startHours = scrollLeft / _pixelsPerHour;
    const endHours = (scrollLeft + clientWidth) / _pixelsPerHour;
    const startMs = _dataStartMs + startHours * 3600000;
    const endMs = _dataStartMs + endHours * 3600000;
    return {
        startIso: new Date(startMs).toISOString(),
        endIso: new Date(endMs).toISOString(),
        pixelsPerHour: _pixelsPerHour
    };
}

export function updateDataRange(dataStartIso, dataEndIso, pixelsPerHour) {
    if (!_initialized) return;

    const oldStartMs = _dataStartMs;
    _dataStartIso = dataStartIso;
    _dataStartMs = new Date(dataStartIso).getTime();
    _dataEndMs = new Date(dataEndIso).getTime();
    _pixelsPerHour = pixelsPerHour || _pixelsPerHour;

    if (_inner) {
        updateInnerWidth();
    }

    if (isAlive() && oldStartMs !== _dataStartMs) {
        const shiftHours = (oldStartMs - _dataStartMs) / 3600000;
        _container.scrollLeft += shiftHours * _pixelsPerHour;
    }
}

export function saveState() {
    saveStateInternal();
    return { pixelsPerHour: _savedPixelsPerHour, scrollTimeMsFromStart: _savedScrollTimeMsFromStart };
}

export function restoreState() {
    restoreStateInternal();
}

// ── Event Handlers ──────────────────────────────────────────────────────────

function onScroll() {
    if (_disposed || !isAlive()) return;
    debouncedNotify();
}

function onWheel(e) {
    if (_disposed || !isAlive() || !_inner) return;

    // Shift+wheel = horizontal pan
    if (e.shiftKey && !e.ctrlKey && !e.metaKey) {
        e.preventDefault();
        _container.scrollLeft += e.deltaY;
        debouncedNotify();
        return;
    }

    // Ctrl+wheel or Meta+wheel (Mac) = zoom
    if (!e.ctrlKey && !e.metaKey) return;

    e.preventDefault();
    e.stopPropagation();

    const direction = e.deltaY < 0 ? 1 : -1;
    const rect = _container.getBoundingClientRect();
    const cursorViewportX = e.clientX - rect.left;
    const cursorTimeHours = (_container.scrollLeft + cursorViewportX) / _pixelsPerHour;

    // Apply zoom immediately in JS so scroll position persists through Blazor re-render.
    // This is the same pattern as button zoom (applyZoom) — update JS FIRST, then notify C#.
    const newPph = clampZoom(direction > 0
        ? _pixelsPerHour * ZOOM_FACTOR
        : _pixelsPerHour / ZOOM_FACTOR);
    if (Math.abs(newPph - _pixelsPerHour) < 0.001) return;

    _pixelsPerHour = newPph;
    updateInnerWidth();
    // Anchor zoom to cursor position — Ctrl+wheel fires in JS before Blazor
    // re-renders, so we must correct scrollLeft immediately here
    _container.scrollLeft = cursorTimeHours * _pixelsPerHour - cursorViewportX;

    // Store anchor — MutationObserver will restore scroll if Blazor's DOM patch displaces it
    setZoomAnchor(cursorTimeHours, cursorViewportX);

    // Debounce C# notification — rapid wheel events batch into one re-render
    debouncedZoomNotify(cursorTimeHours, cursorViewportX);
}

let _pinchDist = 0;
let _touchBarMode = false;      // true = single-finger touch on a bar (potential drag)
let _touchBarLongPress = null;   // timeout ID for long-press detection

function onTouchStart(e) {
    if (_disposed || !isAlive()) return;

    // Two-finger: pinch zoom
    if (e.touches.length === 2) {
        // Cancel any pending bar drag
        cancelTouchBarDrag();
        _pinchDist = touchDist(e.touches);
        return;
    }

    // Single-finger: check if touching a draggable bar
    if (e.touches.length === 1) {
        const touch = e.touches[0];
        const bar = document.elementFromPoint(touch.clientX, touch.clientY)?.closest('[data-bar-type]');
        if (bar) {
            // Start potential bar drag — use a short delay to distinguish from scroll
            const wrapper = bar.closest('.gantt-build-bar-wrapper') || bar;
            _barDragEl = wrapper;
            _barDragType = bar.dataset.barType;
            _barDragEntityId = bar.dataset.execId || bar.dataset.programId;
            _barDragOrigLeft = parseFloat(wrapper.style.left) || 0;
            _barDragStartX = touch.clientX;
            _barDragStartY = touch.clientY;
            _barDragSourceMachineId = wrapper.dataset.machineId || null;
            _barDragTargetMachineId = _barDragSourceMachineId;
            _barDragActive = false;
            _touchBarMode = true;

            // Long-press activates drag immediately (300ms)
            _touchBarLongPress = setTimeout(() => {
                _touchBarLongPress = null;
                if (_barDragEl && !_barDragActive) {
                    activateBarDrag();
                }
            }, 300);
            return;
        }
    }
}

function onTouchMove(e) {
    if (_disposed || !isAlive() || !_inner) return;

    // Two-finger pinch zoom
    if (e.touches.length === 2) {
        const cur = touchDist(e.touches);
        const delta = cur - _pinchDist;
        if (Math.abs(delta) < 20) return;

        e.preventDefault();
        _pinchDist = cur;

        const direction = delta > 0 ? 1 : -1;
        const rect = _container.getBoundingClientRect();
        const midX = (e.touches[0].clientX + e.touches[1].clientX) / 2;
        const anchorViewportX = midX - rect.left;
        const anchorTimeHours = (_container.scrollLeft + anchorViewportX) / _pixelsPerHour;

        // Apply zoom immediately in JS (same pattern as Ctrl+wheel)
        const newPph = clampZoom(direction > 0
            ? _pixelsPerHour * ZOOM_FACTOR
            : _pixelsPerHour / ZOOM_FACTOR);
        if (Math.abs(newPph - _pixelsPerHour) < 0.001) return;

        _pixelsPerHour = newPph;
        updateInnerWidth();
        // Pinch zoom goes through Blazor re-render via debouncedZoomNotify —
        // syncScrollToAnchor handles scroll correction after DOM diff.

        // Store anchor — MutationObserver will restore scroll if Blazor's DOM patch displaces it
        setZoomAnchor(anchorTimeHours, anchorViewportX);

        debouncedZoomNotify(anchorTimeHours, anchorViewportX);
        return;
    }

    // Single-finger bar drag
    if (e.touches.length === 1 && _touchBarMode && _barDragEl) {
        const touch = e.touches[0];
        const deltaX = touch.clientX - _barDragStartX;
        const deltaY = touch.clientY - _barDragStartY;
        const totalDelta = Math.abs(deltaX) + Math.abs(deltaY);

        if (!_barDragActive && totalDelta > BAR_DRAG_THRESHOLD) {
            // Cancel long-press timer — movement activated drag
            if (_touchBarLongPress) { clearTimeout(_touchBarLongPress); _touchBarLongPress = null; }

            // If mostly vertical movement, let browser scroll instead
            if (Math.abs(deltaY) > Math.abs(deltaX) * 2 && _barDragType !== 'build') {
                cancelTouchBarDrag();
                return;
            }
            activateBarDrag();
        }

        if (_barDragActive) {
            e.preventDefault(); // Prevent scrolling while dragging bar
            moveBarDrag(touch.clientX, touch.clientY);
        }
    }
}

function onTouchEnd(e) {
    if (!_touchBarMode) return;

    if (_touchBarLongPress) { clearTimeout(_touchBarLongPress); _touchBarLongPress = null; }

    if (_barDragEl && _barDragActive) {
        completeBarDrag();
    }

    // Reset touch bar state
    _touchBarMode = false;
    resetBarDragState();
}

function cancelTouchBarDrag() {
    if (_touchBarLongPress) { clearTimeout(_touchBarLongPress); _touchBarLongPress = null; }
    _touchBarMode = false;
    if (_barDragEl && !_barDragActive) {
        resetBarDragState();
    }
}

function onContextMenu(e) {
    if (_isDragging || _barDragActive || e.button === 2) {
        e.preventDefault();
    }
}

// ── Keyboard Controls ───────────────────────────────────────────────────────
// Container needs tabindex="0" to receive focus. Set via Razor attribute.
//
// Keys:
//   +/=          Zoom in (center-anchored)
//   -            Zoom out (center-anchored)
//   0            Reset zoom to default (6.0 px/hr)
//   Left/Right   Pan one screen-width quarter (Shift = full screen-width)
//   Home         Scroll to now
//   [/]          Fine pan left/right (1 hour per press)

const PAN_FRACTION = 0.25;           // Arrow keys pan 25% of viewport width
const PAN_FRACTION_SHIFT = 1.0;      // Shift+arrow pans full viewport width
const FINE_PAN_HOURS = 1;            // [/] keys pan 1 hour

function onKeyDown(e) {
    if (_disposed || !isAlive() || !_inner) return;

    // Don't capture if user is in an input/textarea/select
    const tag = e.target?.tagName;
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;

    switch (e.key) {
        case '+':
        case '=': {
            e.preventDefault();
            zoomAtCenter(1);
            break;
        }
        case '-': {
            e.preventDefault();
            zoomAtCenter(-1);
            break;
        }
        case '0': {
            e.preventDefault();
            // Reset to default zoom, keep center time stable
            const centerTimeHours = (_container.scrollLeft + _container.clientWidth / 2) / _pixelsPerHour;
            _pixelsPerHour = 6.0;
            updateInnerWidth();
            _container.scrollLeft = centerTimeHours * _pixelsPerHour - _container.clientWidth / 2;
            debouncedZoomNotify(centerTimeHours, _container.clientWidth / 2);
            break;
        }
        case 'ArrowLeft': {
            e.preventDefault();
            const fraction = e.shiftKey ? PAN_FRACTION_SHIFT : PAN_FRACTION;
            _container.scrollLeft -= _container.clientWidth * fraction;
            debouncedNotify();
            break;
        }
        case 'ArrowRight': {
            e.preventDefault();
            const fraction = e.shiftKey ? PAN_FRACTION_SHIFT : PAN_FRACTION;
            _container.scrollLeft += _container.clientWidth * fraction;
            debouncedNotify();
            break;
        }
        case '[': {
            e.preventDefault();
            _container.scrollLeft -= FINE_PAN_HOURS * _pixelsPerHour;
            debouncedNotify();
            break;
        }
        case ']': {
            e.preventDefault();
            _container.scrollLeft += FINE_PAN_HOURS * _pixelsPerHour;
            debouncedNotify();
            break;
        }
        case 'Home': {
            e.preventDefault();
            scrollToTime(new Date().toISOString());
            debouncedNotify();
            break;
        }
        default:
            return; // Don't prevent default for unhandled keys
    }
}

/** Zoom in/out anchored to viewport center. direction: 1 = in, -1 = out */
function zoomAtCenter(direction) {
    const anchorViewportX = _container.clientWidth / 2;
    const anchorTimeHours = (_container.scrollLeft + anchorViewportX) / _pixelsPerHour;

    const newPph = clampZoom(direction > 0
        ? _pixelsPerHour * ZOOM_FACTOR
        : _pixelsPerHour / ZOOM_FACTOR);
    if (Math.abs(newPph - _pixelsPerHour) < 0.001) return;

    _pixelsPerHour = newPph;
    updateInnerWidth();
    // Keyboard zoom fires in JS — correct scrollLeft immediately (same as Ctrl+wheel)
    _container.scrollLeft = anchorTimeHours * _pixelsPerHour - anchorViewportX;

    debouncedZoomNotify(anchorTimeHours, anchorViewportX);
}

// ── Bar Drag-to-Reschedule ──────────────────────────────────────────────────

const BAR_DRAG_THRESHOLD = 8; // px before drag activates (higher to avoid accidental drag on click)

/** Shared: activate drag visuals (called from both mouse and touch paths) */
function activateBarDrag() {
    _barDragActive = true;
    _barDragEl.style.opacity = '0.7';
    _barDragEl.style.zIndex = '100';
    _barDragEl.classList.add('gantt-bar-dragging');
    document.body.style.cursor = 'grabbing';
    document.body.style.userSelect = 'none';

    // Create ghost tooltip — fixed position so it follows the cursor cleanly
    _barDragGhost = document.createElement('div');
    _barDragGhost.className = 'gantt-drag-ghost';
    document.body.appendChild(_barDragGhost);
}

/** Shared: update bar position during drag */
function moveBarDrag(clientX, clientY) {
    if (!_barDragEl || !_barDragActive) return;

    const deltaX = clientX - _barDragStartX;
    const deltaY = clientY - _barDragStartY;
    const newLeft = _barDragOrigLeft + deltaX;
    _barDragEl.style.left = newLeft + 'px';

    // Allow vertical movement for cross-machine drag (build bars only)
    if (_barDragType === 'build') {
        _barDragEl.style.transform = `translateY(${deltaY}px)`;
        detectTargetMachineRow(clientY);
    }

    // Update ghost tooltip with proposed time
    if (_barDragGhost) {
        const containerRect = _container.getBoundingClientRect();
        const cursorXInContainer = clientX - containerRect.left + _container.scrollLeft;
        const timeMs = _dataStartMs + (cursorXInContainer / _pixelsPerHour) * 3600000;
        const dt = new Date(timeMs);
        let ghostText = dt.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }) + ' ' + dt.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });

        // Show target machine name if different from source
        if (_barDragTargetMachineId && _barDragTargetMachineId !== _barDragSourceMachineId && _barDragHighlightedRow) {
            const machineName = _barDragHighlightedRow.querySelector('.gantt-machine-name')?.textContent?.trim();
            if (machineName) ghostText += '\n→ ' + machineName;
        }

        _barDragGhost.textContent = ghostText;
        _barDragGhost.style.left = (clientX + 12) + 'px';
        _barDragGhost.style.top = (clientY - 28) + 'px';
    }
}

/** Shared: complete the drag and notify Blazor */
function completeBarDrag() {
    if (!_barDragEl || !_barDragActive) return;

    const finalLeft = parseFloat(_barDragEl.style.left) || 0;
    const newTimeMs = _dataStartMs + (finalLeft / _pixelsPerHour) * 3600000;
    const newTimeIso = new Date(newTimeMs).toISOString();

    // Cleanup visual state
    _barDragEl.style.opacity = '';
    _barDragEl.style.zIndex = '';
    _barDragEl.style.transform = '';
    _barDragEl.classList.remove('gantt-bar-dragging');
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
    if (_barDragGhost) {
        _barDragGhost.remove();
        _barDragGhost = null;
    }
    if (_barDragHighlightedRow) {
        _barDragHighlightedRow.classList.remove('gantt-drop-target');
        _barDragHighlightedRow = null;
    }

    // Restore original position (Blazor will re-render at the correct position after reschedule)
    _barDragEl.style.left = _barDragOrigLeft + 'px';

    // Notify Blazor with target machine ID
    if (_dotNetRef && _barDragEntityId) {
        _dotNetRef.invokeMethodAsync('OnBarDragCompleted', _barDragType, _barDragEntityId, newTimeIso, _barDragTargetMachineId || '0');
    }
}

/** Shared: reset all bar drag state variables */
function resetBarDragState() {
    _barDragEl = null;
    _barDragType = null;
    _barDragEntityId = null;
    _barDragActive = false;
    _barDragSourceMachineId = null;
    _barDragTargetMachineId = null;
}

function detectTargetMachineRow(clientY) {
    // Remove highlight from previous row
    if (_barDragHighlightedRow) {
        _barDragHighlightedRow.classList.remove('gantt-drop-target');
        _barDragHighlightedRow = null;
    }

    const rows = _container.querySelectorAll('.gantt-row[data-machine-id]');
    for (const row of rows) {
        const rect = row.getBoundingClientRect();
        if (clientY >= rect.top && clientY <= rect.bottom) {
            _barDragTargetMachineId = row.dataset.machineId;
            row.classList.add('gantt-drop-target');
            _barDragHighlightedRow = row;
            return;
        }
    }
    // If cursor outside all rows, keep current target
}

function onMouseDown(e) {
    if (_disposed || !isAlive()) return;

    // Check if clicking on a draggable bar (left button only)
    if (e.button === 0) {
        const bar = e.target.closest('[data-bar-type]');
        if (bar) {
            // Don't preventDefault here — let Blazor's @onclick fire if user just clicks.
            // We only block default once drag threshold is exceeded (in onMouseMove).
            const wrapper = bar.closest('.gantt-build-bar-wrapper') || bar;
            _barDragEl = wrapper;
            _barDragType = bar.dataset.barType;
            _barDragEntityId = bar.dataset.execId || bar.dataset.programId;
            _barDragOrigLeft = parseFloat(wrapper.style.left) || 0;
            _barDragStartX = e.clientX;
            _barDragStartY = e.clientY;
            _barDragSourceMachineId = wrapper.dataset.machineId || null;
            _barDragTargetMachineId = _barDragSourceMachineId;
            _barDragActive = false;
            return;
        }
    }

    // Left-click on empty space (no bar hit above) or middle/right button = pan
    if (e.button === 0 || e.button === 1 || e.button === 2) {
        if (e.button !== 0) e.preventDefault(); // Don't prevent default for left (allows text selection if needed)
        _isDragging = true;
        _dragStartX = e.clientX;
        _dragStartScrollLeft = _container.scrollLeft;
        _container.style.cursor = 'grabbing';
        _container.style.userSelect = 'none';
    }
}

function onMouseMove(e) {
    // Bar drag in progress
    if (_barDragEl) {
        const deltaX = e.clientX - _barDragStartX;
        const deltaY = e.clientY - _barDragStartY;
        const totalDelta = Math.abs(deltaX) + Math.abs(deltaY);

        // Activate drag after threshold
        if (!_barDragActive && totalDelta > BAR_DRAG_THRESHOLD) {
            e.preventDefault();
            activateBarDrag();
        }

        if (_barDragActive) {
            e.preventDefault();
            moveBarDrag(e.clientX, e.clientY);
        }
        return;
    }

    // Viewport pan
    if (!_isDragging || _disposed || !isAlive()) return;
    const deltaX = e.clientX - _dragStartX;
    _container.scrollLeft = _dragStartScrollLeft - deltaX;
}

function onMouseUp(e) {
    // Complete bar drag
    if (_barDragEl && _barDragActive) {
        completeBarDrag();
    }

    // Reset bar drag state
    resetBarDragState();

    // Complete viewport pan
    if (_isDragging) {
        _isDragging = false;
        if (isAlive()) {
            _container.style.cursor = '';
            _container.style.userSelect = '';
        }
        debouncedNotify();
    }
}
