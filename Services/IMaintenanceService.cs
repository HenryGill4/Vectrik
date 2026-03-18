using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Models.Maintenance;

namespace Opcentrix_V3.Services;

public interface IMaintenanceService
{
    // Components
    Task<List<MachineComponent>> GetComponentsByMachineAsync(string machineId);
    Task<MachineComponent> CreateComponentAsync(MachineComponent component);
    Task<MachineComponent> UpdateComponentAsync(MachineComponent component);
    Task DeleteComponentAsync(int componentId);

    // Rules
    Task<List<MaintenanceRule>> GetRulesForComponentAsync(int componentId);
    Task<MaintenanceRule> CreateRuleAsync(MaintenanceRule rule);
    Task<MaintenanceRule> UpdateRuleAsync(MaintenanceRule rule);
    Task DeleteRuleAsync(int ruleId);

    // Work Orders
    Task<List<MaintenanceWorkOrder>> GetWorkOrdersAsync(MaintenanceWorkOrderStatus? statusFilter = null);
    Task<MaintenanceWorkOrder?> GetWorkOrderByIdAsync(int id);
    Task<MaintenanceWorkOrder> CreateWorkOrderAsync(MaintenanceWorkOrder workOrder);
    Task<MaintenanceWorkOrder> UpdateWorkOrderAsync(MaintenanceWorkOrder workOrder);
    Task<MaintenanceWorkOrder> UpdateStatusAsync(int workOrderId, MaintenanceWorkOrderStatus newStatus, string updatedBy);

    // Evaluation
    Task<List<MaintenanceAlert>> EvaluateMaintenanceRulesAsync();
    Task LogMaintenanceActionAsync(MaintenanceActionLog log);

    // Scheduler blocking
    Task<List<MaintenanceWorkOrder>> GetBlockingWorkOrdersAsync(string machineId, DateTime from, DateTime to);
}

public class MaintenanceAlert
{
    public int RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public MaintenanceSeverity Severity { get; set; }
    public double PercentUsed { get; set; }
    public bool IsOverdue { get; set; }
}
