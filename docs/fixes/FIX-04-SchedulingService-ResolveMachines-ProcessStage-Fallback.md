# FIX-04: SchedulingService — Recover ProcessStage for Existing Legacy-Created Executions

> **Priority**: MEDIUM — Defensive fix for existing data. FIX-02 prevents new jobs from having this problem, but existing jobs in the database created via Path A will still have `ProcessStageId = null`. This fix lets the scheduler recover new-system routing for those jobs when they are re-scheduled.
> **Affected file**: `Services/SchedulingService.cs`

---

## Problem

`AutoScheduleJobAsync()` loads job stages with:
```csharp
.Include(j => j.Stages).ThenInclude(s => s.ProcessStage)
```

For jobs created via `JobService.CreateJobAsync()` (Path A — direct job creation), `StageExecution.ProcessStageId = null`, so `exec.ProcessStage` is always null even after the include.

This means `ResolveMachines(exec.ProductionStage, requirement, exec.ProcessStage=null, ...)` skips steps 1–3 of the priority chain entirely, falling to legacy fallbacks or random machine assignment.

---

## When This Matters

After FIX-02, **newly created** jobs via `JobService` will have `ProcessStageId` set. However:
- All existing jobs in the database created before FIX-02 still have `ProcessStageId = null`
- These jobs will be re-scheduled when users click "Auto-schedule" or reschedule individual executions
- Without this fix, re-scheduling old jobs still produces wrong machine assignments

---

## Fix

In `AutoScheduleJobAsync()`, after loading the job, attempt to recover missing `ProcessStage` references from the job's `ManufacturingProcess`.

**File**: `Services/SchedulingService.cs` — add after the job load (around line 28), before the execution loop (line 57)

```csharp
// Recover ProcessStage references for legacy-created executions that have ProcessStageId = null
// This allows re-scheduling old jobs to benefit from new-system machine routing
if (job.ManufacturingProcessId.HasValue)
{
    var processStageLookup = await _db.ProcessStages
        .Where(ps => ps.ManufacturingProcessId == job.ManufacturingProcessId.Value)
        .ToDictionaryAsync(ps => ps.ProductionStageId, ps => ps);

    foreach (var exec in executions.Where(e => e.ProcessStage == null && e.ProcessStageId == null))
    {
        if (processStageLookup.TryGetValue(exec.ProductionStageId, out var matchedStage))
        {
            exec.ProcessStage = matchedStage; // in-memory only, not saved to DB
        }
    }
}
```

This does **not** save `ProcessStageId` to the database — it only populates the in-memory navigation property for the duration of this scheduling pass. This is intentional: the data fix (setting `ProcessStageId` on existing records) is a separate migration concern.

---

## Same Fix for `AutoScheduleExecutionAsync()`

**File**: `Services/SchedulingService.cs` ~line 120

```csharp
// After loading exec, before ResolveMachines call:
if (exec.ProcessStage == null && exec.ProcessStageId == null && exec.Job?.ManufacturingProcessId.HasValue == true)
{
    exec.ProcessStage = await _db.ProcessStages
        .FirstOrDefaultAsync(ps =>
            ps.ManufacturingProcessId == exec.Job.ManufacturingProcessId.Value
            && ps.ProductionStageId == exec.ProductionStageId);
}
```

---

## Optional: Data Migration

As a longer-term cleanup, a one-time migration can backfill `ProcessStageId` on existing `StageExecution` records:

```sql
UPDATE se
SET se.ProcessStageId = ps.Id
FROM StageExecutions se
JOIN Jobs j ON se.JobId = j.Id
JOIN ProcessStages ps ON ps.ManufacturingProcessId = j.ManufacturingProcessId
    AND ps.ProductionStageId = se.ProductionStageId
WHERE se.ProcessStageId IS NULL
  AND j.ManufacturingProcessId IS NOT NULL;
```

This migration is **not required** for the fix to work — the in-memory recovery handles it at scheduling time. But running it would permanently resolve the data inconsistency for all historical records.

---

## Expected Outcome After Fix

- Re-scheduling any existing job that has `ManufacturingProcessId` set will use new-system machine preferences
- Operators won't need to manually reassign machines for previously broken jobs
- The fix is safe to apply without a database migration — works on existing data as-is
