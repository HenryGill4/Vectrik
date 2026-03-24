using System.ComponentModel.DataAnnotations;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class OperatorFeedback
{
    public int Id { get; set; }

    [Required]
    public int WorkInstructionStepId { get; set; }
    public virtual WorkInstructionStep Step { get; set; } = null!;

    [MaxLength(100)]
    public string OperatorUserId { get; set; } = string.Empty;

    public FeedbackType FeedbackType { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    public FeedbackStatus Status { get; set; } = FeedbackStatus.New;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}
