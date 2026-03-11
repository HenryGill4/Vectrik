using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public interface IStageService
{
    Task<List<ProductionStage>> GetAllStagesAsync(bool activeOnly = true);
    Task<ProductionStage?> GetStageByIdAsync(int id);
    Task<ProductionStage?> GetStageBySlugAsync(string slug);
    Task<ProductionStage> CreateStageAsync(ProductionStage stage);
    Task<ProductionStage> UpdateStageAsync(ProductionStage stage);
    Task DeleteStageAsync(int id);
    Task<List<StageExecution>> GetQueueForStageAsync(int stageId);
    Task<List<StageExecution>> GetActiveWorkForStageAsync(int stageId);
    Task<StageExecution> StartStageExecutionAsync(int executionId, int operatorUserId, string operatorName);
    Task<StageExecution> CompleteStageExecutionAsync(int executionId, string? customFieldValues = null, string? notes = null);
    Task<StageExecution> SkipStageExecutionAsync(int executionId, string reason);
    Task<StageExecution> FailStageExecutionAsync(int executionId, string reason);
    Task<List<StageExecution>> GetRecentCompletionsAsync(int stageId, int count = 20);
}
