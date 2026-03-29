using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Models.Maintenance;

namespace Vectrik.Services;

public class DispatchScoringService : IDispatchScoringService
{
    private readonly TenantDbContext _db;

    public DispatchScoringService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<DispatchScore> ScoreDispatchAsync(SetupDispatch dispatch)
    {
        var config = await GetConfigForMachineAsync(dispatch.MachineId);

        var dueDateScore = await CalculateDueDateScoreAsync(dispatch);
        var changeoverScore = await CalculateChangeoverScoreAsync(dispatch);
        var throughputScore = await CalculateThroughputScoreAsync(dispatch);
        var maintenanceModifier = await CalculateMaintenanceModifierAsync(dispatch, config);

        var weighted = (dueDateScore * config.DueDateWeight)
            + (changeoverScore * config.ChangeoverPenaltyWeight)
            + (throughputScore * config.ThroughputWeight);

        var finalScore = (int)Math.Clamp(weighted + maintenanceModifier, 0, 100);

        var reason = BuildPriorityReason(finalScore, dueDateScore, changeoverScore, throughputScore, maintenanceModifier);

        var breakdown = new
        {
            dueDateScore,
            dueDateWeight = config.DueDateWeight,
            changeoverScore,
            changeoverWeight = config.ChangeoverPenaltyWeight,
            throughputScore,
            throughputWeight = config.ThroughputWeight,
            maintenanceModifier,
            finalScore,
            calculatedAt = DateTime.UtcNow.ToString("o")
        };

        return new DispatchScore(
            FinalScore: finalScore,
            DueDateScore: dueDateScore,
            ChangeoverScore: changeoverScore,
            ThroughputScore: throughputScore,
            MaintenanceModifier: maintenanceModifier,
            DueDateWeight: config.DueDateWeight,
            ChangeoverWeight: config.ChangeoverPenaltyWeight,
            ThroughputWeight: config.ThroughputWeight,
            PriorityReason: reason,
            ScoreBreakdownJson: JsonSerializer.Serialize(breakdown));
    }

    public async Task<List<(SetupDispatch Dispatch, DispatchScore Score)>> ScoreAndRankAsync(List<SetupDispatch> dispatches)
    {
        var results = new List<(SetupDispatch Dispatch, DispatchScore Score)>();

        foreach (var dispatch in dispatches)
        {
            var score = await ScoreDispatchAsync(dispatch);
            results.Add((dispatch, score));
        }

        return results.OrderByDescending(r => r.Score.FinalScore).ToList();
    }

    // ── Due Date Score ────────────────────────────────────────

    private async Task<int> CalculateDueDateScoreAsync(SetupDispatch dispatch)
    {
        DateTime? dueDate = null;
        var jobPriority = JobPriority.Normal;

        // Trace to work order due date
        if (dispatch.JobId.HasValue)
        {
            var job = await _db.Jobs
                .Include(j => j.WorkOrderLine)
                    .ThenInclude(wol => wol!.WorkOrder)
                .FirstOrDefaultAsync(j => j.Id == dispatch.JobId.Value);

            if (job != null)
            {
                dueDate = job.WorkOrderLine?.WorkOrder?.DueDate;
                jobPriority = job.Priority;
            }
        }
        else if (dispatch.StageExecutionId.HasValue)
        {
            var execution = await _db.StageExecutions
                .Include(se => se.Job)
                    .ThenInclude(j => j!.WorkOrderLine)
                        .ThenInclude(wol => wol!.WorkOrder)
                .FirstOrDefaultAsync(se => se.Id == dispatch.StageExecutionId.Value);

            if (execution?.Job != null)
            {
                dueDate = execution.Job.WorkOrderLine?.WorkOrder?.DueDate;
                jobPriority = execution.Job.Priority;
            }
        }

        if (!dueDate.HasValue) return 20;

        var hoursUntilDue = (dueDate.Value - DateTime.UtcNow).TotalHours;
        var baseScore = hoursUntilDue switch
        {
            < 0 => 100,    // Overdue
            < 8 => 95,     // Due within 8h
            < 24 => 80,    // Due within 24h
            < 48 => 60,    // Due within 2 days
            < 120 => 40,   // Due within 5 days
            _ => 20
        };

        // Priority multiplier
        var multiplier = jobPriority switch
        {
            JobPriority.Emergency => 1.5,
            JobPriority.Rush => 1.3,
            JobPriority.High => 1.1,
            JobPriority.Normal => 1.0,
            JobPriority.Low => 0.8,
            _ => 1.0
        };

        // Critical path bonus: +15 if completing this unlocks 2+ downstream stages
        var criticalPathBonus = 0;
        if (dispatch.StageExecutionId.HasValue)
        {
            var downstreamCount = await _db.StageExecutions
                .CountAsync(se => se.JobId == dispatch.JobId
                    && se.Status == StageExecutionStatus.NotStarted
                    && se.Id != dispatch.StageExecutionId);
            if (downstreamCount >= 2) criticalPathBonus = 15;
        }

        return (int)Math.Clamp(baseScore * multiplier + criticalPathBonus, 0, 100);
    }

    // ── Changeover Score ──────────────────────────────────────

