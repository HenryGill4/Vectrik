// site.js - OpCentrix shared utilities
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/js/service-worker.js').catch(() => { });
}
