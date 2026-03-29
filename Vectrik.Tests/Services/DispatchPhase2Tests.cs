using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Hubs;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services;
using Vectrik.Services.Platform;
using Vectrik.Tests.Helpers;

namespace Vectrik.Tests.Services;

// ══════════════════════════════════════════════════════════════
// Stubs for Phase 2 tests
// ══════════════════════════════════════════════════════════════

internal sealed class StubDispatchNotifier : IDispatchNotifier
{
    public List<string> UrgentMessages { get; } = new();
    public int CreatedCount { get; private set; }
    public int StatusChangedCount { get; private set; }
    public int ReprioritizedCount { get; private set; }

    public Task SendDispatchCreatedAsync(string tenantCode, SetupDispatch dispatch) { CreatedCount++; return Task.CompletedTask; }
    public Task SendDispatchStatusChangedAsync(string tenantCode, SetupDispatch dispatch) { StatusChangedCount++; return Task.CompletedTask; }
    public Task SendQueueReprioritizedAsync(string tenantCode, int machineId) { ReprioritizedCount++; return Task.CompletedTask; }
    public Task SendUrgentDispatchAsync(string tenantCode, int machineId, string message) { UrgentMessages.Add(message); return Task.CompletedTask; }
    public Task SendChangeoverCountdownAsync(string tenantCode, int machineId, int minutesRemaining) => Task.CompletedTask;
}

internal sealed class StubServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType) => null;
}

internal sealed class StubDispatchLearningService : IDispatchLearningService
{
    public int ProcessedCount { get; private set; }
    public Task ProcessCompletedDispatchAsync(int dispatchId) { ProcessedCount++; return Task.CompletedTask; }
    public Task RecalculateProficiencyLevelsAsync(int machineId) => Task.CompletedTask;
    public Task<int?> SuggestBestOperatorAsync(int machineId, int? machineProgramId = null) => Task.FromResult<int?>(null);
    public Task<List<OperatorSetupProfile>> GetMachineProfilesAsync(int machineId) => Task.FromResult(new List<OperatorSetupProfile>());
    public Task<List<OperatorSetupProfile>> GetOperatorProfilesAsync(int userId) => Task.FromResult(new List<OperatorSetupProfile>());
}

internal sealed class StubTenantContext : ITenantContext
{
    public string TenantCode => "test";
    public string CompanyName => "Test Company";
    public bool IsSuperAdmin => false;
}

internal sealed class StubShiftManagementService : IShiftManagementService
{
    private readonly List<OperatingShift> _shifts;

    public StubShiftManagementService(List<OperatingShift>? shifts = null)
    {
        _shifts = shifts ?? new List<OperatingShift>
        {
            new() { Id = 1, Name = "Day Shift", StartTime = TimeSpan.FromHours(7), EndTime = TimeSpan.FromHours(17),
                     DaysOfWeek = "Mon,Tue,Wed,Thu,Fri", IsActive = true }
        };
    }

    public Task<List<OperatingShift>> GetAllShiftsAsync() => Task.FromResult(_shifts);
    public Task<OperatingShift?> GetShiftAsync(int id) => Task.FromResult(_shifts.FirstOrDefault(s => s.Id == id));
    public Task<OperatingShift> CreateShiftAsync(OperatingShift shift) { _shifts.Add(shift); return Task.FromResult(shift); }
    public Task UpdateShiftAsync(OperatingShift shift) => Task.CompletedTask;
    public Task DeleteShiftAsync(int id) => Task.CompletedTask;
    public Task<List<OperatingShift>> GetEffectiveShiftsForMachineAsync(int machineId) => Task.FromResult(_shifts);
    public Task SetMachineShiftsAsync(int machineId, List<int> shiftIds) => Task.CompletedTask;
    public Task SetUserShiftsAsync(int userId, List<int> shiftIds, string? assignedBy = null) => Task.CompletedTask;
    public Task<List<UserShiftAssignment>> GetUserShiftsAsync(int userId) => Task.FromResult(new List<UserShiftAssignment>());
    public Task<Dictionary<int, List<OperatingShift>>> GetMachineShiftMapAsync(IEnumerable<int> machineIds)
        => Task.FromResult(machineIds.ToDictionary(id => id, _ => _shifts));
}

