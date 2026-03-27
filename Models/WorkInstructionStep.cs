using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class WorkInstructionStep
{
    public int Id { get; set; }

    [Required]
    public int WorkInstructionId { get; set; }
    public virtual WorkInstruction WorkInstruction { get; set; } = null!;

    public int StepOrder { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? WarningText { get; set; }

    [MaxLength(500)]
    public string? TipText { get; set; }

    public bool RequiresOperatorSignoff { get; set; } = false;

    // Navigation
    public virtual ICollection<WorkInstructionMedia> Media { get; set; } = new List<WorkInstructionMedia>();
    public virtual ICollection<OperatorFeedback> Feedback { get; set; } = new List<OperatorFeedback>();
}
