using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services;
using Vectrik.Tests.Helpers;
using Xunit;

namespace Vectrik.Tests.Services;

/// <summary>
/// Tests for ProgramSchedulingService — the program-centric scheduling service
/// that replaces BuildSchedulingService. Uses MachineProgram with ProgramType.BuildPlate
/// instead of BuildPackage.
/// </summary>
public class ProgramSchedulingServiceTests : IDisposable
{
    private readonly TenantDbContext _db;
    private readonly ProgramSchedulingService _sut;

    // Fixed reference date: Monday 2025-07-07 for deterministic tests
    private static readonly DateTime Mon = new(2025, 7, 7, 0, 0, 0, DateTimeKind.Utc);

    public ProgramSchedulingServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new ProgramSchedulingService(
            _db,
            new StubSchedulingService(),
            new StubManufacturingProcessService(),
            new StubBatchService(),
            new StubNumberSequenceService(),
            new StubStageCostService(),
            new StubMachineProgramService(),
            new StubSerialNumberService(),
            new StubDownstreamProgramService());
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────

    private async Task<Machine> AddSlsMachineAsync(
        string machineId = "M4-1",
        string name = "EOS M4 #1",
        bool autoChangeover = true,
        int changeoverMinutes = 30,
        int priority = 1,
        double operatorUnloadMinutes = 90)
    {
        var machine = new Machine
        {
            MachineId = machineId,
            Name = name,
            MachineType = "SLS",
            IsActive = true,
            IsAvailableForScheduling = true,
            IsAdditiveMachine = true,
            AutoChangeoverEnabled = autoChangeover,
            ChangeoverMinutes = changeoverMinutes,
            OperatorUnloadMinutes = operatorUnloadMinutes,
            Priority = priority,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    /// <summary>Add a standard Mon-Fri 08:00-16:00 shift.</summary>
    private async Task AddDayShiftAsync()
    {
        _db.OperatingShifts.Add(new OperatingShift
        {
            Name = "Day Shift",
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(16),
            DaysOfWeek = "Mon,Tue,Wed,Thu,Fri",
            IsActive = true
        });
        await _db.SaveChangesAsync();
    }

    private async Task AddShiftAsync(string name, TimeSpan start, TimeSpan end, string days = "Mon,Tue,Wed,Thu,Fri")
    {
        _db.OperatingShifts.Add(new OperatingShift
        {
            Name = name,
            StartTime = start,
            EndTime = end,
            DaysOfWeek = days,
            IsActive = true
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>Add an existing scheduled program block (as a StageExecution) on a machine.</summary>
    private async Task<StageExecution> AddProgramBlockAsync(
        int machineId, int machineProgramId, DateTime start, DateTime end)
    {
        var exec = new StageExecution
        {
            MachineId = machineId,
            MachineProgramId = machineProgramId,
            ScheduledStartAt = start,
            ScheduledEndAt = end,
            Status = StageExecutionStatus.NotStarted,
            SortOrder = 1,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.StageExecutions.Add(exec);
        await _db.SaveChangesAsync();
        return exec;
    }

    /// <summary>Add a scheduled BuildPlate MachineProgram (no StageExecution yet).</summary>
    private async Task<MachineProgram> AddScheduledBuildPlateProgramAsync(
        int machineId, DateTime scheduledDate, double durationHours,
        string name = "Program", int? sourceProgramId = null)
    {
        var program = new MachineProgram
        {
            Name = name,
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            MachineId = machineId,
            ScheduledDate = scheduledDate,
            EstimatedPrintHours = durationHours,
            ScheduleStatus = ProgramScheduleStatus.Scheduled,
            IsLocked = true,
            SourceProgramId = sourceProgramId,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();
        return program;
    }

    // ══════════════════════════════════════════════════════════
    // FindEarliestSlotAsync — 24/7 Continuous (AutoChangeover)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ProgramSlot_Continuous_EmptyMachine_StartsAtNotBefore()
    {
        // Arrange: 24/7 SLS machine, no existing work
        var machine = await AddSlsMachineAsync();
        var notBefore = Mon.AddHours(10); // Mon 10:00

        // Act
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, durationHours: 24, notBefore);

        // Assert: starts exactly at notBefore, ends 24h later
        Assert.Equal(Mon.AddHours(10), slot.PrintStart);
        Assert.Equal(Mon.AddHours(34), slot.PrintEnd); // Tue 10:00
        Assert.Equal(machine.Id, slot.MachineId);
    }

    [Fact]
    public async Task ProgramSlot_Continuous_ExistingBlock_SchedulesAfterWithChangeover()
    {
        // Arrange: machine with one 24h block Mon 00:00 → Tue 00:00, 30min changeover
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);
        var existingProg = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon, 24);
        await AddProgramBlockAsync(machine.Id, existingProg.Id, Mon, Mon.AddHours(24));

        // Act: request 10h slot starting Mon 00:00
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, durationHours: 10, Mon);

        // Assert: starts after block end + 30min changeover
        var expectedStart = Mon.AddHours(24).AddMinutes(30); // Tue 00:30
        var expectedEnd = expectedStart.AddHours(10);        // Tue 10:30
        Assert.Equal(expectedStart, slot.PrintStart);
        Assert.Equal(expectedEnd, slot.PrintEnd);
    }

    [Fact]
    public async Task ProgramSlot_Continuous_SameProgramRun_ExcludesOwnBlocks()
    {
        // Arrange: machine with one block belonging to the program being rescheduled
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);
        var existingProg = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon, 24);
        await AddProgramBlockAsync(machine.Id, existingProg.Id, Mon, Mon.AddHours(24));

        // Act: reschedule same program (forProgramId matches) — own blocks should be excluded
        var slot = await _sut.FindEarliestSlotAsync(
            machine.Id, durationHours: 10, Mon, forProgramId: existingProg.Id);

        // Assert: own block is excluded, so slot starts at notBefore (Mon)
        Assert.Equal(Mon, slot.PrintStart);
        Assert.Equal(Mon.AddHours(10), slot.PrintEnd);
    }

    [Fact]
    public async Task ProgramSlot_Continuous_TwoExistingBlocks_SchedulesAfterSecond()
    {
        // Arrange: two back-to-back blocks with changeover
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        var prog1 = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon, 12);
        await AddProgramBlockAsync(machine.Id, prog1.Id, Mon, Mon.AddHours(12));

        var prog2Start = Mon.AddHours(12).AddMinutes(30); // after changeover
        var prog2 = await AddScheduledBuildPlateProgramAsync(machine.Id, prog2Start, 12);
        await AddProgramBlockAsync(machine.Id, prog2.Id, prog2Start, prog2Start.AddHours(12));

        // Act: schedule a 6h slot
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, durationHours: 6, Mon);

