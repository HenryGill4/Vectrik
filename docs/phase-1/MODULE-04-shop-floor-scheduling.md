# Module 04: Shop Floor Management & Scheduling

## Status: [ ] Not Started
## Category: MES
## Phase: 1 — Core Production Engine
## Priority: P1 - Critical

---

## Overview

Shop Floor Management is the operational heart of the MES. Operators interact with
their current stage assignments, log time, record results, and move parts through
production. The Scheduler provides managers with a Gantt-based capacity view with
drag-to-reschedule and finite-capacity optimization.

**ProShop Improvements**: Finite-capacity constraint-based scheduling (vs. simple
drag-and-drop), real-time disruption rescheduling, IoT machine monitoring integration,
lights-out/unmanned job tracking, and a what-if simulation mode.

---

## Current Foundation Assessment

| Item | Status | Location |
|------|--------|----------|
| `StageExecution` model | ✅ Exists | `Models/StageExecution.cs` |
| `ProductionStage` with slugs | ✅ Exists | `Models/ProductionStage.cs` |
| `Machine` model with status | ✅ Exists | `Models/Machine.cs` |
| `OperatingShift` model | ✅ Exists | `Models/OperatingShift.cs` |
| `DelayLog` model | ✅ Exists | `Models/DelayLog.cs` |
| `StageService` (stub) | ✅ Exists | `Services/StageService.cs` |
| Shop floor stage view partials | ✅ Partial | `Components/Pages/ShopFloor/` |
| Scheduler page (Gantt stub) | ✅ Exists | `Components/Pages/Scheduler/` |
| `scheduler.js` (Gantt stub) | ✅ Exists | `wwwroot/js/scheduler.js` |
| `MachineStatus` enum | ✅ Exists | `Models/Enums/ManufacturingEnums.cs` |

**Gap**: Stage execution workflow not connected end-to-end; Gantt not wired to data; no constraint-based scheduling; no operator clock-in/out; no OEE tracking.

---

## What Needs to Be Built

### 1. Database Model Extensions
- Add `ScheduledStartAt`, `ScheduledEndAt`, `MachineId` (optional) to `StageExecution`
- Add `ActualStartAt`, `ActualEndAt`, `OperatorUserId`, `ActualHours` to `StageExecution`
- Add `SchedulingConstraint` model for machine capability rules
- Add `ShiftSchedule` extension for capacity calculation

### 2. Service Layer
- Complete `StageService` — operator workflows (start, complete, pause, fail)
- `SchedulerService` — finite capacity scheduling logic
- `OeeService` — Overall Equipment Effectiveness calculation
- `CapacityPlanningService` — load vs. capacity analysis

### 3. UI Components
- **Operator Queue** (`/shopfloor`) — what each operator should work on now
- **Stage View** (`/shopfloor/stage/{slug}`) — complete the existing partials
- **Scheduler / Gantt** (`/scheduler`) — wire to real data with drag-to-reschedule
- **Capacity Dashboard** — machine load vs. available hours

---

## Implementation Steps

### Step 1 — Extend StageExecution Model
**File**: `Models/StageExecution.cs`
Add:
```csharp
public int? MachineId { get; set; }
public Machine? Machine { get; set; }
public string? AssignedOperatorId { get; set; }
public User? AssignedOperator { get; set; }
public DateTime? ScheduledStartAt { get; set; }
public DateTime? ScheduledEndAt { get; set; }
public DateTime? ActualStartAt { get; set; }
public DateTime? ActualEndAt { get; set; }
public decimal? ActualHours { get; set; }
public string? CompletionNotes { get; set; }
public string? FailureReason { get; set; }
public bool IsUnmanned { get; set; } = false;          // lights-out tracking
public decimal SetupHoursActual { get; set; }
public decimal RunHoursActual { get; set; }
```

### Step 2 — Extend DelayLog Model
**File**: `Models/DelayLog.cs`
Ensure these fields exist:
```csharp
public int StageExecutionId { get; set; }
public StageExecution StageExecution { get; set; } = null!;
public string Reason { get; set; } = string.Empty;
public DelayCategory Category { get; set; }      // Material, Machine, Operator, Other
public DateTime StartedAt { get; set; }
public DateTime? ResolvedAt { get; set; }
public string? Resolution { get; set; }
```

