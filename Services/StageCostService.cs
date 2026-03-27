using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;

namespace Vectrik.Services;

public class StageCostService : IStageCostService
{
    private readonly TenantDbContext _db;

    public StageCostService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<StageCostProfile>> GetAllAsync()
    {
        return await _db.StageCostProfiles
            .Include(p => p.ProductionStage)
            .OrderBy(p => p.ProductionStage.DisplayOrder)
            .ToListAsync();
    }

    public async Task<StageCostProfile?> GetByStageIdAsync(int productionStageId)
    {
        return await _db.StageCostProfiles
            .Include(p => p.ProductionStage)
            .FirstOrDefaultAsync(p => p.ProductionStageId == productionStageId);
    }

    public async Task<StageCostProfile> SaveAsync(StageCostProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        profile.LastModifiedDate = DateTime.UtcNow;

        if (profile.Id == 0)
        {
            // Ensure the ProductionStage exists
            var stageExists = await _db.ProductionStages.AnyAsync(s => s.Id == profile.ProductionStageId);
            if (!stageExists)
                throw new InvalidOperationException($"ProductionStage {profile.ProductionStageId} not found.");

            // Ensure no duplicate
            var existing = await _db.StageCostProfiles
                .FirstOrDefaultAsync(p => p.ProductionStageId == profile.ProductionStageId);
            if (existing != null)
                throw new InvalidOperationException($"A cost profile already exists for stage {profile.ProductionStageId}.");

            profile.CreatedDate = DateTime.UtcNow;
            _db.StageCostProfiles.Add(profile);
        }
        else
        {
            _db.StageCostProfiles.Update(profile);
        }

        await _db.SaveChangesAsync();
        return profile;
    }

    public async Task DeleteAsync(int profileId)
    {
        var profile = await _db.StageCostProfiles.FindAsync(profileId);
        if (profile == null)
            throw new InvalidOperationException("Cost profile not found.");

        _db.StageCostProfiles.Remove(profile);
        await _db.SaveChangesAsync();
    }

    public async Task<StageCostEstimate> EstimateCostAsync(
        int productionStageId, double durationHours, int partCount, int batchCount = 1)
    {
        var profile = await GetByStageIdAsync(productionStageId);

        if (profile != null)
        {
            var laborCost = profile.LaborCostPerHour * (decimal)durationHours;
            var equipmentCost = profile.EquipmentCostPerHour * (decimal)durationHours;
            var overheadCost = profile.OverheadCostPerHour * (decimal)durationHours;
            var partCosts = profile.PerPartCost * partCount;
            var toolingCost = profile.ToolingCostPerRun * batchCount;

            // Apply general overhead percentage
            var directTimeCost = laborCost + equipmentCost + overheadCost;
            var overheadMarkup = directTimeCost * (decimal)(profile.OverheadPercent / 100);
            var totalTimeCost = directTimeCost + overheadMarkup;

            var externalCost = 0m;
            if (profile.ExternalVendorCostPerPart > 0 || profile.ExternalShippingCost > 0)
            {
                externalCost = (profile.ExternalVendorCostPerPart * partCount)
                    + (profile.ExternalShippingCost * batchCount);
                externalCost *= (1 + (decimal)(profile.ExternalMarkupPercent / 100));
            }

            var total = totalTimeCost + partCosts + toolingCost + externalCost;
            var costPerPart = partCount > 0 ? total / partCount : total;

            return new StageCostEstimate(
                LaborCost: laborCost,
                EquipmentCost: equipmentCost,
                OverheadCost: overheadCost + overheadMarkup,
                PerPartCost: partCosts,
                ToolingCost: toolingCost,
                ExternalCost: externalCost,
                TotalCost: total,
                CostPerPart: costPerPart,
                FullyLoadedHourlyRate: profile.FullyLoadedHourlyRate,
                UsedCostProfile: true);
        }

        // Fallback: use ProductionStage.DefaultHourlyRate
        var stage = await _db.ProductionStages.FindAsync(productionStageId);
        var fallbackRate = stage?.DefaultHourlyRate ?? 85m;
        var fallbackTotal = fallbackRate * (decimal)durationHours;
        var fallbackPerPart = partCount > 0 ? fallbackTotal / partCount : fallbackTotal;

        return new StageCostEstimate(
            LaborCost: fallbackTotal,
            EquipmentCost: 0,
            OverheadCost: 0,
            PerPartCost: 0,
            ToolingCost: 0,
            ExternalCost: 0,
            TotalCost: fallbackTotal,
            CostPerPart: fallbackPerPart,
            FullyLoadedHourlyRate: fallbackRate,
            UsedCostProfile: false);
    }
}
