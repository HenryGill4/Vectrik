# FIX-02: JobService.CreateJobAsync Ignores ManufacturingProcess

> **Priority**: CRITICAL — Direct job creation never sets ProcessStageId on StageExecutions, so the scheduler cannot use new-system machine preferences for directly-created jobs.
> **Affected file**: `Services/JobService.cs`

---

## Problem

`JobService.CreateJobAsync()` generates `StageExecution` records exclusively from `PartStageRequirements` (the legacy system). It **never checks for a `ManufacturingProcess`**.

When a part has a `ManufacturingProcess` with explicit machine assignments in `ProcessStage.AssignedMachineId`, those are completely ignored when a job is created directly (not via the build workflow).

All `StageExecution` records created by this path have:
- `ProcessStageId = null`
- Machine assignment derived from the legacy `PartStageRequirement.AssignedMachineId` (string) only

When `AutoScheduleJobAsync` runs for these jobs:
1. `exec.ProcessStage == null` for every execution
2. Scheduler skips ResolveMachines steps 1–3 (new system)
3. Falls to steps 4–5 (legacy requirements) — may work if requirements are up to date
4. Then steps 6–9 (ProductionStage catalog) — step 6 is broken (see FIX-01)
5. Ultimately routes stages to wrong/random machines

---

## Secondary Effects

With `ProcessStageId = null`:
- `StageService.CompleteStageExecutionAsync()` skips EMA learning (line 237: `if (execution.ProcessStageId.HasValue)`)
- Plate release trigger check at line 254 also silently skips
- `LearningService` never receives actual duration data for these executions

---

## Current Code
**File**: `Services/JobService.cs` lines 99–147

```csharp
// Generate StageExecution records from part routing
if (job.PartId > 0)
{
    var routing = await _db.PartStageRequirements
        .Include(r => r.ProductionStage)
        .Where(r => r.PartId == job.PartId && r.IsActive)
        .OrderBy(r => r.ExecutionOrder)
        .ToListAsync();

    if (routing.Count > 0)
    {
        var machineLookup = await _db.Machines
            .Where(m => m.IsActive)
            .ToDictionaryAsync(m => m.MachineId, m => m.Id);

        foreach (var stage in routing)
        {
            var estHours = stage.GetEffectiveEstimatedHours();
            int? machineIntId = job.MachineId;
            if (!string.IsNullOrEmpty(stage.AssignedMachineId)
                && machineLookup.TryGetValue(stage.AssignedMachineId, out var smid))
            {
                machineIntId = smid;
            }

            _db.StageExecutions.Add(new StageExecution
            {
                JobId = job.Id,
                ProductionStageId = stage.ProductionStageId,
                // ProcessStageId NOT SET ← the problem
                SortOrder = stage.ExecutionOrder,
                EstimatedHours = estHours,
                ...
                MachineId = machineIntId,
            });
        }

        await _db.SaveChangesAsync();
        await _scheduler.AutoScheduleJobAsync(job.Id, job.ScheduledStart);
    }
}
```

---

## Fix

Replace the stage execution generation block with logic that prefers `ManufacturingProcess` and only falls back to `PartStageRequirements` if no active process exists.

**File**: `Services/JobService.cs` — replace lines 99–147

