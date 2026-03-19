# Parts & Manufacturing Refactor Plan

## Overview

Five phases, execute in order. Each phase ends with a migration + build-verify step.

---

## Phase 1 — Manufacturing Approach (DB-Configurable)

**Goal:** Replace the hardcoded `string[] _approaches` array with a DB-backed `ManufacturingApproach` entity that admins can manage.

### 1.1 Create Model — `Models/ManufacturingApproach.cs`

```csharp
public class ManufacturingApproach
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;         // "SLS-Based"

    [Required, MaxLength(50)]
    public string Slug { get; set; } = string.Empty;         // "sls-based"

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(10)]
    public string? IconEmoji { get; set; }                   // "🖨️"

    // Drives UI visibility in Parts/Edit.razor
    public bool IsAdditive { get; set; }                     // true → show Stacking + BatchDurations tabs
    public bool RequiresBuildPlate { get; set; }             // true → can be added to BuildPackage
    public bool HasPostPrintBatching { get; set; }           // true → show Depowdering/HeatTreat fields

    // JSON array of stage slugs to auto-suggest when this approach is selected
    // e.g. ["sls-print","depowdering","heat-treatment","cnc-machining","inspection"]
    public string DefaultRoutingTemplate { get; set; } = "[]";

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
```

### 1.2 Update `Part.cs`

- Remove: `[Required, MaxLength(100)] public string ManufacturingApproach { get; set; }`
- Add:
```csharp
public int? ManufacturingApproachId { get; set; }
public virtual ManufacturingApproach? ManufacturingApproach { get; set; }
```

### 1.3 Add DbSet to `AppDbContext.cs`

```csharp
public DbSet<ManufacturingApproach> ManufacturingApproaches { get; set; }
```

### 1.4 Create Service Interface — `Services/IManufacturingApproachService.cs`

```csharp
public interface IManufacturingApproachService
{
    Task<List<ManufacturingApproach>> GetAllAsync(bool activeOnly = true);
    Task<ManufacturingApproach?> GetByIdAsync(int id);
    Task<ManufacturingApproach> CreateAsync(ManufacturingApproach approach);
    Task<ManufacturingApproach> UpdateAsync(ManufacturingApproach approach);
    Task DeleteAsync(int id);
}
```

### 1.5 Implement `Services/ManufacturingApproachService.cs`

Standard EF Core CRUD — same pattern as `MaterialService` or `StageService`.

### 1.6 Register in DI — `Program.cs`

```csharp
builder.Services.AddScoped<IManufacturingApproachService, ManufacturingApproachService>();
```

### 1.7 Migration

```
dotnet ef migrations add AddManufacturingApproach
```

In the migration `Up()`, after creating the table, seed default approaches:

```csharp
migrationBuilder.InsertData("ManufacturingApproaches",
    columns: ["Name","Slug","IsAdditive","RequiresBuildPlate","HasPostPrintBatching","DefaultRoutingTemplate","DisplayOrder","IsActive","IconEmoji"],
    values: new object[,]
    {
        { "SLS-Based",             "sls-based",          true,  true,  true,  "[\"sls-print\",\"depowdering\",\"heat-treatment\",\"inspection\"]", 1,  true, "🖨️" },
        { "CNC Machining",         "cnc-machining",       false, false, false, "[\"cnc-machining\",\"inspection\"]",                               2,  true, "⚙️" },
        { "CNC Turning",           "cnc-turning",         false, false, false, "[\"cnc-turning\",\"inspection\"]",                                 3,  true, "🔩" },
        { "Wire EDM",              "wire-edm",            false, false, false, "[\"wire-edm\",\"inspection\"]",                                    4,  true, "⚡" },
        { "3D Printing (FDM)",     "fdm",                 true,  true,  false, "[\"fdm-print\",\"post-processing\",\"inspection\"]",               5,  true, "🖨️" },
        { "3D Printing (SLA)",     "sla",                 true,  true,  false, "[\"sla-print\",\"post-processing\",\"inspection\"]",               6,  true, "🖨️" },
        { "Additive + Subtractive","additive-subtractive",true,  true,  true,  "[\"sls-print\",\"depowdering\",\"cnc-machining\",\"inspection\"]", 7,  true, "🔧" },
        { "Sheet Metal",           "sheet-metal",         false, false, false, "[\"laser-cut\",\"bending\",\"inspection\"]",                       8,  true, "📐" },
        { "Casting",               "casting",             false, false, false, "[\"casting\",\"machining\",\"inspection\"]",                       9,  true, "🏭" },
        { "Assembly",              "assembly",            false, false, false, "[\"assembly\",\"inspection\"]",                                    10, true, "🔧" },
        { "Manual",                "manual",              false, false, false, "[\"manual-work\",\"inspection\"]",                                 11, true, "✋" },
        { "Other",                 "other",               false, false, false, "[]",                                                               12, true, "❓" },
    });
```

