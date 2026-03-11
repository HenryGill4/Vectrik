using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;

namespace Opcentrix_V3.Services;

public class LearningService : ILearningService
{
    private readonly TenantDbContext _db;
    private const double Alpha = 0.3; // EMA smoothing factor
    private const int AutoSwitchThreshold = 3; // Switch to "Auto" after this many samples

    public LearningService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task UpdateEstimateAsync(int partId, int productionStageId, double actualDurationHours)
    {
        var requirement = await _db.PartStageRequirements
            .FirstOrDefaultAsync(r => r.PartId == partId
                && r.ProductionStageId == productionStageId
                && r.IsActive);

        if (requirement == null) return;

        requirement.LastActualDurationHours = actualDurationHours;
        requirement.ActualSampleCount++;

        // EMA calculation: newAvg = α * actual + (1 - α) * previousAvg
        if (requirement.ActualAverageDurationHours.HasValue)
        {
            requirement.ActualAverageDurationHours =
                Alpha * actualDurationHours + (1 - Alpha) * requirement.ActualAverageDurationHours.Value;
        }
        else
        {
            // First sample — use actual as the baseline
            requirement.ActualAverageDurationHours = actualDurationHours;
        }

        // Auto-switch to "Auto" after enough samples
        if (requirement.ActualSampleCount >= AutoSwitchThreshold && requirement.EstimateSource == "Manual")
        {
            requirement.EstimateSource = "Auto";
        }

        requirement.EstimateLastUpdated = DateTime.UtcNow;
        requirement.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
