using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class StageExecution
{
    public int Id { get; set; }

    public int? JobId { get; set; }

    [Required]
    public int ProductionStageId { get; set; }

    public StageExecutionStatus Status { get; set; } = StageExecutionStatus.NotStarted;

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Time
    public double? EstimatedHours { get; set; }
    public double? ActualHours { get; set; }
    public double? SetupHours { get; set; }

    // Cost
    [Column(TypeName = "decimal(10,2)")]
    public decimal? EstimatedCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? ActualCost { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? MaterialCost { get; set; }

    // Operator
    public int? OperatorUserId { get; set; }

    [MaxLength(100)]
    public string? OperatorName { get; set; }

    // Custom Form Data
    public string CustomFieldValues { get; set; } = "{}";

    // Quality
    public bool QualityCheckRequired { get; set; } = true;
    public bool? QualityCheckPassed { get; set; }

    [MaxLength(1000)]
    public string? QualityNotes { get; set; }

    // Notes
    public string? Notes { get; set; }
    public string? Issues { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [MaxLength(100)]
    public string? LastModifiedBy { get; set; }

    // Navigation
    public virtual Job? Job { get; set; }
    public virtual ProductionStage ProductionStage { get; set; } = null!;
    public virtual User? Operator { get; set; }
}
