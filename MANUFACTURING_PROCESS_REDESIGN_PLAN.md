# Manufacturing Workflow Redesign — Implementation Plan

> **Note**: This plan is a reference document for implementation in Visual Studio. It is not intended to be executed by Claude Code. All file paths, entity designs, and method signatures are provided for manual implementation.

## Context

The manufacturing workflow system has grown organically, resulting in:
- **Hardcoded stage behaviors**: `BuildPackage` has `DepowderingHours`, `HeatTreatmentHours`, `WireEdmHours` fields. `PartAdditiveBuildConfig` hardcodes the same. `BuildPlanningService.GetBuildStageDuration()` has a slug-based switch statement.
- **No process definition entity**: Stage routing is scattered across `ManufacturingApproach` (unused `DefaultRoutingTemplate`), `PartStageRequirement` (per-part, no duration modes), and `ProductionStage` (global flags like `IsBuildLevelStage`, `IsBatchStage`).
- **No formal Batch entity**: Batching is an ad-hoc string (`StageExecution.BatchGroupId` = `"DEPOW-{buildPackageId}-1"`) with no lifecycle, capacity tracking, or history.
- **No batch consolidation or splitting logic**: Nothing calculates batch counts or handles merging batches at downstream machines.
- **Duration modes not configurable**: A stage is either build-level or per-part, with no compound support (setup per-batch + run per-part) and no per-process overrides.

**Goal**: A system where a user defines a Manufacturing Process per part type, schedules a build, and the system auto-expands into batches, individual part flow, and machine assignments with accurate timing.

**Design decisions confirmed with user**:
1. **Process scope**: Per part type (1:1 Part ↔ ManufacturingProcess)
2. **Batch lifecycle**: Re-batchable (parts can move between batches at certain stages)
3. **Consolidation**: Machine-driven (merge only if machine has capacity for all parts)
4. **Duration mode**: Configurable per process stage (same global stage can be per-part in one process and per-batch in another); compound supported (setup + run)
5. **Plate release**: Configurable per process (user picks which stage releases parts from plate)
6. **Migration**: Greenfield OK (no production data to preserve)
7. **Job entity**: Keep and extend with scope (Build/Batch/Part) for scheduling, Gantt, and cost tracking
8. **Batch history**: Full immutable history for ITAR/defense traceability

---

## Phase 1: Core Entities & Migration

### New Enums

**File**: `/home/user/Opcentrix-V3/Models/Enums/ManufacturingEnums.cs` (append)

```csharp
public enum ProcessingLevel { Build, Batch, Part }
public enum DurationMode { None, PerBuild, PerBatch, PerPart }
public enum BatchStatus { Open, Sealed, InProcess, Completed, Dissolved }
public enum BatchAssignmentAction { Assigned, Removed }
public enum JobScope { Build, Batch, Part }
```

### New Entity: `ManufacturingProcess`

**File**: `/home/user/Opcentrix-V3/Models/ManufacturingProcess.cs`

- `Id` (int PK)
- `PartId` (int FK, unique — 1:1 with Part)
- `ManufacturingApproachId` (int? FK — optional categorization)
- `Name` (string, max 200)
- `Description` (string?, max 500)
- `PlateReleaseStageId` (int? — FK to ProcessStage.Id, configurable trigger)
- `DefaultBatchCapacity` (int, default 60 — crate size for this part type)
- `IsActive`, `Version`, audit fields
- Navigation: `Stages` (ICollection\<ProcessStage\>)

### New Entity: `ProcessStage`

**File**: `/home/user/Opcentrix-V3/Models/ProcessStage.cs`

- `Id` (int PK)
- `ManufacturingProcessId` (int FK)
- `ProductionStageId` (int FK — references global catalog for name/icon/color/department)
- `ExecutionOrder` (int, 1-100)
- **Processing level**: `ProcessingLevel` enum (Build/Batch/Part)
- **Compound duration**:
  - `SetupDurationMode` (DurationMode enum, default None)
  - `SetupTimeMinutes` (double?)
  - `RunDurationMode` (DurationMode enum, default PerPart)
  - `RunTimeMinutes` (double?)
  - `DurationFromBuildConfig` (bool — for printing stage, pull from slicer data)
