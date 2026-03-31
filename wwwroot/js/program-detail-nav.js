// Program detail panel — section navigation via IntersectionObserver
let observer = null;
let dotnetRef = null;
let container = null;

export function init(detailEl, ref) {
    container = detailEl;
    dotnetRef = ref;

    const sections = detailEl.querySelectorAll('[id^="prg-section-"]');
    if (!sections.length) return;

    observer = new IntersectionObserver(
        (entries) => {
            // Find the top-most visible section
            let best = null;
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    if (!best || entry.boundingClientRect.top < best.boundingClientRect.top) {
                        best = entry;
                    }
                }
            }
            if (best && dotnetRef) {
                dotnetRef.invokeMethodAsync('OnSectionVisible', best.target.id);
            }
        },
        {
            root: detailEl,
            rootMargin: '-10% 0px -60% 0px',
            threshold: 0.01
        }
    );

    sections.forEach(s => observer.observe(s));
}

export function scrollTo(sectionId) {
    const el = document.getElementById(sectionId);
    if (!el || !container) return;

    el.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

export function dispose() {
    if (observer) {
        observer.disconnect();
        observer = null;
    }
    dotnetRef = null;
    container = null;
}
