using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;

namespace Vectrik.Services;

public class PartPricingService : IPartPricingService
{
    private readonly TenantDbContext _db;
    private readonly IManufacturingProcessService _processService;
    private readonly IStageCostService _costService;

    public PartPricingService(
        TenantDbContext db,
        IManufacturingProcessService processService,
        IStageCostService costService)
    {
        _db = db;
        _processService = processService;
        _costService = costService;
    }

    public async Task<PartPricing?> GetByPartIdAsync(int partId)
    {
        return await _db.PartPricings
            .Include(p => p.Part)
            .FirstOrDefaultAsync(p => p.PartId == partId);
    }

    public async Task<List<PartPricing>> GetAllAsync()
    {
        return await _db.PartPricings
            .Include(p => p.Part)
            .OrderBy(p => p.Part.PartNumber)
            .ToListAsync();
    }

    public async Task<PartPricing> SaveAsync(PartPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        pricing.LastModifiedDate = DateTime.UtcNow;

        if (pricing.Id == 0)
        {
            var partExists = await _db.Parts.AnyAsync(p => p.Id == pricing.PartId);
            if (!partExists)
                throw new InvalidOperationException($"Part {pricing.PartId} not found.");

            var existing = await _db.PartPricings
                .FirstOrDefaultAsync(p => p.PartId == pricing.PartId);
            if (existing != null)
                throw new InvalidOperationException($"Pricing already exists for part {pricing.PartId}.");

            pricing.CreatedDate = DateTime.UtcNow;
            _db.PartPricings.Add(pricing);
        }
        else
        {
            _db.PartPricings.Update(pricing);
        }

        await _db.SaveChangesAsync();
        return pricing;
    }

    public async Task DeleteAsync(int pricingId)
    {
        var pricing = await _db.PartPricings.FindAsync(pricingId);
        if (pricing == null)
            throw new InvalidOperationException("Pricing record not found.");

        _db.PartPricings.Remove(pricing);
        await _db.SaveChangesAsync();
    }

    public async Task<PartProfitability> CalculateProfitabilityAsync(int partId, int quantity = 1)
    {
        var part = await _db.Parts.FindAsync(partId);
        if (part == null)
            throw new InvalidOperationException($"Part {partId} not found.");

        var pricing = await GetByPartIdAsync(partId);
        var process = await _processService.GetByPartIdAsync(partId);

        var sellPrice = pricing?.SellPricePerUnit ?? 0m;
        var materialCost = pricing?.MaterialCostPerUnit ?? 0m;
        var targetMargin = pricing?.TargetMarginPct ?? 25m;

        // Calculate manufacturing cost from process stages
        var mfgCostPerUnit = 0m;
        if (process != null)
        {
            var batchCapacity = process.DefaultBatchCapacity;
            var batchCount = batchCapacity > 0
                ? (int)Math.Ceiling((double)quantity / batchCapacity)
                : 1;

            var totalCost = 0m;
            foreach (var stage in process.Stages.OrderBy(s => s.ExecutionOrder))
            {
                var batchCountForStage = stage.ProcessingLevel == Models.Enums.ProcessingLevel.Build ? 1 : batchCount;
                var duration = _processService.CalculateStageDuration(stage, quantity, batchCountForStage, buildConfigHours: null);
                var hours = duration.TotalMinutes / 60.0;

                var estimate = await _costService.EstimateCostAsync(
                    stage.ProductionStageId, hours, quantity, batchCountForStage);

                totalCost += estimate.TotalCost;
            }

            mfgCostPerUnit = quantity > 0 ? totalCost / quantity : totalCost;
        }

        var totalCostPerUnit = mfgCostPerUnit + materialCost;
        var profitPerUnit = sellPrice - totalCostPerUnit;
        var marginPct = sellPrice > 0 ? (profitPerUnit / sellPrice) * 100 : 0;

        return new PartProfitability(
            PartId: partId,
            PartNumber: part.PartNumber,
            PartName: part.Name,
            SellPricePerUnit: sellPrice,
            ManufacturingCostPerUnit: mfgCostPerUnit,
            MaterialCostPerUnit: materialCost,
            TotalCostPerUnit: totalCostPerUnit,
            ProfitPerUnit: profitPerUnit,
            MarginPct: marginPct,
            TargetMarginPct: targetMargin,
            MeetsTarget: marginPct >= targetMargin,
            HasPricing: pricing != null,
            HasProcess: process != null);
    }

    public async Task<PricingCoverageStats> GetCoverageStatsAsync()
    {
        var activeParts = await _db.Parts.Where(p => p.IsActive).CountAsync();
        var partsWithPricing = await _db.PartPricings
            .Include(p => p.Part)
            .Where(p => p.Part.IsActive)
            .ToListAsync();
        var partsWithProcess = await _db.ManufacturingProcesses
            .Include(p => p.Part)
            .Where(p => p.Part!.IsActive && p.IsActive)
            .CountAsync();

        var pricedPartIds = partsWithPricing.Select(p => p.PartId).ToHashSet();
        var processPartIds = await _db.ManufacturingProcesses
            .Where(p => p.IsActive)
            .Select(p => p.PartId)
            .ToListAsync();

        var fullyConfigured = processPartIds.Count(id => pricedPartIds.Contains(id));
        var avgMargin = partsWithPricing.Count > 0
            ? partsWithPricing.Average(p => p.TargetMarginPct)
            : 0;
        var avgSellPrice = partsWithPricing.Count > 0
            ? partsWithPricing.Average(p => p.SellPricePerUnit)
            : 0;

        return new PricingCoverageStats(
            TotalActiveParts: activeParts,
            PartsWithPricing: partsWithPricing.Count,
            PartsWithProcess: partsWithProcess,
            PartsFullyConfigured: fullyConfigured,
            AverageMarginPct: avgMargin,
            AverageSellPrice: avgSellPrice);
    }
}
