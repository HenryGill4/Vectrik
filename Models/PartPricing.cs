using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vectrik.Models;

/// <summary>
/// Tracks the sell price, material cost, and target margin for a part.
/// One pricing record per Part (1:1). Used to calculate profitability
/// against the ManufacturingProcess cost estimate.
/// </summary>
public class PartPricing
{
    public int Id { get; set; }

    /// <summary>FK to Part — unique (1:1).</summary>
    [Required]
    public int PartId { get; set; }

    // ── Sell Price ────────────────────────────────────────────

    /// <summary>Standard sell price per unit (what the customer pays).</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal SellPricePerUnit { get; set; }

    /// <summary>Currency code (e.g. "USD", "GBP").</summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    /// <summary>Minimum order quantity for this price to apply.</summary>
    [Range(1, 100000)]
    public int MinimumOrderQty { get; set; } = 1;

    // ── Material Cost ────────────────────────────────────────

    /// <summary>Raw material cost per unit.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal MaterialCostPerUnit { get; set; }

    /// <summary>Weight of raw material consumed per unit (kg).</summary>
    [Column(TypeName = "decimal(8,4)")]
    public decimal MaterialWeightPerUnitKg { get; set; }

    // ── Target Margin ────────────────────────────────────────

    /// <summary>Target gross margin percentage (0–100).</summary>
    [Range(0, 100)]
    public decimal TargetMarginPct { get; set; } = 25;

    // ── Pricing Tiers ────────────────────────────────────────

    /// <summary>Optional pricing tier label (e.g. "Standard", "Volume", "Defense").</summary>
    [MaxLength(50)]
    public string? PricingTier { get; set; }

    /// <summary>Date this pricing became effective.</summary>
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this pricing expires (null = no expiry).</summary>
    public DateTime? ExpirationDate { get; set; }

    // ── Notes ────────────────────────────────────────────────

    [MaxLength(1000)]
    public string? PricingNotes { get; set; }

    // ── Audit ────────────────────────────────────────────────

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // ── Navigation ───────────────────────────────────────────

    public virtual Part Part { get; set; } = null!;
}
