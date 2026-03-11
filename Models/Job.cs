using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class Job
{
    public int Id { get; set; }

    public int PartId { get; set; }

    [MaxLength(50)]
    public string? MachineId { get; set; }

    public int? WorkOrderLineId { get; set; }

    // Scheduling
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }

    // Production
    [MaxLength(50)]
    public string? PartNumber { get; set; }

    public int Quantity { get; set; }
    public int ProducedQuantity { get; set; }
    public int DefectQuantity { get; set; }
    public double EstimatedHours { get; set; }

    [MaxLength(100)]
    public string? SlsMaterial { get; set; }

    // Stacking
    public byte? StackLevel { get; set; }
    public int? PartsPerBuild { get; set; }
    public double? PlannedStackDurationHours { get; set; }

    // Workflow
    public JobStatus Status { get; set; } = JobStatus.Draft;
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    public string? Notes { get; set; }

    // Predecessor chain
    public int? PredecessorJobId { get; set; }
    public double? UpstreamGapHours { get; set; }

    // Operator
    public int? OperatorUserId { get; set; }
    public DateTime? LastStatusChangeUtc { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual Part Part { get; set; } = null!;
    public virtual Job? PredecessorJob { get; set; }
    public virtual User? OperatorUser { get; set; }
    public virtual WorkOrderLine? WorkOrderLine { get; set; }
    public virtual ICollection<StageExecution> Stages { get; set; } = new List<StageExecution>();
    public virtual ICollection<JobNote> JobNotes { get; set; } = new List<JobNote>();

    // NotMapped
    [NotMapped]
    public TimeSpan ScheduledDuration => ScheduledEnd - ScheduledStart;

    [NotMapped]
    public double DurationHours => ScheduledDuration.TotalHours;

    [NotMapped]
    public bool IsOverdue => Status != JobStatus.Completed && Status != JobStatus.Cancelled && DateTime.UtcNow > ScheduledEnd;
}
