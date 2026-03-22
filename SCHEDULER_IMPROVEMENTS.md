> ⚠️ **LEGACY** — Superseded by `docs/context/` and `docs/fixes/`. Do not use for new development.

# Scheduler Page — Analysis Report & Improvement Plan

## Current State Analysis

### Architecture (1,981 lines, single-file component)
The Scheduler is the most complex page in the application. It hosts **6 view modes** (Gantt, SLS Builds, Work Orders, Part Path, Stages, Table), **4 modals** (Reschedule, Create Job, Create Build from WO, Schedule Job from WO), and all supporting logic in one `@code` block. Despite the size, the code is well-structured with clear section comments.

### What Works Well ✅
| Feature | Notes |
|---------|-------|
| **Gantt chart** | Solid implementation — machine rows, grouped by type (SLS first), today marker, build-level bar aggregation, conflict detection, priority coloring |
| **Multi-zoom** | 10 zoom levels from 30min → 1 month, mouse-wheel/pinch JS interop, cursor-anchored zoom (maintains position at cursor) |
| **Pan controls** | ◀ Now ▶ navigation with shift-aware repositioning |
| **SLS Builds view** | Build pipeline summary stats, per-machine timelines with changeover windows, ready-to-schedule cards |
| **Work Orders view** | Demand-driven display, outstanding calculation accounts for in-build quantities, segmented progress bars, SLS/CNC action buttons |
| **Part Path view** | Job-oriented rows with stage-colored bars, filtering by part/priority, sorting |
| **Stage Queue view** | Kanban-style stage cards with workload bars, expandable item lists |
| **Table view** | Full data grid with all execution details |
| **Auto-schedule** | One-click scheduling for individual tasks and bulk all-at-once |
| **WO→Build flow** | Create builds directly from WO demand, creates BuildPackage with parts linked to WO line |
| **WO→Job flow** | Generate and auto-schedule CNC jobs from WO line demand |

### Issues Found 🔴

#### 1. **Repeated header rendering** (DRY violation)
Lines 102-140 (Gantt), 342-380 (Builds), 741-779 (Part Path) — the gantt header (day-level vs time-level) is copy-pasted 3 times with only the corner label changing ("Machine", "SLS Machine", "Job / Part").

#### 2. **Repeated sidebar rendering** (DRY violation)
Lines 267-293 (Gantt sidebar) and 857-883 (Part Path sidebar) are nearly identical unscheduled-work sidebars.

#### 3. **Data reloading on every interaction**
`LoadData()` fetches ALL executions, ALL build packages, ALL work orders, and ALL machine timelines on every pan/zoom/action. For a page that might be panned frequently, this is heavy — especially `LoadMachineTimelinesAsync` which makes N sequential HTTP calls (one per SLS machine).

#### 4. **Work Orders loaded with two separate queries**
Lines 1390-1391: `GetAllWorkOrdersAsync(Released)` then `GetAllWorkOrdersAsync(InProgress)` — two DB queries when one multi-status query would suffice.

#### 5. **`AutoScheduleAll` always picks first SLS machine** (Line 1822)
`SlsMachines.FirstOrDefault()` means all auto-scheduled builds go to the same machine, ignoring load balancing. Should use `FindEarliestBuildSlotAsync` across all SLS machines.

#### 6. **No real-time refresh**
The page has no auto-refresh or SignalR push. During production hours, operators on one screen make changes that schedulers on another screen won't see until manual refresh.

#### 7. **Gantt bar text truncation**
Long part numbers or build names overflow the bar text. No CSS `text-overflow: ellipsis` protection.

#### 8. **Missing keyboard shortcuts**
No keyboard navigation for zoom (Ctrl+=/Ctrl+-), pan (←/→), or view switching (1-6).

#### 9. **No drag-and-drop scheduling**
Bars can't be dragged to reschedule — users must open the modal for every change. This is the #1 friction point for daily scheduling.

#### 10. **Stage Queue workload threshold hardcoded** (Line 903)
`queuedHours / 40.0 * 100` — 40h threshold is hardcoded. Should come from shift configuration.

#### 11. **Unscheduled sidebar duplicated across views**
The same unscheduled sidebar appears in Gantt and Part Path views but not in Builds or Table views, creating inconsistency.

#### 12. **No WO due-date markers on Gantt**
The Gantt shows task bars but no visual indicators for when WO due dates are. A scheduler can't visually confirm "am I going to hit this deadline?"

#### 13. **Build-level bar grouping may hide detail** (Lines 176-180)
Grouping by `{BuildPackageId, ProductionStageId}` is correct, but when zoomed in tight, individual execution details are lost.

#### 14. **Table view has no sorting/filtering**
The table renders all executions in raw order. No column sorting, no status filter, no search.

#### 15. **Create Job modal doesn't auto-schedule on create** (Line 1870)
Despite the button saying "Create & Auto-Schedule", the code calls `JobService.CreateJobAsync` which creates the job + stages but doesn't call `Scheduler.AutoScheduleJobAsync`. The stages end up in the "unscheduled" list. (Note: `CreateJobAsync` may internally call scheduling — needs verification.)

#### 16. **No "undo" for scheduling actions**
No way to bulk-unschedule, and no confirmation dialog for auto-scheduling all.

### Improvement Suggestions (Prioritized)

