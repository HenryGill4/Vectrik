using System.ComponentModel.DataAnnotations;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class CorrectiveAction
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string CapaNumber { get; set; } = string.Empty;

    public CapaType Type { get; set; }

    [MaxLength(2000)]
    public string ProblemStatement { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? RootCauseAnalysis { get; set; }

    [MaxLength(2000)]
    public string? ImmediateAction { get; set; }

    [MaxLength(2000)]
    public string? LongTermAction { get; set; }

    [MaxLength(2000)]
    public string? PreventiveAction { get; set; }

    [MaxLength(100)]
    public string OwnerId { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    [MaxLength(2000)]
    public string? EffectivenessVerification { get; set; }

    public CapaStatus Status { get; set; } = CapaStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
