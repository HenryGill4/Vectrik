using System.ComponentModel.DataAnnotations;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

/// <summary>
/// Operator feedback on a machine program, submitted during or after job execution.
/// Engineers review feedback to improve programs and track continuous improvement.
/// Ties into the learning system for data-driven program optimization.
/// </summary>
public class ProgramFeedback
{
    public int Id { get; set; }

    [Required]
    public int MachineProgramId { get; set; }

    /// <summary>
    /// Optional link to the specific execution that triggered this feedback.
    /// </summary>
    public int? StageExecutionId { get; set; }

    [Required, MaxLength(100)]
    public string OperatorUserId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string OperatorName { get; set; } = string.Empty;

    public ProgramFeedbackCategory Category { get; set; }

    public ProgramFeedbackSeverity Severity { get; set; } = ProgramFeedbackSeverity.Low;

    [Required, MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;

    public ProgramFeedbackStatus Status { get; set; } = ProgramFeedbackStatus.New;

    /// <summary>
    /// Engineer who reviewed and resolved the feedback.
    /// </summary>
    [MaxLength(100)]
    public string? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Engineer's resolution notes describing what was changed or why no action was taken.
    /// </summary>
    [MaxLength(2000)]
    public string? Resolution { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ───────────────────────────────────────────
    public virtual MachineProgram MachineProgram { get; set; } = null!;
    public virtual StageExecution? StageExecution { get; set; }
}
