using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

public class PartInstanceStageLog
{
    public int Id { get; set; }

    [Required]
    public int PartInstanceId { get; set; }

    [Required]
    public int ProductionStageId { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [MaxLength(100)]
    public string OperatorName { get; set; } = string.Empty;

    public string? CustomFieldValues { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public virtual PartInstance PartInstance { get; set; } = null!;
    public virtual ProductionStage ProductionStage { get; set; } = null!;
}