- **Batch settings**:
  - `BatchCapacityOverride` (int? — override process-level default)
  - `AllowRebatching` (bool)
  - `ConsolidateBatchesAtStage` (bool — attempt machine-driven merge here)
- **Machine**: `AssignedMachineId`, `RequiresSpecificMachine`, `PreferredMachineIds`
- **Cost**: `HourlyRateOverride`, `MaterialCost`
- **Workflow flags**: `IsRequired`, `IsBlocking`, `AllowParallelExecution`, `AllowSkip`, `RequiresQualityCheck`, `RequiresSerialNumber`, `IsExternalOperation`, `ExternalTurnaroundDays`
- **Custom**: `StageParameters` (JSON), `RequiredMaterials` (JSON), `RequiredTooling`, `QualityRequirements` (JSON), `SpecialInstructions`
- **Learning/EMA**: `ActualAverageDurationMinutes`, `ActualSampleCount`, `EstimateSource`
- Audit fields

**Duration calculation example**: CNC stage with `SetupDurationMode=PerBatch, SetupTimeMinutes=30, RunDurationMode=PerPart, RunTimeMinutes=8`. For a batch of 60 parts: total = 30 + (60 × 8) = 510 minutes.

### New Entity: `ProductionBatch`

**File**: `/home/user/Opcentrix-V3/Models/ProductionBatch.cs`

- `Id` (int PK)
- `BatchNumber` (string, max 50, unique — auto-generated)
- `OriginBuildPackageId` (int? FK — which build created this batch)
- `ContainerLabel` (string?, max 100 — "Crate A", "Tray #3")
- `Capacity` (int), `CurrentPartCount` (int, denormalized)
- `Status` (BatchStatus enum)
- `CurrentProcessStageId` (int? FK to ProcessStage)
- `AssignedMachineId` (int? FK to Machine)
- `StageExecutionId` (int? FK to StageExecution)
- Audit fields
- Navigation: `PartAssignments` (ICollection\<BatchPartAssignment\>)

### New Entity: `BatchPartAssignment`

**File**: `/home/user/Opcentrix-V3/Models/BatchPartAssignment.cs`

Immutable history record for ITAR traceability.

- `Id` (int PK)
- `ProductionBatchId` (int FK)
- `PartInstanceId` (int FK)
- `Action` (BatchAssignmentAction enum — Assigned/Removed)
- `Reason` (string?, max 200 — "Initial batch from build #42", "Consolidated for CNC")
- `AtProcessStageId` (int? — snapshot of stage at time of assignment)
- `Timestamp` (DateTime)
- `PerformedBy` (string, max 100)

### Modified Entities

| Entity | Changes |
|--------|---------|
| **Part** | Add `ManufacturingProcess` navigation property |
| **Job** | Add `Scope` (JobScope enum), `ProductionBatchId` (int? FK), `ManufacturingProcessId` (int? FK) |
| **StageExecution** | Add `ProductionBatchId` (int? FK), `ProcessStageId` (int? FK) |
| **PartInstance** | Add `CurrentBatchId` (int? FK to ProductionBatch) |
| **ProductionStage** | Mark `IsBuildLevelStage`, `IsBatchStage`, `BatchCapacity`, `TriggerPlateRelease` with `[Obsolete]` |
| **BuildPackage** | Mark `DepowderingHours`, `HeatTreatmentHours`, `WireEdmHours` with `[Obsolete]` |
| **PartAdditiveBuildConfig** | Mark `DepowderingDurationHours`, `DepowderingPartsPerBatch`, `HeatTreatmentDurationHours`, `HeatTreatmentPartsPerBatch`, `WireEdmDurationHours`, `WireEdmPartsPerSession` with `[Obsolete]` (stacking config stays) |

### Database Context

**File**: `/home/user/Opcentrix-V3/Data/TenantDbContext.cs`