Also add a migration to copy the old `ManufacturingApproach` string into a temp column, then map it to the new FK via a SQL UPDATE:

```sql
UPDATE Parts p
JOIN ManufacturingApproaches m ON m.Slug = LOWER(REPLACE(p.ManufacturingApproach, ' ', '-'))
SET p.ManufacturingApproachId = m.Id
```

Then drop the old `ManufacturingApproach` string column.

### 1.8 Update `Components/Pages/Parts/Edit.razor`

- Remove: `string[] _approaches = [...]`
- Add: `private List<ManufacturingApproach> _approaches = new();`
- In `OnInitializedAsync`: `_approaches = await ApproachService.GetAllAsync();`
- Change the select dropdown to bind to `_part.ManufacturingApproachId`
- Drive tab visibility with the loaded approach:
  ```csharp
  private ManufacturingApproach? SelectedApproach =>
      _approaches.FirstOrDefault(a => a.Id == _part.ManufacturingApproachId);

  // Replace _showSlsFeatures flag with:
  private bool ShowAdditiveTabs => SelectedApproach?.IsAdditive == true;
  private bool ShowBatchDurationTab => SelectedApproach?.HasPostPrintBatching == true;
  ```
- When approach changes, auto-suggest routing stages from `DefaultRoutingTemplate` (offer a button "Apply default routing").

### 1.9 Create Admin Page — `Components/Pages/Admin/ManufacturingApproaches.razor`

- Route: `@page "/admin/manufacturing-approaches"`
- Pattern: identical to `Admin/Stages.razor` (table + modal form)
- Fields in form: Name, Slug, Description, IconEmoji, IsAdditive, RequiresBuildPlate, HasPostPrintBatching, DefaultRoutingTemplate (textarea), DisplayOrder, IsActive
- Add nav link in `Admin/Index.razor`

---

## Phase 2 — PartAdditiveBuildConfig (New Table)

**Goal:** Extract all SLS/stacking fields out of `Part.cs` into a dedicated 1-to-1 table. `Part.cs` stays lean; additive parts get this record automatically.

### 2.1 Create Model — `Models/PartAdditiveBuildConfig.cs`

```csharp
public class PartAdditiveBuildConfig
{
    public int Id { get; set; }

    [Required]
    public int PartId { get; set; }
    public virtual Part Part { get; set; } = null!;

    // --- Stacking ---
    public bool AllowStacking { get; set; }
    public int MaxStackCount { get; set; } = 1;

    [Range(0.1, 500)]
    public double? SingleStackDurationHours { get; set; }

    [Range(0.1, 500)]
    public double? DoubleStackDurationHours { get; set; }

    [Range(0.1, 500)]
    public double? TripleStackDurationHours { get; set; }

    [Required, Range(1, 100)]
    public int PartsPerBuildSingle { get; set; } = 1;

    [Range(1, 100)]
    public int? PartsPerBuildDouble { get; set; }

    [Range(1, 100)]
    public int? PartsPerBuildTriple { get; set; }

    public bool EnableDoubleStack { get; set; }
    public bool EnableTripleStack { get; set; }

    // --- Batch Stage Durations (post-print) ---
    [Range(0.1, 500)]
    public double? SlsBuildDurationHours { get; set; }

    [Range(1, 500)]
    public int? SlsPartsPerBuild { get; set; }

    [Range(0.1, 100)]
    public double? DepowderingDurationHours { get; set; }

    [Range(1, 100)]
    public int? DepowderingPartsPerBatch { get; set; }

    [Range(0.1, 100)]
    public double? HeatTreatmentDurationHours { get; set; }

    [Range(1, 100)]
    public int? HeatTreatmentPartsPerBatch { get; set; }

    [Range(0.1, 100)]
    public double? WireEdmDurationHours { get; set; }

    [Range(1, 100)]
    public int? WireEdmPartsPerSession { get; set; }

    // --- Computed (NotMapped) --- move all computed properties from Part.cs here ---
    [NotMapped]
    public double? SlsPerPartHours => ...

    [NotMapped]
    public bool HasStackingConfiguration => AllowStacking && SingleStackDurationHours.HasValue;

    // ... move GetStackDuration(), GetPartsPerBuild(), GetRecommendedStackLevel(),
    //     CalculateStackEfficiency(), ValidateStackingConfiguration() from Part.cs here
}
```

