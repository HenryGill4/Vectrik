using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services;
using Opcentrix_V3.Tests.Helpers;
using Xunit;

namespace Opcentrix_V3.Tests.Services;

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
        int priority = 1)
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
    public async Task ProgramSlot_Continuous_SameProgramRun_AppliesChangeover()
    {
        // Arrange: machine with one block, new slot is for same program
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);
        var existingProg = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon, 24);
        await AddProgramBlockAsync(machine.Id, existingProg.Id, Mon, Mon.AddHours(24));

        // Act: schedule same program (forProgramId matches)
        var slot = await _sut.FindEarliestSlotAsync(
            machine.Id, durationHours: 10, Mon, forProgramId: existingProg.Id);

        // Assert: changeover always applied — cool-down/powder extraction required regardless
        Assert.Equal(Mon.AddHours(24).AddMinutes(30), slot.PrintStart);  // Tue 00:30
        Assert.Equal(Mon.AddHours(34).AddMinutes(30), slot.PrintEnd);    // Tue 10:30
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
}
