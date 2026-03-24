using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public interface IStageService
{
    // Stage CRUD
    Task<List<ProductionStage>> GetAllStagesAsync(bool activeOnly = true);
    Task<ProductionStage?> GetStageByIdAsync(int id);
    Task<ProductionStage?> GetStageBySlugAsync(string slug);
    Task<ProductionStage> CreateStageAsync(ProductionStage stage);
    Task<ProductionStage> UpdateStageAsync(ProductionStage stage);
    Task DeleteStageAsync(int id);

    // Queues
    Task<List<StageExecution>> GetQueueForStageAsync(int stageId);
    Task<List<StageExecution>> GetActiveWorkForStageAsync(int stageId);
    Task<List<StageExecution>> GetRecentCompletionsAsync(int stageId, int count = 20);

    // Operator workflows
    Task<StageExecution> StartStageExecutionAsync(int executionId, int operatorUserId, string operatorName);
    Task<StageExecution> CompleteStageExecutionAsync(int executionId, string? customFieldValues = null, string? notes = null);
    Task<StageExecution> SkipStageExecutionAsync(int executionId, string reason);
    Task<StageExecution> FailStageExecutionAsync(int executionId, string reason);
    Task<StageExecution> PauseStageExecutionAsync(int executionId, string reason, DelayCategory category, string loggedBy);
    Task<StageExecution> ResumeStageExecutionAsync(int executionId);
    Task LogUnmannedStartAsync(int executionId, int machineId);

    // Operator queue
    Task<List<StageExecution>> GetOperatorQueueAsync(int operatorUserId);
    Task<StageExecution?> GetCurrentExecutionForOperatorAsync(int operatorUserId);
    Task<List<StageExecution>> GetAvailableWorkAsync();

    // Machine queue
    Task<List<StageExecution>> GetMachineQueueAsync(int machineId);
    Task AssignOperatorAsync(int executionId, int operatorUserId);
    Task AssignMachineAsync(int executionId, int machineId);

    // Scheduling
    Task<List<StageExecution>> GetScheduledExecutionsAsync(DateTime from, DateTime to);
    Task<List<StageExecution>> GetUnscheduledExecutionsAsync();
    Task UpdateScheduleAsync(int executionId, DateTime start, DateTime end, int? machineId = null);

    // Delay logging
    Task<DelayLog> LogDelayAsync(int executionId, string reason, DelayCategory category, int delayMinutes, string loggedBy, string? notes = null);

    // Sign-off
    /// <summary>
    /// Builds or retrieves the sign-off checklist for a stage execution from linked Work Instructions.
    /// </summary>
    Task<List<SignOffChecklistItem>> GetSignOffChecklistAsync(int executionId);

    /// <summary>
    /// Signs off an individual checklist item on a stage execution.
    /// </summary>
    Task SignOffChecklistItemAsync(int executionId, int stepId, string signedBy);

    /// <summary>
    /// Signs off the entire stage execution (marks all checklist items as signed off).
    /// </summary>
    Task SignOffStageAsync(int executionId, string signedBy);

    // Capacity
    Task<List<MachineCapacityInfo>> GetMachineCapacityAsync(DateTime from, DateTime to);
}

public record MachineCapacityInfo(
    int MachineId,
    string MachineName,
    string MachineType,
    double AvailableHours,
    double LoadedHours,
    double UtilizationPct
);
