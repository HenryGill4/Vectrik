# Next Build Advisor — Implementation Plan

## Status: Phase 1 In Progress

---

## Problem Statement

The current scheduling wizard is WO-centric (starts from a work order, tries to schedule all demand at once). This doesn't match how scheduling actually works in an SLS shop:

1. You look at a machine and ask "what should I print next?"
2. You consider all outstanding demand, not just one WO
3. You need to pick stack levels that align changeovers with operator schedules
4. You sometimes mix parts on a plate to fill capacity
5. You plan one build at a time, then move to the next slot

The "Next Build Advisor" is a machine-centric, one-build-at-a-time scheduling assistant.

---

## Production Flow Reference

```
SLS Print (build-level, 24/7)
    → Depowder (build-level)
    → Wire EDM (build-level — parts cut off plate here)
    → [parts become individual, grouped by type]
    → CNC Machining (part-level, machine set up per part type)
    → Sandblasting, Surface Finishing, Laser Engraving, QC, etc.
```

Key: Build stages (print, depowder, EDM) operate on the whole plate. After EDM, parts separate and flow through part-level stages on type-specific machines.

---

## Phase 1: BuildAdvisorService (Foundation)
**Status: NOT STARTED**

### Files to Create
- [ ] `Services/IBuildAdvisorService.cs` — Interface
- [ ] `Services/BuildAdvisorService.cs` — Implementation

### Interface Design
```csharp
interface IBuildAdvisorService
{
    // Core: "What should I print next on this machine?"
    Task<BuildRecommendation> RecommendNextBuildAsync(int machineId, DateTime? startAfter = null);

    // Demand view: all outstanding demand grouped by part
    Task<List<DemandSummary>> GetAggregateDemandAsync();

    // Mixed plate: optimize part composition for a plate
    Task<PlateComposition> OptimizePlateAsync(
        int machineId, DateTime slotStart,
        List<DemandSummary> demand, int maxPartTypes = 4);

    // Capacity: detect downstream bottlenecks
    Task<BottleneckReport> AnalyzeBottlenecksAsync(
        DateTime horizonStart, DateTime horizonEnd);
}
```

### Key Records
```csharp
record BuildRecommendation(
    int MachineId, string MachineName,
    ProgramScheduleSlot Slot,
    PlateComposition Plate,
    string Rationale,
    List<string> Warnings);

record PlateComposition(
    List<PlatePartAllocation> Parts,
    int RecommendedStackLevel,
    double EstimatedPrintHours,
    bool ChangeoverAligned,
    DateTime ChangeoverTime,
    bool OperatorAvailable);

record PlatePartAllocation(
    int PartId, string PartNumber,
    int Positions, int StackLevel,
    int TotalParts, int DemandRemaining,
    int Surplus, // parts beyond demand
    int? WorkOrderLineId,
    DateTime? WoDueDate);

record DemandSummary(
    int PartId, string PartNumber,
    int TotalOutstanding,      // across all WOs
    int AlreadyScheduled,      // in programs/jobs
    int NetRemaining,          // outstanding - scheduled
    DateTime EarliestDueDate,
    JobPriority HighestPriority,
    bool IsOverdue,
    PartAdditiveBuildConfig? BuildConfig,
    List<(int WoLineId, string WoNumber, int Qty)> SourceLines);

record BottleneckReport(
    List<BottleneckItem> Items,
    Dictionary<string, double> DepartmentUtilization);

record BottleneckItem(
    string Department, string MachineName,
    double UtilizationPct, double QueueHours,
    string Severity); // "ok", "warning", "critical"
```

