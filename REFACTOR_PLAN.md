> ⚠️ **LEGACY** — Superseded by `docs/context/` and `docs/fixes/`. Do not use for new development.

# Parts & Manufacturing Refactor Plan

## Overview

Nine phases, execute in order. **Phases 2–4** are the rock-solid foundation (Parts + Scheduling). **Phases 5–9** are enhancement layers built on that foundation. Each phase ends with a migration + build-verify step.

Phase 1 is **COMPLETE** ✅ (ManufacturingApproach DB entity, service, admin page, 269 tests pass).

### Key Architectural Decisions (from real-world workflow)

These decisions drive every phase below:

| # | Decision | Impact |
|---|----------|--------|
| 1 | **Build duration is entered from the slicer**, not computed from parts | `BuildPackage.EstimatedDurationHours` stays as manual entry |
| 2 | **Mixed parts on same build plate** are supported | `BuildPackagePart` already handles multiple part types per package |
| 3 | **Parts-per-build fluctuates** — `Part` holds a planning guideline, actual comes from slicer nesting | `Part.AdditiveBuildConfig.PlannedPartsPerBuildSingle` = guideline; `BuildPackagePart.Quantity` = actual |
| 4 | **SLS machines schedule BUILDS, not parts** | `BuildPackage` is the schedulable entity on SLS machines, not `Job` |
| 5 | **Auto-changeover**: 2-plate capacity, 30min swap, operator must remove plate A before plate B finishes | `Machine.BuildPlateCapacity`, `ChangeoverMinutes`, scheduling constraint |
| 6 | **Smart scheduling**: if no operator for swap, suggest longer build (double-stack) to sync with shift | `AnalyzeChangeoverAsync` returns suggestions |
| 7 | **PartInstances created after EDM cut** (plate release point), not at job creation | `ReleasePlateAsync` creates PartInstances when plate is freed |
| 8 | **Deferred serialization**: temp tracking IDs until laser engraving stage | `PartInstance.IsSerialAssigned`, official serial at engraving |
| 9 | **External operations** (coating) need PO tracking, ATF notifications, auto-adjusting turnaround | `ExternalOperation` entity linked to `StageExecution` |
| 10 | **Operator roles**: assignable, one person can fill multiple roles | `OperatorRole` entity + `UserOperatorRole` junction |
| 11 | **Serial number format**: configurable per tenant, range generation | `SerialNumberConfig` entity |
| 12 | **Demand planning**: semi-auto build suggestions, mixed builds, scrap-to-requeue | `IDemandPlanningService` |
| 13 | **Shifts**: configurable (default 8–5 Mon–Fri) | `OperatingShift` already exists, confirm admin UI |
| 14 | **In-machine recovery vacuum**: happens while next build prints, doesn't need scheduling | Not modeled as a stage — informational only |
| 15 | **Shared resources**: only 1 plate per depowdering machine at a time | Post-print scheduling respects machine constraints |
| 16 | **Multi-tenant**: all entities isolated via `TenantDbContext` | No changes needed — architecture already supports this |

### Two-Track Production Model

```
Track A — Build-Centric (Additive/SLS):
  WorkOrder demand → BuildPackage (plate composition)
    → Slicer data entry (duration + actual counts)
    → Schedule build on SLS machine
    → Print → Auto-changeover → Post-print stages (depowder, heat-treat, EDM)
    → Plate Release → PartInstances created
    → Per-part stages (CNC, QC, engrave, sandblast, coat, assemble, ship)

Track B — Part-Centric (CNC/Traditional):
  WorkOrder demand → Job (per part routing)
    → Schedule stages on machines
    → Per-part execution through routing
```

---

## Phase 1 — Manufacturing Approach (DB-Configurable) ✅ COMPLETE

ManufacturingApproach entity created with 13 seeded approaches, service + admin page, Part.ManufacturingApproachId FK, Edit.razor updated. 269 tests pass.

---

## Phase 2 — PartAdditiveBuildConfig + Machine Enhancements

**Goal:** Extract all SLS/stacking/batch fields from `Part.cs` into a dedicated 1:1 table. Add auto-changeover and build-plate-capacity fields to `Machine`. Stacking/batch values on the Part become **planning guidelines** — actual per-build values come from the slicer at `BuildPackage` time. Ranges widened from 100 → 500 to support high-count plates (e.g., 76+ suppressors per plate).

### 2.1 Create Model — `Models/PartAdditiveBuildConfig.cs`

```csharp
public class PartAdditiveBuildConfig
{
    public int Id { get; set; }

    [Required]
    public int PartId { get; set; }
    public virtual Part Part { get; set; } = null!;

    // ── Stacking Guidelines ──────────────────────────────────
    // These are PLANNING ESTIMATES. Actual per-build values come from the
    // slicer and are recorded on BuildPackagePart.Quantity.
    public bool AllowStacking { get; set; }
    public int MaxStackCount { get; set; } = 1;

    [Range(0.1, 500)]
    public double? SingleStackDurationHours { get; set; }

    [Range(0.1, 500)]
    public double? DoubleStackDurationHours { get; set; }

    [Range(0.1, 500)]
    public double? TripleStackDurationHours { get; set; }

    // Planning guideline: typical parts per build at each stack level.
    // Range widened to 500 for high-count plates (76+ suppressors).
    [Required, Range(1, 500)]
    public int PlannedPartsPerBuildSingle { get; set; } = 1;

    [Range(1, 500)]
    public int? PlannedPartsPerBuildDouble { get; set; }

    [Range(1, 500)]
    public int? PlannedPartsPerBuildTriple { get; set; }

    public bool EnableDoubleStack { get; set; }
    public bool EnableTripleStack { get; set; }

    // ── Post-Print Batch Durations ───────────────────────────
    [Range(0.1, 500)]
    public double? DepowderingDurationHours { get; set; }

    [Range(1, 500)]
    public int? DepowderingPartsPerBatch { get; set; }

    [Range(0.1, 500)]
    public double? HeatTreatmentDurationHours { get; set; }

    [Range(1, 500)]
    public int? HeatTreatmentPartsPerBatch { get; set; }

    [Range(0.1, 500)]
    public double? WireEdmDurationHours { get; set; }

    [Range(1, 500)]
    public int? WireEdmPartsPerSession { get; set; }

    // ── Computed (NotMapped) — moved from Part.cs ────────────
    [NotMapped]
    public bool HasStackingConfiguration => AllowStacking && SingleStackDurationHours.HasValue;

    [NotMapped]
    public double? EffectiveSingleDuration => SingleStackDurationHours;

    [NotMapped]
    public bool HasValidDoubleStack => EnableDoubleStack
        && DoubleStackDurationHours.HasValue && PlannedPartsPerBuildDouble.HasValue;

    [NotMapped]
    public bool HasValidTripleStack => EnableTripleStack
        && TripleStackDurationHours.HasValue && PlannedPartsPerBuildTriple.HasValue;

    [NotMapped]
    public List<int> AvailableStackLevels
    {
        get
        {
            var levels = new List<int> { 1 };
            if (HasValidDoubleStack) levels.Add(2);
            if (HasValidTripleStack) levels.Add(3);
            return levels;
        }
    }

    // Move from Part.cs: GetStackDuration, GetPartsPerBuild,
    // GetRecommendedStackLevel, CalculateStackEfficiency, ValidateStackingConfiguration
    // (same logic, replace PartsPerBuildSingle → PlannedPartsPerBuildSingle etc.)
}
```

