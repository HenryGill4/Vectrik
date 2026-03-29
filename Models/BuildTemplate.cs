using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

public class BuildTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public BuildTemplateStatus Status { get; set; } = BuildTemplateStatus.Draft;

    /// <summary>
    /// Material constraint — all parts on this template must share the same material.
    /// </summary>
    public int? MaterialId { get; set; }
    public virtual Material? Material { get; set; }

    public int StackLevel { get; set; } = 1;

    [Range(0.1, 500)]
    public double EstimatedDurationHours { get; set; }

    /// <summary>
    /// JSON — laser power, layer thickness, scan speed, etc.
    /// </summary>
    public string? BuildParameters { get; set; }

    // Slicer output metadata (the build file IS the slicer output)

    /// <summary>Slicer output file name (e.g. "MyBuild_v3.sli").</summary>
    [MaxLength(200)]
    public string? FileName { get; set; }

    public int? LayerCount { get; set; }

    public double? BuildHeightMm { get; set; }

    public double? EstimatedPowderKg { get; set; }

    /// <summary>JSON: per-part positions on the build plate.</summary>
    public string? PartPositionsJson { get; set; }

    [MaxLength(100)]
    public string? SlicerSoftware { get; set; }

    [MaxLength(50)]
    public string? SlicerVersion { get; set; }

    // Certification
    [MaxLength(100)]
    public string? CertifiedBy { get; set; }
    public DateTime? CertifiedDate { get; set; }

    // Usage tracking
    public int UseCount { get; set; }
    public DateTime? LastUsedDate { get; set; }

    /// <summary>
    /// Hash of part versions at certification time. When any part changes,
    /// <see cref="NeedsRecertification"/> is set and this hash becomes stale.
    /// </summary>
    [MaxLength(200)]
    public string? PartVersionHash { get; set; }
    public bool NeedsRecertification { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    /// <summary>
    /// JSON describing certified layout plate composition:
    /// [{"layoutId": 5, "slots": [0,2]}, {"layoutId": 8, "slots": [1]}, {"layoutId": 3, "slots": [3]}]
    /// Null for legacy templates not using certified layouts.
    /// </summary>
    public string? PlateCompositionJson { get; set; }

    // Navigation
    public virtual ICollection<BuildTemplatePart> Parts { get; set; } = new List<BuildTemplatePart>();
    public virtual ICollection<BuildTemplateRevision> Revisions { get; set; } = new List<BuildTemplateRevision>();

    // Computed
    [NotMapped]
    public int TotalPartCount => Parts?.Sum(p => p.Quantity) ?? 0;

    [NotMapped]
    public int UniquePartCount => Parts?.Select(p => p.PartId).Distinct().Count() ?? 0;

    [NotMapped]
    public bool IsCertified => Status == BuildTemplateStatus.Certified && !NeedsRecertification;
}
