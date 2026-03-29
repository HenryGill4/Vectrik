using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

/// <summary>
/// A certified layout for one region of the 450×450mm SLS build plate.
/// Quadrant layouts occupy 1 slot (1/4 plate), Half layouts occupy 2 adjacent slots (1/2 plate).
/// Layouts are engineer-certified building blocks that can be composed into full plate programs.
/// </summary>
public class CertifiedLayout
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Quadrant = 1 slot (1/4 plate), Half = 2 adjacent slots (1/2 plate).</summary>
    public LayoutSize Size { get; set; } = LayoutSize.Quadrant;

    /// <summary>FK to the Part this layout prints.</summary>
    [Required]
    public int PartId { get; set; }

    /// <summary>Physical plate positions in this layout region.</summary>
    [Range(1, 500)]
    public int Positions { get; set; } = 1;

    /// <summary>1 = single, 2 = double-stack, 3 = triple-stack.</summary>
    [Range(1, 3)]
    public int StackLevel { get; set; } = 1;

    /// <summary>Cached material from Part for filtering. Null = no constraint.</summary>
    public int? MaterialId { get; set; }

    public CertifiedLayoutStatus Status { get; set; } = CertifiedLayoutStatus.Draft;

    [MaxLength(100)]
    public string? CertifiedBy { get; set; }
    public DateTime? CertifiedDate { get; set; }

    /// <summary>
    /// Hash of Part.LastModifiedDate at certification time.
    /// When the Part changes, NeedsRecertification is set true and this hash becomes stale.
    /// </summary>
    [MaxLength(200)]
    public string? PartVersionHash { get; set; }

    public bool NeedsRecertification { get; set; }

    /// <summary>Engineer notes about orientation, supports, nesting, etc.</summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Usage tracking
    public int UseCount { get; set; }
    public DateTime? LastUsedDate { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? LastModifiedBy { get; set; }

    // Navigation
    public virtual Part Part { get; set; } = null!;
    public virtual Material? Material { get; set; }
    public virtual ICollection<CertifiedLayoutRevision> Revisions { get; set; } = new List<CertifiedLayoutRevision>();

    // Computed
    [NotMapped]
    public int TotalParts => Positions * StackLevel;

    [NotMapped]
    public bool IsCertified => Status == CertifiedLayoutStatus.Certified && !NeedsRecertification;

    [NotMapped]
    public int SlotCount => Size == LayoutSize.Half ? 2 : 1;
}
