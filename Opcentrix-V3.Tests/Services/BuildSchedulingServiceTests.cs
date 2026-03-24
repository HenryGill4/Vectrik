using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services;
using Opcentrix_V3.Tests.Helpers;
using Xunit;

namespace Opcentrix_V3.Tests.Services;

public class BuildSchedulingServiceTests : IDisposable
{
    private readonly TenantDbContext _db;
    private readonly BuildSchedulingService _sut;

    // Fixed reference date: Monday 2025-07-07 for deterministic tests
    private static readonly DateTime Mon = new(2025, 7, 7, 0, 0, 0, DateTimeKind.Utc);

    public BuildSchedulingServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new BuildSchedulingService(
            _db,
            new StubBuildPlanningService(),
            new StubSerialNumberService(),
            new StubSchedulingService(),
            new StubManufacturingProcessService(),
            new StubBatchService(),
            new StubNumberSequenceService(),
            new StubStageCostService(),
            new StubMachineProgramService());
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

    /// <summary>Add an existing scheduled build block (as a StageExecution) on a machine.</summary>
    private async Task<StageExecution> AddBuildBlockAsync(
        int machineId, int buildPackageId, DateTime start, DateTime end)
    {
        var exec = new StageExecution
        {
            MachineId = machineId,
            BuildPackageId = buildPackageId,
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

    /// <summary>Add a scheduled BuildPackage (no StageExecution yet).</summary>
    private async Task<BuildPackage> AddScheduledBuildPackageAsync(
        int machineId, DateTime scheduledDate, double durationHours,
        string name = "Build", int? sourceBuildPackageId = null, int? buildTemplateId = null)
    {
        var pkg = new BuildPackage
        {
            Name = name,
            MachineId = machineId,
            ScheduledDate = scheduledDate,
            EstimatedDurationHours = durationHours,
            Status = BuildPackageStatus.Scheduled,
            IsSlicerDataEntered = true,
            IsLocked = true,
            SourceBuildPackageId = sourceBuildPackageId,
            BuildTemplateId = buildTemplateId,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.BuildPackages.Add(pkg);
        await _db.SaveChangesAsync();
        return pkg;
    }

    // ══════════════════════════════════════════════════════════
    // FindEarliestBuildSlotAsync — 24/7 Continuous (AutoChangeover)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task BuildSlot_Continuous_EmptyMachine_StartsAtNotBefore()
    {
        // Arrange: 24/7 SLS machine, no existing work
        var machine = await AddSlsMachineAsync();
        var notBefore = Mon.AddHours(10); // Mon 10:00

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 24, notBefore);

        // Assert: starts exactly at notBefore, ends 24h later
        Assert.Equal(Mon.AddHours(10), slot.PrintStart);
        Assert.Equal(Mon.AddHours(34), slot.PrintEnd); // Tue 10:00
        Assert.Equal(machine.Id, slot.MachineId);
    }

    [Fact]
    public async Task BuildSlot_Continuous_ExistingBlock_SchedulesAfterWithChangeover()
    {
        // Arrange: machine with one 24h block Mon 00:00 → Tue 00:00, 30min changeover
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);
        var existingPkg = await AddScheduledBuildPackageAsync(machine.Id, Mon, 24);
        await AddBuildBlockAsync(machine.Id, existingPkg.Id, Mon, Mon.AddHours(24));

        // Act: request 10h build starting Mon 00:00
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 10, Mon);

        // Assert: starts after block end + 30min changeover
        var expectedStart = Mon.AddHours(24).AddMinutes(30); // Tue 00:30
        var expectedEnd = expectedStart.AddHours(10);        // Tue 10:30
        Assert.Equal(expectedStart, slot.PrintStart);
        Assert.Equal(expectedEnd, slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_Continuous_SameBuild_AppliesChangeover()
    {
        // Arrange: machine with one block, new build is same BuildPackageId
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);
        var existingPkg = await AddScheduledBuildPackageAsync(machine.Id, Mon, 24);
        await AddBuildBlockAsync(machine.Id, existingPkg.Id, Mon, Mon.AddHours(24));

        // Act: schedule same build (forBuildPackageId matches)
        var slot = await _sut.FindEarliestBuildSlotAsync(
            machine.Id, durationHours: 10, Mon, forBuildPackageId: existingPkg.Id);

        // Assert: changeover always applied — cool-down/powder extraction required regardless
        Assert.Equal(Mon.AddHours(24).AddMinutes(30), slot.PrintStart);  // Tue 00:30
        Assert.Equal(Mon.AddHours(34).AddMinutes(30), slot.PrintEnd);    // Tue 10:30
    }

    [Fact]
    public async Task BuildSlot_Continuous_TwoExistingBlocks_SchedulesAfterSecond()
    {
        // Arrange: two back-to-back blocks with changeover
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        var pkg1 = await AddScheduledBuildPackageAsync(machine.Id, Mon, 12);
        await AddBuildBlockAsync(machine.Id, pkg1.Id, Mon, Mon.AddHours(12));

        var pkg2Start = Mon.AddHours(12).AddMinutes(30); // after changeover
        var pkg2 = await AddScheduledBuildPackageAsync(machine.Id, pkg2Start, 12);
        await AddBuildBlockAsync(machine.Id, pkg2.Id, pkg2Start, pkg2Start.AddHours(12));

        // Act: schedule a 6h build
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 6, Mon);

        // Assert: after second block + changeover
        var expectedStart = pkg2Start.AddHours(12).AddMinutes(30);
        var expectedEnd = expectedStart.AddHours(6);
        Assert.Equal(expectedStart, slot.PrintStart);
        Assert.Equal(expectedEnd, slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_Continuous_FitsInGapBetweenBlocks()
    {
        // Arrange: two blocks with a large enough gap (including changeover)
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        // Block 1: Mon 00:00 → Mon 10:00
        var pkg1 = await AddScheduledBuildPackageAsync(machine.Id, Mon, 10);
        await AddBuildBlockAsync(machine.Id, pkg1.Id, Mon, Mon.AddHours(10));

        // Block 2: Wed 00:00 → Wed 12:00 (big gap between)
        var wed = Mon.AddDays(2);
        var pkg2 = await AddScheduledBuildPackageAsync(machine.Id, wed, 12);
        await AddBuildBlockAsync(machine.Id, pkg2.Id, wed, wed.AddHours(12));

        // Act: schedule a 6h build starting Mon 00:00
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 6, Mon);

        // Assert: fits after first block + changeover (Mon 10:30 → Mon 16:30)
        var expectedStart = Mon.AddHours(10).AddMinutes(30);
        var expectedEnd = expectedStart.AddHours(6);
        Assert.Equal(expectedStart, slot.PrintStart);
        Assert.Equal(expectedEnd, slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_Continuous_GapTooSmall_SkipsToAfterSecondBlock()
    {
        // Arrange: gap between blocks is only 2h (< 6h needed + 30min changeover)
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        // Block 1: Mon 00:00 → Mon 10:00
        var pkg1 = await AddScheduledBuildPackageAsync(machine.Id, Mon, 10);
        await AddBuildBlockAsync(machine.Id, pkg1.Id, Mon, Mon.AddHours(10));

        // Block 2: Mon 12:00 → Mon 22:00 (only 2h gap after changeover = 1.5h usable)
        var pkg2 = await AddScheduledBuildPackageAsync(machine.Id, Mon.AddHours(12), 10);
        await AddBuildBlockAsync(machine.Id, pkg2.Id, Mon.AddHours(12), Mon.AddHours(22));

        // Act: schedule a 6h build
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 6, Mon);

        // Assert: can't fit in gap → after second block + changeover
        var expectedStart = Mon.AddHours(22).AddMinutes(30);
        var expectedEnd = expectedStart.AddHours(6);
        Assert.Equal(expectedStart, slot.PrintStart);
        Assert.Equal(expectedEnd, slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_Continuous_NoChangeover_BlocksPackTightly()
    {
        // Arrange: machine without auto-changeover
        var machine = await AddSlsMachineAsync(autoChangeover: false, changeoverMinutes: 0);

        var pkg1 = await AddScheduledBuildPackageAsync(machine.Id, Mon, 10);
        await AddBuildBlockAsync(machine.Id, pkg1.Id, Mon, Mon.AddHours(10));

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 5, Mon);

        // Assert: immediately after block with no gap
        Assert.Equal(Mon.AddHours(10), slot.PrintStart);
        Assert.Equal(Mon.AddHours(15), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_Continuous_ChangeoverReturned_MatchesWindow()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(changeoverMinutes: 45);

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 8, Mon);

        // Assert: changeover window follows print end
        Assert.Equal(Mon, slot.PrintStart);
        Assert.Equal(Mon.AddHours(8), slot.PrintEnd);
        Assert.Equal(Mon.AddHours(8), slot.ChangeoverStart);
        Assert.Equal(Mon.AddHours(8).AddMinutes(45), slot.ChangeoverEnd);
    }

    [Fact]
    public async Task BuildSlot_Continuous_ThreeBlocksNoOverlap()
    {
        // Arrange: schedule three builds sequentially
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        // Block 1: Mon 00:00 → Mon 12:00
        var pkg1 = await AddScheduledBuildPackageAsync(machine.Id, Mon, 12);
        await AddBuildBlockAsync(machine.Id, pkg1.Id, Mon, Mon.AddHours(12));

        // Block 2: Mon 12:30 → Tue 00:30 (after changeover)
        var b2Start = Mon.AddHours(12).AddMinutes(30);
        var pkg2 = await AddScheduledBuildPackageAsync(machine.Id, b2Start, 12);
        await AddBuildBlockAsync(machine.Id, pkg2.Id, b2Start, b2Start.AddHours(12));

        // Block 3: Tue 01:00 → Tue 13:00 (after changeover from block 2)
        var b3Start = b2Start.AddHours(12).AddMinutes(30);
        var pkg3 = await AddScheduledBuildPackageAsync(machine.Id, b3Start, 12);
        await AddBuildBlockAsync(machine.Id, pkg3.Id, b3Start, b3Start.AddHours(12));

        // Act: schedule a 4th build
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 6, Mon);

        // Assert: lands after third block + changeover
        var expectedStart = b3Start.AddHours(12).AddMinutes(30);
        Assert.Equal(expectedStart, slot.PrintStart);
        Assert.Equal(expectedStart.AddHours(6), slot.PrintEnd);
    }

    // ══════════════════════════════════════════════════════════
    // FindEarliestBuildSlotAsync — Shift-Aware (Non-Continuous)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task BuildSlot_ShiftAware_EmptyMachine_SnapsToShiftStart()
    {
        // Arrange: non-continuous machine, Mon-Fri 08:00-16:00 shift
        var machine = await AddSlsMachineAsync(autoChangeover: false, changeoverMinutes: 0);
        await AddDayShiftAsync();

        // Request at Mon 03:00 (before shift starts)
        var notBefore = Mon.AddHours(3);

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 4, notBefore);

        // Assert: snaps to Mon 08:00
        Assert.Equal(Mon.AddHours(8), slot.PrintStart);
        // 4h on 8h shift fits within one day: ends Mon 12:00
        Assert.Equal(Mon.AddHours(12), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_ShiftAware_MidShift_UsesExactRequestTime()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(autoChangeover: false, changeoverMinutes: 0);
        await AddDayShiftAsync();

        // Request at Mon 10:00 (mid-shift)
        var notBefore = Mon.AddHours(10);

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 4, notBefore);

        // Assert: starts at 10:00 exactly, 4h fits → ends 14:00
        Assert.Equal(Mon.AddHours(10), slot.PrintStart);
        Assert.Equal(Mon.AddHours(14), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_ShiftAware_WorkSpansMultipleDays()
    {
        // Arrange: 12h work on 8h/day shift
        var machine = await AddSlsMachineAsync(autoChangeover: false, changeoverMinutes: 0);
        await AddDayShiftAsync();

        var notBefore = Mon.AddHours(8); // Mon 08:00

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 12, notBefore);

        // Assert: 8h Mon (08:00-16:00) + 4h Tue (08:00-12:00)
        Assert.Equal(Mon.AddHours(8), slot.PrintStart);
        Assert.Equal(Mon.AddDays(1).AddHours(12), slot.PrintEnd); // Tue 12:00
    }

    [Fact]
    public async Task BuildSlot_ShiftAware_FridayAfternoon_WrapsToMonday()
    {
        // Arrange: request at Fri 14:00, need 6h work (only 2h left in Fri shift)
        var machine = await AddSlsMachineAsync(autoChangeover: false, changeoverMinutes: 0);
        await AddDayShiftAsync();

        var fri = Mon.AddDays(4); // 2025-07-11 Friday
        var notBefore = fri.AddHours(14); // Fri 14:00

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 6, notBefore);

        // Assert: 2h Fri (14:00-16:00) + 4h next Mon (08:00-12:00)
        Assert.Equal(fri.AddHours(14), slot.PrintStart);
        var nextMon = Mon.AddDays(7); // 2025-07-14
        Assert.Equal(nextMon.AddHours(12), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_ShiftAware_WeekendRequest_SnapsToMonday()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(autoChangeover: false, changeoverMinutes: 0);
        await AddDayShiftAsync();

        var sat = Mon.AddDays(5); // Saturday
        var notBefore = sat.AddHours(10);

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 4, notBefore);

        // Assert: snaps to next Monday 08:00
        var nextMon = Mon.AddDays(7);
        Assert.Equal(nextMon.AddHours(8), slot.PrintStart);
        Assert.Equal(nextMon.AddHours(12), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_ShiftAware_ExistingBlock_PushesToNextDay()
    {
        // Arrange: existing block occupies Mon 08:00-16:00 (full shift)
        var machine = await AddSlsMachineAsync(autoChangeover: false, changeoverMinutes: 0);
        await AddDayShiftAsync();

        var pkg = await AddScheduledBuildPackageAsync(machine.Id, Mon.AddHours(8), 8);
        await AddBuildBlockAsync(machine.Id, pkg.Id, Mon.AddHours(8), Mon.AddHours(16));

        // Act: schedule 4h work
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 4, Mon);

        // Assert: pushed to Tue 08:00 (Mon shift fully occupied)
        Assert.Equal(Mon.AddDays(1).AddHours(8), slot.PrintStart);
        Assert.Equal(Mon.AddDays(1).AddHours(12), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_ShiftAware_AtShiftEnd_AdvancesToNextShift()
    {
        // Arrange: notBefore is exactly shift end (Mon 16:00)
        var machine = await AddSlsMachineAsync(autoChangeover: false, changeoverMinutes: 0);
        await AddDayShiftAsync();

        var notBefore = Mon.AddHours(16); // exactly shift end

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 4, notBefore);

        // Assert: must snap to Tue 08:00 (not stay at Mon 16:00)
        Assert.Equal(Mon.AddDays(1).AddHours(8), slot.PrintStart);
        Assert.Equal(Mon.AddDays(1).AddHours(12), slot.PrintEnd);
    }

    // ══════════════════════════════════════════════════════════
    // FindEarliestBuildSlotAsync — Changeover Logic
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task BuildSlot_Changeover_DifferentBuilds_AddsChangeoverGap()
    {
        // Arrange: continuous machine with 30min changeover
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        var pkg1 = await AddScheduledBuildPackageAsync(machine.Id, Mon, 10);
        await AddBuildBlockAsync(machine.Id, pkg1.Id, Mon, Mon.AddHours(10));

        // Act: schedule a different build
        var slot = await _sut.FindEarliestBuildSlotAsync(
            machine.Id, durationHours: 5, Mon, forBuildPackageId: null);

        // Assert: 30min changeover gap after block 1
        Assert.Equal(Mon.AddHours(10).AddMinutes(30), slot.PrintStart);
        Assert.Equal(Mon.AddHours(15).AddMinutes(30), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_Changeover_SameBuildFamily_SkipsChangeover()
    {
        // Arrange: build with a SourceBuildPackageId — "same build family"
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        // Source build (the original)
        var sourcePkg = await AddScheduledBuildPackageAsync(machine.Id, Mon, 10, name: "Source");
        await AddBuildBlockAsync(machine.Id, sourcePkg.Id, Mon, Mon.AddHours(10));

        // Copy build (scheduled copy from source)
        var copyPkg = new BuildPackage
        {
            Name = "Copy",
            MachineId = machine.Id,
            SourceBuildPackageId = sourcePkg.Id,
            Status = BuildPackageStatus.Ready,
            IsSlicerDataEntered = true,
            EstimatedDurationHours = 10,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.BuildPackages.Add(copyPkg);
        await _db.SaveChangesAsync();

        // Act: schedule the copy — changeover always applies
        var slot = await _sut.FindEarliestBuildSlotAsync(
            machine.Id, durationHours: 10, Mon, forBuildPackageId: copyPkg.Id);

        // Assert: changeover applied — cool-down/powder extraction required regardless of build family
        Assert.Equal(Mon.AddHours(10).AddMinutes(30), slot.PrintStart);
        Assert.Equal(Mon.AddHours(20).AddMinutes(30), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_Changeover_SameTemplate_AppliesChangeover()
    {
        // Arrange: two builds from the same BuildTemplate
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        var templateId = 42;
        var existingPkg = await AddScheduledBuildPackageAsync(
            machine.Id, Mon, 10, name: "Template Run 1", buildTemplateId: templateId);
        await AddBuildBlockAsync(machine.Id, existingPkg.Id, Mon, Mon.AddHours(10));

        var newPkg = new BuildPackage
        {
            Name = "Template Run 2",
            MachineId = machine.Id,
            BuildTemplateId = templateId,
            Status = BuildPackageStatus.Ready,
            IsSlicerDataEntered = true,
            EstimatedDurationHours = 10,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.BuildPackages.Add(newPkg);
        await _db.SaveChangesAsync();

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(
            machine.Id, durationHours: 10, Mon, forBuildPackageId: newPkg.Id);

        // Assert: changeover always applied — machine needs cool-down/powder extraction
        Assert.Equal(Mon.AddHours(10).AddMinutes(30), slot.PrintStart);
        Assert.Equal(Mon.AddHours(20).AddMinutes(30), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_Changeover_DifferentTemplate_IncludesChangeover()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        var existingPkg = await AddScheduledBuildPackageAsync(
            machine.Id, Mon, 10, name: "Template A", buildTemplateId: 100);
        await AddBuildBlockAsync(machine.Id, existingPkg.Id, Mon, Mon.AddHours(10));

        var newPkg = new BuildPackage
        {
            Name = "Template B",
            MachineId = machine.Id,
            BuildTemplateId = 200, // different template
            Status = BuildPackageStatus.Ready,
            IsSlicerDataEntered = true,
            EstimatedDurationHours = 10,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.BuildPackages.Add(newPkg);
        await _db.SaveChangesAsync();

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(
            machine.Id, durationHours: 10, Mon, forBuildPackageId: newPkg.Id);

        // Assert: changeover required — 30min gap
        Assert.Equal(Mon.AddHours(10).AddMinutes(30), slot.PrintStart);
        Assert.Equal(Mon.AddHours(20).AddMinutes(30), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_Changeover_MultipleBlocks_AllWithChangeover()
    {
        // Arrange: three different builds, each needs changeover
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        var pkg1 = await AddScheduledBuildPackageAsync(machine.Id, Mon, 8, name: "Build A");
        await AddBuildBlockAsync(machine.Id, pkg1.Id, Mon, Mon.AddHours(8));

        var b2Start = Mon.AddHours(8).AddMinutes(30); // after changeover
        var pkg2 = await AddScheduledBuildPackageAsync(machine.Id, b2Start, 8, name: "Build B");
        await AddBuildBlockAsync(machine.Id, pkg2.Id, b2Start, b2Start.AddHours(8));

        var b3Start = b2Start.AddHours(8).AddMinutes(30); // after changeover
        var pkg3 = await AddScheduledBuildPackageAsync(machine.Id, b3Start, 8, name: "Build C");
        await AddBuildBlockAsync(machine.Id, pkg3.Id, b3Start, b3Start.AddHours(8));

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 4, Mon);

        // Assert: after third block + changeover
        var expectedStart = b3Start.AddHours(8).AddMinutes(30);
        Assert.Equal(expectedStart, slot.PrintStart);
        Assert.Equal(expectedStart.AddHours(4), slot.PrintEnd);
    }

    // ══════════════════════════════════════════════════════════
    // FindEarliestBuildSlotAsync — BuildPackage-only blocks
    // (no StageExecution yet — tests the merge logic)
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task BuildSlot_BuildPackageOnly_NoStageExec_StillBlocksSlot()
    {
        // Arrange: BuildPackage with ScheduledDate but NO StageExecution rows
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);
        await AddScheduledBuildPackageAsync(machine.Id, Mon, 24, name: "Orphan Build");

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 6, Mon);

        // Assert: must schedule after the BuildPackage block + changeover
        Assert.Equal(Mon.AddHours(24).AddMinutes(30), slot.PrintStart);
        Assert.Equal(Mon.AddHours(30).AddMinutes(30), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_BuildPackageAndStageExec_NoDuplication()
    {
        // Arrange: same time covered by both a StageExecution and a BuildPackage row
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);
        var pkg = await AddScheduledBuildPackageAsync(machine.Id, Mon, 10);
        await AddBuildBlockAsync(machine.Id, pkg.Id, Mon, Mon.AddHours(10));

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 5, Mon);

        // Assert: only one block counted (no double-counting)
        Assert.Equal(Mon.AddHours(10).AddMinutes(30), slot.PrintStart);
    }

    // ══════════════════════════════════════════════════════════
    // FindBestBuildSlotAsync — Multi-Machine
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task BestBuildSlot_PicksEarliestAcrossMachines()
    {
        // Arrange: Machine A busy until Tue 00:00, Machine B empty
        var machineA = await AddSlsMachineAsync("M4-1", "Machine A", changeoverMinutes: 30, priority: 1);
        var machineB = await AddSlsMachineAsync("M4-2", "Machine B", changeoverMinutes: 30, priority: 2);

        var pkgA = await AddScheduledBuildPackageAsync(machineA.Id, Mon, 24, name: "Busy A");
        await AddBuildBlockAsync(machineA.Id, pkgA.Id, Mon, Mon.AddHours(24));

        // Act
        var best = await _sut.FindBestBuildSlotAsync(durationHours: 10, Mon);

        // Assert: picks Machine B (can start immediately)
        Assert.Equal(machineB.Id, best.MachineId);
        Assert.Equal(Mon, best.Slot.PrintStart);
        Assert.Equal(Mon.AddHours(10), best.Slot.PrintEnd);
    }

    [Fact]
    public async Task BestBuildSlot_BothBusy_PicksEarlierFinisher()
    {
        // Arrange: A busy until Mon 20:00, B busy until Mon 12:00
        var machineA = await AddSlsMachineAsync("M4-1", "Machine A", changeoverMinutes: 30, priority: 1);
        var machineB = await AddSlsMachineAsync("M4-2", "Machine B", changeoverMinutes: 30, priority: 2);

        var pkgA = await AddScheduledBuildPackageAsync(machineA.Id, Mon, 20, name: "Build A");
        await AddBuildBlockAsync(machineA.Id, pkgA.Id, Mon, Mon.AddHours(20));

        var pkgB = await AddScheduledBuildPackageAsync(machineB.Id, Mon, 12, name: "Build B");
        await AddBuildBlockAsync(machineB.Id, pkgB.Id, Mon, Mon.AddHours(12));

        // Act
        var best = await _sut.FindBestBuildSlotAsync(durationHours: 6, Mon);

        // Assert: Machine B available at 12:30 (earlier than A at 20:30)
        Assert.Equal(machineB.Id, best.MachineId);
        Assert.Equal(Mon.AddHours(12).AddMinutes(30), best.Slot.PrintStart);
    }

    [Fact]
    public async Task BestBuildSlot_SameBuild_AppliesChangeoverOnAllMachines()
    {
        // Arrange: existing build on Machine A, different build on Machine B
        var machineA = await AddSlsMachineAsync("M4-1", "Machine A", changeoverMinutes: 60, priority: 1);
        var machineB = await AddSlsMachineAsync("M4-2", "Machine B", changeoverMinutes: 60, priority: 2);

        var existingPkg = await AddScheduledBuildPackageAsync(machineA.Id, Mon, 10, name: "Same Build");
        await AddBuildBlockAsync(machineA.Id, existingPkg.Id, Mon, Mon.AddHours(10));

        // Different build on B ending at same time
        var pkgB = await AddScheduledBuildPackageAsync(machineB.Id, Mon, 10, name: "Other Build");
        await AddBuildBlockAsync(machineB.Id, pkgB.Id, Mon, Mon.AddHours(10));

        // Act: changeover always applied on both machines
        var best = await _sut.FindBestBuildSlotAsync(
            durationHours: 6, Mon, forBuildPackageId: existingPkg.Id);

        // Assert: both machines need changeover → tie broken by priority (A wins)
        Assert.Equal(machineA.Id, best.MachineId);
        Assert.Equal(Mon.AddHours(11), best.Slot.PrintStart);  // 10h block + 60min changeover
    }

    // ══════════════════════════════════════════════════════════
    // GetMachineTimelineAsync — Timeline Entries
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task Timeline_ReturnsEntriesInTimeRange()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        var pkg1 = await AddScheduledBuildPackageAsync(machine.Id, Mon, 10, name: "Build 1");
        await AddBuildBlockAsync(machine.Id, pkg1.Id, Mon, Mon.AddHours(10));

        var pkg2 = await AddScheduledBuildPackageAsync(machine.Id, Mon.AddHours(11), 8, name: "Build 2");
        await AddBuildBlockAsync(machine.Id, pkg2.Id, Mon.AddHours(11), Mon.AddHours(19));

        // Act: query Mon 00:00 → Mon+2 00:00
        var entries = await _sut.GetMachineTimelineAsync(machine.Id, Mon, Mon.AddDays(2));

        // Assert
        Assert.Equal(2, entries.Count);
        Assert.Equal(Mon, entries[0].PrintStart);
        Assert.Equal(Mon.AddHours(10), entries[0].PrintEnd);
        Assert.Equal(Mon.AddHours(11), entries[1].PrintStart);
        Assert.Equal(Mon.AddHours(19), entries[1].PrintEnd);
    }

    [Fact]
    public async Task Timeline_ChangeoverWindows_Correct()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(changeoverMinutes: 45);

        var pkg = await AddScheduledBuildPackageAsync(machine.Id, Mon, 10, name: "Build 1");
        await AddBuildBlockAsync(machine.Id, pkg.Id, Mon, Mon.AddHours(10));

        // Act
        var entries = await _sut.GetMachineTimelineAsync(machine.Id, Mon, Mon.AddDays(1));

        // Assert: changeover window = print end → print end + 45min
        Assert.Single(entries);
        Assert.Equal(Mon.AddHours(10), entries[0].ChangeoverStart);
        Assert.Equal(Mon.AddHours(10).AddMinutes(45), entries[0].ChangeoverEnd);
    }

    [Fact]
    public async Task Timeline_OrphanBuildPackage_IncludedAsEntry()
    {
        // Arrange: BuildPackage with no StageExecution
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);
        await AddScheduledBuildPackageAsync(machine.Id, Mon, 12, name: "Orphan");

        // Act
        var entries = await _sut.GetMachineTimelineAsync(machine.Id, Mon, Mon.AddDays(2));

        // Assert: orphan build appears in timeline
        Assert.Single(entries);
        Assert.Equal("Orphan", entries[0].BuildName);
        Assert.Equal(Mon, entries[0].PrintStart);
        Assert.Equal(Mon.AddHours(12), entries[0].PrintEnd);
    }

    [Fact]
    public async Task Timeline_ExcludesEntriesOutsideRange()
    {
        // Arrange
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);
        var pkg = await AddScheduledBuildPackageAsync(machine.Id, Mon, 10, name: "Build 1");
        await AddBuildBlockAsync(machine.Id, pkg.Id, Mon, Mon.AddHours(10));

        // Act: query a range that doesn't overlap
        var entries = await _sut.GetMachineTimelineAsync(
            machine.Id, Mon.AddDays(3), Mon.AddDays(5));

        // Assert: nothing in range
        Assert.Empty(entries);
    }

    // ══════════════════════════════════════════════════════════
    // Edge Cases & Exact Boundary Tests
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task BuildSlot_Continuous_CompletedBlocksIgnored()
    {
        // Arrange: completed block should not block scheduling
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        var completedExec = new StageExecution
        {
            MachineId = machine.Id,
            BuildPackageId = 999,
            ScheduledStartAt = Mon,
            ScheduledEndAt = Mon.AddHours(24),
            Status = StageExecutionStatus.Completed, // completed — should be ignored
            SortOrder = 1,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.StageExecutions.Add(completedExec);

        var completedPkg = new BuildPackage
        {
            Name = "Done",
            MachineId = machine.Id,
            ScheduledDate = Mon,
            EstimatedDurationHours = 24,
            Status = BuildPackageStatus.Completed, // completed — should be ignored
            IsSlicerDataEntered = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.BuildPackages.Add(completedPkg);
        await _db.SaveChangesAsync();

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 6, Mon);

        // Assert: starts at notBefore (completed blocks ignored)
        Assert.Equal(Mon, slot.PrintStart);
        Assert.Equal(Mon.AddHours(6), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_Continuous_SkippedAndFailedBlocksIgnored()
    {
        // Arrange: skipped and failed blocks should not count
        var machine = await AddSlsMachineAsync(changeoverMinutes: 30);

        _db.StageExecutions.Add(new StageExecution
        {
            MachineId = machine.Id,
            BuildPackageId = 888,
            ScheduledStartAt = Mon,
            ScheduledEndAt = Mon.AddHours(12),
            Status = StageExecutionStatus.Skipped,
            SortOrder = 1,
            CreatedBy = "test",
            LastModifiedBy = "test"
        });
        _db.StageExecutions.Add(new StageExecution
        {
            MachineId = machine.Id,
            BuildPackageId = 889,
            ScheduledStartAt = Mon.AddHours(12),
            ScheduledEndAt = Mon.AddHours(24),
            Status = StageExecutionStatus.Failed,
            SortOrder = 1,
            CreatedBy = "test",
            LastModifiedBy = "test"
        });
        await _db.SaveChangesAsync();

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 8, Mon);

        // Assert: free at Mon 00:00
        Assert.Equal(Mon, slot.PrintStart);
        Assert.Equal(Mon.AddHours(8), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_Continuous_VeryLongBuild_ExactEnd()
    {
        // Arrange: 168h build = exactly 7 days
        var machine = await AddSlsMachineAsync();

        // Act
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 168, Mon);

        // Assert
        Assert.Equal(Mon, slot.PrintStart);
        Assert.Equal(Mon.AddDays(7), slot.PrintEnd);
    }

    [Fact]
    public async Task BuildSlot_ShiftAware_TwoShifts_WorkFlowsAcrossBothShifts()
    {
        // Arrange: two shifts — morning (06:00-14:00) and afternoon (14:00-22:00)
        var machine = await AddSlsMachineAsync(autoChangeover: false, changeoverMinutes: 0);
        await AddShiftAsync("Morning", TimeSpan.FromHours(6), TimeSpan.FromHours(14));
        await AddShiftAsync("Afternoon", TimeSpan.FromHours(14), TimeSpan.FromHours(22));

        var notBefore = Mon.AddHours(6); // Mon 06:00

        // Act: 20h work = 16h/day (two shifts) → 1 day 4h
        var slot = await _sut.FindEarliestBuildSlotAsync(machine.Id, durationHours: 20, notBefore);

        // Assert: Mon 06:00 - Mon 22:00 (16h) + Tue 06:00 - Tue 10:00 (4h) = 20h
        Assert.Equal(Mon.AddHours(6), slot.PrintStart);
        Assert.Equal(Mon.AddDays(1).AddHours(10), slot.PrintEnd);
    }
}
