using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;

namespace Opcentrix_V3.Services;

public class PricingEngineService : IPricingEngineService
{
    private readonly TenantDbContext _db;

    public PricingEngineService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<PricingBreakdown> CalculatePartCostAsync(int partId, int quantity = 1)
    {
        var breakdown = new PricingBreakdown();

        var requirements = await _db.PartStageRequirements
            .Include(r => r.ProductionStage)
            .Where(r => r.PartId == partId && r.IsActive)
            .OrderBy(r => r.ExecutionOrder)
            .ToListAsync();

        var laborRate = await GetDefaultLaborRateAsync();
        var overheadRate = await GetDefaultOverheadRateAsync();

        foreach (var req in requirements)
        {
            var estimatedMinutes = (req.ProductionStage?.DefaultDurationHours ?? 0) * 60;
            var setupMinutes = req.ProductionStage?.DefaultSetupMinutes ?? 0;

            breakdown.TotalLaborMinutes += estimatedMinutes;
            breakdown.TotalSetupMinutes += setupMinutes;
        }

        // Labor cost = (labor minutes + setup minutes) * rate / 60
        breakdown.LaborCost = (decimal)(breakdown.TotalLaborMinutes + breakdown.TotalSetupMinutes) / 60m * laborRate;

        // Material cost from part's material
        var part = await _db.Parts.FindAsync(partId);
        if (part != null)
        {
            var material = await _db.Materials
                .FirstOrDefaultAsync(m => m.Name == part.Material);
            if (material != null)
            {
                breakdown.MaterialCost = material.CostPerKg * (decimal)(part.EstimatedWeightKg ?? 0) * quantity;
            }
        }

        // Overhead as percentage of labor
        breakdown.OverheadCost = breakdown.LaborCost * (overheadRate / 100m);

        return breakdown;
    }

    public async Task<decimal> GetDefaultLaborRateAsync()
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "costing.default_labor_rate");
        return decimal.TryParse(setting?.Value, out var rate) ? rate : 65m;
    }

    public async Task<decimal> GetDefaultOverheadRateAsync()
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "costing.default_overhead_pct");
        return decimal.TryParse(setting?.Value, out var rate) ? rate : 150m;
    }
}
