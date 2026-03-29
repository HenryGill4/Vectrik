namespace Vectrik.Models;

/// <summary>
/// Represents a single inspection checklist item for post-print dispatch verification.
/// Serialized as JSON in <see cref="SetupDispatch.InspectionChecklistJson"/>.
/// Separate from <see cref="SignOffChecklistItem"/> to support pass/fail semantics and failure notes.
/// </summary>
public record InspectionChecklistItem
{
    public int StepId { get; init; }
    public string Title { get; init; } = string.Empty;
    public bool Required { get; init; }
    public bool Inspected { get; set; }
    /// <summary>null = not inspected, true = pass, false = fail.</summary>
    public bool? Passed { get; set; }
    public string? FailureNotes { get; set; }
    public string? InspectedBy { get; set; }
    public DateTime? InspectedAt { get; set; }
}
