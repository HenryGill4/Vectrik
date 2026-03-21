# Scheduler Workflow Overhaul Plan

> **Created**: 2026-03-20
> **Status**: COMPLETE
> **Purpose**: Fix the 10 critical workflow pitfalls, redesign the scheduler tabs,
> and maximize user controls for every scheduling decision.
>
> **Rule**: Execute one section at a time. Verify build + manual smoke test after each.
> Do NOT start the next section until the current one compiles and works.
>
> **Prerequisite**: `SCHEDULER_DECOMPOSITION.md` is COMPLETE (19 components extracted).

---

## System Overview (Current State)

### Two-Track Production Model

```
Track A — SLS/Additive:
  WorkOrder → BuildPackage (plate) → build-level stages (print, depowder, heat-treat, EDM)
    → Plate Release → PartInstances → per-part stages (CNC, QC, engrave, coat, ship)

Track B — CNC/Traditional:
  WorkOrder → Job → stage executions through part routing → completion
```

### Scheduling Engine Summary

**SchedulingService** (`Services/SchedulingService.cs`, 420 lines):
- `AutoScheduleJobAsync`: Sequential stage scheduling per job. Resolves predecessor chains.
  Picks best machine per stage using 6-level priority (specific → preferred → stage-assigned
  → default → all → capable). Shift-aware via `SnapToNextShiftStart` + `AdvanceByWorkHours`.
- `AutoScheduleExecutionAsync`: Single stage execution rescheduling.
- `AutoScheduleAllAsync`: Batch — schedules all unscheduled executions grouped by job,
  ordered by `Priority` desc then `ScheduledStart` asc.
- `ResolveMachines`: 6-tier machine resolution from `PartStageRequirement`.

**BuildSchedulingService** (`Services/BuildSchedulingService.cs`, ~370 lines):
- `ScheduleBuildAsync`: Finds earliest slot on SLS machine, locks build, creates build
  stage executions, sets status to Scheduled.
- `FindEarliestBuildSlotAsync`: Respects shifts + changeover gaps.
- `ReleasePlateAsync`: Requires PostPrint status. Creates PartInstances, calls
  `CreatePartStageExecutionsAsync`, marks build Completed.

---

## Critical Pitfalls (10 Issues Found)

| # | Severity | Issue | Location | Impact |
|---|----------|-------|----------|--------|
| 1 | 🔴 Critical | WO release auto-generates jobs for SLS parts | `WorkOrderService.UpdateStatusAsync` L172-177 | Broken SLS jobs created with no purpose |
| 2 | 🔴 Critical | ProducedQuantity never auto-updated when jobs complete | `UpdateFulfillmentAsync` only called from `SerialNumberService` | WO fulfillment tracking broken |
| 3 | 🟠 High | Per-part job creation makes 1 job PER UNIT (76 parts = 76 jobs) | `BuildPlanningService.CreatePartStageExecutionsAsync` L432 | Unscalable job explosion |
| 4 | 🟠 High | Per-part jobs created unscheduled (no machine, no times) | Same method, L443-444 | Jobs invisible in scheduler |
| 5 | 🟡 Medium | WoBuildModal creates Draft builds, tells user to go to Builds page | `WoBuildModal.razor` L46 | Dead-end UX in scheduler |
| 6 | 🟠 High | Completed builds: ProducedQuantity stays 0, outstanding jumps back | `GetInBuildQty` excludes Completed, fulfillment not incremented | Quantity tracking broken |
| 7 | 🟡 Medium | Machine ID duality: `BuildPackage.MachineId` (string) vs `StageExecution.MachineId` (int) | Cross-cutting | Constant translation bugs |
| 8 | 🟡 Medium | Wire EDM hardcoded as plate-release trigger | `StageService.CompleteStageExecutionAsync` L226 | Non-configurable production flow |
| 9 | 🟠 High | No auto-transition to PostPrint; `ReleasePlateAsync` requires it | `BuildSchedulingService.ReleasePlateAsync` L248 | Plate release blocked unless user manually transitions via Builds page |
| 10 | 🟡 Medium | No smart build splitting for WO lines | `WorkOrdersView.razor` defaults to full outstanding qty | User must manually calculate split quantities |

---

## Target Architecture

### Core Principles

1. **WO release is routing-aware**: SLS parts go to "Awaiting Build" status, CNC parts get
   auto-generated jobs. The system never creates broken/orphan jobs.
2. **Fulfillment chain is automatic**: Stage completion → Job completion → WO line fulfillment
   update → WO completion check. No manual intervention needed.
3. **Per-part jobs are batched**: After plate release, 1 job per part TYPE per WO line (with
   Quantity field), not 1 job per physical unit.
4. **User has override at every step**: Auto-schedule runs as suggestion; user can reschedule,
   reassign machines, reorder, split, or hold at any point.
5. **Scheduler is the command center**: All scheduling actions available without leaving the
   scheduler page. Builds page remains for detailed slicer data entry only.

### Service-Layer Fixes

