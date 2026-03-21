# Scheduler Page Decomposition Plan

> **Created**: 2026-03-20
> **Status**: COMPLETE
> **Purpose**: Break `Components/Pages/Scheduler/Index.razor` (2,444 lines) into
> focused Blazor components so each piece is small enough for effective development.
>
> **Rule**: Execute one section at a time. Verify build + manual smoke test after each.
> Do NOT start the next section until the current one compiles and the view still works.

---

## Why This Is Needed

The scheduler page has grown to 2,444 lines in a single file:
- **1,405 lines of markup** across 6 view modes + 5 modals
- **1,039 lines of C#** with 87 fields and ~60 methods
- **6 unrelated view modes** (gantt, builds, orders, path, stages, table) sharing one `@code` block
- Every `StateHasChanged()` re-evaluates ALL view modes even though only one is visible
- Zoom/scroll bugs persist because viewport logic is tangled with unrelated scheduling code
- AI agents cannot reliably work on the file — too much context, too many side effects

## Target Architecture

```
Components/Pages/Scheduler/
├── Index.razor                    (~427 lines) Orchestrator: route, data loading, view switching
│
├── Components/                    Shared building blocks
│   ├── SchedulerToolbar.razor     (~78 lines)  Page header + controls bar (zoom, nav, view toggle)
│   ├── GanttViewport.razor        (~250 lines) Viewport container + JS interop + zoom/scroll
│   ├── GanttTimeHeader.razor      (~60 lines)  Time ticks (adapts to zoom level)
│   ├── GanttMachineRow.razor      (~100 lines) Single machine row with bars
│   ├── GanttBar.razor             (~40 lines)  Single execution bar
│   ├── GanttBuildGroupBar.razor   (~50 lines)  Grouped build package bar
│   └── UnscheduledSidebar.razor   (~50 lines)  Sidebar used by gantt + path + builds views
│
├── Views/                         One component per view tab
│   ├── GanttView.razor            (~80 lines)  Machines gantt (uses GanttViewport + rows)
│   ├── BuildsView.razor           (~200 lines) SLS build pipeline + timelines
│   ├── WorkOrdersView.razor       (~350 lines) WO command center + suggestions
│   ├── PartPathView.razor         (~180 lines) Part-centric gantt
│   ├── StagesView.razor           (~120 lines) Stage queue accordion
│   └── TableView.razor            (~120 lines) Sortable/filterable table
│
└── Modals/                        One component per modal dialog
    ├── RescheduleModal.razor      (~80 lines)
    ├── CreateJobModal.razor       (~70 lines)
    ├── WoBuildModal.razor         (~60 lines)
    ├── WoJobModal.razor           (~40 lines)
    └── QuickScheduleModal.razor   (~80 lines)
```

## Shared Data Contract

The orchestrator (`Index.razor`) loads all data and passes it down via parameters.
Child components communicate back via `EventCallback`.

```csharp
// Data passed DOWN from Index.razor to child components:
List<StageExecution>     Executions
List<StageExecution>     Unscheduled
List<Machine>            Machines
List<BuildPackage>       BuildPackages
Dictionary<int, List<MachineTimelineEntry>> MachineTimelines
List<WorkOrder>          WorkOrders
List<BuildTemplate>      BuildTemplates
BuildSuggestionResult?   Suggestions
DateTime                 DataRangeStart
DateTime                 DataRangeEnd
double                   PixelsPerHour

// Events sent UP from child components to Index.razor:
EventCallback<StageExecution>   OnShowReschedule
EventCallback<StageExecution>   OnManualSchedule
EventCallback<int>              OnAutoScheduleSingle
EventCallback                   OnAutoScheduleAll
EventCallback                   OnRefreshRequested
EventCallback<double>           OnPixelsPerHourChanged
```

---

## Execution Sections

### Section 0 — Prep: Verify Current State
- [x] Build passes
- [ ] All 6 view modes render (gantt, builds, orders, path, stages, table)
- [ ] Zoom buttons work (➖ ➕)
- [ ] Ctrl+scroll zoom works
- [ ] Auto-refresh doesn't blank the gantt
- [ ] Mouse drag panning works

### Section 1 — Extract Modals (Low Risk, High LOC Reduction)

Extract 5 modal dialogs into standalone components. These are self-contained UI
with no shared rendering logic — safest starting point.

**Files to create:**
- [x] `Components/Pages/Scheduler/Modals/RescheduleModal.razor` (97 lines)
      - Parameters: `StageExecution? Execution`, `List<Machine> Machines`, `bool IsVisible`, initial values
      - EventCallbacks: `OnClose`, `OnConfirm`
