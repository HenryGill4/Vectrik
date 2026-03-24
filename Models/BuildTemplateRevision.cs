using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

/// <summary>
/// Tracks version history for a Build File (BuildTemplate).
/// A revision snapshot is created each time a certified template is modified and recertified.
/// </summary>
public class BuildTemplateRevision
{
    public int Id { get; set; }

    public int BuildTemplateId { get; set; }
    public virtual BuildTemplate BuildTemplate { get; set; } = null!;

    public int RevisionNumber { get; set; }

    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string ChangedBy { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ChangeNotes { get; set; }

    /// <summary>JSON snapshot of parts (id, quantity, stack level, position) at this revision.</summary>
    public string PartsSnapshotJson { get; set; } = "[]";

    /// <summary>JSON snapshot of build parameters at this revision.</summary>
    public string? ParametersSnapshotJson { get; set; }

    /// <summary>JSON snapshot of slicer metadata (file name, layers, height, powder) at this revision.</summary>
    public string? SlicerMetadataSnapshotJson { get; set; }
}
