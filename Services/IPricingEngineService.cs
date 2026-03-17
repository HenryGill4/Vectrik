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
    public decimal MaterialCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal OutsideProcessCost { get; set; }
    public decimal TotalCost => LaborCost + MaterialCost + OverheadCost + OutsideProcessCost;
    public double TotalLaborMinutes { get; set; }
    public double TotalSetupMinutes { get; set; }
}
