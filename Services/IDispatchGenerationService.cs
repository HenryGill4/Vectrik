using Vectrik.Models;

namespace Vectrik.Services;

public interface IDispatchGenerationService
{
    /// <summary>
    /// Auto-generates dispatch suggestions for machines with pending stage executions.
    /// Applies scoring, batch grouping, and changeover optimization.
    /// </summary>
    Task<List<SetupDispatch>> GenerateDispatchSuggestionsAsync(int? machineId = null);

    /// <summary>Approves an auto-generated dispatch, moving it from Deferred to Queued.</summary>
    Task<SetupDispatch> ApproveAutoDispatchAsync(int dispatchId, int approvedByUserId);

    /// <summary>Rejects an auto-generated dispatch by cancelling it.</summary>
    Task<SetupDispatch> RejectAutoDispatchAsync(int dispatchId, int rejectedByUserId, string? reason = null);
}
