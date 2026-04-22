using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vectrik.Models;

/// <summary>
/// A hypothetical part inside a CostStudy. May optionally link to a real Part,
/// but all identifying and slicer fields are stored locally so the study remains
/// valid for parts that don't yet exist in production.
/// </summary>
public class CostStudyPart
{
    public int Id { get; set; }

    [Required]
    public int CostStudyId { get; set; }
    public virtual CostStudy? CostStudy { get; set; }

    /// <summary>Optional link to an existing Part catalog entry.</summary>
    public int? PartId { get; set; }
    public virtual Part? Part { get; set; }

    [Required, MaxLength(100)]
    public string PartNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    // ── Quantity / Order ─────────────────────────────────────────

    [Range(1, 100000)]
    public int OrderQuantity { get; set; } = 1;

    // ── Material (free-form, may or may not match catalog) ───────

    public int? MaterialId { get; set; }
    public virtual Material? Material { get; set; }

    [MaxLength(100)]
    public string? MaterialName { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal MaterialCostPerKg { get; set; }

    [Column(TypeName = "decimal(10,4)")]
    public decimal WeightPerPartKg { get; set; }

    /// <summary>Scrap / support / recycling loss percentage applied to powder usage (0–100).</summary>
    [Range(0, 100)]
    public double MaterialScrapPercent { get; set; } = 5.0;

    // ── SLS Slicer Data (optional, used for additive parts) ──────

    public bool IsAdditive { get; set; } = true;

    [Range(1, 200)]
    public int PartsPerPlate { get; set; } = 1;

    [Range(0, 500)]
    public double PlateBuildHours { get; set; }

    [Range(0, 20)]
    public int StackLevel { get; set; } = 1;

    [Column(TypeName = "decimal(10,2)")]
    public decimal MachineHourlyRate { get; set; } = 200.00m;

    /// <summary>Fixed per-plate amortized cost (e.g. nitrogen, filters, build plate wear).</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal ConsumablesPerPlate { get; set; }

    // ── Setup / NRE (one-time, amortized over the order) ─────────

    /// <summary>One-time engineering/programming NRE charge for the order.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal EngineeringNreCost { get; set; }

    /// <summary>One-time tooling/fixture NRE charge for the order.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal ToolingNreCost { get; set; }

    /// <summary>First-article inspection / CoC / certifications cost for the order.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal FirstArticleAndCertCost { get; set; }

    /// <summary>If true, NRE is amortized across all parts in this order. Otherwise billed separately (kept out of per-part cost).</summary>
    public bool AmortizeNreAcrossOrder { get; set; } = true;

    // ── Packaging ────────────────────────────────────────────────

    [Column(TypeName = "decimal(10,2)")]
    public decimal PackagingCostPerPart { get; set; }

    /// <summary>Fixed packaging cost per order (crate, pallet, dunnage).</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal PackagingCostPerOrder { get; set; }

    // ── Freight / Shipping (to customer) ────────────────────────

    [Column(TypeName = "decimal(10,2)")]
    public decimal FreightCostPerOrder { get; set; }

    [Range(0, 100)]
    public double FreightMarkupPercent { get; set; } = 0.0;

    // ── Pricing (Sales) ─────────────────────────────────────────

    /// <summary>Optional negotiated / manual sales price per part that overrides the computed suggested price.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? SalesPriceOverridePerPart { get; set; }

    // ── Additional Context ───────────────────────────────────────

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public int DisplayOrder { get; set; }

    public virtual ICollection<CostStudyStage> Stages { get; set; } = new List<CostStudyStage>();

    // ── Computed helpers (not mapped) ────────────────────────────

    [NotMapped]
    public decimal EffectiveMaterialCostPerPart
    {
        get
        {
            var usage = WeightPerPartKg * (1 + (decimal)(MaterialScrapPercent / 100));
            return usage * MaterialCostPerKg;
        }
    }

    [NotMapped]
    public decimal SlsBuildCostPerPart
    {
        get
        {
            if (!IsAdditive || PartsPerPlate <= 0) return 0;
            var plateTotal = ((decimal)PlateBuildHours * MachineHourlyRate) + ConsumablesPerPlate;
            return plateTotal / PartsPerPlate;
        }
    }
}
