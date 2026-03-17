using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class WorkflowEngine : IWorkflowEngine
{
    private readonly TenantDbContext _db;

    public WorkflowEngine(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<WorkflowInstance?> StartAsync(string entityType, int entityId)
    {
        var definition = await _db.WorkflowDefinitions
            .Include(w => w.Steps)
            .Where(w => w.EntityType == entityType && w.IsActive)
            .FirstOrDefaultAsync();

        if (definition == null || definition.Steps.Count == 0)
            return null;

        var firstStep = definition.Steps.OrderBy(s => s.StepOrder).First();

        var instance = new WorkflowInstance
        {
            WorkflowDefinitionId = definition.Id,
            EntityType = entityType,
            EntityId = entityId,
            CurrentStepOrder = firstStep.StepOrder,
            Status = "Pending",
            StartedAt = DateTime.UtcNow
        };

        _db.WorkflowInstances.Add(instance);
        await _db.SaveChangesAsync();

        return instance;
    }

    public async Task<WorkflowInstance?> ApproveAsync(int instanceId, string approvedBy, string? comment = null)
    {
        var instance = await _db.WorkflowInstances
            .Include(i => i.WorkflowDefinition)
                .ThenInclude(d => d.Steps)
            .FirstOrDefaultAsync(i => i.Id == instanceId);

        if (instance == null || instance.Status != "Pending")
            return instance;

        var steps = instance.WorkflowDefinition.Steps.OrderBy(s => s.StepOrder).ToList();
        var currentIndex = steps.FindIndex(s => s.StepOrder == instance.CurrentStepOrder);

        instance.LastActionBy = approvedBy;
        instance.LastActionComment = comment;

        if (currentIndex >= steps.Count - 1)
        {
            // Last step — workflow complete
            instance.Status = "Approved";
            instance.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            // Advance to next step
            var nextStep = steps[currentIndex + 1];
            instance.CurrentStepOrder = nextStep.StepOrder;
        }

        await _db.SaveChangesAsync();
        return instance;
    }

    public async Task<WorkflowInstance?> RejectAsync(int instanceId, string rejectedBy, string? comment = null)
    {
        var instance = await _db.WorkflowInstances
            .FirstOrDefaultAsync(i => i.Id == instanceId);

        if (instance == null || instance.Status != "Pending")
            return instance;

        instance.Status = "Rejected";
        instance.CompletedAt = DateTime.UtcNow;
        instance.LastActionBy = rejectedBy;
        instance.LastActionComment = comment;

        await _db.SaveChangesAsync();
        return instance;
    }

    public async Task<WorkflowInstance?> GetPendingAsync(string entityType, int entityId)
    {
        return await _db.WorkflowInstances
            .Include(i => i.WorkflowDefinition)
                .ThenInclude(d => d.Steps)
            .Where(i => i.EntityType == entityType && i.EntityId == entityId && i.Status == "Pending")
            .FirstOrDefaultAsync();
    }

    public async Task<List<WorkflowInstance>> GetPendingForRoleAsync(string role)
    {
        return await _db.WorkflowInstances
            .Include(i => i.WorkflowDefinition)
                .ThenInclude(d => d.Steps)
            .Where(i => i.Status == "Pending"
                && i.WorkflowDefinition.Steps.Any(s =>
                    s.StepOrder == i.CurrentStepOrder && s.AssignToRole == role))
            .ToListAsync();
    }

    public async Task<List<WorkflowDefinition>> GetDefinitionsAsync(string entityType)
    {
        return await _db.WorkflowDefinitions
            .Include(w => w.Steps.OrderBy(s => s.StepOrder))
            .Where(w => w.EntityType == entityType)
            .OrderBy(w => w.Name)
            .ToListAsync();
    }
}