Add to enums file: `public enum DelayCategory { Material, Machine, Operator, Quality, WaitingForInspection, Other }`

### Step 3 — Complete StageService
**File**: `Services/StageService.cs`

Implement:
```csharp
// Operator workflow
Task<List<StageExecution>> GetOperatorQueueAsync(string operatorUserId, string tenantCode);
Task<StageExecution?> GetCurrentExecutionAsync(string operatorUserId, string tenantCode);
Task StartStageAsync(int executionId, string operatorUserId, int? machineId, string tenantCode);
Task PauseStageAsync(int executionId, string reason, DelayCategory category, string tenantCode);
Task ResumeStageAsync(int executionId, string tenantCode);
Task CompleteStageAsync(int executionId, decimal actualHours, string notes, string tenantCode);
Task FailStageAsync(int executionId, string reason, string tenantCode);
Task LogUnmannedStartAsync(int executionId, int machineId, string tenantCode);  // lights-out

// Queue management
Task<List<StageExecution>> GetStageQueueAsync(int stageId, string tenantCode);
Task<List<StageExecution>> GetMachineQueueAsync(int machineId, string tenantCode);
Task AssignOperatorAsync(int executionId, string operatorUserId, string tenantCode);
Task AssignMachineAsync(int executionId, int machineId, string tenantCode);

// Scheduling helpers
Task<List<StageExecution>> GetScheduledExecutionsAsync(DateTime from, DateTime to, string tenantCode);
Task UpdateScheduleAsync(int executionId, DateTime start, DateTime end, string tenantCode);
```

**Business rules**:
- `StartStageAsync`: set `ActualStartAt = UtcNow`, status = `InProgress`, update machine status to `Running`
- `CompleteStageAsync`: set `ActualEndAt = UtcNow`, compute `ActualHours`, activate next stage in job sequence
- When all stages complete → set Job.Status = Completed, notify via SignalR
- `PauseStageAsync`: create `DelayLog`, status = `Paused`

### Step 4 — Operator Queue Page
**New File**: `Components/Pages/ShopFloor/OperatorQueue.razor`
**Route**: `/shopfloor` (update existing home/index)

UI requirements (tablet-optimized, large touch targets):
- Header: "Good morning, [OperatorName]" + current shift info
- **My Active Work** card: shows current in-progress stage (if any)
  - Part name, Job #, Stage name, Time elapsed, "Complete" + "Pause" buttons
- **My Queue** list: upcoming stage executions assigned to this operator
  - Each item: Priority badge, Part name, Stage, Due time, Machine assignment
  - "Start" button per item
- **Available Work** section: unassigned stages that need an operator
  - Filter by stage type matching operator's qualified stages
  - "Claim & Start" button
- **Unmanned Machines** section: running lights-out jobs with status

### Step 5 — Complete Stage View Partials
**Files**: `Components/Pages/ShopFloor/StageViews/*.razor`

Each stage partial should follow this pattern:
```razor
<!-- Top section -->
<div class="stage-header">
    <h2>@execution.ProductionStage.Name</h2>
    <span class="badge">@execution.Status</span>
    <div class="timer">⏱ @elapsedTime</div>
</div>

<!-- Work instruction preview (from Module 03) -->
@if (workInstruction != null)
{
    <WorkInstructionPanel Instruction="workInstruction" />
}

<!-- Dynamic form from stage definition -->
<DynamicFormRenderer Fields="@stage.CustomFormFields" OnSubmit="HandleFormSubmit" />

<!-- Action buttons -->
<div class="stage-actions">
    <button @onclick="CompleteStage" class="btn-success btn-large">✓ Complete</button>
    <button @onclick="PauseStage" class="btn-warning">⏸ Pause / Delay</button>
    <button @onclick="FailStage" class="btn-danger">✗ Fail / NCR</button>
</div>
```

Stage-specific views to complete:
- `SLSPrinting.razor` — machine assignment, print parameters, start/complete unmanned
- `Depowdering.razor` — manual process, weight input, powder recovered
- `CNCMachining.razor` — machine/fixture/tooling selection, cycle time logging
- `HeatTreatment.razor` — furnace assignment, temperature log, certificate
- `WireEDM.razor` — machine assignment, program number
- `QualityControl.razor` — inspection result entry (integrates with Module 05)
- `Shipping.razor` — shipping details (integrates with Module 15)
- `GenericStage.razor` — fallback for custom stages

