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

    // Scheduling
    public int? MachineId { get; set; }
    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    public DateTime? ActualStartAt { get; set; }
    public DateTime? ActualEndAt { get; set; }

    // Time
    public double? EstimatedHours { get; set; }
    public double? ActualHours { get; set; }
    public double? SetupHours { get; set; }
    public decimal SetupHoursActual { get; set; }
    public decimal RunHoursActual { get; set; }

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

    [MaxLength(100)]
    public string? AssignedOperatorId { get; set; }

    // Custom Form Data
    public string CustomFieldValues { get; set; } = "{}";

    // Quality
    public bool QualityCheckRequired { get; set; } = true;
    public bool? QualityCheckPassed { get; set; }

    [MaxLength(1000)]
    public string? QualityNotes { get; set; }

    // Notes & Completion
    public string? Notes { get; set; }
    public string? Issues { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }

    [MaxLength(500)]
    public string? FailureReason { get; set; }

    // Unmanned / lights-out
    public bool IsUnmanned { get; set; }

    // Sequence within job
    public int SortOrder { get; set; }

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
    public virtual Machine? Machine { get; set; }
    public virtual ICollection<DelayLog> DelayLogs { get; set; } = new List<DelayLog>();
}