### Recommendation Algorithm
```
1. Find next available slot on machine (FindEarliestSlotAsync)
2. Load aggregate demand (all WOs, net of produced + in-programs)
3. Sort demand by: overdue first, then due date, then priority
4. Pick primary part (highest urgency with available build config)
5. Select stack level:
   a. Get operator schedule for this machine
   b. For each stack level, compute buildEnd + changeoverMinutes
   c. Check if changeover falls within operator shift
   d. If building before a weekend/overnight gap:
      - Prefer the stack level whose total duration fills the gap
      - Double stack for overnight, triple for weekends
   e. If no changeover-aligned option, pick shortest stack level
      that fits within next operator window
6. Calculate primary part positions (from PlannedPartsPerBuildSingle)
7. Calculate remaining plate capacity
8. If remaining > 0 and demand exists for other parts:
   - Pick next-most-urgent parts (max 4 types total)
   - Allocate positions proportionally to demand
   - Print duration = MAX of all part durations at their stack levels
9. Check for overproduction: if totalParts > netRemaining, warn
10. Return BuildRecommendation with full rationale
```

### Registration
- Add `builder.Services.AddScoped<IBuildAdvisorService, BuildAdvisorService>()` in `Program.cs`

---

## Phase 2: NextBuildAdvisor Modal (UI)
**Status: NOT STARTED**

### Files to Create
- [ ] `Components/Pages/Scheduler/Modals/NextBuildAdvisor.razor`
- [ ] `Components/Pages/Scheduler/Components/PlateCompositionCard.razor`

### Step Flow

#### Step 1: Machine & Slot (auto-populated)
- Machine selector (dropdown, pre-filled if opened from Gantt row)
- Current queue display: last 2 builds on this machine
- Next available slot: start time, changeover window
- Operator schedule: when the next shift starts/ends
- "Start After" override

#### Step 2: Plate Composition (recommendation)
- PlateCompositionCard showing:
  - Primary part: name, positions, stack level, demand coverage
  - Fill parts (if any): same info
  - Total print time, changeover time, operator availability
  - Overproduction warnings
- User controls:
  - Stack level selector per part (single/double/triple)
  - Quantity override per part
  - "Add Part" button (opens part picker)
  - "Remove Part" button per fill part
  - "Swap Primary" (pick a different primary part from demand list)
- Demand table below showing all outstanding parts ranked by urgency

