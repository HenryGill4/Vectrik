using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services;

namespace Opcentrix_V3.Tests.Helpers;

/// <summary>
/// No-op stub for IProgramSchedulingService used in scheduling tests.
/// Replaces StubBuildSchedulingService for program-based scheduling.
/// </summary>
internal sealed class StubProgramSchedulingService : IProgramSchedulingService
{
    public Task<ProgramScheduleResult> ScheduleBuildPlateAsync(int machineProgramId, int machineId, DateTime? startAfter = null)
    {
        var now = DateTime.UtcNow;
        var slot = new ProgramScheduleSlot(now, now.AddHours(8), now.AddHours(8), now.AddHours(10), machineId, true);
        return Task.FromResult(new ProgramScheduleResult(slot, null, [], machineProgramId, $"Program-{machineProgramId}"));
    }

    public Task<ProgramScheduleResult> ScheduleBuildPlateAutoMachineAsync(int machineProgramId, DateTime? startAfter = null)
        => ScheduleBuildPlateAsync(machineProgramId, 1, startAfter);

    public Task<ProgramScheduleResult> ScheduleBuildPlateRunAsync(int machineProgramId, int machineId, DateTime? startAfter = null)
        => ScheduleBuildPlateAsync(machineProgramId, machineId, startAfter);

    public Task<ProgramScheduleResult> ScheduleBuildPlateRunAutoMachineAsync(int machineProgramId, DateTime? startAfter = null)
        => ScheduleBuildPlateAsync(machineProgramId, 1, startAfter);

    public Task<StandardProgramScheduleResult> ScheduleStandardProgramAsync(
        int machineProgramId,
        int quantity,
        int? machineId = null,
        int? workOrderLineId = null,
        DateTime? startAfter = null)
    {
        var now = DateTime.UtcNow;
        return Task.FromResult(new StandardProgramScheduleResult(
            1, "JOB-0001", machineProgramId, [], now, now.AddHours(4), 4.0, []));
    }

    public Task<WorkOrderScheduleResult> ScheduleFromWorkOrderLineAsync(
        int workOrderLineId,
        int? preferredMachineId = null,
        DateTime? startAfter = null)
    {
        var now = DateTime.UtcNow;
        return Task.FromResult(new WorkOrderScheduleResult(
            1, "JOB-0001", [], now, now.AddHours(4), []));
    }

    public Task<ProgramScheduleSlot> FindEarliestSlotAsync(
        int machineId,
        double durationHours,
        DateTime notBefore,
        int? forProgramId = null)
    {
        var slot = new ProgramScheduleSlot(
            notBefore,
            notBefore.AddHours(durationHours),
            notBefore.AddHours(durationHours),
            notBefore.AddHours(durationHours + 2),
            machineId,
            true);
        return Task.FromResult(slot);
    }

    public Task<BestProgramSlot> FindBestSlotAsync(
        double durationHours,
        DateTime notBefore,
        string? machineType = null,
        int? forProgramId = null)
    {
        var slot = new ProgramScheduleSlot(
            notBefore,
            notBefore.AddHours(durationHours),
            notBefore.AddHours(durationHours),
            notBefore.AddHours(durationHours + 2),
            1,
            true);
        return Task.FromResult(new BestProgramSlot(slot, 1, "Machine-1"));
    }

    public Task<List<ProgramTimelineEntry>> GetMachineTimelineAsync(int machineId, DateTime from, DateTime to)
        => Task.FromResult(new List<ProgramTimelineEntry>());

    public Task<ChangeoverAnalysis> AnalyzeChangeoverAsync(int machineId, DateTime buildEndTime)
        => Task.FromResult(new ChangeoverAnalysis(true, buildEndTime, buildEndTime.AddHours(2), null, null));

    public Task<List<ScheduleOption>> GenerateScheduleOptionsAsync(
        int machineId, double baseDurationHours, DateTime notBefore,
        PartAdditiveBuildConfig? buildConfig = null, int demandQuantity = 0)
        => Task.FromResult(new List<ScheduleOption>());

    public Task<List<BuildSequenceSuggestion>> SuggestBuildSequenceAsync(
        int machineId, List<BuildCandidate> candidates, DateTime horizonStart, DateTime horizonEnd)
        => Task.FromResult(new List<BuildSequenceSuggestion>());

