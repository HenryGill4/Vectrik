using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class WorkflowInstance
{
    public int Id { get; set; }

    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;

    /// <summary>
    /// Entity type (matches WorkflowDefinition.EntityType).
    /// </summary>
    [Required, MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the entity this workflow instance is running against.
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    /// Current step order in the workflow (0 = not started yet).
    /// </summary>
    public int CurrentStepOrder { get; set; }

    /// <summary>
    /// Pending, Approved, Rejected, Cancelled.
    /// </summary>
    [Required, MaxLength(50)]
    public string Status { get; set; } = "Pending";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// User who performed the last action on this instance.
    /// </summary>
    [MaxLength(100)]
    public string? LastActionBy { get; set; }

    [MaxLength(500)]
    public string? LastActionComment { get; set; }
}
