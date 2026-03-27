using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

public class Part
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string PartNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required, MaxLength(100)]
    public string Material { get; set; } = "Ti-6Al-4V Grade 5";

    // Material FK (replaces fragile string-match in PricingEngine)
    public int? MaterialId { get; set; }
    public virtual Material? MaterialEntity { get; set; }

    // Manufacturing Approach FK (replaces hardcoded string)
    public int? ManufacturingApproachId { get; set; }
    public virtual ManufacturingApproach? ManufacturingApproach { get; set; }

    // PDM Fields
    [MaxLength(50)]
    public string? CustomerPartNumber { get; set; }

    [MaxLength(50)]
    public string? DrawingNumber { get; set; }

    [MaxLength(20)]
    public string? Revision { get; set; }

    public DateTime? RevisionDate { get; set; }

    [Range(0, 10000)]
    public double? EstimatedWeightKg { get; set; }

    [MaxLength(200)]
    public string? RawMaterialSpec { get; set; }

    // DLMS / Customization
    public string? CustomFieldValues { get; set; }

    public ItarClassification ItarClassification { get; set; } = ItarClassification.None;

    public bool IsDefensePart { get; set; }

    // Stage Config (deprecated — use StageRequirements navigation property)
    [Obsolete("Use StageRequirements navigation property instead")]
    [MaxLength(1000)]
    public string RequiredStages { get; set; } = "[]";

    // Status + Audit
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual ICollection<PartStageRequirement> StageRequirements { get; set; } = new List<PartStageRequirement>();
    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
    public virtual ICollection<PartDrawing> Drawings { get; set; } = new List<PartDrawing>();
    public virtual ICollection<PartRevisionHistory> RevisionHistory { get; set; } = new List<PartRevisionHistory>();
    public virtual ICollection<PartNote> Notes { get; set; } = new List<PartNote>();
    public virtual ICollection<InspectionPlan> InspectionPlans { get; set; } = new List<InspectionPlan>();
    public virtual ICollection<PartBomItem> BomItems { get; set; } = new List<PartBomItem>();

    // Additive build configuration (1:0..1 — only for additive parts)
    public virtual PartAdditiveBuildConfig? AdditiveBuildConfig { get; set; }

    // Manufacturing process definition (1:0..1 — configurable process per part type)
    public virtual ManufacturingProcess? ManufacturingProcess { get; set; }

    // Pricing configuration (1:0..1)
    public virtual PartPricing? Pricing { get; set; }
}