    public Task<List<ProgramChangeoverConflict>> DetectChangeoverConflictsAsync(int machineId, DateTime from, DateTime to)
        => Task.FromResult(new List<ProgramChangeoverConflict>());

    public Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int machineProgramId, string createdBy)
        => Task.FromResult(new List<StageExecution>());

    public Task<ProgramPlateReleaseResult> ReleasePlateAsync(int machineProgramId, string releasedBy)
        => Task.FromResult(new ProgramPlateReleaseResult(machineProgramId, [], [], 0));

    public Task LockProgramAsync(int machineProgramId, string lockedBy)
        => Task.CompletedTask;

    public Task UnlockProgramAsync(int machineProgramId, string unlockedBy, string reason)
        => Task.CompletedTask;

    public Task<List<MachineProgram>> GetAvailableProgramsForPartAsync(int partId)
        => Task.FromResult(new List<MachineProgram>());

    public Task<List<MachineProgram>> GetAvailableBuildPlateProgramsAsync()
        => Task.FromResult(new List<MachineProgram>());
}

/// <summary>
/// No-op stub for IProgramPlanningService used in planning tests.
/// Replaces StubBuildPlanningService for program-based planning.
/// </summary>
internal sealed class StubProgramPlanningService : IProgramPlanningService
{
    public Task<List<MachineProgram>> GetAllBuildPlateProgramsAsync()
        => Task.FromResult(new List<MachineProgram>());

    public Task<List<MachineProgram>> GetBuildPlatesForPartAsync(int partId)
        => Task.FromResult(new List<MachineProgram>());

    public Task<MachineProgram?> GetBuildPlateByIdAsync(int id)
        => Task.FromResult<MachineProgram?>(null);

    public Task<MachineProgram> CreateBuildPlateAsync(MachineProgram program, string createdBy)
        => Task.FromResult(program);

    public Task<MachineProgram> UpdateBuildPlateAsync(MachineProgram program, string modifiedBy)
        => Task.FromResult(program);

    public Task LinkProgramPartsToWorkOrdersAsync(int programId, Dictionary<int, int?> partIdToWorkOrderLineId)
        => Task.CompletedTask;

    public Task DeleteBuildPlateAsync(int programId)
        => Task.CompletedTask;

    public Task DeleteBuildWithDownstreamAsync(int programId, string deletedBy)
        => Task.CompletedTask;

    public Task<MachineProgram> CreateScheduledCopyAsync(int sourceProgramId, string createdBy, int? workOrderLineId = null)
        => Task.FromResult(new MachineProgram { Name = "Run", ProgramType = ProgramType.BuildPlate });

    public Task<List<MachineProgram>> GetRunsForSourceProgramAsync(int sourceProgramId)
        => Task.FromResult(new List<MachineProgram>());

    public Task<ProgramPart> AddPartToProgramAsync(int programId, int partId, int quantity, int? workOrderLineId = null)
        => Task.FromResult(new ProgramPart());

    public Task<ProgramPart> UpdateProgramPartAsync(int programPartId, int quantity, int stackLevel, string? positionNotes = null)
        => Task.FromResult(new ProgramPart());

    public Task RemoveProgramPartAsync(int programPartId)
        => Task.CompletedTask;

    public Task UpdateSlicerDataAsync(
        int programId,
        double? estimatedPrintHours,
        int? layerCount = null,
        double? buildHeightMm = null,
        double? estimatedPowderKg = null,
        string? slicerFileName = null,
        string? slicerSoftware = null,
        string? slicerVersion = null,
        string? partPositionsJson = null)
        => Task.CompletedTask;

    public Task UpdateDurationFromSliceAsync(int programId)
        => Task.CompletedTask;

    public Task<ProgramRevision> CreateRevisionAsync(int programId, string changedBy, string? notes = null)
        => Task.FromResult(new ProgramRevision());

    public Task<List<ProgramRevision>> GetRevisionsAsync(int programId)
        => Task.FromResult(new List<ProgramRevision>());

    public Task<string> GenerateProgramNameAsync(List<int> partIds, int machineId = 0, string? template = null)
        => Task.FromResult($"PROGRAM-{DateTime.UtcNow:yyMMdd}-01");
}