### 2.2 Update `Part.cs`

- **Remove** all fields listed in 2.1 (AllowStacking through WireEdmPartsPerSession)
- **Remove** `StageEstimateSingle` (replaced by `SingleStackDurationHours` in config)
- **Remove** all the `[NotMapped]` computed stacking properties
- **Remove** the stacking methods (GetStackDuration, GetRecommendedStackLevel, etc.)
- **Add** navigation property:
  ```csharp
  public virtual PartAdditiveBuildConfig? AdditiveBuildConfig { get; set; }
  ```

### 2.3 Update `AppDbContext.cs`

```csharp
public DbSet<PartAdditiveBuildConfig> PartAdditiveBuildConfigs { get; set; }
```

Add unique index on `PartId`:
```csharp
modelBuilder.Entity<PartAdditiveBuildConfig>()
    .HasIndex(c => c.PartId)
    .IsUnique();
```

### 2.4 Migration

```
dotnet ef migrations add AddPartAdditiveBuildConfig
```

In `Up()`, after creating the table, migrate existing data:
```sql
INSERT INTO PartAdditiveBuildConfigs
    (PartId, AllowStacking, MaxStackCount, SingleStackDurationHours, DoubleStackDurationHours,
     TripleStackDurationHours, PartsPerBuildSingle, PartsPerBuildDouble, PartsPerBuildTriple,
     EnableDoubleStack, EnableTripleStack, SlsBuildDurationHours, SlsPartsPerBuild,
     DepowderingDurationHours, DepowderingPartsPerBatch, HeatTreatmentDurationHours,
     HeatTreatmentPartsPerBatch, WireEdmDurationHours, WireEdmPartsPerSession)
SELECT Id, AllowStacking, MaxStackCount, SingleStackDurationHours, DoubleStackDurationHours,
     TripleStackDurationHours, PartsPerBuildSingle, PartsPerBuildDouble, PartsPerBuildTriple,
     EnableDoubleStack, EnableTripleStack, SlsBuildDurationHours, SlsPartsPerBuild,
     DepowderingDurationHours, DepowderingPartsPerBatch, HeatTreatmentDurationHours,
     HeatTreatmentPartsPerBatch, WireEdmDurationHours, WireEdmPartsPerSession
FROM Parts
WHERE AllowStacking = 1 OR SlsBuildDurationHours IS NOT NULL
```

Then drop the old columns from `Parts`.

### 2.5 Update `PartService.cs`

Wherever `GetPartAsync` / `GetAllPartsAsync` / `GetPartByIdAsync` are called, add `.Include(p => p.AdditiveBuildConfig)` to EF queries.

In `CreatePartAsync` / `UpdatePartAsync`: if `part.ManufacturingApproach.IsAdditive == true` and `part.AdditiveBuildConfig == null`, initialize a new `PartAdditiveBuildConfig { PartId = part.Id }`.

### 2.6 Update `Parts/Edit.razor`

- Stacking tab: change all `_part.AllowStacking`, `_part.SingleStackDurationHours`, etc. → `_part.AdditiveBuildConfig!.AllowStacking`, etc.
- BatchDurations tab: same substitution
- Ensure `_part.AdditiveBuildConfig` is initialized before the form renders (if null, create a new instance)

---

## Phase 3 — BuildPackage: Print Build System