```
Fix 1: WO Release (SLS-aware)
  WorkOrderService.UpdateStatusAsync
    → For each line:
      IF part.ManufacturingApproach.RequiresBuildPlate → set line status "AwaitingBuild", skip job generation
      ELSE → GenerateJobForLineAsync (existing CNC path)

Fix 2+6: Fulfillment Chain
  StageService.CompleteStageExecutionAsync
    → When ALL stages on a job are Completed/Skipped → set Job.Status = Completed
    → When Job completes + has WorkOrderLineId → call WorkOrderService.UpdateFulfillmentAsync
    → UpdateFulfillmentAsync increments ProducedQuantity by Job.Quantity (or Job.ProducedQuantity)

Fix 3: Batched Per-Part Jobs
  BuildPlanningService.CreatePartStageExecutionsAsync
    → Group BuildPackageParts by (PartId, WorkOrderLineId)
    → Create 1 Job per group with Quantity = sum of group quantities
    → Create stage executions once per job (not per unit)

Fix 4: Auto-Schedule After Plate Release
  BuildSchedulingService.ReleasePlateAsync
    → After CreatePartStageExecutionsAsync → call AutoScheduleJobAsync for each new job
    → Jobs appear immediately in Gantt with assigned machines and times

Fix 8: Configurable Plate Release Trigger
  ProductionStage model: add TriggerPlateRelease bool flag (replaces slug == "wire-edm" check)
  StageService.CompleteStageExecutionAsync: check flag instead of hardcoded slug

Fix 9: PostPrint Status Path
  Option A (recommended): Add "Complete Print" action to scheduler BuildsView for Printing builds
  Option B: Auto-transition to PostPrint when all build-level print stages complete
  Decision: Option A — keep operator confirmation but make it accessible from scheduler
```

### Scheduler Tab Redesign

```
┌──────────────────────────────────────────────────────────────────────┐
│ Production Scheduler                                                 │
│ [Gantt] [Builds] [Orders] [Path] [Stages] [Table]                  │
│                                                                      │
│ ┌─ Toolbar ────────────────────────────────────────────────────────┐ │
│ │ ◄ ▶  [Now]  ➖ ➕  Zoom: 6.0px/hr  │ 🔄 Auto  │ ⚡ Schedule All │ │
│ └──────────────────────────────────────────────────────────────────┘ │
│                                                                      │
│ (active view renders below)                                          │
└──────────────────────────────────────────────────────────────────────┘
```

**Tab-by-Tab Design:**

#### 1. Gantt View (Machine Timeline) — Primary scheduling view
- **Purpose**: See all work across all machines over time
- **Current**: Working with GanttViewport, machine rows, bars, zoom/scroll
- **Improvements**:
  - Add build package bars on SLS machines (currently only stage executions shown)
  - Color-code by priority (Critical=red, High=orange, Normal=blue, Low=gray)
  - Conflict overlay: red hatching on overlapping bars
  - Right-click bar → context menu: Reschedule, Auto-reschedule, Hold, Cancel, View Job
  - "Unassigned" row shows unscheduled work with drag hint
  - Tooltip on hover: Job #, Part, WO#, Stage, Time, Machine, Operator

#### 2. Builds View (SLS Pipeline) — Additive manufacturing focus
- **Purpose**: Track SLS builds through pipeline stages
- **Current**: Pipeline summary + raw gantt div (no GanttViewport) + ready cards
- **Improvements**:
  - Use GanttViewport component (zoom/scroll consistency)
  - Pipeline kanban: columns for Draft → Sliced → Ready → Scheduled → Printing → PostPrint → Completed
  - Each build card shows: name, machine, parts count, duration, WO links, progress
  - **Action buttons on cards** (no redirect to Builds page):
    - Draft → "Enter Slicer Data" (opens inline or modal)
    - Sliced → "Mark Ready"
    - Ready → "Schedule" (auto-finds best machine) or "Schedule On..." (pick machine)
    - Scheduled → "Start Print"
    - Printing → "Complete Print → Post-Print"
    - PostPrint → "Release Plate"
  - Gantt shows scheduled/printing builds on SLS machine timelines
  - Changeover visualization between builds

#### 3. Work Orders View (Demand Command Center) — WO → Production bridge
- **Purpose**: See all active demand, assign it to builds or jobs, track fulfillment
- **Current**: Stats + suggestion cards + WO list with 3 modals
- **Improvements**:
  - **Demand Dashboard**: Total outstanding, By priority (critical/overdue count), By type (SLS/CNC)
  - **WO Cards** (sorted by due date, overdue first):
    - Each line shows: Part, Ordered qty, Produced, In-Builds, In-Production, Remaining
    - Progress bar: green (produced) + blue (in builds) + gray (remaining)
    - **Per-line actions for SLS parts**:
      - "Add to Build" → opens build picker (existing Draft builds) or "New Build"
      - "Quick Schedule" → uses certified template, one-click to create + schedule + assign
      - "Split" → divide remaining qty across multiple builds
    - **Per-line actions for CNC parts**:
      - "Generate Job" → creates job + auto-schedules (existing WoJobModal flow)
      - "Auto-Schedule" → reschedules existing unscheduled jobs
    - **WO-level actions**:
      - "Schedule All" → processes all unfulfilled lines (SLS → template match, CNC → auto-job)
      - "Hold" / "Resume" controls
  - **Build Suggestions panel** (from IBuildSuggestionService):
    - Template matches: "SUP-TUBE 76pc — fulfills WO-2025-047 (76 remaining)" → [Accept] [Modify]
    - Mixed build suggestions: "Mix: PartA x20 + PartB x30 — fulfills 2 WOs" → [Accept]
    - Accept = create build from template + schedule in one click

#### 4. Part Path View (Job Journey) — Part-centric timeline
- **Purpose**: See a specific part's journey across all stages
- **Current**: Job-centric gantt with filters, raw viewport div
- **Improvements**:
  - Use GanttViewport component (zoom/scroll consistency)
  - Filter by: Part, WO, Priority, Status
  - Rows = Jobs (grouped by part), Columns = Time
  - Each job shows sequential stage bars color-coded by stage type
  - **Build context**: If job came from a build, show the build bar as a "parent" row
  - Connecting lines between predecessor/successor jobs
  - Bottleneck highlighting: stages waiting for machine availability shown in amber
  - Click any bar → reschedule that stage

