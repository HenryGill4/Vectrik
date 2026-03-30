using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;

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

    public async Task<QuoteLineEstimate> GetQuoteLineEstimateAsync(int partId, int quantity, decimal targetMarginPct = 25)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

        var estimate = new QuoteLineEstimate();

        // 1. Get full cost breakdown from existing pricing engine
        estimate.CostBreakdown = await CalculatePartCostAsync(partId, quantity);

        // 2. Pull PartPricing if configured (sell price, tier)
        var partPricing = await _db.PartPricings
            .FirstOrDefaultAsync(p => p.PartId == partId);

        if (partPricing != null)
        {
            estimate.PartPricingSellPrice = partPricing.SellPricePerUnit;
            estimate.PricingTier = partPricing.PricingTier;
        }

        // 3. Calculate target margin price: Cost / (1 - margin%)
        var costPerPart = quantity > 0 ? estimate.CostBreakdown.TotalCost / quantity : 0;
        estimate.TargetMarginPrice = targetMarginPct < 100
            ? Math.Round(costPerPart / (1 - targetMarginPct / 100m), 2)
            : 0;

        // 4. Stacking options for additive parts
        var buildConfig = await _db.PartAdditiveBuildConfigs
            .FirstOrDefaultAsync(c => c.PartId == partId);

        if (buildConfig?.HasStackingConfiguration == true)
        {
            estimate.IsAdditivePart = true;
            estimate.RecommendedStackLevel = buildConfig.GetRecommendedStackLevel(quantity);

            var machineRate = await GetMachineHourlyRateAsync();

            foreach (var level in buildConfig.AvailableStackLevels)
            {
                var duration = buildConfig.GetStackDuration(level);
                var partsPerBuild = buildConfig.GetPartsPerBuild(level);
                if (!duration.HasValue || !partsPerBuild.HasValue || partsPerBuild.Value == 0)
                    continue;

                var buildsRequired = (int)Math.Ceiling((double)quantity / partsPerBuild.Value);
                var totalBuildHours = buildsRequired * duration.Value;
                var machineCostPerPart = machineRate * (decimal)totalBuildHours / quantity;

                // Recalculate full cost at this stack level:
                // Machine cost changes, but post-processing & material stay the same
                var postProcessAndMaterial = costPerPart > 0
                    ? costPerPart - (estimate.CostBreakdown.LaborCost / quantity)
                    : 0;
                var totalCostPerPart = machineCostPerPart + postProcessAndMaterial;

                var label = level switch
                {
                    1 => "Single Stack",
                    2 => "Double Stack",
                    3 => "Triple Stack",
                    _ => $"{level}x Stack"
                };

                estimate.StackingOptions.Add(new StackingOption(
                    StackLevel: level,
                    Label: label,
                    PartsPerBuild: partsPerBuild.Value,
                    BuildDurationHours: duration.Value,
                    BuildsRequired: buildsRequired,
                    MachineCostPerPart: Math.Round(machineCostPerPart, 2),
                    TotalCostPerPart: Math.Round(totalCostPerPart, 2),
                    TotalCost: Math.Round(totalCostPerPart * quantity, 2),
                    IsRecommended: level == estimate.RecommendedStackLevel));
            }
        }

        // 5. Volume break pricing — show how cost changes at standard breakpoints
        var setupCostTotal = estimate.CostBreakdown.SetupCost;
        var variableCostPerPart = costPerPart > 0 && quantity > 0
            ? (estimate.CostBreakdown.TotalCost - setupCostTotal) / quantity
            : 0;

        var breakpoints = new[] { 1, 5, 10, 25, 50, 100, 250 }
            .Where(q => q != quantity)
            .Append(quantity)
            .Distinct()
            .OrderBy(q => q)
            .ToArray();

        foreach (var qty in breakpoints)
        {
            var setupPerPart = qty > 0 ? setupCostTotal / qty : 0;
            var qtyEstCost = variableCostPerPart + setupPerPart;
            var suggestedPrice = targetMarginPct < 100
                ? Math.Round(qtyEstCost / (1 - targetMarginPct / 100m), 2)
                : 0;

            estimate.QuantityBreaks.Add(new QuantityBreak(
                Quantity: qty,
                CostPerPart: Math.Round(qtyEstCost, 2),
                SetupCostPerPart: Math.Round(setupPerPart, 2),
                TotalCost: Math.Round(qtyEstCost * qty, 2),
                SuggestedPrice: suggestedPrice));
        }

        return estimate;
    }

    private async Task<decimal> GetMachineHourlyRateAsync()
    {
        // Use the SLS machine rate from system settings, fallback to $200/hr (EOS M4 Onyx rate)
        return await GetSettingDecimalAsync("costing.sls_machine_hourly_rate", 200m);
    }

    private async Task<decimal> GetSettingDecimalAsync(string key, decimal defaultValue)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        return decimal.TryParse(setting?.Value, out var val) ? val : defaultValue;
    }
}
