using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vectrik.Models;

/// <summary>
/// A single manufacturing stage line in a cost study — either seeded from a
/// ProductionStage/StageCostProfile or typed in fresh. All estimated times and
/// rates are editable on the study itself, independent of production records.
/// </summary>
public class CostStudyStage
{
    public int Id { get; set; }

    [Required]
    public int CostStudyPartId { get; set; }
    public virtual CostStudyPart? CostStudyPart { get; set; }

    /// <summary>Optional reference to the catalog ProductionStage this row was seeded from.</summary>
    public int? ProductionStageId { get; set; }
    public virtual ProductionStage? ProductionStage { get; set; }

    public int DisplayOrder { get; set; }

    [Required, MaxLength(100)]
    public string StageName { get; set; } = string.Empty;

    /// <summary>Optional category label (e.g. Additive, Post-Process, Finishing, External, QC).</summary>
    [MaxLength(50)]
    public string? Category { get; set; }

    // ── Time estimates ───────────────────────────────────────────

    [Range(0, 10000)]
    public double SetupMinutes { get; set; }

    /// <summary>Cycle/run time per part (minutes). Use either this or BatchMinutes.</summary>
    [Range(0, 10000)]
    public double MinutesPerPart { get; set; }

    /// <summary>Optional fixed run time per batch (e.g. heat treat, oven, coating) used when per-part timing doesn't apply.</summary>
    [Range(0, 10000)]
    public double BatchMinutes { get; set; }

    [Range(1, 10000)]
    public int BatchSize { get; set; } = 1;

    // ── Rates ────────────────────────────────────────────────────

    [Column(TypeName = "decimal(10,2)")]
    public decimal HourlyRate { get; set; } = 85.00m;

    [Range(0, 20)]
    public int OperatorCount { get; set; } = 1;

    /// <summary>Overhead applied to the labor+machine time (0–100%).</summary>
    [Range(0, 200)]
    public double OverheadPercent { get; set; } = 0.0;

    // ── Per-part & per-run costs ─────────────────────────────────

    [Column(TypeName = "decimal(10,2)")]
    public decimal MaterialCostPerPart { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal ConsumablesPerPart { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal ToolingCostPerRun { get; set; }

    // ── External / Outsourced ───────────────────────────────────

    public bool IsExternal { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal ExternalVendorCostPerPart { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal ExternalShippingCost { get; set; }

    [Range(0, 200)]
    public double ExternalMarkupPercent { get; set; }

    // ── Yield ────────────────────────────────────────────────────

    /// <summary>Expected pass-through yield percent (0–100). 100 = no loss; 95 = 5% scrap.</summary>
    [Range(0, 100)]
    public double YieldPercent { get; set; } = 100.0;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // ── Computed helpers (not mapped) ────────────────────────────

    public double TotalMinutesForOrder(int orderQuantity)
    {
        var effectiveQty = orderQuantity;
        if (YieldPercent > 0 && YieldPercent < 100)
            effectiveQty = (int)Math.Ceiling(orderQuantity * (100.0 / YieldPercent));

        var runMinutes = (MinutesPerPart * effectiveQty);
        if (BatchMinutes > 0 && BatchSize > 0)
        {
            var batches = (int)Math.Ceiling((double)effectiveQty / BatchSize);
            runMinutes += BatchMinutes * batches;
        }
        return SetupMinutes + runMinutes;
    }
}
