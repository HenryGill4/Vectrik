namespace Opcentrix_V3.Services;

public interface IPricingEngineService
{
    Task<PricingBreakdown> CalculatePartCostAsync(int partId, int quantity = 1);
    Task<decimal> GetDefaultLaborRateAsync();
    Task<decimal> GetDefaultOverheadRateAsync();

    /// <summary>
    /// Allocates build-level costs (powder, gas, laser time) across parts in a build package.
    /// Returns a dictionary of PartId → allocated cost.
    /// </summary>
    Task<BuildCostAllocation> AllocateBuildCostAsync(int buildPackageId);
}

public class PricingBreakdown
{
    public decimal LaborCost { get; set; }
    public decimal SetupCost { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal StageMaterialCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal OutsideProcessCost { get; set; }
    public decimal TotalCost => LaborCost + SetupCost + MaterialCost + StageMaterialCost + OverheadCost + OutsideProcessCost;
    public double TotalLaborMinutes { get; set; }
    public double TotalSetupMinutes { get; set; }
}

public class BuildCostAllocation
{
    public int BuildPackageId { get; set; }
    public decimal PowderCost { get; set; }
    public decimal GasCost { get; set; }
    public decimal LaserTimeCost { get; set; }
    public decimal TotalBuildCost => PowderCost + GasCost + LaserTimeCost;
    public int TotalPartCount { get; set; }

    /// <summary>PartId → allocated cost share</summary>
    public Dictionary<int, decimal> PerPartCost { get; set; } = new();
}
