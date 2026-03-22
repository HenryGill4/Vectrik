using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services;
using Opcentrix_V3.Tests.Helpers;
using Xunit;

namespace Opcentrix_V3.Tests.Services;

public class SchedulingServiceTests : IDisposable
{
    private readonly TenantDbContext _db;
    private readonly SchedulingService _sut;

    public SchedulingServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new SchedulingService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────

    private async Task<Machine> AddMachineAsync(
        string machineId = "SLS-001",
        string name = "SLS Printer 1",
        int priority = 5)
    {
        var machine = new Machine
        {
            MachineId = machineId,
            Name = name,
            MachineType = "SLS",
            IsActive = true,
            IsAvailableForScheduling = true,
            Priority = priority,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    private async Task<ProductionStage> AddStageAsync(
        string name = "SLS Printing",
        string slug = "sls-printing",
        double durationHours = 4.0,
        string? assignedMachineIds = null,
        bool requiresMachine = false,
        string? defaultMachineId = null)
    {
        var stage = new ProductionStage
        {
            Name = name,
            StageSlug = slug,
            DefaultDurationHours = durationHours,
            AssignedMachineIds = assignedMachineIds,
            RequiresMachineAssignment = requiresMachine,
            DefaultMachineId = defaultMachineId,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();
        return stage;
    }

    private async Task<Part> AddPartAsync(string partNumber = "PN-001")
    {
        var part = new Part
        {
            PartNumber = partNumber,
            Name = "Test Part",
            Material = "Ti-6Al-4V Grade 5",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();
        return part;
    }

    private async Task<Job> AddJobAsync(int partId, DateTime scheduledStart, params StageExecution[] stages)
    {
        var job = new Job
        {
            PartId = partId,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledStart.AddHours(8),
            Quantity = 1,
            Status = JobStatus.Scheduled,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        for (int i = 0; i < stages.Length; i++)
        {
            stages[i].JobId = job.Id;
            stages[i].SortOrder = i + 1;
            _db.StageExecutions.Add(stages[i]);
        }
        await _db.SaveChangesAsync();

        return job;
    }

    private async Task AddShiftAsync(
        string name = "Day Shift",
        TimeSpan? start = null,
        TimeSpan? end = null,
        string days = "Mon,Tue,Wed,Thu,Fri")
    {
        var shift = new OperatingShift
        {
            Name = name,
            StartTime = start ?? new TimeSpan(8, 0, 0),
            EndTime = end ?? new TimeSpan(17, 0, 0),
            DaysOfWeek = days,
            IsActive = true
        };
        _db.OperatingShifts.Add(shift);
        await _db.SaveChangesAsync();
    }

    // ── FindEarliestSlotAsync ─────────────────────────────────

    [Fact]
    public async Task FindEarliestSlotAsync_WhenNoShifts_RunsContinuously()
    {
        var machine = await AddMachineAsync();
        var notBefore = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        var slot = await _sut.FindEarliestSlotAsync(machine.Id, 3.0, notBefore);

        Assert.Equal(notBefore, slot.Start);
        Assert.Equal(notBefore.AddHours(3), slot.End);
        Assert.Equal(machine.Id, slot.MachineId);
    }

    [Fact]
    public async Task FindEarliestSlotAsync_WithShifts_SnapsToShiftStart()
    {
        await AddShiftAsync("Day", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
        var machine = await AddMachineAsync();

        // Request at 6:00 AM Monday — should snap to 8:00 AM
        var monday6am = GetNextDayOfWeek(DayOfWeek.Monday).AddHours(6);
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, 2.0, monday6am);

        Assert.Equal(8, slot.Start.Hour);
        Assert.Equal(0, slot.Start.Minute);
    }

    [Fact]
    public async Task FindEarliestSlotAsync_AvoidsExistingScheduledWork()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync();

        // Schedule an existing block on the machine (10 AM - 2 PM)
        var monday = GetNextDayOfWeek(DayOfWeek.Monday);
        var existing = new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = monday.AddHours(10),
            ScheduledEndAt = monday.AddHours(14),
            Status = StageExecutionStatus.NotStarted
        };
        _db.StageExecutions.Add(existing);
        await _db.SaveChangesAsync();

        // Try to find a 3-hour slot starting at 10 AM (conflicts with existing)
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, 3.0, monday.AddHours(10));

        // Should start after the existing block ends
        Assert.True(slot.Start >= monday.AddHours(14));
    }

    [Fact]
    public async Task FindEarliestSlotAsync_IgnoresCompletedExecutions()
    {
        var machine = await AddMachineAsync();
        var stage = await AddStageAsync();

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);
        var completed = new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = monday.AddHours(10),
            ScheduledEndAt = monday.AddHours(14),
            Status = StageExecutionStatus.Completed
        };
        _db.StageExecutions.Add(completed);
        await _db.SaveChangesAsync();

        var slot = await _sut.FindEarliestSlotAsync(machine.Id, 3.0, monday.AddHours(10));

        // Completed work should not block — slot should start at or near 10 AM
        Assert.Equal(monday.AddHours(10), slot.Start);
    }