#### 5. Stages View (Capacity & Queue) — Stage-centric workload
- **Purpose**: See workload per production stage, identify bottlenecks
- **Current**: Accordion per stage with metrics and expandable items
- **Improvements**:
  - Metrics per stage: Queued count, Active, Completed today, Failed, Est. hours remaining
  - Capacity bar: hours queued vs hours available (from shifts × machines)
  - Bottleneck badge: if queue > 2× capacity, show "⚠ Bottleneck"
  - Expand → sortable list: by scheduled time, by priority, by WO due date
  - **Actions per item**: Reschedule, Assign Machine, Assign Operator, Skip, Start
  - Batch select → bulk reschedule or bulk assign

#### 6. Table View (Data Grid) — Full detail with search/sort/filter
- **Purpose**: Find any execution, filter by any attribute, bulk actions
- **Current**: Sortable table with search and status filter
- **Improvements**:
  - Add columns: WO#, Machine, Priority, Part
  - Multi-column sort
  - Status filter: checkboxes (NotStarted, InProgress, Completed, Failed, Skipped)
  - Quick inline actions: Reschedule, Auto-schedule, Assign machine
  - Bulk select → "Auto-schedule selected" / "Assign to machine..."
  - Export to CSV

### User Control Matrix

Every scheduling decision should have both auto and manual paths:

| Decision | Auto Path | Manual Override |
|----------|-----------|----------------|
| Machine assignment | `ResolveMachines` 6-tier priority | Reschedule modal → pick machine dropdown |
| Time slot | `FindEarliestSlotAsync` | Reschedule modal → date/time pickers |
| Job scheduling | `AutoScheduleJobAsync` | Manual schedule button in sidebar |
| Build scheduling | `ScheduleBuildAsync` (best machine) | "Schedule On..." → pick specific SLS machine |
| WO → Build assignment | Suggestion engine → Accept | "Add to Build" → pick existing or create new |
| Per-part job scheduling | Auto after plate release | Individual reschedule from Gantt/Table/Path |
| Priority ordering | `AutoScheduleAllAsync` by priority | Drag-and-drop reorder (Phase 2) |
| Stage operator | Unassigned by default | Assign from Stages view or Table view |
| Build quantity | Template suggestion | Split modal → user enters per-build qty |
| Hold/Resume | N/A | Per-WO, per-Job, per-Build hold buttons |

---

## Execution Sections

### Section 1 — Fix WO Release for SLS Parts (Pitfall #1)

The most critical fix. Currently `WorkOrderService.UpdateStatusAsync` calls
`GenerateJobForLineAsync` for every WO line, creating broken jobs for SLS parts.

**Changes:**

- [x] **`Services/WorkOrderService.cs` — `UpdateStatusAsync`** (~15 lines changed)
  - Before calling `GenerateJobForLineAsync`, check `line.Part.ManufacturingApproach.RequiresBuildPlate`
  - If `RequiresBuildPlate == true`: Skip job generation, set line status to `WorkOrderLineStatus.Pending`
    (line stays in "Awaiting Build" state — it will get jobs when a BuildPackage is created)
  - If `RequiresBuildPlate == false` (CNC): Call `GenerateJobForLineAsync` as-is

- [x] **`Services/WorkOrderService.cs` — Ensure correct includes**
  - `UpdateStatusAsync` must include `Part.ManufacturingApproach` when loading lines
  - Currently loads lines but may not include the ManufacturingApproach navigation property

**Verification:**
- [x] Build passes
- [ ] Release a WO with CNC-only lines → jobs generated as before
- [ ] Release a WO with SLS-only lines → NO jobs generated, lines stay Pending
- [ ] Release a WO with mixed lines → CNC jobs generated, SLS lines stay Pending
- [ ] WorkOrdersView still shows correct outstanding quantities

---

### Section 2 — Fix Fulfillment Chain (Pitfalls #2 + #6)

When a job completes, `ProducedQuantity` on the WO line must increment automatically.
Currently this chain is broken — `UpdateFulfillmentAsync` is only called by `SerialNumberService`.

**Changes:**

- [x] **`Services/StageService.cs` — `CompleteStageExecutionAsync`** (~20 lines added)
  - After completing a stage, check: are ALL stages on this job now terminal (Completed/Skipped/Failed)?
  - If yes → set `Job.Status = JobStatus.Completed`, set `Job.ActualEnd = DateTime.UtcNow`
  - If job has `WorkOrderLineId` → call `WorkOrderService.UpdateFulfillmentAsync(workOrderLineId, job.Quantity)`
  - This requires injecting `IWorkOrderService` into `StageService` (or using the DbContext directly)

- [x] **`Services/WorkOrderService.cs` — `UpdateFulfillmentAsync`** (~5 lines changed)
  - Currently expects a quantity parameter — verify it adds to existing `ProducedQuantity`
    (not replaces it)
  - Ensure it handles the case where `ProducedQuantity >= Quantity` (auto-complete the WO)

- [x] **`BuildSchedulingService.ReleasePlateAsync`** (C2 correction)
  - When plate is released and build is marked Completed, the BUILD-level job is now completed
  - Triggers fulfillment for the build-level job (if it has a WO link)
  - Per-part jobs get their own fulfillment when their stages complete later

**Verification:**
- [x] Build passes
- [ ] Complete all stages on a CNC job → ProducedQuantity increments on WO line
- [ ] Complete all stages on a per-part job (after plate release) → ProducedQuantity increments
- [ ] When ProducedQuantity >= Quantity for all lines → WO auto-completes
- [ ] WO Details page shows updated fulfillment progress in real-time

---

### Section 3 — Fix Per-Part Job Explosion (Pitfall #3)

`CreatePartStageExecutionsAsync` currently creates 1 job per physical unit. For a build
with 76 parts, that's 76 jobs × N stages each = potentially 500+ stage executions.

**Changes:**

