using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Opcentrix_V3.Models;

/// <summary>
/// Tracks the true operational cost for a production stage, breaking down
/// labor, equipment, overhead, and external expenses. One profile per ProductionStage.
/// </summary>
public class StageCostProfile
{
    public int Id { get; set; }

    /// <summary>
    /// FK to the global ProductionStage catalog entry.
    /// </summary>
    [Required]
    public int ProductionStageId { get; set; }

    // ── Labor ────────────────────────────────────────────────

    /// <summary>Hourly pay rate for the primary operator.</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal OperatorHourlyRate { get; set; }

    /// <summary>Number of operators required to run this stage.</summary>
    [Range(0, 20)]
    public int OperatorsRequired { get; set; } = 1;

    /// <summary>Hourly rate for supervisory labor allocated to this stage.</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal SupervisionHourlyRate { get; set; }

    /// <summary>Percentage of supervisor time allocated (0–100).</summary>
    [Range(0, 100)]
    public double SupervisionAllocationPercent { get; set; }

    /// <summary>Burden rate: benefits, taxes, insurance as a percentage of labor cost (0–100).</summary>
    [Range(0, 200)]
    public double LaborBurdenPercent { get; set; } = 30;

    // ── Equipment ────────────────────────────────────────────

    /// <summary>Machine/equipment hourly operating cost (depreciation, lease, amortization).</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal EquipmentHourlyRate { get; set; }

    /// <summary>Per-run tooling cost (wear, replacement amortization).</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal ToolingCostPerRun { get; set; }

    /// <summary>Consumable materials cost per part (gas, coolant, abrasives, etc.).</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal ConsumablesPerPart { get; set; }

    // ── Overhead ─────────────────────────────────────────────

    /// <summary>Facility cost per hour (rent, floor space allocation).</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal FacilityHourlyRate { get; set; }

    /// <summary>Utilities cost per hour (electricity, gas, compressed air).</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal UtilitiesHourlyRate { get; set; }

    /// <summary>Quality/inspection cost per part at this stage.</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal QualityInspectionCostPerPart { get; set; }

    /// <summary>General overhead percentage applied to direct costs (0–100).</summary>
    [Range(0, 200)]
    public double OverheadPercent { get; set; }

    // ── External ─────────────────────────────────────────────

    /// <summary>Vendor/subcontractor cost per part (for outsourced operations).</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal ExternalVendorCostPerPart { get; set; }

    /// <summary>Shipping/logistics cost per batch for external operations.</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal ExternalShippingCost { get; set; }

    /// <summary>Markup percentage on external costs (0–100).</summary>
    [Range(0, 200)]
    public double ExternalMarkupPercent { get; set; }

    // ── Notes ────────────────────────────────────────────────

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // ── Audit ────────────────────────────────────────────────

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // ── Navigation ───────────────────────────────────────────

    public virtual ProductionStage ProductionStage { get; set; } = null!;

    // ── Computed Properties ──────────────────────────────────

    /// <summary>Total labor cost per hour including burden.</summary>
    [NotMapped]
    public decimal LaborCostPerHour
    {
        get
        {
            var directLabor = (OperatorHourlyRate * OperatorsRequired)
                + (SupervisionHourlyRate * (decimal)(SupervisionAllocationPercent / 100));
            return directLabor * (1 + (decimal)(LaborBurdenPercent / 100));
        }
    }

    /// <summary>Total equipment cost per hour.</summary>
    [NotMapped]
    public decimal EquipmentCostPerHour => EquipmentHourlyRate;

    /// <summary>Total overhead cost per hour (facility + utilities).</summary>
    [NotMapped]
    public decimal OverheadCostPerHour => FacilityHourlyRate + UtilitiesHourlyRate;

    /// <summary>Fully-loaded hourly rate before per-part costs.</summary>
    [NotMapped]
    public decimal FullyLoadedHourlyRate
    {
        get
        {
            var baseRate = LaborCostPerHour + EquipmentCostPerHour + OverheadCostPerHour;
            return baseRate * (1 + (decimal)(OverheadPercent / 100));
        }
    }

    /// <summary>Per-part cost from consumables, quality, and tooling (amortized).</summary>
    [NotMapped]
    public decimal PerPartCost => ConsumablesPerPart + QualityInspectionCostPerPart;

    /// <summary>
    /// Calculates the total cost for a given duration and part count.
    /// </summary>
    public decimal CalculateTotalCost(double durationHours, int partCount, int batchCount = 1)
    {
        var timeCost = FullyLoadedHourlyRate * (decimal)durationHours;
        var partCosts = PerPartCost * partCount;
        var toolingCosts = ToolingCostPerRun * batchCount;

        var externalCost = 0m;
        if (ExternalVendorCostPerPart > 0 || ExternalShippingCost > 0)
        {
            externalCost = (ExternalVendorCostPerPart * partCount)
                + (ExternalShippingCost * batchCount);
            externalCost *= (1 + (decimal)(ExternalMarkupPercent / 100));
        }

        return timeCost + partCosts + toolingCosts + externalCost;
    }
}