```csharp
if (job.PartId > 0)
{
    // Prefer ManufacturingProcess (new system) over PartStageRequirements (legacy)
    var process = await _db.ManufacturingProcesses
        .Include(p => p.Stages.OrderBy(s => s.ExecutionOrder))
            .ThenInclude(s => s.ProductionStage)
        .FirstOrDefaultAsync(p => p.PartId == job.PartId && p.IsActive);

    if (process != null)
    {
        // New system path: use ProcessStages filtered to Batch + Part levels
        // (Build-level stages are handled by the build workflow, not direct jobs)
        var stages = process.Stages
            .Where(s => s.ProcessingLevel == ProcessingLevel.Batch
                     || s.ProcessingLevel == ProcessingLevel.Part)
            .OrderBy(s => s.ExecutionOrder)
            .ToList();

        if (stages.Any())
        {
            var machineLookup = await _db.Machines
                .Where(m => m.IsActive)
                .ToDictionaryAsync(m => m.Id, m => m);
            var machineStringLookup = await _db.Machines
                .Where(m => m.IsActive)
                .ToDictionaryAsync(m => m.MachineId, m => m);

            var sortOrder = 0;
            foreach (var processStage in stages)
            {
                var dur = _processService.CalculateStageDuration(
                    processStage, job.Quantity, batchCount: 1, buildConfigHours: null);
                var estimatedHours = dur.TotalMinutes / 60.0;

                // Resolve machine from ProcessStage (int-based)
                int? machineIntId = null;
                if (processStage.AssignedMachineId.HasValue
                    && machineLookup.ContainsKey(processStage.AssignedMachineId.Value))
                {
                    machineIntId = processStage.AssignedMachineId.Value;
                }
                else if (!string.IsNullOrEmpty(processStage.PreferredMachineIds))
                {
                    foreach (var pid in processStage.PreferredMachineIds.Split(',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (int.TryParse(pid, out var intId) && machineLookup.ContainsKey(intId))
                        {
                            machineIntId = intId;
                            break;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(processStage.ProductionStage?.DefaultMachineId)
                    && machineStringLookup.TryGetValue(processStage.ProductionStage.DefaultMachineId, out var defMachine))
                {
                    machineIntId = defMachine.Id;
                }

                _db.StageExecutions.Add(new StageExecution
                {
                    JobId = job.Id,
                    ProductionStageId = processStage.ProductionStageId,
                    ProcessStageId = processStage.Id,  // ← SET CORRECTLY
                    SortOrder = sortOrder++,
                    EstimatedHours = estimatedHours,
                    SetupHours = dur.SetupMinutes / 60.0,
                    QualityCheckRequired = processStage.RequiresQualityCheck,
                    MachineId = machineIntId,
                    CreatedBy = job.CreatedBy,
                    LastModifiedBy = job.LastModifiedBy,
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                });
            }

            job.ManufacturingProcessId = process.Id;
            await _db.SaveChangesAsync();
            await _scheduler.AutoScheduleJobAsync(job.Id, job.ScheduledStart);
        }
    }
    else
    {
        // Legacy fallback: use PartStageRequirements
        var routing = await _db.PartStageRequirements
            .Include(r => r.ProductionStage)
            .Where(r => r.PartId == job.PartId && r.IsActive)
            .OrderBy(r => r.ExecutionOrder)
            .ToListAsync();

        if (routing.Count > 0)
        {
            var machineLookup = await _db.Machines
                .Where(m => m.IsActive)
                .ToDictionaryAsync(m => m.MachineId, m => m.Id);

            foreach (var stage in routing)
            {
                var estHours = stage.GetEffectiveEstimatedHours();
                int? machineIntId = job.MachineId;
                if (!string.IsNullOrEmpty(stage.AssignedMachineId)
                    && machineLookup.TryGetValue(stage.AssignedMachineId, out var smid))
                {
                    machineIntId = smid;
                }

                _db.StageExecutions.Add(new StageExecution
                {
                    JobId = job.Id,
                    ProductionStageId = stage.ProductionStageId,
                    // ProcessStageId remains null — no ManufacturingProcess exists
                    SortOrder = stage.ExecutionOrder,
                    EstimatedHours = estHours,
                    EstimatedCost = stage.EstimatedCost,
                    MaterialCost = stage.MaterialCost,
                    SetupHours = stage.SetupTimeMinutes.HasValue ? stage.SetupTimeMinutes.Value / 60.0 : null,
                    QualityCheckRequired = stage.ProductionStage?.RequiresQualityCheck ?? true,
                    MachineId = machineIntId,
                    CreatedBy = job.CreatedBy,
                    LastModifiedBy = job.LastModifiedBy
                });
            }

            await _db.SaveChangesAsync();
            await _scheduler.AutoScheduleJobAsync(job.Id, job.ScheduledStart);
        }
    }
}
```

**Note**: This fix requires access to `IManufacturingProcessService` (or its CalculateStageDuration helper) injected into `JobService`. Alternatively, use `ManufacturingProcessService.CalculateStageDuration()` directly or inline the simple duration calculation.

---

## Dependencies

- Requires `IManufacturingProcessService` injected into `JobService` (or inline the duration math)
- FIX-01 should land first or simultaneously so that `AutoScheduleJobAsync` correctly routes the newly-set ProcessStage references

---

## Expected Outcome After Fix

- Jobs created directly (not via build workflow) for parts with a `ManufacturingProcess` will have `ProcessStageId` set on all `StageExecution` records
- `AutoScheduleJobAsync` will correctly route stages to their assigned machines
- EMA learning will fire on stage completion
- Plate release trigger will work if applicable
- Duplicate job protection in `CreatePartStageExecutionsAsync` will work correctly (checks `ManufacturingProcessId != null`)
