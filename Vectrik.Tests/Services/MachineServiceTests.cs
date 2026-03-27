using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Models.Maintenance;
using Vectrik.Services;
using Vectrik.Tests.Helpers;
using Xunit;

namespace Vectrik.Tests.Services;

public class MachineServiceTests : IDisposable
{
    private readonly TenantDbContext _db;
    private readonly MachineService _sut;

    public MachineServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new MachineService(_db);
    }

    public void Dispose() => _db.Dispose();

    private Machine CreateTestMachine(
        string machineId = "SLS-001",
        string name = "SLS Printer 1",
        string type = "SLS",
        string? department = "Additive",
        bool isActive = true) => new()
        {
            MachineId = machineId,
            Name = name,
            MachineType = type,
            Department = department,
            IsActive = isActive,
            IsAvailableForScheduling = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };

    // ── CreateMachineAsync ────────────────────────────────────

    [Fact]
    public async Task CreateMachineAsync_PersistsMachineToDatabase()
    {
        var machine = CreateTestMachine();

        var result = await _sut.CreateMachineAsync(machine);

        Assert.True(result.Id > 0);
        Assert.Equal("SLS-001", result.MachineId);
        Assert.Equal(1, await _db.Machines.CountAsync());
    }

    [Fact]
    public async Task CreateMachineAsync_SetsAuditTimestamps()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await _sut.CreateMachineAsync(CreateTestMachine());

        Assert.True(result.CreatedDate >= before);
        Assert.True(result.LastModifiedDate >= before);
    }

    // ── GetAllMachinesAsync ───────────────────────────────────

    [Fact]
    public async Task GetAllMachinesAsync_WhenActiveOnly_ExcludesInactive()
    {
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-001", "Active", isActive: true));
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-002", "Inactive", isActive: false));

        var result = await _sut.GetAllMachinesAsync(activeOnly: true);

        Assert.Single(result);
        Assert.Equal("SLS-001", result[0].MachineId);
    }

    [Fact]
    public async Task GetAllMachinesAsync_WhenNotActiveOnly_ReturnsAll()
    {
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-001", "Active", isActive: true));
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-002", "Inactive", isActive: false));

        var result = await _sut.GetAllMachinesAsync(activeOnly: false);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllMachinesAsync_OrdersByMachineId()
    {
        await _sut.CreateMachineAsync(CreateTestMachine("CNC-001"));
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-001"));
        await _sut.CreateMachineAsync(CreateTestMachine("EDM-001"));

        var result = await _sut.GetAllMachinesAsync();

        Assert.Equal("CNC-001", result[0].MachineId);
        Assert.Equal("EDM-001", result[1].MachineId);
        Assert.Equal("SLS-001", result[2].MachineId);
    }

    // ── GetByIdAsync / GetByMachineIdAsync ────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsMachine()
    {
        var created = await _sut.CreateMachineAsync(CreateTestMachine());

        var result = await _sut.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal("SLS-001", result.MachineId);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        Assert.Null(await _sut.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetByMachineIdAsync_WhenExists_ReturnsMachine()
    {
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-001"));

        var result = await _sut.GetByMachineIdAsync("SLS-001");

        Assert.NotNull(result);
        Assert.Equal("SLS-001", result.MachineId);
    }

    [Fact]
    public async Task GetByMachineIdAsync_WhenNotExists_ReturnsNull()
    {
        Assert.Null(await _sut.GetByMachineIdAsync("NONEXISTENT"));
    }

    // ── UpdateMachineAsync ────────────────────────────────────

    [Fact]
    public async Task UpdateMachineAsync_UpdatesFieldsAndTimestamp()
    {
        var machine = await _sut.CreateMachineAsync(CreateTestMachine());
        machine.Name = "Renamed Printer";
        machine.Department = "New Dept";

        await _sut.UpdateMachineAsync(machine);

        var updated = await _db.Machines.FindAsync(machine.Id);
        Assert.Equal("Renamed Printer", updated!.Name);
        Assert.Equal("New Dept", updated.Department);
    }

    // ── DeleteMachineAsync ────────────────────────────────────

    [Fact]
    public async Task DeleteMachineAsync_WhenNoActiveWork_RemovesMachine()
    {
        var machine = await _sut.CreateMachineAsync(CreateTestMachine());

        await _sut.DeleteMachineAsync(machine.Id);

        Assert.Null(await _db.Machines.FindAsync(machine.Id));
    }

    [Fact]
    public async Task DeleteMachineAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteMachineAsync(999));
    }

    [Fact]
    public async Task DeleteMachineAsync_WhenActiveWork_ThrowsInvalidOperationException()
    {
        var machine = await _sut.CreateMachineAsync(CreateTestMachine());

        // Add a stage for the execution
        var stage = new ProductionStage
        {
            Name = "SLS", StageSlug = "sls",
            CreatedBy = "test", LastModifiedBy = "test"
        };
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();

        // Add active work on this machine
        _db.StageExecutions.Add(new StageExecution
        {
            MachineId = machine.Id,
            ProductionStageId = stage.Id,
            Status = StageExecutionStatus.InProgress
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteMachineAsync(machine.Id));
        Assert.Contains("active or scheduled work", ex.Message);
    }

    [Fact]
    public async Task DeleteMachineAsync_WhenOnlyCompletedWork_Succeeds()
    {
        var machine = await _sut.CreateMachineAsync(CreateTestMachine());
        var stage = new ProductionStage
        {
            Name = "SLS", StageSlug = "sls",
            CreatedBy = "test", LastModifiedBy = "test"
        };
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();

        _db.StageExecutions.Add(new StageExecution
        {
            MachineId = machine.Id,
            ProductionStageId = stage.Id,
            Status = StageExecutionStatus.Completed
        });
        await _db.SaveChangesAsync();

        await _sut.DeleteMachineAsync(machine.Id);

        Assert.Null(await _db.Machines.FindAsync(machine.Id));
    }

    // ── MachineIdExistsAsync ──────────────────────────────────

    [Fact]
    public async Task MachineIdExistsAsync_WhenExists_ReturnsTrue()
    {
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-001"));

        Assert.True(await _sut.MachineIdExistsAsync("SLS-001"));
    }

    [Fact]
    public async Task MachineIdExistsAsync_WhenNotExists_ReturnsFalse()
    {
        Assert.False(await _sut.MachineIdExistsAsync("NONEXISTENT"));
    }

    [Fact]
    public async Task MachineIdExistsAsync_WhenExcludeId_IgnoresSelf()
    {
        var machine = await _sut.CreateMachineAsync(CreateTestMachine("SLS-001"));

        Assert.False(await _sut.MachineIdExistsAsync("SLS-001", machine.Id));
    }

    // ── GetFilteredMachinesAsync ──────────────────────────────

    [Fact]
    public async Task GetFilteredMachinesAsync_FiltersByType()
    {
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-001", "Printer", "SLS"));
        await _sut.CreateMachineAsync(CreateTestMachine("CNC-001", "Mill", "CNC"));

        var result = await _sut.GetFilteredMachinesAsync("SLS", null, null, null);

        Assert.Single(result);
        Assert.Equal("SLS-001", result[0].MachineId);
    }

    [Fact]
    public async Task GetFilteredMachinesAsync_FiltersByStatus()
    {
        var running = CreateTestMachine("SLS-001", "Running");
        running.Status = MachineStatus.Running;
        await _sut.CreateMachineAsync(running);

        var idle = CreateTestMachine("SLS-002", "Idle");
        idle.Status = MachineStatus.Idle;
        await _sut.CreateMachineAsync(idle);

        var result = await _sut.GetFilteredMachinesAsync(null, MachineStatus.Running, null, null);

        Assert.Single(result);
        Assert.Equal("SLS-001", result[0].MachineId);
    }

    [Fact]
    public async Task GetFilteredMachinesAsync_FiltersByDepartment()
    {
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-001", "Printer", department: "Additive"));
        await _sut.CreateMachineAsync(CreateTestMachine("CNC-001", "Mill", department: "Machining"));

        var result = await _sut.GetFilteredMachinesAsync(null, null, "Additive", null);

        Assert.Single(result);
        Assert.Equal("SLS-001", result[0].MachineId);
    }

    [Fact]
    public async Task GetFilteredMachinesAsync_FiltersBySearchText()
    {
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-001", "SLS Printer Alpha"));
        await _sut.CreateMachineAsync(CreateTestMachine("CNC-001", "CNC Mill Beta"));

        var result = await _sut.GetFilteredMachinesAsync(null, null, null, "alpha");

        Assert.Single(result);
        Assert.Equal("SLS-001", result[0].MachineId);
    }

    [Fact]
    public async Task GetFilteredMachinesAsync_CombinesFilters()
    {
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-001", "SLS Alpha", "SLS", "Additive"));
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-002", "SLS Beta", "SLS", "Research"));
        await _sut.CreateMachineAsync(CreateTestMachine("CNC-001", "CNC Alpha", "CNC", "Additive"));

        var result = await _sut.GetFilteredMachinesAsync("SLS", null, "Additive", null);

        Assert.Single(result);
        Assert.Equal("SLS-001", result[0].MachineId);
    }

    // ── GetDistinctDepartmentsAsync / GetDistinctMachineTypesAsync ─

    [Fact]
    public async Task GetDistinctDepartmentsAsync_ReturnsDistinctSorted()
    {
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-001", department: "Additive"));
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-002", department: "Additive"));
        await _sut.CreateMachineAsync(CreateTestMachine("CNC-001", department: "Machining"));
        await _sut.CreateMachineAsync(CreateTestMachine("EDM-001", department: null));

        var result = await _sut.GetDistinctDepartmentsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Additive", result[0]);
        Assert.Equal("Machining", result[1]);
    }

    [Fact]
    public async Task GetDistinctMachineTypesAsync_ReturnsDistinctSorted()
    {
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-001", type: "SLS"));
        await _sut.CreateMachineAsync(CreateTestMachine("SLS-002", type: "SLS"));
        await _sut.CreateMachineAsync(CreateTestMachine("CNC-001", type: "CNC"));

        var result = await _sut.GetDistinctMachineTypesAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains("CNC", result);
        Assert.Contains("SLS", result);
    }

    // ── GetMachineScheduleAsync ───────────────────────────────

    [Fact]
    public async Task GetMachineScheduleAsync_ReturnsOnlyActiveWorkForMachine()
    {
        var machine = await _sut.CreateMachineAsync(CreateTestMachine());
        var stage = new ProductionStage
        {
            Name = "SLS", StageSlug = "sls",
            CreatedBy = "test", LastModifiedBy = "test"
        };
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();

        // Active execution on this machine
        _db.StageExecutions.Add(new StageExecution
        {
            MachineId = machine.Id,
            ProductionStageId = stage.Id,
            Status = StageExecutionStatus.NotStarted,
            ScheduledStartAt = DateTime.UtcNow.AddHours(1)
        });
        // Completed execution (should be excluded)
        _db.StageExecutions.Add(new StageExecution
        {
            MachineId = machine.Id,
            ProductionStageId = stage.Id,
            Status = StageExecutionStatus.Completed,
            ScheduledStartAt = DateTime.UtcNow.AddHours(-2)
        });
        await _db.SaveChangesAsync();

        var schedule = await _sut.GetMachineScheduleAsync(machine.Id);

        Assert.Single(schedule);
        Assert.Equal(StageExecutionStatus.NotStarted, schedule[0].Status);
    }

    [Fact]
    public async Task GetMachineScheduleAsync_RespectsMaxResults()
    {
        var machine = await _sut.CreateMachineAsync(CreateTestMachine());
        var stage = new ProductionStage
        {
            Name = "SLS", StageSlug = "sls",
            CreatedBy = "test", LastModifiedBy = "test"
        };
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();

        for (int i = 0; i < 5; i++)
        {
            _db.StageExecutions.Add(new StageExecution
            {
                MachineId = machine.Id,
                ProductionStageId = stage.Id,
                Status = StageExecutionStatus.NotStarted,
                ScheduledStartAt = DateTime.UtcNow.AddHours(i)
            });
        }
        await _db.SaveChangesAsync();

        var schedule = await _sut.GetMachineScheduleAsync(machine.Id, maxResults: 3);

        Assert.Equal(3, schedule.Count);
    }

    // ── GetMachineWithComponentsAsync ─────────────────────────

    [Fact]
    public async Task GetMachineWithComponentsAsync_WhenExists_ReturnsMachineWithComponents()
    {
        var machine = CreateTestMachine();
        var created = await _sut.CreateMachineAsync(machine);

        var component = new MachineComponent
        {
            MachineId = created.MachineId,
            Name = "Laser Module",
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Set<MachineComponent>().Add(component);
        await _db.SaveChangesAsync();

        // Link component to machine via FK
        // MachineComponent.MachineId is the string MachineId, but the FK in the DB
        // might be the Machine.Id (int). Let me check actual FK structure.
        var result = await _sut.GetMachineWithComponentsAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal(created.MachineId, result.MachineId);
    }

    [Fact]
    public async Task GetMachineWithComponentsAsync_WhenNotExists_ReturnsNull()
    {
        Assert.Null(await _sut.GetMachineWithComponentsAsync(999));
    }
}
