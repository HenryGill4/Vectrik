// site.js - OpCentrix shared utilities
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/js/service-worker.js').catch(() => { });
}

// ── Theme management ──
window.opcentrix = window.opcentrix || {};

// Guard: true while we are programmatically setting data-theme so the
// MutationObserver knows to ignore the change it just caused.
var _themeApplying = false;

function applyThemeToDOM() {
    try {
        var stored = localStorage.getItem('opcentrix-theme') || 'dark';
        var effective = stored;
        if (stored === 'system') {
            effective = window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
        }
        if (document.documentElement.getAttribute('data-theme') !== effective) {
            _themeApplying = true;
            document.documentElement.setAttribute('data-theme', effective);
        }
    } catch (e) { }
}

window.opcentrix.setTheme = function (theme) {
    try { localStorage.setItem('opcentrix-theme', theme); } catch (e) { }
    applyThemeToDOM();
};

window.opcentrix.getTheme = function () {
    try {
        var saved = localStorage.getItem('opcentrix-theme');
        if (saved) return saved;
    } catch (e) { }
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
};

window.opcentrix.resolveSystemTheme = function () {
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
};

// Re-apply theme after Blazor enhanced navigation resets the DOM.
// Primary: Blazor JS API (documented for .NET 8+)
if (typeof Blazor !== 'undefined' && typeof Blazor.addEventListener === 'function') {
    Blazor.addEventListener('enhancedload', applyThemeToDOM);
}
// Secondary: document-level custom event (some Blazor versions dispatch this)
document.addEventListener('blazor:enhancedload', applyThemeToDOM);

// Tertiary: MutationObserver catches ANY external data-theme reset that the
// event listeners above might miss (e.g. DOM patching before events fire).
try {
    new MutationObserver(function () {
        if (_themeApplying) { _themeApplying = false; return; }
        applyThemeToDOM();
    }).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
} catch (e) { }

// Re-apply theme when OS preference changes and user has "system" selected
try {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', applyThemeToDOM);
} catch (e) { }

// Print rendered HTML in a new window
window.opcentrix.printHtml = function (html, title) {
    var win = window.open('', '_blank', 'width=900,height=700');
    win.document.write('<!DOCTYPE html><html><head><title>' + (title || 'Print') + '</title>');
    win.document.write('<style>body{font-family:Arial,Helvetica,sans-serif;margin:20px;color:#1a1a1a}table{width:100%;border-collapse:collapse;margin:12px 0}th,td{border:1px solid #ccc;padding:6px 10px;text-align:left;font-size:0.9rem}th{background:#f5f5f5;font-weight:600}.header{margin-bottom:20px}.totals{margin-top:16px}.sign-line{border-bottom:1px solid #333;width:200px;display:inline-block;margin:8px 16px 8px 0}@media print{body{margin:0}}</style>');
    win.document.write('</head><body>');
    win.document.write(html);
    win.document.write('</body></html>');
    win.document.close();
    win.focus();
    win.print();
};

