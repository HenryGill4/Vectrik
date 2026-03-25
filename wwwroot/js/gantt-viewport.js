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
let _barDragActive = false;   // True once mouse moves past threshold
let _barDragGhost = null;     // Tooltip showing proposed time

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
        _container.removeEventListener('mousedown', onMouseDown);
        _container.removeEventListener('mousemove', onMouseMove);
        _container.removeEventListener('mouseup', onMouseUp);
        _container.removeEventListener('mouseleave', onMouseUp);
        _container.removeEventListener('contextmenu', onContextMenu);
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
    _container.addEventListener('mousedown', onMouseDown, { passive: false });
    _container.addEventListener('mousemove', onMouseMove, { passive: true });
    _container.addEventListener('mouseup', onMouseUp, { passive: true });
    _container.addEventListener('mouseleave', onMouseUp, { passive: true });
    _container.addEventListener('contextmenu', onContextMenu, { passive: false });
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

    // Scroll to "now" on first init
    setTimeout(() => scrollToTime(new Date().toISOString()), 50);
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
 * C# has already updated _pixelsPerHour; this syncs JS and keeps viewport centered.
 * Runs BEFORE Blazor auto-render so scroll position persists through the diff.
 */
export function applyZoom(pixelsPerHour) {
    if (!isAlive() || !_inner) {
        _pixelsPerHour = clampZoom(pixelsPerHour);
        return;
    }

    // Save center time using current JS _pixelsPerHour (DOM still at old positions)
    const centerX = _container.scrollLeft + _container.clientWidth / 2;
    const centerTimeHours = centerX / _pixelsPerHour;

    _pixelsPerHour = clampZoom(pixelsPerHour);
    updateInnerWidth();

    // Restore center — scroll persists through Blazor re-render
    _container.scrollLeft = centerTimeHours * _pixelsPerHour - _container.clientWidth / 2;
}

/**
 * Called by C# OnAfterRenderAsync after Ctrl+wheel zoom.
 * Blazor has already re-rendered tick/bar positions at the new zoom.
 * This syncs JS _pixelsPerHour and scrolls so the cursor anchor stays put.
 */
export function applyZoomAnchored(pixelsPerHour, anchorTimeHours, anchorViewportX) {
    _pixelsPerHour = clampZoom(pixelsPerHour);

    if (!isAlive() || !_inner) return;

    // The DOM already has the correct inner width from Blazor's render
    // Just scroll so the anchor time is at the same viewport X position
    _container.scrollLeft = anchorTimeHours * _pixelsPerHour - anchorViewportX;
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

    // Delegate zoom to C# so tick/bar positions re-render at the correct scale.
    // Pass the zoom direction and cursor anchor so C# can restore scroll after render.
    const direction = e.deltaY < 0 ? 1 : -1;
    const rect = _container.getBoundingClientRect();
    const cursorViewportX = e.clientX - rect.left;
    const cursorTimeHours = (_container.scrollLeft + cursorViewportX) / _pixelsPerHour;

    if (_dotNetRef) {
        _dotNetRef.invokeMethodAsync('OnZoomRequested', direction, cursorTimeHours, cursorViewportX);
    }
}

let _pinchDist = 0;

function onTouchStart(e) {
    if (e.touches.length === 2) {
        _pinchDist = touchDist(e.touches);
    }
}

function onTouchMove(e) {
    if (_disposed || !isAlive() || !_inner || e.touches.length !== 2) return;

    const cur = touchDist(e.touches);
    const delta = cur - _pinchDist;
    if (Math.abs(delta) < 20) return;

    e.preventDefault();
    _pinchDist = cur;

    // Delegate zoom to C# so tick/bar positions re-render at the correct scale
    const direction = delta > 0 ? 1 : -1;
    const rect = _container.getBoundingClientRect();
    const midX = (e.touches[0].clientX + e.touches[1].clientX) / 2;
    const anchorViewportX = midX - rect.left;
    const anchorTimeHours = (_container.scrollLeft + anchorViewportX) / _pixelsPerHour;

    if (_dotNetRef) {
        _dotNetRef.invokeMethodAsync('OnZoomRequested', direction, anchorTimeHours, anchorViewportX);
    }
}

function onContextMenu(e) {
    if (_isDragging || _barDragActive || e.button === 2) {
        e.preventDefault();
    }
}

// ── Bar Drag-to-Reschedule ──────────────────────────────────────────────────

const BAR_DRAG_THRESHOLD = 5; // px before drag activates

function onMouseDown(e) {
    if (_disposed || !isAlive()) return;

    // Check if clicking on a draggable bar (left button only)
    if (e.button === 0) {
        const bar = e.target.closest('[data-bar-type]');
        if (bar) {
            e.preventDefault();
            const wrapper = bar.closest('.gantt-build-bar-wrapper') || bar;
            _barDragEl = wrapper;
            _barDragType = bar.dataset.barType;
            _barDragEntityId = bar.dataset.execId || bar.dataset.programId;
            _barDragOrigLeft = parseFloat(wrapper.style.left) || 0;
            _barDragStartX = e.clientX;
            _barDragActive = false;
            return;
        }
    }

    // Middle/right button = pan
    if (e.button === 1 || e.button === 2) {
        e.preventDefault();
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

        // Activate drag after threshold
        if (!_barDragActive && Math.abs(deltaX) > BAR_DRAG_THRESHOLD) {
            _barDragActive = true;
            _barDragEl.style.opacity = '0.7';
            _barDragEl.style.zIndex = '50';
            _barDragEl.classList.add('gantt-bar-dragging');
            document.body.style.cursor = 'grabbing';

            // Create ghost tooltip
            _barDragGhost = document.createElement('div');
            _barDragGhost.className = 'gantt-drag-ghost';
            _barDragEl.parentElement.appendChild(_barDragGhost);
        }

        if (_barDragActive) {
            const newLeft = _barDragOrigLeft + deltaX;
            _barDragEl.style.left = newLeft + 'px';

            // Update ghost with proposed time
            if (_barDragGhost) {
                const timeMs = _dataStartMs + ((_container.scrollLeft + (e.clientX - _container.getBoundingClientRect().left)) / _pixelsPerHour) * 3600000;
                const dt = new Date(timeMs);
                _barDragGhost.textContent = dt.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }) + ' ' + dt.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
                _barDragGhost.style.left = newLeft + 'px';
                _barDragGhost.style.top = '-20px';
            }
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
        const finalLeft = parseFloat(_barDragEl.style.left) || 0;
        const newTimeMs = _dataStartMs + (finalLeft / _pixelsPerHour) * 3600000;
        const newTimeIso = new Date(newTimeMs).toISOString();

        // Cleanup visual state
        _barDragEl.style.opacity = '';
        _barDragEl.style.zIndex = '';
        _barDragEl.classList.remove('gantt-bar-dragging');
        document.body.style.cursor = '';
        if (_barDragGhost) {
            _barDragGhost.remove();
            _barDragGhost = null;
        }

        // Restore original position (Blazor will re-render at the correct position after reschedule)
        _barDragEl.style.left = _barDragOrigLeft + 'px';

        // Notify Blazor
        if (_dotNetRef && _barDragEntityId) {
            _dotNetRef.invokeMethodAsync('OnBarDragCompleted', _barDragType, _barDragEntityId, newTimeIso);
        }
    }

    // Reset bar drag state
    _barDragEl = null;
    _barDragType = null;
    _barDragEntityId = null;
    _barDragActive = false;

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