**Goal:** BuildPackage becomes the authoritative planning unit for batch manufacturing. Duration is computed, not manually entered. Status locking creates revision snapshots.

### 3.1 Update `BuildPackagePart` (in `Models/BuildPackage.cs`)

Add fields:
```csharp
public int StackLevel { get; set; } = 1;                      // 1 = single, 2 = double, 3 = triple
public double? EstimatedBuildDurationHours { get; set; }       // per-part contribution to print time
public int? EstimatedTotalBuilds { get; set; }                 // ceil(Quantity / PartsPerBuild)
```

### 3.2 Update `BuildPackage` (in `Models/BuildPackage.cs`)

- Mark `EstimatedDurationHours` as `[NotMapped]` — it will be computed, not stored:
  ```csharp
  // Remove the stored column, replace with:
  [NotMapped]
  public double? EstimatedDurationHours =>
      Parts?.Any() == true
          ? Parts.Where(p => p.EstimatedBuildDurationHours.HasValue)
                 .Sum(p => p.EstimatedBuildDurationHours!.Value * (p.EstimatedTotalBuilds ?? 1))
          : null;
  ```

  **NOTE:** If you want this persisted for scheduling queries, keep a `CachedDurationHours` column that gets written when parts change.

- Add:
  ```csharp
  public bool IsLocked { get; set; }   // set to true on Draft→Ready transition
  ```

### 3.3 Update `BuildPackageRevision` — add snapshot

```csharp
// Add to BuildPackageRevision:
public string? SnapshotJson { get; set; }  // JSON of parts+quantities+stack levels at lock time
```

### 3.4 Update `IBuildPlanningService.cs`

Add methods:
```csharp
// Recomputes stack level + per-part build duration for all parts in a package,
// then updates EstimatedTotalBuilds and EstimatedBuildDurationHours on each BuildPackagePart
Task RecomputePackageDurationsAsync(int buildPackageId);

// Called when transitioning Draft→Ready. Creates a revision snapshot and sets IsLocked = true.
Task LockPackageAsync(int buildPackageId, string lockedBy);

// Reverts a locked package to Draft (creates a new revision note), clears IsLocked
Task UnlockPackageAsync(int buildPackageId, string unlockedBy, string reason);
```

### 3.5 Implement in `BuildPlanningService.cs`

`RecomputePackageDurationsAsync`:
1. Load package with `.Include(p => p.Parts).ThenInclude(pp => pp.Part).ThenInclude(p => p.AdditiveBuildConfig)`
2. For each `BuildPackagePart`:
   - Load `PartAdditiveBuildConfig`
   - Call `config.GetRecommendedStackLevel(pp.Quantity)` → set `pp.StackLevel`
   - `pp.EstimatedTotalBuilds = Math.Ceiling((double)pp.Quantity / config.GetPartsPerBuild(pp.StackLevel))`
   - `pp.EstimatedBuildDurationHours = config.GetStackDuration(pp.StackLevel)`
3. Save changes

`LockPackageAsync`:
1. Call `RecomputePackageDurationsAsync`
2. Serialize all parts to JSON for snapshot
3. Create `BuildPackageRevision` with `SnapshotJson`
4. Set `IsLocked = true`, increment `CurrentRevision`
5. Save

`AddPartToPackageAsync` (existing method): After adding the part, call `RecomputePackageDurationsAsync`.

### 3.6 Update `Builds/Index.razor`

- **Remove** `_newDuration` input from create modal (duration is now computed)
- **Add** per-part computed duration to the parts table:
  ```
  | Part | Qty | Stack | Builds | Build Duration | Est. Per-Part Hours |
  ```
- Show 🔒 icon on packages where `IsLocked = true`
- On `Draft→Ready` transition, call `BuildPlanningService.LockPackageAsync()` instead of just changing status
- Add "🔓 Reopen" button for Ready/Scheduled packages that calls `UnlockPackageAsync`
- **Filter parts dropdown**: only show parts where `ManufacturingApproach.RequiresBuildPlate == true`

### 3.7 Migration

```
dotnet ef migrations add BuildPackageEnhancements
```

Adds: `BuildPackagePart.StackLevel`, `BuildPackagePart.EstimatedBuildDurationHours`, `BuildPackagePart.EstimatedTotalBuilds`, `BuildPackage.IsLocked`, `BuildPackageRevision.SnapshotJson`.

