using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class StageService : IStageService
{
    private readonly TenantDbContext _db;
    private readonly IBuildPlanningService _buildPlanning;

    public StageService(TenantDbContext db, IBuildPlanningService buildPlanning)
    {
        _db = db;
        _buildPlanning = buildPlanning;
    }

    // ── Stage CRUD ──────────────────────────────────────────────

    public async Task<List<ProductionStage>> GetAllStagesAsync(bool activeOnly = true)
    {
        var query = _db.ProductionStages.AsQueryable();
        if (activeOnly)
            query = query.Where(s => s.IsActive);
        return await query.OrderBy(s => s.DisplayOrder).ToListAsync();
    }

    public async Task<ProductionStage?> GetStageByIdAsync(int id)
    {
        return await _db.ProductionStages.FindAsync(id);
    }

    public async Task<ProductionStage?> GetStageBySlugAsync(string slug)
    {
        return await _db.ProductionStages
            .FirstOrDefaultAsync(s => s.StageSlug == slug);
    }

    public async Task<ProductionStage> CreateStageAsync(ProductionStage stage)
    {
        stage.CreatedDate = DateTime.UtcNow;
        stage.LastModifiedDate = DateTime.UtcNow;
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();
        return stage;
    }

    public async Task<ProductionStage> UpdateStageAsync(ProductionStage stage)
    {
        stage.LastModifiedDate = DateTime.UtcNow;
        _db.ProductionStages.Update(stage);
        await _db.SaveChangesAsync();
        return stage;
    }

    public async Task DeleteStageAsync(int id)
    {
        var stage = await _db.ProductionStages.FindAsync(id);
        if (stage == null) throw new InvalidOperationException("Stage not found.");
        stage.IsActive = false;
        stage.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Queue queries ───────────────────────────────────────────

    public async Task<List<StageExecution>> GetQueueForStageAsync(int stageId)
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.Job)
                .ThenInclude(j => j!.WorkOrderLine)
                    .ThenInclude(wl => wl!.WorkOrder)
            .Include(e => e.Machine)
            .Where(e => e.ProductionStageId == stageId && e.Status == StageExecutionStatus.NotStarted)
            .OrderBy(e => e.Job != null ? e.Job.Priority : JobPriority.Normal)
            .ThenBy(e => e.Job != null ? e.Job.ScheduledEnd : DateTime.MaxValue)
            .ToListAsync();
    }

    public async Task<List<StageExecution>> GetActiveWorkForStageAsync(int stageId)
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.Operator)
            .Include(e => e.Machine)
            .Where(e => e.ProductionStageId == stageId
                && (e.Status == StageExecutionStatus.InProgress || e.Status == StageExecutionStatus.Paused))
            .OrderBy(e => e.StartedAt)
            .ToListAsync();
    }

    public async Task<List<StageExecution>> GetRecentCompletionsAsync(int stageId, int count = 20)
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.Operator)
            .Where(e => e.ProductionStageId == stageId
                && (e.Status == StageExecutionStatus.Completed || e.Status == StageExecutionStatus.Failed))
            .OrderByDescending(e => e.CompletedAt)
            .Take(count)
            .ToListAsync();
    }

    // ── Operator workflows ──────────────────────────────────────

    public async Task<StageExecution> StartStageExecutionAsync(int executionId, int operatorUserId, string operatorName)
    {
        var execution = await _db.StageExecutions
            .Include(e => e.Machine)
            .FirstOrDefaultAsync(e => e.Id == executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.Status = StageExecutionStatus.InProgress;
        execution.StartedAt = DateTime.UtcNow;
        execution.ActualStartAt = DateTime.UtcNow;
        execution.OperatorUserId = operatorUserId;
        execution.OperatorName = operatorName;
        execution.LastModifiedDate = DateTime.UtcNow;

        // Update machine status if assigned
        if (execution.MachineId.HasValue)
        {
            var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == execution.MachineId.Value);
            if (machine != null)
            {
                machine.Status = MachineStatus.Running;
                machine.LastModifiedDate = DateTime.UtcNow;
            }
        }

        // Update job status if still draft/scheduled
        if (execution.JobId.HasValue)
        {
            var job = await _db.Jobs.FindAsync(execution.JobId.Value);
            if (job != null && (job.Status == JobStatus.Draft || job.Status == JobStatus.Scheduled))
            {
                job.Status = JobStatus.InProgress;
                job.ActualStart ??= DateTime.UtcNow;
                job.LastStatusChangeUtc = DateTime.UtcNow;
                job.LastModifiedDate = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return execution;
    }

    public async Task<StageExecution> CompleteStageExecutionAsync(int executionId, string? customFieldValues = null, string? notes = null)
    {
        var execution = await _db.StageExecutions
            .Include(e => e.ProductionStage)
            .Include(e => e.Job)
                .ThenInclude(j => j!.Stages)
            .FirstOrDefaultAsync(e => e.Id == executionId);

        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.Status = StageExecutionStatus.Completed;
        execution.CompletedAt = DateTime.UtcNow;
        execution.ActualEndAt = DateTime.UtcNow;
        execution.LastModifiedDate = DateTime.UtcNow;

        if (execution.StartedAt.HasValue)
            execution.ActualHours = (DateTime.UtcNow - execution.StartedAt.Value).TotalHours;

        if (!string.IsNullOrWhiteSpace(customFieldValues))
            execution.CustomFieldValues = customFieldValues;

        if (!string.IsNullOrWhiteSpace(notes))
            execution.CompletionNotes = notes;

        // Release machine
        if (execution.MachineId.HasValue)
        {
            var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == execution.MachineId.Value);
            if (machine != null)
            {
                machine.Status = MachineStatus.Idle;
                machine.TotalOperatingHours += execution.ActualHours ?? 0;
                machine.HoursSinceLastMaintenance += execution.ActualHours ?? 0;
                machine.LastModifiedDate = DateTime.UtcNow;
            }
        }

        // Activate next stage in job sequence
        if (execution.Job != null)
        {
            var stages = execution.Job.Stages
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Id)
                .ToList();

            var currentIdx = stages.FindIndex(s => s.Id == execution.Id);
            if (currentIdx >= 0 && currentIdx < stages.Count - 1)
            {
                // There's a next stage — it stays NotStarted (ready for operator to pick up)
            }
            else
            {
                // All stages complete — check if job is done
                var allDone = stages.All(s =>
                    s.Status == StageExecutionStatus.Completed ||
                    s.Status == StageExecutionStatus.Skipped);

                if (allDone)
                {
                    execution.Job.Status = JobStatus.Completed;
                    execution.Job.ActualEnd = DateTime.UtcNow;
                    execution.Job.LastStatusChangeUtc = DateTime.UtcNow;
                    execution.Job.LastModifiedDate = DateTime.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync();

        // If this was the Wire EDM build-level stage completing, spawn per-part jobs
        if (execution.BuildPackageId.HasValue
            && execution.ProductionStage.IsBuildLevelStage
            && execution.ProductionStage.StageSlug == "wire-edm")
        {
            await _buildPlanning.CreatePartStageExecutionsAsync(
                execution.BuildPackageId.Value,
                execution.OperatorName ?? execution.CreatedBy ?? "System");
        }

        return execution;
    }

    public async Task<StageExecution> SkipStageExecutionAsync(int executionId, string reason)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.Status = StageExecutionStatus.Skipped;
        execution.Notes = reason;
        execution.CompletedAt = DateTime.UtcNow;
        execution.ActualEndAt = DateTime.UtcNow;
        execution.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return execution;
    }

    public async Task<StageExecution> FailStageExecutionAsync(int executionId, string reason)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.Status = StageExecutionStatus.Failed;
        execution.FailureReason = reason;
        execution.Issues = reason;
        execution.CompletedAt = DateTime.UtcNow;
        execution.ActualEndAt = DateTime.UtcNow;
        execution.LastModifiedDate = DateTime.UtcNow;

        if (execution.StartedAt.HasValue)
            execution.ActualHours = (DateTime.UtcNow - execution.StartedAt.Value).TotalHours;

        // Release machine
        if (execution.MachineId.HasValue)
        {
            var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == execution.MachineId.Value);
            if (machine != null)
            {
                machine.Status = MachineStatus.Idle;
                machine.LastModifiedDate = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return execution;
    }

    public async Task<StageExecution> PauseStageExecutionAsync(int executionId, string reason, DelayCategory category, string loggedBy)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.Status = StageExecutionStatus.Paused;
        execution.LastModifiedDate = DateTime.UtcNow;

        _db.DelayLogs.Add(new DelayLog
        {
            StageExecutionId = executionId,
            JobId = execution.JobId,
            Reason = reason,
            Category = category,
            StartedAt = DateTime.UtcNow,
            LoggedBy = loggedBy,
            LoggedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return execution;
    }

    public async Task<StageExecution> ResumeStageExecutionAsync(int executionId)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.Status = StageExecutionStatus.InProgress;
        execution.LastModifiedDate = DateTime.UtcNow;

        // Resolve any open delay logs
        var openDelay = await _db.DelayLogs
            .Where(d => d.StageExecutionId == executionId && d.ResolvedAt == null)
            .OrderByDescending(d => d.StartedAt)
            .FirstOrDefaultAsync();

        if (openDelay != null)
        {
            openDelay.ResolvedAt = DateTime.UtcNow;
            openDelay.DelayMinutes = (int)(DateTime.UtcNow - openDelay.StartedAt).TotalMinutes;
        }

        await _db.SaveChangesAsync();
        return execution;
    }

    public async Task LogUnmannedStartAsync(int executionId, int machineId)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.Status = StageExecutionStatus.InProgress;
        execution.StartedAt = DateTime.UtcNow;
        execution.ActualStartAt = DateTime.UtcNow;
        execution.MachineId = machineId;
        execution.IsUnmanned = true;
        execution.LastModifiedDate = DateTime.UtcNow;

        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == machineId);
        if (machine != null)
        {
            machine.Status = MachineStatus.Running;
            machine.LastModifiedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    // ── Delay logging ────────────────────────────────────────────

    public async Task<DelayLog> LogDelayAsync(int executionId, string reason, DelayCategory category, int delayMinutes, string loggedBy, string? notes = null)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        var delay = new DelayLog
        {
            StageExecutionId = executionId,
            JobId = execution.JobId,
            Reason = reason,
            Category = category,
            DelayMinutes = delayMinutes,
            StartedAt = DateTime.UtcNow,
            ResolvedAt = DateTime.UtcNow,
            LoggedBy = loggedBy,
            LoggedAt = DateTime.UtcNow,
            Notes = notes
        };

        _db.DelayLogs.Add(delay);
        await _db.SaveChangesAsync();
        return delay;
    }

    // ── Operator queue ──────────────────────────────────────────

    public async Task<List<StageExecution>> GetOperatorQueueAsync(int operatorUserId)
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.Job)
                .ThenInclude(j => j!.WorkOrderLine)
                    .ThenInclude(wl => wl!.WorkOrder)
            .Include(e => e.ProductionStage)
            .Include(e => e.Machine)
            .Where(e => e.OperatorUserId == operatorUserId
                && (e.Status == StageExecutionStatus.NotStarted
                    || e.Status == StageExecutionStatus.InProgress
                    || e.Status == StageExecutionStatus.Paused))
            .OrderBy(e => e.Status == StageExecutionStatus.InProgress ? 0 :
                          e.Status == StageExecutionStatus.Paused ? 1 : 2)
            .ThenBy(e => e.Job != null ? e.Job.Priority : JobPriority.Normal)
            .ThenBy(e => e.Job != null ? e.Job.ScheduledEnd : DateTime.MaxValue)
            .ToListAsync();
    }

    public async Task<StageExecution?> GetCurrentExecutionForOperatorAsync(int operatorUserId)
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.ProductionStage)
            .Include(e => e.Machine)
            .Where(e => e.OperatorUserId == operatorUserId
                && (e.Status == StageExecutionStatus.InProgress || e.Status == StageExecutionStatus.Paused))
            .FirstOrDefaultAsync();
    }

    public async Task<List<StageExecution>> GetAvailableWorkAsync()
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.ProductionStage)
            .Include(e => e.Machine)
            .Where(e => e.OperatorUserId == null
                && e.Status == StageExecutionStatus.NotStarted
                && !e.IsUnmanned)
            .OrderBy(e => e.Job != null ? e.Job.Priority : JobPriority.Normal)
            .ThenBy(e => e.Job != null ? e.Job.ScheduledEnd : DateTime.MaxValue)
            .Take(50)
            .ToListAsync();
    }

    // ── Machine queue ───────────────────────────────────────────

    public async Task<List<StageExecution>> GetMachineQueueAsync(int machineId)
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.ProductionStage)
            .Include(e => e.Operator)
            .Where(e => e.MachineId == machineId
                && (e.Status == StageExecutionStatus.NotStarted
                    || e.Status == StageExecutionStatus.InProgress
                    || e.Status == StageExecutionStatus.Paused))
            .OrderBy(e => e.ScheduledStartAt ?? DateTime.MaxValue)
            .ToListAsync();
    }

    public async Task AssignOperatorAsync(int executionId, int operatorUserId)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        var user = await _db.Users.FindAsync(operatorUserId);
        execution.OperatorUserId = operatorUserId;
        execution.OperatorName = user?.FullName;
        execution.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task AssignMachineAsync(int executionId, int machineId)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.MachineId = machineId;
        execution.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Scheduling ──────────────────────────────────────────────

    public async Task<List<StageExecution>> GetScheduledExecutionsAsync(DateTime from, DateTime to)
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.ProductionStage)
            .Include(e => e.Operator)
            .Include(e => e.Machine)
            .Include(e => e.BuildPackage)
            .Where(e => e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed
                && ((e.ScheduledStartAt != null && e.ScheduledStartAt <= to && (e.ScheduledEndAt ?? e.ScheduledStartAt) >= from)
                    || (e.ScheduledStartAt == null && e.Job != null && e.Job.ScheduledStart <= to && e.Job.ScheduledEnd >= from)))
            .OrderBy(e => e.ScheduledStartAt ?? (e.Job != null ? e.Job.ScheduledStart : DateTime.MaxValue))
            .ToListAsync();
    }

    public async Task<List<StageExecution>> GetUnscheduledExecutionsAsync()
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.ProductionStage)
            .Include(e => e.Machine)
            .Where(e => e.Status == StageExecutionStatus.NotStarted
                && (e.ScheduledStartAt == null || e.MachineId == null))
            .OrderByDescending(e => e.Job != null ? (int)e.Job.Priority : 0)
            .ThenBy(e => e.Job != null ? e.Job.ScheduledStart : DateTime.MaxValue)
            .ThenBy(e => e.SortOrder)
            .ToListAsync();
    }

    public async Task UpdateScheduleAsync(int executionId, DateTime start, DateTime end, int? machineId = null)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.ScheduledStartAt = start;
        execution.ScheduledEndAt = end;
        if (machineId.HasValue)
            execution.MachineId = machineId.Value;
        execution.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Capacity ────────────────────────────────────────────────

    public async Task<List<MachineCapacityInfo>> GetMachineCapacityAsync(DateTime from, DateTime to)
    {
        var machines = await _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling)
            .ToListAsync();

        var shifts = await _db.OperatingShifts
            .Where(s => s.IsActive)
            .ToListAsync();

        var scheduledWork = await _db.StageExecutions
            .Where(e => e.MachineId != null
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed
                && e.ScheduledEndAt > from
                && e.ScheduledStartAt < to)
            .ToListAsync();

        var totalDays = (to - from).TotalDays;
        var result = new List<MachineCapacityInfo>();

        foreach (var machine in machines)
        {
            // Calculate available hours from shifts
            double availableHours = 0;
            for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                var dayName = day.DayOfWeek.ToString()[..3];
                foreach (var shift in shifts)
                {
                    if (shift.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase))
                    {
                        var shiftHours = (shift.EndTime - shift.StartTime).TotalHours;
                        if (shiftHours < 0) shiftHours += 24; // overnight shift
                        availableHours += shiftHours;
                    }
                }
            }

            // Calculate loaded hours
            var machineWork = scheduledWork.Where(w => w.MachineId == machine.Id).ToList();
            double loadedHours = machineWork.Sum(w =>
            {
                if (w.ScheduledStartAt.HasValue && w.ScheduledEndAt.HasValue)
                    return (w.ScheduledEndAt.Value - w.ScheduledStartAt.Value).TotalHours;
                return w.EstimatedHours ?? 0;
            });

            var utilization = availableHours > 0 ? (loadedHours / availableHours) * 100 : 0;

            result.Add(new MachineCapacityInfo(
                machine.Id,
                machine.Name,
                machine.MachineType,
                Math.Round(availableHours, 1),
                Math.Round(loadedHours, 1),
                Math.Round(utilization, 1)
            ));
        }

        return result.OrderByDescending(r => r.UtilizationPct).ToList();
    }
}
