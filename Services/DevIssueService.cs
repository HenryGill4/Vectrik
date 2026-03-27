using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;

namespace Vectrik.Services;

public class DevIssueService(TenantDbContext db) : IDevIssueService
{
    public async Task<List<DevIssue>> GetAllAsync()
    {
        return await db.DevIssues
            .OrderBy(i => i.Status == DevIssueStatus.InProgress ? 0 : 1)
            .ThenBy(i => i.Status == DevIssueStatus.Open ? 0 : 1)
            .ThenByDescending(i => i.Priority)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<DevIssue?> GetByIdAsync(int id)
    {
        return await db.DevIssues.FindAsync(id);
    }

    public async Task<DevIssue?> GetCurrentAsync()
    {
        return await db.DevIssues
            .Where(i => i.Status == DevIssueStatus.InProgress)
            .OrderByDescending(i => i.Priority)
            .ThenBy(i => i.StartedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<DevIssue> CreateAsync(DevIssue issue)
    {
        var maxOrder = await db.DevIssues
            .Where(i => i.Status == DevIssueStatus.Open)
            .MaxAsync(i => (int?)i.SortOrder) ?? 0;

        issue.SortOrder = maxOrder + 1;
        issue.CreatedAt = DateTime.UtcNow;
        issue.Status = DevIssueStatus.Open;

        db.DevIssues.Add(issue);
        await db.SaveChangesAsync();
        return issue;
    }

    public async Task UpdateAsync(DevIssue issue)
    {
        db.DevIssues.Update(issue);
        await db.SaveChangesAsync();
    }

    public async Task StartAsync(int id)
    {
        var issue = await db.DevIssues.FindAsync(id);
        if (issue is null) return;

        // Only one issue in progress at a time — pause any others
        var active = await db.DevIssues
            .Where(i => i.Status == DevIssueStatus.InProgress && i.Id != id)
            .ToListAsync();

        foreach (var a in active)
        {
            a.Status = DevIssueStatus.Open;
        }

        issue.Status = DevIssueStatus.InProgress;
        issue.StartedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ResolveAsync(int id, string resolution)
    {
        var issue = await db.DevIssues.FindAsync(id);
        if (issue is null) return;

        issue.Status = DevIssueStatus.Fixed;
        issue.ResolvedAt = DateTime.UtcNow;
        issue.Resolution = resolution;
        await db.SaveChangesAsync();
    }

    public async Task VerifyAsync(int id)
    {
        var issue = await db.DevIssues.FindAsync(id);
        if (issue is null) return;

        issue.Status = DevIssueStatus.Verified;
        await db.SaveChangesAsync();
    }

    public async Task WontFixAsync(int id, string reason)
    {
        var issue = await db.DevIssues.FindAsync(id);
        if (issue is null) return;

        issue.Status = DevIssueStatus.WontFix;
        issue.ResolvedAt = DateTime.UtcNow;
        issue.Resolution = reason;
        await db.SaveChangesAsync();
    }

    public async Task ReopenAsync(int id)
    {
        var issue = await db.DevIssues.FindAsync(id);
        if (issue is null) return;

        issue.Status = DevIssueStatus.Open;
        issue.ResolvedAt = null;
        issue.Resolution = string.Empty;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var issue = await db.DevIssues.FindAsync(id);
        if (issue is null) return;

        db.DevIssues.Remove(issue);
        await db.SaveChangesAsync();
    }

    public async Task ReorderAsync(int id, int newSortOrder)
    {
        var issue = await db.DevIssues.FindAsync(id);
        if (issue is null) return;

        issue.SortOrder = newSortOrder;
        await db.SaveChangesAsync();
    }
}