internal sealed class StubBuildAdvisorService : IBuildAdvisorService
{
    public List<DemandSummary> Demand { get; set; } = new();

    public Task<BuildRecommendation> RecommendNextBuildAsync(int machineId, DateTime? startAfter = null)
        => Task.FromResult(new BuildRecommendation(machineId, "Machine", default!, default!, "", new()));

    public Task<List<DemandSummary>> GetAggregateDemandAsync() => Task.FromResult(Demand);

    public Task<PlateComposition> OptimizePlateAsync(int machineId, DateTime slotStart, List<DemandSummary> demand, int maxPartTypes = 4, int? forcePrimaryPartId = null)
        => Task.FromResult(new PlateComposition(new(), 1, 24, true, DateTime.UtcNow.AddHours(24), true));

    public Task<BottleneckReport> AnalyzeBottlenecksAsync(DateTime horizonStart, DateTime horizonEnd)
        => Task.FromResult(new BottleneckReport(new(), new(), new()));

    public Task<List<MachineAvailabilitySummary>> GetMachineAvailabilitySummaryAsync()
        => Task.FromResult(new List<MachineAvailabilitySummary>());

    public Task<DateTime?> EstimateCompletionDateAsync(int partId, int quantity)
        => Task.FromResult<DateTime?>(null);
}

// ══════════════════════════════════════════════════════════════
// Helper to create common test fixtures
// ══════════════════════════════════════════════════════════════

internal static class DispatchTestFixtures
{
    internal static (TenantDbContext db, ISetupDispatchService svc, StubDispatchNotifier notifier) CreateDispatchService()
    {
        var db = TestDbContextFactory.Create();
        var notifier = new StubDispatchNotifier();
        var numService = new StubNumberSequenceService();
        var tenant = new StubTenantContext();
        var serviceProvider = new StubServiceProvider();
        var svc = new SetupDispatchService(db, numService, notifier, tenant, serviceProvider);
        return (db, svc, notifier);
    }

    internal static Machine CreateSlsMachine(TenantDbContext db, string name = "EOS M4 #1")
    {
        var machine = new Machine
        {
            Name = name, IsActive = true, IsAvailableForScheduling = true,
            IsAdditiveMachine = true, AutoChangeoverEnabled = true,
            BuildPlateCapacity = 2, ChangeoverMinutes = 30, OperatorUnloadMinutes = 90,
            Status = MachineStatus.Idle
        };
        db.Machines.Add(machine);
        db.SaveChanges();
        return machine;
    }

    internal static MachineProgram CreateProgram(TenantDbContext db, int machineId, string name = "Program-001")
    {
        var program = new MachineProgram
        {
            MachineId = machineId, Name = name, ProgramNumber = name,
            ProgramType = ProgramType.BuildPlate, SetupTimeMinutes = 30,
            ScheduleStatus = ProgramScheduleStatus.None
        };
        db.MachinePrograms.Add(program);
        db.SaveChanges();
        return program;
    }
}

// ══════════════════════════════════════════════════════════════
// Changeover Dispatch Tests
// ══════════════════════════════════════════════════════════════

public class ChangeoverDispatchServiceTests
{
    [Fact]
    public async Task CreateOrUpdate_CreatesDispatch_ForAutoChangeoverMachine()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var shiftService = new StubShiftManagementService();
        var tenant = new StubTenantContext();

        var changeoverSvc = new ChangeoverDispatchService(db, svc, shiftService, notifier, tenant);
        var result = await changeoverSvc.CreateOrUpdateChangeoverDispatchAsync(machine.Id, DateTime.UtcNow.AddHours(2));