#### Step 3: Program Match
- Match to existing MachineProgram or create new
- For mixed plates: always create new (can't reuse single-part programs)
- Name auto-generated from parts + date

#### Step 4: Confirm
- Timeline visualization showing this build in context of machine queue
- Downstream stages preview (depowder, EDM, CNC assignments)
- **"Schedule This Build"** — schedules and closes
- **"Schedule & Queue Next"** — schedules, reopens at Step 1 for next slot
- Session summary: builds scheduled in this advisor session

### Entry Points
- Gantt: "+" zone at end of SLS machine row → opens pre-populated for that machine
- Toolbar: "New Build" button → opens with machine selector
- WO Panel: "Schedule" button on a WO → opens with that part pre-selected
- Self: "Schedule & Queue Next" loops back

---

## Phase 3: Gantt Integration
**Status: NOT STARTED**

### Files to Modify
- [ ] `Components/Pages/Scheduler/Components/GanttMachineRow.razor` — Add "+" zone for SLS machines
- [ ] `Components/Pages/Scheduler/Views/GanttView.razor` — Add `OnNextBuildRequested` callback
- [ ] `Components/Pages/Scheduler/Index.razor` — Wire advisor modal, add `_showBuildAdvisor` state
- [ ] `Components/Pages/Scheduler/Components/SchedulerToolbar.razor` — Add "New Build" dropdown
- [ ] `Components/Pages/Scheduler/Components/WorkOrderPanel.razor` — Route "Schedule" to advisor

### "+" Zone Design
After the last build bar on an SLS machine row, render a dashed-outline rectangle:
```html
<div class="gantt-next-build-zone" @onclick="RequestNextBuild" title="Schedule next build">
    <span>+</span>
</div>
```
Only shown on machines where `IsAdditiveMachine == true`.

---

## Phase 4: CNC Setup Affinity
**Status: NOT STARTED**

### Files to Modify
- [ ] `Models/ProcessStage.cs` — Add `SetupChangeoverMinutes` (nullable double)
- [ ] `Data/TenantDbContext.cs` — No change needed (auto-discovered)
- [ ] EF Migration: `AddCncSetupChangeover`
- [ ] `Services/SchedulingService.cs` — Modify `AutoScheduleJobCoreAsync`:
  - When scheduling a Part-level stage on a CNC machine:
  - Check what part was last scheduled on each capable machine
  - Prefer machine with same part (skip setup time)
  - If switching parts, add `SetupChangeoverMinutes` to duration

### Algorithm Change in ResolveMachines / FindEarliestSlotOnMachine
```
For each capable CNC machine:
    lastPart = most recent StageExecution on this machine's PartId
    if lastPart == currentPart:
        setupPenalty = 0  (already set up)
    else:
        setupPenalty = processStage.SetupChangeoverMinutes ?? 0
    slot = FindEarliestSlot(machine, duration + setupPenalty, cursor, shifts)
    effectiveEnd = slot.End  (penalty is baked into duration)
```

---

## Phase 5: Bottleneck Detection
**Status: NOT STARTED**

### Files to Create
- [ ] `Components/Pages/Scheduler/Components/BottleneckPanel.razor`

### Files to Modify
- [ ] `Services/BuildAdvisorService.cs` — Implement `AnalyzeBottlenecksAsync`
- [ ] `Components/Pages/Scheduler/Components/KpiStrip.razor` — Add bottleneck indicator
- [ ] `Components/Pages/Scheduler/Views/WorkOrdersView.razor` — Add bottleneck panel

### Detection Logic
```
For each department (SLS, Depowder, EDM, CNC, etc.):
    totalScheduledHours = sum of all StageExecution.EstimatedHours in horizon
    totalCapacityHours = sum of (machine count * shift hours/day * days in horizon)
    utilization = scheduledHours / capacityHours

    if utilization > 0.9: severity = "critical"
    else if utilization > 0.75: severity = "warning"
    else: severity = "ok"

Special check for SLS→CNC throughput:
    slsPartsPerDay = estimated from scheduled builds
    cncPartsPerDay = estimated from CNC capacity
    if slsPartsPerDay > cncPartsPerDay * 1.1:
        flag "CNC capacity bottleneck: SLS producing faster than CNC can machine"
```

---

## Phase 6: Polish & Cleanup
**Status: NOT STARTED**

- [ ] Simplify `UnifiedScheduleWizard.razor` to CNC-only (remove SLS path)
- [ ] Update toolbar "+" button: dropdown with "SLS Build" and "CNC Job" options
- [ ] End-to-end testing of full advisor flow
- [ ] Responsive styling for advisor modal
- [ ] Update memory/CLAUDE.md with final architecture

---

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Scheduling approach | Machine-centric, one build at a time | Matches real workflow; operators plan per-machine |
| Mixed plates | Up to 4 part types, fill remaining slots | Maximizes plate utilization, reduces overproduction |
| Stack level selection | Changeover-aligned with operator schedule | Prevents machine downtime from missed changeovers |
| Weekend builds | Double/triple stack to span gaps | Machine runs continuously, changeover at shift start |
| CNC assignment | Setup affinity (prefer same-part machine) | Minimizes CNC setup changes |
| Overproduction | Warn but allow (surplus goes to stock) | Practical reality of plate-based manufacturing |
| Planning horizon | Monthly default, easy reschedule | Balance between planning ahead and flexibility |

---

## Dependencies Between Phases

```
Phase 1 (Service) ─── required by ──→ Phase 2 (Modal)
Phase 2 (Modal)   ─── required by ──→ Phase 3 (Gantt Integration)
Phase 4 (CNC)     ─── independent ──  (can be done in parallel with 2-3)
Phase 5 (Bottleneck) ── requires ──→  Phase 1 (Service)
Phase 6 (Polish)   ─── requires ──→   All above
```

Phase 1 is the critical path. Phases 4 and 5 can be done in parallel with Phases 2-3.
