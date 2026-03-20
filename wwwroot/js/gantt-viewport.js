// gantt-viewport.js — Infinite scroll + fluid zoom for the scheduler Gantt chart.
// Loaded as an ES module from Blazor via IJSRuntime.InvokeAsync<IJSObjectReference>.

let _dotNetRef = null;
let _container = null;
let _inner = null;
let _pixelsPerHour = 6.0;
let _dataStartIso = null;   // ISO string of the data range start (set from Blazor)
let _dataStartMs = 0;       // same in epoch ms for fast math
let _debounceId = 0;
let _disposed = false;

const MIN_PX_PER_HOUR = 0.5;
const MAX_PX_PER_HOUR = 120;
const ZOOM_FACTOR = 1.15;
const DEBOUNCE_MS = 150;

// ── Public API ──────────────────────────────────────────────────────────────

export function initGanttViewport(dotNetRef, containerEl, dataStartIso, dataEndIso, pixelsPerHour) {
    _dotNetRef = dotNetRef;
    _container = containerEl;
    _pixelsPerHour = pixelsPerHour || 6.0;
    _dataStartIso = dataStartIso;
    _dataStartMs = new Date(dataStartIso).getTime();

    // The inner div is the first child with class 'gantt-inner'
    _inner = _container.querySelector('.gantt-inner');

    // Set initial inner width
    const dataEndMs = new Date(dataEndIso).getTime();
    const totalHours = (dataEndMs - _dataStartMs) / 3600000;
    if (_inner) {
        _inner.style.width = (totalHours * _pixelsPerHour) + 'px';
    }

    _container.addEventListener('scroll', onScroll, { passive: true });
    _container.addEventListener('wheel', onWheel, { passive: false });

    // Touch pinch zoom
    _container.addEventListener('touchstart', onTouchStart, { passive: true });
    _container.addEventListener('touchmove', onTouchMove, { passive: false });

    _disposed = false;

    // Initial scroll to "now" area
    scrollToTime(new Date().toISOString());
}

export function dispose() {
    _disposed = true;
    if (_container) {
        _container.removeEventListener('scroll', onScroll);
        _container.removeEventListener('wheel', onWheel);
        _container.removeEventListener('touchstart', onTouchStart);
        _container.removeEventListener('touchmove', onTouchMove);
    }
    _dotNetRef = null;
    _container = null;
    _inner = null;
}

export function scrollToTime(isoDateTimeString) {
    if (!_container || !_inner) return;
    const targetMs = new Date(isoDateTimeString).getTime();
    const hoursFromStart = (targetMs - _dataStartMs) / 3600000;
    const px = hoursFromStart * _pixelsPerHour;
    // Center the target in the viewport
    const scrollTarget = px - _container.clientWidth / 2;
    _container.scrollLeft = Math.max(0, scrollTarget);
}

export function setZoom(pixelsPerHour) {
    if (!_container || !_inner) return;
    _pixelsPerHour = clampZoom(pixelsPerHour);
    updateInnerWidth();
    notifyViewportChanged();
}

export function getViewport() {
    if (!_container) return { startIso: _dataStartIso, endIso: _dataStartIso, pixelsPerHour: _pixelsPerHour };
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
    const oldStartMs = _dataStartMs;
    _dataStartIso = dataStartIso;
    _dataStartMs = new Date(dataStartIso).getTime();
    _pixelsPerHour = pixelsPerHour || _pixelsPerHour;

    const dataEndMs = new Date(dataEndIso).getTime();
    const totalHours = (dataEndMs - _dataStartMs) / 3600000;
    if (_inner) {
        _inner.style.width = (totalHours * _pixelsPerHour) + 'px';
    }

    // Adjust scroll position so the same time stays visible
    if (_container && oldStartMs !== _dataStartMs) {
        const shiftHours = (oldStartMs - _dataStartMs) / 3600000;
        _container.scrollLeft += shiftHours * _pixelsPerHour;
    }
}

// ── Event Handlers ──────────────────────────────────────────────────────────

function onScroll() {
    if (_disposed) return;
    debouncedNotify();
}

function onWheel(e) {
    if (_disposed || !_container || !_inner) return;

    // Only zoom on Ctrl+wheel (or pinch gesture which browsers map to ctrlKey)
    if (!e.ctrlKey) return;

    e.preventDefault();

    // Cursor position relative to the container's scrolled content
    const rect = _container.getBoundingClientRect();
    const cursorX = e.clientX - rect.left + _container.scrollLeft;
    const cursorTimeHours = cursorX / _pixelsPerHour;

    // Apply zoom
    const oldPxPerHour = _pixelsPerHour;
    if (e.deltaY < 0) {
        _pixelsPerHour = clampZoom(_pixelsPerHour * ZOOM_FACTOR);
    } else {
        _pixelsPerHour = clampZoom(_pixelsPerHour / ZOOM_FACTOR);
    }

    if (_pixelsPerHour === oldPxPerHour) return;

    // Update inner width
    updateInnerWidth();

    // Adjust scroll so cursor stays over the same time
    const newCursorX = cursorTimeHours * _pixelsPerHour;
    _container.scrollLeft = newCursorX - (e.clientX - rect.left);

    debouncedNotify();
}

let _pinchDist = 0;

function onTouchStart(e) {
    if (e.touches.length === 2) {
        _pinchDist = touchDist(e.touches);
    }
}

function onTouchMove(e) {
    if (_disposed || !_container || !_inner || e.touches.length !== 2) return;

    const cur = touchDist(e.touches);
    const delta = cur - _pinchDist;
    if (Math.abs(delta) < 20) return;

    e.preventDefault();
    _pinchDist = cur;

    // Pinch center in scroll coordinates
    const rect = _container.getBoundingClientRect();
    const midX = (e.touches[0].clientX + e.touches[1].clientX) / 2;
    const cursorX = midX - rect.left + _container.scrollLeft;
    const cursorTimeHours = cursorX / _pixelsPerHour;

    const oldPxPerHour = _pixelsPerHour;
    if (delta > 0) {
        _pixelsPerHour = clampZoom(_pixelsPerHour * ZOOM_FACTOR);
    } else {
        _pixelsPerHour = clampZoom(_pixelsPerHour / ZOOM_FACTOR);
    }

    if (_pixelsPerHour === oldPxPerHour) return;

    updateInnerWidth();
    _container.scrollLeft = cursorTimeHours * _pixelsPerHour - (midX - rect.left);

    debouncedNotify();
}

// ── Helpers ─────────────────────────────────────────────────────────────────

function clampZoom(v) {
    return Math.max(MIN_PX_PER_HOUR, Math.min(MAX_PX_PER_HOUR, v));
}

function updateInnerWidth() {
    if (!_inner) return;
    const currentWidthHours = parseFloat(_inner.style.width) || 0;
    // Recalculate from data range stored in the inner's data attribute
    const dataEndIso = _inner.dataset.dataEnd;
    if (dataEndIso) {
        const dataEndMs = new Date(dataEndIso).getTime();
        const totalHours = (dataEndMs - _dataStartMs) / 3600000;
        _inner.style.width = (totalHours * _pixelsPerHour) + 'px';
    }
}

function notifyViewportChanged() {
    if (_disposed || !_dotNetRef || !_container) return;
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
