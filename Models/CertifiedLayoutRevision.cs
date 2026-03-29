using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

/// <summary>
/// Tracks version history for a CertifiedLayout.
/// A revision snapshot is created each time a certified layout is modified and recertified,
/// capturing the state *before* the recertification for full audit trail.
/// </summary>
public class CertifiedLayoutRevision
{
    public int Id { get; set; }

    public int CertifiedLayoutId { get; set; }
    public virtual CertifiedLayout CertifiedLayout { get; set; } = null!;

    public int RevisionNumber { get; set; }

    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string ChangedBy { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ChangeNotes { get; set; }

    /// <summary>Part ID at time of snapshot.</summary>
    public int PreviousPartId { get; set; }

    /// <summary>Positions at time of snapshot.</summary>
    public int PreviousPositions { get; set; }

    /// <summary>Stack level at time of snapshot.</summary>
    public int PreviousStackLevel { get; set; }

    /// <summary>Notes at time of snapshot.</summary>
    public string? PreviousNotes { get; set; }

    /// <summary>Full JSON snapshot for audit trail.</summary>
    public string SnapshotJson { get; set; } = "{}";
}
