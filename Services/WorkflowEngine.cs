using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;

namespace Vectrik.Services;

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

    public async Task<List<WorkflowDefinition>> GetAllDefinitionsAsync()
    {
        return await _db.WorkflowDefinitions
            .Include(w => w.Steps.OrderBy(s => s.StepOrder))
            .OrderBy(w => w.EntityType)
            .ThenBy(w => w.Name)
            .ToListAsync();
    }

    public async Task<bool> HasWorkflowAsync(string entityType)
    {
        return await _db.WorkflowDefinitions
            .AnyAsync(w => w.EntityType == entityType && w.IsActive);
    }

    public async Task<WorkflowDefinition> SaveDefinitionAsync(WorkflowDefinition definition)
    {
        if (definition.Id == 0)
        {
            _db.WorkflowDefinitions.Add(definition);
        }
        else
        {
            var existing = await _db.WorkflowDefinitions
                .Include(w => w.Steps)
                .FirstOrDefaultAsync(w => w.Id == definition.Id);

            if (existing == null)
                throw new InvalidOperationException("Workflow definition not found.");

            existing.Name = definition.Name;
            existing.EntityType = definition.EntityType;
            existing.TriggerEvent = definition.TriggerEvent;
            existing.IsActive = definition.IsActive;
            existing.ConditionsJson = definition.ConditionsJson;

            // Remove old steps and replace
            _db.WorkflowSteps.RemoveRange(existing.Steps);
            foreach (var step in definition.Steps)
            {
                step.Id = 0;
                step.WorkflowDefinitionId = existing.Id;
                existing.Steps.Add(step);
            }
        }

        await _db.SaveChangesAsync();
        return definition;
    }

    public async Task DeleteDefinitionAsync(int definitionId)
    {
        var definition = await _db.WorkflowDefinitions
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == definitionId);

        if (definition == null) return;

        _db.WorkflowSteps.RemoveRange(definition.Steps);
        _db.WorkflowDefinitions.Remove(definition);
        await _db.SaveChangesAsync();
    }
}
