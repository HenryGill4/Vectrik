using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vectrik.Models;

/// <summary>
/// Cached feature vector for a part, used by the smart pricing engine for
/// similar-part matching and ML-based cost prediction. Updated whenever a
/// job for this part completes or when the part's process definition changes.
/// </summary>
public class PartSignature
{
    public int Id { get; set; }

    [Required]
    public int PartId { get; set; }

    // ── Physical Attributes ─────────────────────────────────
    public double WeightKg { get; set; }

    [MaxLength(100)]
    public string MaterialCategory { get; set; } = string.Empty;

    [MaxLength(100)]
    public string MaterialName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal MaterialCostPerKg { get; set; }

    // ── Process Attributes ──────────────────────────────────
    public int StageCount { get; set; }
    public double TotalEstimatedHours { get; set; }
    public double TotalSetupMinutes { get; set; }
    public int ManufacturingApproachId { get; set; }
    public bool IsAdditive { get; set; }
    public int BomItemCount { get; set; }

    // ── Stacking (SLS only) ─────────────────────────────────
    public bool HasStacking { get; set; }
    public int MaxStackLevel { get; set; }
    public int PlannedPartsPerBuild { get; set; }

    // ── Complexity Score (1-10) ─────────────────────────────
    public double ComplexityScore { get; set; }

    // ── Actual Cost Data (from completed jobs) ──────────────
    /// <summary>Average actual cost per part from completed jobs.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal ActualCostPerPart { get; set; }

    /// <summary>Average actual total hours per part from completed jobs.</summary>
    public double ActualHoursPerPart { get; set; }

    /// <summary>Number of completed jobs used for averaging.</summary>
    public int CompletedJobCount { get; set; }

    /// <summary>Average quantity across completed jobs.</summary>
    public int AverageJobQuantity { get; set; }

    /// <summary>Average actual margin achieved.</summary>
    public double ActualMarginPct { get; set; }

    /// <summary>Ratio of actual cost to estimated cost (1.0 = perfect, >1 = over estimate).</summary>
    public double CostAccuracyRatio { get; set; }

    // ── Sell Price Data ─────────────────────────────────────
    [Column(TypeName = "decimal(10,2)")]
    public decimal LastSellPrice { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal EstimatedCostPerPart { get; set; }

    // ── Metadata ────────────────────────────────────────────
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public bool IsStale { get; set; }

    // ── Navigation ──────────────────────────────────────────
    public virtual Part Part { get; set; } = null!;
}
