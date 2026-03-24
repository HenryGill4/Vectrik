# Scheduler Refactor - Current State & Remaining Work

## What's Done

### Phase 2: Model Enhancements (COMPLETE)
- `Models/MachineProgram.cs`: Updated `TotalPartCount` to use `Quantity * StackLevel` for correct stacking math
- `Models/MachineProgram.cs`: Added `BasePartCount` computed property (Quantity sum without stacking)
- `Models/MachineProgram.cs`: Added `BuildPurpose` field (string, max 50)
- `Models/Enums/ManufacturingEnums.cs`: Added `BuildPurpose` enum (Weekday, Weekend, ChangeoverBackup, DemandFill, Custom)

### Phase 1: Legacy Deletion (COMPLETE)
- **Deleted files:**
  - `Models/BuildPackage.cs` (included BuildPackagePart)
  - `Models/BuildPackageRevision.cs`
  - `Models/BuildFileInfo.cs`
  - `Services/IBuildPlanningService.cs`
  - `Services/BuildPlanningService.cs`
  - `Services/IBuildSchedulingService.cs`
  - `Services/BuildSchedulingService.cs`
  - `Tests/Services/BuildSchedulingServiceTests.cs`
  - `Tests/Services/BuildPlanningServiceTests.cs`
  - `Tests/Helpers/StubBuildPlanningService.cs`

### Phase 1b: Service Layer Cleanup (COMPLETE via agent)
- `Program.cs`: Removed DI registrations for legacy services
- `Data/TenantDbContext.cs`: Removed DbSets and OnModelCreating for BuildPackage entities
- `Models/Enums/ManufacturingEnums.cs`: Removed `BuildPackageStatus` enum
- `Models/StageExecution.cs`: Removed `BuildPackageId` FK
- `Models/PartInstance.cs`: Removed `BuildPackageId` FK
- `Models/ProductionBatch.cs`: Removed `OriginBuildPackageId`
- `Models/ScheduleDiagnostics.cs`: Removed `BuildPackageId`
- `Models/PartSeparationResult.cs`: Replaced `BuildPackageId` with `MachineProgramId`
- `Models/BuildTemplate.cs`: Removed `SourceBuildPackageId`
- `Models/WorkOrder.cs`: Removed `BuildPackageParts` navigation from WorkOrderLine
- `Services/WorkOrderService.cs`: Removed BuildPackageParts include chains
- `Services/SchedulingDiagnosticsService.cs`: Removed IBuildSchedulingService dependency
- `Services/BatchService.cs`: Removed BuildPackage references
- `Services/StageService.cs`: Removed IBuildPlanningService dependency
- `Services/BuildSuggestionService.cs`: Updated to use ProgramParts instead of BuildPackageParts
- `Services/BuildTemplateService.cs`: Removed CreateFromBuildPackageAsync
- `Services/PricingEngineService.cs`: Removed AllocateBuildCostAsync
- `Services/SerialNumberService.cs`: Removed buildPackageId parameter
- `Services/SchedulingService.cs`: Removed BuildPackage references
- Various interface files updated accordingly

### Bug Fix: Missing Record Types
- `Services/IProgramSchedulingService.cs`: Added missing `StandardProgramScheduleResult` and `WorkOrderScheduleResult` record definitions

### Phase 1b: Razor Component Cleanup (IN PROGRESS via background agent)
- Agent was launched to clean all ~29 Razor files referencing BuildPackage
- Status: may or may not have completed — check files for remaining `BuildPackage` references

## What's NOT Done Yet

### Phase 3: UnifiedScheduleWizard (NOT STARTED - CRITICAL)
**File to create:** `Components/Pages/Scheduler/Modals/UnifiedScheduleWizard.razor`

This replaces ProgramScheduleWizard, WoJobModal, and CreateJobModal with one wizard.