### 2.2 Update `Part.cs`

**Remove** (lines 56–109 + lines 136–259):
- `AllowStacking`, `SingleStackDurationHours`, `DoubleStackDurationHours`, `TripleStackDurationHours`
- `MaxStackCount`, `PartsPerBuildSingle`, `PartsPerBuildDouble`, `PartsPerBuildTriple`
- `EnableDoubleStack`, `EnableTripleStack`, `StageEstimateSingle`
- `SlsBuildDurationHours`, `SlsPartsPerBuild`
- `DepowderingDurationHours`, `DepowderingPartsPerBatch`
- `HeatTreatmentDurationHours`, `HeatTreatmentPartsPerBatch`
- `WireEdmDurationHours`, `WireEdmPartsPerSession`
- All `[NotMapped]` computed stacking/batch properties
- All stacking methods (`GetStackDuration`, `GetRecommendedStackLevel`, etc.)

**Add:**
```csharp
public virtual PartAdditiveBuildConfig? AdditiveBuildConfig { get; set; }
```

### 2.3 Update `Machine.cs`

**Add** after existing build dimension fields (line 57):
```csharp
// Build plate management
public int BuildPlateCapacity { get; set; } = 1;       // Plates the machine can hold (2 = auto-changeover)
public bool AutoChangeoverEnabled { get; set; }         // Machine auto-swaps plates
public double ChangeoverMinutes { get; set; } = 30;     // Time for plate swap

// Laser configuration (planning reference)
public int? LaserCount { get; set; }
```

### 2.4 Add DbSet + Configuration — `TenantDbContext.cs`

```csharp
public DbSet<PartAdditiveBuildConfig> PartAdditiveBuildConfigs { get; set; }
```

In `OnModelCreating`:
```csharp
modelBuilder.Entity<PartAdditiveBuildConfig>()
    .HasIndex(c => c.PartId)
    .IsUnique();
```

### 2.5 Migration — `AddPartAdditiveBuildConfig`

In `Up()`, after creating the table:

```sql
-- Copy existing data from Parts to PartAdditiveBuildConfigs
INSERT INTO PartAdditiveBuildConfigs
    (PartId, AllowStacking, MaxStackCount, SingleStackDurationHours, DoubleStackDurationHours,
     TripleStackDurationHours, PlannedPartsPerBuildSingle, PlannedPartsPerBuildDouble,
     PlannedPartsPerBuildTriple, EnableDoubleStack, EnableTripleStack,
     DepowderingDurationHours, DepowderingPartsPerBatch, HeatTreatmentDurationHours,
     HeatTreatmentPartsPerBatch, WireEdmDurationHours, WireEdmPartsPerSession)
SELECT Id, AllowStacking, MaxStackCount, SingleStackDurationHours, DoubleStackDurationHours,
     TripleStackDurationHours, PartsPerBuildSingle, PartsPerBuildDouble, PartsPerBuildTriple,
     EnableDoubleStack, EnableTripleStack,
     DepowderingDurationHours, DepowderingPartsPerBatch, HeatTreatmentDurationHours,
     HeatTreatmentPartsPerBatch, WireEdmDurationHours, WireEdmPartsPerSession
FROM Parts
WHERE AllowStacking = 1 OR SlsBuildDurationHours IS NOT NULL
```

Then drop old columns from Parts. Add Machine columns with defaults.

### 2.6 Update `PartService.cs`

- Add `.Include(p => p.AdditiveBuildConfig)` to all Part queries
- In `CreatePartAsync` / `UpdatePartAsync`: if `part.ManufacturingApproach?.IsAdditive == true` and `part.AdditiveBuildConfig == null`, auto-create a new `PartAdditiveBuildConfig { PartId = part.Id }`

### 2.7 Update `Parts/Edit.razor`

- Stacking tab: `_part.AllowStacking` → `_part.AdditiveBuildConfig!.AllowStacking`, etc.
- BatchDurations tab: same substitution
- Ensure `_part.AdditiveBuildConfig` initialized before form renders
- All `PartsPerBuildSingle` → `PlannedPartsPerBuildSingle` with "(planning guideline)" label in UI

### 2.8 Update Tests

- `PartModelTests.cs`: update all stacking tests to use `AdditiveBuildConfig` instead of direct Part properties
- Ensure 269+ tests still pass

---

## Phase 3 — Build-Centric Scheduling Core

**Goal:** `BuildPackage` becomes the authoritative scheduling unit for additive machines. Duration is entered from the slicer, not computed. Auto-changeover and operator availability drive scheduling decisions. Post-print stages are scheduled as build-level batch operations.

### Build Lifecycle

```
Draft ──→ Sliced ──→ Ready ──→ Scheduled ──→ Printing ──→ PostPrint ──→ Completed
  │          │         │          │             │            │
  │     Slicer data  Approved  Machine +     Active on    Post-print    All parts
  │      entered     for sched  time slot    machine      stages done   released
  │     (duration,   (IsLocked              (depowder,
  │      counts)      = true)                heat-treat,
  │                                          EDM)
  └── Cancelled (from any state)
```

