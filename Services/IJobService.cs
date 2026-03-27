using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public interface IJobService
{
    Task<List<Job>> GetAllJobsAsync(JobStatus? statusFilter = null);
    Task<List<Job>> GetJobsByMachineAsync(int machineId, DateTime? from = null, DateTime? to = null);
    Task<Job?> GetJobByIdAsync(int id);
    Task<Job> CreateJobAsync(Job job);
    Task<Job> UpdateJobAsync(Job job);
    Task DeleteJobAsync(int id);
    Task<Job> UpdateStatusAsync(int jobId, JobStatus newStatus, string updatedBy);
    Task<bool> HasOverlapAsync(int? machineId, DateTime start, DateTime end, int? excludeJobId = null);
    Task<List<Job>> GetJobsForSchedulerAsync(DateTime from, DateTime to);
}
