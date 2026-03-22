# FIX-01: ProductionStage.AssignedMachineIds Lookup Type Mismatch

> **Priority**: CRITICAL — Root cause of all Gantt misrouting. Stages fall through to "all machines" fallback, piling onto the machine with the most free slots regardless of what machine should handle them.
> **Affected files**: 3 files

---

## Problem

`ProductionStage.AssignedMachineIds` stores comma-separated **`Machine.Id` int PKs** (e.g. `"1,2,3"`), but every consumer of `GetAssignedMachineIds()` looks them up as **`Machine.MachineId` business strings** (e.g. `"SLS-001"`).

### Where it breaks

#### 1. `SchedulingService.ResolveMachines()` — Step 6
**File**: `Services/SchedulingService.cs` lines 335–344

```csharp
// machineLookup is keyed by Machine.MachineId (string like "SLS-001")
var machineLookup = allMachines.ToDictionary(m => m.MachineId, m => m);

// GetAssignedMachineIds() returns ["1", "2"] (int PKs as strings)
var stageCapable = stage.GetAssignedMachineIds();
foreach (var sid in stageCapable)
{
    // sid = "1", machineLookup has "SLS-001" as key → NEVER MATCHES
    if (machineLookup.TryGetValue(sid, out var m) && !result.Contains(m))
        result.Add(m);
}
```

#### 2. `SchedulingService.ResolveMachines()` — Step 9
**File**: `Services/SchedulingService.cs` line 372

```csharp
result.AddRange(fallbackMachines
    .Where(m => stage.CanMachineExecuteStage(m.MachineId))
    // CanMachineExecuteStage calls GetAssignedMachineIds().Contains(m.MachineId)
    // GetAssignedMachineIds() = ["1","2"], m.MachineId = "SLS-001" → ALWAYS FALSE
    .OrderBy(m => m.Priority));
```

#### 3. `StagesView.GetCapableMachines()`
**File**: `Components/Pages/Scheduler/Views/StagesView.razor` line 190

```csharp
var assignedIds = stage.GetAssignedMachineIds(); // ["1","2"]
return Machines.Where(m => assignedIds.Contains(m.MachineId)).ToList();
// m.MachineId = "SLS-001", assignedIds has "1" → NEVER MATCHES
// Result: always empty → no machines shown, bottleneck detection always wrong
```

---

## Intended Behavior

`ProductionStage.AssignedMachineIds = "1,2"` should resolve to the machines whose `Machine.Id` is 1 and 2.

---

## Fix

### Step 1 — Add `GetAssignedMachineIntIds()` to `Models/ProductionStage.cs`

```csharp
/// <summary>
/// Returns assigned machine IDs as integers (Machine.Id), parsed from the
/// comma-separated AssignedMachineIds field which stores Machine.Id int values.
/// </summary>
public List<int> GetAssignedMachineIntIds()
{
    if (string.IsNullOrWhiteSpace(AssignedMachineIds))
        return new List<int>();
    var result = new List<int>();
    foreach (var entry in AssignedMachineIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (int.TryParse(entry, out var id))
            result.Add(id);
    }
    return result;
}
```

### Step 2 — Fix `SchedulingService.ResolveMachines()` Step 6
**File**: `Services/SchedulingService.cs` lines 335–344

```csharp
// BEFORE (broken):
var stageCapable = stage.GetAssignedMachineIds();
if (stageCapable.Any())
{
    foreach (var sid in stageCapable)
    {
        if (machineLookup.TryGetValue(sid, out var m) && !result.Contains(m))
            result.Add(m);
    }
}

// AFTER (fixed):
var stageCapableIds = stage.GetAssignedMachineIntIds();
if (stageCapableIds.Any())
{
    foreach (var intId in stageCapableIds)
    {
        if (machineIdLookup.TryGetValue(intId, out var m) && !result.Contains(m))
            result.Add(m);
    }
}
```

### Step 3 — Fix `SchedulingService.ResolveMachines()` Step 9
**File**: `Services/SchedulingService.cs` line 369–374

```csharp
// BEFORE (broken):
if (!result.Any() && stage.RequiresMachineAssignment)
{
    result.AddRange(fallbackMachines
        .Where(m => stage.CanMachineExecuteStage(m.MachineId))
        .OrderBy(m => m.Priority));
}

// AFTER (fixed):
if (!result.Any() && stage.RequiresMachineAssignment)
{
    var capableIntIds = stage.GetAssignedMachineIntIds();
    result.AddRange(fallbackMachines
        .Where(m => capableIntIds.Count == 0 || capableIntIds.Contains(m.Id))
        .OrderBy(m => m.Priority));
}
```

### Step 4 — Fix `StagesView.GetCapableMachines()`
**File**: `Components/Pages/Scheduler/Views/StagesView.razor` lines 185–202

```csharp
// BEFORE (broken):
private List<Machine> GetCapableMachines(ProductionStage stage)
{
    var assignedIds = stage.GetAssignedMachineIds();
    if (assignedIds.Count > 0)
    {
        return Machines.Where(m => assignedIds.Contains(m.MachineId)).ToList();
    }
    ...
}

// AFTER (fixed):
private List<Machine> GetCapableMachines(ProductionStage stage)
{
    var assignedIntIds = stage.GetAssignedMachineIntIds();
    if (assignedIntIds.Count > 0)
    {
        return Machines.Where(m => assignedIntIds.Contains(m.Id)).ToList();
    }
    ...
}
```

---

## Note on `CanMachineExecuteStage()`

The existing method `ProductionStage.CanMachineExecuteStage(string machineId)` is still broken after the above fix because it still uses `GetAssignedMachineIds()`. However, after the fix to step 9 above, this method is no longer called in the critical path. It remains for legacy use but should be noted as unreliable when `AssignedMachineIds` stores int PKs.

Consider adding an overload:
```csharp
public bool CanMachineExecuteStage(int machineId)
{
    var assigned = GetAssignedMachineIntIds();
    return assigned.Count == 0 || assigned.Contains(machineId);
}
```

---

## Expected Outcome After Fix

- Stages with `AssignedMachineIds = "2"` route exclusively to Machine.Id=2
- StagesView shows correct machine badges and non-zero capacity bars
- Gantt shows stages distributed across their correct machines instead of piled on one
- `CanMachineExecuteStage(int)` works correctly for scheduling validation
