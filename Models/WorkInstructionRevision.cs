using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class WorkInstructionRevision
{
    public int Id { get; set; }

    [Required]
    public int WorkInstructionId { get; set; }
    public virtual WorkInstruction WorkInstruction { get; set; } = null!;

    public int RevisionNumber { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ChangeNotes { get; set; }

    [MaxLength(100)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
