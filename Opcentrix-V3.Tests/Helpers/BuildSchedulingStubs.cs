using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services;

namespace Opcentrix_V3.Tests.Helpers;

/// <summary>
/// No-op stub for ISerialNumberService used in BuildSchedulingService tests.
/// </summary>
internal sealed class StubSerialNumberService : ISerialNumberService
{
    private int _counter;

    public Task<string> GenerateSerialNumberAsync() =>
        Task.FromResult($"SN-{Interlocked.Increment(ref _counter):D6}");

    public Task<List<PartInstance>> AssignSerialNumbersAsync(int workOrderLineId, int partId, int quantity, string createdBy, int? buildPackageId = null) =>
        Task.FromResult(new List<PartInstance>());

    public Task<PartInstance?> GetBySerialNumberAsync(string serialNumber) =>
        Task.FromResult<PartInstance?>(null);

    public Task<List<PartInstance>> GetByWorkOrderLineAsync(int workOrderLineId) =>
        Task.FromResult(new List<PartInstance>());

    public Task<PartInstance> UpdateStatusAsync(int partInstanceId, PartInstanceStatus status) =>
        Task.FromResult(new PartInstance());

    public Task<PartInstance> MoveToStageAsync(int partInstanceId, int stageId) =>
        Task.FromResult(new PartInstance());

    public Task<string> GenerateTemporaryTrackingIdAsync(int buildPackageId, int index) =>
        Task.FromResult($"TMP-{buildPackageId}-{index:D4}");

    public Task<PartInstance> AssignOfficialSerialAsync(int partInstanceId) =>
        Task.FromResult(new PartInstance());

    public Task<List<string>> GenerateSerialRangeAsync(int count) =>
        Task.FromResult(Enumerable.Range(1, count).Select(i => $"SN-{i:D6}").ToList());
}

/// <summary>
/// Stub for INumberSequenceService used in BuildSchedulingService tests.
/// </summary>
internal sealed class StubNumberSequenceService : INumberSequenceService
{
    private int _counter;
    public Task<string> NextAsync(string entityType) =>
        Task.FromResult($"{entityType}-{Interlocked.Increment(ref _counter):D4}");
}

/// <summary>
/// No-op stub for IManufacturingProcessService used in BuildSchedulingService tests.
/// </summary>
internal sealed class StubManufacturingProcessService : IManufacturingProcessService
{
    public Task<ManufacturingProcess?> GetByPartIdAsync(int partId) =>
        Task.FromResult<ManufacturingProcess?>(null);

    public Task<ManufacturingProcess?> GetByIdAsync(int id) =>
        Task.FromResult<ManufacturingProcess?>(null);

    public Task<ManufacturingProcess> CreateAsync(ManufacturingProcess process) =>
        Task.FromResult(process);

    public Task<ManufacturingProcess> UpdateAsync(ManufacturingProcess process) =>
        Task.FromResult(process);

    public Task DeleteAsync(int id) => Task.CompletedTask;

    public Task<ProcessStage> AddStageAsync(ProcessStage stage) =>
        Task.FromResult(stage);

    public Task<ProcessStage> UpdateStageAsync(ProcessStage stage) =>
        Task.FromResult(stage);

    public Task RemoveStageAsync(int stageId) => Task.CompletedTask;

    public Task ReorderStagesAsync(int processId, List<int> stageIdsInOrder) =>
        Task.CompletedTask;

    public Task<List<string>> ValidateProcessAsync(int processId) =>
        Task.FromResult(new List<string>());

    public DurationResult CalculateStageDuration(ProcessStage stage, int partCount, int batchCount, double? buildConfigHours) =>
        new(0, 0, 0, "stub");

    public Task<DurationResult> CalculateStageDurationWithProgramAsync(
        ProcessStage stage, int partCount, int batchCount, double? buildConfigHours, int? machineProgramId) =>
        Task.FromResult(new DurationResult(0, 0, 0, "stub"));

    public Task<ManufacturingProcess> CloneProcessAsync(int sourceProcessId, int targetPartId, string createdBy) =>
        Task.FromResult(new ManufacturingProcess());

