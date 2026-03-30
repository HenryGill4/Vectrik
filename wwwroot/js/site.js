// site.js - Vectrik shared utilities
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/js/service-worker.js').catch(() => { });
}

// Change password API — called from Blazor interactive ChangePassword page
window.changePasswordApi = async function (currentPassword, newPassword) {
    try {
        var formData = new URLSearchParams();
        formData.append('currentPassword', currentPassword);
        formData.append('newPassword', newPassword);
        var response = await fetch('/api/account/change-password', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: formData.toString(),
            credentials: 'same-origin'
        });
        if (response.ok) {
            return { success: true };
        }
        var data = await response.json().catch(function () { return {}; });
        return { success: false, error: data.error || 'Failed to change password.' };
    } catch (e) {
        return { success: false, error: e.message || 'Network error.' };
    }
};

// ── Theme management ──
window.vectrik = window.vectrik || {};
window.opcentrix = window.vectrik; // backwards compat

// Guard: true while we are programmatically setting data-theme so the
// MutationObserver knows to ignore the change it just caused.
var _themeApplying = false;

function applyThemeToDOM() {
    try {
        var stored = localStorage.getItem('vectrik-theme') || localStorage.getItem('opcentrix-theme') || 'dark';
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

window.vectrik.setTheme = function (theme) {
    try { localStorage.setItem('vectrik-theme', theme); } catch (e) { }
    applyThemeToDOM();
};

window.vectrik.getTheme = function () {
    try {
        var saved = localStorage.getItem('vectrik-theme') || localStorage.getItem('opcentrix-theme');
        if (saved) return saved;
    } catch (e) { }
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
};

window.vectrik.resolveSystemTheme = function () {
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
window.vectrik.printHtml = function (html, title) {
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

/* ── Debug Feedback Tool ── */
window.vectrik.debugFab = {
    isEnabled: function () {
        try { return localStorage.getItem('vectrik-debug-fab') === 'true'; }
        catch (e) { return false; }
    },
    setEnabled: function (on) {
        try { localStorage.setItem('vectrik-debug-fab', on ? 'true' : 'false'); }
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

// ── Navigation loading overlay ──
(function () {
    var overlay = null;
    var timer = null;
    var minTimer = null;
    var showing = false;
    // Minimum display time so the overlay doesn't just flash for 1 frame
    var MIN_SHOW_MS = 250;
    var showTime = 0;

    function getOverlay() {
        if (!overlay) overlay = document.getElementById('nav-loading');
        return overlay;
    }

    window.vectrik.navLoading = {
        show: function () {
            var el = getOverlay();
            if (!el || showing) return;
            showing = true;
            showTime = Date.now();
            el.classList.add('nav-loading-active');
            // Safety timeout — hide after 5s no matter what
            clearTimeout(timer);
            timer = setTimeout(function () { window.vectrik.navLoading.hide(); }, 5000);
        },
        hide: function () {
            var el = getOverlay();
            if (!el || !showing) return;
            // Enforce minimum display time so fast navigations still show a brief flash
            var elapsed = Date.now() - showTime;
            if (elapsed < MIN_SHOW_MS) {
                clearTimeout(minTimer);
                minTimer = setTimeout(function () { window.vectrik.navLoading.hide(); }, MIN_SHOW_MS - elapsed);
                return;
            }
            showing = false;
            clearTimeout(timer);
            clearTimeout(minTimer);
            el.classList.remove('nav-loading-active');
        }
    };

    // Auto-hide on Blazor enhanced navigation completion
    if (typeof Blazor !== 'undefined' && Blazor.addEventListener) {
        Blazor.addEventListener('enhancedload', function () { window.vectrik.navLoading.hide(); });
    }
    document.addEventListener('blazor:enhancedload', function () { window.vectrik.navLoading.hide(); });

    // Intercept sidebar nav clicks to show overlay instantly (before Blazor round-trip)
    document.addEventListener('click', function (e) {
        var link = e.target.closest('.sidebar-nav a[href], .nav-item[href]');
        if (link && link.getAttribute('href') && !link.getAttribute('href').startsWith('#')) {
            window.vectrik.navLoading.show();
        }
    }, true);
})();

// ── Vectrik public site utilities ──
window.vectrik = window.vectrik || {};

window.vectrik.toggleMobileMenu = function () {
    var btn = document.getElementById('pub-hamburger-btn');
    var menu = document.getElementById('pub-mobile-menu');
    if (!btn || !menu) return;
    var isOpen = menu.classList.toggle('pub-nav-links-open');
    btn.classList.toggle('pub-hamburger-open', isOpen);
};

window.vectrik.initPublicNav = function () {
    var nav = document.querySelector('.pub-nav');
    if (!nav) return;
    var update = function () {
        if (window.scrollY > 20) {
            nav.classList.add('pub-nav-scrolled');
        } else {
            nav.classList.remove('pub-nav-scrolled');
        }
    };
    window.addEventListener('scroll', update, { passive: true });
    update();
};

window.vectrik.initScrollReveal = function () {
    var elements = document.querySelectorAll('.pub-fade-up');
    if (!elements.length) return;
    if (!('IntersectionObserver' in window)) {
        elements.forEach(function (el) { el.classList.add('pub-visible'); });
        return;
    }
    var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('pub-visible');
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.1, rootMargin: '0px 0px -40px 0px' });
    elements.forEach(function (el) { observer.observe(el); });
};

// Auto-init for SSR pages and Blazor enhanced navigation
function vectrikAutoInit() {
    if (document.querySelector('.pub-layout')) {
        window.vectrik.initPublicNav();
        window.vectrik.initScrollReveal();
    }
}
document.addEventListener('DOMContentLoaded', vectrikAutoInit);
// Blazor enhanced navigation re-renders without DOMContentLoaded
if (typeof Blazor !== 'undefined' && Blazor.addEventListener) {
    Blazor.addEventListener('enhancedload', vectrikAutoInit);
} else {
    // Blazor not ready yet — listen for it
    document.addEventListener('blazor:enhancedload', vectrikAutoInit);
}