---

## Phase 4 — Scheduling: Batch-Aware

**Goal:** Scheduler understands that batch stages (print, depowdering, heat-treat) produce one `StageExecution` shared across multiple parts, while per-part stages produce one execution per `PartInstance`.

### 4.1 Update `StageExecution.cs`

Add:
```csharp
// Groups multiple PartInstances into a single batch execution
// (e.g. "DEPOW-BATCH-{buildPackageId}-1", "DEPOW-BATCH-{buildPackageId}-2")
[MaxLength(100)]
public string? BatchGroupId { get; set; }

// How many part instances are in this batch execution
public int? BatchPartCount { get; set; }
```

### 4.2 Update `ISchedulingService.cs`

Add methods:
```csharp
/// <summary>
/// Creates and schedules the build-level print stage for a BuildPackage.
/// Creates one StageExecution with BuildPackageId set, EstimatedHours from package duration.
/// </summary>
Task<StageExecution> ScheduleBuildPrintAsync(int buildPackageId, DateTime? startAfter = null);

/// <summary>
/// After a print completes, groups PartInstances into depowdering batches
/// (batch size = PartAdditiveBuildConfig.DepowderingPartsPerBatch) and creates
/// one StageExecution per batch. Then chains heat-treatment batches.
/// Returns the list of created executions.
/// </summary>
Task<List<StageExecution>> SchedulePostPrintBatchesAsync(int buildPackageId);

/// <summary>
/// Returns the full execution chain for a build package in timeline order.
/// </summary>
Task<List<StageExecution>> GetBuildExecutionChainAsync(int buildPackageId);
```

### 4.3 Implement scheduling logic

**`ScheduleBuildPrintAsync`:**
1. Load `BuildPackage` with machine and parts
2. Find the `ProductionStage` where `IsBuildLevelStage = true` and `StageSlug = "sls-print"` (or the stage configured on the approach)
3. Create one `StageExecution`:
   - `BuildPackageId = buildPackageId`
   - `JobId = null` (build-level, not job-specific)
   - `EstimatedHours = buildPackage.CachedDurationHours`
   - `MachineId = machine assigned to package`
4. Call existing `FindEarliestSlotAsync` to schedule it
5. Return the execution

**`SchedulePostPrintBatchesAsync`:**
1. Load all `PartInstance` records where `BuildPackageId = buildPackageId`
2. Group by `DepowderingPartsPerBatch` (from `PartAdditiveBuildConfig` of each part)
3. For each depowdering batch group:
   - Create one `StageExecution` for the Depowdering stage
   - Set `BatchGroupId = "DEPOW-{buildPackageId}-{batchIndex}"`
   - Set `BatchPartCount = group.Count`
   - Schedule after print completion using `FindEarliestSlotAsync`
4. Repeat for HeatTreatment if `HasPostPrintBatching = true`
5. For stages NOT flagged `IsBuildLevelStage` or `IsBatchStage`: create one `StageExecution` per `PartInstance`

**`AutoScheduleJobAsync` (existing):**
- At the start, check if the job's first stage requirement has `IsBuildLevelStage = true`
- If yes, delegate to `ScheduleBuildPrintAsync` for the linked `BuildPackage`, then return
- Normal per-part scheduling proceeds for subsequent stages

### 4.4 Migration

```
dotnet ef migrations add AddBatchSchedulingFields
```

Adds: `StageExecution.BatchGroupId`, `StageExecution.BatchPartCount`.

---

## Phase 5 — Legacy Cleanup

**Goal:** Remove dead fields from `Part.cs` and clean up the schema.

### 5.1 Remove `RequiredStages` from `Part.cs`

- Delete the `[Obsolete]` `RequiredStages` property from `Part.cs`
- Search entire solution for any remaining references to `part.RequiredStages` and remove them
- Migration: `migrationBuilder.DropColumn("RequiredStages", "Parts")`

### 5.2 Consolidate `Material` string on `Part.cs`

`Part.cs` currently has both `public string Material` and `public int? MaterialId`. The string is legacy.