    public Task<ManufacturingProcess> CreateProcessFromApproachAsync(int partId, int approachId, string createdBy) =>
        Task.FromResult(new ManufacturingProcess());

    public Task<List<ProcessStage>> GetStagesPendingProgramSetupAsync() =>
        Task.FromResult(new List<ProcessStage>());

    public Task LinkProgramToStageAsync(int processStageId, int machineProgramId) =>
        Task.CompletedTask;
}

/// <summary>
/// No-op stub for IBatchService used in BuildSchedulingService tests.
/// </summary>
internal sealed class StubBatchService : IBatchService
{
    public Task<List<ProductionBatch>> CreateBatchesFromBuildAsync(int buildPackageId, int batchCapacity, string createdBy) =>
        Task.FromResult(new List<ProductionBatch>());

    public Task AssignPartToBatchAsync(int partInstanceId, int batchId, string reason, string performedBy, int? atProcessStageId = null) =>
        Task.CompletedTask;

    public Task RemovePartFromBatchAsync(int partInstanceId, int batchId, string reason, string performedBy, int? atProcessStageId = null) =>
        Task.CompletedTask;

    public Task<List<ProductionBatch>> RebatchPartsAsync(List<int> partInstanceIds, int newCapacity, string reason, string performedBy) =>
        Task.FromResult(new List<ProductionBatch>());

    public Task<ConsolidationResult> TryConsolidateBatchesAsync(List<int> batchIds, int targetMachineCapacity, string performedBy) =>
        Task.FromResult(new ConsolidationResult(false, "stub", null, []));

    public Task<List<BatchPartAssignment>> GetAssignmentHistoryForPartAsync(int partInstanceId) =>
        Task.FromResult(new List<BatchPartAssignment>());

    public Task<ProductionBatch?> GetByIdAsync(int id) =>
        Task.FromResult<ProductionBatch?>(null);

    public Task<List<ProductionBatch>> GetBatchesForBuildAsync(int buildPackageId) =>
        Task.FromResult(new List<ProductionBatch>());

    public Task SealBatchAsync(int batchId) => Task.CompletedTask;

    public Task CompleteBatchAsync(int batchId) => Task.CompletedTask;

    public Task DissolveBatchAsync(int batchId, string reason, string performedBy) =>
        Task.CompletedTask;
}

/// <summary>
/// No-op stub for ISchedulingService used in BuildSchedulingService tests.
/// Per-part job scheduling is out of scope for build slot placement tests.
/// </summary>
internal sealed class StubSchedulingService : ISchedulingService
{
    public Task AutoScheduleJobAsync(int jobId, DateTime? startAfter = null) =>
        Task.CompletedTask;

    public Task<JobScheduleDiagnostic> AutoScheduleJobWithDiagnosticsAsync(int jobId, DateTime? startAfter = null) =>
        Task.FromResult(new JobScheduleDiagnostic { JobId = jobId });

    public Task<StageExecution> AutoScheduleExecutionAsync(int executionId, DateTime? startAfter = null) =>
        Task.FromResult(new StageExecution());

    public Task<ScheduleSlot> FindEarliestSlotAsync(int machineId, double durationHours, DateTime notBefore) =>
        Task.FromResult(new ScheduleSlot(notBefore, notBefore.AddHours(durationHours), machineId));

    public Task<List<Machine>> GetCapableMachinesAsync(int productionStageId, PartStageRequirement? requirement = null) =>
        Task.FromResult(new List<Machine>());

    public Task<int> AutoScheduleAllAsync() => Task.FromResult(0);

    public Task<ScheduleClearResult> ClearAllScheduleDataAsync() =>
        Task.FromResult(new ScheduleClearResult(0, 0, 0));

    public Task<DataDeleteResult> DeleteAllSchedulingDataAsync() =>
        Task.FromResult(new DataDeleteResult(0, 0, 0, 0, 0));

    public Task<DataDeleteResult> DeleteAllSchedulingAndWorkOrderDataAsync() =>
        Task.FromResult(new DataDeleteResult(0, 0, 0, 0, 0));

