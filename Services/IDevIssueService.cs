using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IDevIssueService
{
    Task<List<DevIssue>> GetAllAsync();
    Task<DevIssue?> GetByIdAsync(int id);
    Task<DevIssue?> GetCurrentAsync();
    Task<DevIssue> CreateAsync(DevIssue issue);
    Task UpdateAsync(DevIssue issue);
    Task StartAsync(int id);
    Task ResolveAsync(int id, string resolution);
    Task VerifyAsync(int id);
    Task WontFixAsync(int id, string reason);
    Task ReopenAsync(int id);
    Task DeleteAsync(int id);
    Task ReorderAsync(int id, int newSortOrder);
}
