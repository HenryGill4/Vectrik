namespace Opcentrix_V3.Services;

public interface ILearningService
{
    /// <summary>
    /// Updates the EMA estimate on PartStageRequirement (legacy path).
    /// </summary>
    Task UpdateEstimateAsync(int partId, int productionStageId, double actualDurationHours);

    /// <summary>
    /// Updates the EMA estimate on ProcessStage using actual completion data.
    /// Called when a StageExecution with a ProcessStageId completes.
    /// </summary>
    Task UpdateProcessStageEstimateAsync(int processStageId, double actualDurationMinutes);
}