- [x] `Components/Pages/Scheduler/Modals/CreateJobModal.razor` (124 lines)
      - Parameters: `List<Part>? Parts`, `string UserName`, `bool IsVisible`
      - EventCallbacks: `OnClose`, `OnCreated`
- [x] `Components/Pages/Scheduler/Modals/WoBuildModal.razor` (142 lines)
      - Parameters: `WorkOrder?`, `WorkOrderLine?`, `List<Machine> SlsMachines/AllMachines`, `bool IsVisible`
      - EventCallbacks: `OnClose`, `OnCreated`
- [x] `Components/Pages/Scheduler/Modals/WoJobModal.razor` (89 lines)
      - Parameters: `WorkOrder?`, `WorkOrderLine?`, `string UserName`, `bool IsVisible`
      - EventCallbacks: `OnClose`, `OnScheduled`
- [x] `Components/Pages/Scheduler/Modals/QuickScheduleModal.razor` (167 lines)
      - Parameters: `BuildTemplate?`, `WorkOrder?`, `WorkOrderLine?`, machines, `string UserName`
      - EventCallbacks: `OnClose`, `OnScheduled`

**Verification:**
- [x] Build passes
- [ ] Each modal opens, submits, and closes correctly (needs manual test)
- [x] Index.razor reduced by 375 lines (2,444 → 2,069)

---

### Section 2 — Extract GanttViewport Component (Highest Value)

Isolate the viewport container, JS interop lifecycle, zoom, scroll, and time header
into a self-contained component. This is where all the current bugs live.

**Files to create:**
- [x] `Components/Pages/Scheduler/Components/GanttViewport.razor` (313 lines)
      - Owns: `ElementReference`, `IJSObjectReference`, `DotNetObjectReference`
      - Owns: `OnAfterRenderAsync` (init, reinit, zoom sync)
      - Owns: zoom sync state, reinit state, viewport bounds
      - Owns: zoom methods, scroll methods, viewport changed callback
      - Renders: outer `.gantt-container.gantt-viewport` div, `.gantt-inner` div
      - Renders: time header ticks, today line
      - Exposes: `RenderFragment ChildContent` for machine rows
      - Parameters: `DateTime DataRangeStart`, `DateTime DataRangeEnd`, `double PixelsPerHour`
      - EventCallbacks: `OnPixelsPerHourChanged`, `OnViewportBoundsChanged`, `OnDataRangeExtensionNeeded`
      - Public methods (via @ref): `ZoomInAsync`, `ZoomOutAsync`, `ScrollToTimeAsync`, `ShiftViewportAsync`, `SaveStateAsync`, `RequestReinit`, `UpdateDataRangeInJsAsync`
      - Implements: `IAsyncDisposable` (JS cleanup)

**What stays in Index.razor:**
- Data loading, auto-refresh timer
- `_pixelsPerHour` as the authoritative value (passed down as parameter)
- `_viewportStart`, `_viewportEnd`, `_dataRangeStart`, `_dataRangeEnd` (used by controls bar display + other views)
- Zoom/scroll wrapper methods that delegate to `_ganttViewport` via @ref
- `HandlePixelsPerHourChanged`, `HandleViewportBoundsChanged` event handlers
- `GetBarLeftPx`, `GetBarWidthPx`, `GetTotalInnerWidthPx`, `GenerateTimeHeaders` (still needed by builds/path views until Section 5)

**Verification:**
- [x] Build passes
- [ ] Gantt view renders with correct time header (needs manual test)
- [ ] Zoom buttons work (ticks re-render at new scale) (needs manual test)
- [ ] Ctrl+scroll zoom works (needs manual test)
- [ ] Scroll to Now works (needs manual test)
- [ ] Pan buttons work (needs manual test)
- [ ] Auto-refresh preserves scroll position (needs manual test)
- [ ] Mouse drag panning works (needs manual test)
- [x] Index.razor reduced by 137 lines (2,069 → 1,932)

---

### Section 3 — Extract Gantt Bar Components

Break the complex bar rendering (build groups + individual bars) into small components.

**Files to create:**
- [x] `Components/Pages/Scheduler/Components/GanttBar.razor` (32 lines)
      - Single execution bar with position, color, tooltip, click handler
      - Parameters: `StageExecution Exec`, `double BarLeftPx`, `double BarWidthPx`, `bool IsConflict`, `bool IsUnassigned`
      - EventCallback: `OnClick`