**Design decisions confirmed with user:**
- Wizard auto-detects SLS vs CNC based on manufacturing approach
- Per-part stacking (each part in a build can have its own StackLevel)
- Quantity * StackLevel = effective total (e.g., 72 parts × 2 stack = 144)
- Auto-suggest builds based on machine weekend gaps + demand
- Build purposes: Weekday, Weekend, ChangeoverBackup, DemandFill
- Different builds should have different variations

**Steps:**
1. Select Parts (shared) — from WO lines or standalone
2. For SLS: Configure Build Variations — multiple builds with different stacking
3. For CNC: Simple quantity + machine config
4. Program Matching (SLS) — match to existing BuildPlate programs
5. Downstream Setup (SLS) — depowder/EDM validation
6. Schedule & Confirm

**Key services to use:**
- `IProgramSchedulingService.ScheduleBuildPlateAsync/RunAsync/AutoMachineAsync`
- `IProgramPlanningService.CreateBuildPlateAsync/AddPartToProgramAsync`
- `IDownstreamProgramService.GetRequiredProgramsAsync`
- `ISchedulingService.AutoScheduleJobAsync` (for CNC)
- `PartAdditiveBuildConfig.AvailableStackLevels/GetPartsPerBuild/GetStackDuration`

**Internal data model:**
```csharp
class BuildVariation {
    int Index;
    int SourceProgramId; // -1 = create new
    string NewProgramName;
    List<BuildPartConfig> Parts;
    string Purpose; // Weekday/Weekend/ChangeoverBackup/DemandFill
    int SelectedMachineId;
    double EstimatedHours;
    int TotalParts => Parts.Sum(p => p.Quantity * p.StackLevel);
}

class BuildPartConfig {
    int PartId;
    Part Part;
    int Quantity; // parts per plate layer
    int StackLevel; // 1, 2, or 3
    int? WorkOrderLineId;
    int EffectiveTotal => Quantity * StackLevel;
}

class CncJobConfig {
    WorkOrderLine Line;
    int Quantity;
    int SelectedMachineId;
    JobPriority Priority;
}
```

### Phase 4: Update Demand Tab (NOT STARTED)
- Replace separate SLS/CNC buttons with unified "Schedule" button
- Wire to UnifiedScheduleWizard
- Update `GetInProgramQty` to multiply by StackLevel
- Add build coverage summary per SLS line

### Phase 5: Rewire Scheduler Index (NOT STARTED)
- Remove `_buildPackages`, `ReadyBuilds`, `HandleBuildAction`
- Remove legacy service injections
- Replace `CreateJobModal` with `UnifiedScheduleWizard`
- Simplify `LoadMachineTimelinesAsync` to program-only
- Simplify `AutoScheduleAll` to use ProgramScheduling

### Phase 6: Gantt Cleanup (PARTIAL - via agent)
- May need manual verification after Razor agent completes

### Phase 7: Build & Test (NOT STARTED)
- Run `dotnet build` and fix any remaining compile errors
- Create EF migration for BuildPurpose field + removed columns
- Run `dotnet test`

## Key Files Reference

| Purpose | File |
|---------|------|
| MachineProgram model | `Models/MachineProgram.cs` |
| ProgramPart (join) | `Models/ProgramPart.cs` |
| Stacking config | `Models/PartAdditiveBuildConfig.cs` |
| Program scheduling | `Services/IProgramSchedulingService.cs` |
| Program planning | `Services/IProgramPlanningService.cs` |
| Downstream programs | `Services/IDownstreamProgramService.cs` |
| Scheduler page | `Components/Pages/Scheduler/Index.razor` |
| Demand tab | `Components/Pages/Scheduler/Views/WorkOrdersView.razor` |
| Current wizard (replace) | `Components/Pages/Scheduler/Modals/ProgramScheduleWizard.razor` |
| Wizard CSS | `wwwroot/css/scheduler-wizard.css` |
| Enums | `Models/Enums/ManufacturingEnums.cs` |
