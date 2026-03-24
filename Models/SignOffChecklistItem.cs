namespace Opcentrix_V3.Models;

/// <summary>
/// Represents a single sign-off checklist item derived from a Work Instruction step.
/// Serialized as JSON in <see cref="StageExecution.SignOffChecklistJson"/>.
/// </summary>
public record SignOffChecklistItem
{
    public int StepId { get; init; }
    public string Title { get; init; } = string.Empty;
    public bool Required { get; init; }
    public bool SignedOff { get; set; }
    public string? SignedBy { get; set; }
    public DateTime? SignedAt { get; set; }
}
