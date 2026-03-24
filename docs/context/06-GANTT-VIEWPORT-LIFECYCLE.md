# 06 — GanttViewport JS Interop Lifecycle

## Overview

`GanttViewport.razor` is the scroll/zoom container for the scheduler Gantt chart. It pairs a Blazor Server component with a JS module (`wwwroot/js/gantt-viewport.js`) to manage horizontal scrolling, zooming, and time-header rendering. C# owns all data and pixel-scale state; JS owns scroll position and DOM event handling.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  GanttViewport.razor (C# / Blazor)                          │
│  Owns: DataRangeStart/End, PixelsPerHour, time headers,     │
│        bar positions, child content rendering                │
│  Renders: .gantt-container > .gantt-inner > tick marks,      │
│           today line, child rows (via RenderFragment)        │
├─────────────────────────────────────────────────────────────┤
│  gantt-viewport.js (ES module)                               │
│  Owns: scroll position, DOM event listeners, drag state      │
│  Handles: scroll, wheel zoom, pinch zoom, mouse drag-to-pan │
│  Notifies C# via: OnViewportChanged, OnZoomRequested         │
└─────────────────────────────────────────────────────────────┘
```

**Key principle:** C# is authoritative for `PixelsPerHour`. When JS detects a zoom gesture (Ctrl+wheel, pinch), it delegates to C# via `OnZoomRequested`. C# computes the new value, re-renders tick/bar positions, then tells JS to sync its scroll offset in `OnAfterRenderAsync`.

## Lifecycle Phases

### 1. Initialization (first render)

```
OnAfterRenderAsync(firstRender: true)
  → JS.InvokeAsync("import", "./js/gantt-viewport.js")   // load ES module
  → InitJsAsync()
    → jsModule.InvokeVoidAsync("initGanttViewport",
        dotNetRef, containerEl, dataStartIso, dataEndIso, pixelsPerHour)
```

JS binds event listeners to the container, stores the `DotNetObjectReference` for callbacks, and scrolls to "now".

### 2. Scroll / Pan (steady state)

User scrolls (wheel, drag, touch) → JS `onScroll` fires → debounced `notifyViewportChanged()` → calls C# `[JSInvokable] OnViewportChanged(startIso, endIso, pph)`.

C# updates `_viewportStart`/`_viewportEnd`, notifies the parent via `OnViewportBoundsChanged`, and checks whether the user has scrolled near the data range edge (triggers `OnDataRangeExtensionNeeded` for infinite scroll).

### 3. Zoom — Ctrl+Wheel (cursor-anchored)

```
JS onWheel (Ctrl held)
  → calls C# OnZoomRequested(direction, anchorTimeHours, anchorViewportX)
    → C# computes new PixelsPerHour, stores anchor, sets _needsZoomSync
    → fires OnPixelsPerHourChanged → parent updates parameter → re-render

OnAfterRenderAsync (non-firstRender, _needsZoomSync == true)
  → jsModule.InvokeVoidAsync("applyZoomAnchored", pph, anchorTimeHours, anchorViewportX)
    → JS updates inner width + scroll so cursor stays over the same time
```

### 4. Zoom — Button Click (center-anchored)

```
ZoomInAsync() / ZoomOutAsync()
  → jsModule.InvokeVoidAsync("applyZoom", newPph)     // JS adjusts immediately
  → OnPixelsPerHourChanged.InvokeAsync(newPph)         // parent re-renders
```

Button zoom calls JS first (before the Blazor re-render) so scroll position is preserved through the diff. JS saves the center time, updates scale, and restores center.

### 5. Data Refresh (background reload)

When the parent reloads scheduling data, the DOM may be destroyed and recreated by Blazor's diff:

```
Parent calls viewport.SaveStateAsync()
  → JS saveState() → stores scrollTimeMsFromStart + pixelsPerHour

Parent updates data, Blazor re-renders child content

Parent calls viewport.RequestReinit()
  → sets _needsReinit = true

OnAfterRenderAsync (non-firstRender, _needsReinit == true)
  → jsModule.InvokeVoidAsync("reinit", containerEl, dataStartIso, dataEndIso, pph)
    → JS detaches old listeners, binds new container, restores scroll position
```

### 6. Data Range Extension (infinite scroll)

When the viewport nears the edge of loaded data:

```
OnViewportChanged detects: viewportStart < DataRangeStart + 1 day
  → fires OnDataRangeExtensionNeeded
  → parent loads more data, updates DataRangeStart/End

