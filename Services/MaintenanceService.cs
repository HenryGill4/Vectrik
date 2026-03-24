using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Models.Maintenance;

namespace Opcentrix_V3.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly TenantDbContext _db;

    public MaintenanceService(TenantDbContext db)
    {
        _db = db;
    }

    // Components
    public async Task<List<MachineComponent>> GetComponentsByMachineAsync(string machineId)
    {
        return await _db.MachineComponents
            .Include(c => c.MaintenanceRules)
            .Where(c => c.MachineId == machineId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<MachineComponent> CreateComponentAsync(MachineComponent component)
    {
        component.CreatedDate = DateTime.UtcNow;
        component.LastModifiedDate = DateTime.UtcNow;
        _db.MachineComponents.Add(component);
        await _db.SaveChangesAsync();
        return component;
    }

    public async Task<MachineComponent> UpdateComponentAsync(MachineComponent component)
    {
        component.LastModifiedDate = DateTime.UtcNow;
        _db.MachineComponents.Update(component);
        await _db.SaveChangesAsync();
        return component;
    }

    public async Task DeleteComponentAsync(int componentId)
    {
        var component = await _db.MachineComponents.FindAsync(componentId);
        if (component == null) throw new InvalidOperationException("Component not found.");
        component.IsActive = false;
        component.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // Rules
    public async Task<List<MaintenanceRule>> GetRulesForComponentAsync(int componentId)
    {
        return await _db.MaintenanceRules
            .Where(r => r.MachineComponentId == componentId && r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<MaintenanceRule> CreateRuleAsync(MaintenanceRule rule)
    {
        rule.CreatedDate = DateTime.UtcNow;
        rule.LastModifiedDate = DateTime.UtcNow;
        _db.MaintenanceRules.Add(rule);
        await _db.SaveChangesAsync();
        return rule;
    }

    public async Task<MaintenanceRule> UpdateRuleAsync(MaintenanceRule rule)
    {
        rule.LastModifiedDate = DateTime.UtcNow;
        _db.MaintenanceRules.Update(rule);
        await _db.SaveChangesAsync();
        return rule;
    }

    public async Task DeleteRuleAsync(int ruleId)
    {
        var rule = await _db.MaintenanceRules.FindAsync(ruleId);
        if (rule == null) throw new InvalidOperationException("Maintenance rule not found.");
        rule.IsActive = false;
        rule.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // Work Orders
    public async Task<List<MaintenanceWorkOrder>> GetWorkOrdersAsync(MaintenanceWorkOrderStatus? statusFilter = null)
    {
        var query = _db.MaintenanceWorkOrders
            .Include(w => w.Machine)
            .Include(w => w.MachineComponent)
            .Include(w => w.AssignedTechnician)
            .AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(w => w.Status == statusFilter.Value);

        return await query.OrderByDescending(w => w.CreatedDate).ToListAsync();
    }

    public async Task<MaintenanceWorkOrder?> GetWorkOrderByIdAsync(int id)
    {
        return await _db.MaintenanceWorkOrders
            .Include(w => w.Machine)
            .Include(w => w.MachineComponent)
            .Include(w => w.MaintenanceRule)
            .Include(w => w.AssignedTechnician)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<MaintenanceWorkOrder> CreateWorkOrderAsync(MaintenanceWorkOrder workOrder)
    {
        workOrder.CreatedDate = DateTime.UtcNow;
        workOrder.LastModifiedDate = DateTime.UtcNow;
        _db.MaintenanceWorkOrders.Add(workOrder);
        await _db.SaveChangesAsync();
        return workOrder;
    }

    public async Task<MaintenanceWorkOrder> UpdateWorkOrderAsync(MaintenanceWorkOrder workOrder)
    {
        workOrder.LastModifiedDate = DateTime.UtcNow;
        _db.MaintenanceWorkOrders.Update(workOrder);
        await _db.SaveChangesAsync();
        return workOrder;
    }

    public async Task<MaintenanceWorkOrder> UpdateStatusAsync(int workOrderId, MaintenanceWorkOrderStatus newStatus, string updatedBy)
    {
        var wo = await _db.MaintenanceWorkOrders.FindAsync(workOrderId);
        if (wo == null) throw new InvalidOperationException("Maintenance work order not found.");

        wo.Status = newStatus;
        wo.LastModifiedDate = DateTime.UtcNow;
        wo.LastModifiedBy = updatedBy;

        if (newStatus == MaintenanceWorkOrderStatus.InProgress && !wo.StartedDate.HasValue)
            wo.StartedDate = DateTime.UtcNow;
        else if (newStatus == MaintenanceWorkOrderStatus.Completed && !wo.CompletedDate.HasValue)
            wo.CompletedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return wo;
    }

    // Evaluation
    public async Task<List<MaintenanceAlert>> EvaluateMaintenanceRulesAsync()
    {
        var alerts = new List<MaintenanceAlert>();

        var components = await _db.MachineComponents
            .Include(c => c.Machine)
            .Include(c => c.MaintenanceRules)
            .Where(c => c.IsActive)
            .ToListAsync();

        foreach (var component in components)
        {
            foreach (var rule in component.MaintenanceRules.Where(r => r.IsActive))
            {
                double currentValue = rule.TriggerType switch
                {
                    MaintenanceTriggerType.HoursRun => component.CurrentHours ?? 0,
                    MaintenanceTriggerType.BuildsCompleted => component.CurrentBuilds ?? 0,
                    _ => 0
                };

                var percentUsed = rule.ThresholdValue > 0
                    ? (currentValue / rule.ThresholdValue) * 100
                    : 0;

                if (percentUsed >= rule.EarlyWarningPercent)
                {
                    alerts.Add(new MaintenanceAlert
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        MachineName = component.Machine.Name,
                        MachineId = component.MachineId,
                        ComponentName = component.Name,
                        Severity = rule.Severity,
                        PercentUsed = percentUsed,
                        IsOverdue = percentUsed >= 100
                    });
                }
            }
        }

        return alerts.OrderByDescending(a => a.PercentUsed).ToList();
    }

    public async Task LogMaintenanceActionAsync(MaintenanceActionLog log)
    {
        log.PerformedAt = DateTime.UtcNow;
        _db.MaintenanceActionLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    public async Task<List<MaintenanceWorkOrder>> GetBlockingWorkOrdersAsync(string machineId, DateTime from, DateTime to)
    {
        return await _db.MaintenanceWorkOrders
            .Where(w => w.MachineId == machineId
                && w.RequiresShutdown
                && w.Status != MaintenanceWorkOrderStatus.Completed
                && w.Status != MaintenanceWorkOrderStatus.Cancelled
                && w.ScheduledDate.HasValue
                && w.ScheduledDate.Value < to
                && w.ScheduledDate.Value >= from)
            .ToListAsync();
    }
}
