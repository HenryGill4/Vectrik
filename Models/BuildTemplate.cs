using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

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

    // Certification
    [MaxLength(100)]
    public string? CertifiedBy { get; set; }
    public DateTime? CertifiedDate { get; set; }

    // Usage tracking
    public int UseCount { get; set; }
    public DateTime? LastUsedDate { get; set; }

    /// <summary>
    /// If this template was created from a completed build.
    /// </summary>
    public int? SourceBuildPackageId { get; set; }
    public virtual BuildPackage? SourceBuildPackage { get; set; }

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

    // Navigation
    public virtual ICollection<BuildTemplatePart> Parts { get; set; } = new List<BuildTemplatePart>();

    // Computed
    [NotMapped]
    public int TotalPartCount => Parts?.Sum(p => p.Quantity) ?? 0;

    [NotMapped]
    public int UniquePartCount => Parts?.Select(p => p.PartId).Distinct().Count() ?? 0;

    [NotMapped]
    public bool IsCertified => Status == BuildTemplateStatus.Certified && !NeedsRecertification;
}
