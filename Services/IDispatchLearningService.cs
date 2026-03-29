using Vectrik.Models;

namespace Vectrik.Services;

public interface IDispatchLearningService
{
    /// <summary>
    /// Processes a completed dispatch: updates operator proficiency profile,
    /// program setup EMA, and changeover-specific EMA.
    /// Should be called after CompleteDispatchAsync writes SetupHistory.
    /// </summary>
    Task ProcessCompletedDispatchAsync(int dispatchId);

    /// <summary>
    /// Recalculates proficiency levels for all operators on a machine
    /// based on median setup time comparison.
    /// </summary>
    Task RecalculateProficiencyLevelsAsync(int machineId);

    /// <summary>
    /// Returns the best available operator for a machine/program combination,
    /// considering proficiency and availability.
    /// </summary>
    Task<int?> SuggestBestOperatorAsync(int machineId, int? machineProgramId = null);

    /// <summary>
    /// Gets proficiency profiles for a machine, ordered by proficiency level descending.
    /// </summary>
    Task<List<OperatorSetupProfile>> GetMachineProfilesAsync(int machineId);

    /// <summary>
    /// Gets all proficiency profiles for an operator across machines.
    /// </summary>
    Task<List<OperatorSetupProfile>> GetOperatorProfilesAsync(int userId);
}