    private async Task<int> CalculateChangeoverScoreAsync(SetupDispatch dispatch)
    {
        var machine = await _db.Machines.FindAsync(dispatch.MachineId);
        if (machine == null) return 50;

        // Same program = no changeover needed
        if (dispatch.MachineProgramId.HasValue && machine.CurrentProgramId == dispatch.MachineProgramId)
            return 100;

        // Check fixture similarity
        if (dispatch.MachineProgramId.HasValue && machine.CurrentProgramId.HasValue)
        {
            var currentProg = await _db.MachinePrograms.FindAsync(machine.CurrentProgramId.Value);
            var targetProg = await _db.MachinePrograms.FindAsync(dispatch.MachineProgramId.Value);

            if (currentProg != null && targetProg != null)
            {
                // Same fixture
                if (!string.IsNullOrEmpty(currentProg.FixtureRequired)
                    && currentProg.FixtureRequired == targetProg.FixtureRequired)
                    return 70;

                // Same part family (first segment of part number)
                if (dispatch.PartId.HasValue && currentProg.PartId.HasValue)
                {
                    var currentPart = await _db.Parts.FindAsync(currentProg.PartId.Value);
                    var targetPart = await _db.Parts.FindAsync(dispatch.PartId.Value);
                    if (currentPart != null && targetPart != null)
                    {
                        var currentFamily = currentPart.PartNumber.Split('-').FirstOrDefault() ?? "";
                        var targetFamily = targetPart.PartNumber.Split('-').FirstOrDefault() ?? "";
                        if (!string.IsNullOrEmpty(currentFamily) && currentFamily == targetFamily)
                            return 50;
                    }
                }
            }
        }

        // Full changeover: score decreases with estimated changeover time
        var changeoverMinutes = dispatch.EstimatedChangeoverMinutes ?? dispatch.EstimatedSetupMinutes ?? 30;
        return (int)Math.Max(0, 100 - changeoverMinutes * 2);
    }

    // ── Throughput Score ──────────────────────────────────────

    private async Task<int> CalculateThroughputScoreAsync(SetupDispatch dispatch)
    {
        var score = 0;

        // Count downstream waiting executions
        if (dispatch.JobId.HasValue)
        {
            var downstreamWaiting = await _db.StageExecutions
                .CountAsync(se => se.JobId == dispatch.JobId
                    && se.Status == StageExecutionStatus.NotStarted);
            score = Math.Min(100, downstreamWaiting * 15);
        }

        // Machine idle bonus
        var hasInProgress = await _db.SetupDispatches
            .AnyAsync(d => d.MachineId == dispatch.MachineId
                && d.Status == DispatchStatus.InProgress
                && d.Id != dispatch.Id);
        if (!hasInProgress) score += 15;

        return Math.Min(100, score);
    }

    // ── Maintenance Modifier ──────────────────────────────────

    private async Task<int> CalculateMaintenanceModifierAsync(SetupDispatch dispatch, DispatchConfiguration config)
    {
        var machineIdStr = dispatch.MachineId.ToString();

        // Check for upcoming maintenance
        var upcomingMaintenance = await _db.MaintenanceWorkOrders
            .Where(mw => mw.MachineId == machineIdStr
                && mw.Status != MaintenanceWorkOrderStatus.Completed
                && mw.Status != MaintenanceWorkOrderStatus.Cancelled)
            .ToListAsync();

        var nearest = upcomingMaintenance
            .Where(mw => mw.ScheduledDate.HasValue)
            .OrderBy(mw => mw.ScheduledDate!.Value)
            .FirstOrDefault();

        if (nearest?.ScheduledDate == null) return 0;

        var hoursUntilMaintenance = (nearest.ScheduledDate.Value - DateTime.UtcNow).TotalHours;
        var estimatedJobHours = (dispatch.EstimatedSetupMinutes ?? 60) / 60.0;

        // Job overruns maintenance window
        if (estimatedJobHours > hoursUntilMaintenance)
            return -50;

        // Machine in maintenance buffer window and job is short
        if (hoursUntilMaintenance <= config.MaintenanceBufferHours && estimatedJobHours < 2)
            return 20;

        // Fits comfortably
        if (hoursUntilMaintenance > config.MaintenanceBufferHours)
            return 10;

        return 0;
    }

    // ── Helpers ────────────────────────────────────────────────

    private async Task<DispatchConfiguration> GetConfigForMachineAsync(int machineId)
    {
        // Machine-specific config first, then global default
        var config = await _db.DispatchConfigurations
            .FirstOrDefaultAsync(c => c.MachineId == machineId);

        config ??= await _db.DispatchConfigurations
            .FirstOrDefaultAsync(c => c.MachineId == null);

        return config ?? new DispatchConfiguration();
    }

    private static string BuildPriorityReason(int final, int dueDate, int changeover, int throughput, int maintenance)
    {
        var parts = new List<string>();
        if (dueDate >= 80) parts.Add("urgent due date");
        if (changeover >= 80) parts.Add("minimal changeover");
        else if (changeover < 30) parts.Add("heavy changeover penalty");
        if (throughput >= 50) parts.Add("good throughput impact");
        if (maintenance < 0) parts.Add("maintenance conflict");
        else if (maintenance > 0) parts.Add("maintenance-friendly");

        return parts.Count > 0
            ? $"Score {final}: {string.Join(", ", parts)}"
            : $"Score {final}";
    }
}
