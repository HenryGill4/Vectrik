# Scheduling Flow

> **Status**: Current — describes how the scheduler assigns machines and time slots
> **Key bug**: Steps 6 and 9 of `ResolveMachines()` are broken — see `docs/fixes/FIX-01-ProductionStage-AssignedMachineIds-Lookup.md`

---

## Entry Points

There are three ways scheduling happens:

| Entry Point | Service Method | Triggered By |
|------------|----------------|--------------|
| Full job schedule | `SchedulingService.AutoScheduleJobAsync(jobId)` | Job creation, build scheduling, manual trigger |
| Single execution | `SchedulingService.AutoScheduleExecutionAsync(executionId)` | "⚡ Auto-schedule" button on a specific task |
| All unscheduled | `SchedulingService.AutoScheduleAllAsync()` | "Auto-schedule All" toolbar button |
| Build schedule | `BuildSchedulingService.ScheduleBuildAsync(buildId, machineId)` | Scheduling a build plate |

---

## `AutoScheduleJobAsync(jobId, startAfter?)` — Full Job Schedule

```
1. Load Job + Stages (with ProductionStage + ProcessStage includes)
2. Load active OperatingShifts
3. Load all active IsAvailableForScheduling machines
4. Load PartStageRequirements for job.PartId (legacy fallback only)
5. Resolve predecessor constraint: cursor = max(startAfter, predecessor job end + gap)

6. For each StageExecution (ordered by SortOrder):
   a. duration = exec.EstimatedHours ?? exec.ProductionStage.DefaultDurationHours
   b. totalDuration = duration + setupHours
   c. requirement = matching PartStageRequirement (by ProductionStageId)
   d. capableMachines = ResolveMachines(productionStage, requirement, processStage, allMachines)
   e. If no capable machines: schedule without machine (MachineId = null), advance cursor
   f. Else: try each capable machine, pick earliest finish slot
   g. exec.ScheduledStartAt/EndAt/MachineId = best slot
   h. cursor = bestSlot.End (sequential within job)

7. Update job.ScheduledStart/End/EstimatedHours
8. SaveChangesAsync()
```

---

## `ResolveMachines()` — Priority Chain

Given a `ProductionStage`, optional `PartStageRequirement` (legacy), optional `ProcessStage` (new), and all active machines:

```
Step 1: ProcessStage.RequiresSpecificMachine + AssignedMachineId (int)
        → hard lock: return exactly [that machine] if found in machineIdLookup (int→Machine)
        → EARLY RETURN

Step 2: ProcessStage.PreferredMachineIds (comma-separated ints)
        → parse each as int, look up in machineIdLookup
        → add found machines to result list

Step 3: ProcessStage.AssignedMachineId (int, non-required)
        → if int found in machineIdLookup AND not already in result → insert at front

Step 4: PartStageRequirement.RequiresSpecificMachine + AssignedMachineId (string)
        → only if result is still empty
        → look up in machineLookup (string MachineId → Machine)
        → EARLY RETURN if found

Step 5: PartStageRequirement.PreferredMachineIds (comma-separated strings)
        → only if result is still empty
        → parse as strings, look up in machineLookup
        → add found machines to result

Step 6: ProductionStage.AssignedMachineIds (stored as int PKs, comma-separated)
        → ⚠️ BUG: GetAssignedMachineIds() returns List<string> e.g. ["1","2"]
        → code does machineLookup.TryGetValue("1", ...) where machineLookup is keyed by "SLS-001"
        → ALWAYS MISSES — see FIX-01

Step 7: ProductionStage.DefaultMachineId (string MachineId, e.g. "SLS-001")
        → look up in machineLookup (string → Machine) — CORRECT, this one works
        → insert at front if found and not already in result

Step 8: Fallback — all non-additive machines (if processStage is non-Build level)
        → only if result is still empty AND stage.RequiresMachineAssignment == false
        → ordered by machine.Priority

Step 9: Fallback — machines that CanMachineExecuteStage (if stage requires assignment)
        → ⚠️ BUG: stage.CanMachineExecuteStage(m.MachineId) calls GetAssignedMachineIds().Contains("SLS-001")
        → list contains "1","2" not "SLS-001" → ALWAYS FALSE — see FIX-01

Return result list
```

**The practical effect of bugs in steps 6 and 9:**
- If a `ProcessStage` has no explicit machine assignment AND `PartStageRequirements` are also empty → step 6 fails → step 7 may match OR falls to step 8 (all non-additive machines)
- "All non-additive machines" means the scheduler picks whatever machine has the earliest free slot across ALL machines → stages pile up on whatever is least busy, regardless of which machine should handle that stage type

---

## `FindEarliestSlotOnMachine(machineId, durationHours, notBefore, shifts)`

```
1. Query all StageExecutions on machineId where:
   - ScheduledStartAt != null AND ScheduledEndAt != null
   - Status not in (Completed, Skipped, Failed)
   - ScheduledEndAt > notBefore
   - Ordered by ScheduledStartAt

2. Snap candidate start to next shift start (SnapToNextShiftStart)
3. Advance candidate end by durationHours of working time (AdvanceByWorkHours)

4. For each existing block:
   - If candidateEnd <= blockStart: we fit before this block → stop
   - Else: shift candidate to after blockEnd, recalculate end

5. Return ScheduleSlot(candidate, candidateEnd, machineId)
```

`AdvanceByWorkHours` respects `OperatingShift` records — only counts time during active shifts. Handles overnight shifts and multi-day gaps.

---

## `FindEarliestBuildSlotAsync(machineId, durationHours, notBefore)` (for SLS builds)

Similar to above but queries `BuildPackage.ScheduledDate + EstimatedDurationHours` instead of `StageExecution` records. Returns a `BuildScheduleSlot` with `PrintStart` and `PrintEnd`.

---

## Machine Resolution in Build Planning

`BuildPlanningService.ResolveStageMachine(stage, byIntId, byStringId)`:
```
1. stage.AssignedMachineId (int?) → byIntId lookup → return int id
2. stage.PreferredMachineIds (comma-separated ints) → first found in byIntId → return
3. stage.ProductionStage.DefaultMachineId (string) → byStringId lookup → return .Id
4. → null (no machine assigned, will be scheduled later by AutoScheduleJobAsync)
```
This helper is **correct** — used by `CreateBuildStageExecutionsAsync` and `CreatePartStageExecutionsAsync`.

---

## Shift Calculation

`OperatingShift` records define working hours:
- `DaysOfWeek` (string, e.g. "Mon,Tue,Wed,Thu,Fri")
- `StartTime` / `EndTime` (TimeSpan)
- Overnight shifts: `EndTime <= StartTime` → adds a day

If no shifts are active, `SnapToNextShiftStart` and `AdvanceByWorkHours` treat it as 24/7 operation.

---

## Gantt View Data Flow

```
Scheduler/Index.razor
  → StageService.GetScheduledExecutionsAsync(from, to) → _executions
  → StageService.GetUnscheduledExecutionsAsync() → _unscheduled
  → GanttView.GetExecutionsForMachine(machine) → Executions.Where(e.MachineId == machine.Id)
  → GanttMachineRow renders one bar per StageExecution on that machine
```

When routing is broken (bugs in ResolveMachines steps 6/9), multiple unrelated stage types end up on the same machine (the one with the most free slots), causing visual pile-up on one machine row.