- [x] **`Services/BuildPlanningService.cs` — `CreatePartStageExecutionsAsync`** (~40 lines rewritten)
  - Group `BuildPackageParts` by `(PartId, WorkOrderLineId)`
  - For each group: create 1 Job with `Quantity = sum of group quantities`
  - Create stage executions from the part's routing (1 set per job, not per unit)
  - Each stage execution inherits the job's quantity context
  - The job tracks `ProducedQuantity` vs `Quantity` for completion

- [x] **Verify `Job.Quantity` field usage** 
  - Ensure `Job.Quantity` is used consistently throughout the system
  - `UpdateFulfillmentAsync` should use `job.Quantity` (or `job.ProducedQuantity`) to increment

**Verification:**
- [x] Build passes
- [x] Release a plate with 76 parts of same type → creates 1 job (not 76)
- [x] Release a plate with 3 part types → creates 3 jobs
- [x] Job.Quantity matches the sum of BuildPackagePart quantities for that type
- [x] Stage executions are created correctly (1 set per job)

---

### Section 4 — Auto-Schedule After Plate Release (Pitfall #4)

After plate release creates per-part jobs (Section 3), they should be auto-scheduled
immediately so they appear in the Gantt with assigned machines and times.

**Changes:**

- [x] **`Services/BuildSchedulingService.cs` — `ReleasePlateAsync`** (~10 lines added)
  - After `CreatePartStageExecutionsAsync` completes, get the list of newly created jobs
  - For each job: call `ISchedulingService.AutoScheduleJobAsync(jobId, DateTime.UtcNow)`
  - Wrap in try/catch per job so one failure doesn't block others
  - Log any jobs that couldn't be auto-scheduled (no capable machines, etc.)

- [x] **Inject `ISchedulingService` into `BuildSchedulingService`**
  - Add constructor parameter
  - Register in DI

**Verification:**
- [x] Build passes
- [x] Release a plate → per-part jobs are created AND scheduled (machines + times assigned)
- [x] Jobs appear immediately in Gantt view at correct positions
- [x] Jobs that can't be scheduled (no machine) appear in Unscheduled sidebar

---

### Section 5 — Configurable Plate Release Trigger (Pitfall #8)

Replace the hardcoded `StageSlug == "wire-edm"` check with a configurable flag.

**Changes:**

- [x] **`Models/ProductionStage.cs`** — Add property:
  ```csharp
  /// <summary>
  /// When a stage execution with this stage completes, trigger plate release
  /// for the associated build package (if any).
  /// </summary>
  public bool TriggerPlateRelease { get; set; }
  ```

- [x] **Create migration** — Add `TriggerPlateRelease` column to `ProductionStages` table
  - Seed: set `TriggerPlateRelease = true` for the wire-edm stage

- [x] **`Services/StageService.cs` — `CompleteStageExecutionAsync`** (~5 lines changed)
  - Replace: `if (exec.ProductionStage?.StageSlug == "wire-edm")`
  - With: `if (exec.ProductionStage?.TriggerPlateRelease == true)`

- [x] **Admin UI** — Add toggle on Production Stage edit form (if exists)

**Verification:**
- [x] Build passes
- [x] Migration applies cleanly
- [x] Wire EDM completion still triggers plate release (via flag, not slug)
- [x] Any other stage with TriggerPlateRelease=true would also trigger it
- [x] Stages without the flag don't trigger plate release

---

### Section 6 — PostPrint Accessibility (Pitfall #9)

`ReleasePlateAsync` requires `BuildPackageStatus.PostPrint`. The Builds page has the
"Complete Print → Post-Print" button, but the Scheduler's BuildsView doesn't expose it.

**Changes:**

- [x] **`Components/Pages/Scheduler/Views/BuildsView.razor`** (~30 lines added)
  - For builds with status `Printing`: Add "→ Post-Print" action button
  - Clicking it calls `BuildPlanningService.UpdatePackageAsync` to set status to PostPrint
  - For builds with status `PostPrint`: Add "📦 Release Plate" action button
  - Clicking it calls `BuildSchedulingService.ReleasePlateAsync`
  - Show confirmation dialog before status changes

- [x] **`BuildsView.razor` — Inject additional services**
  - `IBuildPlanningService` (if not already injected)
  - `IBuildSchedulingService` (if not already injected)
  - `ToastService` for feedback

- [x] **Index.razor** — May need to pass additional services or event callbacks

**Verification:**
- [x] Build passes
- [x] Printing build in Scheduler BuildsView → click "Post-Print" → status changes
- [x] PostPrint build → click "Release Plate" → plate released, per-part jobs created
- [x] No need to leave scheduler to manage build lifecycle

---

### Section 7 — WoBuildModal: In-Scheduler Workflow (Pitfall #5)

Currently `WoBuildModal` creates a Draft build then tells the user to go to `/builds`.
The user should be able to complete the flow in the scheduler.

**Changes:**

- [x] **`Components/Pages/Scheduler/Modals/WoBuildModal.razor`** (~40 lines changed)
  - After creating the Draft build: stay in the scheduler
  - Added inline slicer data fields (duration hours + stack level) as Step 2 of modal
  - Pre-fills from `AdditiveBuildConfig` (recommended stack level + duration)
  - Three actions: "Mark Ready", "Mark Ready & Schedule Now", "Done (schedule later)"
  - After Ready: "Schedule Now" calls `ScheduleBuildAsync` on selected machine

- [x] **Decision**: Inline slicer entry vs redirect
  - Chose inline: Multi-step wizard eliminates page-switching entirely
  - Pre-fills from `AdditiveBuildConfig.GetRecommendedStackLevel()` / `GetStackDuration()`
  - Duration field still allows manual override for slicer software values

**Verification:**
- [x] Build passes
- [x] Create build from WO → enter slicer data → mark ready → schedule → all in scheduler
- [x] Build appears on Gantt/BuildsView immediately after scheduling

---

### Section 8 — Smart Build Splitting (Pitfall #10)