        Assert.NotNull(result);
        Assert.Equal(DispatchType.Changeover, result!.DispatchType);
        Assert.Equal(machine.Id, result.MachineId);
    }

    [Fact]
    public async Task CreateOrUpdate_ReturnsNull_WhenAutoChangeoverDisabled()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        machine.AutoChangeoverEnabled = false;
        db.SaveChanges();

        var shiftService = new StubShiftManagementService();
        var tenant = new StubTenantContext();

        var changeoverSvc = new ChangeoverDispatchService(db, svc, shiftService, notifier, tenant);
        var result = await changeoverSvc.CreateOrUpdateChangeoverDispatchAsync(machine.Id, DateTime.UtcNow.AddHours(2));

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateOrUpdate_UpdatesExisting_InsteadOfDuplicate()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var shiftService = new StubShiftManagementService();
        var tenant = new StubTenantContext();

        var changeoverSvc = new ChangeoverDispatchService(db, svc, shiftService, notifier, tenant);

        await changeoverSvc.CreateOrUpdateChangeoverDispatchAsync(machine.Id, DateTime.UtcNow.AddHours(2));
        await changeoverSvc.CreateOrUpdateChangeoverDispatchAsync(machine.Id, DateTime.UtcNow.AddHours(1));

        var all = await svc.GetActiveDispatchesByTypeAsync(DispatchType.Changeover);
        Assert.Single(all.Where(d => d.MachineId == machine.Id));
    }

    [Theory]
    [InlineData(180, 50, 75)]  // 3 hours remaining → normal/elevated range
    [InlineData(45, 70, 90)]   // 45 min remaining → high
    [InlineData(15, 90, 100)]  // 15 min remaining → urgent
    public void CalculatePriority_EscalatesCorrectly(double minutesRemaining, int minExpected, int maxExpected)
    {
        var shiftEnd = DateTime.UtcNow.AddMinutes(minutesRemaining);
        var dayName = DateTime.UtcNow.DayOfWeek switch
        {
            DayOfWeek.Monday => "Mon", DayOfWeek.Tuesday => "Tue", DayOfWeek.Wednesday => "Wed",
            DayOfWeek.Thursday => "Thu", DayOfWeek.Friday => "Fri",
            DayOfWeek.Saturday => "Sat", DayOfWeek.Sunday => "Sun", _ => ""
        };

        var shifts = new List<OperatingShift>
        {
            new() { Id = 1, Name = "Test", StartTime = DateTime.UtcNow.AddHours(-4).TimeOfDay,
                     EndTime = shiftEnd.TimeOfDay, DaysOfWeek = dayName, IsActive = true }
        };

        var priority = ChangeoverDispatchService.CalculateChangeoverPriority(
            DateTime.UtcNow, shifts, out _, out _);

        Assert.InRange(priority, minExpected, maxExpected);
    }

    [Fact]
    public void CalculatePriority_Returns100_WhenNoActiveShift()
    {
        var shifts = new List<OperatingShift>(); // No shifts at all
        var priority = ChangeoverDispatchService.CalculateChangeoverPriority(
            DateTime.UtcNow, shifts, out var reason, out _);

        Assert.Equal(100, priority);
        Assert.Contains("CRITICAL", reason);
    }

    [Fact]
    public async Task EscalatePriorities_SendsUrgent_WhenPriorityReaches95()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        // Create an empty-shift scenario so all changeovers become critical
        var shiftService = new StubShiftManagementService(new List<OperatingShift>());
        var tenant = new StubTenantContext();

        // Seed a changeover dispatch manually
        var dispatch = await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Changeover);

        var changeoverSvc = new ChangeoverDispatchService(db, svc, shiftService, notifier, tenant);
        await changeoverSvc.EscalateChangeoverPrioritiesAsync();

        Assert.NotEmpty(notifier.UrgentMessages);
    }
}

