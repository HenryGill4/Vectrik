using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vectrik.Data;
using Vectrik.Hubs;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Models.Maintenance;
using Vectrik.Services.Platform;

namespace Vectrik.Services;

public class MaintenanceDispatchService : IMaintenanceDispatchService
{
    private readonly TenantDbContext _db;
    private readonly ISetupDispatchService _dispatchService;
    private readonly IMaintenanceService _maintenanceService;
    private readonly IDispatchNotifier _notifier;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MaintenanceDispatchService> _logger;

    public MaintenanceDispatchService(
        TenantDbContext db,
        ISetupDispatchService dispatchService,
        IMaintenanceService maintenanceService,
        IDispatchNotifier notifier,
        ITenantContext tenantContext,
        ILogger<MaintenanceDispatchService> logger)
    {
        _db = db;
        _dispatchService = dispatchService;
        _maintenanceService = maintenanceService;
        _notifier = notifier;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<SetupDispatch>> GenerateMaintenanceDispatchesAsync()
    {
        var created = new List<SetupDispatch>();

        // Evaluate all maintenance rules to find alerts
        var alerts = await _maintenanceService.EvaluateMaintenanceRulesAsync();
        if (alerts.Count == 0) return created;

        // Get existing active maintenance dispatches to avoid duplicates
        var existingDispatches = await _dispatchService.GetActiveDispatchesByTypeAsync(DispatchType.Maintenance);

        // Get open/assigned maintenance work orders
        var openWorkOrders = await _maintenanceService.GetWorkOrdersAsync(MaintenanceWorkOrderStatus.Open);
        var assignedWorkOrders = await _maintenanceService.GetWorkOrdersAsync(MaintenanceWorkOrderStatus.Assigned);
        var allPendingWOs = openWorkOrders.Concat(assignedWorkOrders).ToList();

        foreach (var alert in alerts)
        {
            // Only create dispatches for Warning and Critical severity
            if (alert.Severity == MaintenanceSeverity.Info) continue;

            // Parse machine ID (MaintenanceWorkOrder uses string MachineId)
            if (!int.TryParse(alert.MachineId, out var machineId)) continue;

            // Check if we already have a maintenance dispatch for this machine
            var alreadyDispatched = existingDispatches.Any(d =>
                d.MachineId == machineId && d.DispatchType == DispatchType.Maintenance);
            if (alreadyDispatched) continue;

            // Find or create the associated work order
            var existingWO = allPendingWOs.FirstOrDefault(wo =>
                wo.MachineId == alert.MachineId && wo.MaintenanceRuleId == alert.RuleId);

            int? workOrderId = existingWO?.Id;
            if (existingWO == null && alert.IsOverdue)
            {
                // Auto-create work order for overdue maintenance
                var wo = await _maintenanceService.CreateWorkOrderAsync(new MaintenanceWorkOrder
                {
                    MachineId = alert.MachineId,
                    MaintenanceRuleId = alert.RuleId,
                    Type = MaintenanceWorkOrderType.Preventive,
                    Status = MaintenanceWorkOrderStatus.Open,
                    Priority = alert.Severity == MaintenanceSeverity.Critical
                        ? MaintenanceWorkOrderPriority.Critical
                        : MaintenanceWorkOrderPriority.High,
                    Title = $"Auto-generated: {alert.RuleName} — {alert.ComponentName}",
                    Description = $"Maintenance rule triggered at {alert.PercentUsed:F0}% usage. Machine: {alert.MachineName}.",
                    ScheduledDate = DateTime.UtcNow,
                    CreatedBy = "system"
                });
                workOrderId = wo.Id;
            }

            // Calculate priority based on severity and percent used
            var priority = CalculateMaintenancePriority(alert);
            var notes = alert.IsOverdue
                ? $"OVERDUE: {alert.RuleName} on {alert.MachineName} ({alert.ComponentName}) — {alert.PercentUsed:F0}% used"
                : $"Due soon: {alert.RuleName} on {alert.MachineName} ({alert.ComponentName}) — {alert.PercentUsed:F0}% used";

            var dispatch = await _dispatchService.CreateManualDispatchAsync(
                machineId: machineId,
                type: DispatchType.Maintenance,
                notes: notes);

            // Link to maintenance work order
            var entity = await _db.SetupDispatches.FindAsync(dispatch.Id);
            if (entity != null)
            {
                entity.MaintenanceWorkOrderId = workOrderId;
                await _db.SaveChangesAsync();
            }

            await _dispatchService.UpdateDispatchPriorityAsync(dispatch.Id, priority, notes);

            // Update machine state if critical
            if (alert.IsOverdue || alert.Severity == MaintenanceSeverity.Critical)
            {
                var machine = await _db.Machines.FindAsync(machineId);
                if (machine != null)
                {
                    machine.SetupState = MachineSetupState.MaintenanceDue;
                    machine.LastModifiedDate = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }

                await NotifyUrgentAsync(machineId,
                    $"Maintenance required on {alert.MachineName}: {alert.RuleName} ({alert.PercentUsed:F0}% used)");
            }

            created.Add(dispatch);
        }

        return created;
    }

    public async Task<MaintenanceBlockResult> CheckMaintenanceBlockAsync(int machineId)
    {
        var result = new MaintenanceBlockResult();

        // Check maintenance alerts for this machine
        var allAlerts = await _maintenanceService.EvaluateMaintenanceRulesAsync();
        var machineAlerts = allAlerts
            .Where(a => a.MachineId == machineId.ToString())
            .ToList();

        result.ActiveAlerts = machineAlerts;

        // Check for blocking work orders (within next 24 hours)
        var blockingWOs = await _maintenanceService.GetBlockingWorkOrdersAsync(
            machineId.ToString(), DateTime.UtcNow, DateTime.UtcNow.AddHours(24));
        result.BlockingWorkOrders = blockingWOs;

        // Check machine state
        var machine = await _db.Machines.FindAsync(machineId);

        // Blocked if: critical/overdue alerts, or blocking work orders, or machine in maintenance state
        var criticalAlerts = machineAlerts.Where(a =>
            a.Severity == MaintenanceSeverity.Critical || a.IsOverdue).ToList();

        if (criticalAlerts.Count > 0)
        {
            result.IsBlocked = true;
            result.BlockReason = $"Critical maintenance: {string.Join(", ", criticalAlerts.Select(a => a.RuleName))}";
        }
        else if (blockingWOs.Count > 0)
        {
            result.IsBlocked = true;
            result.BlockReason = $"Maintenance work order scheduled: {blockingWOs.First().Title}";
        }
        else if (machine?.SetupState is MachineSetupState.MaintenanceDue or MachineSetupState.MaintenanceInProgress)
        {
            result.IsBlocked = true;
            result.BlockReason = $"Machine in {machine.SetupState} state";
        }

        return result;
    }

    public async Task<List<ToolingWearAlert>> CheckToolingWearAsync(int machineProgramId)
    {
        var alerts = new List<ToolingWearAlert>();

        var toolingItems = await _db.ProgramToolingItems
            .Include(t => t.MachineComponent)
            .Where(t => t.MachineProgramId == machineProgramId && t.IsActive && t.MachineComponentId.HasValue)
            .ToListAsync();

        foreach (var item in toolingItems)
        {
            var wearPercent = item.WearPercent;
            if (!wearPercent.HasValue) continue;

            if (wearPercent.Value >= item.WarningThresholdPercent)
            {
                alerts.Add(new ToolingWearAlert
                {
                    ProgramToolingItemId = item.Id,
                    ToolName = item.Name,
                    ToolPosition = item.ToolPosition,
                    WearPercent = wearPercent.Value,
                    WarningThreshold = item.WarningThresholdPercent,
                    IsCritical = wearPercent.Value >= 100,
                    ComponentName = item.MachineComponent?.Name
                });
            }
        }

        return alerts.OrderByDescending(a => a.WearPercent).ToList();
    }

    public async Task HandleMaintenanceCompletionAsync(int dispatchId)
    {
        var dispatch = await _dispatchService.GetByIdAsync(dispatchId);
        if (dispatch == null) return;

        // Update linked work order status
        if (dispatch.MaintenanceWorkOrderId.HasValue)
        {
            await _maintenanceService.UpdateStatusAsync(
                dispatch.MaintenanceWorkOrderId.Value,
                MaintenanceWorkOrderStatus.Completed,
                "system");
        }

        // Reset machine maintenance state
        var machine = await _db.Machines.FindAsync(dispatch.MachineId);
        if (machine != null)
        {
            machine.SetupState = MachineSetupState.SetUp;
            machine.HoursSinceLastMaintenance = 0;
            machine.LastMaintenanceDate = DateTime.UtcNow;
            machine.NextMaintenanceDate = DateTime.UtcNow.AddHours(machine.MaintenanceIntervalHours);
            machine.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    private static int CalculateMaintenancePriority(MaintenanceAlert alert)
    {
        if (alert.IsOverdue) return 95;

        return alert.Severity switch
        {
            MaintenanceSeverity.Critical => Math.Min(95, 70 + (int)(alert.PercentUsed - 80)),
            MaintenanceSeverity.Warning => Math.Min(80, 50 + (int)(alert.PercentUsed - 60)),
            _ => 40
        };
    }

    private async Task NotifyUrgentAsync(int machineId, string message)
    {
        var tenantCode = _tenantContext.TenantCode;
        if (!string.IsNullOrEmpty(tenantCode))
        {
            try { await _notifier.SendUrgentDispatchAsync(tenantCode, machineId, message); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Urgent maintenance notification failed for machine {MachineId}", machineId);
            }
        }
    }
}