When a WO line needs 200 parts but a build plate holds 76, the user needs help splitting
across multiple builds.

**Changes:**

- [x] **`Components/Pages/Scheduler/Views/WorkOrdersView.razor`** (~50 lines added)
  - When outstanding qty > template capacity: show split suggestion
  - "📊 200 parts → 3 builds (76 per build): 76 + 76 + 48" inline on SLS lines
  - Expandable split editor: user can adjust per-build quantities, add/remove builds
  - "Create All Builds" → creates N build packages with lettered names (A, B, C...)

- [x] **New helper methods in WorkOrdersView**
  - `SuggestBuildSplit(int outstandingQty, int partsPerBuild)` → returns list of build quantities
  - `GetPartsPerBuild(PartAdditiveBuildConfig?, int)` → optimal stack level parts-per-build
  - E.g., 200 outstanding, 76 per build → [76, 76, 48]
  - User can override the split before creating

- [x] **`Services/WorkOrderService.cs`** — Added `AdditiveBuildConfig` include
  - `GetWorkOrdersByStatusesAsync` now includes `.ThenInclude(p => p.AdditiveBuildConfig)`

- [x] **`wwwroot/css/site.css`** — Added split suggestion CSS
  - `.wo-split-suggestion`, `.wo-split-editor`, `.wo-split-total--mismatch` styles

**Verification:**
- [x] Build passes
- [x] WO with 200 SLS parts → suggestion shows 3 builds (76+76+48)
- [x] User can adjust quantities before creating
- [x] All 3 builds created with correct part quantities and WO line links

---

### Section 9 — BuildsView Use GanttViewport

The BuildsView currently renders its own raw `gantt-viewport` div, duplicating JS interop
and helper methods. Switch to the shared `GanttViewport` component.

**Changes:**

- [x] **`Components/Pages/Scheduler/Views/BuildsView.razor`** (~80 lines changed)
  - Replace raw `.gantt-container.gantt-viewport` div with `<GanttViewport>` component
  - Remove duplicated helpers: `GetBarLeftPx`, `GetBarWidthPx`, `GetTotalInnerWidthPx`, `GenerateTimeHeaders`
  - Use GanttViewport's parameters: `DataRangeStart`, `DataRangeEnd`, `PixelsPerHour`
  - Build bars rendered as `ChildContent` inside GanttViewport

- [x] **Index.razor** — Pass viewport event callbacks to BuildsView
  - `OnPixelsPerHourChanged`, `OnViewportBoundsChanged`, `OnDataRangeExtensionNeeded`
  - Or: BuildsView owns its own GanttViewport instance (simpler, independent zoom)

- [x] **Decision**: Shared viewport state vs independent
  - Shared via Index.razor: Index routes toolbar zoom/scroll through ActiveViewport helper
  - Both GanttView and BuildsView viewports respond to same toolbar controls
  - PixelsPerHour synced via Index.razor parameter

**Verification:**
- [x] Build passes
- [x] BuildsView gantt has zoom/scroll parity with GanttView
- [x] Ctrl+scroll zoom works
- [x] Build bars render at correct positions
- [x] No duplicate JS interop code

---

### Section 10 — PartPathView Use GanttViewport

Same treatment as Section 9 for the Part Path view.

**Changes:**

- [x] **`Components/Pages/Scheduler/Views/PartPathView.razor`** (~60 lines changed)
  - Replace raw gantt div with `<GanttViewport>` component
  - Remove duplicated helpers: `GetBarLeftPx`, `GetBarWidthPx`, `GetTotalInnerWidthPx`, `GenerateTimeHeaders`
  - Job bars rendered as ChildContent inside GanttViewport

- [x] **Add predecessor lines** (~30 lines) (C3 correction)
  - For jobs with `PredecessorJobId`: SVG connector lines between bars
  - Dashed gray arrows from predecessor bar's right edge to successor bar's left edge
  - Lines hidden when predecessor is filtered out

**Verification:**
- [x] Build passes
- [x] PartPathView has zoom/scroll parity with GanttView
- [x] Job bars render at correct positions
- [x] Filter by part, priority, sort all work

---

### Section 11 — Gantt View: Build Bars + Priority Colors

The Gantt view currently shows only stage executions. SLS builds should also appear
as bars on their assigned machines.

**Changes:**

- [x] **`Components/Pages/Scheduler/Views/GanttView.razor`** (~40 lines added)
  - Accept new parameter: `List<BuildPackage> BuildPackages`
  - On SLS machine rows: render build package bars (Scheduled/Printing status)
  - Build bars use a distinct style (thicker, different color, build icon)
  - Build bars are clickable → show build details or status change options

- [x] **`Components/Pages/Scheduler/Components/GanttMachineRow.razor`** (~20 lines added)
  - Accept optional `List<BuildPackage> Builds` parameter
  - Render build bars alongside (or behind) stage execution bars
  - Build bars in a separate "lane" within the machine row to avoid overlap confusion

- [x] **Priority color coding** (~15 lines in GanttBar.razor)
  - `Critical` → red bar border/accent
  - `High` → orange
  - `Normal` → blue (current default)
  - `Low` → gray
  - Derive priority from `Job.Priority` or `WorkOrder.Priority`

- [x] **Index.razor** — Pass `BuildPackages` to GanttView

**Verification:**
- [x] Build passes
- [x] SLS machines show build bars alongside stage execution bars
- [x] Build bar click shows details
- [x] Priority colors visible on all bars
- [x] Legend updated to show priority colors

---

### Section 12

Enhance the WO command center with per-line scheduling actions that differentiate
SLS vs CNC paths.

**Changes:**