- Find all places that read `part.Material` (the string) for display — update them to use `part.MaterialEntity?.Name ?? part.Material`
- Once all reads go through `MaterialEntity`, mark the string as `[Obsolete]`
- In a follow-up migration, drop the `Material` column (do this only after confirming all queries use the FK path)

### 5.3 Remove `StageEstimateSingle` from `Part.cs`

This field was used as a fallback for `SingleStackDurationHours`. Since Phase 2 moves everything into `PartAdditiveBuildConfig`, there is no longer a need for it.

- Remove `StageEstimateSingle` from `Part.cs`
- Remove the `EffectiveSingleDuration` computed property that used it (it's now in `PartAdditiveBuildConfig`)
- Migration: `migrationBuilder.DropColumn("StageEstimateSingle", "Parts")`

---

## Execution Order Checklist

```
[ ] Phase 1: ManufacturingApproach entity + service + admin page
    [ ] Model, DbSet, service registered
    [ ] Migration (with seed data + data migration for existing Parts)
    [ ] Build passes, admin page works
    [ ] Parts/Edit.razor dropdown loads from DB
    [ ] Tab visibility driven by approach flags

[ ] Phase 2: PartAdditiveBuildConfig table
    [ ] Model created, DbSet added
    [ ] Migration (with data migration from Parts columns)
    [ ] Part.cs cleaned of extracted fields
    [ ] PartService queries updated (.Include AdditiveBuildConfig)
    [ ] Edit.razor Stacking + BatchDurations tabs updated
    [ ] Build passes, stacking still works end-to-end

[ ] Phase 3: BuildPackage enhancements
    [ ] BuildPackagePart fields added (StackLevel, EstimatedBuildDurationHours, EstimatedTotalBuilds)
    [ ] BuildPackage IsLocked added, EstimatedDurationHours made computed
    [ ] BuildPackageRevision SnapshotJson added
    [ ] IBuildPlanningService + implementation updated
    [ ] Migration run
    [ ] Builds/Index.razor updated (no manual duration, lock/unlock, filtered part dropdown)

[ ] Phase 4: Batch-aware scheduling
    [ ] StageExecution.BatchGroupId + BatchPartCount added
    [ ] ISchedulingService new methods added
    [ ] SchedulingService implementation updated
    [ ] AutoScheduleJobAsync updated to handle build-level stages
    [ ] Migration run

[ ] Phase 5: Legacy cleanup
    [ ] RequiredStages dropped
    [ ] StageEstimateSingle dropped
    [ ] Material string path reviewed and marked obsolete
    [ ] Final migration run
    [ ] Full build + smoke test
```

---

## Files Changed Summary

| File | Change |
|------|--------|
| `Models/ManufacturingApproach.cs` | **NEW** |
| `Models/PartAdditiveBuildConfig.cs` | **NEW** |
| `Models/Part.cs` | Remove stacking/batch/legacy fields, add FKs |
| `Models/BuildPackage.cs` | Add IsLocked, update BuildPackagePart fields |
| `Models/BuildPackageRevision.cs` | Add SnapshotJson |
| `Models/StageExecution.cs` | Add BatchGroupId, BatchPartCount |
| `Services/IManufacturingApproachService.cs` | **NEW** |
| `Services/ManufacturingApproachService.cs` | **NEW** |
| `Services/IBuildPlanningService.cs` | Add Recompute/Lock/Unlock methods |
| `Services/BuildPlanningService.cs` | Implement new methods |
| `Services/ISchedulingService.cs` | Add batch scheduling methods |
| `Services/SchedulingService.cs` | Implement batch-aware logic |
| `Services/IPartService.cs` | Include AdditiveBuildConfig in queries |
| `Services/PartService.cs` | Include + initialize AdditiveBuildConfig |
| `Components/Pages/Admin/ManufacturingApproaches.razor` | **NEW** |
| `Components/Pages/Admin/Index.razor` | Add nav link |
| `Components/Pages/Parts/Edit.razor` | Load approaches from DB, update tab visibility, update stacking/batch bindings |
| `Components/Pages/Builds/Index.razor` | Remove manual duration, add lock/unlock, show computed durations |
| `Data/Migrations/*.cs` | 4 new migrations (one per phase) |
| `Program.cs` | Register new service |
