using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Hubs;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Models.Maintenance;
using Vectrik.Services;
using Vectrik.Services.Platform;
using Vectrik.Tests.Helpers;

namespace Vectrik.Tests.Services;

// ══════════════════════════════════════════════════════════════
// Stub MaintenanceService for tests
// ══════════════════════════════════════════════════════════════

internal sealed class StubMaintenanceService : IMaintenanceService
{
    public List<MaintenanceAlert> Alerts { get; set; } = new();
    public List<MaintenanceWorkOrder> WorkOrders { get; set; } = new();
    public List<MaintenanceWorkOrder> BlockingWOs { get; set; } = new();
    public List<MaintenanceWorkOrder> CreatedWorkOrders { get; } = new();
    public List<(int woId, MaintenanceWorkOrderStatus status)> StatusUpdates { get; } = new();

    public Task<List<MachineComponent>> GetComponentsByMachineAsync(string machineId) => Task.FromResult(new List<MachineComponent>());
    public Task<MachineComponent> CreateComponentAsync(MachineComponent component) => Task.FromResult(component);
    public Task<MachineComponent> UpdateComponentAsync(MachineComponent component) => Task.FromResult(component);
    public Task DeleteComponentAsync(int componentId) => Task.CompletedTask;

    public Task<List<MaintenanceRule>> GetRulesForComponentAsync(int componentId) => Task.FromResult(new List<MaintenanceRule>());
    public Task<MaintenanceRule> CreateRuleAsync(MaintenanceRule rule) => Task.FromResult(rule);
    public Task<MaintenanceRule> UpdateRuleAsync(MaintenanceRule rule) => Task.FromResult(rule);
    public Task DeleteRuleAsync(int ruleId) => Task.CompletedTask;

    public Task<List<MaintenanceWorkOrder>> GetWorkOrdersAsync(MaintenanceWorkOrderStatus? statusFilter = null)
    {
        if (statusFilter.HasValue)
            return Task.FromResult(WorkOrders.Where(wo => wo.Status == statusFilter.Value).ToList());
        return Task.FromResult(WorkOrders.ToList());
    }

    public Task<MaintenanceWorkOrder?> GetWorkOrderByIdAsync(int id)
        => Task.FromResult(WorkOrders.FirstOrDefault(wo => wo.Id == id));

    public Task<MaintenanceWorkOrder> CreateWorkOrderAsync(MaintenanceWorkOrder workOrder)
    {
        workOrder.Id = CreatedWorkOrders.Count + 100;
        CreatedWorkOrders.Add(workOrder);
        return Task.FromResult(workOrder);
    }

    public Task<MaintenanceWorkOrder> UpdateWorkOrderAsync(MaintenanceWorkOrder workOrder) => Task.FromResult(workOrder);

    public Task<MaintenanceWorkOrder> UpdateStatusAsync(int workOrderId, MaintenanceWorkOrderStatus newStatus, string updatedBy)
    {
        StatusUpdates.Add((workOrderId, newStatus));
        var wo = WorkOrders.FirstOrDefault(wo => wo.Id == workOrderId);
        if (wo != null) wo.Status = newStatus;
        return Task.FromResult(wo ?? new MaintenanceWorkOrder());
    }

    public Task<List<MaintenanceAlert>> EvaluateMaintenanceRulesAsync() => Task.FromResult(Alerts.ToList());
    public Task LogMaintenanceActionAsync(MaintenanceActionLog log) => Task.CompletedTask;

    public Task<List<MaintenanceWorkOrder>> GetBlockingWorkOrdersAsync(string machineId, DateTime from, DateTime to)
        => Task.FromResult(BlockingWOs.Where(wo => wo.MachineId == machineId).ToList());
}

// ══════════════════════════════════════════════════════════════
// Maintenance Dispatch Service Tests
// ══════════════════════════════════════════════════════════════

