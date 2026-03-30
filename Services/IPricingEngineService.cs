namespace Vectrik.Services;

public interface IPricingEngineService
{
    Task<PricingBreakdown> CalculatePartCostAsync(int partId, int quantity = 1);
    Task<decimal> GetDefaultLaborRateAsync();
    Task<decimal> GetDefaultOverheadRateAsync();

    /// <summary>
    /// Returns a full quote line estimate: cost breakdown, stacking options (for SLS parts),
    /// volume breaks, and suggested pricing from PartPricing or target margin.
    /// </summary>
    Task<QuoteLineEstimate> GetQuoteLineEstimateAsync(int partId, int quantity, decimal targetMarginPct = 25);
}

public class PricingBreakdown
{
    public decimal LaborCost { get; set; }
    public decimal SetupCost { get; set; }

    /// <summary>
    /// Legacy: raw material cost from Part.MaterialEntity × weight.
    /// Prefer BomMaterialCost when BOM is populated.
    /// </summary>
    public decimal MaterialCost { get; set; }

    /// <summary>
    /// Total BOM material cost (raw materials + inventory items + sub-part roll-up).
    /// When the part has BOM items, this replaces MaterialCost.
    /// </summary>
    public decimal BomMaterialCost { get; set; }

    public decimal StageMaterialCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal OutsideProcessCost { get; set; }

    /// <summary>Effective material cost: uses BOM cost when available, otherwise legacy material cost.</summary>
    public decimal EffectiveMaterialCost => BomMaterialCost > 0 ? BomMaterialCost : MaterialCost;

    public decimal TotalCost => LaborCost + SetupCost + EffectiveMaterialCost + StageMaterialCost + OverheadCost + OutsideProcessCost;
    public double TotalLaborMinutes { get; set; }
    public double TotalSetupMinutes { get; set; }
}

/// <summary>
/// Full estimate for a quote line: cost breakdown, stacking options, volume breaks, and suggested pricing.
/// </summary>
public class QuoteLineEstimate
{
    public PricingBreakdown CostBreakdown { get; set; } = new();

    /// <summary>Sell price from PartPricing, if configured.</summary>
    public decimal? PartPricingSellPrice { get; set; }

    /// <summary>Pricing tier from PartPricing (e.g. Standard, Volume, Defense).</summary>
    public string? PricingTier { get; set; }

    /// <summary>Suggested price per part using target margin: Cost / (1 - margin%).</summary>
    public decimal TargetMarginPrice { get; set; }

    /// <summary>Stacking options for SLS/additive parts. Empty for non-additive.</summary>
    public List<StackingOption> StackingOptions { get; set; } = new();

    /// <summary>Volume break pricing at standard quantity breakpoints.</summary>
    public List<QuantityBreak> QuantityBreaks { get; set; } = new();

    /// <summary>The recommended stack level for this quantity, or null if non-additive.</summary>
    public int? RecommendedStackLevel { get; set; }

    /// <summary>True if this part is additive (SLS/DMLS) and has stacking config.</summary>
    public bool IsAdditivePart { get; set; }
}

/// <summary>Cost comparison for a specific stack level.</summary>
public record StackingOption(
    int StackLevel,
    string Label,
    int PartsPerBuild,
    double BuildDurationHours,
    int BuildsRequired,
    decimal MachineCostPerPart,
    decimal TotalCostPerPart,
    decimal TotalCost,
    bool IsRecommended);

/// <summary>Cost at a specific quantity breakpoint showing setup amortization effect.</summary>
public record QuantityBreak(
    int Quantity,
    decimal CostPerPart,
    decimal SetupCostPerPart,
    decimal TotalCost,
    decimal SuggestedPrice);

