using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class StageService : IStageService
{
    private readonly TenantDbContext _db;

    public StageService(TenantDbContext db)
    {
        _db = db;
    }

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

    public async Task<List<StageExecution>> GetQueueForStageAsync(int stageId)
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.Job)
                .ThenInclude(j => j!.WorkOrderLine)
                    .ThenInclude(wl => wl!.WorkOrder)
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
            .Where(e => e.ProductionStageId == stageId && e.Status == StageExecutionStatus.InProgress)
            .OrderBy(e => e.StartedAt)
            .ToListAsync();
    }

    public async Task<StageExecution> StartStageExecutionAsync(int executionId, int operatorUserId, string operatorName)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.Status = StageExecutionStatus.InProgress;
        execution.StartedAt = DateTime.UtcNow;
        execution.OperatorUserId = operatorUserId;
        execution.OperatorName = operatorName;
        execution.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return execution;
    }

    public async Task<StageExecution> CompleteStageExecutionAsync(int executionId, string? customFieldValues = null, string? notes = null)
    {
        var execution = await _db.StageExecutions
            .Include(e => e.ProductionStage)
            .FirstOrDefaultAsync(e => e.Id == executionId);

        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.Status = StageExecutionStatus.Completed;
        execution.CompletedAt = DateTime.UtcNow;
        execution.LastModifiedDate = DateTime.UtcNow;

        if (execution.StartedAt.HasValue)
            execution.ActualHours = (DateTime.UtcNow - execution.StartedAt.Value).TotalHours;

        if (!string.IsNullOrWhiteSpace(customFieldValues))
            execution.CustomFieldValues = customFieldValues;

        if (!string.IsNullOrWhiteSpace(notes))
            execution.Notes = notes;

        await _db.SaveChangesAsync();
        return execution;
    }

    public async Task<StageExecution> SkipStageExecutionAsync(int executionId, string reason)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.Status = StageExecutionStatus.Skipped;
        execution.Notes = reason;
        execution.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return execution;
    }

    public async Task<StageExecution> FailStageExecutionAsync(int executionId, string reason)
    {
        var execution = await _db.StageExecutions.FindAsync(executionId);
        if (execution == null) throw new InvalidOperationException("Stage execution not found.");

        execution.Status = StageExecutionStatus.Failed;
        execution.Issues = reason;
        execution.CompletedAt = DateTime.UtcNow;
        execution.LastModifiedDate = DateTime.UtcNow;

        if (execution.StartedAt.HasValue)
            execution.ActualHours = (DateTime.UtcNow - execution.StartedAt.Value).TotalHours;

        await _db.SaveChangesAsync();
        return execution;
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
}
