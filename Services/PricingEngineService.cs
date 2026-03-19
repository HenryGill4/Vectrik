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

        // Raw material cost (from Part → Material FK)
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

    public async Task<BuildCostAllocation> AllocateBuildCostAsync(int buildPackageId)
    {
        var package = await _db.BuildPackages
            .Include(bp => bp.Parts)
            .Include(bp => bp.BuildFileInfo)
            .FirstOrDefaultAsync(bp => bp.Id == buildPackageId);

        if (package == null)
            throw new InvalidOperationException($"Build package {buildPackageId} not found.");

        var result = new BuildCostAllocation
        {
            BuildPackageId = buildPackageId,
            TotalPartCount = package.TotalPartCount
        };

        // Powder cost: EstimatedPowderKg * powder cost-per-kg from system settings
        if (package.BuildFileInfo?.EstimatedPowderKg is > 0)
        {
            var powderRate = await GetSettingDecimalAsync("costing.powder_cost_per_kg", 80m);
            result.PowderCost = (decimal)package.BuildFileInfo.EstimatedPowderKg * powderRate;
        }

        // Gas cost: flat rate per build hour (argon/nitrogen)
        if (package.BuildFileInfo?.EstimatedPrintTimeHours is > 0)
        {
            var gasRatePerHour = await GetSettingDecimalAsync("costing.gas_cost_per_hour", 12m);
            result.GasCost = (decimal)package.BuildFileInfo.EstimatedPrintTimeHours * gasRatePerHour;
        }

        // Laser time cost: print hours * machine hourly rate
        if (package.BuildFileInfo?.EstimatedPrintTimeHours is > 0)
        {
            var machineRate = await GetSettingDecimalAsync("costing.sls_machine_hourly_rate", 95m);
            result.LaserTimeCost = (decimal)package.BuildFileInfo.EstimatedPrintTimeHours * machineRate;
        }

        // Allocate across parts proportionally by quantity
        if (result.TotalPartCount > 0 && result.TotalBuildCost > 0)
        {
            foreach (var part in package.Parts)
            {
                var share = result.TotalBuildCost * ((decimal)part.Quantity / result.TotalPartCount);
                if (result.PerPartCost.ContainsKey(part.PartId))
                    result.PerPartCost[part.PartId] += share;
                else
                    result.PerPartCost[part.PartId] = share;
            }
        }

        return result;
    }

    private async Task<decimal> GetSettingDecimalAsync(string key, decimal defaultValue)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        return decimal.TryParse(setting?.Value, out var val) ? val : defaultValue;
    }
}
