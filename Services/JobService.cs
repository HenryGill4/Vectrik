using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class JobService : IJobService
{
    private readonly TenantDbContext _db;

    public JobService(TenantDbContext db)
    {
        _db = db;
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
            var part = await _db.Parts.FindAsync(job.PartId);
            if (part != null)
            {
                job.PartNumber = part.PartNumber;
                job.SlsMaterial = part.Material;

                // Hydrate stacking duration
                if (job.StackLevel.HasValue)
                {
                    var duration = part.GetStackDuration(job.StackLevel.Value);
                    if (duration.HasValue)
                        job.PlannedStackDurationHours = duration.Value;

                    var ppb = part.GetPartsPerBuild(job.StackLevel.Value);
                    if (ppb.HasValue)
                        job.PartsPerBuild = ppb.Value;
                }
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
