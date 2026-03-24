namespace Opcentrix_V3.Services;

public interface IPricingEngineService
{
    Task<PricingBreakdown> CalculatePartCostAsync(int partId, int quantity = 1);
    Task<decimal> GetDefaultLaborRateAsync();
    Task<decimal> GetDefaultOverheadRateAsync();

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