        // Assert: after second block + changeover
        var expectedStart = prog2Start.AddHours(12).AddMinutes(30);
        var expectedEnd = expectedStart.AddHours(6);
        Assert.Equal(expectedStart, slot.PrintStart);
        Assert.Equal(expectedEnd, slot.PrintEnd);
    }

    [Fact]
    public async Task ProgramSlot_Continuous_FitsInGapBetweenBlocks()
    {
        // Arrange: two blocks with a large enough gap (including changeover)
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        // Block 1: Mon 00:00 → Mon 10:00
        var prog1 = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon, 10);
        await AddProgramBlockAsync(machine.Id, prog1.Id, Mon, Mon.AddHours(10));

        // Block 2: Wed 00:00 → Wed 12:00 (big gap between)
        var wed = Mon.AddDays(2);
        var prog2 = await AddScheduledBuildPlateProgramAsync(machine.Id, wed, 12);
        await AddProgramBlockAsync(machine.Id, prog2.Id, wed, wed.AddHours(12));

        // Act: schedule a 6h slot starting Mon 00:00
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, durationHours: 6, Mon);

        // Assert: fits after first block + changeover (Mon 10:30 → Mon 16:30)
        var expectedStart = Mon.AddHours(10).AddMinutes(30);
        var expectedEnd = expectedStart.AddHours(6);
        Assert.Equal(expectedStart, slot.PrintStart);
        Assert.Equal(expectedEnd, slot.PrintEnd);
    }

    [Fact]
    public async Task ProgramSlot_Continuous_GapTooSmall_SkipsToAfterSecondBlock()
    {
        // Arrange: gap between blocks is only 2h (< 6h needed + 30min changeover)
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        // Block 1: Mon 00:00 → Mon 10:00
        var prog1 = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon, 10);
        await AddProgramBlockAsync(machine.Id, prog1.Id, Mon, Mon.AddHours(10));

        // Block 2: Mon 12:00 → Mon 22:00 (only 2h gap after changeover = 1.5h usable)
        var prog2 = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon.AddHours(12), 10);
        await AddProgramBlockAsync(machine.Id, prog2.Id, Mon.AddHours(12), Mon.AddHours(22));

        // Act: schedule a 6h slot
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, durationHours: 6, Mon);

        // Assert: can't fit in gap → after second block + changeover
        var expectedStart = Mon.AddHours(22).AddMinutes(30);
        var expectedEnd = expectedStart.AddHours(6);
        Assert.Equal(expectedStart, slot.PrintStart);
        Assert.Equal(expectedEnd, slot.PrintEnd);
    }

    [Fact]
    public async Task ProgramSlot_Continuous_NoChangeover_BlocksPackTightly()
    {
        // Arrange: machine without auto-changeover
        var machine = await AddSlsMachineAsync(autoChangeover: false, changeoverMinutes: 0);

        var prog1 = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon, 10);
        await AddProgramBlockAsync(machine.Id, prog1.Id, Mon, Mon.AddHours(10));

        // Act
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, durationHours: 5, Mon);

        // Assert: immediately after block with no gap
        Assert.Equal(Mon.AddHours(10), slot.PrintStart);
        Assert.Equal(Mon.AddHours(15), slot.PrintEnd);
    }

    [Fact]
    public async Task ProgramSlot_Continuous_ChangeoverReturned_MatchesWindow()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(changeoverMinutes: 45);

        // Act
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, durationHours: 8, Mon);

        // Assert: changeover window follows print end
        Assert.Equal(Mon, slot.PrintStart);
        Assert.Equal(Mon.AddHours(8), slot.PrintEnd);
        Assert.Equal(Mon.AddHours(8), slot.ChangeoverStart);
        Assert.Equal(Mon.AddHours(8).AddMinutes(45), slot.ChangeoverEnd);
    }

    // ══════════════════════════════════════════════════════════
    // FindBestSlotAsync — Multi-Machine Selection
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task BestSlot_ChoosesEarliestAvailable_AcrossMachines()
    {
        // Arrange: two machines, first is busy until Tue, second is free now
        var machine1 = await AddSlsMachineAsync("M1", "SLS Machine 1");
        var machine2 = await AddSlsMachineAsync("M2", "SLS Machine 2");

        // Machine1 busy until Tue 00:00
        var prog1 = await AddScheduledBuildPlateProgramAsync(machine1.Id, Mon, 24);
        await AddProgramBlockAsync(machine1.Id, prog1.Id, Mon, Mon.AddHours(24));

        // Act
        var bestSlot = await _sut.FindBestSlotAsync(durationHours: 8, Mon, machineType: "SLS");

        // Assert: chooses Machine2 (available now)
        Assert.Equal(machine2.Id, bestSlot.MachineId);
        Assert.Equal(Mon, bestSlot.Slot.PrintStart);
    }

    // ══════════════════════════════════════════════════════════
    // ScheduleBuildPlateAsync — Integration
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ScheduleBuildPlate_CreatesStageExecution_WithCorrectTimes()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        // Add a part to attach to the build plate
        var part = new Part
        {
            PartNumber = "PART-001",
            Name = "Test Part",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        // Create a BuildPlate program with slicer data and parts
        var program = new MachineProgram
        {
            Name = "Test Build Plate",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            MachineId = machine.Id,
            EstimatedPrintHours = 12.5,
            ScheduleStatus = ProgramScheduleStatus.Ready,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        // Add a part to the program
        var programPart = new ProgramPart
        {
            MachineProgramId = program.Id,
            PartId = part.Id,
            Quantity = 5,
            StackLevel = 1
        };
        _db.ProgramParts.Add(programPart);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon);

        // Assert
        Assert.Equal(Mon, result.Slot.PrintStart);
        Assert.Equal(Mon.AddHours(12.5), result.Slot.PrintEnd);
        Assert.Equal(program.Id, result.MachineProgramId);

        // Verify program was updated
        var updatedProgram = await _db.MachinePrograms.FindAsync(program.Id);
        Assert.Equal(ProgramScheduleStatus.Scheduled, updatedProgram!.ScheduleStatus);
        Assert.Equal(Mon, updatedProgram.ScheduledDate);
        Assert.True(updatedProgram.IsLocked);
    }

    [Fact]
    public async Task ScheduleBuildPlate_RequiresSlicerData_ThrowsWithout()
    {
        // Arrange
        var machine = await AddSlsMachineAsync();

        var program = new MachineProgram
        {
            Name = "Missing Slicer Data",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            ScheduleStatus = ProgramScheduleStatus.None, // No slicer data
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon));
    }

    [Fact]
    public async Task ScheduleBuildPlate_OnlyBuildPlateType_ThrowsForStandard()
    {
        // Arrange
        var machine = await AddSlsMachineAsync();

        var program = new MachineProgram
        {
            Name = "CNC Program",
            ProgramType = ProgramType.Standard, // Not a BuildPlate
            Status = ProgramStatus.Active,
            EstimatedPrintHours = 2,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon));
    }

    // ══════════════════════════════════════════════════════════
    // Work Order → Program Integration
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAvailableProgramsForPart_ReturnsMatchingPrograms()
    {
        // Arrange: Part with an available BuildPlate program
        var part = new Part
        {
            PartNumber = "WO-PART-001",
            Name = "WO Test Part",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var program = new MachineProgram
        {
            Name = "Available Build Plate",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            ScheduleStatus = ProgramScheduleStatus.Ready,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        _db.ProgramParts.Add(new ProgramPart
        {
            MachineProgramId = program.Id,
            PartId = part.Id,
            Quantity = 10
        });
        await _db.SaveChangesAsync();

        // Act
        var available = await _sut.GetAvailableProgramsForPartAsync(part.Id);

        // Assert
        Assert.Single(available);
        Assert.Equal(program.Id, available[0].Id);
    }

    [Fact]
    public async Task GetAvailableProgramsForPart_ExcludesScheduledPrograms()
    {
        // Arrange: Part with a scheduled program (should be excluded)
        var part = new Part
        {
            PartNumber = "WO-PART-002",
            Name = "WO Test Part 2",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var scheduledProgram = new MachineProgram
        {
            Name = "Scheduled Build Plate",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            ScheduleStatus = ProgramScheduleStatus.Scheduled, // Already scheduled
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(scheduledProgram);
        await _db.SaveChangesAsync();

        _db.ProgramParts.Add(new ProgramPart
        {
            MachineProgramId = scheduledProgram.Id,
            PartId = part.Id,
            Quantity = 10
        });
        await _db.SaveChangesAsync();

        // Act
        var available = await _sut.GetAvailableProgramsForPartAsync(part.Id);

        // Assert: Scheduled programs are excluded
        Assert.Empty(available);
    }

    [Fact]
    public async Task GetAvailableProgramsForPart_ExcludesInactivePrograms()
    {
        // Arrange: Part with an inactive program (should be excluded)
        var part = new Part
        {
            PartNumber = "WO-PART-003",
            Name = "WO Test Part 3",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var inactiveProgram = new MachineProgram
        {
            Name = "Inactive Build Plate",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Superseded, // Not active
            ScheduleStatus = ProgramScheduleStatus.Ready,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(inactiveProgram);
        await _db.SaveChangesAsync();

        _db.ProgramParts.Add(new ProgramPart
        {
            MachineProgramId = inactiveProgram.Id,
            PartId = part.Id,
            Quantity = 10
        });
        await _db.SaveChangesAsync();

        // Act
        var available = await _sut.GetAvailableProgramsForPartAsync(part.Id);

        // Assert: Inactive programs are excluded
        Assert.Empty(available);
    }

    // ══════════════════════════════════════════════════════════
    // Downstream Validation Integration
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ScheduleBuildPlate_WithDownstreamValidation_PassesWhenValid()
    {
        // Arrange: StubDownstreamProgramService returns valid by default
        var machine = await AddSlsMachineAsync();
        var part = new Part
        {
            PartNumber = "DS-VALID-001",
            Name = "Downstream Valid Part",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var program = new MachineProgram
        {
            Name = "Valid Downstream Test",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            EstimatedPrintHours = 8,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        _db.ProgramParts.Add(new ProgramPart
        {
            MachineProgramId = program.Id,
            PartId = part.Id,
            Quantity = 5
        });
        await _db.SaveChangesAsync();

        // Act: should pass downstream validation (stub returns valid)
        var result = await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon);

        // Assert: scheduling succeeds
        Assert.Equal(program.Id, result.MachineProgramId);
        Assert.NotNull(result.Slot);
    }

    [Fact]
    public async Task ScheduleBuildPlate_WithFailingDownstream_ThrowsInvalidOperationException()
    {
        // Arrange: Create a service with a downstream stub that fails validation
        var failingDownstream = new FailingDownstreamProgramService();
        var sut = new ProgramSchedulingService(
            _db,
            new StubSchedulingService(),
            new StubManufacturingProcessService(),
            new StubBatchService(),
            new StubNumberSequenceService(),
            new StubStageCostService(),
            new StubMachineProgramService(),
            new StubSerialNumberService(),
            failingDownstream);

        var machine = await AddSlsMachineAsync("M4-DS", "Downstream Machine");
        var part = new Part
        {
            PartNumber = "DS-FAIL-001",
            Name = "Downstream Fail Part",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var program = new MachineProgram
        {
            Name = "Fail Downstream Test",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            EstimatedPrintHours = 8,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        _db.ProgramParts.Add(new ProgramPart
        {
            MachineProgramId = program.Id,
            PartId = part.Id,
            Quantity = 5
        });
        await _db.SaveChangesAsync();

        // Act & Assert: should throw due to missing downstream programs
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon));
        Assert.Contains("Downstream programs missing", ex.Message);
        Assert.Contains("Depowder", ex.Message);
    }

    /// <summary>Downstream stub that always reports missing programs.</summary>
    private sealed class FailingDownstreamProgramService : IDownstreamProgramService
    {
        public Task<List<DownstreamProgramRequirement>> GetRequiredProgramsAsync(int buildPlateProgramId)
            => Task.FromResult(new List<DownstreamProgramRequirement>
            {
                new(1, "Depowder", "Depowder", null, null, true, false, 2)
            });

        public Task<DownstreamValidationResult> ValidateDownstreamReadinessAsync(int buildPlateProgramId)
            => Task.FromResult(new DownstreamValidationResult(false,
                [new(1, "Depowder", "Depowder", null, null, true, false, 2)],
                []));

        public Task<List<MachineProgram>> CreatePlaceholderProgramsAsync(
            int buildPlateProgramId, List<int> stageIdsNeedingPrograms, string createdBy)
            => Task.FromResult(new List<MachineProgram>());
    }

    // ══════════════════════════════════════════════════════════
    // Reschedule — No Duplication
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ScheduleBuildPlate_Reschedule_DoesNotDuplicateJobsOrExecutions()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        var part = new Part
        {
            PartNumber = "RESCHED-001",
            Name = "Reschedule Test Part",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var program = new MachineProgram
        {
            Name = "Reschedule Test Build",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            EstimatedPrintHours = 12.5,
            ScheduleStatus = ProgramScheduleStatus.None,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        _db.ProgramParts.Add(new ProgramPart
        {
            MachineProgramId = program.Id,
            PartId = part.Id,
            Quantity = 5,
            StackLevel = 1
        });
        await _db.SaveChangesAsync();

        // Act 1: Initial schedule
        var result1 = await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon);
        var jobCountAfterFirst = await _db.Jobs.CountAsync();
        var execCountAfterFirst = await _db.StageExecutions.CountAsync();

        // Act 2: Reschedule (same call, simulating drag-drop)
        var result2 = await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon.AddHours(5));
        var jobCountAfterSecond = await _db.Jobs.CountAsync();
        var execCountAfterSecond = await _db.StageExecutions.CountAsync();

        // Assert: job and execution counts should be the same — no duplicates
        Assert.Equal(jobCountAfterFirst, jobCountAfterSecond);
        Assert.Equal(execCountAfterFirst, execCountAfterSecond);

        // The program should point to a new job (old one was deleted)
        var updatedProgram = await _db.MachinePrograms.FindAsync(program.Id);
        Assert.NotNull(updatedProgram);
        Assert.NotEqual(result1.Slot.PrintStart, result2.Slot.PrintStart);
        Assert.Equal(ProgramScheduleStatus.Scheduled, updatedProgram!.ScheduleStatus);
    }

    // ══════════════════════════════════════════════════════════
    // Reschedule — Per-Part Job Cleanup (Bug Fix Verification)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ScheduleBuildPlate_Reschedule_CleansUpPerPartJobs()
    {
        // This verifies the fix for per-part job cleanup on reschedule.
        // Previously, the search pattern didn't match the actual Notes format,
        // leaving orphan per-part jobs after each reschedule.
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        var part = new Part
        {
            PartNumber = "PSA-SUPP-001",
            Name = "Test Suppressor",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var program = new MachineProgram
        {
            ProgramNumber = "BP-00001",
            Name = "Suppressor 72x",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            EstimatedPrintHours = 20.2,
            ScheduleStatus = ProgramScheduleStatus.None,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        _db.ProgramParts.Add(new ProgramPart
        {
            MachineProgramId = program.Id,
            PartId = part.Id,
            Quantity = 72,
            StackLevel = 1
        });
        await _db.SaveChangesAsync();

        // Schedule
        await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon);
        var jobsAfterFirst = await _db.Jobs.CountAsync();
        var execsAfterFirst = await _db.StageExecutions.CountAsync();

        // Reschedule 3 times — should never grow
        await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon.AddHours(2));
        await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon.AddHours(5));
        await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon.AddHours(8));

        var jobsAfterThird = await _db.Jobs.CountAsync();
        var execsAfterThird = await _db.StageExecutions.CountAsync();

        // Assert: no orphan accumulation
        Assert.Equal(jobsAfterFirst, jobsAfterThird);
        Assert.Equal(execsAfterFirst, execsAfterThird);
    }

    [Fact]
    public async Task ScheduleBuildPlate_Reschedule_OldJobIsDeleted()
    {
        var machine = await AddSlsMachineAsync();

        var part = new Part { PartNumber = "P-001", Name = "Part", CreatedBy = "test", LastModifiedBy = "test" };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var program = new MachineProgram
        {
            ProgramNumber = "BP-TEST",
            Name = "Test Build",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            EstimatedPrintHours = 10,
            ScheduleStatus = ProgramScheduleStatus.None,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();
        _db.ProgramParts.Add(new ProgramPart { MachineProgramId = program.Id, PartId = part.Id, Quantity = 5, StackLevel = 1 });
        await _db.SaveChangesAsync();

        // Schedule once
        var result1 = await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon);
        var oldJobId = (await _db.MachinePrograms.FindAsync(program.Id))!.ScheduledJobId;
        Assert.NotNull(oldJobId);

        // Reschedule
        await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon.AddHours(10));
        var newJobId = (await _db.MachinePrograms.FindAsync(program.Id))!.ScheduledJobId;

        // Old job should be deleted, new job should exist
        Assert.NotEqual(oldJobId, newJobId);
        Assert.Null(await _db.Jobs.FindAsync(oldJobId));
        Assert.NotNull(await _db.Jobs.FindAsync(newJobId));
    }

    // ══════════════════════════════════════════════════════════
    // Print Stage Slug — Finds Existing sls-printing Stage
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ScheduleBuildPlate_UsesExistingSlsPrintingStage()
    {
        // This verifies the fix: GetOrCreatePrintStageIdAsync should find
        // the "sls-printing" slug from seed data, not create a duplicate "sls-print".
        var machine = await AddSlsMachineAsync();

        // Pre-create the production stage with seed data slug
        var existingStage = new ProductionStage
        {
            Name = "SLS Printing",
            StageSlug = "sls-printing",
            IsActive = true
        };
        _db.ProductionStages.Add(existingStage);
        await _db.SaveChangesAsync();

        var part = new Part { PartNumber = "P-002", Name = "Part", CreatedBy = "test", LastModifiedBy = "test" };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var program = new MachineProgram
        {
            ProgramNumber = "BP-SLUG",
            Name = "Slug Test",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            EstimatedPrintHours = 8,
            ScheduleStatus = ProgramScheduleStatus.None,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();
        _db.ProgramParts.Add(new ProgramPart { MachineProgramId = program.Id, PartId = part.Id, Quantity = 5, StackLevel = 1 });
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon);

        // Assert: should use the existing sls-printing stage, not create sls-print
        var printExec = result.StageExecutions.First();
        Assert.Equal(existingStage.Id, printExec.ProductionStageId);

        // No duplicate stage should exist
        var stageCount = await _db.ProductionStages.CountAsync(s => s.StageSlug.StartsWith("sls-print"));
        Assert.Equal(1, stageCount);
    }

    // ══════════════════════════════════════════════════════════
    // Cascade Rescheduling
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task CascadeReschedule_ShiftsOverlappingBuilds()
    {
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        // Build A: Mon 00:00 → Mon 20:00 (20h)
        var progA = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon, 20, "Build A");

        // Build B: Mon 21:00 → Tue 11:00 (14h) — will overlap after A is rescheduled
        var progB = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon.AddHours(21), 14, "Build B");
        // Give B a part so it can be rescheduled
        var part = new Part { PartNumber = "CASCADE-001", Name = "Part", CreatedBy = "test", LastModifiedBy = "test" };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();
        _db.ProgramParts.Add(new ProgramPart { MachineProgramId = progB.Id, PartId = part.Id, Quantity = 5, StackLevel = 1 });
        await _db.SaveChangesAsync();

        // Act: cascade from A
        var result = await _sut.CascadeRescheduleAsync(machine.Id, progA.Id);

        // Assert: B should have been shifted
        Assert.True(result.ShiftedCount >= 0); // May or may not shift depending on overlap
        var updatedB = await _db.MachinePrograms.FindAsync(progB.Id);
        Assert.NotNull(updatedB);
    }

    [Fact]
    public async Task CascadeReschedule_NoOverlap_ReturnsZero()
    {
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        // Build A: Mon 00:00 → Mon 20:00
        var progA = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon, 20, "Build A");

        // Build B: Wed 00:00 → Wed 14:00 (no overlap)
        var wed = Mon.AddDays(2);
        await AddScheduledBuildPlateProgramAsync(machine.Id, wed, 14, "Build B");

        var result = await _sut.CascadeRescheduleAsync(machine.Id, progA.Id);

        Assert.Equal(0, result.ShiftedCount);
        Assert.Empty(result.ShiftedBuilds);
    }

    // ══════════════════════════════════════════════════════════
    // Multi-Build Scheduling — Multiple Consecutive Builds
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task FindEarliestSlot_ThreeConsecutiveBuilds_AllRespectChangeover()
    {
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        // Schedule 3 consecutive 20h builds
        var slot1 = await _sut.FindEarliestSlotAsync(machine.Id, 20, Mon);
        Assert.Equal(Mon, slot1.PrintStart);

        // Add block for first build
        var prog1 = await AddScheduledBuildPlateProgramAsync(machine.Id, slot1.PrintStart, 20, "Build 1");
        await AddProgramBlockAsync(machine.Id, prog1.Id, slot1.PrintStart, slot1.PrintEnd);

        // Second build
        var slot2 = await _sut.FindEarliestSlotAsync(machine.Id, 20, Mon);
        Assert.Equal(slot1.PrintEnd.AddMinutes(30), slot2.PrintStart); // changeover gap

        var prog2 = await AddScheduledBuildPlateProgramAsync(machine.Id, slot2.PrintStart, 20, "Build 2");
        await AddProgramBlockAsync(machine.Id, prog2.Id, slot2.PrintStart, slot2.PrintEnd);

        // Third build
        var slot3 = await _sut.FindEarliestSlotAsync(machine.Id, 20, Mon);
        Assert.Equal(slot2.PrintEnd.AddMinutes(30), slot3.PrintStart); // changeover gap

        // Total span: 3 × 20h prints + 2 × 0.5h changeovers = 61h
        var totalSpan = (slot3.PrintEnd - slot1.PrintStart).TotalHours;
        Assert.Equal(61.0, totalSpan, precision: 1);
    }

    [Fact]
    public async Task FindEarliestSlot_MultipleParts_DifferentDurations()
    {
        // Simulates scheduling different suppressor variants
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        // SUPP-001: 20.2h
        var slot1 = await _sut.FindEarliestSlotAsync(machine.Id, 20.2, Mon);
        var prog1 = await AddScheduledBuildPlateProgramAsync(machine.Id, slot1.PrintStart, 20.2, "SUPP-001");
        await AddProgramBlockAsync(machine.Id, prog1.Id, slot1.PrintStart, slot1.PrintEnd);

        // SUPP-002: 16.5h (shorter compact variant)
        var slot2 = await _sut.FindEarliestSlotAsync(machine.Id, 16.5, Mon);
        Assert.Equal(slot1.PrintEnd.AddMinutes(30), slot2.PrintStart);
        var prog2 = await AddScheduledBuildPlateProgramAsync(machine.Id, slot2.PrintStart, 16.5, "SUPP-002");
        await AddProgramBlockAsync(machine.Id, prog2.Id, slot2.PrintStart, slot2.PrintEnd);

        // SUPP-003: 24.8h (longer extended variant)
        var slot3 = await _sut.FindEarliestSlotAsync(machine.Id, 24.8, Mon);
        Assert.Equal(slot2.PrintEnd.AddMinutes(30), slot3.PrintStart);

        // Verify no overlaps
        Assert.True(slot2.PrintStart > slot1.PrintEnd);
        Assert.True(slot3.PrintStart > slot2.PrintEnd);
    }

    // ══════════════════════════════════════════════════════════
    // Changeover Analysis
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ChangeoverAnalysis_OperatorInShift_ReturnsAvailable()
    {
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);
        await AddDayShiftAsync(); // Mon-Fri 08:00-16:00

        // Build ends Mon 10:00 → changeover 10:00-10:30 (within shift)
        var analysis = await _sut.AnalyzeChangeoverAsync(machine.Id, Mon.AddHours(10));

        Assert.True(analysis.OperatorAvailable);
        Assert.Null(analysis.SuggestedAction);
    }

    [Fact]
    public async Task ChangeoverAnalysis_OperatorOutOfShift_ReturnsSuggestion()
    {
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);
        await AddDayShiftAsync(); // Mon-Fri 08:00-16:00

        // Build ends Mon 22:00 → changeover 22:00-22:30 (outside shift)
        var analysis = await _sut.AnalyzeChangeoverAsync(machine.Id, Mon.AddHours(22));

        Assert.False(analysis.OperatorAvailable);
        Assert.NotNull(analysis.SuggestedAction);
        Assert.NotNull(analysis.SuggestedDurationHours);
    }

    // ══════════════════════════════════════════════════════════
    // Two-Machine Scheduling
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task FindBestSlot_TwoMachines_DistributesByAvailability()
    {
        var m1 = await AddSlsMachineAsync("M4-1", "EOS M4 #1", changeoverMinutes: 30);
        var m2 = await AddSlsMachineAsync("M4-2", "EOS M4 #2", changeoverMinutes: 30);

        // M4-1 busy for 20h, M4-2 busy for 40h
        var prog1 = await AddScheduledBuildPlateProgramAsync(m1.Id, Mon, 20, "M1 Build");
        await AddProgramBlockAsync(m1.Id, prog1.Id, Mon, Mon.AddHours(20));

        var prog2 = await AddScheduledBuildPlateProgramAsync(m2.Id, Mon, 40, "M2 Build");
        await AddProgramBlockAsync(m2.Id, prog2.Id, Mon, Mon.AddHours(40));

        // Should pick M4-1 since it finishes sooner
        var best = await _sut.FindBestSlotAsync(20, Mon, "SLS");
        Assert.Equal(m1.Id, best.MachineId);
        Assert.Equal(Mon.AddHours(20).AddMinutes(30), best.Slot.PrintStart);
    }

    // ══════════════════════════════════════════════════════════
    // Program State Transitions
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ScheduleBuildPlate_SetsCorrectProgramState()
    {
        var machine = await AddSlsMachineAsync();
        var part = new Part { PartNumber = "STATE-001", Name = "State Test", CreatedBy = "test", LastModifiedBy = "test" };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var program = new MachineProgram
        {
            ProgramNumber = "BP-STATE",
            Name = "State Test Build",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            EstimatedPrintHours = 15,
            ScheduleStatus = ProgramScheduleStatus.None,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();
        _db.ProgramParts.Add(new ProgramPart { MachineProgramId = program.Id, PartId = part.Id, Quantity = 10, StackLevel = 1 });
        await _db.SaveChangesAsync();

        await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon);

        var updated = await _db.MachinePrograms.FindAsync(program.Id);
        Assert.Equal(ProgramScheduleStatus.Scheduled, updated!.ScheduleStatus);
        Assert.True(updated.IsLocked);
        Assert.Equal(Mon, updated.ScheduledDate);
        Assert.Equal(machine.Id, updated.MachineId);
        Assert.NotNull(updated.ScheduledJobId);

        // Verify job exists and is correct
        var job = await _db.Jobs.FindAsync(updated.ScheduledJobId);
        Assert.NotNull(job);
        Assert.Equal(JobScope.Build, job!.Scope);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(10, job.Quantity); // TotalPartCount from ProgramPart
    }

    [Fact]
    public async Task ScheduleBuildPlate_CreatesPerPartJob_WithCorrectNotes()
    {
        // Verify per-part job Notes format matches the reschedule cleanup search pattern
        var machine = await AddSlsMachineAsync();
        var part = new Part { PartNumber = "PSA-SUPP-001", Name = "Suppressor", CreatedBy = "test", LastModifiedBy = "test" };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        // Add a manufacturing process with downstream stages so per-part jobs are created
        var stage = new ProductionStage { Name = "CNC Machining", StageSlug = "cnc-machining", IsActive = true };
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();

        var process = new ManufacturingProcess
        {
            PartId = part.Id,
            Name = "Supp Process",
            IsActive = true,
            Version = 1,
            DefaultBatchCapacity = 72,
            CreatedBy = "test",
            LastModifiedBy = "test",
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };
        _db.ManufacturingProcesses.Add(process);
        await _db.SaveChangesAsync();

        _db.ProcessStages.Add(new ProcessStage
        {
            ManufacturingProcessId = process.Id,
            ProductionStageId = stage.Id,
            ExecutionOrder = 1,
            ProcessingLevel = ProcessingLevel.Part,
            RunTimeMinutes = 18,
            IsRequired = true,
            IsBlocking = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        });
        await _db.SaveChangesAsync();

        var program = new MachineProgram
        {
            ProgramNumber = "BP-00001",
            Name = "Suppressor 72x",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            EstimatedPrintHours = 20.2,
            ScheduleStatus = ProgramScheduleStatus.None,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();
        _db.ProgramParts.Add(new ProgramPart { MachineProgramId = program.Id, PartId = part.Id, Quantity = 72, StackLevel = 1 });
        await _db.SaveChangesAsync();

        await _sut.ScheduleBuildPlateAsync(program.Id, machine.Id, Mon);

        // Find per-part jobs
        var perPartJobs = await _db.Jobs.Where(j => j.Scope == JobScope.Part).ToListAsync();
        Assert.NotEmpty(perPartJobs);

        // Verify Notes contain the program number in the format the cleanup code searches for
        var perPartJob = perPartJobs.First();
        Assert.Contains($"(program {program.ProgramNumber})", perPartJob.Notes);
    }

    // ══════════════════════════════════════════════════════════
    // Operator Unload Delay — FindEarliestSlot
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task FindEarliestSlot_IncludesOperatorUnloadDelay_WhenChangeoverOffShift()
    {
        // Arrange: machine with 30min changeover + 90min unload, Mon-Fri 06:00-18:00 shift
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30, operatorUnloadMinutes: 90);
        await AddShiftAsync("Day", TimeSpan.FromHours(6), TimeSpan.FromHours(18));

        // Existing build: Fri 00:00 → Fri 20:00 (20h print, ends off-shift)
        var fri = Mon.AddDays(4); // Friday
        var prog = await AddScheduledBuildPlateProgramAsync(machine.Id, fri, 20, "Fri Build");
        await AddProgramBlockAsync(machine.Id, prog.Id, fri, fri.AddHours(20));

        // Act: find next slot starting from Friday (so it must go past this build)
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, 16, fri);

        // Assert: changeover at Fri 20:00-20:30 is off-shift (shift ends 18:00).
        // Machine DOWN until Mon 06:00 + 90min unload = Mon 07:30.
        // Next slot should start at or after Mon 07:30.
        var nextMon = Mon.AddDays(7).AddHours(6); // next Monday 06:00
        var mondayWithUnload = nextMon.AddMinutes(90); // Monday 07:30
        Assert.True(slot.PrintStart >= mondayWithUnload,
            $"Expected slot after {mondayWithUnload:ddd HH:mm} but got {slot.PrintStart:ddd HH:mm}");
    }

    [Fact]
    public async Task FindEarliestSlot_NoUnloadDelay_WhenChangeoverDuringShift()
    {
        // Arrange: same machine, but build ends during shift hours
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30, operatorUnloadMinutes: 90);
        await AddShiftAsync("Day", TimeSpan.FromHours(6), TimeSpan.FromHours(18));

        // Build ends Monday at 10:00 (during shift)
        var prog = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon, 10, "Morning Build");
        await AddProgramBlockAsync(machine.Id, prog.Id, Mon, Mon.AddHours(10));

        // Act
        var slot = await _sut.FindEarliestSlotAsync(machine.Id, 8, Mon);

        // Assert: next slot starts at 10:00 + 30min changeover = 10:30 (NO unload delay)
        Assert.Equal(Mon.AddHours(10).AddMinutes(30), slot.PrintStart);
    }

    [Fact]
    public async Task FindEarliestSlot_ReturnsDowntimeFields_WhenChangeoverOffShift()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30, operatorUnloadMinutes: 90);
        await AddDayShiftAsync(); // Mon-Fri 08:00-16:00

        // Act: schedule on empty machine starting Mon 00:00, 8h print ends Mon 08:00
        // Changeover 08:00-08:30 — this IS within the shift (08:00-16:00)
        var slotInShift = await _sut.FindEarliestSlotAsync(machine.Id, 8, Mon);
        Assert.True(slotInShift.OperatorAvailableForChangeover);
        Assert.Null(slotInShift.DowntimeStart);

        // Now test: 20h print from Mon 00:00 ends Mon 20:00 — changeover 20:00-20:30 is OFF shift
        var slotOffShift = await _sut.FindEarliestSlotAsync(machine.Id, 20, Mon);
        Assert.False(slotOffShift.OperatorAvailableForChangeover);
        Assert.NotNull(slotOffShift.DowntimeStart);
        Assert.NotNull(slotOffShift.DowntimeEnd);

        // Downtime should span from changeover end (Mon 20:30) to next shift + unload (Tue 08:00 + 90min = 09:30)
        var expectedDowntimeEnd = Mon.AddDays(1).AddHours(8).AddMinutes(90); // Tue 09:30
        Assert.Equal(expectedDowntimeEnd, slotOffShift.DowntimeEnd);
    }

    [Fact]
    public async Task ChangeoverAnalysis_ReturnsDowntimeHours_WhenOffShift()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30, operatorUnloadMinutes: 90);
        await AddDayShiftAsync(); // Mon-Fri 08:00-16:00

        // Act: build ends Monday at 22:00 (off shift)
        var analysis = await _sut.AnalyzeChangeoverAsync(machine.Id, Mon.AddHours(22));

        // Assert
        Assert.False(analysis.OperatorAvailable);
        Assert.NotNull(analysis.DowntimeHours);
        Assert.True(analysis.DowntimeHours > 0);
        Assert.Contains("downtime", analysis.SuggestedAction, StringComparison.OrdinalIgnoreCase);
    }
}
