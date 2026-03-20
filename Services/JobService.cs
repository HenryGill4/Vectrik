using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class JobService : IJobService
{
    private readonly TenantDbContext _db;
    private readonly ISchedulingService _scheduler;

    public JobService(TenantDbContext db, ISchedulingService scheduler)
    {
        _db = db;
        _scheduler = scheduler;
    }

    public async Task<List<Job>> GetAllJobsAsync(JobStatus? statusFilter = null)
    {
        var query = _db.Jobs
            .Include(j => j.Part)
            .AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(j => j.Status == statusFilter.Value);

        return await query.OrderBy(j => j.ScheduledStart).ToListAsync();
    }

    public async Task<List<Job>> GetJobsByMachineAsync(string machineId, DateTime? from = null, DateTime? to = null)
    {
        var query = _db.Jobs
            .Include(j => j.Part)
            .Where(j => j.MachineId == machineId);

        if (from.HasValue)
            query = query.Where(j => j.ScheduledEnd >= from.Value);
        if (to.HasValue)
            query = query.Where(j => j.ScheduledStart <= to.Value);

        return await query.OrderBy(j => j.ScheduledStart).ToListAsync();
    }

    public async Task<Job?> GetJobByIdAsync(int id)
    {
        return await _db.Jobs
            .Include(j => j.Part)
            .Include(j => j.Stages)
                .ThenInclude(s => s.ProductionStage)
            .Include(j => j.JobNotes)
            .Include(j => j.OperatorUser)
            .Include(j => j.WorkOrderLine)
                .ThenInclude(wl => wl!.WorkOrder)
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<Job> CreateJobAsync(Job job)
    {
        // Hydrate from Part if available
        if (job.PartId > 0)
        {
            var part = await _db.Parts
                .Include(p => p.AdditiveBuildConfig)
                .FirstOrDefaultAsync(p => p.Id == job.PartId)
                ?? throw new InvalidOperationException($"Part with ID {job.PartId} not found.");

            job.PartNumber = part.PartNumber;
            job.SlsMaterial = part.Material;

            // Hydrate stacking duration from AdditiveBuildConfig
            if (job.StackLevel.HasValue && part.AdditiveBuildConfig != null)
            {
                var duration = part.AdditiveBuildConfig.GetStackDuration(job.StackLevel.Value);
                if (duration.HasValue)
                    job.PlannedStackDurationHours = duration.Value;

                var ppb = part.AdditiveBuildConfig.GetPartsPerBuild(job.StackLevel.Value);
                if (ppb.HasValue)
                    job.PartsPerBuild = ppb.Value;
            }
        }

        // Check for overlaps
        if (!string.IsNullOrEmpty(job.MachineId))
        {
            var hasOverlap = await HasOverlapAsync(job.MachineId, job.ScheduledStart, job.ScheduledEnd);
            if (hasOverlap)
                throw new InvalidOperationException("Job overlaps with an existing job on this machine.");
        }

        job.CreatedDate = DateTime.UtcNow;
        job.LastModifiedDate = DateTime.UtcNow;
        job.LastStatusChangeUtc = DateTime.UtcNow;

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        // Generate StageExecution records from part routing
        if (job.PartId > 0)
        {
            var routing = await _db.PartStageRequirements
                .Include(r => r.ProductionStage)
                .Where(r => r.PartId == job.PartId && r.IsActive)
                .OrderBy(r => r.ExecutionOrder)
                .ToListAsync();

            if (routing.Count > 0)
            {
                // Build machine lookup: string MachineId → int Id
                var machineLookup = await _db.Machines
                    .ToDictionaryAsync(m => m.MachineId, m => m.Id);

                int? defaultMachineIntId = null;
                if (!string.IsNullOrEmpty(job.MachineId) && machineLookup.TryGetValue(job.MachineId, out var mid))
                    defaultMachineIntId = mid;

                foreach (var stage in routing)
                {
                    var estHours = stage.EstimatedHours ?? stage.ProductionStage?.DefaultDurationHours ?? 1.0;

                    // Resolve stage-specific machine, fall back to job machine
                    int? machineIntId = defaultMachineIntId;
                    if (!string.IsNullOrEmpty(stage.AssignedMachineId)
                        && machineLookup.TryGetValue(stage.AssignedMachineId, out var smid))
                    {
                        machineIntId = smid;
                    }

                    _db.StageExecutions.Add(new StageExecution
                    {
                        JobId = job.Id,
                        ProductionStageId = stage.ProductionStageId,
                        SortOrder = stage.ExecutionOrder,
                        EstimatedHours = estHours,
                        EstimatedCost = stage.EstimatedCost,
                        MaterialCost = stage.MaterialCost,
                        SetupHours = stage.SetupTimeMinutes.HasValue ? stage.SetupTimeMinutes.Value / 60.0 : null,
                        QualityCheckRequired = stage.ProductionStage?.RequiresQualityCheck ?? true,
                        MachineId = machineIntId,
                        CreatedBy = job.CreatedBy,
                        LastModifiedBy = job.LastModifiedBy
                    });
                }

                await _db.SaveChangesAsync();

                // Auto-schedule: assign optimal machines and non-overlapping time slots
                await _scheduler.AutoScheduleJobAsync(job.Id, job.ScheduledStart);
            }
        }

        return job;
    }

    public async Task<Job> UpdateJobAsync(Job job)
    {
        job.LastModifiedDate = DateTime.UtcNow;
        _db.Jobs.Update(job);
        await _db.SaveChangesAsync();
        return job;
    }

    public async Task DeleteJobAsync(int id)
    {
        var job = await _db.Jobs.FindAsync(id);
        if (job == null) throw new InvalidOperationException("Job not found.");
        job.Status = JobStatus.Cancelled;
        job.LastModifiedDate = DateTime.UtcNow;
        job.LastStatusChangeUtc = DateTime.UtcNow;

        // Cancel outstanding stage executions so they don't remain in the scheduler
        var activeStages = await _db.StageExecutions
            .Where(s => s.JobId == id
                && s.Status != StageExecutionStatus.Completed
                && s.Status != StageExecutionStatus.Skipped
                && s.Status != StageExecutionStatus.Failed)
            .ToListAsync();

        foreach (var stage in activeStages)
        {
            stage.Status = StageExecutionStatus.Skipped;
            stage.Notes = "Auto-skipped: job cancelled";
            stage.CompletedAt = DateTime.UtcNow;
            stage.ActualEndAt = DateTime.UtcNow;
            stage.LastModifiedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<Job> UpdateStatusAsync(int jobId, JobStatus newStatus, string updatedBy)
    {
        var job = await _db.Jobs.FindAsync(jobId);
        if (job == null) throw new InvalidOperationException("Job not found.");

        job.Status = newStatus;
        job.LastStatusChangeUtc = DateTime.UtcNow;
        job.LastModifiedDate = DateTime.UtcNow;
        job.LastModifiedBy = updatedBy;

        if (newStatus == JobStatus.InProgress && !job.ActualStart.HasValue)
            job.ActualStart = DateTime.UtcNow;
        else if (newStatus == JobStatus.Completed && !job.ActualEnd.HasValue)
            job.ActualEnd = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return job;
    }

    public async Task<bool> HasOverlapAsync(string machineId, DateTime start, DateTime end, int? excludeJobId = null)
    {
        var query = _db.Jobs
            .Where(j => j.MachineId == machineId
                && j.Status != JobStatus.Cancelled
                && j.ScheduledStart < end
                && j.ScheduledEnd > start);

        if (excludeJobId.HasValue)
            query = query.Where(j => j.Id != excludeJobId.Value);

        return await query.AnyAsync();
    }

    public async Task<List<Job>> GetJobsForSchedulerAsync(DateTime from, DateTime to)
    {
        return await _db.Jobs
            .Include(j => j.Part)
            .Where(j => j.Status != JobStatus.Cancelled
                && j.ScheduledStart < to
                && j.ScheduledEnd > from)
            .OrderBy(j => j.ScheduledStart)
            .ToListAsync();
    }
}