public class MaintenanceDispatchServiceTests
{
    private static (TenantDbContext db, ISetupDispatchService svc, StubDispatchNotifier notifier,
        StubMaintenanceService maintenanceSvc, MaintenanceDispatchService maintenanceDispatchSvc) CreateServices()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var maintenanceSvc = new StubMaintenanceService();
        var tenant = new StubTenantContext();
        var maintenanceDispatchSvc = new MaintenanceDispatchService(db, svc, maintenanceSvc, notifier, tenant);
        return (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc);
    }

    [Fact]
    public async Task Generate_CreatesDispatch_ForCriticalAlert()
    {
        var (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc) = CreateServices();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        maintenanceSvc.Alerts.Add(new MaintenanceAlert
        {
            RuleId = 1, RuleName = "Laser tube replacement", MachineName = machine.Name,
            MachineId = machine.Id.ToString(), ComponentName = "Laser tube",
            Severity = MaintenanceSeverity.Critical, PercentUsed = 95, IsOverdue = false
        });

        var dispatches = await maintenanceDispatchSvc.GenerateMaintenanceDispatchesAsync();

        Assert.Single(dispatches);
        Assert.Equal(DispatchType.Maintenance, dispatches[0].DispatchType);
    }

    [Fact]
    public async Task Generate_SkipsInfoSeverity()
    {
        var (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc) = CreateServices();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        maintenanceSvc.Alerts.Add(new MaintenanceAlert
        {
            RuleId = 1, RuleName = "Filter check", MachineName = machine.Name,
            MachineId = machine.Id.ToString(), ComponentName = "Air filter",
            Severity = MaintenanceSeverity.Info, PercentUsed = 60, IsOverdue = false
        });

        var dispatches = await maintenanceDispatchSvc.GenerateMaintenanceDispatchesAsync();

        Assert.Empty(dispatches);
    }

    [Fact]
    public async Task Generate_DoesNotDuplicate_ExistingDispatch()
    {
        var (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc) = CreateServices();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        maintenanceSvc.Alerts.Add(new MaintenanceAlert
        {
            RuleId = 1, RuleName = "Laser check", MachineName = machine.Name,
            MachineId = machine.Id.ToString(), ComponentName = "Laser",
            Severity = MaintenanceSeverity.Warning, PercentUsed = 85, IsOverdue = false
        });

        await maintenanceDispatchSvc.GenerateMaintenanceDispatchesAsync();
        var second = await maintenanceDispatchSvc.GenerateMaintenanceDispatchesAsync();

        Assert.Empty(second);
    }

    [Fact]
    public async Task Generate_AutoCreatesWorkOrder_WhenOverdue()
    {
        var (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc) = CreateServices();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        maintenanceSvc.Alerts.Add(new MaintenanceAlert
        {
            RuleId = 1, RuleName = "Overdue filter", MachineName = machine.Name,
            MachineId = machine.Id.ToString(), ComponentName = "Filter",
            Severity = MaintenanceSeverity.Critical, PercentUsed = 110, IsOverdue = true
        });

        await maintenanceDispatchSvc.GenerateMaintenanceDispatchesAsync();

        Assert.Single(maintenanceSvc.CreatedWorkOrders);
        Assert.Equal(MaintenanceWorkOrderPriority.Critical, maintenanceSvc.CreatedWorkOrders[0].Priority);
    }

    [Fact]
    public async Task Generate_SetsMaintenanceDueState_WhenOverdue()
    {
        var (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc) = CreateServices();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        maintenanceSvc.Alerts.Add(new MaintenanceAlert
        {
            RuleId = 1, RuleName = "Overdue check", MachineName = machine.Name,
            MachineId = machine.Id.ToString(), ComponentName = "Tube",
            Severity = MaintenanceSeverity.Critical, PercentUsed = 105, IsOverdue = true
        });

        await maintenanceDispatchSvc.GenerateMaintenanceDispatchesAsync();

        var updatedMachine = await db.Machines.FindAsync(machine.Id);
        Assert.Equal(MachineSetupState.MaintenanceDue, updatedMachine!.SetupState);
    }

    [Fact]
    public async Task Generate_SendsUrgentNotification_WhenCritical()
    {
        var (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc) = CreateServices();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        maintenanceSvc.Alerts.Add(new MaintenanceAlert
        {
            RuleId = 1, RuleName = "Critical alert", MachineName = machine.Name,
            MachineId = machine.Id.ToString(), ComponentName = "Laser",
            Severity = MaintenanceSeverity.Critical, PercentUsed = 100, IsOverdue = true
        });

        await maintenanceDispatchSvc.GenerateMaintenanceDispatchesAsync();

        Assert.NotEmpty(notifier.UrgentMessages);
    }

    [Fact]
    public async Task CheckBlock_Blocked_WhenCriticalAlert()
    {
        var (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc) = CreateServices();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        maintenanceSvc.Alerts.Add(new MaintenanceAlert
        {
            RuleId = 1, RuleName = "Critical", MachineName = machine.Name,
            MachineId = machine.Id.ToString(), ComponentName = "Laser",
            Severity = MaintenanceSeverity.Critical, PercentUsed = 100, IsOverdue = true
        });

        var result = await maintenanceDispatchSvc.CheckMaintenanceBlockAsync(machine.Id);

        Assert.True(result.IsBlocked);
        Assert.Contains("Critical", result.BlockReason);
    }

    [Fact]
    public async Task CheckBlock_NotBlocked_WhenOnlyInfo()
    {
        var (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc) = CreateServices();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        maintenanceSvc.Alerts.Add(new MaintenanceAlert
        {
            RuleId = 1, RuleName = "Info", MachineName = machine.Name,
            MachineId = machine.Id.ToString(), ComponentName = "Filter",
            Severity = MaintenanceSeverity.Info, PercentUsed = 50, IsOverdue = false
        });

        var result = await maintenanceDispatchSvc.CheckMaintenanceBlockAsync(machine.Id);

        Assert.False(result.IsBlocked);
    }

    [Fact]
    public async Task CheckBlock_Blocked_WhenBlockingWorkOrder()
    {
        var (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc) = CreateServices();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        maintenanceSvc.BlockingWOs.Add(new MaintenanceWorkOrder
        {
            Id = 1, MachineId = machine.Id.ToString(), Title = "Scheduled maintenance",
            RequiresShutdown = true, ScheduledDate = DateTime.UtcNow.AddHours(4)
        });

        var result = await maintenanceDispatchSvc.CheckMaintenanceBlockAsync(machine.Id);

        Assert.True(result.IsBlocked);
        Assert.Contains("Scheduled maintenance", result.BlockReason);
    }

    [Fact]
    public async Task CheckToolingWear_ReturnsCriticalAlerts()
    {
        var db = TestDbContextFactory.Create();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);

        var component = new MachineComponent
        {
            MachineId = machine.Id.ToString(), Name = "End Mill", PartNumber = "EM-001",
            CurrentHours = 90, IsActive = true
        };
        db.MachineComponents.Add(component);
        await db.SaveChangesAsync();

        var tooling = new ProgramToolingItem
        {
            MachineProgramId = program.Id, ToolPosition = "T1", Name = "6mm End Mill",
            MachineComponentId = component.Id, WearLifeHours = 100,
            WarningThresholdPercent = 80, IsActive = true
        };
        db.ProgramToolingItems.Add(tooling);
        await db.SaveChangesAsync();

        var (_, svc2, notifier2) = DispatchTestFixtures.CreateDispatchService();
        var maintenanceSvc = new StubMaintenanceService();
        var tenant = new StubTenantContext();
        var maintenanceDispatchSvc = new MaintenanceDispatchService(db, svc2, maintenanceSvc, notifier2, tenant);

        var alerts = await maintenanceDispatchSvc.CheckToolingWearAsync(program.Id);

        Assert.Single(alerts);
        Assert.Equal(90, alerts[0].WearPercent);
        Assert.Equal("6mm End Mill", alerts[0].ToolName);
    }

    [Fact]
    public async Task HandleCompletion_ResetsMaintenanceState()
    {
        var (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc) = CreateServices();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        machine.SetupState = MachineSetupState.MaintenanceDue;
        machine.HoursSinceLastMaintenance = 500;
        await db.SaveChangesAsync();

        var dispatch = await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Maintenance);
        await svc.StartDispatchAsync(dispatch.Id);
        await svc.CompleteDispatchAsync(dispatch.Id);

        await maintenanceDispatchSvc.HandleMaintenanceCompletionAsync(dispatch.Id);

        var updatedMachine = await db.Machines.FindAsync(machine.Id);
        Assert.Equal(MachineSetupState.SetUp, updatedMachine!.SetupState);
        Assert.Equal(0, updatedMachine.HoursSinceLastMaintenance);
        Assert.NotNull(updatedMachine.LastMaintenanceDate);
    }

    [Fact]
    public async Task HandleCompletion_CompletesLinkedWorkOrder()
    {
        var (db, svc, notifier, maintenanceSvc, maintenanceDispatchSvc) = CreateServices();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        maintenanceSvc.WorkOrders.Add(new MaintenanceWorkOrder
        {
            Id = 42, MachineId = machine.Id.ToString(), Title = "Scheduled",
            Status = MaintenanceWorkOrderStatus.InProgress
        });

        var dispatch = await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Maintenance);
        var entity = await db.SetupDispatches.FindAsync(dispatch.Id);
        entity!.MaintenanceWorkOrderId = 42;
        await db.SaveChangesAsync();

        await svc.StartDispatchAsync(dispatch.Id);
        await svc.CompleteDispatchAsync(dispatch.Id);

        await maintenanceDispatchSvc.HandleMaintenanceCompletionAsync(dispatch.Id);

        Assert.Contains(maintenanceSvc.StatusUpdates, u => u.woId == 42 && u.status == MaintenanceWorkOrderStatus.Completed);
    }
}
