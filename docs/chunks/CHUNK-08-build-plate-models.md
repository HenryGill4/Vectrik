> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# CHUNK-08: Build Plate Models + Migration

> **Size**: M (Medium) — ~6-8 file edits + migration
> **ROADMAP tasks**: BP.1, BP.2, BP.3, BP.4, BP.5, BP.6
> **Prerequisites**: CHUNK-07 complete

---

## Scope

Add the database model changes needed for the SLS build plate multi-part flow:
BuildPackageRevision model, new fields on BuildPackage and StageExecution, and
the IsBuildLevelStage flag on ProductionStage. Run migration.

---

## Files to Read First

| File | Why |
|------|-----|
| `Models/BuildPackage.cs` | Add CurrentRevision + BuildParameters |
| `Models/BuildFileInfo.cs` | Understand slice file data |
| `Models/StageExecution.cs` | Add BuildPackageId FK |
| `Models/ProductionStage.cs` | Add IsBuildLevelStage flag |
| `Data/TenantDbContext.cs` | Add DbSet + relationships |

---

## Tasks

### 1. Create BuildPackageRevision model (BP.1)
**New File**: `Models/BuildPackageRevision.cs`
```csharp
public class BuildPackageRevision
{
    public int Id { get; set; }
    public int BuildPackageId { get; set; }
    public virtual BuildPackage BuildPackage { get; set; } = null!;
    public int RevisionNumber { get; set; }
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public string ChangedBy { get; set; } = string.Empty;
    public string? ChangeNotes { get; set; }
    public string PartsSnapshotJson { get; set; } = "[]";
    public string? ParametersSnapshotJson { get; set; }
}
```

### 2. Add fields to BuildPackage (BP.2)
**File**: `Models/BuildPackage.cs`
Add:
- `int? CurrentRevision` — current revision number
- `string? BuildParameters` — JSON for build-level params (layer thickness, laser power, etc.)
- `ICollection<BuildPackageRevision> Revisions` nav property

### 3. Add BuildPackageId to StageExecution (BP.3)
**File**: `Models/StageExecution.cs`
Add:
- `int? BuildPackageId` — nullable FK to link build-level executions
- `virtual BuildPackage? BuildPackage` nav property

### 4. Add IsBuildLevelStage to ProductionStage (BP.4)
**File**: `Models/ProductionStage.cs`
Add:
- `bool IsBuildLevelStage { get; set; }` — marks stages where build plate moves as unit

### 5. Register in TenantDbContext (BP.5)
**File**: `Data/TenantDbContext.cs`
- Add `DbSet<BuildPackageRevision> BuildPackageRevisions`
- Configure FK relationships in `OnModelCreating`:
  - BuildPackageRevision → BuildPackage
  - StageExecution → BuildPackage (optional)

### 6. Run migration
```bash
dotnet ef migrations add AddBuildPlateSupport --context TenantDbContext --output-dir Data/Migrations/Tenant
```

### 7. Seed IsBuildLevelStage (BP.6)
**File**: `Program.cs` or `Services/DataSeedingService.cs`
- Find where production stages are seeded
- Set `IsBuildLevelStage = true` for stages with slugs: "sls-printing", "depowdering", "wire-edm"
- Verify the slug values match what's in the seeder

---

## Verification

1. Build passes
2. Migration generates cleanly
3. `dotnet ef database update --context TenantDbContext` succeeds
4. BuildPackage has CurrentRevision and BuildParameters columns
5. StageExecution has BuildPackageId column
6. ProductionStage has IsBuildLevelStage column
7. SLS-related stages have IsBuildLevelStage = true after seeding

---

## Files Modified (fill in after completion)

- `Models/BuildPackageRevision.cs` — **Created** (BP.1): New model with Id, BuildPackageId, RevisionNumber, RevisionDate, ChangedBy, ChangeNotes, PartsSnapshotJson, ParametersSnapshotJson
- `Models/BuildPackage.cs` — **Modified** (BP.2): Added `CurrentRevision`, `BuildParameters`, `Revisions` nav property
- `Models/StageExecution.cs` — **Modified** (BP.3): Added `BuildPackageId` nullable FK + `BuildPackage` nav property
- `Models/ProductionStage.cs` — **Modified** (BP.4): Added `IsBuildLevelStage` bool property
- `Data/TenantDbContext.cs` — **Modified** (BP.5): Added `DbSet<BuildPackageRevision>`, added BuildPackageRevision→BuildPackage FK config, added StageExecution→BuildPackage FK config
- `Services/DataSeedingService.cs` — **Modified** (BP.6): Set `IsBuildLevelStage = true` on sls-printing, depowdering, wire-edm stages
- `Data/Migrations/Tenant/20260318221712_AddBuildPlateSupport.cs` — **Generated**: Migration adds new columns + BuildPackageRevisions table
