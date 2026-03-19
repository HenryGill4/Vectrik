// site.js - OpCentrix shared utilities
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/js/service-worker.js').catch(() => { });
}

// ── Theme management ──
window.opcentrix = window.opcentrix || {};

window.opcentrix.setTheme = function (theme) {
    try { localStorage.setItem('opcentrix-theme', theme); } catch (e) { }
    var effective = theme;
    if (theme === 'system') {
        effective = window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
    }
    document.documentElement.setAttribute('data-theme', effective);
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

// Listen for OS theme changes when set to "system"
try {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function () {
        var saved = localStorage.getItem('opcentrix-theme');
        if (saved === 'system') {
            var effective = window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
            document.documentElement.setAttribute('data-theme', effective);
        }
    });
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