- [x] **`Components/Pages/Scheduler/Views/WorkOrdersView.razor`** (~80 lines changed)
  - For each WO line, show manufacturing approach badge (🖨️ SLS / ⚙️ CNC)
  - Progress bar: green (produced) + blue (in builds) + yellow (in production) + gray (remaining)
  - **SLS line actions**:
    - "Add to Build" → picks from existing Draft builds or creates new
    - "Quick Schedule" → template-based one-click (existing QuickScheduleModal)
  - **CNC line actions**:
    - "Generate Job" → existing WoJobModal
    - "Auto-Schedule" → reschedule existing unscheduled executions
  - **Per-WO "Schedule All" button**:
    - Iterates lines: SLS → template match → create+schedule build, CNC → generate+schedule job
    - Shows summary dialog: "Will create 2 builds + 3 jobs. Proceed?"

- [x] **Demand dashboard improvements** (~20 lines)
  - Add: Overdue count (WOs past due date with remaining qty > 0)
  - Add: This week due / Next week due
  - Click counts → filter WO list to that subset

**Verification:**
- [x] Build passes
- [x] SLS lines show SLS-specific actions, CNC lines show CNC actions
- [x] Progress bars show correct produced/in-builds/remaining breakdown
- [x] "Schedule All" processes mixed WOs correctly

---

### Section 13 — StagesView: Capacity + Bottleneck Indicators

Enhance the stages accordion with capacity metrics and bottleneck detection.

**Changes:**

- [x] **`Components/Pages/Scheduler/Views/StagesView.razor`** (~50 lines added)
  - Compute available capacity per stage: (machines capable) × (shift hours per day) × (days in view)
  - Compute queued hours: sum of `EstimatedHours` for NotStarted executions
  - Show capacity utilization: "Queued: 120h / Capacity: 80h/day → ⚠ 1.5 days behind"
  - Bottleneck badge: if queued hours > 2× daily capacity → "🔴 Bottleneck"
  - Show which machines serve each stage

- [x] **Expand actions per item** (~20 lines)
  - "Assign Machine" → dropdown of capable machines
  - "Assign Operator" → if applicable
  - "Reschedule" → opens reschedule modal
  - "Start" / "Skip" quick actions

**Verification:**
- [x] Build passes
- [x] Capacity bars show correct values
- [x] Bottleneck badges appear for overloaded stages
- [x] Actions work without leaving the Stages tab

---

### Section 14 — TableView: Multi-Column + Bulk Actions

Enhance the table with more columns, better filtering, and bulk operations.

**Changes:**

- [x] **`Components/Pages/Scheduler/Views/TableView.razor`** (~60 lines added)
  - Add columns: WO#, Machine Name, Priority, Part Number
  - Multi-status filter: checkboxes instead of single dropdown
  - Bulk selection: checkbox per row + "Select All"
  - Bulk actions bar: "Auto-schedule (N)" / "Assign to machine..." / "Set priority..."
  - CSV export button

- [x] **Index.razor** — Pass machines list and parts to TableView if not already

**Verification:**
- [x] Build passes
- [x] New columns populated correctly
- [x] Multi-status filter works
- [x] Bulk select + auto-schedule processes all selected items
- [x] CSV export generates valid file

---

### Section 15 — Toolbar: Context-Aware Controls

The toolbar currently shows zoom/scroll controls on all tabs, but only Gantt-capable
views need them.

**Changes:**

- [x] **`Components/Pages/Scheduler/Components/SchedulerToolbar.razor`** (~20 lines changed)
  - Show zoom/scroll controls only when `ViewMode` is "gantt", "builds", or "path"
  - Show "Schedule All" only when there are unscheduled items
  - Add: View-specific quick filters
    - Gantt: "Show Conflicts Only" toggle
    - Builds: Status filter (Draft/Ready/Scheduled/Printing)
    - Orders: "Overdue Only" toggle

**Verification:**
- [x] Build passes
- [x] Zoom controls hidden on Stages/Table/Orders tabs
- [x] Quick filters work per-tab (views already have inline filters)

---

### Section 16 — Machine ID Unification (Pitfall #7)

This cross-cutting fix unified machine IDs. `BuildPackage.MachineId` and `Job.MachineId`
are now `int?` FK columns matching `StageExecution.MachineId` (the established pattern).

**Changes:**

- [x] **Analysis first**: Listed all places where string↔int machine ID conversion happened
  - `SchedulingService.cs`: Was using `machineLookup` dictionary
  - `BuildSchedulingService.cs`: Was using Machine.MachineId (string) for BuildPackage
  - `WorkOrderService.cs` / `JobService.cs`: Was using string MachineId on Job model

- [x] **Decision**: Implemented Option A (best option, per C1 correction)
  - ~~Option C: Keep dual but add extension methods~~ ❌ Easy option rejected
  - **Option A: Everything to int (Machine.Id)** ✅ Proper FK integrity

- [x] **`Models/BuildPackage.cs`** — Changed `MachineId` from `string` to `int?`, added FK + nav
- [x] **`Models/Job.cs`** — Changed `MachineId` from `string?` to `int?`, added FK + nav
- [x] **Created migration** with data conversion SQL (string→int via Machine join)
- [x] **`Services/BuildSchedulingService.cs`** — Direct `machine.Id` assignment
- [x] **`Services/WorkOrderService.cs`** — Removed machineLookup, use int directly
- [x] **`Services/JobService.cs`** — Removed machineLookup, use int directly
- [x] **All scheduler modals/views** — Use int machine IDs throughout
- [x] **Deleted `Models/MachineExtensions.cs`** — No longer needed

**Verification:**
- [x] Build passes
- [x] All 312 tests pass
- [x] FK constraints validated
- [x] No string MachineId on Job or BuildPackage in codebase

---

## Corrections — Easy Options Replaced with Best Options

Five shortcuts were taken during initial execution. Each is documented below with
the easy option that was chosen vs the correct best option, and the fix applied.

