using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

/// <summary>
/// DB-backed manufacturing approach that replaces the hardcoded approaches array.
/// Drives UI visibility flags (additive tabs, build plate eligibility, post-print batching).
/// </summary>
public class ManufacturingApproach
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(10)]
    public string? IconEmoji { get; set; }

    /// <summary>true → show Stacking + BatchDurations tabs in Parts/Edit</summary>
    public bool IsAdditive { get; set; }

    /// <summary>true → part can be added to a BuildPackage</summary>
    public bool RequiresBuildPlate { get; set; }

    /// <summary>true → show Depowdering/HeatTreat fields</summary>
    public bool HasPostPrintBatching { get; set; }

    /// <summary>
    /// JSON array of stage slugs to auto-suggest when this approach is selected.
    /// e.g. ["sls-print","depowdering","heat-treatment","inspection"]
    /// </summary>
    public string DefaultRoutingTemplate { get; set; } = "[]";

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
