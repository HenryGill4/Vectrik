using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class BuildService : IBuildService
{
    private readonly TenantDbContext _db;

    public BuildService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<BuildJob>> GetAllBuildJobsAsync(BuildJobStatus? statusFilter = null)
    {
        var query = _db.BuildJobs
            .Include(b => b.Parts)
                .ThenInclude(p => p.Part)
            .Include(b => b.User)
            .AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(b => b.Status == statusFilter.Value);

        return await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
    }

    public async Task<BuildJob?> GetBuildJobByIdAsync(int buildId)
    {
        return await _db.BuildJobs
            .Include(b => b.Parts)
                .ThenInclude(p => p.Part)
            .Include(b => b.User)
            .Include(b => b.Delays)
            .Include(b => b.Job)
            .FirstOrDefaultAsync(b => b.BuildId == buildId);
    }

    public async Task<BuildJob> CreateBuildJobAsync(BuildJob buildJob)
    {
        buildJob.CreatedAt = DateTime.UtcNow;
        _db.BuildJobs.Add(buildJob);
        await _db.SaveChangesAsync();
        return buildJob;
    }

    public async Task<BuildJob> StartBuildAsync(int buildId, string operatorName)
    {
        var build = await _db.BuildJobs.FindAsync(buildId);
        if (build == null) throw new InvalidOperationException("Build job not found.");

        build.Status = BuildJobStatus.Preheating;
        build.ActualStartTime = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return build;
    }

    public async Task<BuildJob> CompleteBuildAsync(int buildId, string? endReason = null)
    {
        var build = await _db.BuildJobs.FindAsync(buildId);
        if (build == null) throw new InvalidOperationException("Build job not found.");

        build.Status = BuildJobStatus.Completed;
        build.ActualEndTime = DateTime.UtcNow;
        build.CompletedAt = DateTime.UtcNow;
        build.EndReason = endReason ?? "Completed successfully";
        await _db.SaveChangesAsync();
        return build;
    }

    public async Task<BuildJob> FailBuildAsync(int buildId, string endReason)
    {
        var build = await _db.BuildJobs.FindAsync(buildId);
        if (build == null) throw new InvalidOperationException("Build job not found.");

        build.Status = BuildJobStatus.Failed;
        build.ActualEndTime = DateTime.UtcNow;
        build.CompletedAt = DateTime.UtcNow;
        build.EndReason = endReason;
        await _db.SaveChangesAsync();
        return build;
    }

    public async Task<BuildJobPart> AddPartToBuildAsync(int buildId, int partId, string partNumber, int quantity)
    {
        var part = new BuildJobPart
        {
            BuildJobId = buildId,
            PartId = partId,
            PartNumber = partNumber,
            Quantity = quantity
        };

        _db.BuildJobParts.Add(part);

        // Update total parts count
        var build = await _db.BuildJobs.FindAsync(buildId);
        if (build != null)
        {
            build.TotalPartsInBuild += quantity;
        }

        await _db.SaveChangesAsync();
        return part;
    }

    public async Task<DelayLog> LogDelayAsync(int? buildJobId, int? jobId, string reason, int delayMinutes, string loggedBy, string? reasonCode = null, string? notes = null)
    {
        var delay = new DelayLog
        {
            BuildJobId = buildJobId,
            JobId = jobId,
            Reason = reason,
            ReasonCode = reasonCode,
            DelayMinutes = delayMinutes,
            LoggedBy = loggedBy,
            LoggedAt = DateTime.UtcNow,
            Notes = notes
        };

        _db.DelayLogs.Add(delay);
        await _db.SaveChangesAsync();
        return delay;
    }
}