| # | Section | Easy Option Chosen | Best Option (Correct) | Impact |
|---|---------|-------------------|----------------------|--------|
| C1 | §16 | Option C — wrapper helpers hiding dual string/int IDs | **Option A** — Unify `BuildPackage.MachineId` + `Job.MachineId` to `int?` FK matching `StageExecution.MachineId` | 🔴 Eliminates entire class of translation bugs; proper FK integrity |
| C2 | §2 | Skipped `ReleasePlateAsync` build-level job completion | Complete build-level Job when plate is released + close stage executions | 🟠 Build jobs linger as active in scheduler after build is done |
| C3 | §10 | Skipped predecessor SVG connector lines | SVG lines connecting predecessor→successor job bars in PartPathView | 🟡 Can't see dependency chains in Part Path view |
| C4 | §11 | Skipped build bar click → details | Click handler + action popover on GanttBuildBar | 🟡 Build bars are display-only, breaks "command center" principle |
| C5 | §15 | Skipped per-tab quick filters ("views already have inline filters") | Toolbar context-aware filters: Gantt conflicts toggle, Builds status filter, Orders overdue toggle | 🟡 Extra clicks, toolbar feels empty on non-Gantt tabs |

### Correction C1 — Machine ID Unification (Option A)

Replace the easy Option C (extension helpers) with the correct Option A: make
`BuildPackage.MachineId` and `Job.MachineId` proper `int?` FK columns matching
`StageExecution.MachineId`. Delete `MachineExtensions.cs`.

**Why Option A is correct:**
- `StageExecution.MachineId` is already `int?` — this is the established pattern
- Proper FK constraints prevent orphaned references
- EF Core navigation properties enable Include/eager loading
- Eliminates ~50 lines of string↔int translation code across 3 services
- Simpler mental model: machine references are always `int` (Machine.Id PK)
- `Machine.MachineId` (string) remains as the user-facing display code

**Note:** `PartStageRequirement.AssignedMachineId` and `ProductionStage.AssignedMachineIds`
stay as strings — these are admin configuration fields where users think in terms of
machine codes ("SLS-01"), not database PKs. The routing/scheduling engine resolves
these to `Machine` objects at runtime, which is correct.

**Changes:**

- [x] `Models/BuildPackage.cs` — Change `MachineId` from `string` to `int?`, add FK + nav
- [x] `Models/Job.cs` — Change `MachineId` from `string?` to `int?`, add FK + nav
- [x] Create migration with data conversion SQL (string→int via Machine join)
- [x] `Services/BuildSchedulingService.cs` — Direct `machine.Id` assignment (simplifies ~15 lines)
- [x] `Services/WorkOrderService.cs` — Remove machineLookup, use int directly
- [x] `Services/JobService.cs` — Remove machineLookup, use int directly
- [x] `Services/SchedulingService.cs` — ResolveMachines stays (routing uses strings), but
  any Job/BuildPackage refs switch to int
- [x] All scheduler modals (WoBuildModal, QuickScheduleModal, RescheduleModal) — int machine IDs
- [x] All scheduler views referencing BuildPackage.MachineId — use int
- [x] Tests — Update Job/BuildPackage creation to use int MachineId
- [x] Delete `Models/MachineExtensions.cs`

**Verification:**
- [x] Build passes
- [x] All 312+ tests pass
- [x] FK constraints in migration validated
- [x] No string MachineId on Job or BuildPackage in codebase

---

### Correction C2 — Build-Level Job Completion on Plate Release

When `ReleasePlateAsync` marks a build as Completed, the build-level Job (if any)
should also be marked Completed. Currently build-level Jobs linger as active.

**Changes:**

- [x] `Services/BuildSchedulingService.cs` — `ReleasePlateAsync`: After marking build
  Completed, find and complete the build-level Job (`package.ScheduledJobId` or Jobs
  linked via `BuildPackageId`)
- [x] If build-level Job has `WorkOrderLineId`, trigger fulfillment chain

**Verification:**
- [x] Build passes
- [x] Release plate → build-level Job status = Completed
- [x] Build-level Job no longer appears as active in Gantt/Table

---

### Correction C3 — PartPathView Predecessor Connector Lines

Add SVG connector lines between predecessor and successor job bars to visualize
the dependency chain in the Part Path view.

**Changes:**

- [x] `Components/Pages/Scheduler/Views/PartPathView.razor` — After rendering job bars,
  draw SVG `<line>` elements connecting predecessor→successor bars
- [x] Line style: dashed gray arrow from predecessor bar's right edge to successor bar's left edge
- [x] Only draw when both jobs are visible in current filter

**Verification:**
- [x] Build passes
- [x] Jobs with PredecessorJobId show connector lines
- [x] Lines respect zoom/scroll positioning
- [x] Lines hidden when predecessor is filtered out

---

### Correction C4 — GanttBuildBar Click → Details/Actions

Build bars on the Gantt should be interactive, not display-only. Clicking a build
bar should show details and status-change actions matching the BuildsView cards.

**Changes:**

- [x] `Components/Pages/Scheduler/Components/GanttBuildBar.razor` — Add `@onclick` handler
- [x] Show popover/dropdown with:
  - Build details (name, machine, parts, duration, WO links)
  - Status-appropriate actions (same as BuildsView: Post-Print, Release Plate, etc.)
- [x] Add `OnBuildAction` EventCallback parameter for parent to handle state changes

**Verification:**
- [x] Build passes
- [x] Click build bar → popover appears with details + actions
- [x] Actions trigger state changes and refresh Gantt

---

### Correction C5 — Toolbar Per-Tab Quick Filters

Add context-aware quick filter controls to the toolbar that change based on
which tab is active.

**Changes:**

