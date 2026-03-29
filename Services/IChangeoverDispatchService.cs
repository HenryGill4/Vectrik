using Vectrik.Models;

namespace Vectrik.Services;

public interface IChangeoverDispatchService
{
    /// <summary>
    /// Creates or updates a changeover dispatch for an SLS machine based on estimated build end time.
    /// Only creates for machines with AutoChangeoverEnabled = true.
    /// </summary>
    Task<SetupDispatch?> CreateOrUpdateChangeoverDispatchAsync(int machineId, DateTime buildEndTime);

    /// <summary>
    /// Re-scores all active changeover dispatches based on current shift time remaining.
    /// Called periodically to escalate priority as shift end approaches.
    /// </summary>
    Task EscalateChangeoverPrioritiesAsync();

    /// <summary>Returns all active changeover dispatches.</summary>
    Task<List<SetupDispatch>> GetActiveChangeoverDispatchesAsync();

    /// <summary>
    /// Handles post-completion state: resets machine chamber state.
    /// </summary>
    Task HandleChangeoverCompletionAsync(int dispatchId);
}