// ── Scheduler zoom (mouse wheel + touch pinch) ──
window.opcentrix.initSchedulerZoom = function (dotNetRef, element) {
    if (!element || element._zoomInit) return;
    element._zoomInit = true;
    var lastWheel = 0;
    var WHEEL_MS = 180;

    function getLabelW() {
        var g = element.querySelector('.gantt-container');
        return g ? parseInt(getComputedStyle(g).getPropertyValue('--gantt-label-w')) || 180 : 180;
    }

    element.addEventListener('wheel', function (e) {
        var gantt = element.querySelector('.gantt-container');
        if (!gantt || !gantt.contains(e.target)) return;

        // Shift+wheel = horizontal scroll (pan through days)
        if (e.shiftKey) {
            var card = e.target.closest('.sched-gantt-card');
            if (card) {
                e.preventDefault();
                card.scrollLeft += e.deltaY * 2;
            }
            return;
        }

        var labelW = getLabelW();
        var rect = gantt.getBoundingClientRect();
        if (e.clientX - rect.left < labelW) return;
        var now = Date.now();
        if (now - lastWheel < WHEEL_MS) { e.preventDefault(); return; }
        e.preventDefault();
        lastWheel = now;
        var trackW = rect.width - labelW;
        var pct = Math.max(0, Math.min(1, (e.clientX - rect.left - labelW) / trackW));
        if (e.deltaY < 0) {
            dotNetRef.invokeMethodAsync('JsZoomIn', pct);
        } else {
            dotNetRef.invokeMethodAsync('JsZoomOut', pct);
        }
    }, { passive: false });

    var pinchDist = 0;
    var lastPinch = 0;
    var PINCH_MS = 300;
    var PINCH_THRESHOLD = 40;

    element.addEventListener('touchstart', function (e) {
        if (e.touches.length === 2) {
            pinchDist = touchDist(e.touches);
        }
    }, { passive: true });

    element.addEventListener('touchmove', function (e) {
        if (e.touches.length !== 2) return;
        var gantt = element.querySelector('.gantt-container');
        if (!gantt) return;
        var cur = touchDist(e.touches);
        var delta = cur - pinchDist;
        if (Math.abs(delta) < PINCH_THRESHOLD) return;
        var now = Date.now();
        if (now - lastPinch < PINCH_MS) return;
        e.preventDefault();
        lastPinch = now;
        pinchDist = cur;
        var rect = gantt.getBoundingClientRect();
        var midX = (e.touches[0].clientX + e.touches[1].clientX) / 2;
        var labelW = getLabelW();
        var trackW = rect.width - labelW;
        var pct = Math.max(0, Math.min(1, (midX - rect.left - labelW) / trackW));
        if (delta > 0) {
            dotNetRef.invokeMethodAsync('JsZoomIn', pct);
        } else {
            dotNetRef.invokeMethodAsync('JsZoomOut', pct);
        }
    }, { passive: false });

    function touchDist(t) {
        var dx = t[0].clientX - t[1].clientX;
        var dy = t[0].clientY - t[1].clientY;
        return Math.sqrt(dx * dx + dy * dy);
    }

    // ── Drag to pan (click-and-drag to scroll horizontally) ──
    var drag = { active: false, startX: 0, scrollLeft: 0, moved: false, card: null };

    element.addEventListener('mousedown', function (e) {
        var card = e.target.closest('.sched-gantt-card');
        if (!card || e.target.closest('.gantt-bar') || e.button !== 0) return;
        drag.active = true;
        drag.moved = false;
        drag.card = card;
        drag.startX = e.pageX;
        drag.scrollLeft = card.scrollLeft;
        card.style.cursor = 'grabbing';
        card.style.userSelect = 'none';
    });

    document.addEventListener('mousemove', function (e) {
        if (!drag.active) return;
        var walk = e.pageX - drag.startX;
        if (Math.abs(walk) > 3) drag.moved = true;
        if (drag.moved) {
            e.preventDefault();
            drag.card.scrollLeft = drag.scrollLeft - walk;
        }
    });

    document.addEventListener('mouseup', function () {
        if (!drag.active) return;
        if (drag.card) {
            drag.card.style.cursor = 'grab';
            drag.card.style.userSelect = '';
        }
        drag.active = false;
    });
};

/* ── Debug Feedback Tool ── */
window.opcentrix.debugFab = {
    isEnabled: function () {
        try { return localStorage.getItem('opcentrix-debug-fab') === 'true'; }
        catch (e) { return false; }
    },
    setEnabled: function (on) {
        try { localStorage.setItem('opcentrix-debug-fab', on ? 'true' : 'false'); }
        catch (e) { }
    },
    getContext: function () {
        var w = window.innerWidth;
        var breakpoint = w < 480 ? 'mobile' : w < 768 ? 'mobile-landscape' : w < 1024 ? 'tablet' : w < 1440 ? 'desktop' : 'wide';
        return {
            viewport: w + 'x' + window.innerHeight,
            breakpoint: breakpoint,
            theme: document.documentElement.getAttribute('data-theme') || 'dark',
            pageTitle: document.title || '',
            scrollY: String(Math.round(window.scrollY)),
            pixelRatio: String(window.devicePixelRatio || 1),
            userAgent: navigator.userAgent
        };
    },
    copyText: function (text) {
        return navigator.clipboard.writeText(text);
    }
};