- Add `DbSet<ManufacturingProcess>`, `DbSet<ProcessStage>`, `DbSet<ProductionBatch>`, `DbSet<BatchPartAssignment>`
- Add OnModelCreating: ManufacturingProcess 1:1 Part (unique index on PartId), ProcessStage cascade from ManufacturingProcess, ProductionBatch unique index on BatchNumber, BatchPartAssignment index on (PartInstanceId, Timestamp)

### Migration

Run: `dotnet ef migrations add ManufacturingProcessRedesign --context TenantDbContext`

---

## Phase 2: Service Layer

### New: `IManufacturingProcessService` / `ManufacturingProcessService`

**Files**: `/home/user/Opcentrix-V3/Services/IManufacturingProcessService.cs`, `ManufacturingProcessService.cs`

Key methods:
- `GetByPartIdAsync(int partId)` — load process with stages
- `CreateAsync`, `UpdateAsync`, `DeleteAsync` — CRUD
- `AddStageAsync`, `UpdateStageAsync`, `RemoveStageAsync`, `ReorderStagesAsync` — stage management
- `ValidateProcessAsync(int processId)` — check completeness (plate release defined for processes with build-level stages, etc.)
- `CalculateStageDuration(ProcessStage, int partCount, int batchCount, double? buildConfigHours)` → `DurationResult` (setupMinutes, runMinutes, totalMinutes, humanReadableBreakdown)
- `ExpandForBuildAsync(int buildPackageId)` → `ProcessExpansionResult` (build-level executions, batches, batch-level executions, part-level executions, jobs)
- `CloneProcessAsync(int sourceProcessId, int targetPartId, string createdBy)` — for creating new parts based on existing ones

### New: `IBatchService` / `BatchService`

**Files**: `/home/user/Opcentrix-V3/Services/IBatchService.cs`, `BatchService.cs`

Key methods:
- `CreateBatchesFromBuildAsync(int buildPackageId, int batchCapacity, string createdBy)` — splitting algorithm: `ceil(N / capacity)` batches, distributes PartInstances sequentially
- `AssignPartToBatchAsync`, `RemovePartFromBatchAsync` — with immutable BatchPartAssignment history
- `RebatchPartsAsync(List<int> partInstanceIds, int newCapacity, string reason, string performedBy)` — re-group parts into new batches
- `TryConsolidateBatchesAsync(List<int> batchIds, int targetMachineId, string performedBy)` → `ConsolidationResult` — machine-driven: merge if totalParts ≤ machineCapacity, else keep separate with explanation
- `GetAssignmentHistoryForPartAsync(int partInstanceId)` — ITAR traceability
- `SealBatchAsync`, `CompleteBatchAsync`, `DissolveBatchAsync` — lifecycle

### Modified: `BuildPlanningService`

**File**: `/home/user/Opcentrix-V3/Services/BuildPlanningService.cs`

- **`CreateBuildStageExecutionsAsync`** (line ~414): Rewrite to load ManufacturingProcess for build's parts, iterate ProcessStages where `ProcessingLevel == Build`, calculate duration via `CalculateStageDuration()` (no more hardcoded slug switch in `GetBuildStageDuration`), determine plate release from `ManufacturingProcess.PlateReleaseStageId`
- **`CreatePartStageExecutionsAsync`** (line ~576): Rewrite to create `ProductionBatch` entities via `IBatchService.CreateBatchesFromBuildAsync()`, create batch-level StageExecutions for `ProcessingLevel == Batch` stages, create part-level StageExecutions for `ProcessingLevel == Part` stages, use compound duration, handle `ConsolidateBatchesAtStage` flag
- **Remove `GetBuildStageDuration`** (line ~562): The hardcoded slug switch is replaced by `ManufacturingProcessService.CalculateStageDuration()`

### Modified: `BuildSchedulingService`

**File**: `/home/user/Opcentrix-V3/Services/BuildSchedulingService.cs`

- **`ScheduleBuildAsync`** (line ~23): Use `IManufacturingProcessService.ExpandForBuildAsync()` for the full expansion
- **`ReleasePlateAsync`** (line ~271): Check plate-release-trigger from `ManufacturingProcess.PlateReleaseStageId` (not `ProductionStage.TriggerPlateRelease`), create PartInstances, create batches via `IBatchService`, assign parts to batches with history

