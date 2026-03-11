# Sprint 11: PWA + Mobile Polish

> **Status**: NOT STARTED
> **Goal**: Production-grade on iPad with offline shell, bottom nav, and touch gestures.
> **Depends on**: All previous sprints (this is polish)

---

## Tasks

```
[ ] 11.1  Service worker — cache app shell (HTML, CSS, JS, icons)
[ ] 11.2  Service worker — network-first strategy for API/data calls
[ ] 11.3  Offline indicator banner — show "You are offline" when disconnected
[ ] 11.4  Bottom nav on mobile — replaces sidebar on screens < 768px
[ ] 11.5  Bottom nav — Dashboard, Shop Floor, Tracker, Admin icons
[ ] 11.6  Sidebar collapse — hamburger menu on tablet, hidden on phone
[ ] 11.7  Touch gestures — swipe right on stage queue item to start work
[ ] 11.8  Touch gestures — swipe left on active work to log delay
[ ] 11.9  Form inputs — all number inputs use inputmode="numeric"
[ ] 11.10 Form inputs — all text inputs use appropriate inputmode
[ ] 11.11 Tap targets — verify all buttons/links are ≥ 44×44px
[ ] 11.12 PWA install prompt — "Add to Home Screen" banner
[ ] 11.13 iOS safe area — respect notch/home indicator insets
[ ] 11.14 Test on iPad Safari — full flow works
[ ] 11.15 Test on iPhone Safari — responsive layout works
```

---

## Acceptance Criteria

- App installable on iPad/iPhone home screen
- App shell loads even without network
- Data requests show "offline" message gracefully
- Bottom nav works on mobile with 4 main sections
- All tap targets ≥ 44×44px (Apple HIG)
- Swipe gestures work on shop floor
- Number inputs show numeric keyboard on iPad
- No hover-only interactions anywhere

## Files to Touch

- `wwwroot/js/service-worker.js` — real caching logic
- `wwwroot/js/site.js` — install prompt, gesture handling
- `wwwroot/css/site.css` — bottom nav, responsive breakpoints, safe areas
- `Components/Layout/MainLayout.razor` — mobile layout variant
- `Components/Layout/NavMenu.razor` — collapse behavior
- All shop floor pages — gesture support