### 3.1 Expand `BuildPackageStatus` Enum

In `Models/Enums/ManufacturingEnums.cs`:

```csharp
public enum BuildPackageStatus
{
    Draft,          // Parts being assembled onto plate
    Sliced,         // Slicer data entered (duration, actual counts)
    Ready,          // Approved for scheduling
    Scheduled,      // Assigned to machine + time slot
    Printing,       // Actively printing on machine
    PostPrint,      // Plate off printer, going through post-print stages
    Completed,      // All parts released as PartInstances
    Cancelled
}
```

### 3.2 Update `BuildPackagePart`

**Add** to `BuildPackagePart` in `Models/BuildPackage.cs`:

```csharp
public int StackLevel { get; set; } = 1;           // 1=single, 2=double, 3=triple

[MaxLength(500)]
public string? SlicerNotes { get; set; }            // Position/orientation notes from slicer
```

Note: `BuildPackagePart.Quantity` already serves as the actual parts-per-build for this part type on this plate. No separate `ActualPartsNested` needed — Quantity IS the slicer-determined count.

### 3.3 Update `BuildPackage`

**Add** to `BuildPackage` in `Models/BuildPackage.cs`:

```csharp
// Slicer data
public bool IsSlicerDataEntered { get; set; }

// Scheduling lock
public bool IsLocked { get; set; }                     // Set true on Ready → Scheduled

// Build lifecycle timestamps
public DateTime? PrintStartedAt { get; set; }
public DateTime? PrintCompletedAt { get; set; }
public DateTime? PlateReleasedAt { get; set; }          // After EDM cut → PartInstances created

// Changeover chain: links to the build that was printing before this one
public int? PredecessorBuildPackageId { get; set; }
public virtual BuildPackage? PredecessorBuildPackage { get; set; }
```

**Keep** `EstimatedDurationHours` as stored `double?` — this is the slicer-provided build time, manually entered. **NOT computed.**

### 3.4 Update `StageExecution.cs`

**Add** batch execution fields:

```csharp
// Groups multiple PartInstances into a single batch execution
// e.g. "DEPOW-{buildPackageId}-1", "HEAT-{buildPackageId}-1"
[MaxLength(100)]
public string? BatchGroupId { get; set; }

// How many part instances are in this batch execution
public int? BatchPartCount { get; set; }
```

### 3.5 Create `IBuildSchedulingService` — `Services/IBuildSchedulingService.cs`

```csharp
public interface IBuildSchedulingService
{
    /// <summary>
    /// Schedule a build on an SLS machine, respecting auto-changeover
    /// and operator availability during the changeover window.
    /// </summary>
    Task<BuildScheduleResult> ScheduleBuildAsync(int buildPackageId, int machineId, DateTime? startAfter = null);

    /// <summary>
    /// Find the earliest slot for a build on a specific machine,
    /// factoring in changeover time between consecutive builds.
    /// </summary>
    Task<BuildScheduleSlot> FindEarliestBuildSlotAsync(int machineId, double durationHours, DateTime notBefore);

    /// <summary>
    /// Get the full build timeline for a machine: scheduled, printing, changeover windows.
    /// </summary>
    Task<List<MachineTimelineEntry>> GetMachineTimelineAsync(int machineId, DateTime from, DateTime to);

    /// <summary>
    /// Check operator availability during changeover window.
    /// If unavailable, suggest alternative build config to sync with shift.
    /// </summary>
    Task<ChangeoverAnalysis> AnalyzeChangeoverAsync(int machineId, DateTime buildEndTime);

    /// <summary>
    /// Create build-level StageExecutions (print, depowder, heat-treat, EDM)
    /// for a scheduled build package. Respects shared-resource constraints
    /// (e.g., only 1 plate per depowder machine at a time).
    /// </summary>
    Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int buildPackageId, string createdBy);

    /// <summary>
    /// After all build-level stages complete (post-EDM), create PartInstances
    /// from the plate and schedule per-part stages.
    /// </summary>
    Task<PlateReleaseResult> ReleasePlateAsync(int buildPackageId, string releasedBy);

    /// <summary>
    /// Lock a build (Ready → Scheduled). Creates revision snapshot, sets IsLocked.
    /// </summary>
    Task LockBuildAsync(int buildPackageId, string lockedBy);

    /// <summary>
    /// Unlock a build back to Draft. Creates revision note with reason.
    /// </summary>
    Task UnlockBuildAsync(int buildPackageId, string unlockedBy, string reason);
}

public record BuildScheduleSlot(
    DateTime PrintStart,
    DateTime PrintEnd,
    DateTime ChangeoverStart,
    DateTime ChangeoverEnd,
    int MachineId,
    bool OperatorAvailableForChangeover);

public record BuildScheduleResult(
    BuildScheduleSlot Slot,
    ChangeoverAnalysis? ChangeoverInfo,
    List<StageExecution> BuildStageExecutions);

public record ChangeoverAnalysis(
    bool OperatorAvailable,
    DateTime ChangeoverWindowStart,
    DateTime ChangeoverWindowEnd,
    string? SuggestedAction,             // "Schedule double-stack build to sync with shift"
    double? SuggestedDurationHours);     // Duration that would sync with next shift start

public record MachineTimelineEntry(
    int BuildPackageId,
    string BuildName,
    DateTime PrintStart,
    DateTime PrintEnd,
    DateTime? ChangeoverStart,
    DateTime? ChangeoverEnd,
    BuildPackageStatus Status);

public record PlateReleaseResult(
    int BuildPackageId,
    List<PartInstance> CreatedInstances,
    List<Job> CreatedJobs,
    int TotalPartCount);
```

### 3.6 Implement `BuildSchedulingService`