Parent calls viewport.UpdateDataRangeInJsAsync()
  → JS updateDataRange() adjusts inner width + scroll offset for the shifted origin
```

### 7. Disposal (circuit teardown)

```
DisposeAsync()
  → _disposed = true                          // guard all future calls
  → jsModule.InvokeVoidAsync("dispose")        // JS detaches listeners, nulls refs
  → jsModule.DisposeAsync()                    // releases the JS module reference
  → _dotNetRef.Dispose()                       // releases the .NET ref so GC can collect
```

## Blazor Server Circuit Safety

In Blazor Server, the SignalR circuit can disconnect at any time (user navigates away, network drop, tab close). When this happens, **any** JS interop call throws `JSDisconnectedException`.

### Rules for all JS interop calls

Every `InvokeAsync` / `InvokeVoidAsync` / `DisposeAsync` call must catch:
- **`JSDisconnectedException`** — circuit already torn down
- **`ObjectDisposedException`** — JS runtime or module already disposed

The `_disposed` flag is set at the top of `DisposeAsync` and checked at the start of every method to short-circuit early. However, the flag alone is insufficient because disposal and other async methods can race.

### Exception handling patterns used

| Context | Catches | Why |
|---|---|---|
| `DisposeAsync` | `JSDisconnectedException`, `ObjectDisposedException` | Circuit may already be gone |
| `OnAfterRenderAsync` zoom/reinit | `JSException` (parent of `JSDisconnectedException`), `ObjectDisposedException` | Logs the message for debugging |
| `InitJsAsync` | `JSException` | Logs init failure; `ObjectDisposedException` caught by caller |
| Public methods (zoom, scroll, pan) | `JSDisconnectedException`, `ObjectDisposedException` | Silently swallowed — the component is going away |

### Anti-patterns to avoid

- **Catching only `ObjectDisposedException`** — misses circuit disconnect (the original bug)
- **Fire-and-forget JS calls** — exceptions go unobserved and crash the circuit
- **JS calls after `_disposed = true`** — always check `_disposed` early in every method

## JS Module State

The JS module uses module-scoped variables (not a class) since only one Gantt viewport exists at a time:

| Variable | Purpose |
|---|---|
| `_dotNetRef` | Reference back to C# for `[JSInvokable]` callbacks |
| `_container` | The `.gantt-container` DOM element |
| `_inner` | The `.gantt-inner` element (sets total width) |
| `_pixelsPerHour` | Local copy of zoom level (C# is authoritative) |
| `_dataStartMs` / `_dataEndMs` | Data range in epoch ms for position math |
| `_savedScrollTimeMsFromStart` | Preserved scroll offset surviving DOM destruction |
| `_isDragging` | Mouse drag-to-pan active flag |

## JS Public API

| Function | Called by | Purpose |
|---|---|---|
| `initGanttViewport(dotNetRef, el, start, end, pph)` | `InitJsAsync` | First-time setup, binds listeners, scrolls to now |
| `reinit(el, start, end, pph)` | `OnAfterRenderAsync` | Rebind after DOM recreation, preserves scroll |
| `dispose()` | `DisposeAsync` | Detach listeners, null all refs |
| `scrollToTime(iso)` | `ScrollToTimeAsync` | Center viewport on a time |
| `applyZoom(pph)` | `ZoomInAsync`/`ZoomOutAsync` | Button zoom — center-anchored |
| `applyZoomAnchored(pph, anchorH, anchorX)` | `OnAfterRenderAsync` | Ctrl+wheel zoom — cursor-anchored |
| `updateDataRange(start, end, pph)` | `UpdateDataRangeInJsAsync` | Adjust after data range extension |
| `saveState()` | `SaveStateAsync` | Snapshot scroll/zoom before refresh |

## C# → JS Callback Flow

| JS Event | C# Method | What happens |
|---|---|---|
| scroll / drag / pan | `OnViewportChanged` | Updates `_viewportStart`/`_viewportEnd`, notifies parent, checks infinite scroll |
| Ctrl+wheel / pinch | `OnZoomRequested` | Computes new PPH, stores anchor, notifies parent to re-render |

## Files

- `Components/Pages/Scheduler/Components/GanttViewport.razor` — Blazor component
- `wwwroot/js/gantt-viewport.js` — ES module (loaded via dynamic import)
- `wwwroot/css/site.css` — Styles for `.gantt-container`, `.gantt-inner`, `.gantt-tick`, `.gantt-today-line`
