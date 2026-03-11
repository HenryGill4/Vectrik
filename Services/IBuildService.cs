using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public interface IBuildService
{
    Task<List<BuildJob>> GetAllBuildJobsAsync(BuildJobStatus? statusFilter = null);
    Task<BuildJob?> GetBuildJobByIdAsync(int buildId);
    Task<BuildJob> CreateBuildJobAsync(BuildJob buildJob);
    Task<BuildJob> StartBuildAsync(int buildId, string operatorName);
    Task<BuildJob> CompleteBuildAsync(int buildId, string? endReason = null);
    Task<BuildJob> FailBuildAsync(int buildId, string endReason);
    Task<BuildJobPart> AddPartToBuildAsync(int buildId, int partId, string partNumber, int quantity);
    Task<DelayLog> LogDelayAsync(int? buildJobId, int? jobId, string reason, int delayMinutes, string loggedBy, string? reasonCode = null, string? notes = null);
}
