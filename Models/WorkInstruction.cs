using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class WorkInstruction
{
    public int Id { get; set; }

    [Required]
    public int PartId { get; set; }
    public virtual Part Part { get; set; } = null!;

    [Required]
    public int ProductionStageId { get; set; }
    public virtual ProductionStage ProductionStage { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int RevisionNumber { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    [MaxLength(100)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<WorkInstructionStep> Steps { get; set; } = new List<WorkInstructionStep>();
    public virtual ICollection<WorkInstructionRevision> Revisions { get; set; } = new List<WorkInstructionRevision>();
}