### Modified: `SchedulingService`

**File**: `/home/user/Opcentrix-V3/Services/SchedulingService.cs`

- **`ResolveMachines`** (line ~278): Read machine preferences from `ProcessStage` instead of `PartStageRequirement` (same 6-tier priority structure)
- **Duration calculation**: Support compound mode (setup + run × count) via `CalculateStageDuration`
- **`AutoScheduleJobAsync`**: Handle `JobScope` (Build/Batch/Part) for different scheduling behavior

### Modified: `StageService`

- Plate release trigger check: Compare `execution.ProcessStageId` against `ManufacturingProcess.PlateReleaseStageId` instead of `ProductionStage.TriggerPlateRelease`

### Modified: `DataSeedingService`

- Add `SeedManufacturingProcessesAsync()` creating example processes for seeded parts
- Remove behavioral flags from seeded ProductionStages (mark as deprecated defaults)

### Register Services

**File**: `/home/user/Opcentrix-V3/Program.cs` — add `IManufacturingProcessService` and `IBatchService` DI registrations

---

## Phase 3: UI

### New: Process Editor Component

**File**: `/home/user/Opcentrix-V3/Components/Pages/Parts/ProcessEditor.razor`

Replaces the Routing tab in `Parts/Edit.razor`. Features:
- Visual stage flow editor with drag-and-drop reordering
- Per stage: select from ProductionStage catalog, set ProcessingLevel (Build/Batch/Part), configure compound duration (setup mode + run mode), machine preferences, batch settings
- Plate release trigger selector (dropdown of stages in the process)
- Default batch capacity setting
- Validation panel (warnings for missing plate release, etc.)
- Duration preview calculator (enter part count → see estimated times per stage)

### New: Batch Management Page

**File**: `/home/user/Opcentrix-V3/Components/Pages/Production/Batches.razor`

- Active batch list with status, part count, origin build, current stage
- Batch detail: part list, assignment history (ITAR)
- Re-batch action: drag parts between batches or create new batch
- Consolidation assistant: select batches from same build → pick target machine → see feasibility → execute

### Modified: Parts/Edit.razor

**File**: `/home/user/Opcentrix-V3/Components/Pages/Parts/Edit.razor`

- Replace Routing tab content with `<ProcessEditor PartId="@partId" />` component
- Remove old PartStageRequirement-based routing UI

### Modified: Builds/Index.razor

**File**: `/home/user/Opcentrix-V3/Components/Pages/Builds/Index.razor`

- Remove hardcoded depowdering/heat-treatment/wire-EDM duration input fields
- Post-print stage durations read from ManufacturingProcess of parts in build
- Add batch preview: "72 parts / 60 per crate = 2 batches (60 + 12)"
- Show plate release trigger stage name (from process definition)

### Modified: Admin/Stages.razor

**File**: `/home/user/Opcentrix-V3/Components/Pages/Admin/Stages.razor`

- Simplify to catalog-only management (name, slug, icon, color, department, default duration, default rate)
- Remove behavioral flags that now live on ProcessStage (IsBuildLevelStage, IsBatchStage, TriggerPlateRelease, BatchCapacity)

### Modified: Scheduler Views

**Files**: `/home/user/Opcentrix-V3/Components/Pages/Scheduler/Views/`

- Add `JobScope` (Build/Batch/Part) as filterable dimension across all views
- Show batch-level jobs on Gantt with distinct styling
- Batch consolidation indicators on stage queue view
- BuildsView: show batches created from each build

---

## Phase 4: Cleanup

- Remove `[Obsolete]` fields from entities once all code paths use new model
- Deprecate `PartStageRequirement` usage in services (keep entity for historical queries)
- Final migration to drop deprecated columns
- Remove old routing UI code from Parts/Edit.razor

---

## Files Summary

