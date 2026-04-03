using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;

namespace Vectrik.Services;

public class SchedulingWeightsService : ISchedulingWeightsService
{
    private readonly TenantDbContext _db;

    public SchedulingWeightsService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<SchedulingWeights> GetWeightsAsync()
    {
        var weights = await _db.SchedulingWeights.FirstOrDefaultAsync();
        if (weights != null) return weights;

        // Auto-create default row
        weights = new SchedulingWeights { LastModifiedDate = DateTime.UtcNow };
        _db.SchedulingWeights.Add(weights);
        await _db.SaveChangesAsync();
        return weights;
    }

    public async Task UpdateWeightsAsync(SchedulingWeights weights)
    {
        weights.LastModifiedDate = DateTime.UtcNow;
        _db.SchedulingWeights.Update(weights);
        await _db.SaveChangesAsync();
    }

    public async Task<SchedulingWeights> ResetToDefaultsAsync()
    {
        var weights = await _db.SchedulingWeights.FirstOrDefaultAsync();
        if (weights == null)
        {
            weights = new SchedulingWeights { LastModifiedDate = DateTime.UtcNow };
            _db.SchedulingWeights.Add(weights);
        }
        else
        {
            var defaults = new SchedulingWeights();
            weights.BaseScore = defaults.BaseScore;
            weights.ChangeoverAlignmentBonus = defaults.ChangeoverAlignmentBonus;
            weights.DowntimePenaltyPerHour = defaults.DowntimePenaltyPerHour;
            weights.MaxDowntimePenalty = defaults.MaxDowntimePenalty;
            weights.EarlinessBonus4h = defaults.EarlinessBonus4h;
            weights.EarlinessBonus24h = defaults.EarlinessBonus24h;
            weights.OverproductionPenaltyMax = defaults.OverproductionPenaltyMax;
            weights.WeekendOptimizationBonus = defaults.WeekendOptimizationBonus;
            weights.ShiftAlignedBonus = defaults.ShiftAlignedBonus;
            weights.StackChangeoverBonus = defaults.StackChangeoverBonus;
            weights.StackDemandFitBonus = defaults.StackDemandFitBonus;
            weights.StackEfficiencyMultiplier = defaults.StackEfficiencyMultiplier;
            weights.LastModifiedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return weights;
    }
}
