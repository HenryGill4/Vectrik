using Microsoft.EntityFrameworkCore;
using Vectrik.Data;

namespace Vectrik.Services;

public class PricingEngineService : IPricingEngineService
{
    private readonly TenantDbContext _db;
    private readonly IPartService _partService;

    public PricingEngineService(TenantDbContext db, IPartService partService)
    {
        _db = db;
        _partService = partService;
    }

    public async Task<PricingBreakdown> CalculatePartCostAsync(int partId, int quantity = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

        var breakdown = new PricingBreakdown();

        var requirements = await _db.PartStageRequirements
            .Include(r => r.ProductionStage)
            .Where(r => r.PartId == partId && r.IsActive)
            .OrderBy(r => r.ExecutionOrder)
            .ToListAsync();

        var laborRate = await GetDefaultLaborRateAsync();

        foreach (var req in requirements)
        {
            var hours = req.GetEffectiveEstimatedHours();
            var rate = req.GetEffectiveHourlyRate();
            var setupMinutes = req.SetupTimeMinutes ?? req.ProductionStage?.DefaultSetupMinutes ?? 0;

            breakdown.TotalLaborMinutes += hours * 60;
            breakdown.TotalSetupMinutes += setupMinutes;
            breakdown.StageMaterialCost += req.MaterialCost;
        }

        // Labor cost: each stage uses its effective rate
        breakdown.LaborCost = requirements.Sum(r =>
            (decimal)r.GetEffectiveEstimatedHours() * r.GetEffectiveHourlyRate());

        // Setup cost (uses default labor rate)
        var setupHours = breakdown.TotalSetupMinutes / 60.0;
        breakdown.SetupCost = (decimal)setupHours * laborRate;

        // BOM material cost (preferred) — full roll-up from BOM tree
        var bomCost = await _partService.CalculateBomCostAsync(partId);
        breakdown.BomMaterialCost = bomCost.TotalBomCost * quantity;

        // Legacy raw material cost (from Part → Material FK, used when BOM is empty)
        var part = await _db.Parts
            .Include(p => p.MaterialEntity)
            .FirstOrDefaultAsync(p => p.Id == partId);

        if (part?.MaterialEntity != null)
        {
            breakdown.MaterialCost = part.MaterialEntity.CostPerKg
                * (decimal)(part.EstimatedWeightKg ?? 0) * quantity;
        }

        // Stage material costs (tooling, consumables, etc.) scaled by quantity
        breakdown.StageMaterialCost *= quantity;

        // Overhead as percentage of labor
        var overheadRate = await GetDefaultOverheadRateAsync();
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

    private async Task<decimal> GetSettingDecimalAsync(string key, decimal defaultValue)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        return decimal.TryParse(setting?.Value, out var val) ? val : defaultValue;
    }
}