    public Task<DatabaseStats> GetDatabaseStatsAsync() =>
        Task.FromResult(new DatabaseStats(0, 0, 0, 0, 0, 0, 0, 0, 0));
}

/// <summary>
/// No-op stub for IStageCostService used in BuildSchedulingService tests.
/// </summary>
internal sealed class StubStageCostService : IStageCostService
{
    public Task<List<StageCostProfile>> GetAllAsync() =>
        Task.FromResult(new List<StageCostProfile>());

    public Task<StageCostProfile?> GetByStageIdAsync(int productionStageId) =>
        Task.FromResult<StageCostProfile?>(null);

    public Task<StageCostProfile> SaveAsync(StageCostProfile profile) =>
        Task.FromResult(profile);

    public Task DeleteAsync(int profileId) => Task.CompletedTask;

    public Task<StageCostEstimate> EstimateCostAsync(int productionStageId, double durationHours, int partCount, int batchCount = 1) =>
        Task.FromResult(new StageCostEstimate(0, 0, 0, 0, 0, 0, 0, 0, 0, false));
}

/// <summary>
/// No-op stub for IMachineProgramService used in scheduling tests.
/// Returns null/empty for all queries - scheduling falls back to stage defaults.
/// </summary>
internal sealed class StubMachineProgramService : IMachineProgramService
{
    public Task<MachineProgram?> GetByIdAsync(int id) => Task.FromResult<MachineProgram?>(null);
    public Task<List<MachineProgram>> GetAllAsync() => Task.FromResult(new List<MachineProgram>());
    public Task<List<MachineProgram>> GetProgramsForPartAsync(int partId) => Task.FromResult(new List<MachineProgram>());
    public Task<List<MachineProgram>> GetProgramsForMachineAsync(int machineId) => Task.FromResult(new List<MachineProgram>());
    public Task<MachineProgram?> GetProgramForStageAsync(int processStageId) => Task.FromResult<MachineProgram?>(null);
    public Task<List<MachineProgram>> GetActiveProgramsAsync(int? partId = null, int? machineId = null, int? processStageId = null) => Task.FromResult(new List<MachineProgram>());
    public Task<MachineProgram> CreateAsync(MachineProgram program, string createdBy) => Task.FromResult(program);
    public Task UpdateAsync(MachineProgram program, string modifiedBy) => Task.CompletedTask;
    public Task DeleteAsync(int id) => Task.CompletedTask;
    public Task<List<MachineProgramAssignment>> GetMachineAssignmentsAsync(int programId) => Task.FromResult(new List<MachineProgramAssignment>());
    public Task<MachineProgramAssignment> AssignMachineAsync(int programId, int machineId, string assignedBy, bool isPreferred = false, string? notes = null) => Task.FromResult(new MachineProgramAssignment());
    public Task UnassignMachineAsync(int programId, int machineId) => Task.CompletedTask;
    public Task SetPreferredMachineAsync(int programId, int machineId) => Task.CompletedTask;
    public Task SyncMachineAssignmentsAsync(int programId, List<int> machineIds, string assignedBy) => Task.CompletedTask;
    public Task<MachineProgram> CreateNewVersionAsync(int programId, string createdBy) => Task.FromResult(new MachineProgram());
    public Task<MachineProgramFile> UploadFileAsync(int programId, string fileName, Stream fileStream, long fileSize, string uploadedBy, string? description = null) => Task.FromResult(new MachineProgramFile());
    public Task<List<MachineProgramFile>> GetFilesAsync(int programId) => Task.FromResult(new List<MachineProgramFile>());
    public Task<MachineProgramFile?> GetFileByIdAsync(int fileId) => Task.FromResult<MachineProgramFile?>(null);
    public Task DeleteFileAsync(int fileId) => Task.CompletedTask;
    public Task SetPrimaryFileAsync(int programId, int fileId) => Task.CompletedTask;
    public Task<MachineProgram> CloneProgramAsync(int sourceProgramId, int? targetPartId, int? targetMachineId, string createdBy) => Task.FromResult(new MachineProgram());
    public string GetDefaultParametersForMachineType(string machineType) => "{}";
    public Task<List<ProgramToolingItem>> GetToolingItemsAsync(int programId) => Task.FromResult(new List<ProgramToolingItem>());
    public Task<ProgramToolingItem> SaveToolingItemAsync(ProgramToolingItem item, string modifiedBy) => Task.FromResult(item);
    public Task DeleteToolingItemAsync(int toolingItemId) => Task.CompletedTask;
    public Task<List<ToolingReadinessAlert>> CheckToolingReadinessAsync(int programId) => Task.FromResult(new List<ToolingReadinessAlert>());
    public Task<List<ProgramFeedback>> GetFeedbackAsync(int programId, ProgramFeedbackStatus? statusFilter = null) => Task.FromResult(new List<ProgramFeedback>());
    public Task<ProgramFeedback> SubmitFeedbackAsync(ProgramFeedback feedback) => Task.FromResult(feedback);
    public Task<ProgramFeedback> ReviewFeedbackAsync(int feedbackId, ProgramFeedbackStatus newStatus, string reviewedBy, string? resolution = null) => Task.FromResult(new ProgramFeedback());
    public Task<List<StageExecution>> GetExecutionHistoryAsync(int programId, int maxResults = 20) => Task.FromResult(new List<StageExecution>());
    public Task<Dictionary<int, int>> GetUnresolvedFeedbackCountsAsync(List<int> programIds) => Task.FromResult(new Dictionary<int, int>());
    public Task<List<MachineProgram>> GetBuildPlateProgramsAsync(ProgramStatus? statusFilter = null) => Task.FromResult(new List<MachineProgram>());
    public Task<List<ProgramPart>> GetProgramPartsAsync(int programId) => Task.FromResult(new List<ProgramPart>());
    public Task<ProgramPart> AddProgramPartAsync(int programId, int partId, int quantity, int stackLevel = 1, int? workOrderLineId = null) => Task.FromResult(new ProgramPart());
    public Task<ProgramPart> UpdateProgramPartAsync(int programPartId, int quantity, int stackLevel, string? positionNotes = null) => Task.FromResult(new ProgramPart());
    public Task RemoveProgramPartAsync(int programPartId) => Task.CompletedTask;
    public Task UpdateSlicerDataAsync(int programId, double? estimatedPrintHours, int? layerCount = null, double? buildHeightMm = null, double? estimatedPowderKg = null, string? slicerFileName = null, string? slicerSoftware = null, string? slicerVersion = null, string? partPositionsJson = null) => Task.CompletedTask;
    public Task UpdatePostProcessingLinksAsync(int buildPlateProgramId, int? depowderProgramId, int? edmProgramId, string modifiedBy) => Task.CompletedTask;
    public Task<List<MachineProgram>> GetPostProcessingCandidatesAsync(string machineType) => Task.FromResult(new List<MachineProgram>());
    public Task<ProgramDurationResult?> GetDurationFromProgramAsync(int programId, int quantity = 1) => Task.FromResult<ProgramDurationResult?>(null);
    public Task<MachineProgram?> GetBestProgramForStageAsync(int partId, int? machineId = null, int? productionStageId = null) => Task.FromResult<MachineProgram?>(null);
}

/// <summary>
/// No-op stub for IDownstreamProgramService used in scheduling tests.
/// Always returns valid (no missing programs) so scheduling can proceed.
/// </summary>
internal sealed class StubDownstreamProgramService : IDownstreamProgramService
{
    public Task<List<DownstreamProgramRequirement>> GetRequiredProgramsAsync(int buildPlateProgramId)
        => Task.FromResult(new List<DownstreamProgramRequirement>());

    public Task<DownstreamValidationResult> ValidateDownstreamReadinessAsync(int buildPlateProgramId)
        => Task.FromResult(new DownstreamValidationResult(true, [], []));

    public Task<List<MachineProgram>> CreatePlaceholderProgramsAsync(int buildPlateProgramId, List<int> stageIdsNeedingPrograms, string createdBy)
        => Task.FromResult(new List<MachineProgram>());
}
