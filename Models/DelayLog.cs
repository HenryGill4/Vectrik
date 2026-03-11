using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class DelayLog
{
    public int Id { get; set; }

    public int? BuildJobId { get; set; }
    public int? JobId { get; set; }
    public int? StageExecutionId { get; set; }

    [Required, MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ReasonCode { get; set; }

    public int DelayMinutes { get; set; }

    [Required, MaxLength(100)]
    public string LoggedBy { get; set; } = string.Empty;

    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public virtual BuildJob? BuildJob { get; set; }
    public virtual Job? Job { get; set; }
    public virtual StageExecution? StageExecution { get; set; }
}
