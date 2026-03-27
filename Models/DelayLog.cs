using System.ComponentModel.DataAnnotations;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

public class DelayLog
{
    public int Id { get; set; }

    public int? JobId { get; set; }
    public int? StageExecutionId { get; set; }

    [Required, MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ReasonCode { get; set; }

    public DelayCategory Category { get; set; } = DelayCategory.Other;

    public int DelayMinutes { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(500)]
    public string? Resolution { get; set; }

    [Required, MaxLength(100)]
    public string LoggedBy { get; set; } = string.Empty;

    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public virtual Job? Job { get; set; }
    public virtual StageExecution? StageExecution { get; set; }
}
