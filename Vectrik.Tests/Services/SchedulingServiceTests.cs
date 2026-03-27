using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services;
using Vectrik.Tests.Helpers;
using Xunit;

namespace Vectrik.Tests.Services;

public class SchedulingServiceTests : IDisposable
{
    private readonly TenantDbContext _db;
    private readonly SchedulingService _sut;

    public SchedulingServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new SchedulingService(_db, new StubMachineProgramService(), new ShiftManagementService(_db));
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

    // ── Block Placement — Exact Time Assertions (24/7 mode) ──

    // Fixed Monday for deterministic tests. No shifts = 24/7 continuous operation.
    private static readonly DateTime Mon = new(2025, 7, 7, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BlockPlacement_SingleStage_24x7_ExactStartAndEnd()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 4.0, machine.Id.ToString(), true);

        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 4.0 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(8), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(8));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.Equal(Mon.AddHours(8), scheduled.ScheduledStartAt);
        Assert.Equal(Mon.AddHours(12), scheduled.ScheduledEndAt);
        Assert.Equal(machine.Id, scheduled.MachineId);
    }

    [Fact]
    public async Task BlockPlacement_ThreeSequentialStages_EachStartsWhereLastEnds()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var s1 = await AddStageAsync("Stage A", "a", 2.0, machine.Id.ToString(), true);
        var s2 = await AddStageAsync("Stage B", "b", 3.0, machine.Id.ToString(), true);
        var s3 = await AddStageAsync("Stage C", "c", 1.5, machine.Id.ToString(), true);

        var e1 = new StageExecution { ProductionStageId = s1.Id, EstimatedHours = 2.0 };
        var e2 = new StageExecution { ProductionStageId = s2.Id, EstimatedHours = 3.0 };
        var e3 = new StageExecution { ProductionStageId = s3.Id, EstimatedHours = 1.5 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(6), e1, e2, e3);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(6));

        var execs = await _db.StageExecutions
            .Where(e => e.JobId == job.Id)
            .OrderBy(e => e.SortOrder)
            .ToListAsync();

        // Stage A: 06:00 – 08:00
        Assert.Equal(Mon.AddHours(6), execs[0].ScheduledStartAt);
        Assert.Equal(Mon.AddHours(8), execs[0].ScheduledEndAt);

        // Stage B: 08:00 – 11:00
        Assert.Equal(Mon.AddHours(8), execs[1].ScheduledStartAt);
        Assert.Equal(Mon.AddHours(11), execs[1].ScheduledEndAt);

        // Stage C: 11:00 – 12:30
        Assert.Equal(Mon.AddHours(11), execs[2].ScheduledStartAt);
        Assert.Equal(Mon.AddHours(12.5), execs[2].ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_SetupHoursIncludedInBlock_ExactDuration()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 4.0, machine.Id.ToString(), true);

        var exec = new StageExecution
        {
            ProductionStageId = stage.Id,
            EstimatedHours = 4.0,
            SetupHours = 1.5
        };
        var job = await AddJobAsync(part.Id, Mon.AddHours(10), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(10));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        // Block = 4.0 run + 1.5 setup = 5.5 hours total
        Assert.Equal(Mon.AddHours(10), scheduled.ScheduledStartAt);
        Assert.Equal(Mon.AddHours(15.5), scheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_ExistingBlock_NewJobScheduledAfterIt()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 3.0, machine.Id.ToString(), true);

        // Pre-existing block: Mon 08:00 – 14:00
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = Mon.AddHours(8),
            ScheduledEndAt = Mon.AddHours(14),
            Status = StageExecutionStatus.InProgress
        });
        await _db.SaveChangesAsync();

        // New job wants 3h starting at Mon 08:00 — must land after existing block
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 3.0 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(8), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(8));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.Equal(Mon.AddHours(14), scheduled.ScheduledStartAt);
        Assert.Equal(Mon.AddHours(17), scheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_FitsInGapBetweenExistingBlocks()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 2.0, machine.Id.ToString(), true);

        // Block A: Mon 06:00 – 10:00
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = Mon.AddHours(6),
            ScheduledEndAt = Mon.AddHours(10),
            Status = StageExecutionStatus.NotStarted
        });

        // Block B: Mon 14:00 – 18:00 (leaves a 4h gap from 10:00-14:00)
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = Mon.AddHours(14),
            ScheduledEndAt = Mon.AddHours(18),
            Status = StageExecutionStatus.NotStarted
        });
        await _db.SaveChangesAsync();

        // New 2h job starting at Mon 06:00 — should fit in the gap at 10:00
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 2.0 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(6), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(6));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.Equal(Mon.AddHours(10), scheduled.ScheduledStartAt);
        Assert.Equal(Mon.AddHours(12), scheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_NoOverlap_ThreeJobsSameMachine()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 3.0, machine.Id.ToString(), true);

        // Schedule 3 jobs one by one on the same machine, all wanting Mon 08:00
        var jobs = new List<Job>();
        for (int i = 0; i < 3; i++)
        {
            var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 3.0 };
            var job = await AddJobAsync(part.Id, Mon.AddHours(8), exec);
            await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(8));
            jobs.Add(job);
        }

        var allExecs = await _db.StageExecutions
            .Where(e => jobs.Select(j => j.Id).Contains(e.JobId!.Value))
            .OrderBy(e => e.ScheduledStartAt)
            .ToListAsync();

        Assert.Equal(3, allExecs.Count);

        // Each block: start, end, no overlap
        Assert.Equal(Mon.AddHours(8), allExecs[0].ScheduledStartAt);
        Assert.Equal(Mon.AddHours(11), allExecs[0].ScheduledEndAt);

        Assert.Equal(Mon.AddHours(11), allExecs[1].ScheduledStartAt);
        Assert.Equal(Mon.AddHours(14), allExecs[1].ScheduledEndAt);

        Assert.Equal(Mon.AddHours(14), allExecs[2].ScheduledStartAt);
        Assert.Equal(Mon.AddHours(17), allExecs[2].ScheduledEndAt);

        // Verify no pair overlaps
        for (int i = 0; i < allExecs.Count - 1; i++)
        {
            Assert.True(allExecs[i].ScheduledEndAt <= allExecs[i + 1].ScheduledStartAt,
                $"Block {i} end ({allExecs[i].ScheduledEndAt}) overlaps block {i + 1} start ({allExecs[i + 1].ScheduledStartAt})");
        }
    }

    [Fact]
    public async Task BlockPlacement_PredecessorGap_ExactHours()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 2.0, machine.Id.ToString(), true);

        // Predecessor: scheduled Mon 08:00 – 12:00
        var predExec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 4.0 };
        var predJob = await AddJobAsync(part.Id, Mon.AddHours(8), predExec);
        await _sut.AutoScheduleJobAsync(predJob.Id, startAfter: Mon.AddHours(8));

        var predScheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == predJob.Id);
        Assert.Equal(Mon.AddHours(8), predScheduled.ScheduledStartAt);
        Assert.Equal(Mon.AddHours(12), predScheduled.ScheduledEndAt);

        // Refresh predJob to get updated ScheduledEnd
        await _db.Entry(predJob).ReloadAsync();

        // Successor with 2h gap after predecessor
        var succExec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 2.0 };
        var succJob = await AddJobAsync(part.Id, Mon.AddHours(8), succExec);
        succJob.PredecessorJobId = predJob.Id;
        succJob.UpstreamGapHours = 2.0;
        await _db.SaveChangesAsync();

        await _sut.AutoScheduleJobAsync(succJob.Id);

        var succScheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == succJob.Id);
        // Predecessor ends at 12:00, +2h gap = 14:00 start
        Assert.Equal(Mon.AddHours(14), succScheduled.ScheduledStartAt);
        Assert.Equal(Mon.AddHours(16), succScheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_OptimalMachineSelection_PicksLeastLoaded()
    {
        var m1 = await AddMachineAsync("SLS-001", "Printer 1");
        var m2 = await AddMachineAsync("SLS-002", "Printer 2");
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 4.0, $"{m1.Id},{m2.Id}", true);

        // m1 is loaded until Mon 20:00, m2 is loaded until Mon 12:00
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = m1.Id,
            ScheduledStartAt = Mon.AddHours(8),
            ScheduledEndAt = Mon.AddHours(20),
            Status = StageExecutionStatus.NotStarted
        });
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = m2.Id,
            ScheduledStartAt = Mon.AddHours(8),
            ScheduledEndAt = Mon.AddHours(12),
            Status = StageExecutionStatus.NotStarted
        });
        await _db.SaveChangesAsync();

        // New 4h job starting Mon 08:00 — m2 finishes earliest (12:00+4=16:00 vs 20:00+4=24:00)
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 4.0 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(8), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(8));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.Equal(m2.Id, scheduled.MachineId);
        Assert.Equal(Mon.AddHours(12), scheduled.ScheduledStartAt);
        Assert.Equal(Mon.AddHours(16), scheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_JobWindowUpdated_MatchesFirstAndLastBlock()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var s1 = await AddStageAsync("Stage A", "a", 2.0, machine.Id.ToString(), true);
        var s2 = await AddStageAsync("Stage B", "b", 3.0, machine.Id.ToString(), true);

        var e1 = new StageExecution { ProductionStageId = s1.Id, EstimatedHours = 2.0 };
        var e2 = new StageExecution { ProductionStageId = s2.Id, EstimatedHours = 3.0 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(9), e1, e2);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(9));

        var updatedJob = await _db.Jobs.FindAsync(job.Id);
        Assert.NotNull(updatedJob);
        Assert.Equal(Mon.AddHours(9), updatedJob.ScheduledStart);
        Assert.Equal(Mon.AddHours(14), updatedJob.ScheduledEnd);
        Assert.Equal(5.0, updatedJob.EstimatedHours, precision: 1);
    }

    [Fact]
    public async Task BlockPlacement_DefaultDurationUsedWhenEstimatedHoursNull()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        // Stage default is 4.0 hours
        var stage = await AddStageAsync("Print", "print", 4.0, machine.Id.ToString(), true);

        // Execution has no EstimatedHours — should fall back to stage DefaultDurationHours
        var exec = new StageExecution { ProductionStageId = stage.Id };
        var job = await AddJobAsync(part.Id, Mon.AddHours(10), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(10));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.Equal(Mon.AddHours(10), scheduled.ScheduledStartAt);
        Assert.Equal(Mon.AddHours(14), scheduled.ScheduledEndAt);
    }

    // ── Block Placement — Shift-Aware Tests ─────────────────────

    [Fact]
    public async Task BlockPlacement_ShiftBoundary_SnapsToShiftStart()
    {
        // Day shift: 08:00 - 17:00, Mon-Fri
        await AddShiftAsync("Day", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 3.0, machine.Id.ToString(), true);

        // Request at Mon 05:00 (before shift) — should snap to 08:00
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 3.0 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(5), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(5));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.Equal(Mon.AddHours(8), scheduled.ScheduledStartAt);
        Assert.Equal(Mon.AddHours(11), scheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_MidShiftStart_UsesExactRequestTime()
    {
        await AddShiftAsync("Day", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 2.0, machine.Id.ToString(), true);

        // Request at Mon 10:30 (mid-shift) — should start at 10:30
        var tenThirty = Mon.AddHours(10).AddMinutes(30);
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 2.0 };
        var job = await AddJobAsync(part.Id, tenThirty, exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: tenThirty);

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.Equal(tenThirty, scheduled.ScheduledStartAt);
        Assert.Equal(tenThirty.AddHours(2), scheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_WorkSpansShiftBoundary_WrapsToNextDay()
    {
        // 8-hour day shift Mon-Fri
        await AddShiftAsync("Day", new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0));
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 12.0, machine.Id.ToString(), true);

        // 12h task starting Mon 08:00 — 8h on Mon, 4h on Tue
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 12.0 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(8), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(8));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.Equal(Mon.AddHours(8), scheduled.ScheduledStartAt);
        // 8h Mon (08:00-16:00) + 4h Tue (08:00-12:00) = Tue 12:00
        var tue = Mon.AddDays(1);
        Assert.Equal(tue.AddHours(12), scheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_FridayAfternoon_WrapsToMonday()
    {
        // 8-hour day shift Mon-Fri
        await AddShiftAsync("Day", new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0));
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 6.0, machine.Id.ToString(), true);

        // Friday = Mon + 4 days (2025-07-11). Start at 14:00, only 2h left in shift.
        // 6h task: 2h Fri (14:00-16:00) + 4h Mon (08:00-12:00) = next Mon 12:00
        var fri = Mon.AddDays(4);
        var nextMon = Mon.AddDays(7);

        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 6.0 };
        var job = await AddJobAsync(part.Id, fri.AddHours(14), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: fri.AddHours(14));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.Equal(fri.AddHours(14), scheduled.ScheduledStartAt);
        Assert.Equal(nextMon.AddHours(12), scheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_WeekendRequest_SnapsToMondayShift()
    {
        await AddShiftAsync("Day", new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0));
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 3.0, machine.Id.ToString(), true);

        // Saturday = Mon + 5
        var sat = Mon.AddDays(5);
        var nextMon = Mon.AddDays(7);

        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 3.0 };
        var job = await AddJobAsync(part.Id, sat.AddHours(10), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: sat.AddHours(10));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        // Should snap to next Monday 08:00
        Assert.Equal(nextMon.AddHours(8), scheduled.ScheduledStartAt);
        Assert.Equal(nextMon.AddHours(11), scheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_TwoShifts_WorkSpansMorningGap()
    {
        // Morning: 06:00-12:00, Afternoon: 14:00-20:00 (2h lunch gap)
        await AddShiftAsync("Morning", new TimeSpan(6, 0, 0), new TimeSpan(12, 0, 0));
        await AddShiftAsync("Afternoon", new TimeSpan(14, 0, 0), new TimeSpan(20, 0, 0));
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 10.0, machine.Id.ToString(), true);

        // 10h task starting Mon 06:00. Morning has 6h, afternoon has 6h.
        // 6h morning (06:00-12:00) + 4h afternoon (14:00-18:00) = Mon 18:00
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 10.0 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(6), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(6));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        Assert.Equal(Mon.AddHours(6), scheduled.ScheduledStartAt);
        Assert.Equal(Mon.AddHours(18), scheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_ShiftAware_ExistingBlockPushesToNextShiftDay()
    {
        // 8h shift Mon-Fri
        await AddShiftAsync("Day", new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0));
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var stage = await AddStageAsync("Print", "print", 4.0, machine.Id.ToString(), true);

        // Mon fully blocked: 08:00-16:00
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = Mon.AddHours(8),
            ScheduledEndAt = Mon.AddHours(16),
            Status = StageExecutionStatus.NotStarted
        });
        await _db.SaveChangesAsync();

        // New 4h job wanting Mon 08:00 — Mon full, pushes to Tue 08:00
        var exec = new StageExecution { ProductionStageId = stage.Id, EstimatedHours = 4.0 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(8), exec);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(8));

        var scheduled = await _db.StageExecutions.FirstAsync(e => e.JobId == job.Id);
        var tue = Mon.AddDays(1);
        Assert.Equal(tue.AddHours(8), scheduled.ScheduledStartAt);
        Assert.Equal(tue.AddHours(12), scheduled.ScheduledEndAt);
    }

    [Fact]
    public async Task BlockPlacement_MultiStage_DifferentMachines_ParallelNotForced()
    {
        var m1 = await AddMachineAsync("CNC-001", "CNC Mill");
        var m2 = await AddMachineAsync("EDM-001", "Wire EDM");
        var part = await AddPartAsync();
        // Stage 1 on CNC, Stage 2 on EDM
        var s1 = await AddStageAsync("CNC", "cnc", 3.0, m1.Id.ToString(), true);
        var s2 = await AddStageAsync("EDM", "edm", 2.0, m2.Id.ToString(), true);

        var e1 = new StageExecution { ProductionStageId = s1.Id, EstimatedHours = 3.0 };
        var e2 = new StageExecution { ProductionStageId = s2.Id, EstimatedHours = 2.0 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(8), e1, e2);

        await _sut.AutoScheduleJobAsync(job.Id, startAfter: Mon.AddHours(8));

        var execs = await _db.StageExecutions
            .Where(e => e.JobId == job.Id)
            .OrderBy(e => e.SortOrder)
            .ToListAsync();

        // Stage 1: CNC 08:00-11:00
        Assert.Equal(Mon.AddHours(8), execs[0].ScheduledStartAt);
        Assert.Equal(Mon.AddHours(11), execs[0].ScheduledEndAt);
        Assert.Equal(m1.Id, execs[0].MachineId);

        // Stage 2: EDM 11:00-13:00 (sequential — starts after stage 1 even though different machine)
        Assert.Equal(Mon.AddHours(11), execs[1].ScheduledStartAt);
        Assert.Equal(Mon.AddHours(13), execs[1].ScheduledEndAt);
        Assert.Equal(m2.Id, execs[1].MachineId);
    }

    [Fact]
    public async Task BlockPlacement_FindSlot_ExactGapFitting_TightFit()
    {
        var machine = await AddMachineAsync();
        var stage = await AddStageAsync();

        // Block A: 08:00-11:00, Block B: 13:00-17:00. Gap = 11:00-13:00 (exactly 2h).
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = Mon.AddHours(8),
            ScheduledEndAt = Mon.AddHours(11),
            Status = StageExecutionStatus.NotStarted
        });
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = Mon.AddHours(13),
            ScheduledEndAt = Mon.AddHours(17),
            Status = StageExecutionStatus.NotStarted
        });
        await _db.SaveChangesAsync();

        // 2h job fits exactly in the gap
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, 2.0, Mon.AddHours(8));
        Assert.Equal(Mon.AddHours(11), slot.Start);
        Assert.Equal(Mon.AddHours(13), slot.End);
    }

    [Fact]
    public async Task BlockPlacement_FindSlot_GapTooSmall_SkipsToAfterLastBlock()
    {
        var machine = await AddMachineAsync();
        var stage = await AddStageAsync();

        // Block A: 08:00-11:00, Block B: 12:00-17:00. Gap = 11:00-12:00 (1h).
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = Mon.AddHours(8),
            ScheduledEndAt = Mon.AddHours(11),
            Status = StageExecutionStatus.NotStarted
        });
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = Mon.AddHours(12),
            ScheduledEndAt = Mon.AddHours(17),
            Status = StageExecutionStatus.NotStarted
        });
        await _db.SaveChangesAsync();

        // 3h job doesn't fit in the 1h gap — must go after block B
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, 3.0, Mon.AddHours(8));
        Assert.Equal(Mon.AddHours(17), slot.Start);
        Assert.Equal(Mon.AddHours(20), slot.End);
    }

    [Fact]
    public async Task BlockPlacement_FindSlot_MultipleGaps_PicksFirst()
    {
        var machine = await AddMachineAsync();
        var stage = await AddStageAsync();

        // Block A: 08:00-10:00, Block B: 14:00-16:00, Block C: 20:00-22:00
        // Gap1: 10:00-14:00 (4h), Gap2: 16:00-20:00 (4h)
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = Mon.AddHours(8),
            ScheduledEndAt = Mon.AddHours(10),
            Status = StageExecutionStatus.NotStarted
        });
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = Mon.AddHours(14),
            ScheduledEndAt = Mon.AddHours(16),
            Status = StageExecutionStatus.NotStarted
        });
        _db.StageExecutions.Add(new StageExecution
        {
            ProductionStageId = stage.Id,
            MachineId = machine.Id,
            ScheduledStartAt = Mon.AddHours(20),
            ScheduledEndAt = Mon.AddHours(22),
            Status = StageExecutionStatus.NotStarted
        });
        await _db.SaveChangesAsync();

        // 3h job — fits in Gap1 (10:00-14:00)
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, 3.0, Mon.AddHours(8));
        Assert.Equal(Mon.AddHours(10), slot.Start);
        Assert.Equal(Mon.AddHours(13), slot.End);
    }

    [Fact]
    public async Task BlockPlacement_AutoScheduleExecution_ExactTimesAfterPredecessor()
    {
        var machine = await AddMachineAsync();
        var part = await AddPartAsync();
        var s1 = await AddStageAsync("Stage 1", "s1", 2.0, machine.Id.ToString(), true);
        var s2 = await AddStageAsync("Stage 2", "s2", 3.0, machine.Id.ToString(), true);

        var e1 = new StageExecution
        {
            ProductionStageId = s1.Id,
            EstimatedHours = 2.0,
            ScheduledStartAt = Mon.AddHours(8),
            ScheduledEndAt = Mon.AddHours(10),
            MachineId = machine.Id
        };
        var e2 = new StageExecution { ProductionStageId = s2.Id, EstimatedHours = 3.0 };
        var job = await AddJobAsync(part.Id, Mon.AddHours(8), e1, e2);

        // Auto-schedule just execution 2
        var result = await _sut.AutoScheduleExecutionAsync(e2.Id, Mon.AddHours(8));

        // e2 must start at or after e1's end (10:00)
        Assert.Equal(Mon.AddHours(10), result.ScheduledStartAt);
        Assert.Equal(Mon.AddHours(13), result.ScheduledEndAt);
        Assert.Equal(machine.Id, result.MachineId);
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
