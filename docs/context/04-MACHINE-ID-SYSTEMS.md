# Machine ID Systems Reference

> **Status**: Current — master reference for the dual machine ID problem
> **Critical**: Do not mix `Machine.Id` (int) with `Machine.MachineId` (string). They are two different identifiers.

---

## The Two Identifiers

```
Machine
  ├── Id         : int      ← database primary key, EF-generated, never changes
  └── MachineId  : string   ← human business identifier (e.g. "SLS-001", "EDM-02")
```

All modern FKs use `Machine.Id` (int). The `Machine.MachineId` (string) is a display/business concept.

---

## Where Each ID Is Used

### Models using `Machine.Id` (int) — NEW SYSTEM ✅

| Model | Field | Notes |
|-------|-------|-------|
| `ProcessStage` | `AssignedMachineId` (int?) | FK to Machine.Id |
| `ProcessStage` | `PreferredMachineIds` (string) | Comma-separated Machine.Id ints, e.g. "1,3,7" |
| `StageExecution` | `MachineId` (int?) | FK to Machine.Id |
| `Job` | `MachineId` (int?) | FK to Machine.Id |
| `BuildPackage` | `MachineId` (int?) | FK to Machine.Id |
| `ProductionBatch` | `AssignedMachineId` (int?) | FK to Machine.Id |
| `MachineProgram` | `MachineId` (int?) | FK to Machine.Id |

### Models using `Machine.MachineId` (string) — LEGACY/CATALOG ⚠️

| Model | Field | Notes |
|-------|-------|-------|
| `PartStageRequirement` | `AssignedMachineId` (string?) | Matches Machine.MachineId |
| `PartStageRequirement` | `PreferredMachineIds` (string?) | Comma-separated Machine.MachineId strings |
| `ProductionStage` | `DefaultMachineId` (string?) | Matches Machine.MachineId |

### Models with INCONSISTENT ID — BUG ❌

| Model | Field | Stored As | Intended To Match | Problem |
|-------|-------|-----------|------------------|---------|
| `ProductionStage` | `AssignedMachineIds` (string?) | Comma-separated int PKs, e.g. "1,2,3" | Should match Machine.Id | Code looks up via `machineLookup[string MachineId]` — always misses |

This inconsistency in `ProductionStage` is the root cause of scheduling routing failures. See `docs/fixes/FIX-01-ProductionStage-AssignedMachineIds-Lookup.md`.

---

## Lookup Dictionary Patterns

### Correct patterns in use:

```csharp
// For new-system int ID lookups:
var machineIdLookup = machines.ToDictionary(m => m.Id, m => m);       // int → Machine
// Usage:
machineIdLookup.TryGetValue(processStage.AssignedMachineId.Value, out var machine);

// For legacy string ID lookups:
var machineLookup = machines.ToDictionary(m => m.MachineId, m => m);  // string → Machine
// Usage:
machineLookup.TryGetValue(requirement.AssignedMachineId, out var machine);

// For string-to-int conversion (legacy → new):
var idConversion = machines.ToDictionary(m => m.MachineId, m => m.Id); // string → int
// Usage:
idConversion.TryGetValue(legacyStringId, out var intId);
```

### Broken pattern — DO NOT USE:

```csharp
// WRONG: using string MachineId lookup for AssignedMachineIds entries that are actually ints
var machineLookup = machines.ToDictionary(m => m.MachineId, m => m); // keyed by "SLS-001"
stage.GetAssignedMachineIds()  // returns ["1", "2", "3"] (int PKs as strings)
  .ForEach(sid => machineLookup.TryGetValue(sid, ...))  // "1" != "SLS-001" → never matches
```

---

## `ProductionStage` Field Summary

```csharp
// AssignedMachineIds: stores int Machine.Id values as comma-separated string
// e.g. "1,2,3" means machines with Id=1, Id=2, Id=3 can execute this stage
public string? AssignedMachineIds { get; set; }

// GetAssignedMachineIds() returns the raw string split — List<string> {"1","2","3"}
// These are NOT machine business IDs — they are int PKs stored as strings
public List<string> GetAssignedMachineIds() { ... }

// DefaultMachineId: stores string Machine.MachineId (human business ID)
// e.g. "SLS-001" — completely different type than AssignedMachineIds entries
public string? DefaultMachineId { get; set; }
```

The fix adds `GetAssignedMachineIntIds()` that parses entries as `int` for correct lookup.

---

## `PartStageRequirement` Field Summary

```csharp
// AssignedMachineId: stores string Machine.MachineId (human business ID)
// e.g. "EDM-01"
public string? AssignedMachineId { get; set; }

// PreferredMachineIds: comma-separated Machine.MachineId strings
// e.g. "EDM-01,EDM-02"
public string? PreferredMachineIds { get; set; }
```

These are correctly looked up in `machineLookup` (keyed by `Machine.MachineId`) in `SchedulingService.ResolveMachines()`.

---

## Checklist for New Code

When writing code that involves machine assignment, answer these questions:

1. **Which machine ID type does my source model use?**
   - `ProcessStage.AssignedMachineId` → int → use `machineIdLookup`
   - `PartStageRequirement.AssignedMachineId` → string → use `machineLookup`
   - `ProductionStage.AssignedMachineIds` → int (stored as string) → parse as int, use `machineIdLookup`
   - `ProductionStage.DefaultMachineId` → string → use `machineLookup`

2. **What does my target field expect?**
   - `StageExecution.MachineId` → int (Machine.Id)
   - `Job.MachineId` → int (Machine.Id)
   - `ProcessStage.AssignedMachineId` → int (Machine.Id)
   - `PartStageRequirement.AssignedMachineId` → string (Machine.MachineId)

3. **Am I storing new machine preferences?**
   - Store as int (Machine.Id) in ProcessStage fields
   - Never create new PartStageRequirement records
