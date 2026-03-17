using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IWorkflowEngine
{
    /// <summary>
    /// Starts a new workflow instance for the given entity.
    /// Returns the instance, or null if no active workflow is defined for this entity type.
    /// </summary>
    Task<WorkflowInstance?> StartAsync(string entityType, int entityId);

    /// <summary>
    /// Advances the workflow to the next step (approves the current step).
    /// </summary>
    Task<WorkflowInstance?> ApproveAsync(int instanceId, string approvedBy, string? comment = null);

    /// <summary>
    /// Rejects the current step, ending the workflow.
    /// </summary>
    Task<WorkflowInstance?> RejectAsync(int instanceId, string rejectedBy, string? comment = null);

    /// <summary>
    /// Gets the pending workflow instance for an entity, if any.
    /// </summary>
    Task<WorkflowInstance?> GetPendingAsync(string entityType, int entityId);

    /// <summary>
    /// Gets all pending approval instances assigned to a given role.
    /// </summary>
    Task<List<WorkflowInstance>> GetPendingForRoleAsync(string role);

    /// <summary>
    /// Gets all workflow definitions for an entity type.
    /// </summary>
    Task<List<WorkflowDefinition>> GetDefinitionsAsync(string entityType);
}