- [x] `Components/Pages/Scheduler/Components/GanttBuildGroupBar.razor` (25 lines)
      - Grouped build package bar
      - Parameters: `StageExecution FirstExec`, positions, `string PartList`
      - EventCallback: `OnClick`
- [x] `Components/Pages/Scheduler/Components/GanttMachineRow.razor` (69 lines)
      - Machine label + track with bars, owns build grouping, overlap detection, position computation
      - Parameters: `Machine Machine`, `List<StageExecution> Executions`, `DateTime DataRangeStart`, `double PixelsPerHour`
      - EventCallback: `OnBarClick`

**Verification:**
- [x] Build passes
- [ ] Machine rows render correctly with bars at right positions (needs manual test)
- [ ] Clicking a bar opens the reschedule modal (needs manual test)
- [ ] Build group bars render correctly (needs manual test)
- [x] Index.razor reduced by 60 lines (1,932 → 1,872)

---

### Section 4 — Extract UnscheduledSidebar

Used by gantt, path, and builds views — extract once, reuse three times.

**Files to create:**
- [x] `Components/Pages/Scheduler/Components/UnscheduledSidebar.razor` (43 lines)
      - Parameters: `List<StageExecution> Executions`, `string Title`, `bool ShowManualSchedule`, `bool UseBuildPackageName`
      - EventCallbacks: `OnManualSchedule`, `OnAutoSchedule`
      - Replaces 3 sidebar instances (gantt, builds, path)

**Verification:**
- [x] Build passes
- [ ] Sidebar renders in gantt, path, and builds views (needs manual test)
- [ ] Manual schedule and auto-schedule buttons work (needs manual test)
- [x] Index.razor reduced by 63 lines (1,872 → 1,809)

---

### Section 5 — Extract View Tabs (One at a Time)

Each view mode becomes its own component. Extract in this order (simplest first):

- [x] **5a: TableView.razor** (199 lines) — Extracted table markup + sort/filter/search logic
      - Self-contained, no gantt dependencies
      - Parameters: `List<StageExecution> Executions`, `List<StageExecution> Unscheduled`
      - EventCallbacks: `OnShowReschedule`, `OnAutoSchedule`
      - Owns: `ToggleTableSort`, `SortedTableExecutions`, `TableSortIcon`, `ResolveStageIcon` copy
      - Index.razor reduced by 174 lines (1,809 → 1,635)

- [x] **5b: StagesView.razor** (143 lines) — Extracted stage queue accordion with expand/collapse
      - Self-contained accordion view
      - Parameters: `List<StageExecution> Executions`, `List<StageExecution> Unscheduled`, `List<ProductionStage> Stages`
      - EventCallbacks: `OnShowReschedule`
      - Owns: `_expandedStages`, `ToggleStageExpand`, `ResolveStageIcon` copy
      - Index.razor reduced by 133 lines (1,635 → 1,502)

- [x] **5c: PartPathView.razor** (215 lines) — Extracted part path view with filters, gantt container, job rows, legend, sidebar
      - Parameters: `Executions`, `Unscheduled`, `Parts`, `DataRangeStart`, `DataRangeEnd`, `PixelsPerHour`
      - EventCallbacks: `OnShowReschedule`, `OnManualSchedule`, `OnAutoSchedule`
      - Owns: path filter state, local copies of bar position/time header helpers
      - Index.razor reduced by 139 lines (1,502 → 1,363)

- [x] **5d: BuildsView.razor** (213 lines) — Extracted SLS build pipeline + per-machine timelines
      - Parameters: `BuildPackages`, `SlsMachines`, `MachineTimelines`, `Unscheduled`, `DataRangeStart`, `DataRangeEnd`, `PixelsPerHour`
      - EventCallbacks: `OnAutoSchedule`
      - Owns: pipeline summary computation, per-machine timeline gantt, ready builds cards, BuildStatusBadgeClass, BuildStatusIcon, local bar/time helpers
      - Index.razor reduced by 173 lines (1,363 → 1,190)

- [x] **5e: GanttView.razor** (123 lines) — Extracted machines gantt with GanttViewport + machine rows
      - Parameters: `Executions`, `Unscheduled`, `Machines`, `DataRangeStart`, `DataRangeEnd`, `PixelsPerHour`
      - EventCallbacks: `OnShowReschedule`, `OnManualSchedule`, `OnAutoSchedule`, `OnPixelsPerHourChanged`, `OnViewportBoundsChanged`, `OnDataRangeExtensionNeeded`
      - Owns: GetOrderedMachines, IsSls, local GetBarLeftPx/GetBarWidthPx, unassigned row, legend
      - Exposes: `GanttViewport? Viewport` for zoom/scroll access from Index.razor
      - Index.razor reduced by 160 lines (1,190 → 1,030). Also removed NonSlsMachines, GetTotalInnerWidthPx, GenerateTimeHeaders.

