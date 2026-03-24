> ⚠️ **LEGACY** — Historical reference only. Do not use for planning or development.

# CHUNK-09: Build Plate Execution Engine

> **Size**: L (Large) — ~8-12 file edits
> **ROADMAP tasks**: BP.7, BP.8, BP.9, BP.10, BP.11, BP.12, BP.13, BP.14, BP.15, BP.16
> **Prerequisites**: CHUNK-08 complete

---

## Scope

Build the service-layer logic for build plate execution: creating build-level
stage executions, splitting parts after EDM, duration allocation from slice files,
and build revision control.

---

## Files to Read First

| File | Why |
|------|-----|
| `Services/IBuildPlanningService.cs` | Extend with new methods |
| `Services/BuildPlanningService.cs` | Implement new methods |
| `Models/BuildPackage.cs` | Updated model from CHUNK-08 |
| `Models/BuildFileInfo.cs` | Slice file duration data |
| `Models/StageExecution.cs` | Updated model from CHUNK-08 |
| `Models/ProductionStage.cs` | IsBuildLevelStage flag |
| `Models/PartStageRequirement.cs` | Part routing for downstream stages |
| `Services/IStageService.cs` | May need stage lookup methods |

---

## Tasks

### 1. Duration from slice file (BP.7, BP.8, BP.9, BP.10)

Update `IBuildPlanningService` / `BuildPlanningService`:

**New method**: `Task UpdateBuildDurationFromSliceAsync(int buildPackageId)`
- Load BuildPackage with BuildFileInfo
- If BuildFileInfo.EstimatedPrintTimeHours exists:
  - Set BuildPackage.EstimatedDurationHours = that value
  - Per-part allocation: `EstimatedPrintTimeHours / TotalPartCount`
- Update any linked StageExecutions for the SLS printing stage

**Auto-trigger**: Call this when BuildFileInfo is saved/updated on a package.

### 2. Create build-level stage executions (BP.11, BP.12)

**New method**: `Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int buildPackageId, string createdBy)`
- Load all ProductionStages where `IsBuildLevelStage == true`, ordered by typical sequence
  (SLS Printing → Depowdering → Wire EDM)
- For each build-level stage, create ONE StageExecution:
  - `BuildPackageId = buildPackageId`
  - `Status = Queued`
  - SLS stage gets `EstimatedHours` from BuildFileInfo
  - Other stages get default durations from ProductionStage
- The Job on these executions is the "build job" (the BuildPackage's parent BuildJob)
- Return created executions

**Trigger**: When BuildPackage status → "Scheduled", call this method.

### 3. Create per-part stage executions after EDM (BP.13)

**New method**: `Task CreatePartStageExecutionsAsync(int buildPackageId, string createdBy)`
- Load BuildPackage with its parts (BuildJobParts)
- For each part in the package:
  - Load the Part's routing (PartStageRequirements)
  - **Skip** stages where `IsBuildLevelStage == true` (SLS, Depowder, EDM — already done)
  - Create individual StageExecutions for remaining stages (heat treat, CNC, QC, etc.)
  - Create a new Job per part linking to the WorkOrderLine
- This is called when the Wire EDM build-level stage completes

### 4. Build revision control (BP.14, BP.15, BP.16)

**New method**: `Task<BuildPackageRevision> CreateRevisionAsync(int buildPackageId, string changedBy, string? notes)`
- Snapshot current parts list (serialize BuildJobParts to JSON)
- Snapshot current parameters (BuildParameters JSON)
- Increment CurrentRevision
- Save BuildPackageRevision record

**Auto-create revision when**:
- Parts are added/removed from a BuildPackage (in existing add/remove methods)
- BuildFileInfo is updated (in the save method)

---

## Verification

1. Build passes
2. Create a BuildPackage with parts → schedule it → StageExecutions created for
   SLS, Depowder, EDM (all linked to BuildPackageId)
3. SLS StageExecution has EstimatedHours from slice file
4. Complete the EDM stage → individual part jobs + executions created for
   remaining routing stages
5. Add/remove parts from a package → revision history updated
6. Update slice file → revision created, duration recalculated

---

## Files Modified (fill in after completion)

- `Services/IBuildPlanningService.cs` — Added 4 new methods: UpdateBuildDurationFromSliceAsync, CreateBuildStageExecutionsAsync, CreatePartStageExecutionsAsync, CreateRevisionAsync
- `Services/BuildPlanningService.cs` — Implemented all 4 new methods; wired auto-triggers in AddPartToPackageAsync, RemovePartFromPackageAsync, SaveBuildFileInfoAsync, UpdatePackageAsync; injected INumberSequenceService
- `Services/StageService.cs` — Injected IBuildPlanningService; added Wire EDM completion trigger to spawn per-part jobs via CreatePartStageExecutionsAsync
