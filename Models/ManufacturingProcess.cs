using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

/// <summary>
/// Defines the full manufacturing process for a specific part type (1:1 with Part).
/// Contains an ordered sequence of ProcessStages that describe how to manufacture
/// a part from raw build output through finished goods.
/// </summary>
public class ManufacturingProcess
{
    public int Id { get; set; }

    /// <summary>
    /// FK to Part — unique constraint enforces 1:1 relationship.
    /// </summary>
    [Required]
    public int PartId { get; set; }

    /// <summary>
    /// Optional categorization link to ManufacturingApproach (e.g., SLS, CNC-only).
    /// </summary>
    public int? ManufacturingApproachId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Which ProcessStage triggers plate release for build packages containing this part.
    /// Null if part does not come from a build plate.
    /// </summary>
    public int? PlateReleaseStageId { get; set; }

    /// <summary>
    /// Default number of parts per batch (crate size) for this part type.
    /// Individual ProcessStages can override via BatchCapacityOverride.
    /// </summary>
    [Range(1, 10000)]
    public int DefaultBatchCapacity { get; set; } = 60;

    public bool IsActive { get; set; } = true;

    public int Version { get; set; } = 1;

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual Part Part { get; set; } = null!;
    public virtual ManufacturingApproach? ManufacturingApproach { get; set; }
    public virtual ProcessStage? PlateReleaseStage { get; set; }
    public virtual ICollection<ProcessStage> Stages { get; set; } = new List<ProcessStage>();

    // Computed behavior properties (derived from process stages)

    /// <summary>true when process has any Build-level stages (part requires a build plate).</summary>
    [NotMapped]
    public bool RequiresBuildPlate => Stages?.Any(s => s.ProcessingLevel == ProcessingLevel.Build) ?? false;

    /// <summary>true when process has any Batch-level stages.</summary>
    [NotMapped]
    public bool HasBatchStages => Stages?.Any(s => s.ProcessingLevel == ProcessingLevel.Batch) ?? false;

    /// <summary>true when process has a stage that pulls duration from slicer/build config data.</summary>
    [NotMapped]
    public bool UsesSlicerData => Stages?.Any(s => s.DurationFromBuildConfig) ?? false;
}