#### Priority 1 — Correctness & Logic Fixes
- [ ] **Fix `AutoScheduleAll` build load balancing** — use `FindEarliestBuildSlotAsync` across all SLS machines instead of always picking the first
- [ ] **Single WO query** — replace two `GetAllWorkOrdersAsync` calls with one multi-status query
- [ ] **Verify Create Job auto-scheduling** — ensure `CreateJobAsync` triggers auto-scheduling or add explicit call

#### Priority 2 — Performance
- [ ] **Debounce `LoadData`** — skip refetch if date range unchanged (cache by `_dateFrom`+`_dateTo` hash)
- [ ] **Parallel machine timeline loading** — `Task.WhenAll` instead of sequential `foreach`
- [ ] **Incremental data fetch** — only fetch executions that changed (use ETag or last-modified timestamp)

#### Priority 3 — UX Polish
- [ ] **WO due-date markers on Gantt** — vertical dashed lines showing when WO deadlines fall
- [ ] **Table sorting/filtering** — column sort, status filter dropdown, search box
- [ ] **Keyboard shortcuts** — zoom (Ctrl+=/Ctrl+-), pan (←/→), view (1-6 keys)
- [ ] **Auto-refresh timer** — 30-second polling or SignalR push for multi-user environments
- [ ] **Confirmation dialog for bulk auto-schedule** — "This will schedule X jobs and Y builds. Continue?"

#### Priority 4 — Advanced Features (Future)
- [ ] **Drag-and-drop rescheduling** — JS interop for bar dragging on Gantt
- [ ] **Extract shared Gantt header as Blazor component** — eliminate the 3x copy-paste
- [ ] **Shift overlay on Gantt** — show shift boundaries (8am-5pm) as highlighted bands
- [ ] **Resource conflict highlighting** — flash machines with overlapping work
- [ ] **Undo stack** — track last N scheduling actions for single-click undo

---

## Executable Improvement Plan

### Phase A — Logic Fixes (do first, zero UI change)

#### A.1 Fix AutoScheduleAll build load balancing
**File:** `Components/Pages/Scheduler/Index.razor` (lines 1818-1829)

**Current:** Always picks `SlsMachines.FirstOrDefault()`
**Fix:** For each ready build, find the machine with the earliest available slot:
```
foreach build in readyBuilds:
    bestMachine = null, bestEnd = DateTime.MaxValue
    foreach machine in SlsMachines:
        slot = FindEarliestBuildSlotAsync(machine.Id, build.EstimatedDurationHours, DateTime.UtcNow)
        if slot.PrintEnd < bestEnd: bestMachine = machine, bestEnd = slot.PrintEnd
    if bestMachine != null: ScheduleBuildAsync(build.Id, bestMachine.Id)
```

#### A.2 Single WO query
**File:** `Services/IWorkOrderService.cs` + `Services/WorkOrderService.cs`
- Add `GetWorkOrdersByStatusesAsync(params WorkOrderStatus[] statuses)` method
- Replace lines 1390-1391 with single call

#### A.3 Parallel machine timeline loading
**File:** `Components/Pages/Scheduler/Index.razor` (lines 1424-1436)
- Replace sequential `foreach` with `Task.WhenAll` pattern

### Phase B — UX Quick Wins

#### B.1 Table view sorting + filtering
**File:** `Components/Pages/Scheduler/Index.razor` (lines 998-1091)
- Add state variables: `_tableSortCol`, `_tableSortAsc`, `_tableFilterStatus`
- Add header click handlers for column sort
- Add status filter dropdown above table
- Sort executions before rendering

#### B.2 WO due-date markers on Machine Gantt
**File:** `Components/Pages/Scheduler/Index.razor` (gantt track sections)
- Collect unique WO due dates from `_workOrders`
- Render as dashed vertical lines with WO number tooltip on each machine's gantt track

#### B.3 Auto-refresh timer
**File:** `Components/Pages/Scheduler/Index.razor`
- Add `System.Threading.Timer` that calls `LoadData` every 30 seconds
- Add visual indicator showing last refresh time
- Dispose timer in `Dispose()`

#### B.4 Keyboard shortcuts
**File:** `Components/Pages/Scheduler/Index.razor`
- Register `keydown` listener via JS interop
- Handle: `1-6` for views, `+/-` for zoom, `←/→` for pan, `R` for refresh

### Phase C — DRY Refactoring

#### C.1 Extract GanttHeader component
**File:** `Components/Shared/GanttHeader.razor` (NEW)
- Parameters: `string CornerLabel`, `List<DateTime> Slots`, `bool IsDayLevel`, `Func<List<(DateTime Date, int Count)>> GetDateGroups`
- Replace 3 header blocks in Index.razor with `<GanttHeader>`

#### C.2 Extract UnscheduledSidebar component
**File:** `Components/Shared/UnscheduledSidebar.razor` (NEW)
- Parameters: `List<StageExecution> Items`, `EventCallback<StageExecution> OnManualSchedule`, `EventCallback<int> OnAutoSchedule`
- Replace 2 sidebar blocks

### Phase D — Advanced (Future Phases)

#### D.1 Shift band overlay
- Query `OperatingShifts` for the date range
- Render semi-transparent bands on Gantt showing non-shift hours (dimmed)

#### D.2 Drag-and-drop
- JS interop library (e.g., custom or interact.js)
- `DragStart` captures execution ID, `Drop` on time slot calls `ConfirmReschedule`

---

## Execution Priority

```
Immediate (fixes):  A.1, A.2, A.3
Next sprint:        B.1, B.2, B.3, B.4
Maintenance:        C.1, C.2
Future:             D.1, D.2
```
