# Program-Scheduler Integration Plan

## Overview
Connect the Programs system to the Scheduler to enable:
1. Scheduling parts for production using program files/durations
2. Fulfilling work orders by scheduling programs
3. Using program durations (instead of defaults) for scheduling
4. Connecting stage machines with the program system

## Current Architecture Analysis

### Existing FK Relationships
- `ProcessStage.MachineProgramId` (line 84) - Links stage definition to program
- `ProcessStage.ProgramSetupRequired` (line 90) - Flag for pending program setup
- `StageExecution.MachineProgramId` (line 112) - Records which program was used
- `StageExecution.ProcessStageId` (line 106) - Links execution to stage definition

### Duration Sources (Priority Order)
1. **Program.EstimatedPrintHours** - For BuildPlate programs (slicer data)
2. **Program.SetupTimeMinutes + RunTimeMinutes + CycleTimeMinutes** - For standard programs
3. **ProcessStage.SetupTimeMinutes + RunTimeMinutes** - Stage defaults
4. **ProcessStage.ActualAverageDurationMinutes** - Learned EMA from executions
5. **ProductionStage.DefaultDurationHours** - Global fallback

### Existing Methods
- `IBuildSchedulingService.ScheduleProgramBuildAsync()` - Already handles BuildPlate programs
- `IMachineProgramService.GetActiveProgramsAsync()` - Queries programs by part/machine/stage
- `IMachineProgramService.GetProgramForStageAsync()` - Gets linked program for a stage
- `ISchedulingService.AutoScheduleJobAsync()` - Schedules job stages sequentially

### Gap Analysis
1. **Duration Resolution**: SchedulingService.AutoScheduleJobCoreAsync uses `exec.EstimatedHours ?? ProductionStage.DefaultDurationHours` but doesn't query program durations
2. **Program Auto-Selection**: When scheduling, no automatic selection of best program for a stage
3. **Machine-Program-Stage Link**: ProcessStage.MachineProgramId exists but isn't used in duration calculation
4. **Work Order → Program Flow**: No direct path from WO line to program selection for scheduling

## Steps

1. Add GetDurationFromProgram method to IMachineProgramService
   - Calculate total duration from SetupTimeMinutes + RunTimeMinutes * quantity
   - Handle BuildPlate vs Standard program types differently
   - Return null if program has no duration data

2. Update IManufacturingProcessService.CalculateStageDuration
   - Add optional MachineProgramId parameter
   - Query program duration if MachineProgramId is provided
   - Fall back to existing stage duration logic

3. Modify SchedulingService.AutoScheduleJobCoreAsync
   - Query ProcessStage.MachineProgramId for each execution
   - Use program duration when available
   - Populate StageExecution.MachineProgramId during scheduling

4. Add GetBestProgramForStageAsync to IMachineProgramService
   - Input: partId, machineId (optional), productionStageId
   - Query active programs matching criteria
   - Prefer programs with learned EMA data (ActualAverageDurationMinutes)
   - Return best match or null

5. Update StageExecution creation in BuildSchedulingService
   - Already sets MachineProgramId for print stage (line 874)
   - Already sets MachineProgramId from ProcessStage for downstream stages (line 965)
   - Verify all paths populate this field correctly

6. Add ScheduleFromWorkOrderLine method to IBuildSchedulingService
   - Input: workOrderLineId, preferredMachineId (optional)
   - Query Part's ManufacturingProcess and active programs
   - For additive parts: find matching BuildPlate program or create suggestion
   - For CNC/standard parts: create Job with program-linked stage executions
   - Auto-schedule the created job

7. Update Scheduler UI WorkOrdersView
   - Add "Schedule Now" button per WO line (for non-additive)
   - Wire to new ScheduleFromWorkOrderLine method
   - Show program selection when multiple programs exist for a part

8. Add program duration inheritance to StageExecution creation
   - When creating execution, if ProcessStage.MachineProgramId is set
   - Query program and calculate duration
   - Populate EstimatedHours from program data

9. Update Programs page "Schedule" button flow
   - For BuildPlate: existing ScheduleProgramBuildAsync works
   - For Standard programs: create new ScheduleStandardProgramAsync
   - Standard programs create per-part Jobs with program-linked executions

10. Add program-to-stage duration sync utility
    - Method: SyncStageDurationsFromProgramAsync(processStageId)
    - Updates ProcessStage.SetupTimeMinutes/RunTimeMinutes from linked program
    - Sets ProcessStage.EstimateSource = "Program"

## Files

- Services/IMachineProgramService.cs (modify) - Add GetDurationFromProgram, GetBestProgramForStageAsync
- Services/MachineProgramService.cs (modify) - Implement new methods
- Services/IManufacturingProcessService.cs (modify) - Add MachineProgramId parameter to CalculateStageDuration
- Services/ManufacturingProcessService.cs (modify) - Implement program duration lookup
- Services/SchedulingService.cs (modify) - Use program durations in AutoScheduleJobCoreAsync
- Services/IBuildSchedulingService.cs (modify) - Add ScheduleFromWorkOrderLine, ScheduleStandardProgramAsync
- Services/BuildSchedulingService.cs (modify) - Implement new scheduling methods
- Components/Pages/Scheduler/Views/WorkOrdersView.razor (modify) - Add Schedule Now button
- Components/Pages/Programs/Index.razor (modify) - Update Schedule button for standard programs

## Implementation Notes

### Duration Calculation Priority
1. StageExecution.MachineProgramId → MachineProgram duration fields
2. ProcessStage.MachineProgramId → MachineProgram duration fields  
3. ProcessStage.ActualAverageDurationMinutes (learned EMA)
4. ProcessStage.SetupTimeMinutes + RunTimeMinutes
5. ProductionStage.DefaultDurationHours

### Program Selection Criteria
When auto-selecting a program for a stage:
1. Filter by PartId (must match)
2. Filter by MachineId if provided (via MachineAssignments)
3. Filter by Status = Active
4. Prefer programs with EstimateSource = "Auto" (learned data)
5. Prefer programs with higher ActualSampleCount
6. Fall back to first active match

### StageExecution.MachineProgramId Usage
- Set during creation (scheduling phase)
- Used for duration calculation
- Used for learning feedback (record actual vs estimated)
- Used for tooling readiness checks before job start