// ══════════════════════════════════════════════════════════════
// Print Start Dispatch Tests
// ══════════════════════════════════════════════════════════════

public class PrintStartDispatchServiceTests
{
    [Fact]
    public async Task CreatePrintStart_HasPrePrintChecklist()
    {
        var (db, svc, _) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        var tenant = new StubTenantContext();

        var printStartSvc = new PrintStartDispatchService(db, svc, tenant);
        var dispatch = await printStartSvc.CreatePrintStartDispatchAsync(machine.Id, program.Id);

        Assert.Equal(DispatchType.PrintStart, dispatch.DispatchType);

        // Re-read from DB to get PrePrintChecklistJson
        var entity = await db.SetupDispatches.FindAsync(dispatch.Id);
        Assert.NotNull(entity!.PrePrintChecklistJson);

        var items = JsonSerializer.Deserialize<List<SignOffChecklistItem>>(entity.PrePrintChecklistJson!);
        Assert.Equal(5, items!.Count);
        Assert.All(items, i => Assert.True(i.Required));
    }

    [Fact]
    public void DefaultPrePrintChecklist_Has5RequiredItems()
    {
        var (db, svc, _) = DispatchTestFixtures.CreateDispatchService();
        var tenant = new StubTenantContext();
        var printStartSvc = new PrintStartDispatchService(db, svc, tenant);

        var checklist = printStartSvc.GetDefaultPrePrintChecklist();
        Assert.Equal(5, checklist.Count);
        Assert.All(checklist, i => Assert.True(i.Required));
    }

    [Fact]
    public async Task CompletePrintStart_SetsPrintingStatus()
    {
        var (db, svc, _) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        var tenant = new StubTenantContext();

        var printStartSvc = new PrintStartDispatchService(db, svc, tenant);
        var dispatch = await printStartSvc.CreatePrintStartDispatchAsync(machine.Id, program.Id);

        // Sign off all checklist items
        var entity = await db.SetupDispatches.FindAsync(dispatch.Id);
        var items = JsonSerializer.Deserialize<List<SignOffChecklistItem>>(entity!.PrePrintChecklistJson!);
        foreach (var item in items!) item.SignedOff = true;
        entity.PrePrintChecklistJson = JsonSerializer.Serialize(items);
        await db.SaveChangesAsync();

        var completed = await printStartSvc.CompletePrintStartAsync(dispatch.Id, 1);

        Assert.Equal(DispatchStatus.Completed, completed.Status);
        var updatedProgram = await db.MachinePrograms.FindAsync(program.Id);
        Assert.Equal(ProgramScheduleStatus.Printing, updatedProgram!.ScheduleStatus);
        Assert.NotNull(updatedProgram.PrintStartedAt);
    }

    [Fact]
    public async Task CompletePrintStart_Throws_WhenChecklistIncomplete()
    {
        var (db, svc, _) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        var tenant = new StubTenantContext();

        var printStartSvc = new PrintStartDispatchService(db, svc, tenant);
        var dispatch = await printStartSvc.CreatePrintStartDispatchAsync(machine.Id, program.Id);

        // Don't sign off — should throw
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => printStartSvc.CompletePrintStartAsync(dispatch.Id, 1));
    }
}

// ══════════════════════════════════════════════════════════════
// Print Completion + Inspection Tests
// ══════════════════════════════════════════════════════════════

public class PrintCompletionServiceTests
{
    [Fact]
    public async Task CreatePrintCompletion_HasInspectionChecklist()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        var tenant = new StubTenantContext();

        var completionSvc = new PrintCompletionService(db, svc, notifier, tenant);
        var dispatch = await completionSvc.CreatePrintCompletionDispatchAsync(machine.Id, program.Id);

        Assert.Equal(DispatchType.Teardown, dispatch.DispatchType);

        var entity = await db.SetupDispatches.FindAsync(dispatch.Id);
        Assert.NotNull(entity!.InspectionChecklistJson);

