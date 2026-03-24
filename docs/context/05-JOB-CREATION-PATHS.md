# Job Creation Paths

> **Status**: Current — documents both job creation paths and their differences
> **Key problem**: Path A ignores ManufacturingProcess — see `docs/fixes/FIX-02-JobService-Missing-ManufacturingProcess.md`

---

## Path A — Direct Job Creation (Legacy)

**Entry point**: `JobService.CreateJobAsync(job)`

**Triggered by**: Direct "Create Job" UI form in the scheduler

```
JobService.CreateJobAsync(job)
  1. Hydrate job from Part (PartNumber, SlsMaterial, stacking config)
  2. Check for machine overlap
  3. Save Job
  4. Load PartStageRequirements for job.PartId (legacy routing)
  5. If any requirements exist:
     a. Build machineLookup (string MachineId → int Id)
     b. For each requirement (ordered by ExecutionOrder):
        - Convert requirement.AssignedMachineId (string) → int via lookup
        - Create StageExecution with:
            JobId = job.Id
            ProductionStageId = requirement.ProductionStageId
            ProcessStageId = null  ← NOT SET — scheduler can't use new routing
            MachineId = resolved int id (or job.MachineId as default)
            EstimatedHours = requirement.GetEffectiveEstimatedHours()
     c. SaveChangesAsync()
     d. AutoScheduleJobAsync(job.Id, job.ScheduledStart)
```

**Problems with Path A:**
- `ProcessStageId = null` on all created `StageExecution` records
- When `AutoScheduleJobAsync` runs `ResolveMachines()`, `exec.ProcessStage == null`
- Scheduler skips steps 1–3 (ProcessStage-based routing) entirely
- Falls to steps 4–5 (PartStageRequirement-based) IF requirements exist
- Then steps 6–9 (ProductionStage catalog), where step 6 is broken (see FIX-01)
- Result: machine assignment based on luck or fallback-to-all

**When Path A works acceptably:**
- Part has specific `PartStageRequirement.AssignedMachineId` entries (steps 4–5 catch it)
- Part has no manufacturing process and no machine preferences → schedules without machine

**When Path A breaks badly:**
- Part has a `ManufacturingProcess` with precise machine assignments → ignored entirely
- Scheduler routes stages to wrong machines

---

## Path B — Build Workflow (New System)

**Entry point**: `BuildSchedulingService.ScheduleBuildAsync(buildPackageId, machineId)`

**Triggered by**: "Schedule Build" button in the Builds view

```
BuildSchedulingService.ScheduleBuildAsync()
  1. Find slot on SLS machine → slot.PrintStart / slot.PrintEnd
  2. Update package: ScheduledDate, Status=Scheduled, MachineId=machineId
  3. CreateBuildStageExecutionsAsync(buildPackageId, "Scheduler")
     → Load ManufacturingProcess for each part in the build
     → Filter ProcessStages: ProcessingLevel == Build
     → Deduplicate by ProductionStageId
     → Create Job (Scope=Build, ManufacturingProcessId set)
     → For each build-level ProcessStage:
         - CalculateStageDuration() for estimated hours
         - ResolveStageMachine(): AssignedMachineId (int) → PreferredMachineIds → DefaultMachineId (string)
         - Create StageExecution with:
             ProcessStageId = processStage.Id  ← SET CORRECTLY
             MachineId = resolved int id
             BuildPackageId = buildPackageId
  4. CreatePartStageExecutionsAsync(buildPackageId, "Scheduler", startAfter=lastBuildStageEnd)
     → Load ManufacturingProcess for each part
     → Group by (PartId, WorkOrderLineId) → 1 Job per group
     → For each Job:
         - Create Batch-level StageExecutions with ProcessStageId SET
         - Create Part-level StageExecutions with ProcessStageId SET
         - All machine assignments use int-based ResolveStageMachine()
  5. AutoScheduleJobAsync(jobId) for each per-part job
     → ResolveMachines() uses exec.ProcessStage (correctly loaded)
     → Steps 1–3 work correctly
     → Steps 6–7 may still have issues per FIX-01, but steps 1–3 are usually sufficient
```

**Why Path B works correctly:**
- All `StageExecution` records have `ProcessStageId` set
- Machine resolution uses `ProcessStage.AssignedMachineId` (int) first → usually finds the right machine
- Scheduler's `ResolveMachines()` step 1–3 short-circuits before reaching broken steps 6–9

---

## The Gap — Parts Created via Path A That Have ManufacturingProcess

This is the most common failure scenario:
1. Part is configured with a full `ManufacturingProcess` (all stages, machine assignments)
2. Scheduler creates a job directly (e.g. a standalone CNC job from the UI)
3. Path A runs → reads `PartStageRequirements` (possibly empty or outdated)
4. `ProcessStageId = null` on all executions
5. `AutoScheduleJobAsync` has no `ProcessStage` to reference
6. Stages get routed to wrong machines

**After FIX-02**: Path A checks for `ManufacturingProcess` first, uses `ProcessStages` if found, sets `ProcessStageId` on all executions. The fix makes Path A behave like Path B for parts that have been migrated to the new system.

---

## Job Scope

Both paths create jobs with `Scope` set:

| Scope | Created By | Meaning |
|-------|-----------|---------|
| `JobScope.Build` | Path B — `CreateBuildStageExecutionsAsync` | One job per build plate, runs build-level stages |
| `JobScope.Part` | Path B — `CreatePartStageExecutionsAsync` | One job per part-type per WO line, runs batch + part stages |
| `JobScope.Batch` | Not currently created | Reserved for batch-only jobs (not implemented) |

Path A creates jobs without explicitly setting Scope (defaults to Build — may be incorrect for non-SLS parts).

---

## Idempotency in Path B

`CreatePartStageExecutionsAsync` has idempotency protection:

```csharp
if (!forceNewJobs)
{
    var existingPartJobs = await _db.Jobs
        .Where(j => partIds.Contains(j.PartId)
            && j.Scope == JobScope.Part
            && j.ManufacturingProcessId != null
            && j.Stages.Any(s => s.BuildPackageId == buildPackageId))
        .Select(j => j.Id)
        .ToListAsync();

    if (existingPartJobs.Count > 0)
        return existingPartJobs; // Don't create duplicates
}
```

This means calling `CreatePartStageExecutionsAsync` twice (once from `ScheduleBuildAsync`, once from `ReleasePlateAsync`) is safe — the second call returns existing job IDs.

**Edge case**: The idempotency check requires `j.ManufacturingProcessId != null`. If a job was created via Path A (no ManufacturingProcessId), the check fails and a duplicate job would be created. This is another reason FIX-02 is important.
