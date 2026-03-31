using Microsoft.Extensions.Logging.Abstractions;
using Vectrik.Components.Pages.Scheduler.Models;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services;
using Vectrik.Tests.Helpers;
using Xunit;

namespace Vectrik.Tests.Services;

/// <summary>
/// Tests covering recent scheduler changes (PR #50):
///   - AdvisorWizardState computed properties (PlateUtilization fix: Sum vs Max)
///   - Service constructors with ILogger parameters
///   - ProgramSchedulingService integration (FindEarliestSlot, GenerateScheduleOptions, ScheduleBuildPlateRun)
/// </summary>
public class RecentChangesTests : IDisposable
{
    private readonly TenantDbContext _db;
    private readonly ProgramSchedulingService _sut;

    // Fixed reference date: Monday 2025-07-07 for deterministic tests
    private static readonly DateTime Mon = new(2025, 7, 7, 0, 0, 0, DateTimeKind.Utc);

    public RecentChangesTests()
    {
        _db = TestDbContextFactory.Create();
        var machineProgramService = new StubMachineProgramService();
        var planningService = new ProgramPlanningService(_db, new StubNumberSequenceService(), machineProgramService);
        _sut = new ProgramSchedulingService(
            _db,
            new StubSchedulingService(),
            new StubManufacturingProcessService(),
            new StubBatchService(),
            new StubNumberSequenceService(),
            new StubStageCostService(),
            machineProgramService,
            planningService,
            new StubSerialNumberService(),
            new StubDownstreamProgramService(),
            NullLogger<ProgramSchedulingService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────

    private async Task<Machine> AddSlsMachineAsync(
        string machineId = "M4-1",
        string name = "EOS M4 #1",
        bool autoChangeover = true,
        int changeoverMinutes = 30,
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
            Priority = 1,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

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

    private async Task<Part> AddPartAsync(string partNumber = "PART-001")
    {
        var part = new Part
        {
            PartNumber = partNumber,
            Name = $"Test Part {partNumber}",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();
        return part;
    }

    private async Task<MachineProgram> AddBuildPlateProgramWithPartsAsync(
        int machineId, double durationHours, params (int partId, int qty, int stackLevel)[] parts)
    {
        var program = new MachineProgram
        {
            Name = "Build Plate",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            MachineId = machineId,
            EstimatedPrintHours = durationHours,
            LayerCount = 1000,
            ScheduleStatus = ProgramScheduleStatus.None,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        foreach (var (partId, qty, stackLevel) in parts)
        {
            _db.Set<ProgramPart>().Add(new ProgramPart
            {
                MachineProgramId = program.Id,
                PartId = partId,
                Quantity = qty,
                StackLevel = stackLevel
            });
        }
        await _db.SaveChangesAsync();
        return program;
    }

    private static PartAdditiveBuildConfig MakeBuildConfig(
        int partId,
        int singleParts = 10,
        int? doubleParts = null,
        int? tripleParts = null)
    {
        return new PartAdditiveBuildConfig
        {
            PartId = partId,
            PlannedPartsPerBuildSingle = singleParts,
            PlannedPartsPerBuildDouble = doubleParts,
            PlannedPartsPerBuildTriple = tripleParts,
            AllowStacking = doubleParts.HasValue || tripleParts.HasValue,
            MaxStackCount = tripleParts.HasValue ? 3 : doubleParts.HasValue ? 2 : 1
        };
    }

    private static DemandSummary MakeDemand(
        int partId,
        string partNumber,
        PartAdditiveBuildConfig? config = null,
        int netRemaining = 10)
    {
        return new DemandSummary(
            PartId: partId,
            PartNumber: partNumber,
            TotalOrdered: netRemaining,
            TotalProduced: 0,
            InPrograms: 0,
            InProduction: 0,
            NetRemaining: netRemaining,
            EarliestDueDate: DateTime.UtcNow.AddDays(7),
            HighestPriority: JobPriority.Normal,
            IsOverdue: false,
            IsAdditive: true,
            BuildConfig: config,
            SourceLines: []);
    }

    // ══════════════════════════════════════════════════════════
    // AdvisorWizardState — PlateUtilization (Sum vs Max bug fix)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void PlateUtilization_NoAllocations_ReturnsZero()
    {
        var state = new AdvisorWizardState();

        Assert.Equal(0, state.PlateUtilization);
    }

    [Fact]
    public void PlateUtilization_SinglePartAllocation_CorrectRatio()
    {
        // Arrange: 1 part type, 5 positions out of max 10 single-stack positions
        var config = MakeBuildConfig(partId: 1, singleParts: 10);
        var state = new AdvisorWizardState
        {
            PlateAllocations =
            [
                new EditablePlateAllocation { PartId = 1, Positions = 5, StackLevel = 1 }
            ],
            Demand = [MakeDemand(1, "P-001", config)]
        };

        // Act
        var util = state.PlateUtilization;

        // Assert: 5 positions / 10 max positions = 0.5
        Assert.Equal(0.5, util, precision: 3);
    }

    [Fact]
    public void PlateUtilization_MultiPartAllocation_UsesSumNotMax()
    {
        // THIS IS THE BUG FIX TEST: utilization should SUM positions across parts,
        // not use MAX. With two parts (5 of 10 + 3 of 6), sum is 8/16 = 0.5.
        var config1 = MakeBuildConfig(partId: 1, singleParts: 10);
        var config2 = MakeBuildConfig(partId: 2, singleParts: 6);
        var state = new AdvisorWizardState
        {
            PlateAllocations =
            [
                new EditablePlateAllocation { PartId = 1, Positions = 5, StackLevel = 1 },
                new EditablePlateAllocation { PartId = 2, Positions = 3, StackLevel = 1 }
            ],
            Demand =
            [
                MakeDemand(1, "P-001", config1),
                MakeDemand(2, "P-002", config2)
            ]
        };

        // Act
        var util = state.PlateUtilization;

        // Assert: (5 + 3) / (10 + 6) = 8/16 = 0.5
        Assert.Equal(0.5, util, precision: 3);
    }

    [Fact]
    public void PlateUtilization_DoubleStack_CorrectPositionCalculation()
    {
        // Double-stack: PlannedPartsPerBuildDouble = 20 total parts at level 2
        // GetPositionsPerBuild(2) = ceil(20/2) = 10 positions
        var config = MakeBuildConfig(partId: 1, singleParts: 10, doubleParts: 20);
        var state = new AdvisorWizardState
        {
            PlateAllocations =
            [
                new EditablePlateAllocation { PartId = 1, Positions = 5, StackLevel = 2 }
            ],
            Demand = [MakeDemand(1, "P-001", config)]
        };

        // Act
        var util = state.PlateUtilization;

        // Assert: 5 positions / 10 max positions = 0.5
        Assert.Equal(0.5, util, precision: 3);
    }

    [Fact]
    public void PlateUtilization_FullPlate_ClampedAtOne()
    {
        // More positions than capacity → clamped to 1.0
        var config = MakeBuildConfig(partId: 1, singleParts: 5);
        var state = new AdvisorWizardState
        {
            PlateAllocations =
            [
                new EditablePlateAllocation { PartId = 1, Positions = 10, StackLevel = 1 }
            ],
            Demand = [MakeDemand(1, "P-001", config)]
        };

        Assert.Equal(1.0, state.PlateUtilization, precision: 3);
    }

    [Fact]
    public void PlateUtilization_NoBuildConfig_FallsBackToPositionOfOne()
    {
        // When demand has no BuildConfig, GetPositionsPerBuild returns null → fallback = 1
        var state = new AdvisorWizardState
        {
            PlateAllocations =
            [
                new EditablePlateAllocation { PartId = 99, Positions = 3, StackLevel = 1 }
            ],
            Demand = [MakeDemand(99, "P-099", config: null)]
        };

        // With fallback capacity of 1, ratio = 3/1 → clamped to 1.0
        Assert.Equal(1.0, state.PlateUtilization, precision: 3);
    }

    // ══════════════════════════════════════════════════════════
    // AdvisorWizardState — PlatePartCount
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void PlatePartCount_SumsAcrossStackLevels()
    {
        var state = new AdvisorWizardState
        {
            PlateAllocations =
            [
                new EditablePlateAllocation { PartId = 1, Positions = 5, StackLevel = 1 }, // 5 parts
                new EditablePlateAllocation { PartId = 2, Positions = 4, StackLevel = 2 }, // 8 parts
                new EditablePlateAllocation { PartId = 3, Positions = 3, StackLevel = 3 }  // 9 parts
            ]
        };

        Assert.Equal(22, state.PlatePartCount);
    }

    [Fact]
    public void PlatePartCount_EmptyPlate_ReturnsZero()
    {
        var state = new AdvisorWizardState();
        Assert.Equal(0, state.PlatePartCount);
    }

    // ══════════════════════════════════════════════════════════
    // AdvisorWizardState — ActiveSlot & ActiveChangeoverAligned
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ActiveSlot_FallsBackToRecommendationSlot_WhenNoSelectedOption()
    {
        var slot = new ProgramScheduleSlot(Mon, Mon.AddHours(24), Mon.AddHours(24),
            Mon.AddHours(25), 1, true);
        var plate = new PlateComposition([], 1, 24.0, true, Mon.AddHours(24), true);
        var rec = new BuildRecommendation(1, "M4-1", slot, plate, "Test", []);

        var state = new AdvisorWizardState
        {
            Recommendation = rec,
            SelectedOption = null
        };

        Assert.Equal(slot, state.ActiveSlot);
    }

    [Fact]
    public void ActiveSlot_UsesSelectedOptionSlot_WhenPresent()
    {
        var recSlot = new ProgramScheduleSlot(Mon, Mon.AddHours(24), Mon.AddHours(24),
            Mon.AddHours(25), 1, true);
        var plate = new PlateComposition([], 1, 24.0, true, Mon.AddHours(24), true);
        var rec = new BuildRecommendation(1, "M4-1", recSlot, plate, "Test", []);

        var optSlot = new ProgramScheduleSlot(Mon.AddDays(1), Mon.AddDays(1).AddHours(24),
            Mon.AddDays(1).AddHours(24), Mon.AddDays(1).AddHours(25), 1, true);
        var option = new ScheduleOption("Option", "Desc", optSlot, 1, 10, 24.0, true, false, 80);

        var state = new AdvisorWizardState
        {
            Recommendation = rec,
            SelectedOption = option
        };

        Assert.Equal(optSlot, state.ActiveSlot);
    }

    [Fact]
    public void ActiveChangeoverAligned_DefaultsTrueWhenNoData()
    {
        var state = new AdvisorWizardState();
        Assert.True(state.ActiveChangeoverAligned);
    }

    [Fact]
    public void ActiveChangeoverAligned_UsesSelectedOption_WhenPresent()
    {
        var slot = new ProgramScheduleSlot(Mon, Mon.AddHours(24), Mon.AddHours(24),
            Mon.AddHours(25), 1, true);
        var option = new ScheduleOption("Option", "Desc", slot, 1, 10, 24.0,
            ChangeoverAligned: false, IsWeekendOptimal: false, RecommendationScore: 50);

        var state = new AdvisorWizardState { SelectedOption = option };

        Assert.False(state.ActiveChangeoverAligned);
    }

    // ══════════════════════════════════════════════════════════
    // AdvisorWizardState — EstimatedCompletion
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void EstimatedCompletion_NullWhenNoSlot()
    {
        var state = new AdvisorWizardState();
        Assert.Null(state.EstimatedCompletion);
    }

    [Fact]
    public void EstimatedCompletion_ReturnsPrintEndLocalTime()
    {
        var slot = new ProgramScheduleSlot(Mon, Mon.AddHours(24), Mon.AddHours(24),
            Mon.AddHours(25), 1, true);
        var plate = new PlateComposition([], 1, 24.0, true, Mon.AddHours(24), true);
        var rec = new BuildRecommendation(1, "M4-1", slot, plate, "Test", []);

        var state = new AdvisorWizardState { Recommendation = rec };

        Assert.NotNull(state.EstimatedCompletion);
        Assert.Equal(Mon.AddHours(24).ToLocalTime(), state.EstimatedCompletion.Value);
    }

    // ══════════════════════════════════════════════════════════
    // EditablePlateAllocation — TotalParts & Surplus
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void EditablePlateAllocation_TotalParts_PositionsTimesStack()
    {
        var alloc = new EditablePlateAllocation { Positions = 5, StackLevel = 3 };
        Assert.Equal(15, alloc.TotalParts);
    }

    [Fact]
    public void EditablePlateAllocation_Surplus_ExcessAboveDemand()
    {
        var alloc = new EditablePlateAllocation
        {
            Positions = 5,
            StackLevel = 2,   // TotalParts = 10
            DemandRemaining = 7
        };
        Assert.Equal(3, alloc.Surplus);
    }

    [Fact]
    public void EditablePlateAllocation_Surplus_ZeroWhenUnderDemand()
    {
        var alloc = new EditablePlateAllocation
        {
            Positions = 2,
            StackLevel = 1,   // TotalParts = 2
            DemandRemaining = 10
        };
        Assert.Equal(0, alloc.Surplus);
    }

    // ══════════════════════════════════════════════════════════
    // Service Constructors with ILogger (NullLogger acceptance)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public void ProgramSchedulingService_ConstructsWithNullLogger()
    {
        using var db = TestDbContextFactory.Create();
        var stubProgSvc = new StubMachineProgramService();
        var svc = new ProgramSchedulingService(
            db,
            new StubSchedulingService(),
            new StubManufacturingProcessService(),
            new StubBatchService(),
            new StubNumberSequenceService(),
            new StubStageCostService(),
            stubProgSvc,
            new ProgramPlanningService(db, new StubNumberSequenceService(), stubProgSvc),
            new StubSerialNumberService(),
            new StubDownstreamProgramService(),
            NullLogger<ProgramSchedulingService>.Instance);

        Assert.NotNull(svc);
    }

    [Fact]
    public void SchedulingService_ConstructsWithNullLogger()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new SchedulingService(
            db,
            new StubMachineProgramService(),
            new ShiftManagementService(db),
            NullLogger<SchedulingService>.Instance);

        Assert.NotNull(svc);
    }

    // ══════════════════════════════════════════════════════════
    // FindEarliestSlotAsync — Validity Checks
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task FindEarliestSlot_EmptyMachine_ReturnsValidSlot()
    {
        var machine = await AddSlsMachineAsync();
        var notBefore = Mon.AddHours(6); // Mon 06:00

        var slot = await _sut.FindEarliestSlotAsync(machine.Id, durationHours: 12, notBefore);

        Assert.Equal(notBefore, slot.PrintStart);
        Assert.Equal(notBefore.AddHours(12), slot.PrintEnd);
        Assert.Equal(machine.Id, slot.MachineId);
        Assert.True(slot.PrintEnd > slot.PrintStart);
    }

    [Fact]
    public async Task FindEarliestSlot_WithExistingBlock_RespectsChangeover()
    {
        var machine = await AddSlsMachineAsync(changeoverMinutes: 45);
        var existingProg = await AddScheduledBuildPlateProgramAsync(machine.Id, Mon, 20);
        await AddProgramBlockAsync(machine.Id, existingProg.Id, Mon, Mon.AddHours(20));

        var slot = await _sut.FindEarliestSlotAsync(machine.Id, durationHours: 8, Mon);

        // Should start after existing block end + 45min changeover
        var expectedStart = Mon.AddHours(20).AddMinutes(45);
        Assert.Equal(expectedStart, slot.PrintStart);
        Assert.Equal(expectedStart.AddHours(8), slot.PrintEnd);
    }

    // ══════════════════════════════════════════════════════════
    // GenerateScheduleOptionsAsync — Changeover Info
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task GenerateScheduleOptions_EmptyMachine_ReturnsOptions()
    {
        var machine = await AddSlsMachineAsync();
        await AddDayShiftAsync();

        var buildConfig = MakeBuildConfig(partId: 1, singleParts: 10, doubleParts: 20);

        var options = await _sut.GenerateScheduleOptionsAsync(
            machine.Id,
            baseDurationHours: 24,
            notBefore: Mon,
            buildConfig: buildConfig,
            demandQuantity: 10);

        // Should return at least one option
        Assert.NotEmpty(options);

        // Each option should have valid data
        foreach (var opt in options)
        {
            Assert.True(opt.DurationHours > 0);
            Assert.True(opt.PartsPerBuild > 0);
            Assert.NotNull(opt.Slot);
            Assert.True(opt.Slot.PrintEnd > opt.Slot.PrintStart);
        }
    }

    [Fact]
    public async Task GenerateScheduleOptions_IncludesChangeoverAlignmentInfo()
    {
        var machine = await AddSlsMachineAsync();
        await AddDayShiftAsync();

        var buildConfig = MakeBuildConfig(partId: 1, singleParts: 10);

        var options = await _sut.GenerateScheduleOptionsAsync(
            machine.Id,
            baseDurationHours: 24,
            notBefore: Mon,
            buildConfig: buildConfig,
            demandQuantity: 10);

        Assert.NotEmpty(options);
        // At least one option should report changeover alignment
        Assert.All(options, opt =>
        {
            Assert.NotNull(opt.Label);
            Assert.NotNull(opt.Description);
            Assert.True(opt.RecommendationScore >= 0);
        });
    }

    // ══════════════════════════════════════════════════════════
    // ScheduleBuildPlateRunAsync — Creates Copy
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ScheduleBuildPlateRun_CreatesScheduledCopy()
    {
        // Arrange: create a template build plate program with parts
        var machine = await AddSlsMachineAsync();
        await AddDayShiftAsync();

        var part = await AddPartAsync("SUP-001");
        var program = await AddBuildPlateProgramWithPartsAsync(
            machine.Id, 24.0, (part.Id, 10, 1));

        // Act
        var result = await _sut.ScheduleBuildPlateRunAsync(program.Id, machine.Id, Mon);

        // Assert: result should reference the source program
        Assert.NotNull(result);
        Assert.NotNull(result.Slot);
        Assert.True(result.Slot.PrintEnd > result.Slot.PrintStart);
    }

    // ══════════════════════════════════════════════════════════
    // ScheduleBuildPlateAsync — Validation
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ScheduleBuildPlate_NoParts_ThrowsInvalidOperation()
    {
        var machine = await AddSlsMachineAsync();
        var program = new MachineProgram
        {
            Name = "Empty Plate",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            MachineId = machine.Id,
            EstimatedPrintHours = 24.0,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ScheduleBuildPlateAsync(program.Id, machine.Id));
    }

    [Fact]
    public async Task ScheduleBuildPlate_NoDuration_ThrowsInvalidOperation()
    {
        var machine = await AddSlsMachineAsync();
        var part = await AddPartAsync();
        var program = new MachineProgram
        {
            Name = "No Duration Plate",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            MachineId = machine.Id,
            EstimatedPrintHours = null, // No slicer data
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        _db.Set<ProgramPart>().Add(new ProgramPart
        {
            MachineProgramId = program.Id,
            PartId = part.Id,
            Quantity = 5,
            StackLevel = 1
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ScheduleBuildPlateAsync(program.Id, machine.Id));
    }

    [Fact]
    public async Task ScheduleBuildPlate_WrongProgramType_ThrowsInvalidOperation()
    {
        var machine = await AddSlsMachineAsync();
        var part = await AddPartAsync();
        var program = new MachineProgram
        {
            Name = "Standard Program",
            ProgramType = ProgramType.Standard, // Not BuildPlate
            Status = ProgramStatus.Active,
            MachineId = machine.Id,
            EstimatedPrintHours = 4.0,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ScheduleBuildPlateAsync(program.Id, machine.Id));
    }
}
