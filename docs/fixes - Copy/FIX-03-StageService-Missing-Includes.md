# FIX-03: StageService Missing Navigation Property Includes

> **Priority**: HIGH — Causes null reference errors and missing data in shop floor and scheduler views
> **Affected file**: `Services/StageService.cs`

---

## Problem Summary

Several `StageService` query methods are missing `.Include()` calls for navigation properties that their consumers depend on. This causes null reference errors silently degraded to fallback display values (e.g. "Build #123" instead of the actual build package name).

---

## Issue 1 — `GetUnscheduledExecutionsAsync()` missing `ProcessStage` include

**File**: `Services/StageService.cs` ~line 533

**Impact**: The unscheduled sidebar in the Gantt view and any downstream consumer of unscheduled executions cannot access `ProcessStage` data (e.g. to determine machine preferences for auto-scheduling).

**Current query** (missing include):
```csharp
return await _db.StageExecutions
    .Include(e => e.Job).ThenInclude(j => j!.Part)
    .Include(e => e.Job).ThenInclude(j => j!.WorkOrderLine).ThenInclude(wl => wl!.WorkOrder)
    .Include(e => e.ProductionStage)
    .Include(e => e.Machine)
    // ← MISSING: .Include(e => e.ProcessStage)
    .Where(...)
```

**Fix**: Add `.Include(e => e.ProcessStage)` after `.Include(e => e.Machine)`.

---

## Issue 2 — `GetCurrentExecutionForOperatorAsync()` missing `BuildPackage` include

**File**: `Services/StageService.cs` ~line 442

**Impact**: `ShopFloor/Index.razor` displays the current operator's active execution. For build-level stages (SLS printing, depowdering, etc.) the display shows `exec.BuildPackage?.Name`. With the include missing, `BuildPackage` is always null → display falls back to `$"Build #{exec.BuildPackageId}"`.

**Current query** (missing include):
```csharp
return await _db.StageExecutions
    .Include(e => e.Job).ThenInclude(j => j!.Part)
    .Include(e => e.ProductionStage)
    .Include(e => e.Machine)
    // ← MISSING: .Include(e => e.BuildPackage)
    .Where(e => e.OperatorUserId == operatorUserId
        && (e.Status == StageExecutionStatus.InProgress || e.Status == StageExecutionStatus.Paused))
    .FirstOrDefaultAsync();
```

**Fix**: Add `.Include(e => e.BuildPackage)` after `.Include(e => e.Machine)`.

---

## Issue 3 — `GetAvailableWorkAsync()` missing `BuildPackage` include

**File**: `Services/StageService.cs` ~line 454

**Impact**: `ShopFloor/Index.razor` shows a list of available (unassigned) work items. Build-level items display `exec.BuildPackage?.Name` for context. Without the include, build package names are never shown.

**Current query** (missing include):
```csharp
return await _db.StageExecutions
    .Include(e => e.Job).ThenInclude(j => j!.Part)
    .Include(e => e.ProductionStage)
    .Include(e => e.Machine)
    // ← MISSING: .Include(e => e.BuildPackage)
    .Where(e => e.OperatorUserId == null
        && e.Status == StageExecutionStatus.NotStarted
        && !e.IsUnmanned)
    .OrderBy(...)
    .Take(50)
    .ToListAsync();
```

**Fix**: Add `.Include(e => e.BuildPackage)` after `.Include(e => e.Machine)`.

---

## Issue 4 — `GetOperatorQueueAsync()` missing `BuildPackage` include

**File**: `Services/StageService.cs` ~line 421

**Impact**: The "My Queue" section of `ShopFloor/Index.razor` shows the operator's assigned pending work. Build-level tasks won't show their build package name.

**Current query** (missing include):
```csharp
return await _db.StageExecutions
    .Include(e => e.Job).ThenInclude(j => j!.Part)
    .Include(e => e.Job).ThenInclude(j => j!.WorkOrderLine).ThenInclude(wl => wl!.WorkOrder)
    .Include(e => e.ProductionStage)
    .Include(e => e.Machine)
    // ← MISSING: .Include(e => e.BuildPackage)
    .Where(e => e.OperatorUserId == operatorUserId
        && (e.Status == StageExecutionStatus.NotStarted
            || e.Status == StageExecutionStatus.InProgress
            || e.Status == StageExecutionStatus.Paused))
    .OrderBy(...)
    .ToListAsync();
```

**Fix**: Add `.Include(e => e.BuildPackage)` after `.Include(e => e.Machine)`.

---

## Issue 5 — `GetMachineQueueAsync()` missing `BuildPackage` and `ProcessStage` includes

**File**: `Services/StageService.cs` — the `GetMachineQueueAsync` method

**Impact**: Machine queue views can't display build context or access process stage configuration.

**Fix**: Add both `.Include(e => e.BuildPackage)` and `.Include(e => e.ProcessStage)`.

---

## All Fixes In One Place

For each method listed above, add the missing includes:

| Method | Add Include |
|--------|------------|
| `GetUnscheduledExecutionsAsync` | `.Include(e => e.ProcessStage)` |
| `GetCurrentExecutionForOperatorAsync` | `.Include(e => e.BuildPackage)` |
| `GetAvailableWorkAsync` | `.Include(e => e.BuildPackage)` |
| `GetOperatorQueueAsync` | `.Include(e => e.BuildPackage)` |
| `GetMachineQueueAsync` | `.Include(e => e.BuildPackage)`, `.Include(e => e.ProcessStage)` |

All additions go after the existing `.Include(e => e.Machine)` line in each query.

---

## Note: `CompleteStageExecutionAsync` include

`CompleteStageExecutionAsync` (~line 163) also loads without `ProcessStage`:
```csharp
var execution = await _db.StageExecutions
    .Include(e => e.ProductionStage)
    .Include(e => e.Job).ThenInclude(j => j!.Stages)
    .FirstOrDefaultAsync(e => e.Id == executionId);
```

This is acceptable because the method only uses `execution.ProcessStageId` (the FK int) for EMA and plate release, not the full `ProcessStage` navigation object. No change needed here — the FK check `if (execution.ProcessStageId.HasValue)` works without loading the nav property.