        var items = JsonSerializer.Deserialize<List<InspectionChecklistItem>>(entity.InspectionChecklistJson!);
        Assert.Equal(6, items!.Count);
        Assert.Equal(5, items.Count(i => i.Required));
        Assert.Single(items.Where(i => !i.Required)); // Photos are optional
    }

    [Fact]
    public void DefaultSlsInspectionChecklist_Has6Items()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var tenant = new StubTenantContext();
        var completionSvc = new PrintCompletionService(db, svc, notifier, tenant);

        var checklist = completionSvc.GetDefaultSlsInspectionChecklist();
        Assert.Equal(6, checklist.Count);
        Assert.Equal(5, checklist.Count(i => i.Required));
    }

    [Fact]
    public async Task CompleteInspection_CompletesWhenAllPass()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        var tenant = new StubTenantContext();

        var completionSvc = new PrintCompletionService(db, svc, notifier, tenant);
        var dispatch = await completionSvc.CreatePrintCompletionDispatchAsync(machine.Id, program.Id);

        // Start the dispatch first
        await svc.StartDispatchAsync(dispatch.Id);

        // Mark all items inspected and passed
        var items = completionSvc.GetDefaultSlsInspectionChecklist();
        foreach (var item in items) { item.Inspected = true; item.Passed = true; }
        var json = JsonSerializer.Serialize(items);

        // Also update the entity's InspectionChecklistJson so gate passes
        var entity = await db.SetupDispatches.FindAsync(dispatch.Id);
        entity!.InspectionChecklistJson = json;
        await db.SaveChangesAsync();

        var completed = await completionSvc.CompleteInspectionAsync(dispatch.Id, json, 1);

        Assert.Equal(DispatchStatus.Completed, completed.Status);
        var updatedProgram = await db.MachinePrograms.FindAsync(program.Id);
        Assert.Equal(ProgramScheduleStatus.Completed, updatedProgram!.ScheduleStatus);
    }

    [Fact]
    public async Task CompleteInspection_BlocksWhenItemFails()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        var tenant = new StubTenantContext();

        var completionSvc = new PrintCompletionService(db, svc, notifier, tenant);
        var dispatch = await completionSvc.CreatePrintCompletionDispatchAsync(machine.Id, program.Id);
        await svc.StartDispatchAsync(dispatch.Id);

        var items = completionSvc.GetDefaultSlsInspectionChecklist();
        foreach (var item in items) { item.Inspected = true; item.Passed = true; }
        items[0].Passed = false; // Fail the first item
        items[0].FailureNotes = "Warping detected";
        var json = JsonSerializer.Serialize(items);

        var result = await completionSvc.CompleteInspectionAsync(dispatch.Id, json, 1);

        Assert.Equal(DispatchStatus.Blocked, result.Status);
        Assert.NotEmpty(notifier.UrgentMessages);
    }

    [Fact]
    public async Task CompleteInspection_Throws_WhenRequiredItemNotInspected()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        var tenant = new StubTenantContext();

        var completionSvc = new PrintCompletionService(db, svc, notifier, tenant);
        var dispatch = await completionSvc.CreatePrintCompletionDispatchAsync(machine.Id, program.Id);
        await svc.StartDispatchAsync(dispatch.Id);

        // Leave all items uninspected
        var items = completionSvc.GetDefaultSlsInspectionChecklist();
        var json = JsonSerializer.Serialize(items);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => completionSvc.CompleteInspectionAsync(dispatch.Id, json, 1));
    }

    [Fact]
    public async Task CreatePrintCompletion_TransitionsToPostPrint()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        program.ScheduleStatus = ProgramScheduleStatus.Printing;
        await db.SaveChangesAsync();
        var tenant = new StubTenantContext();

        var completionSvc = new PrintCompletionService(db, svc, notifier, tenant);
        await completionSvc.CreatePrintCompletionDispatchAsync(machine.Id, program.Id);

        var updated = await db.MachinePrograms.FindAsync(program.Id);
        Assert.Equal(ProgramScheduleStatus.PostPrint, updated!.ScheduleStatus);
        Assert.NotNull(updated.PrintCompletedAt);
    }
}

