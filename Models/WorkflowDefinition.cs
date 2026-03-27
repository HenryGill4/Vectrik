using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class WorkflowDefinition
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Entity type this workflow applies to: WorkOrder, Quote, NCR, PO, Document.
    /// </summary>
    [Required, MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Event that triggers this workflow: StatusChange, Create, FieldUpdate.
    /// </summary>
    [MaxLength(50)]
    public string TriggerEvent { get; set; } = "StatusChange";

    /// <summary>
    /// JSON conditions for when this workflow should fire (field comparisons).
    /// </summary>
    public string? ConditionsJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public List<WorkflowStep> Steps { get; set; } = new();
}
