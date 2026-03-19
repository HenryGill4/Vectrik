# Parts & Scheduling Architecture Plan

> **Created**: 2026-03-20
> **Status**: ✅ IMPLEMENTED — All 3 phases complete, build verified
> **Purpose**: Unify job generation, fix scheduling bugs, add Part Path visibility.
> **Scope**: Only Parts + Scheduling hardening. No new features until this is complete.
> **Directive**: Saved to `.github/copilot-instructions.md` — "User wants to focus on
> perfecting the Part system and Scheduling system before adding any new features."

---

## Table of Contents

1. [Problem Statement](#problem-statement)
2. [Current Architecture (As-Is)](#current-architecture-as-is)
3. [Target Architecture (To-Be)](#target-architecture-to-be)
4. [Issue Registry (7 Issues Found)](#issue-registry)
5. [Phase A — Foundation: Unify Job Generation](#phase-a--foundation-unify-job-generation)
6. [Phase B — Part Path View in Scheduler](#phase-b--part-path-view-in-scheduler)
7. [Phase C — Supporting Fixes](#phase-c--supporting-fixes)
8. [File Impact Matrix](#file-impact-matrix)
9. [Verification Checklist](#verification-checklist)
10. [Out of Scope](#out-of-scope)

---

## Problem Statement

The Parts system is solid (model, service, 3 UI pages — no code changes needed).
The Scheduling system has **7 issues** (2 critical, 3 important, 1 moderate, 1 minor)
identified during a deep audit of ~20 files across models, services, and UI.

The most critical issue: **two conflicting job generation paradigms** that produce
different data shapes depending on whether work enters the system via Work Orders
or the Scheduler UI. This makes consistent reporting, visualization, and scheduling
impossible.

The highest-visibility gap: **no way to see a part's complete journey through
production stages** on the scheduler. Managers and admins need this for daily
production management.

---

## Current Architecture (As-Is)

### Data Flow

```
Part → PartStageRequirement[] (routing definition)
  → Job generation (TWO CONFLICTING PATHS)
    → Job[] → StageExecution[] → Machine assignment + time slots
```

### The Two Conflicting Job Generation Paths

#### Path 1: Work Order Release (BROKEN)
**File**: `Services/WorkOrderService.cs` → `GenerateJobsForLineAsync` (lines 188-273)

Creates **N separate Jobs** — one per routing stage — chained via `PredecessorJobId`.

```
WO Line (Part XYZ, qty 50)
  ├── Job #1 (SLS Printing)      PredecessorJobId = null
  │     └── StageExecution #1
  ├── Job #2 (Depowdering)       PredecessorJobId = Job#1.Id
  │     └── StageExecution #2
  ├── Job #3 (CNC Machining)     PredecessorJobId = Job#2.Id
  │     └── StageExecution #3
  └── Job #4 (QC)                PredecessorJobId = Job#3.Id
        └── StageExecution #4
```

**Problems with this approach**:
- `Job.Quantity` / `ProducedQuantity` / `DefectQuantity` are duplicated across N jobs
  for the same production run — which Job is the "real" quantity tracker?
- `Job.EstimatedHours` on each job is only 1 stage's hours, not the total
- `Job.ScheduledStart` / `ScheduledEnd` per job is 1 stage window, not the part's
  full production window
- N+1 `SaveChangesAsync` calls: 2 per routing step (line 233 + line 260) +
  1 `AutoScheduleJobAsync` per job (line 263) = 3 DB round-trips per stage
- `PredecessorJobId` is misused for intra-routing sequencing — should be reserved
  for inter-job dependencies
- `SchedulingService.AutoScheduleJobAsync` is called N times instead of once
- The scheduler Gantt sees N separate single-stage jobs instead of 1 multi-stage job

#### Path 2: Scheduler UI "Create Job" (CORRECT)
**File**: `Services/JobService.cs` → `CreateJobAsync` (lines 58-153)

Creates **1 Job with N StageExecutions** inside it.

```
WO Line (Part XYZ, qty 50)
  └── Job #1 (Part XYZ production)
        ├── StageExecution #1 (SLS Printing,   SortOrder=1)
        ├── StageExecution #2 (Depowdering,    SortOrder=2)
        ├── StageExecution #3 (CNC Machining,  SortOrder=3)
        └── StageExecution #4 (QC,             SortOrder=4)
```

**Why this is correct**:
- Job = "produce this quantity of this part" — single source of truth for qty
- `Job.EstimatedHours` = sum of all stages
- `Job.ScheduledStart` / `ScheduledEnd` = first stage start → last stage end
- Single `SaveChangesAsync` for all StageExecutions (line 145)
- Single `AutoScheduleJobAsync` call (line 148) which iterates stages internally
- `SchedulingService.AutoScheduleJobAsync` already handles sequential stage
  scheduling via `cursor` variable (line 54-99)
- Part Path view can group by JobId to show the complete journey

### Scheduling Engine (Solid)
**File**: `Services/SchedulingService.cs` (408 lines)

The scheduling engine already handles the correct pattern well:
- `AutoScheduleJobAsync` (lines 17-115): loads job + stages, resolves predecessor
  chain, iterates stages sequentially with a cursor, finds best machine per stage
- `ResolveMachines` (lines 269-328): 4-tier machine preference resolution
  (specific → preferred → stage-capable → default)
- `FindEarliestSlotOnMachine` (lines 232-267): shift-aware gap-finding
- `SnapToNextShiftStart` (lines 330-358): aligns to shift boundaries
- `AdvanceByWorkHours` (lines 360-407): advances through work hours only

### Existing UI Components

| Component | Location | What it shows |
|-----------|----------|---------------|
| **Machine Gantt** | `Scheduler/Index.razor` (lines 59-194) | Rows = machines, bars = StageExecutions. Priority-colored. Build-plate grouping. |
| **Table View** | `Scheduler/Index.razor` (lines 227-306) | Flat list of all StageExecutions with machine, timing, status. |
| **Job Detail Timeline** | `WorkOrders/JobDetail.razor` (lines 56-127) | Vertical stage flow with ✅🔄❌⏭️⬜ markers, timing, operator, QC status. |
| **Part Tracker** | `Tracking/Index.razor` (lines 1-120) | Search by WO/Part/Serial. Shows serialized part history (after-the-fact). |
| **Capacity Dashboard** | `Scheduler/Capacity.razor` | OEE summary + utilization bars per machine. |

### What's Missing

**No "Part Path" view** — nowhere can a manager see "Part XYZ needs to go through
5 stages, here's where each is scheduled, on which machines, what's the current
status — all on a timeline." The data exists but is only viewable machine-by-machine
(Gantt) or job-by-job (buried in WO → Job → Detail navigation).

---

## Target Architecture (To-Be)

### Unified Data Model

```
WorkOrder
  └── WorkOrderLine (PartId, Quantity)
        └── Job (1 per line — "produce this quantity of this part")
              └── StageExecution[] (N per job — one per routing step)
                    ├── ProductionStageId (which stage: SLS, CNC, etc.)
                    ├── MachineId (int? — assigned machine)
                    ├── ScheduledStartAt / ScheduledEndAt (time slot)
                    ├── SortOrder (sequence within the job)
                    └── Status (NotStarted → InProgress → Completed)
```

### Key Principles

1. **Job = unit of production** — one part, one quantity, flows through N stages
2. **StageExecution = unit of scheduling** — one task on one machine at one time
3. **PredecessorJobId = inter-job dependency only** — "Job B can't start until
   Job A finishes" (different WO lines, different parts). NOT for sequencing
   stages within the same routing.
4. **Job.MachineId** — becomes a "primary machine" hint or is deprecated. Each
   stage has its own machine via StageExecution.MachineId.
5. **Job.EstimatedHours** — total across all stages
6. **Job.ScheduledStart/End** — first stage start → last stage end (set by
   `AutoScheduleJobAsync` lines 102-111)

### Scheduler View Modes (3 total)

```
┌──────────────────────────────────────────────────────────┐
│  📊 Gantt (Machine)  │  🔀 Part Path  │  📋 Table       │
└──────────────────────────────────────────────────────────┘
```

1. **📊 Gantt (Machine View)** — existing. Rows = machines, bars = StageExecutions.
   Color = priority. Answers: "What's each machine doing?"
2. **🔀 Part Path (NEW)** — Rows = Jobs, bars = StageExecutions. Color = StageColor.
   Answers: "Where is each part in its production journey?"
3. **📋 Table** — existing. Flat list of all executions.

---

## Issue Registry

All issues found during deep audit. Each has a fix mapped to a Phase below.

| # | Severity | Issue | Location | Phase |
|---|----------|-------|----------|-------|
| **1** | 🔴 CRITICAL | Two conflicting job generation paradigms — WO creates 1-job-per-stage, Scheduler creates 1-job-all-stages | `WorkOrderService.GenerateJobsForLineAsync` (lines 188-273) | A |
| **2** | 🔴 CRITICAL | N+1 SaveChangesAsync in WO job generation — 3 DB round-trips per routing step | `WorkOrderService.GenerateJobsForLineAsync` (lines 233, 260, 263) | A |
| **3** | 🟡 IMPORTANT | Hardcoded "System" in Scheduler CreateJob — should use authenticated user | `Scheduler/Index.razor` (lines 585-586) | B |
| **4** | 🟡 IMPORTANT | Capacity calculation ignores date range — counts ALL non-completed work, inflating utilization | `StageService.GetMachineCapacityAsync` (lines 525-530) | C |
| **5** | 🟡 IMPORTANT | Silent failure when WO line has no routing — returns empty list, no user feedback | `WorkOrderService.GenerateJobsForLineAsync` (line 201-202) | A |
| **6** | 🟠 MODERATE | Machine ID type mismatch — `Job.MachineId` = string, `StageExecution.MachineId` = int | `Models/Job.cs` line 17, `Models/StageExecution.cs` line 22 | DEFERRED |
| **7** | 🔵 MINOR | Overnight shift handling — `SnapToNextShiftStart` and `AdvanceByWorkHours` don't handle `EndTime < StartTime` | `SchedulingService.cs` (lines 330-407) | C |

---

## Phase A — Foundation: Unify Job Generation

> **Priority**: HIGHEST — must be done first. Phase B cannot work correctly until
> job generation produces consistent data shapes.

### A.1 — Rewrite `WorkOrderService.GenerateJobsForLineAsync`

**File**: `Services/WorkOrderService.cs`

**Current** (lines 188-273): Creates N Jobs with 1 StageExecution each, chained
via PredecessorJobId, with N+1 SaveChangesAsync calls.

**Target**: Match the pattern from `JobService.CreateJobAsync` (lines 58-153):
1. Create 1 Job for the WO line
2. Load routing (`PartStageRequirements` ordered by `ExecutionOrder`)
3. Build machine lookup (string → int ID)
4. Loop routing → create StageExecution per step (no Save inside loop)
5. Single `SaveChangesAsync` (all StageExecutions in one batch)
6. Single `AutoScheduleJobAsync` call
7. Return the single Job (not `List<Job>`)

**Pseudocode**:
```csharp
public async Task<Job> GenerateJobForLineAsync(int workOrderLineId, string createdBy)
{
    var line = await _db.WorkOrderLines
        .Include(l => l.Part)
        .FirstOrDefaultAsync(l => l.Id == workOrderLineId)
        ?? throw new InvalidOperationException("Work order line not found.");

    var routing = await _db.PartStageRequirements
        .Include(r => r.ProductionStage)
        .Where(r => r.PartId == line.PartId && r.IsActive)
        .OrderBy(r => r.ExecutionOrder)
        .ToListAsync();

    if (routing.Count == 0)
        throw new InvalidOperationException(
            $"Part '{line.Part.PartNumber}' has no active routing. " +
            "Add stage requirements in Parts → Edit before generating jobs.");

    var machineLookup = await _db.Machines
        .ToDictionaryAsync(m => m.MachineId, m => m.Id);

    // Single job for the entire routing
    var totalEstHours = routing.Sum(r =>
        r.EstimatedHours ?? r.ProductionStage.DefaultDurationHours);

    var job = new Job
    {
        JobNumber = await _numberSeq.NextAsync("Job"),
        PartId = line.PartId,
        WorkOrderLineId = line.Id,
        PartNumber = line.Part.PartNumber,
        SlsMaterial = line.Part.Material,
        Quantity = line.Quantity,
        EstimatedHours = totalEstHours,
        Status = JobStatus.Draft,
        Priority = JobPriority.Normal,
        CreatedBy = createdBy,
        LastModifiedBy = createdBy,
        Notes = $"Auto-generated for {line.Part.PartNumber}"
    };

    _db.Jobs.Add(job);
    await _db.SaveChangesAsync(); // Get job.Id

    // Create all StageExecutions in one loop (no Save per iteration)
    foreach (var stage in routing)
    {
        int? machineIntId = null;
        if (!string.IsNullOrEmpty(stage.AssignedMachineId)
            && machineLookup.TryGetValue(stage.AssignedMachineId, out var smid))
        {
            machineIntId = smid;
        }

        _db.StageExecutions.Add(new StageExecution
        {
            JobId = job.Id,
            ProductionStageId = stage.ProductionStageId,
            SortOrder = stage.ExecutionOrder,
            EstimatedHours = stage.EstimatedHours
                ?? stage.ProductionStage.DefaultDurationHours,
            EstimatedCost = stage.EstimatedCost,
            MaterialCost = stage.MaterialCost,
            SetupHours = stage.SetupTimeMinutes.HasValue
                ? stage.SetupTimeMinutes.Value / 60.0 : null,
            QualityCheckRequired = stage.ProductionStage.RequiresQualityCheck,
            MachineId = machineIntId,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy
        });
    }

    await _db.SaveChangesAsync(); // Single batch save for all stages

    // Auto-schedule: assign machines + time slots
    await _scheduler.AutoScheduleJobAsync(job.Id);

    // Reload to get updated schedule times
    await _db.Entry(job).ReloadAsync();

    return job;
}
```

**DB calls**: 2 SaveChangesAsync (job create + batch stages) + 1 inside
AutoScheduleJobAsync = 3 total. Down from 3×N (was 3 per routing step).

### A.2 — Update Interface

**File**: `Services/IWorkOrderService.cs`

```csharp
// OLD:
Task<List<Job>> GenerateJobsForLineAsync(int workOrderLineId, string createdBy);

// NEW:
Task<Job> GenerateJobForLineAsync(int workOrderLineId, string createdBy);
```

Note the method rename: `GenerateJobsForLineAsync` → `GenerateJobForLineAsync`
(singular). This communicates the 1-job-per-line intent.

### A.3 — Update Callers

#### Caller 1: `WorkOrderService.UpdateStatusAsync` (line 142)

**File**: `Services/WorkOrderService.cs` (lines 137-143)

```csharp
// OLD:
foreach (var line in wo.Lines)
{
    var hasJobs = await _db.Jobs.AnyAsync(j => j.WorkOrderLineId == line.Id);
    if (!hasJobs)
        await GenerateJobsForLineAsync(line.Id, updatedBy);
}

// NEW:
foreach (var line in wo.Lines)
{
    var hasJobs = await _db.Jobs.AnyAsync(j => j.WorkOrderLineId == line.Id);
    if (!hasJobs)
        await GenerateJobForLineAsync(line.Id, updatedBy);
}
```

Note: The outer `SaveChangesAsync` on line 146 is now redundant for job data
(the rewritten method does its own saves), but still needed for the WO status
update itself. Keep it.

#### Caller 2: `WorkOrders/Details.razor` (line 406)

**File**: `Components/Pages/WorkOrders/Details.razor` (lines 402-411)

```razor
// OLD:
private async Task GenerateJobs(int lineId)
{
    try
    {
        var jobs = await WorkOrderService.GenerateJobsForLineAsync(lineId, "System");
        Toast.ShowSuccess($"{jobs.Count} job(s) generated from routing");
        await LoadOrder();
    }
    catch (Exception ex) { Toast.ShowError(ex.Message); }
}

// NEW:
private async Task GenerateJobs(int lineId)
{
    try
    {
        var job = await WorkOrderService.GenerateJobForLineAsync(lineId, _userName);
        Toast.ShowSuccess($"Job {job.JobNumber} created with {job.Stages.Count} stages");
        await LoadOrder();
    }
    catch (Exception ex) { Toast.ShowError(ex.Message); }
}
```

Also needs: capture authenticated user name (replace "System" with `_userName`).
Check if this page already captures auth state — if not, add
`[CascadingParameter] Task<AuthenticationState>? AuthState { get; set; }` and
resolve username in `OnInitializedAsync`.

### A.4 — Routing Validation (Issue #5)

Already handled in A.1 above — the rewritten method throws an
`InvalidOperationException` with a helpful message instead of silently returning
an empty list. The UI caller wraps in try-catch and shows the toast error.

---

## Phase B — Part Path View in Scheduler

> **Priority**: HIGH — the main visibility feature for managers/admins.
> Depends on Phase A being complete (consistent 1-job-N-stages data).

### B.1 — Add View Mode Toggle

**File**: `Components/Pages/Scheduler/Index.razor`

Add `"path"` to the existing view toggle (lines 45-48):

```razor
<div class="sched-view-toggle">
    <button class="btn btn-sm @(_viewMode == "gantt" ? "btn-primary" : "btn-secondary")"
            @onclick='() => _viewMode = "gantt"'>📊 Machines</button>
    <button class="btn btn-sm @(_viewMode == "path" ? "btn-primary" : "btn-secondary")"
            @onclick='() => _viewMode = "path"'>🔀 Part Path</button>
    <button class="btn btn-sm @(_viewMode == "table" ? "btn-primary" : "btn-secondary")"
            @onclick='() => _viewMode = "table"'>📋 Table</button>
</div>
```

### B.2 — Part Path Gantt Rendering

**File**: `Components/Pages/Scheduler/Index.razor`

New `else if (_viewMode == "path")` block. Reuses existing `_executions` data,
`_ganttDays`, `GetGanttPosition`, `GetGanttWidth` helpers. Just groups differently.

**Layout**:
```
┌──────────────────────┬──────────────────────────────────────────────────┐
│   Job / Part         │ Mon 6/2    Tue 6/3    Wed 6/4    Thu 6/5       │
├──────────────────────┼──────────────────────────────────────────────────┤
│ P-1001 · Widget      │ [■ SLS ■]──[■ Depow ■]──[■ CNC ■]──[■ QC ■]  │
│ J-2025-042           │  Mach-1     Mach-3      Mach-5     Manual      │
│ ⚡Rush  50 qty       │                                                │
├──────────────────────┼──────────────────────────────────────────────────┤
│ P-1003 · Bracket     │       [■ SLS ■]────────[■ QC ■]               │
│ J-2025-045           │        Mach-2           Manual                 │
│ Normal  100 qty      │                                                │
└──────────────────────┴──────────────────────────────────────────────────┘
```

**Key rendering decisions**:

1. **Row grouping**: Group `_executions` by `JobId`. Each group = one row.
   Sort rows by earliest `ScheduledStartAt` across the group (default),
   with dropdown options for Priority, Part Number, Due Date.

2. **Left label** (180px, matching machine view):
   - Line 1: Part number + Part name (bold, truncated)
   - Line 2: Job number (link to JobDetail page)
   - Line 3: Priority badge + quantity + WO number (if linked)

3. **Bar coloring**: Use `ProductionStage.StageColor` (each stage type has a
   distinct color). This differs from the machine view which uses priority colors.
   Rationale: in Part Path view the user cares about *what work* is happening,
   not urgency — they can see priority in the left label.

4. **Bar content**: Stage name (e.g., "SLS", "CNC"). Machine name shown as
   small text below the bar or in tooltip.

5. **Status styling on bars**:
   - Completed: solid fill, slight opacity reduction (0.7), no border
   - In Progress: solid fill, subtle pulse animation (reuse `gantt-pulse-dot`)
   - Not Started: semi-transparent fill with dashed border
   - Failed: red border, ❌ marker
   - Skipped: gray with strikethrough

6. **Connectors**: Thin line (`::after` pseudo-element) connecting the right
   edge of each bar to the left edge of the next bar in sort order. Color =
   `var(--border)`. Creates the visual "flow" between stages.

7. **Today marker**: Same red vertical line as machine view.

8. **Overdue rows**: If `Job.IsOverdue`, the row background gets
   `rgba(var(--danger-rgb), 0.06)`.

9. **Click behavior**: Clicking a bar opens the existing reschedule modal.
   Clicking the job label navigates to `/workorders/{woId}/jobs/{jobId}`.

10. **Tooltip**: Full details — stage name, machine, scheduled times,
    estimated hours, status, operator (if assigned).

### B.3 — Part Path Filter Bar

Add an optional filter section above the Part Path Gantt:

```razor
@if (_viewMode == "path")
{
    <div style="display:flex; gap:8px; margin-bottom:8px; flex-wrap:wrap;">
        <select class="form-control" style="width:auto;" @bind="_pathFilterPartId">
            <option value="0">All Parts</option>
            @foreach (var p in _parts ?? [])
            {
                <option value="@p.Id">@p.PartNumber — @p.Name</option>
            }
        </select>
        <select class="form-control" style="width:auto;" @bind="_pathFilterPriority">
            <option value="">All Priorities</option>
            @foreach (var p in Enum.GetValues<JobPriority>())
            {
                <option value="@p">@p</option>
            }
        </select>
        <select class="form-control" style="width:auto;" @bind="_pathSortMode">
            <option value="schedule">Sort: Schedule</option>
            <option value="priority">Sort: Priority</option>
            <option value="part">Sort: Part Number</option>
        </select>
    </div>
}
```

**State variables to add**:
```csharp
private int _pathFilterPartId;
private string? _pathFilterPriority;
private string _pathSortMode = "schedule";
```

### B.4 — Fix Hardcoded "System" in CreateJob (Issue #3)

**File**: `Components/Pages/Scheduler/Index.razor`

Add auth state capture:
```csharp
[CascadingParameter]
private Task<AuthenticationState>? AuthState { get; set; }

private string _userName = "System";

protected override async Task OnInitializedAsync()
{
    if (AuthState != null)
    {
        var state = await AuthState;
        _userName = state.User?.Identity?.Name ?? "System";
    }
    // ... existing init code
}
```

Update `CreateJob()` (lines 584-586):
```csharp
// OLD:
_newJob.CreatedBy = "System";
_newJob.LastModifiedBy = "System";

// NEW:
_newJob.CreatedBy = _userName;
_newJob.LastModifiedBy = _userName;
```

### B.5 — CSS for Part Path View

**File**: `wwwroot/css/site.css`

New CSS classes (append after the existing Gantt section, ~line 1172):

```css
/* ── Part Path View ── */
.gantt-path-label {
    padding: 8px 12px;
    font-size: 0.78rem;
    display: flex;
    flex-direction: column;
    gap: 2px;
    border-right: 1px solid var(--border);
    min-width: 0;
}

.gantt-path-part {
    font-weight: 600;
    font-size: 0.82rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.gantt-path-job {
    font-size: 0.72rem;
    color: var(--accent);
    text-decoration: none;
}

.gantt-path-job:hover {
    text-decoration: underline;
}

.gantt-path-meta {
    font-size: 0.7rem;
    color: var(--text-muted);
    display: flex;
    align-items: center;
    gap: 6px;
}

/* Stage-colored bars (use StageColor via --bar-accent) */
.gantt-bar-stage {
    background: var(--bar-accent, var(--accent));
    border-left: 3px solid color-mix(in srgb, var(--bar-accent, var(--accent)), black 20%);
}

/* Status modifiers for path view bars */
.gantt-bar-completed {
    opacity: 0.6;
}

.gantt-bar-inprogress {
    animation: gantt-pulse-dot 2s infinite;
}

.gantt-bar-notstarted {
    opacity: 0.5;
    border: 2px dashed var(--bar-accent, var(--accent));
    background: transparent;
    color: var(--text-secondary);
    text-shadow: none;
}

.gantt-bar-failed {
    outline: 2px solid var(--danger);
    outline-offset: 1px;
}

/* Machine label on path view bars */
.gantt-bar-machine {
    font-size: 0.6rem;
    opacity: 0.8;
    margin-left: 4px;
    font-weight: 400;
}

/* Overdue row */
.gantt-row-overdue {
    background: rgba(239, 68, 68, 0.06);
}

/* Stage color legend for path view */
.gantt-path-legend {
    display: flex;
    gap: 12px;
    flex-wrap: wrap;
    margin-top: 8px;
    padding: 8px 0;
    font-size: 0.75rem;
    color: var(--text-muted);
}

.gantt-path-legend-swatch {
    display: inline-block;
    width: 12px;
    height: 12px;
    border-radius: 3px;
    margin-right: 4px;
    vertical-align: middle;
}
```

---

## Phase C — Supporting Fixes

### C.1 — Fix Capacity Date Range (Issue #4)

**File**: `Services/StageService.cs` — `GetMachineCapacityAsync` (lines 525-530)

**Current** (line 525-530):
```csharp
var scheduledWork = await _db.StageExecutions
    .Where(e => e.MachineId != null
        && e.Status != StageExecutionStatus.Completed
        && e.Status != StageExecutionStatus.Skipped
        && e.Status != StageExecutionStatus.Failed)
    .ToListAsync();
```

**Fixed**: Add date range filter to match the `from`/`to` parameters:
```csharp
var scheduledWork = await _db.StageExecutions
    .Where(e => e.MachineId != null
        && e.Status != StageExecutionStatus.Completed
        && e.Status != StageExecutionStatus.Skipped
        && e.Status != StageExecutionStatus.Failed
        && e.ScheduledEndAt > from
        && e.ScheduledStartAt < to)
    .ToListAsync();
```

This ensures only work that overlaps the requested date range is counted.
Previously, a machine with 100 hours of future work scheduled would show
inflated utilization even when viewing the current week.

### C.2 — Overnight Shift Handling (Issue #7)

**File**: `Services/SchedulingService.cs`

**The bug**: If `OperatingShift` has `StartTime = 22:00` and `EndTime = 06:00`
(overnight shift), then `shiftEnd = checkDate + EndTime` puts the end BEFORE
the start on the same calendar day.

**Both affected methods**:

1. `SnapToNextShiftStart` (lines 330-358) — Line 347 `var shiftEnd = checkDate + shift.EndTime`
   would be `checkDate + 06:00` = 6am, which is before `shiftStart` = 10pm.
   The `if (from <= shiftEnd)` check on line 350 breaks.

2. `AdvanceByWorkHours` (lines 360-407) — Same issue at line 383.

**Fix**: Detect overnight and add 1 day to the end:
```csharp
var shiftStart = checkDate + shift.StartTime;
var shiftEnd = checkDate + shift.EndTime;
if (shift.EndTime <= shift.StartTime)
    shiftEnd = shiftEnd.AddDays(1); // overnight shift
```

Apply this pattern in both methods.

---

## File Impact Matrix

| File | Phase | Changes |
|------|-------|---------|
| `Services/IWorkOrderService.cs` | A | Rename method, change return type `List<Job>` → `Job` |
| `Services/WorkOrderService.cs` | A | Rewrite `GenerateJobsForLineAsync` → `GenerateJobForLineAsync`, update `UpdateStatusAsync` caller |
| `Components/Pages/WorkOrders/Details.razor` | A | Update `GenerateJobs` to use new signature, capture auth user |
| `Components/Pages/Scheduler/Index.razor` | B | Add "path" view mode, Part Path Gantt rendering, filter bar, fix hardcoded "System" |
| `wwwroot/css/site.css` | B | Add Part Path CSS classes (~50 lines) |
| `Services/StageService.cs` | C | Fix date range filter in `GetMachineCapacityAsync` |
| `Services/SchedulingService.cs` | C | Fix overnight shift in `SnapToNextShiftStart` + `AdvanceByWorkHours` |

**Files NOT changed** (confirmed solid):
- `Models/Job.cs` — no schema changes needed
- `Models/StageExecution.cs` — no schema changes needed
- `Models/Part.cs` — solid
- `Models/PartStageRequirement.cs` — solid
- `Models/ProductionStage.cs` — solid
- `Services/PartService.cs` — solid
- `Services/JobService.cs` — already correct pattern (no changes)
- `Services/ISchedulingService.cs` — interface unchanged
- `Services/IStageService.cs` — interface unchanged

---

## Verification Checklist

### After Phase A ✅ IMPLEMENTED

- [x] A.1: Build passes after all Phase A changes
- [ ] A.2: Create a WO with 2 lines, each part having 3+ routing stages
- [ ] A.3: Release the WO → verify 1 Job per line (not N jobs per line)
- [ ] A.4: Each Job has N StageExecutions matching the part's routing
- [ ] A.5: StageExecutions have correct SortOrder, EstimatedHours, MachineId
- [ ] A.6: Jobs are auto-scheduled (ScheduledStart/End populated)
- [ ] A.7: StageExecutions have ScheduledStartAt/EndAt from auto-scheduler
- [ ] A.8: No PredecessorJobId set (intra-routing uses SortOrder now)
- [x] A.9: WO Details page shows correct toast ("Job J-xxx created with N stages")
- [x] A.10: WO line with no routing shows error toast (not silent failure)
- [x] A.11: Existing scheduler "Create Job" still works (JobService.CreateJobAsync unchanged)
- [ ] A.12: Scheduler Gantt shows jobs correctly (bars appear on correct machines)

### After Phase B ✅ IMPLEMENTED

- [x] B.1: Build passes after all Phase B changes
- [x] B.2: Scheduler has 3 view mode buttons: Machines, Part Path, Table
- [x] B.3: Part Path view shows rows grouped by Job
- [x] B.4: Each row shows Part#, Job#, Priority badge, Quantity
- [x] B.5: Bars are colored by StageColor (not priority)
- [x] B.6: Completed stages are dimmed, In Progress pulse, Not Started dashed
- [x] B.7: Clicking a bar opens reschedule modal
- [x] B.8: Clicking job label navigates to JobDetail page
- [x] B.9: Part filter works (shows only jobs for selected part)
- [x] B.10: Priority filter works
- [x] B.11: Sort modes work (Schedule, Priority, Part Number)
- [x] B.12: Today marker appears on Part Path view
- [x] B.13: Overdue jobs have red-tinted row background
- [x] B.14: "Create Job" uses authenticated user name (not "System")
- [ ] B.15: Build-plate grouped executions display correctly in path view

### After Phase C ✅ IMPLEMENTED

- [x] C.1: Build passes after all Phase C changes
- [ ] C.2: Capacity page shows utilization based on selected date range only
- [ ] C.3: Changing date range on Capacity page updates utilization numbers
- [ ] C.4: Overnight shifts (e.g., 22:00-06:00) schedule correctly
- [ ] C.5: AdvanceByWorkHours correctly spans overnight shift boundaries

---

## Out of Scope

These items are explicitly **NOT** part of this plan:

| Item | Reason |
|------|--------|
| **FAIR Forms (Chunk 14)** | Feature work paused per user directive |
| **Phase 2 modules** (Job Costing, Time Clock, etc.) | Feature work paused |
| **Machine ID type mismatch** (`Job.MachineId` string vs `StageExecution.MachineId` int) | Larger schema migration, not blocking functionality |
| **Schema/migration changes** | No model changes needed for any of the 3 phases |
| **New models or services** | All changes are within existing files |
| **Part Tracker enhancement** | Existing Tracking page is functional for serial tracking |

---

## Implementation Order

```
Phase A (Foundation)    ──── Must be first. ~1 session.
  │
  ├── A.1: Rewrite GenerateJobsForLineAsync
  ├── A.2: Update IWorkOrderService interface
  ├── A.3: Update callers (UpdateStatusAsync + Details.razor)
  └── A.4: Routing validation (handled in A.1)
  │
  ▼  Verify: Build + checklist A.1-A.12
  │
Phase B (Part Path UI)  ──── Depends on A. ~1-2 sessions.
  │
  ├── B.1: View mode toggle
  ├── B.2: Part Path Gantt rendering
  ├── B.3: Filter bar
  ├── B.4: Fix hardcoded "System"
  └── B.5: CSS
  │
  ▼  Verify: Build + checklist B.1-B.15
  │
Phase C (Polish)        ──── Independent fixes. ~1 session.
  │
  ├── C.1: Capacity date range fix
  └── C.2: Overnight shift handling
  │
  ▼  Verify: Build + checklist C.1-C.5
```

---

## Reference: Key File Locations

| What | Where | Lines |
|------|-------|-------|
| WO job generation (BROKEN — rewrite target) | `Services/WorkOrderService.cs` | 188-273 |
| WO status → auto job generation | `Services/WorkOrderService.cs` | 117-148 |
| WO interface | `Services/IWorkOrderService.cs` | 21 |
| WO Details page (caller) | `Components/Pages/WorkOrders/Details.razor` | 402-411 |
| Job creation (CORRECT pattern) | `Services/JobService.cs` | 58-153 |
| Job interface | `Services/IJobService.cs` | 11 |
| Scheduling engine | `Services/SchedulingService.cs` | 17-115 |
| Machine resolution | `Services/SchedulingService.cs` | 269-328 |
| Shift snapping | `Services/SchedulingService.cs` | 330-358 |
| Work hours advance | `Services/SchedulingService.cs` | 360-407 |
| Scheduler UI (Gantt + Table) | `Components/Pages/Scheduler/Index.razor` | 1-597 |
| Scheduler CreateJob (hardcoded System) | `Components/Pages/Scheduler/Index.razor` | 575-596 |
| Job Detail stage timeline | `Components/Pages/WorkOrders/JobDetail.razor` | 56-127 |
| Part Tracker | `Components/Pages/Tracking/Index.razor` | 1-120 |
| Capacity calculation (date bug) | `Services/StageService.cs` | 515-573 |
| Gantt CSS | `wwwroot/css/site.css` | 932-1172 |
| Scheduler layout CSS | `wwwroot/css/site.css` | 1174-1330 |
| Stage timeline CSS | `wwwroot/css/site.css` | 888-906 |
| Job model | `Models/Job.cs` | 1-85 |
| StageExecution model | `Models/StageExecution.cs` | 1-100 |
| OperatingShift model | `Models/OperatingShift.cs` | 1-22 |
| ProductionStage model | `Models/ProductionStage.cs` | 1-100 |
