using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;

namespace Opcentrix_V3.Services;

public class LearningService : ILearningService
{
    private readonly TenantDbContext _db;

    private static readonly double DefaultAlpha = 0.3;
    private static readonly int DefaultAutoSwitchThreshold = 3;

    public LearningService(TenantDbContext db)
    {
        _db = db;
    }

    private async Task<double> GetAlphaAsync()
    {
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "scheduling.ema_alpha");
        return setting is not null && double.TryParse(setting.Value, out var v) ? v : DefaultAlpha;
    }

    private async Task<int> GetAutoSwitchThresholdAsync()
    {
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "scheduling.ema_auto_switch_samples");
        return setting is not null && int.TryParse(setting.Value, out var v) ? v : DefaultAutoSwitchThreshold;
    }

    public async Task UpdateEstimateAsync(int partId, int productionStageId, double actualDurationHours)
    {
        var requirement = await _db.PartStageRequirements
            .FirstOrDefaultAsync(r => r.PartId == partId
                && r.ProductionStageId == productionStageId
                && r.IsActive);

        if (requirement == null) return;

        var alpha = await GetAlphaAsync();
        var autoSwitchThreshold = await GetAutoSwitchThresholdAsync();

        requirement.LastActualDurationHours = actualDurationHours;
        requirement.ActualSampleCount++;

        // EMA calculation: newAvg = α * actual + (1 - α) * previousAvg
        if (requirement.ActualAverageDurationHours.HasValue)
        {
            requirement.ActualAverageDurationHours =
                alpha * actualDurationHours + (1 - alpha) * requirement.ActualAverageDurationHours.Value;
        }
        else
        {
            // First sample — use actual as the baseline
            requirement.ActualAverageDurationHours = actualDurationHours;
        }

        // Auto-switch to "Auto" after enough samples
        if (requirement.ActualSampleCount >= autoSwitchThreshold && requirement.EstimateSource == "Manual")
        {
            requirement.EstimateSource = "Auto";
        }

        requirement.EstimateLastUpdated = DateTime.UtcNow;
        requirement.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task UpdateProcessStageEstimateAsync(int processStageId, double actualDurationMinutes)
    {
        var stage = await _db.ProcessStages.FindAsync(processStageId);
        if (stage is null) return;

        var alpha = await GetAlphaAsync();
        var autoSwitchThreshold = await GetAutoSwitchThresholdAsync();

        var sampleCount = stage.ActualSampleCount ?? 0;
        sampleCount++;

        // EMA calculation: newAvg = α * actual + (1 - α) * previousAvg
        if (stage.ActualAverageDurationMinutes.HasValue)
        {
            stage.ActualAverageDurationMinutes =
                alpha * actualDurationMinutes + (1 - alpha) * stage.ActualAverageDurationMinutes.Value;
        }
        else
        {
            stage.ActualAverageDurationMinutes = actualDurationMinutes;
        }

        stage.ActualSampleCount = sampleCount;

        // Auto-switch to "Auto" after enough samples
        if (sampleCount >= autoSwitchThreshold && stage.EstimateSource != "Auto")
        {
            stage.EstimateSource = "Auto";
        }

        stage.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task UpdateMachineProgramEstimateAsync(int machineProgramId, double actualDurationMinutes)
    {
        var program = await _db.MachinePrograms.FindAsync(machineProgramId);
        if (program is null) return;

        var alpha = await GetAlphaAsync();
        var autoSwitchThreshold = await GetAutoSwitchThresholdAsync();

        var sampleCount = program.ActualSampleCount ?? 0;
        sampleCount++;

        // EMA calculation: newAvg = α * actual + (1 - α) * previousAvg
        if (program.ActualAverageDurationMinutes.HasValue)
        {
            program.ActualAverageDurationMinutes =
                alpha * actualDurationMinutes + (1 - alpha) * program.ActualAverageDurationMinutes.Value;
        }
        else
        {
            program.ActualAverageDurationMinutes = actualDurationMinutes;
        }

        program.ActualSampleCount = sampleCount;
        program.TotalRunCount++;

        // Auto-switch to "Auto" after enough samples
        if (sampleCount >= autoSwitchThreshold && program.EstimateSource != "Auto")
        {
            program.EstimateSource = "Auto";
        }

        program.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
