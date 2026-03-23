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

    public Task<ManufacturingProcess> CloneProcessAsync(int sourceProcessId, int targetPartId, string createdBy) =>
        Task.FromResult(new ManufacturingProcess());

    public Task<ManufacturingProcess> CreateProcessFromApproachAsync(int partId, int approachId, string createdBy) =>
        Task.FromResult(new ManufacturingProcess());
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
