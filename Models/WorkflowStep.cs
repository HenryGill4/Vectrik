using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class WorkflowStep
{
    public int Id { get; set; }

    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;

    public int StepOrder { get; set; }

    /// <summary>
    /// RequireApproval, SendNotification, SetField, CreateTask.
    /// </summary>
    [Required, MaxLength(50)]
    public string ActionType { get; set; } = "RequireApproval";

    /// <summary>
    /// Role that this step is assigned to (e.g. "Manager", "Quality").
    /// </summary>
    [MaxLength(50)]
    public string? AssignToRole { get; set; }

    /// <summary>
    /// Specific user this step is assigned to (overrides role).
    /// </summary>
    public int? AssignToUserId { get; set; }

    /// <summary>
    /// JSON configuration specific to the action type.
    /// </summary>
    public string? ConfigJson { get; set; }

    /// <summary>
    /// Hours before this step escalates if not completed.
    /// </summary>
    public double? TimeoutHours { get; set; }
}