### Step 6 — Scheduler (Gantt) Page
**File**: `Components/Pages/Scheduler/Index.razor`

UI requirements:
- Date range selector (default: next 2 weeks)
- Resource selector: view by Machine or by Operator
- **Gantt Chart** (rendered via `scheduler.js`):
  - Y-axis: machines or operators
  - X-axis: dates/times
  - Bars: stage executions with color by job priority
  - Drag to reschedule → calls `UpdateScheduleAsync`
  - Overlapping bars highlighted in red (overloaded)
  - "Must Leave By" deadline marker on each job bar
- **Sidebar**: unscheduled jobs list (drag from sidebar onto Gantt to schedule)
- **Conflict indicators**: red machine overload warnings

Update `scheduler.js` to:
- Accept JSON data of stage executions with start/end/resource
- Render Gantt using canvas or SVG
- Emit Blazor JS interop events on drag-complete with new times
- Show tooltip on hover with job details

### Step 7 — Capacity Dashboard
**New File**: `Components/Pages/Scheduler/Capacity.razor`
**Route**: `/scheduler/capacity`

UI requirements:
- Bar chart per machine: available hours vs. loaded hours for next 4 weeks
- Color: green (under 80%), yellow (80-100%), red (over 100%)
- Table: Machine, Available Hours/Week, Loaded Hours, Open Capacity, % Utilized
- Filter by date range
- "What-If" toggle: temporarily add phantom jobs to see impact

### Step 8 — OEE Service
**New File**: `Services/OeeService.cs`
**New File**: `Services/IOeeService.cs`

```csharp
public interface IOeeService
{
    Task<OeeData> GetMachineOeeAsync(int machineId, DateTime from, DateTime to, string tenantCode);
    Task<OeeData> GetOverallOeeAsync(DateTime from, DateTime to, string tenantCode);
}

public record OeeData(
    decimal Availability,    // (Scheduled - Downtime) / Scheduled
    decimal Performance,     // Actual output / Theoretical output
    decimal Quality,         // Good parts / Total parts
    decimal Oee             // Availability × Performance × Quality
);
```

Calculation sources:
- `Availability`: from `StageExecution` actual times vs. shift available hours, minus `DelayLog` durations
- `Performance`: actual hours vs. estimated hours per stage
- `Quality`: `QCInspection` pass rate (Module 05)

### Step 9 — Nav Menu Update
**File**: `Components/Layout/NavMenu.razor`
Add shop floor and scheduler links visible to Operator+ roles:
- Shop Floor → `/shopfloor`
- Scheduler → `/scheduler`
- Capacity → `/scheduler/capacity`

### Step 10 — EF Core Migration
```bash
dotnet ef migrations add AddShopFloorEnhancements --context TenantDbContext
dotnet ef database update
```

---

## Acceptance Criteria

- [ ] Operator can log in and see their queue at `/shopfloor`
- [ ] Operator can start a stage, see elapsed timer, and complete it
- [ ] Completing a stage automatically activates the next stage in the job
- [ ] Completing all stages marks the Job as complete
- [ ] Pause / delay records a `DelayLog` with category and reason
- [ ] Gantt chart renders stage executions on correct machine/date axes
- [ ] Dragging a Gantt bar reschedules the `StageExecution` in the DB
- [ ] Machine overload (double-booked) shows red conflict indicator
- [ ] Unmanned / lights-out jobs can be started without operator clocking on
- [ ] OEE data calculates correctly from stage execution records
- [ ] Capacity dashboard shows machine load vs. available capacity

---

## Dependencies

- **Module 02** (Work Order Management) — Stage executions spawned from WO release
- **Module 03** (Visual Work Instructions) — Instructions displayed in stage views
- **Module 05** (Quality Systems) — QC stage integrates inspection entry
- **Module 11** (Calibration/Maintenance) — Machine availability affects scheduling
- **Module 13** (Time Clock) — Labor time logging per stage

---

## Future Enhancements (Post-MVP)

- AI-based scheduling optimization (minimize setup changes, batch similar jobs)
- Machine monitoring via MTConnect/OPC-UA for actual cycle time vs. programmed
- Automatic rescheduling when machine goes down (pull from SignalR machine state)
- Constraint-based scheduler considering tooling availability (Module 10)
- Operator skill-based assignment (only assign stages to qualified operators)