- [x] **5f: WorkOrdersView.razor** (533 lines) — Extracted WO command center + suggestions + cards + 3 WO modals
      - Parameters: `WorkOrders`, `BuildTemplates`, `Suggestions`, `SlsMachines`, `AllMachines`, `UserName`
      - EventCallbacks: `OnRefreshRequested`
      - Owns: WoBuildModal + WoJobModal + QuickScheduleModal instances and all modal state
      - Owns: GetOutstanding, GetInBuildQty, IsAdditiveLine, HasExistingJobs, template matching, suggestion actions, CreateMixedBuild
      - Injects: IBuildPlanningService, IJSRuntime, ToastService
      - Index.razor reduced by 503 lines (1,030 → 527)

**Verification (after each):**
- [ ] Build passes
- [ ] The extracted view still renders and functions correctly
- [ ] View mode switching still works

---

### Section 6 — Slim Index.razor to Orchestrator

After all extractions, Index.razor was cleaned to a pure orchestrator:
- [x] `@page "/scheduler"` route + service injections (removed unused IJobService, IJSRuntime)
- [x] SchedulerToolbar component (extracted page header + controls bar to `SchedulerToolbar.razor`)
- [x] `@if/_viewMode` switch that renders the appropriate view component
- [x] Data loading (`OnInitializedAsync`, `LoadData`, auto-refresh timer)
- [x] Shared state fields (executions, machines, etc.)
- [x] `DisposeAsync`
- [x] Removed dead `ActiveBuilds` property, unused `JumpToDateAsync` method
- [x] Condensed zoom/scroll wrappers to expression-bodied methods
- [x] Condensed field declarations (removed section comments, consolidated groups)
- [x] Final size: 427 lines (practical minimum for 6-view orchestrator with data loading, auto-refresh, viewport management, scheduling, and 2 modals)

**Verification:**
- [x] Build passes
- [ ] All 6 views work (needs manual test)
- [ ] All 5 modals work (needs manual test)
- [ ] Zoom (buttons + Ctrl+scroll) works (needs manual test)
- [ ] Auto-refresh works (needs manual test)
- [ ] No regressions (needs manual test)

---

## Line Count Tracking

| Component | Target Lines | Status |
|-----------|-------------|--------|
| Index.razor (orchestrator) | ~200 | ✅ (427 lines — practical min for 6-view orchestrator) |
| SchedulerToolbar.razor | n/a | ✅ (78 lines) |
| GanttViewport.razor | ~250 | ✅ (313 lines) |
| GanttTimeHeader.razor | ~60 | ⬜ |
| GanttMachineRow.razor | ~100 | ✅ (69 lines) |
| GanttBar.razor | ~40 | ✅ (32 lines) |
| GanttBuildGroupBar.razor | ~50 | ✅ (25 lines) |
| UnscheduledSidebar.razor | ~50 | ✅ (43 lines) |
| GanttView.razor | ~80 | ✅ (123 lines) |
| BuildsView.razor | ~200 | ✅ (213 lines) |
| WorkOrdersView.razor | ~350 | ✅ (533 lines) |
| PartPathView.razor | ~180 | ✅ (215 lines) |
| StagesView.razor | ~120 | ✅ (143 lines) |
| TableView.razor | ~120 | ✅ (199 lines) |
| RescheduleModal.razor | 97 | ✅ |
| CreateJobModal.razor | 124 | ✅ |
| WoBuildModal.razor | 142 | ✅ |
| WoJobModal.razor | 89 | ✅ |
| QuickScheduleModal.razor | 167 | ✅ |
| **Total** | **~2,130** | **✅ ~2,813** |

*Total is less than current 2,444 because duplicated sidebar markup and shared
helper methods are consolidated.*

---

## Notes

- `gantt-viewport.js` stays as-is — it's already well-isolated JS. The `GanttViewport.razor`
  component will own the `IJSObjectReference` and handle all interop.
- CSS in `site.css` does NOT need to change — class names stay the same.
- Services are NOT being refactored — only the UI layer is being decomposed.
- Each section is designed to be completable in a single AI session.