### Create (8 files)
1. `/home/user/Opcentrix-V3/Models/ManufacturingProcess.cs`
2. `/home/user/Opcentrix-V3/Models/ProcessStage.cs`
3. `/home/user/Opcentrix-V3/Models/ProductionBatch.cs`
4. `/home/user/Opcentrix-V3/Models/BatchPartAssignment.cs`
5. `/home/user/Opcentrix-V3/Services/IManufacturingProcessService.cs`
6. `/home/user/Opcentrix-V3/Services/ManufacturingProcessService.cs`
7. `/home/user/Opcentrix-V3/Services/IBatchService.cs`
8. `/home/user/Opcentrix-V3/Services/BatchService.cs`

### Create (2 UI files)
9. `/home/user/Opcentrix-V3/Components/Pages/Parts/ProcessEditor.razor`
10. `/home/user/Opcentrix-V3/Components/Pages/Production/Batches.razor`

### Modify (14 files)
11. `/home/user/Opcentrix-V3/Models/Enums/ManufacturingEnums.cs` — add 5 enums
12. `/home/user/Opcentrix-V3/Models/Part.cs` — add ManufacturingProcess navigation
13. `/home/user/Opcentrix-V3/Models/Job.cs` — add Scope, ProductionBatchId, ManufacturingProcessId
14. `/home/user/Opcentrix-V3/Models/StageExecution.cs` — add ProductionBatchId, ProcessStageId
15. `/home/user/Opcentrix-V3/Models/PartInstance.cs` — add CurrentBatchId
16. `/home/user/Opcentrix-V3/Models/ProductionStage.cs` — mark deprecated fields
17. `/home/user/Opcentrix-V3/Models/BuildPackage.cs` — mark deprecated fields
18. `/home/user/Opcentrix-V3/Models/PartAdditiveBuildConfig.cs` — mark deprecated fields
19. `/home/user/Opcentrix-V3/Data/TenantDbContext.cs` — add DbSets + OnModelCreating
20. `/home/user/Opcentrix-V3/Services/BuildPlanningService.cs` — rewrite stage creation methods
21. `/home/user/Opcentrix-V3/Services/BuildSchedulingService.cs` — rewrite scheduling + plate release
22. `/home/user/Opcentrix-V3/Services/SchedulingService.cs` — update machine resolution + duration calc
23. `/home/user/Opcentrix-V3/Services/StageService.cs` — update plate release trigger check
24. `/home/user/Opcentrix-V3/Program.cs` — register new services

### Modify (4 UI files)
25. `/home/user/Opcentrix-V3/Components/Pages/Parts/Edit.razor` — replace routing tab
26. `/home/user/Opcentrix-V3/Components/Pages/Builds/Index.razor` — remove hardcoded fields
27. `/home/user/Opcentrix-V3/Components/Pages/Admin/Stages.razor` — simplify to catalog
28. `/home/user/Opcentrix-V3/Components/Pages/Scheduler/Views/` — add batch/scope support

---

## Verification

1. **Build**: `dotnet build` — must compile clean with no errors
2. **Migration**: `dotnet ef migrations add ManufacturingProcessRedesign --context TenantDbContext` — verify migration generates correctly
3. **Seed**: Run app, verify seeded ManufacturingProcess data appears for example parts
4. **Process Editor**: Navigate to Parts → Edit → Process tab, verify stage flow editor works (add/remove/reorder stages, set duration modes, set plate release trigger)
5. **Build expansion**: Create a BuildPackage with parts that have a ManufacturingProcess → schedule it → verify auto-expansion creates:
   - Build-level StageExecutions (with correct compound durations)
   - ProductionBatch entities (correct splitting: e.g., 72/60 = 2 batches)
   - Batch-level StageExecutions
   - Part-level StageExecutions
6. **Plate release**: Complete the plate-release-trigger stage → verify PartInstances created, assigned to batches, BatchPartAssignment history recorded
7. **Batch consolidation**: Have 2 batches from same build → attempt consolidation at a machine → verify machine-driven logic (merge if capacity allows, explain if not)
8. **Scheduler**: Verify Gantt shows Build/Batch/Part scope jobs correctly, batch consolidation indicators visible
9. **ITAR traceability**: View batch assignment history for a part instance — verify full chain of Assigned/Removed events