// ══════════════════════════════════════════════════════════════
// Dispatch Scoring Engine Tests
// ══════════════════════════════════════════════════════════════

public class DispatchScoringServiceTests
{
    [Fact]
    public async Task Score_SameProgramChangeover_Scores100()
    {
        var db = TestDbContextFactory.Create();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        machine.CurrentProgramId = program.Id;
        await db.SaveChangesAsync();

        var scoringSvc = new DispatchScoringService(db);
        var dispatch = new SetupDispatch
        {
            Id = 1, MachineId = machine.Id, MachineProgramId = program.Id,
            EstimatedSetupMinutes = 30
        };

        var score = await scoringSvc.ScoreDispatchAsync(dispatch);

        Assert.Equal(100, score.ChangeoverScore);
    }

    [Fact]
    public async Task Score_FullChangeover_ScoresLowerChangeover()
    {
        var db = TestDbContextFactory.Create();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var currentProgram = DispatchTestFixtures.CreateProgram(db, machine.Id, "Current");
        var targetProgram = DispatchTestFixtures.CreateProgram(db, machine.Id, "Target");
        machine.CurrentProgramId = currentProgram.Id;
        await db.SaveChangesAsync();

        var scoringSvc = new DispatchScoringService(db);
        var dispatch = new SetupDispatch
        {
            Id = 1, MachineId = machine.Id, MachineProgramId = targetProgram.Id,
            EstimatedSetupMinutes = 30, EstimatedChangeoverMinutes = 30
        };

        var score = await scoringSvc.ScoreDispatchAsync(dispatch);

        Assert.True(score.ChangeoverScore < 100);
    }

    [Fact]
    public async Task Score_FinalScoreClamped0To100()
    {
        var db = TestDbContextFactory.Create();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        await db.SaveChangesAsync();

        var scoringSvc = new DispatchScoringService(db);
        var dispatch = new SetupDispatch
        {
            Id = 1, MachineId = machine.Id, EstimatedSetupMinutes = 30
        };

        var score = await scoringSvc.ScoreDispatchAsync(dispatch);

        Assert.InRange(score.FinalScore, 0, 100);
    }

    [Fact]
    public async Task Score_BreakdownJsonIsValid()
    {
        var db = TestDbContextFactory.Create();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var scoringSvc = new DispatchScoringService(db);
        var dispatch = new SetupDispatch { Id = 1, MachineId = machine.Id, EstimatedSetupMinutes = 30 };

        var score = await scoringSvc.ScoreDispatchAsync(dispatch);

        Assert.NotEmpty(score.ScoreBreakdownJson);
        var doc = JsonDocument.Parse(score.ScoreBreakdownJson);
        Assert.True(doc.RootElement.TryGetProperty("finalScore", out _));
        Assert.True(doc.RootElement.TryGetProperty("dueDateScore", out _));
        Assert.True(doc.RootElement.TryGetProperty("changeoverScore", out _));
    }

    [Fact]
    public async Task ScoreAndRank_ReturnsDescendingOrder()
    {
        var db = TestDbContextFactory.Create();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var program = DispatchTestFixtures.CreateProgram(db, machine.Id);
        machine.CurrentProgramId = program.Id;
        await db.SaveChangesAsync();

        var scoringSvc = new DispatchScoringService(db);

        var dispatches = new List<SetupDispatch>
        {
            new() { Id = 1, MachineId = machine.Id, MachineProgramId = program.Id, EstimatedSetupMinutes = 30 },
            new() { Id = 2, MachineId = machine.Id, EstimatedSetupMinutes = 60, EstimatedChangeoverMinutes = 60 }
        };

        var ranked = await scoringSvc.ScoreAndRankAsync(dispatches);

        Assert.Equal(2, ranked.Count);
        Assert.True(ranked[0].Score.FinalScore >= ranked[1].Score.FinalScore);
    }

