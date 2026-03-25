// Scheduler utility functions — ES module for safe JS interop

/**
 * Triggers a CSV download from a base64-encoded string.
 * Replaces unsafe eval() usage for client-side file downloads.
 */
export function downloadCsv(base64, filename) {
    const a = document.createElement('a');
    a.href = 'data:text/csv;base64,' + base64;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
}