**`ScheduleBuildAsync`:**
1. Load `BuildPackage`, verify status is `Ready` or `Sliced` with `IsSlicerDataEntered`
2. Get machine, verify it's an SLS/additive machine
3. `FindEarliestBuildSlotAsync` → find slot
4. If `machine.AutoChangeoverEnabled`:
   - `AnalyzeChangeoverAsync` → check operator at changeover time
   - If no operator → return result with `ChangeoverInfo.SuggestedAction` (don't block — user decides)
5. Set `BuildPackage.ScheduledDate`, `Status = Scheduled`, `IsLocked = true`
6. Link predecessor: set `PredecessorBuildPackageId` to whatever build is last on this machine
7. `CreateBuildStageExecutionsAsync` → create print + post-print stages
8. Save + create revision snapshot

**`FindEarliestBuildSlotAsync`:**
- Query `BuildPackages` scheduled on this machine (not `StageExecutions`)
- Add changeover buffer (`machine.ChangeoverMinutes`) between consecutive builds
- Use existing shift-aware time advancement (`AdvanceByWorkHours` pattern from `SchedulingService`)

**`AnalyzeChangeoverAsync`:**
1. Changeover window = `[buildEndTime, buildEndTime + machine.ChangeoverMinutes]`
2. Load active `OperatingShifts`, check if any shift covers the changeover window
3. If yes → `OperatorAvailable = true`
4. If no → calculate duration that would push build completion to next shift start:
   - `SuggestedDurationHours = hoursUntilNextShiftStart`
   - `SuggestedAction = "Schedule a {X}h build (double-stack) to sync with {shift} at {time}"`

**`CreateBuildStageExecutionsAsync`:**
1. Load BuildPackage with parts and their `AdditiveBuildConfig`
2. Determine build-level stages from the approach's `DefaultRoutingTemplate`:
   - Stages where `IsBuildLevelStage = true` or `IsBatchStage = true` (print, depowder, heat-treat, EDM)
3. For each build-level stage:
   - Create one `StageExecution` per BuildPackage (NOT per part)
   - `StageExecution.BuildPackageId = buildPackageId`, `JobId = null`
   - Print stage: `EstimatedHours = BuildPackage.EstimatedDurationHours`
   - Post-print stages: `EstimatedHours` from `AdditiveBuildConfig` batch durations
   - `BatchGroupId = "{STAGE_SLUG}-{buildPackageId}"`
   - `BatchPartCount = BuildPackage.TotalPartCount`
4. Post-print scheduling respects shared resources:
   - Depowdering machine: only 1 plate at a time → `FindEarliestSlotAsync` on depowder machine
   - Sequential: depowder → heat-treat → EDM

**`ReleasePlateAsync`:**
1. Verify all build-level `StageExecutions` with `BuildPackageId` are `Completed`
2. For each `BuildPackagePart`:
   - Create `PartInstance` records: quantity = `BuildPackagePart.Quantity`
   - `PartInstance.BuildPackageId = buildPackageId`
   - `PartInstance.SerialNumber = null` (deferred — see Phase 4)
   - `PartInstance.TemporaryTrackingId = auto-generated` (see Phase 4)
   - `PartInstance.Status = InProcess`
3. Set `BuildPackage.PlateReleasedAt = UtcNow`, `Status = Completed`
4. For each PartInstance, create a `Job` with remaining per-part stages from the part's routing
5. Return `PlateReleaseResult` with created instances and jobs

### 3.7 Register in DI — `Program.cs`

```csharp
builder.Services.AddScoped<IBuildSchedulingService, BuildSchedulingService>();
```

### 3.8 Migration — `AddBuildCentricScheduling`

Adds:
- `BuildPackageStatus` enum values: `Sliced`, `Printing`, `PostPrint`
- `BuildPackage`: `IsSlicerDataEntered`, `IsLocked`, `PrintStartedAt`, `PrintCompletedAt`, `PlateReleasedAt`, `PredecessorBuildPackageId`
- `BuildPackagePart`: `StackLevel`, `SlicerNotes`
- `StageExecution`: `BatchGroupId`, `BatchPartCount`

### 3.9 Update `Builds/Index.razor`

- **Add** "Enter Slicer Data" step: after assembling parts, button opens form for `EstimatedDurationHours` + review per-part quantities
- **Add** machine timeline view: Gantt-style showing builds on each SLS machine
- **Show** changeover analysis: when scheduling, display operator availability warning + suggestion
- **Add** "Release Plate" button: visible when all PostPrint stages complete
- **Show** 🔒 icon on locked/scheduled builds
- **Add** "🔓 Reopen" button for reopening builds (calls `UnlockBuildAsync`)
- **Filter** parts dropdown: only show parts where `ManufacturingApproach.RequiresBuildPlate == true`
- **Remove** manual duration input from create modal (moved to slicer data entry step)

---

## Phase 4 — Plate Release + PartInstance Lifecycle

**Goal:** PartInstances are created at plate release (after EDM cut), not at work-order creation. Serial numbers are deferred until laser engraving. Temp tracking IDs bridge the gap.

### 4.1 Update `PartInstance.cs`

```csharp
// Change from [Required] to nullable — serial assigned at laser engraving
[MaxLength(50)]
public string? SerialNumber { get; set; }

// Auto-generated tracking ID (assigned at plate release, before official serial)
[Required, MaxLength(50)]
public string TemporaryTrackingId { get; set; } = string.Empty;

// True after official serial is assigned at laser engraving stage
public bool IsSerialAssigned { get; set; }
```

### 4.2 Update `ISerialNumberService`

Add methods:
```csharp
/// <summary>
/// Generate a temporary tracking ID for a PartInstance (used before official serial).
/// Format: "TMP-{buildPackageId}-{partIndex:D3}" or similar.
/// </summary>
Task<string> GenerateTemporaryTrackingIdAsync(int buildPackageId, int index);

/// <summary>
/// Assign the official serial number to a PartInstance (called at laser engraving stage).
/// Uses the tenant's SerialNumberConfig for formatting.
/// </summary>
Task<PartInstance> AssignOfficialSerialAsync(int partInstanceId);

/// <summary>
/// Generate a range of serial numbers for bulk assignment.
/// </summary>
Task<List<string>> GenerateSerialRangeAsync(int count);
```

### 4.3 Update `SerialNumberService`

- `AssignSerialNumbersAsync`: update to set `TemporaryTrackingId` instead of `SerialNumber` when creating from plate release
- `AssignOfficialSerialAsync`: generates serial via `GenerateSerialNumberAsync`, sets `SerialNumber`, sets `IsSerialAssigned = true`
- `GenerateTemporaryTrackingIdAsync`: creates `"TMP-{buildPackageId}-{index:D4}"`

### 4.4 Integrate with Build Scheduling

In `BuildSchedulingService.ReleasePlateAsync` (Phase 3):
- Call `GenerateTemporaryTrackingIdAsync` for each PartInstance instead of generating a serial
- PartInstances are created with `SerialNumber = null`, `IsSerialAssigned = false`

In laser engraving `StageExecution` completion handler:
- Call `AssignOfficialSerialAsync` for the PartInstance being engraved
- This is where the serial number becomes permanent

### 4.5 Migration — `AddDeferredSerialization`

- `PartInstance.SerialNumber`: remove `NOT NULL` constraint (make nullable)
- `PartInstance`: add `TemporaryTrackingId` (`NOT NULL`, default `''`), `IsSerialAssigned` (`NOT NULL`, default `false`)
- Data migration: for existing PartInstances, set `TemporaryTrackingId = SerialNumber`, `IsSerialAssigned = true`

### 4.6 Update UI References

- Anywhere displaying `PartInstance.SerialNumber`, show `TemporaryTrackingId` as fallback:
  ```csharp
  instance.SerialNumber ?? instance.TemporaryTrackingId
  ```
- Part tracking views: show badge "Temp" vs "Serial" based on `IsSerialAssigned`

---

## Phase 5 — Operator Roles

**Goal:** Replace the single `User.Role` string and `ProductionStage.RequiredRole` string with a proper role system. Operators get assignable roles. One person can fill multiple roles. Stages require specific operator roles.

### 5.1 Create Model — `Models/OperatorRole.cs`

```csharp
public class OperatorRole
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;        // "SLS Operator"

    [Required, MaxLength(50)]
    public string Slug { get; set; } = string.Empty;        // "sls-operator"

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    // Navigation
    public virtual ICollection<UserOperatorRole> UserRoles { get; set; } = new List<UserOperatorRole>();
}
```

### 5.2 Create Junction — `Models/UserOperatorRole.cs`

```csharp
public class UserOperatorRole
{
    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public int OperatorRoleId { get; set; }
    public virtual OperatorRole OperatorRole { get; set; } = null!;

    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string AssignedBy { get; set; } = string.Empty;
}
```

### 5.3 Update `ProductionStage.cs`

**Add** FK (keep the old `RequiredRole` string for backward compat temporarily):
```csharp
public int? RequiredOperatorRoleId { get; set; }
public virtual OperatorRole? RequiredOperatorRole { get; set; }
```

### 5.4 Update `User.cs`

**Add** navigation:
```csharp
public virtual ICollection<UserOperatorRole> OperatorRoles { get; set; } = new List<UserOperatorRole>();
```

### 5.5 Service — `IOperatorRoleService`

Standard CRUD + role assignment:
```csharp
public interface IOperatorRoleService
{
    Task<List<OperatorRole>> GetAllAsync(bool activeOnly = true);
    Task<OperatorRole?> GetByIdAsync(int id);
    Task<OperatorRole> CreateAsync(OperatorRole role);
    Task<OperatorRole> UpdateAsync(OperatorRole role);
    Task DeleteAsync(int id);
    Task AssignRoleToUserAsync(int userId, int roleId, string assignedBy);
    Task RemoveRoleFromUserAsync(int userId, int roleId);
    Task<List<User>> GetUsersWithRoleAsync(int roleId);
    Task<bool> UserHasRoleAsync(int userId, int roleId);
}
```

### 5.6 Seed Default Roles

```
SLS Operator, CNC Operator, EDM Operator, QC Inspector, Laser Engraver,
Surface Finishing, Assembly Technician, Shipping Clerk, Scheduler, Supervisor
```

### 5.7 Migration — `AddOperatorRoles`

Create tables, add FK on ProductionStage, seed default roles.

### 5.8 Admin UI — `Components/Pages/Admin/OperatorRoles.razor`

- Route: `@page "/admin/operator-roles"`
- Pattern: identical to `Admin/Stages.razor`
- Also update user management to assign roles

---

## Phase 6 — External Operations + New Stages

**Goal:** Support external vendor operations (coating, heat-treat outsource, etc.) with PO tracking, ATF ship/receive notifications, and auto-adjusting turnaround estimates. Add missing production stages.

### 6.1 Update `ProductionStage.cs`

**Add:**
```csharp
public bool IsExternalOperation { get; set; }       // true = vendor-performed, needs PO tracking
public double? DefaultTurnaroundDays { get; set; }   // Expected vendor turnaround (initial estimate)
```

### 6.2 Create Model — `Models/ExternalOperation.cs`

```csharp
public class ExternalOperation
{
    public int Id { get; set; }

    [Required]
    public int StageExecutionId { get; set; }
    public virtual StageExecution StageExecution { get; set; } = null!;

    // Vendor
    [Required, MaxLength(200)]
    public string VendorName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? VendorContact { get; set; }

    // Purchase Order
    [MaxLength(100)]
    public string? PurchaseOrderNumber { get; set; }

    // Shipping
    public DateTime? ShipDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }

    [MaxLength(100)]
    public string? OutboundTrackingNumber { get; set; }

    [MaxLength(100)]
    public string? ReturnTrackingNumber { get; set; }

    // Turnaround tracking (auto-adjusting EMA like PartStageRequirement)
    public double? EstimatedTurnaroundDays { get; set; }     // User-entered initial estimate
    public double? ActualTurnaroundDays { get; set; }        // Computed from ship/receive dates
    public double? AverageTurnaroundDays { get; set; }       // EMA from historical data
    public int TurnaroundSampleCount { get; set; }

    // ATF Compliance (ITAR/defense parts)
    public bool RequiresAtfNotification { get; set; }
    public DateTime? AtfShipNotificationDate { get; set; }
    public DateTime? AtfReceiveNotificationDate { get; set; }
    public bool AtfShipNotified { get; set; }
    public bool AtfReceiveNotified { get; set; }

    // Status
    public int Quantity { get; set; }
    public int? ReceivedQuantity { get; set; }

    public string? Notes { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
}
```

### 6.3 Update `StageExecution.cs`

**Add** navigation:
```csharp
public virtual ExternalOperation? ExternalOperation { get; set; }
```

### 6.4 Service — `IExternalOperationService`

```csharp
public interface IExternalOperationService
{
    Task<ExternalOperation> CreateAsync(ExternalOperation operation);
    Task<ExternalOperation> UpdateAsync(ExternalOperation operation);
    Task<ExternalOperation?> GetByStageExecutionAsync(int stageExecutionId);
    Task<List<ExternalOperation>> GetPendingShipmentsAsync();
    Task<List<ExternalOperation>> GetAwaitingReturnAsync();
    Task RecordShipmentAsync(int id, DateTime shipDate, string? trackingNumber, string? poNumber);
    Task RecordReceiptAsync(int id, DateTime receiveDate, int receivedQuantity, string? trackingNumber);
    Task NotifyAtfShipAsync(int id, DateTime notificationDate);
    Task NotifyAtfReceiveAsync(int id, DateTime notificationDate);
}
```

### 6.5 Seed New Stages

Add to `SeedProductionStagesAsync`:
```
sandblasting         — "Sandblasting"          — IsBatchStage: true
external-coating     — "External Coating"      — IsExternalOperation: true, DefaultTurnaroundDays: 14
oil-sleeve           — "Oil & Sleeve Assembly"
packaging            — "Packaging & Shipping"
```

### 6.6 Migration — `AddExternalOperations`

Create `ExternalOperation` table, add `ProductionStage.IsExternalOperation`, `ProductionStage.DefaultTurnaroundDays`, seed new stages.

### 6.7 UI — External Operation Tracking

- On stage execution detail page: if `ProductionStage.IsExternalOperation`, show external operation form
- Dashboard widget: "Awaiting Return" with expected dates and overdue alerts
- ATF notification checklist for defense parts

---

## Phase 7 — Serial Number Configuration

**Goal:** Tenant-configurable serial number format, range generation capability, and clean integration with the deferred serialization from Phase 4.

### 7.1 Create Model — `Models/SerialNumberConfig.cs`

```csharp
public class SerialNumberConfig
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;        // "Default", "Defense Parts", etc.

    // Format template: {PREFIX}-{YEAR}-{SEQ:5}
    // Supported tokens: {PREFIX}, {YEAR}, {MONTH}, {SEQ:N} (N = pad length)
    [Required, MaxLength(200)]
    public string FormatTemplate { get; set; } = "{PREFIX}-{YEAR}-{SEQ:5}";

    [MaxLength(20)]
    public string Prefix { get; set; } = "SN";

    public int NextSequence { get; set; } = 1;

    // Auto-reset sequence yearly
    public bool ResetSequenceYearly { get; set; } = true;
    public int? LastResetYear { get; set; }

    public bool IsDefault { get; set; } = true;
    public bool IsActive { get; set; } = true;
}
```

### 7.2 Update `ISerialNumberService`

Replace the hardcoded prefix logic with config-driven generation:
```csharp
Task<string> GenerateSerialNumberAsync(int? configId = null);
Task<List<string>> GenerateSerialRangeAsync(int count, int? configId = null);
Task<SerialNumberConfig> GetDefaultConfigAsync();
Task<List<SerialNumberConfig>> GetAllConfigsAsync();
Task<SerialNumberConfig> SaveConfigAsync(SerialNumberConfig config);
```

### 7.3 Migration — `AddSerialNumberConfig`

Create table, seed default config matching current `SystemSettings` prefix logic. Migrate existing `SystemSettings["SerialNumberPrefix"]` into config.

### 7.4 Admin UI — `Components/Pages/Admin/SerialNumberConfig.razor`

- Route: `@page "/admin/serial-numbers"`
- Configure format templates, preview generated serials
- Generate range tool: enter count → preview serial range → download/print label sheet

---

## Phase 8 — Demand Planning

**Goal:** Semi-automatic build suggestions based on outstanding work order demand. Support mixed builds for plate efficiency. Scrap-to-requeue workflow. Queryable build history for scheduling reference.

### 8.1 Create Service — `IDemandPlanningService`

```csharp
public interface IDemandPlanningService
{
    /// <summary>
    /// Calculate outstanding demand: parts needed from released work orders
    /// minus parts already in process (PartInstances + scheduled builds).
    /// </summary>
    Task<List<DemandSummary>> GetOutstandingDemandAsync();

    /// <summary>
    /// Suggest builds to meet demand. Uses Part.AdditiveBuildConfig.PlannedPartsPerBuildSingle
    /// as a starting point. Suggests full plates, partial plates, and mixed-part opportunities.
    /// </summary>
    Task<List<BuildSuggestion>> SuggestBuildsAsync();

    /// <summary>
    /// Find mixed-build opportunities: combine different parts on one plate
    /// to improve fill efficiency when single-part plates would be under-filled.
    /// </summary>
    Task<List<MixedBuildSuggestion>> SuggestMixedBuildsAsync();

    /// <summary>
    /// Log parts as scrap, add back to demand queue, notify scheduler.
    /// </summary>
    Task ScrapAndRequeueAsync(int partInstanceId, string reason, string scrapBy);

    /// <summary>
    /// Queryable build history: past build compositions, durations, success rates.
    /// Used for reference when planning new builds.
    /// </summary>
    Task<List<BuildHistoryEntry>> GetBuildHistoryAsync(int? partId = null, int? machineId = null, int? limit = 50);
}

public record DemandSummary(
    int PartId,
    string PartNumber,
    string PartName,
    int TotalOrdered,
    int InProcess,           // PartInstances with Status = InProcess
    int InScheduledBuilds,   // Qty in BuildPackages with Status = Scheduled/Printing/PostPrint
    int Outstanding);        // TotalOrdered - InProcess - InScheduledBuilds

public record BuildSuggestion(
    int PartId,
    string PartNumber,
    int SuggestedQuantity,
    int StackLevel,
    double EstimatedDurationHours,   // From AdditiveBuildConfig planning data
    double PlateUtilization,         // % of max capacity used
    string Rationale);               // "Full plate single-stack: 76 parts"

public record MixedBuildSuggestion(
    List<MixedBuildLine> Parts,
    double EstimatedPlateUtilization,
    string Rationale);               // "Combine SUP-TUBE-001 (40) + SUP-BAFFLE-003 (20) for 78% fill"

public record MixedBuildLine(
    int PartId,
    string PartNumber,
    int SuggestedQuantity);

public record BuildHistoryEntry(
    int BuildPackageId,
    string BuildName,
    string MachineName,
    DateTime CompletedDate,
    double DurationHours,
    int TotalParts,
    List<string> PartNumbers,
    int StackLevel,
    bool HadScrap);
```

### 8.2 Implement `DemandPlanningService`

**`GetOutstandingDemandAsync`:**
1. Query `WorkOrderLines` where `WorkOrder.Status` is `Released` or `InProgress`
2. Sum `Quantity` per `PartId`
3. Subtract `PartInstances` in `InProcess`/`Passed` status
4. Subtract quantities in `BuildPackageParts` linked to non-complete `BuildPackages`
5. Return demand where `Outstanding > 0`

**`SuggestBuildsAsync`:**
1. For each part with outstanding demand:
2. Load `AdditiveBuildConfig` → get `PlannedPartsPerBuildSingle`, stack options
3. Calculate builds needed: `Math.Ceiling(outstanding / plannedPerBuild)`
4. For the last (partial) build, check if a mixed build would be more efficient
5. Return sorted by priority (from work order priority)

**`SuggestMixedBuildsAsync`:**
1. Find parts with `Outstanding < PlannedPartsPerBuildSingle` (partial plate demand)
2. Group by material + manufacturing approach (must match for mixing)
3. Suggest combinations that maximize plate fill
4. User manually confirms and arranges in slicer

**`ScrapAndRequeueAsync`:**
1. Update `PartInstance.Status = Scrapped`
2. Create scrap log entry
3. The demand calculation automatically picks up the shortfall (outstanding increases)
4. Optionally: notify scheduler that a build may need modification

### 8.3 Migration — None Required

Demand planning is a read/compute service — no new tables. `ScrapAndRequeue` uses existing `PartInstance.Status`.

### 8.4 UI — `Components/Pages/Scheduling/DemandPlanning.razor`

- Route: `@page "/scheduling/demand-planning"`
- Outstanding demand table with suggested builds
- "Create Build from Suggestion" button → pre-populates a new `BuildPackage`
- Mixed build suggestions with "Create Mixed Build" action
- Build history search: filter by part, machine, date range

---

## Phase 9 — Legacy Cleanup + Seed Data Update

**Goal:** Remove deprecated fields, update seed data to match real equipment specs.

### 9.1 Remove `RequiredStages` from `Part.cs`

- Delete the `[Obsolete] RequiredStages` property
- Search solution for remaining references, remove them
- Migration: `DROP COLUMN RequiredStages FROM Parts`

### 9.2 Consolidate `Material` String on `Part.cs`

- Update all reads of `part.Material` (string) to use `part.MaterialEntity?.Name ?? part.Material`
- Mark `Material` string as `[Obsolete]`
- In a follow-up migration: drop the `Material` column (only after confirming all queries use FK path)

### 9.3 Remove `StageEstimateSingle` from `Part.cs`

- Already superseded by `PartAdditiveBuildConfig.SingleStackDurationHours` in Phase 2
- Migration: `DROP COLUMN StageEstimateSingle FROM Parts`

### 9.4 Remove `SlsBuildDurationHours` + `SlsPartsPerBuild` from `Part.cs`

- These were for the old "Part defines its own build time" model
- Superseded by `BuildPackage.EstimatedDurationHours` (slicer-entered) in Phase 3
- Already extracted to `PartAdditiveBuildConfig` in Phase 2, but conceptually obsolete
- Remove from `PartAdditiveBuildConfig` if truly unused after Phase 3 implementation

### 9.5 Drop Old `ProductionStage.RequiredRole` String

- After Phase 5 migrates to `RequiredOperatorRoleId` FK
- Verify all references use the FK path, then drop the string column

### 9.6 Update Seed Data — Machines

Update `SeedMachinesAsync` to reflect real equipment:

| MachineId | Name | MachineType | BuildLength/Width/Height | BuildPlateCapacity | AutoChangeover | ChangeoverMin | LaserCount |
|-----------|------|-------------|-------------------------|--------------------|----------------|---------------|------------|
| M4-1 | M4 Onyx #1 | SLS | 450×450×400 | 2 | true | 30 | 6 |
| M4-2 | M4 Onyx #2 | SLS | 450×450×400 | 2 | true | 30 | 6 |
| INC1 | Incineris Depowder | Depowder | — | 1 | false | — | — |
| EDM1 | Wire EDM | EDM | — | — | false | — | — |
| CNC1 | Haas VF-2 | CNC | — | — | false | — | — |
| LATHE1 | CNC Lathe | CNC-Turning | — | — | false | — | — |

### 9.7 Update Seed Data — Stages

Add new stages from Phase 6. Update existing stages with `RequiredOperatorRoleId` from Phase 5. Ensure `IsBuildLevelStage` / `IsBatchStage` / `IsExternalOperation` flags are correct.

### 9.8 Final Migration — `LegacyCleanup`

Single migration that drops all deprecated columns.

---

## Execution Order Checklist

```
[x] Phase 1: ManufacturingApproach entity + service + admin page ✅ COMPLETE
    [x] Model, DbSet, service registered
    [x] Migration (with seed data)
    [x] Build passes, admin page works
    [x] Parts/Edit.razor dropdown loads from DB
    [x] Tab visibility driven by approach flags

[x] Phase 2: PartAdditiveBuildConfig + Machine Enhancements ✅ COMPLETE
    [x] Model created (PartAdditiveBuildConfig), DbSet added
    [x] Machine fields added (BuildPlateCapacity, AutoChangeoverEnabled, ChangeoverMinutes, LaserCount)
    [x] Migration (with data migration from Parts columns)
    [x] Part.cs cleaned of extracted fields
    [x] PartService queries updated (.Include AdditiveBuildConfig)
    [x] Edit.razor Stacking + BatchDurations tabs updated
    [x] Tests updated and passing
    [x] Build passes, stacking still works end-to-end

[x] Phase 3: Build-Centric Scheduling Core ✅ COMPLETE
    [x] BuildPackageStatus expanded (Sliced, Printing, PostPrint)
    [x] BuildPackage fields added (IsSlicerDataEntered, IsLocked, lifecycle timestamps, predecessor)
    [x] BuildPackagePart fields added (StackLevel, SlicerNotes)
    [x] StageExecution fields added (BatchGroupId, BatchPartCount)
    [x] IBuildSchedulingService + implementation
    [x] Migration run
    [x] Builds/Index.razor updated (slicer data entry, timeline, changeover analysis, plate release)
    [x] Build passes, scheduling works end-to-end

[x] Phase 4: Plate Release + PartInstance Lifecycle ✅ COMPLETE
    [x] PartInstance.SerialNumber made nullable
    [x] PartInstance.TemporaryTrackingId + IsSerialAssigned added
    [x] ISerialNumberService updated (temp IDs, deferred assignment, range generation)
    [x] Integration with BuildSchedulingService.ReleasePlateAsync
    [x] Migration run
    [x] UI updated to show temp vs official serial
    [x] Build passes

[x] Phase 5: Operator Roles ✅ COMPLETE
    [x] OperatorRole + UserOperatorRole models created
    [x] ProductionStage.RequiredOperatorRoleId FK added
    [x] User.OperatorRoles navigation added
    [x] IOperatorRoleService + implementation
    [x] Default roles seeded
    [x] Migration run
    [x] Admin UI for role management
    [x] Build passes

[x] Phase 6: External Operations + New Stages ✅ COMPLETE
    [x] ProductionStage.IsExternalOperation + DefaultTurnaroundDays added
    [x] ExternalOperation model created
    [x] IExternalOperationService + implementation
    [x] New stages seeded (sandblasting, external-coating, oil-sleeve, packaging)
    [x] Migration run
    [x] External operation tracking UI
    [x] Build passes

[ ] Phase 7: Serial Number Configuration
    [ ] SerialNumberConfig model created
    [ ] ISerialNumberService updated for config-driven generation
    [ ] Default config seeded
    [ ] Migration run
    [ ] Admin UI for serial number config + range generation
    [ ] Build passes

[ ] Phase 8: Demand Planning
    [ ] IDemandPlanningService + implementation
    [ ] Outstanding demand calculation
    [ ] Build suggestions + mixed build suggestions
    [ ] Scrap-to-requeue workflow
    [ ] Build history query
    [ ] Demand Planning UI page
    [ ] Build passes

[ ] Phase 9: Legacy Cleanup + Seed Data
    [ ] RequiredStages dropped from Part
    [ ] Material string marked obsolete / dropped
    [ ] StageEstimateSingle dropped
    [ ] RequiredRole string dropped from ProductionStage
    [ ] Seed data updated (M4 Onyx machines, new stages, correct flags)
    [ ] Final migration run
    [ ] Full build + smoke test
```

---

## Files Changed Summary

| File | Phase | Change |
|------|-------|--------|
| `Models/PartAdditiveBuildConfig.cs` | 2 | **NEW** — 1:1 with Part, stacking/batch planning data |
| `Models/Part.cs` | 2, 9 | Remove stacking/batch fields, add AdditiveBuildConfig nav, drop legacy |
| `Models/Machine.cs` | 2 | Add BuildPlateCapacity, AutoChangeoverEnabled, ChangeoverMinutes, LaserCount |
| `Models/Enums/ManufacturingEnums.cs` | 3 | Expand BuildPackageStatus enum |
| `Models/BuildPackage.cs` | 3 | Add lifecycle fields, IsLocked, predecessor chain |
| `Models/StageExecution.cs` | 3, 6 | Add BatchGroupId, BatchPartCount, ExternalOperation nav |
| `Models/PartInstance.cs` | 4 | SerialNumber nullable, add TemporaryTrackingId, IsSerialAssigned |
| `Models/OperatorRole.cs` | 5 | **NEW** — operator role definition |
| `Models/UserOperatorRole.cs` | 5 | **NEW** — User↔Role junction |
| `Models/ProductionStage.cs` | 5, 6 | Add RequiredOperatorRoleId FK, IsExternalOperation, DefaultTurnaroundDays |
| `Models/User.cs` | 5 | Add OperatorRoles nav |
| `Models/ExternalOperation.cs` | 6 | **NEW** — vendor PO/ATF tracking |
| `Models/SerialNumberConfig.cs` | 7 | **NEW** — configurable serial format |
| `Services/IBuildSchedulingService.cs` | 3 | **NEW** — build-centric scheduling |
| `Services/BuildSchedulingService.cs` | 3 | **NEW** — implementation |
| `Services/ISerialNumberService.cs` | 4, 7 | Add temp ID, deferred assignment, config-driven generation |
| `Services/SerialNumberService.cs` | 4, 7 | Implement updated interface |
| `Services/IOperatorRoleService.cs` | 5 | **NEW** |
| `Services/OperatorRoleService.cs` | 5 | **NEW** |
| `Services/IExternalOperationService.cs` | 6 | **NEW** |
| `Services/ExternalOperationService.cs` | 6 | **NEW** |
| `Services/IDemandPlanningService.cs` | 8 | **NEW** |
| `Services/DemandPlanningService.cs` | 8 | **NEW** |
| `Services/PartService.cs` | 2 | Include AdditiveBuildConfig, auto-create on save |
| `Services/DataSeedingService.cs` | 5, 6, 9 | Seed roles, new stages, updated machines |
| `Data/TenantDbContext.cs` | 2–7 | Add DbSets, relationships, indexes |
| `Program.cs` | 3, 5, 6, 7, 8 | Register new services |
| `Components/Pages/Parts/Edit.razor` | 2 | Rebind stacking/batch tabs to AdditiveBuildConfig |
| `Components/Pages/Builds/Index.razor` | 3 | Slicer data entry, timeline, changeover, plate release |
| `Components/Pages/Admin/OperatorRoles.razor` | 5 | **NEW** — role management |
| `Components/Pages/Admin/SerialNumberConfig.razor` | 7 | **NEW** — serial config + range preview |
| `Components/Pages/Scheduling/DemandPlanning.razor` | 8 | **NEW** — demand + build suggestions |
| `Components/Pages/Admin/Index.razor` | 5, 7 | Add nav links for new admin pages |
| `Data/Migrations/*.cs` | 2–9 | One migration per phase |
