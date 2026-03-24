# FIX-05: Plate Release Trigger Fragile Dependency on ProcessStageId

> **Priority**: MEDIUM — Plate release auto-trigger silently skips when ProcessStageId is null. After FIX-02, this becomes reliable for new jobs. This fix adds a defensive fallback for old data.
> **Affected file**: `Services/StageService.cs`

---

## Problem

`CompleteStageExecutionAsync()` has a plate release trigger block:

**File**: `Services/StageService.cs` lines 250–267

```csharp
// Check if this build-level stage triggers plate release
if (execution.BuildPackageId.HasValue)
{
    var shouldRelease = false;

    // Check ProcessStageId against ManufacturingProcess.PlateReleaseStageId
    if (execution.ProcessStageId.HasValue)  // ← ONLY fires if ProcessStageId is set
    {
        var processStage = await _db.ProcessStages
            .Include(ps => ps.ManufacturingProcess)
            .FirstOrDefaultAsync(ps => ps.Id == execution.ProcessStageId.Value);

        if (processStage?.ManufacturingProcess?.PlateReleaseStageId == processStage?.Id)
            shouldRelease = true;
    }

    if (shouldRelease)
    {
        await _buildPlanning.CreatePartStageExecutionsAsync(
            execution.BuildPackageId.Value,
            execution.OperatorName ?? execution.CreatedBy ?? "System");
    }
}
```

If `execution.ProcessStageId == null` (which is the case for all jobs created via Path A — `JobService.CreateJobAsync()`), the block **silently skips** with `shouldRelease = false`. Plate release never auto-triggers.

**Practical impact**: For manually-created build jobs, operators must manually trigger plate release via the UI instead of it happening automatically when the plate release stage completes.

---

## When This Is a Problem

1. User creates a build job directly (not via `BuildSchedulingService.ScheduleBuildAsync`)
2. The last build-level stage completes
3. `ProcessStageId == null` → plate release trigger skipped
4. Per-part jobs are NOT auto-created
5. User must manually click "Release Plate" from the Scheduler / Builds view

After FIX-02, new jobs created via `JobService` will have `ProcessStageId` set, so this becomes less of an issue going forward. However, existing data and edge cases still benefit from a fallback.

---

## Fix — Defensive Fallback

Add a fallback that checks the `ManufacturingProcess.PlateReleaseStage.ProductionStageId` against the current execution's `ProductionStageId` when `ProcessStageId` is null.

**File**: `Services/StageService.cs` — replace lines 250–267

```csharp
if (execution.BuildPackageId.HasValue)
{
    var shouldRelease = false;

    if (execution.ProcessStageId.HasValue)
    {
        // New system: check if this ProcessStage is the designated plate release trigger
        var processStage = await _db.ProcessStages
            .Include(ps => ps.ManufacturingProcess)
            .FirstOrDefaultAsync(ps => ps.Id == execution.ProcessStageId.Value);

        if (processStage?.ManufacturingProcess?.PlateReleaseStageId == processStage?.Id)
            shouldRelease = true;
    }
    else
    {
        // Fallback: ProcessStageId not set (legacy-created execution)
        // Check if this ProductionStage matches the plate release stage's ProductionStageId
        // for any ManufacturingProcess of a part in this build
        var buildPartIds = await _db.BuildPackageParts
            .Where(bp => bp.BuildPackageId == execution.BuildPackageId.Value)
            .Select(bp => bp.PartId)
            .Distinct()
            .ToListAsync();

        if (buildPartIds.Any())
        {
            var plateReleaseProductionStageId = await _db.ManufacturingProcesses
                .Where(p => buildPartIds.Contains(p.PartId) && p.IsActive && p.PlateReleaseStageId.HasValue)
                .Select(p => (int?)p.PlateReleaseStage!.ProductionStageId)
                .FirstOrDefaultAsync();

            if (plateReleaseProductionStageId.HasValue
                && plateReleaseProductionStageId.Value == execution.ProductionStageId)
            {
                shouldRelease = true;
            }
        }
    }

    if (shouldRelease)
    {
        await _buildPlanning.CreatePartStageExecutionsAsync(
            execution.BuildPackageId.Value,
            execution.OperatorName ?? execution.CreatedBy ?? "System");
    }
}
```

**Note**: The fallback query is slightly more expensive (requires loading BuildPackageParts and ManufacturingProcess). It only executes when `ProcessStageId` is null AND the execution has a `BuildPackageId`. After FIX-02 lands, this fallback will become increasingly rare.

---

## Related: `ManufacturingProcess.PlateReleaseStageId` Must Be Set

The plate release trigger (both new system and fallback) depends on `ManufacturingProcess.PlateReleaseStageId` being configured. This is set either:
1. Manually in the process editor
2. Automatically by `ManufacturingProcessService.CreateProcessFromApproachAsync()` — sets it to the last build-level stage if no explicit trigger is marked in the template

If `PlateReleaseStageId` is null, the auto-trigger will never fire regardless of which fix is applied. Validate that all active `ManufacturingProcess` records for SLS parts have this set.

---

## Expected Outcome After Fix

- Plate release auto-triggers correctly even for jobs created before FIX-02
- No silent failures — the fallback path uses `ProductionStageId` matching as a reliable secondary check
- After FIX-02 is fully deployed and old data is backfilled, the fallback path becomes a no-op and can be removed