    [Fact]
    public async Task Score_MachineIdleBonus_AddsWhenNoInProgress()
    {
        var db = TestDbContextFactory.Create();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        var scoringSvc = new DispatchScoringService(db);
        var dispatch = new SetupDispatch { Id = 1, MachineId = machine.Id, EstimatedSetupMinutes = 30 };

        var score = await scoringSvc.ScoreDispatchAsync(dispatch);

        // Machine has no in-progress dispatch, so throughput should include idle bonus
        Assert.True(score.ThroughputScore >= 15);
    }
}

// ══════════════════════════════════════════════════════════════
// Dispatch Generation Service Tests
// ══════════════════════════════════════════════════════════════

public class DispatchGenerationServiceTests
{
    [Fact]
    public async Task Generate_RespectsAutoDispatchDisabled()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        // No DispatchConfiguration means AutoDispatchEnabled defaults to false
        var tenant = new StubTenantContext();
        var scoringSvc = new DispatchScoringService(db);

        var genSvc = new DispatchGenerationService(db, svc, scoringSvc, new StubDispatchLearningService(), notifier, tenant);
        var result = await genSvc.GenerateDispatchSuggestionsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task Approve_TransitionsDeferredToQueued()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var tenant = new StubTenantContext();
        var scoringSvc = new DispatchScoringService(db);

        // Create a deferred auto-dispatch
        var dispatch = await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Setup);
        var entity = await db.SetupDispatches.FindAsync(dispatch.Id);
        entity!.IsAutoGenerated = true;
        entity.Status = DispatchStatus.Deferred;
        await db.SaveChangesAsync();

        var genSvc = new DispatchGenerationService(db, svc, scoringSvc, new StubDispatchLearningService(), notifier, tenant);
        var approved = await genSvc.ApproveAutoDispatchAsync(dispatch.Id, 1);

        Assert.Equal(DispatchStatus.Queued, approved.Status);
    }

    [Fact]
    public async Task Reject_CancelsDispatch()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var tenant = new StubTenantContext();
        var scoringSvc = new DispatchScoringService(db);

        var dispatch = await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Setup);

        var genSvc = new DispatchGenerationService(db, svc, scoringSvc, new StubDispatchLearningService(), notifier, tenant);
        var rejected = await genSvc.RejectAutoDispatchAsync(dispatch.Id, 1, "Not needed");

        Assert.Equal(DispatchStatus.Cancelled, rejected.Status);
    }

    [Fact]
    public async Task Generate_RespectsMaxQueueDepth()
    {
        var (db, svc, notifier) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        // Enable auto-dispatch with max queue depth of 1
        db.DispatchConfigurations.Add(new DispatchConfiguration
        {
            MachineId = machine.Id, AutoDispatchEnabled = true, MaxQueueDepth = 1
        });
        await db.SaveChangesAsync();

        // Already have one dispatch queued
        await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Setup);

        var tenant = new StubTenantContext();
        var scoringSvc = new DispatchScoringService(db);
        var genSvc = new DispatchGenerationService(db, svc, scoringSvc, new StubDispatchLearningService(), notifier, tenant);

        var result = await genSvc.GenerateDispatchSuggestionsAsync(machine.Id);

        Assert.Empty(result); // Queue is full
    }
}

// ══════════════════════════════════════════════════════════════
// SetupDispatchService Phase 2 Extension Tests
// ══════════════════════════════════════════════════════════════

