using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class BuildJob
{
    [Key]
    public int BuildId { get; set; }

    [Required, MaxLength(50)]
    public string PrinterName { get; set; } = string.Empty;

    public DateTime ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ScheduledEndTime { get; set; }

    public BuildJobStatus Status { get; set; } = BuildJobStatus.Pending;

    public int UserId { get; set; }

    [MaxLength(100)]
    public string? Material { get; set; }

    public string? Notes { get; set; }

    // Print results
    [MaxLength(50)]
    public string? LaserRunTime { get; set; }

    public float? GasUsedLiters { get; set; }
    public float? PowderUsedLiters { get; set; }

    [MaxLength(200)]
    public string? EndReason { get; set; }

    // Operator estimates
    public decimal? OperatorEstimatedHours { get; set; }
    public decimal? OperatorActualHours { get; set; }
    public int TotalPartsInBuild { get; set; }

    // Link to scheduled job
    public int? JobId { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public virtual User User { get; set; } = null!;
    public virtual Job? Job { get; set; }
    public virtual ICollection<BuildJobPart> Parts { get; set; } = new List<BuildJobPart>();
    public virtual ICollection<DelayLog> Delays { get; set; } = new List<DelayLog>();
}