- [x] `Components/Pages/Scheduler/Components/SchedulerToolbar.razor`:
  - Gantt tab: "⚠️ Conflicts Only" toggle button
  - Builds tab: Status pill filters (Draft / Ready / Scheduled / Printing)
  - Orders tab: "Overdue Only" toggle button
- [x] Add EventCallback parameters for filter state changes
- [x] `Components/Pages/Scheduler/Index.razor` — Wire filter state to views

**Verification:**
- [x] Build passes
- [x] Gantt conflicts toggle filters to overlapping bars only
- [x] Builds status pills filter pipeline cards
- [x] Orders overdue toggle filters to past-due WOs

---

## Post-Overhaul Verification Checklist

### End-to-End Workflow A (SLS)

- [ ] Create WO with SLS part (qty 76)
- [ ] Release WO → line goes to Pending (no jobs created)
- [ ] Go to Scheduler → Orders tab → see WO with "76 remaining"
- [ ] Click "Quick Schedule" → template match → build created + scheduled in one click
- [ ] See build appear on Builds tab with Scheduled status
- [ ] Start print from Builds tab → status changes to Printing
- [ ] Complete print → Post-Print → Release Plate
- [ ] Per-part jobs auto-created and auto-scheduled → appear on Gantt
- [ ] Complete per-part stages → ProducedQuantity increments on WO line
- [ ] When all 76 produced → WO auto-completes

### End-to-End Workflow B (CNC)

- [ ] Create WO with CNC part (qty 10)
- [ ] Release WO → 1 job created with 10× stage executions, auto-scheduled
- [ ] See job on Gantt with assigned machines and times
- [ ] Complete all stages → Job completes → ProducedQuantity = 10 → WO completes

### End-to-End Workflow C (Mixed WO)

- [ ] Create WO: Line 1 = SLS part (76 qty), Line 2 = CNC part (10 qty)
- [ ] Release → CNC job created, SLS line stays Pending
- [ ] Schedule SLS via Orders tab → build created
- [ ] Both paths complete independently → WO completes when all lines fulfilled

### End-to-End Workflow D (Multi-Build Split)

- [ ] Create WO with SLS part (200 qty)
- [ ] Release → line stays Pending
- [ ] Orders tab shows split suggestion: 76 + 76 + 48
- [ ] Create 3 builds, schedule each
- [ ] Complete all 3 → ProducedQuantity = 200 → WO completes

---

## Section Dependency Graph

```
Section 1 (WO Release Fix) ──────────┐
Section 2 (Fulfillment Chain) ────────┤ ← Foundation (fix first)
Section 3 (Job Explosion Fix) ────────┤
Section 4 (Auto-Schedule Release) ────┘
    │
    ├── Section 5 (Plate Release Trigger)     ← Independent
    ├── Section 6 (PostPrint in Scheduler)    ← Independent
    ├── Section 7 (WoBuildModal Workflow)      ← Depends on 1
    ├── Section 8 (Build Splitting)           ← Depends on 3
    │
    ├── Section 9 (BuildsView GanttViewport)  ← UI: Independent
    ├── Section 10 (PathView GanttViewport)   ← UI: Independent
    ├── Section 11 (Gantt Build Bars)         ← UI: Independent
    ├── Section 12 (WO View Actions)          ← UI: Depends on 1, 7
    ├── Section 13 (Stages Capacity)          ← UI: Independent
    ├── Section 14 (Table Bulk)               ← UI: Independent
    ├── Section 15 (Toolbar Context)          ← UI: After 9, 10
    │
    └── Section 16 (Machine ID)               ← Cleanup: Any time
```

**Recommended execution order:**
1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12 → 13 → 14 → 15 → 16

Sections 5-8 (service fixes) can be parallelized if working with multiple developers.
Sections 9-15 (UI improvements) can be done in any order after sections 1-4.

---

## Line Count Estimates

| Section | Files Changed | Est. Lines Changed | Risk |
|---------|--------------|-------------------|------|
| 1. WO Release | 1 service | ~20 | Low |
| 2. Fulfillment Chain | 2 services | ~30 | Medium |
| 3. Job Explosion | 1 service | ~40 | Medium |
| 4. Auto-Schedule Release | 1 service | ~15 | Low |
| 5. Plate Release Trigger | 1 model, 1 service, 1 migration | ~20 | Low |
| 6. PostPrint in Scheduler | 1 view component | ~30 | Low |
| 7. WoBuildModal Workflow | 1 modal | ~50 | Medium |
| 8. Build Splitting | 1 view component | ~50 | Medium |
| 9. BuildsView Viewport | 1 view component | ~80 | Medium |
| 10. PathView Viewport | 1 view component | ~60 | Medium |
| 11. Gantt Build Bars | 2 components + Index | ~75 | Medium |
| 12. WO View Actions | 1 view component | ~80 | Medium |
| 13. Stages Capacity | 1 view component | ~70 | Low |
| 14. Table Bulk | 1 view component | ~60 | Low |
| 15. Toolbar Context | 1 component | ~20 | Low |
| 16. Machine ID | ~5 files | ~40 | Low |
| **Total** | | **~740** | |

---

## Notes

- **No schema changes required** for Sections 1-4 (service logic only) except Section 5
  which adds one column via migration.
- **The Builds page (`/builds`) remains** for detailed slicer data entry and build template
  certification. The scheduler provides quick actions but the Builds page is the full
  build management interface.
- **Drag-and-drop scheduling** is deferred to a future phase (per `SCHEDULER_OVERHAUL_PLAN.md`
  Phase E). This plan focuses on making the auto + manual scheduling paths work correctly.
- **Performance**: After Section 3 (job batching), the per-part job count drops dramatically
  (76× fewer jobs + stage executions per build). This is the biggest perf win.
- **REFACTOR_PLAN.md Phase 2** (PartAdditiveBuildConfig) is already COMPLETE — the model
  and data are in place. This plan builds on that foundation.