public class SetupDispatchServicePhase2Tests
{
    [Fact]
    public async Task GetActiveDispatchesByType_FiltersCorrectly()
    {
        var (db, svc, _) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Setup);
        await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Changeover);
        await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Changeover);

        var changeovers = await svc.GetActiveDispatchesByTypeAsync(DispatchType.Changeover);

        Assert.Equal(2, changeovers.Count);
        Assert.All(changeovers, d => Assert.Equal(DispatchType.Changeover, d.DispatchType));
    }

    [Fact]
    public async Task UpdateDispatchPriority_UpdatesFieldsCorrectly()
    {
        var (db, svc, _) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var dispatch = await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Setup);

        await svc.UpdateDispatchPriorityAsync(dispatch.Id, 95, "Urgent", "{\"test\":true}");

        var updated = await svc.GetByIdAsync(dispatch.Id);
        Assert.Equal(95, updated!.Priority);
        Assert.Equal("Urgent", updated.PriorityReason);
        Assert.Equal("{\"test\":true}", updated.ScoreBreakdownJson);
    }

    [Fact]
    public async Task UpdateDispatchPriority_ClampsTo1_100()
    {
        var (db, svc, _) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var dispatch = await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Setup);

        await svc.UpdateDispatchPriorityAsync(dispatch.Id, 150);
        var updated = await db.SetupDispatches.FindAsync(dispatch.Id);
        Assert.Equal(100, updated!.Priority);

        await svc.UpdateDispatchPriorityAsync(dispatch.Id, -10);
        updated = await db.SetupDispatches.FindAsync(dispatch.Id);
        Assert.Equal(1, updated!.Priority);
    }

    [Fact]
    public async Task GetDispatchesByRole_FiltersCorrectly()
    {
        var (db, svc, _) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);
        var role = new OperatorRole { Name = "SLS Engineer", Slug = "sls-engineer", IsActive = true };
        db.OperatorRoles.Add(role);
        await db.SaveChangesAsync();

        var dispatch = await svc.CreateManualDispatchAsync(machine.Id, DispatchType.PlateLayout);
        var entity = await db.SetupDispatches.FindAsync(dispatch.Id);
        entity!.TargetRoleId = role.Id;
        await db.SaveChangesAsync();

        var roleDispatches = await svc.GetDispatchesByRoleAsync(role.Id);
        Assert.Single(roleDispatches);
        Assert.Equal(DispatchType.PlateLayout, roleDispatches[0].DispatchType);
    }

    [Fact]
    public async Task CompleteDispatch_GatesOnInspectionChecklist()
    {
        var (db, svc, _) = DispatchTestFixtures.CreateDispatchService();
        var machine = DispatchTestFixtures.CreateSlsMachine(db);

        var dispatch = await svc.CreateManualDispatchAsync(machine.Id, DispatchType.Teardown);

        // Set inspection checklist with uninspected required items
        var items = new List<InspectionChecklistItem>
        {
            new() { StepId = 1, Title = "Visual check", Required = true, Inspected = false }
        };
        var entity = await db.SetupDispatches.FindAsync(dispatch.Id);
        entity!.InspectionChecklistJson = JsonSerializer.Serialize(items);
        await db.SaveChangesAsync();

        await svc.StartDispatchAsync(dispatch.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CompleteDispatchAsync(dispatch.Id));
    }
}

// ══════════════════════════════════════════════════════════════
// InspectionChecklistItem Model Tests
// ══════════════════════════════════════════════════════════════

public class InspectionChecklistItemTests
{
    [Fact]
    public void RoundTrip_JsonSerialization()
    {
        var items = new List<InspectionChecklistItem>
        {
            new() { StepId = 1, Title = "Visual check", Required = true, Inspected = true, Passed = true,
                     InspectedBy = "Operator A", InspectedAt = DateTime.UtcNow },
            new() { StepId = 2, Title = "Photos", Required = false, Inspected = false }
        };

        var json = JsonSerializer.Serialize(items);
        var deserialized = JsonSerializer.Deserialize<List<InspectionChecklistItem>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Count);
        Assert.True(deserialized[0].Passed);
        Assert.False(deserialized[1].Inspected);
        Assert.Null(deserialized[1].Passed);
    }
}