    // ── GetCapableMachinesAsync ───────────────────────────────

    [Fact]
    public async Task GetCapableMachinesAsync_WhenStageNotFound_ReturnsEmpty()
    {
        var machines = await _sut.GetCapableMachinesAsync(999);

        Assert.Empty(machines);
    }

    [Fact]
    public async Task GetCapableMachinesAsync_WhenNoAssignment_ReturnsEmpty()
    {
        await AddMachineAsync("SLS-001", "Printer 1", 3);
        await AddMachineAsync("SLS-002", "Printer 2", 5);
        var stage = await AddStageAsync(requiresMachine: false);

        var machines = await _sut.GetCapableMachinesAsync(stage.Id);

        // No machines configured on stage → returns empty (no all-machines fallback)
        Assert.Empty(machines);
    }

    [Fact]
    public async Task GetCapableMachinesAsync_WithSpecificRequirement_ReturnsOnlyAssigned()
    {
        var machine1 = await AddMachineAsync("SLS-001", "Printer 1");
        var machine2 = await AddMachineAsync("SLS-002", "Printer 2");
        var stage = await AddStageAsync();

        var requirement = new PartStageRequirement
        {
            RequiresSpecificMachine = true,
            AssignedMachineId = "SLS-001",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };

        var machines = await _sut.GetCapableMachinesAsync(stage.Id, requirement);

        Assert.Single(machines);
        Assert.Equal("SLS-001", machines[0].MachineId);
    }

    [Fact]
    public async Task GetCapableMachinesAsync_WithPreferredMachines_ReturnsPreferredFirst()
    {
        await AddMachineAsync("SLS-001", "Printer 1");
        await AddMachineAsync("SLS-002", "Printer 2");
        var stage = await AddStageAsync();

        var requirement = new PartStageRequirement
        {
            PreferredMachineIds = "SLS-002",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };

        var machines = await _sut.GetCapableMachinesAsync(stage.Id, requirement);

        Assert.Equal("SLS-002", machines[0].MachineId);
    }

    [Fact]
    public async Task GetCapableMachinesAsync_ExcludesInactiveMachines()
    {
        var activeMachine = await AddMachineAsync("SLS-001", "Active");
        var inactiveMachine = new Machine
        {
            MachineId = "SLS-002",
            Name = "Inactive",
            MachineType = "SLS",
            IsActive = false,
            IsAvailableForScheduling = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Machines.Add(inactiveMachine);
        await _db.SaveChangesAsync();

        // Assign both machines (int PKs) so filtering is meaningful
        var stage = await AddStageAsync(
            assignedMachineIds: $"{activeMachine.Id},{inactiveMachine.Id}",
            requiresMachine: true);

        var machines = await _sut.GetCapableMachinesAsync(stage.Id);

        Assert.Single(machines);
        Assert.Equal("SLS-001", machines[0].MachineId);
    }

    [Fact]
    public async Task GetCapableMachinesAsync_ExcludesUnavailableForScheduling()
    {
        var available = await AddMachineAsync("SLS-001", "Available");
        var unavailable = new Machine
        {
            MachineId = "SLS-002",
            Name = "Unavailable",
            MachineType = "SLS",
            IsActive = true,
            IsAvailableForScheduling = false,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Machines.Add(unavailable);
        await _db.SaveChangesAsync();

        // Assign both machines (int PKs) so filtering is meaningful
        var stage = await AddStageAsync(
            assignedMachineIds: $"{available.Id},{unavailable.Id}",
            requiresMachine: true);

        var machines = await _sut.GetCapableMachinesAsync(stage.Id);

        Assert.Single(machines);
        Assert.Equal("SLS-001", machines[0].MachineId);
    }

    [Fact]
    public async Task GetCapableMachinesAsync_WithDefaultMachine_PutsItFirst()
    {
        await AddMachineAsync("SLS-001", "Machine 1", 3);
        await AddMachineAsync("SLS-002", "Machine 2", 5);
        var stage = await AddStageAsync(defaultMachineId: "SLS-002", requiresMachine: false);

        var machines = await _sut.GetCapableMachinesAsync(stage.Id);

        Assert.Equal("SLS-002", machines[0].MachineId);
    }

    // ── AutoScheduleJobAsync ──────────────────────────────────

    [Fact]
    public async Task AutoScheduleJobAsync_SchedulesAllExecutionsSequentially()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage1 = await AddStageAsync("Stage 1", "stage-1", 2.0, machine.MachineId, true);
        var stage2 = await AddStageAsync("Stage 2", "stage-2", 3.0, machine.MachineId, true);

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);
        var exec1 = new StageExecution { ProductionStageId = stage1.Id, EstimatedHours = 2.0 };
        var exec2 = new StageExecution { ProductionStageId = stage2.Id, EstimatedHours = 3.0 };
        var job = await AddJobAsync(part.Id, monday, exec1, exec2);

        await _sut.AutoScheduleJobAsync(job.Id);

        var executions = await _db.StageExecutions
            .Where(e => e.JobId == job.Id)
            .OrderBy(e => e.SortOrder)
            .ToListAsync();

        Assert.NotNull(executions[0].ScheduledStartAt);
        Assert.NotNull(executions[0].ScheduledEndAt);
        Assert.NotNull(executions[1].ScheduledStartAt);
        Assert.NotNull(executions[1].ScheduledEndAt);

        // Second stage starts at or after first stage ends
        Assert.True(executions[1].ScheduledStartAt >= executions[0].ScheduledEndAt);
    }

