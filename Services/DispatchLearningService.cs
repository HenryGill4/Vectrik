using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class DispatchLearningService : IDispatchLearningService
{
    private readonly TenantDbContext _db;

    private static readonly double DefaultAlpha = 0.3;

    public DispatchLearningService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task ProcessCompletedDispatchAsync(int dispatchId)
    {
        var history = await _db.SetupHistories
            .FirstOrDefaultAsync(h => h.SetupDispatchId == dispatchId);
        if (history == null) return;

        var dispatch = await _db.SetupDispatches.FindAsync(dispatchId);
        if (dispatch == null) return;

        var alpha = await GetAlphaAsync();

        // 1. Update MachineProgram setup EMA
        if (history.MachineProgramId.HasValue && history.SetupDurationMinutes > 0)
        {
            await UpdateProgramSetupEmaAsync(history.MachineProgramId.Value, history.SetupDurationMinutes, alpha);
        }

        // 2. Update changeover EMA (from-program → to-program transition)
        if (history.WasChangeover && history.ChangeoverDurationMinutes is > 0
            && history.PreviousProgramId.HasValue && history.MachineProgramId.HasValue)
        {
            await UpdateProgramSetupEmaAsync(history.MachineProgramId.Value,
                history.ChangeoverDurationMinutes.Value, alpha);
        }

        // 3. Update operator proficiency profile
        if (history.OperatorUserId.HasValue && history.SetupDurationMinutes > 0)
        {
            await UpdateOperatorProfileAsync(
                history.OperatorUserId.Value,
                history.MachineId,
                history.MachineProgramId,
                history.SetupDurationMinutes,
                alpha);

            // Recalculate proficiency levels for the machine
            await RecalculateProficiencyLevelsAsync(history.MachineId);
        }
    }

    public async Task RecalculateProficiencyLevelsAsync(int machineId)
    {
        var profiles = await _db.OperatorSetupProfiles
            .Where(p => p.MachineId == machineId && p.MachineProgramId == null && p.SampleCount >= 3)
            .ToListAsync();

        if (profiles.Count < 2) return; // Need at least 2 operators to compare

        var medianSetup = GetMedian(profiles.Select(p => p.AverageSetupMinutes ?? 0).ToList());
        if (medianSetup <= 0) return;

        foreach (var profile in profiles)
        {
            var avg = profile.AverageSetupMinutes ?? 0;
            if (avg <= 0) continue;

            var ratio = avg / medianSetup;

            // Expert(5): ≤70% of median, Advanced(4): ≤85%, Competent(3): ≤100%,
            // Learning(2): ≤120%, Novice(1): >120%
            profile.ProficiencyLevel = ratio switch
            {
                <= 0.70 => 5, // Expert
                <= 0.85 => 4, // Advanced
                <= 1.00 => 3, // Competent
                <= 1.20 => 2, // Learning
                _ => 1         // Novice
            };

            profile.LastUpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<int?> SuggestBestOperatorAsync(int machineId, int? machineProgramId = null)
    {
        // First try program-specific profiles
        if (machineProgramId.HasValue)
        {
            var programProfile = await _db.OperatorSetupProfiles
                .Where(p => p.MachineId == machineId
                    && p.MachineProgramId == machineProgramId
                    && p.SampleCount >= 3)
                .ToListAsync();

            var preferred = programProfile
                .OrderByDescending(p => p.IsPreferred ? 1 : 0)
                .ThenByDescending(p => p.ProficiencyLevel)
                .ThenBy(p => p.AverageSetupMinutes ?? double.MaxValue)
                .FirstOrDefault();

            if (preferred != null) return preferred.UserId;
        }

        // Fall back to machine-level profiles
        var machineProfiles = await _db.OperatorSetupProfiles
            .Where(p => p.MachineId == machineId && p.MachineProgramId == null && p.SampleCount >= 3)
            .ToListAsync();

        var best = machineProfiles
            .OrderByDescending(p => p.IsPreferred ? 1 : 0)
            .ThenByDescending(p => p.ProficiencyLevel)
            .ThenBy(p => p.AverageSetupMinutes ?? double.MaxValue)
            .FirstOrDefault();

        return best?.UserId;
    }

    public async Task<List<OperatorSetupProfile>> GetMachineProfilesAsync(int machineId)
    {
        var profiles = await _db.OperatorSetupProfiles
            .Include(p => p.User)
            .Include(p => p.MachineProgram)
            .Where(p => p.MachineId == machineId)
            .ToListAsync();

        return profiles.OrderByDescending(p => p.ProficiencyLevel).ThenBy(p => p.AverageSetupMinutes).ToList();
    }

    public async Task<List<OperatorSetupProfile>> GetOperatorProfilesAsync(int userId)
    {
        var profiles = await _db.OperatorSetupProfiles
            .Include(p => p.Machine)
            .Include(p => p.MachineProgram)
            .Where(p => p.UserId == userId)
            .ToListAsync();

        return profiles.OrderByDescending(p => p.ProficiencyLevel).ThenBy(p => p.AverageSetupMinutes).ToList();
    }

    // ── EMA Update Helpers ────────────────────────────────────

    private async Task UpdateProgramSetupEmaAsync(int programId, double actualMinutes, double alpha)
    {
        var program = await _db.MachinePrograms.FindAsync(programId);
        if (program == null) return;

        var prevAvg = program.ActualAverageSetupMinutes;
        program.SetupSampleCount++;

        if (prevAvg.HasValue)
        {
            program.ActualAverageSetupMinutes = alpha * actualMinutes + (1 - alpha) * prevAvg.Value;
            var diff = actualMinutes - prevAvg.Value;
            var prevVar = program.SetupVarianceMinutes ?? 0;
            program.SetupVarianceMinutes = (1 - alpha) * (prevVar + alpha * diff * diff);
        }
        else
        {
            program.ActualAverageSetupMinutes = actualMinutes;
            program.SetupVarianceMinutes = 0;
        }

        program.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task UpdateOperatorProfileAsync(
        int userId, int machineId, int? programId, double actualMinutes, double alpha)
    {
        // Update machine-level profile
        await UpdateSingleProfileAsync(userId, machineId, null, actualMinutes, alpha);

        // Update program-level profile if applicable
        if (programId.HasValue)
            await UpdateSingleProfileAsync(userId, machineId, programId, actualMinutes, alpha);
    }

    private async Task UpdateSingleProfileAsync(
        int userId, int machineId, int? programId, double actualMinutes, double alpha)
    {
        var profile = await _db.OperatorSetupProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId
                && p.MachineId == machineId
                && p.MachineProgramId == programId);

        if (profile == null)
        {
            profile = new OperatorSetupProfile
            {
                UserId = userId,
                MachineId = machineId,
                MachineProgramId = programId,
                AverageSetupMinutes = actualMinutes,
                SampleCount = 1,
                VarianceMinutes = 0,
                FastestSetupMinutes = actualMinutes,
                ProficiencyLevel = 1
            };
            _db.OperatorSetupProfiles.Add(profile);
        }
        else
        {
            var prevAvg = profile.AverageSetupMinutes ?? actualMinutes;
            profile.SampleCount++;

            profile.AverageSetupMinutes = alpha * actualMinutes + (1 - alpha) * prevAvg;

            var diff = actualMinutes - prevAvg;
            var prevVar = profile.VarianceMinutes ?? 0;
            profile.VarianceMinutes = (1 - alpha) * (prevVar + alpha * diff * diff);

            if (!profile.FastestSetupMinutes.HasValue || actualMinutes < profile.FastestSetupMinutes)
                profile.FastestSetupMinutes = actualMinutes;
        }

        profile.LastUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Helpers ────────────────────────────────────────────────

    private async Task<double> GetAlphaAsync()
    {
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "dispatch.setup_ema_alpha");
        if (setting != null && double.TryParse(setting.Value, out var v)) return v;

        // Fall back to general scheduling alpha
        setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "scheduling.ema_alpha");
        return setting is not null && double.TryParse(setting.Value, out var v2) ? v2 : DefaultAlpha;
    }

    private static double GetMedian(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }
}