    [Fact]
    public async Task AutoScheduleJobAsync_AssignsMachinesToExecutions()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("SLS", "sls", 4.0, machine.Id.ToString(), true);

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 4.0 };
        var job = await AddJobAsync(part.Id, monday, exec);

        await _sut.AutoScheduleJobAsync(job.Id);

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.Equal(machine.Id, scheduled.MachineId);
    }

    [Fact]
    public async Task AutoScheduleJobAsync_UpdatesJobScheduledWindow()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("SLS", "sls", 4.0, machine.MachineId, true);

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 4.0 };
        var job = await AddJobAsync(part.Id, monday, exec);

        await _sut.AutoScheduleJobAsync(job.Id);

        var updatedJob = await _db.Jobs.FindAsync(job.Id);
        Assert.NotNull(updatedJob);
        Assert.True(updatedJob.EstimatedHours > 0);
    }

    [Fact]
    public async Task AutoScheduleJobAsync_WhenJobNotFound_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.AutoScheduleJobAsync(999));
    }

    [Fact]
    public async Task AutoScheduleJobAsync_RespectsPredecessorJob()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("SLS", "sls", 2.0, machine.MachineId, true);

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);

        // Create predecessor job that ends at noon
        var predExec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 2.0 };
        var predJob = await AddJobAsync(part.Id, monday, predExec);
        predJob.ScheduledEnd = monday.AddHours(12);
        await _db.SaveChangesAsync();

        // Create successor job with a 1-hour gap
        var succExec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 2.0 };
        var succJob = await AddJobAsync(part.Id, monday, succExec);
        succJob.PredecessorJobId = predJob.Id;
        succJob.UpstreamGapHours = 1.0;
        await _db.SaveChangesAsync();

        await _sut.AutoScheduleJobAsync(succJob.Id);

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == succJob.Id);
        // Should not start before predecessor end + gap (12:00 + 1hr = 13:00)
        Assert.True(scheduled.ScheduledStartAt >= monday.AddHours(13));
    }

    [Fact]
    public async Task AutoScheduleJobAsync_PicksMachineThatFinishesEarliest()
    {
        var machine1 = await AddMachineAsync("SLS-001", "Printer 1");
        var machine2 = await AddMachineAsync("SLS-002", "Printer 2");
        var part = await AddPartAsync();
        var stage = await AddStageAsync("SLS", "sls", 4.0, $"{machine1.Id},{machine2.Id}", true);

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);

        // Block machine1 with existing work
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine1.Id,
            ScheduledStartAt = monday,
            ScheduledEndAt = monday.AddHours(20),
            Status = StageExecutionStatus.InProgress
        });
        await _db.SaveChangesAsync();

        // Machine2 is free
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 4.0 };
        var job = await AddJobAsync(part.Id, monday, exec);

        await _sut.AutoScheduleJobAsync(job.Id);

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        // Should pick machine2 since machine1 is blocked
        Assert.Equal(machine2.Id, scheduled.MachineId);
    }

    [Fact]
    public async Task AutoScheduleJobAsync_IncludesSetupHoursInDuration()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("SLS", "sls", 4.0, machine.MachineId, true);

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);
        var exec = new StageExecution
        {
            ProductionStageId = stage.Id,
            EstimatedHours = 4.0,
            SetupHours = 1.0
        };
        var job = await AddJobAsync(part.Id, monday, exec);

        await _sut.AutoScheduleJobAsync(job.Id);

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        var duration = (scheduled.ScheduledEndAt!.Value - scheduled.ScheduledStartAt!.Value).TotalHours;
        // Total should be 5 hours (4 run + 1 setup)
        Assert.True(duration >= 5.0 - 0.01);
    }

    // ── AutoScheduleExecutionAsync ────────────────────────────

    [Fact]
    public async Task AutoScheduleExecutionAsync_WhenExecutionNotFound_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.AutoScheduleExecutionAsync(999));
    }

    [Fact]
    public async Task AutoScheduleExecutionAsync_SchedulesSingleExecution()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("SLS", "sls", 4.0, machine.Id.ToString(), true);

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 4.0 };
        var job = await AddJobAsync(part.Id, monday, exec);

        var result = await _sut.AutoScheduleExecutionAsync(exec.Id, monday);

        Assert.NotNull(result.ScheduledStartAt);
        Assert.NotNull(result.ScheduledEndAt);
        Assert.Equal(machine.Id, result.MachineId);
    }

    [Fact]
    public async Task AutoScheduleExecutionAsync_RespectsEarlierStagesInSameJob()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage1 = await AddStageAsync("Stage 1", "stage-1", 2.0, machine.MachineId, true);
        var stage2 = await AddStageAsync("Stage 2", "stage-2", 3.0, machine.MachineId, true);

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);
        var exec1 = new StageExecution
        {
            ProductionStageId = stage1.Id,
            EstimatedHours = 2.0,
            ScheduledStartAt = monday,
            ScheduledEndAt = monday.AddHours(2)
        };
        var exec2 = new StageExecution { ProductionStageId = stage2.Id, EstimatedHours = 3.0 };
        var job = await AddJobAsync(part.Id, monday, exec1, exec2);

        var result = await _sut.AutoScheduleExecutionAsync(exec2.Id, monday);

        // exec2 should not start before exec1 ends
        Assert.True(result.ScheduledStartAt >= monday.AddHours(2));
    }

    // ── AutoScheduleAllAsync ──────────────────────────────────

    [Fact]
    public async Task AutoScheduleAllAsync_SchedulesUnscheduledJobs()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("SLS", "sls", 2.0, machine.MachineId, true);

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);

        var exec1 = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 2.0 };
        await AddJobAsync(part.Id, monday, exec1);

        var exec2 = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 2.0 };
        await AddJobAsync(part.Id, monday, exec2);

        var count = await _sut.AutoScheduleAllAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task AutoScheduleAllAsync_WhenNoUnscheduledWork_ReturnsZero()
    {
        var count = await _sut.AutoScheduleAllAsync();

        Assert.Equal(0, count);
    }

    // ── Shift-aware scheduling ────────────────────────────────

    [Fact]
    public async Task AutoScheduleJobAsync_WithShifts_SpansMultipleDaysWhenNeeded()
    {
        // Single 4-hour shift Mon-Fri (8AM-12PM)
        await AddShiftAsync("Morning", new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0));
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Long Task", "long-task", 10.0, machine.MachineId, true);

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 10.0 };
        var job = await AddJobAsync(part.Id, monday, exec);

        await _sut.AutoScheduleJobAsync(job.Id);

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);

        Assert.NotNull(scheduled.ScheduledStartAt);
        Assert.NotNull(scheduled.ScheduledEndAt);
        // 10 hours at 4hrs/day = should span at least 3 days
        var span = scheduled.ScheduledEndAt!.Value - scheduled.ScheduledStartAt!.Value;
        Assert.True(span.TotalDays >= 2, "Work should span multiple days with limited shift hours");
    }

    // ── Edge cases ────────────────────────────────────────────

    [Fact]
    public async Task AutoScheduleJobAsync_WhenNoCapableMachines_SchedulesWithoutMachineAssignment()
    {
        var part = await AddPartAsync();
        // Stage with no machines configured and no machines in system
        var stage = await AddStageAsync("Manual Stage", "manual", 2.0, requiresMachine: false);

        var monday = GetNextDayOfWeek(DayOfWeek.Monday);
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 2.0 };
        var job = await AddJobAsync(part.Id, monday, exec);

        await _sut.AutoScheduleJobAsync(job.Id);

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.NotNull(scheduled.ScheduledStartAt);
        Assert.NotNull(scheduled.ScheduledEndAt);
        Assert.Null(scheduled.MachineId);
    }

    [Fact]
    public async Task AutoScheduleJobAsync_WhenJobHasNoStages_DoesNotThrow()
    {
        var part = await AddPartAsync();
        var job = await AddJobAsync(part.Id, DateTime.UtcNow);

        // Should not throw — just a no-op
        await _sut.AutoScheduleJobAsync(job.Id);
    }

    // ── Helpers ───────────────────────────────────────────────

    private static DateTime GetNextDayOfWeek(DayOfWeek target)
    {
        var today = DateTime.UtcNow.Date;
        var daysUntilTarget = ((int)target - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilTarget == 0) daysUntilTarget = 7; // always get next week's occurrence
        return today.AddDays(daysUntilTarget);
    }
}
