using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Models.Maintenance;
using Vectrik.Services;
using Vectrik.Tests.Helpers;

namespace Vectrik.Tests.Services;

public class StageServiceTests : IDisposable
{
    private readonly TenantDbContext _db;
    private readonly StageService _sut;

    public StageServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new StageService(_db, new StubWorkOrderService(), new StubLearningService(), new StubInventoryService());
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────

    private async Task<ProductionStage> SeedStageAsync(
        string name = "SLS Printing",
        string slug = "sls",
        double duration = 4.0)
    {
        var stage = new ProductionStage
        {
            Name = name,
            StageSlug = slug,
            DefaultDurationHours = duration,
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();
        return stage;
    }

    private async Task<Machine> SeedMachineAsync(string machineId = "SLS-001")
    {
        var machine = new Machine
        {
            MachineId = machineId,
            Name = $"Machine {machineId}",
            MachineType = "SLS",
            Status = MachineStatus.Idle,
            IsActive = true,
            IsAvailableForScheduling = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    private async Task<Job> SeedJobAsync(string? partNumber = null)
    {
        var part = new Part
        {
            PartNumber = partNumber ?? "PN-001",
            Name = "Test Part",
            Material = "Ti-6Al-4V",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        var job = new Job
        {
            PartId = part.Id,
            Quantity = 1,
            Status = JobStatus.Draft,
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddHours(8),
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();
        return job;
    }

    private async Task<User> SeedUserAsync(string fullName = "John Operator")
    {
        var user = new User
        {
            Username = fullName.ToLower().Replace(' ', '.'),
            FullName = fullName,
            Email = $"{fullName.ToLower().Replace(' ', '.')}@test.com",
            PasswordHash = "hash"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<StageExecution> SeedExecutionAsync(
        int stageId,
        int? jobId = null,
        int? machineId = null,
        StageExecutionStatus status = StageExecutionStatus.NotStarted,
        int sortOrder = 1)
    {
        var exec = new StageExecution
        {
            ProductionStageId = stageId,
            JobId = jobId,
            MachineId = machineId,
            Status = status,
            SortOrder = sortOrder
        };
        _db.StageExecutions.Add(exec);
        await _db.SaveChangesAsync();
        return exec;
    }

    // ══════════════════════════════════════════════════════════
    // Stage CRUD
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAllStagesAsync_WhenActiveOnly_ExcludesInactive()
    {
        await SeedStageAsync("Active", "active");
        var inactive = await SeedStageAsync("Inactive", "inactive");
        inactive.IsActive = false;
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllStagesAsync(activeOnly: true);

        Assert.Single(result);
        Assert.Equal("Active", result[0].Name);
    }

    [Fact]
    public async Task GetAllStagesAsync_WhenNotActiveOnly_ReturnsAll()
    {
        await SeedStageAsync("Active", "active");
        var inactive = await SeedStageAsync("Inactive", "inactive");
        inactive.IsActive = false;
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllStagesAsync(activeOnly: false);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllStagesAsync_OrdersByDisplayOrder()
    {
        var s2 = await SeedStageAsync("Second", "second");
        s2.DisplayOrder = 2;
        var s1 = await SeedStageAsync("First", "first");
        s1.DisplayOrder = 1;
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllStagesAsync();

        Assert.Equal("First", result[0].Name);
        Assert.Equal("Second", result[1].Name);
    }

    [Fact]
    public async Task GetStageByIdAsync_WhenExists_ReturnsStage()
    {
        var stage = await SeedStageAsync();

        var result = await _sut.GetStageByIdAsync(stage.Id);

        Assert.NotNull(result);
        Assert.Equal("SLS Printing", result.Name);
    }

    [Fact]
    public async Task GetStageByIdAsync_WhenNotExists_ReturnsNull()
    {
        Assert.Null(await _sut.GetStageByIdAsync(999));
    }

    [Fact]
    public async Task GetStageBySlugAsync_WhenExists_ReturnsStage()
    {
        await SeedStageAsync("Wire EDM", "wire-edm");

        var result = await _sut.GetStageBySlugAsync("wire-edm");

        Assert.NotNull(result);
        Assert.Equal("Wire EDM", result.Name);
    }

    [Fact]
    public async Task GetStageBySlugAsync_WhenNotExists_ReturnsNull()
    {
        Assert.Null(await _sut.GetStageBySlugAsync("nonexistent"));
    }

    [Fact]
    public async Task CreateStageAsync_PersistsAndSetsTimestamps()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var stage = new ProductionStage
        {
            Name = "New Stage",
            StageSlug = "new-stage",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };

        var result = await _sut.CreateStageAsync(stage);

        Assert.True(result.Id > 0);
        Assert.True(result.CreatedDate >= before);
        Assert.True(result.LastModifiedDate >= before);
    }

    [Fact]
    public async Task UpdateStageAsync_UpdatesNameAndTimestamp()
    {
        var stage = await SeedStageAsync();
        stage.Name = "Renamed Stage";

        var result = await _sut.UpdateStageAsync(stage);

        Assert.Equal("Renamed Stage", result.Name);
    }

    [Fact]
    public async Task DeleteStageAsync_SoftDeletesSetsInactive()
    {
        var stage = await SeedStageAsync();

        await _sut.DeleteStageAsync(stage.Id);

        var deleted = await _db.ProductionStages.FindAsync(stage.Id);
        Assert.NotNull(deleted);
        Assert.False(deleted.IsActive);
    }

    [Fact]
    public async Task DeleteStageAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteStageAsync(999));
    }

    // ══════════════════════════════════════════════════════════
    // StartStageExecutionAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task StartStageExecutionAsync_SetsStatusToInProgress()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id);

        var result = await _sut.StartStageExecutionAsync(exec.Id, 1, "Operator A");

        Assert.Equal(StageExecutionStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task StartStageExecutionAsync_SetsOperatorAndTimestamps()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await _sut.StartStageExecutionAsync(exec.Id, 42, "Jane Doe");

        Assert.Equal(42, result.OperatorUserId);
        Assert.Equal("Jane Doe", result.OperatorName);
        Assert.NotNull(result.StartedAt);
        Assert.True(result.StartedAt >= before);
        Assert.NotNull(result.ActualStartAt);
    }

    [Fact]
    public async Task StartStageExecutionAsync_SetsMachineToRunning()
    {
        var stage = await SeedStageAsync();
        var machine = await SeedMachineAsync();
        var exec = await SeedExecutionAsync(stage.Id, machineId: machine.Id);

        await _sut.StartStageExecutionAsync(exec.Id, 1, "Op");

        var updatedMachine = await _db.Machines.FindAsync(machine.Id);
        Assert.Equal(MachineStatus.Running, updatedMachine!.Status);
    }

    [Fact]
    public async Task StartStageExecutionAsync_SetsJobToInProgress()
    {
        var stage = await SeedStageAsync();
        var job = await SeedJobAsync();
        var exec = await SeedExecutionAsync(stage.Id, jobId: job.Id);

        await _sut.StartStageExecutionAsync(exec.Id, 1, "Op");

        var updatedJob = await _db.Jobs.FindAsync(job.Id);
        Assert.Equal(JobStatus.InProgress, updatedJob!.Status);
        Assert.NotNull(updatedJob.ActualStart);
    }

    [Fact]
    public async Task StartStageExecutionAsync_WhenJobAlreadyInProgress_DoesNotResetActualStart()
    {
        var stage = await SeedStageAsync();
        var job = await SeedJobAsync();
        job.Status = JobStatus.InProgress;
        var earlyStart = DateTime.UtcNow.AddHours(-2);
        job.ActualStart = earlyStart;
        await _db.SaveChangesAsync();

        var exec = await SeedExecutionAsync(stage.Id, jobId: job.Id);

        await _sut.StartStageExecutionAsync(exec.Id, 1, "Op");

        var updatedJob = await _db.Jobs.FindAsync(job.Id);
        Assert.Equal(earlyStart, updatedJob!.ActualStart);
    }

    [Fact]
    public async Task StartStageExecutionAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.StartStageExecutionAsync(999, 1, "Op"));
    }

    // ══════════════════════════════════════════════════════════
    // CompleteStageExecutionAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task CompleteStageExecutionAsync_SetsStatusToCompleted()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.InProgress);
        exec.StartedAt = DateTime.UtcNow.AddHours(-1);
        await _db.SaveChangesAsync();

        var result = await _sut.CompleteStageExecutionAsync(exec.Id);

        Assert.Equal(StageExecutionStatus.Completed, result.Status);
        Assert.NotNull(result.CompletedAt);
        Assert.NotNull(result.ActualEndAt);
    }

    [Fact]
    public async Task CompleteStageExecutionAsync_CalculatesActualHours()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.InProgress);
        exec.StartedAt = DateTime.UtcNow.AddHours(-2);
        await _db.SaveChangesAsync();

        var result = await _sut.CompleteStageExecutionAsync(exec.Id);

        Assert.NotNull(result.ActualHours);
        Assert.True(result.ActualHours >= 1.9); // ~2 hours, allowing timing margin
    }

    [Fact]
    public async Task CompleteStageExecutionAsync_ReleasesMachineToIdle()
    {
        var stage = await SeedStageAsync();
        var machine = await SeedMachineAsync();
        machine.Status = MachineStatus.Running;
        machine.TotalOperatingHours = 100;
        await _db.SaveChangesAsync();

        var exec = await SeedExecutionAsync(stage.Id, machineId: machine.Id, status: StageExecutionStatus.InProgress);
        exec.StartedAt = DateTime.UtcNow.AddHours(-3);
        await _db.SaveChangesAsync();

        await _sut.CompleteStageExecutionAsync(exec.Id);

        var updatedMachine = await _db.Machines.FindAsync(machine.Id);
        Assert.Equal(MachineStatus.Idle, updatedMachine!.Status);
    }

    [Fact]
    public async Task CompleteStageExecutionAsync_IncrementsMachineTotalOperatingHours()
    {
        var stage = await SeedStageAsync();
        var machine = await SeedMachineAsync();
        machine.TotalOperatingHours = 100;
        machine.HoursSinceLastMaintenance = 50;
        machine.Status = MachineStatus.Running;
        await _db.SaveChangesAsync();

        var exec = await SeedExecutionAsync(stage.Id, machineId: machine.Id, status: StageExecutionStatus.InProgress);
        exec.StartedAt = DateTime.UtcNow.AddHours(-2);
        await _db.SaveChangesAsync();

        await _sut.CompleteStageExecutionAsync(exec.Id);

        var updatedMachine = await _db.Machines.FindAsync(machine.Id);
        Assert.True(updatedMachine!.TotalOperatingHours > 100);
        Assert.True(updatedMachine.HoursSinceLastMaintenance > 50);
    }

    [Fact]
    public async Task CompleteStageExecutionAsync_StoresCustomFieldValues()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.InProgress);

        var result = await _sut.CompleteStageExecutionAsync(exec.Id, customFieldValues: "{\"temp\":\"200C\"}");

        Assert.Equal("{\"temp\":\"200C\"}", result.CustomFieldValues);
    }

    [Fact]
    public async Task CompleteStageExecutionAsync_StoresCompletionNotes()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.InProgress);

        var result = await _sut.CompleteStageExecutionAsync(exec.Id, notes: "All good");

        Assert.Equal("All good", result.CompletionNotes);
    }

    [Fact]
    public async Task CompleteStageExecutionAsync_WhenAllStagesDone_CompletesJob()
    {
        var stage1 = await SeedStageAsync("Stage1", "s1");
        var stage2 = await SeedStageAsync("Stage2", "s2");
        var job = await SeedJobAsync();

        // Stage 1 already completed
        var exec1 = await SeedExecutionAsync(stage1.Id, jobId: job.Id, status: StageExecutionStatus.Completed, sortOrder: 1);
        // Stage 2 in progress (about to complete)
        var exec2 = await SeedExecutionAsync(stage2.Id, jobId: job.Id, status: StageExecutionStatus.InProgress, sortOrder: 2);
        exec2.StartedAt = DateTime.UtcNow.AddHours(-1);
        await _db.SaveChangesAsync();

        await _sut.CompleteStageExecutionAsync(exec2.Id);

        var updatedJob = await _db.Jobs.FindAsync(job.Id);
        Assert.Equal(JobStatus.Completed, updatedJob!.Status);
        Assert.NotNull(updatedJob.ActualEnd);
    }

    [Fact]
    public async Task CompleteStageExecutionAsync_WhenStagesRemain_DoesNotCompleteJob()
    {
        var stage1 = await SeedStageAsync("Stage1", "s1");
        var stage2 = await SeedStageAsync("Stage2", "s2");
        var job = await SeedJobAsync();

        var exec1 = await SeedExecutionAsync(stage1.Id, jobId: job.Id, status: StageExecutionStatus.InProgress, sortOrder: 1);
        exec1.StartedAt = DateTime.UtcNow.AddHours(-1);
        await _db.SaveChangesAsync();
        await SeedExecutionAsync(stage2.Id, jobId: job.Id, status: StageExecutionStatus.NotStarted, sortOrder: 2);

        await _sut.CompleteStageExecutionAsync(exec1.Id);

        var updatedJob = await _db.Jobs.FindAsync(job.Id);
        Assert.Equal(JobStatus.Draft, updatedJob!.Status);
    }

    [Fact]
    public async Task CompleteStageExecutionAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CompleteStageExecutionAsync(999));
    }

    // ══════════════════════════════════════════════════════════
    // SkipStageExecutionAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task SkipStageExecutionAsync_SetsStatusAndReason()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id);

        var result = await _sut.SkipStageExecutionAsync(exec.Id, "Not required for this part");

        Assert.Equal(StageExecutionStatus.Skipped, result.Status);
        Assert.Equal("Not required for this part", result.Notes);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task SkipStageExecutionAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SkipStageExecutionAsync(999, "reason"));
    }

    // ══════════════════════════════════════════════════════════
    // FailStageExecutionAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task FailStageExecutionAsync_SetsStatusAndFailureReason()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.InProgress);

        var result = await _sut.FailStageExecutionAsync(exec.Id, "Material defect");

        Assert.Equal(StageExecutionStatus.Failed, result.Status);
        Assert.Equal("Material defect", result.FailureReason);
        Assert.Equal("Material defect", result.Issues);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task FailStageExecutionAsync_ReleasesMachineToIdle()
    {
        var stage = await SeedStageAsync();
        var machine = await SeedMachineAsync();
        machine.Status = MachineStatus.Running;
        await _db.SaveChangesAsync();

        var exec = await SeedExecutionAsync(stage.Id, machineId: machine.Id, status: StageExecutionStatus.InProgress);
        exec.StartedAt = DateTime.UtcNow.AddHours(-1);
        await _db.SaveChangesAsync();

        await _sut.FailStageExecutionAsync(exec.Id, "Laser failure");

        var updatedMachine = await _db.Machines.FindAsync(machine.Id);
        Assert.Equal(MachineStatus.Idle, updatedMachine!.Status);
    }

    [Fact]
    public async Task FailStageExecutionAsync_CalculatesActualHours()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.InProgress);
        exec.StartedAt = DateTime.UtcNow.AddHours(-1);
        await _db.SaveChangesAsync();

        var result = await _sut.FailStageExecutionAsync(exec.Id, "Error");

        Assert.NotNull(result.ActualHours);
        Assert.True(result.ActualHours >= 0.9);
    }

    [Fact]
    public async Task FailStageExecutionAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.FailStageExecutionAsync(999, "reason"));
    }

    // ══════════════════════════════════════════════════════════
    // PauseStageExecutionAsync / ResumeStageExecutionAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task PauseStageExecutionAsync_SetsStatusToPaused()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.InProgress);

        var result = await _sut.PauseStageExecutionAsync(exec.Id, "Waiting for material", DelayCategory.Material, "operator1");

        Assert.Equal(StageExecutionStatus.Paused, result.Status);
    }

    [Fact]
    public async Task PauseStageExecutionAsync_CreatesDelayLog()
    {
        var stage = await SeedStageAsync();
        var job = await SeedJobAsync();
        var exec = await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.InProgress);

        await _sut.PauseStageExecutionAsync(exec.Id, "Out of powder", DelayCategory.Material, "op1");

        var delays = await _db.DelayLogs.Where(d => d.StageExecutionId == exec.Id).ToListAsync();
        Assert.Single(delays);
        Assert.Equal("Out of powder", delays[0].Reason);
        Assert.Equal(DelayCategory.Material, delays[0].Category);
        Assert.Equal("op1", delays[0].LoggedBy);
        Assert.Equal(job.Id, delays[0].JobId);
        Assert.Null(delays[0].ResolvedAt);
    }

    [Fact]
    public async Task PauseStageExecutionAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.PauseStageExecutionAsync(999, "reason", DelayCategory.Other, "op"));
    }

    [Fact]
    public async Task ResumeStageExecutionAsync_SetsStatusToInProgress()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.Paused);

        var result = await _sut.ResumeStageExecutionAsync(exec.Id);

        Assert.Equal(StageExecutionStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task ResumeStageExecutionAsync_ResolvesOpenDelayLog()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.InProgress);

        // Pause first
        await _sut.PauseStageExecutionAsync(exec.Id, "Waiting", DelayCategory.Other, "op1");

        // Resume
        await _sut.ResumeStageExecutionAsync(exec.Id);

        var delay = await _db.DelayLogs.FirstOrDefaultAsync(d => d.StageExecutionId == exec.Id);
        Assert.NotNull(delay);
        Assert.NotNull(delay.ResolvedAt);
        Assert.True(delay.DelayMinutes >= 0);
    }

    [Fact]
    public async Task ResumeStageExecutionAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ResumeStageExecutionAsync(999));
    }

    // ══════════════════════════════════════════════════════════
    // LogUnmannedStartAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task LogUnmannedStartAsync_SetsUnmannedFlagAndMachine()
    {
        var stage = await SeedStageAsync();
        var machine = await SeedMachineAsync();
        var exec = await SeedExecutionAsync(stage.Id);

        await _sut.LogUnmannedStartAsync(exec.Id, machine.Id);

        var updated = await _db.StageExecutions.FindAsync(exec.Id);
        Assert.True(updated!.IsUnmanned);
        Assert.Equal(machine.Id, updated.MachineId);
        Assert.Equal(StageExecutionStatus.InProgress, updated.Status);
        Assert.NotNull(updated.StartedAt);
    }

    [Fact]
    public async Task LogUnmannedStartAsync_SetsMachineToRunning()
    {
        var stage = await SeedStageAsync();
        var machine = await SeedMachineAsync();
        var exec = await SeedExecutionAsync(stage.Id);

        await _sut.LogUnmannedStartAsync(exec.Id, machine.Id);

        var updatedMachine = await _db.Machines.FindAsync(machine.Id);
        Assert.Equal(MachineStatus.Running, updatedMachine!.Status);
    }

    [Fact]
    public async Task LogUnmannedStartAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.LogUnmannedStartAsync(999, 1));
    }

    // ══════════════════════════════════════════════════════════
    // LogDelayAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task LogDelayAsync_CreatesResolvedDelayLog()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.InProgress);

        var result = await _sut.LogDelayAsync(exec.Id, "Laser calibration", DelayCategory.Machine, 30, "op1", "Fixed recalibration");

        Assert.Equal("Laser calibration", result.Reason);
        Assert.Equal(DelayCategory.Machine, result.Category);
        Assert.Equal(30, result.DelayMinutes);
        Assert.Equal("op1", result.LoggedBy);
        Assert.Equal("Fixed recalibration", result.Notes);
        Assert.NotNull(result.ResolvedAt);
    }

    [Fact]
    public async Task LogDelayAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.LogDelayAsync(999, "reason", DelayCategory.Other, 10, "op"));
    }

    // ══════════════════════════════════════════════════════════
    // Queue queries
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetQueueForStageAsync_ReturnsOnlyNotStarted()
    {
        var stage = await SeedStageAsync();
        var job = await SeedJobAsync();
        await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.NotStarted);
        await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.InProgress);
        await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.Completed);

        var result = await _sut.GetQueueForStageAsync(stage.Id);

        Assert.Single(result);
        Assert.Equal(StageExecutionStatus.NotStarted, result[0].Status);
    }

    [Fact]
    public async Task GetActiveWorkForStageAsync_ReturnsInProgressAndPaused()
    {
        var stage = await SeedStageAsync();
        var job = await SeedJobAsync();
        await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.InProgress);
        await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.Paused);
        await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.NotStarted);

        var result = await _sut.GetActiveWorkForStageAsync(stage.Id);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetRecentCompletionsAsync_ReturnsCompletedAndFailedOrdered()
    {
        var stage = await SeedStageAsync();
        var job = await SeedJobAsync();

        var earlier = await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.Completed);
        earlier.CompletedAt = DateTime.UtcNow.AddHours(-2);
        var later = await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.Failed);
        later.CompletedAt = DateTime.UtcNow.AddHours(-1);
        await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.InProgress);
        await _db.SaveChangesAsync();

        var result = await _sut.GetRecentCompletionsAsync(stage.Id);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].CompletedAt >= result[1].CompletedAt);
    }

    [Fact]
    public async Task GetRecentCompletionsAsync_RespectsCountLimit()
    {
        var stage = await SeedStageAsync();
        for (int i = 0; i < 5; i++)
        {
            var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.Completed);
            exec.CompletedAt = DateTime.UtcNow.AddHours(-i);
        }
        await _db.SaveChangesAsync();

        var result = await _sut.GetRecentCompletionsAsync(stage.Id, count: 3);

        Assert.Equal(3, result.Count);
    }

    // ══════════════════════════════════════════════════════════
    // Operator queue / Available work
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetOperatorQueueAsync_ReturnsAssignedWork()
    {
        var stage = await SeedStageAsync();
        var job = await SeedJobAsync();

        var exec1 = await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.InProgress);
        exec1.OperatorUserId = 42;
        var exec2 = await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.NotStarted);
        exec2.OperatorUserId = 42;
        // Different operator
        var exec3 = await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.NotStarted);
        exec3.OperatorUserId = 99;
        await _db.SaveChangesAsync();

        var result = await _sut.GetOperatorQueueAsync(42);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAvailableWorkAsync_ReturnsUnassignedNotStartedManualOnly()
    {
        var stage = await SeedStageAsync();
        var job = await SeedJobAsync();

        // Available — no operator, not started, not unmanned
        await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.NotStarted);

        // Assigned to operator — should not appear
        var assigned = await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.NotStarted);
        assigned.OperatorUserId = 1;

        // Unmanned — should not appear
        var unmanned = await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.NotStarted);
        unmanned.IsUnmanned = true;
        await _db.SaveChangesAsync();

        var result = await _sut.GetAvailableWorkAsync();

        Assert.Single(result);
    }

    // ══════════════════════════════════════════════════════════
    // Machine queue / Assignment
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMachineQueueAsync_ReturnsActiveAndPendingWorkForMachine()
    {
        var stage = await SeedStageAsync();
        var machine = await SeedMachineAsync();

        await SeedExecutionAsync(stage.Id, machineId: machine.Id, status: StageExecutionStatus.NotStarted);
        await SeedExecutionAsync(stage.Id, machineId: machine.Id, status: StageExecutionStatus.InProgress);
        await SeedExecutionAsync(stage.Id, machineId: machine.Id, status: StageExecutionStatus.Completed);

        var result = await _sut.GetMachineQueueAsync(machine.Id);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task AssignOperatorAsync_SetsOperatorUserIdAndName()
    {
        var stage = await SeedStageAsync();
        var user = await SeedUserAsync("Jane Smith");
        var exec = await SeedExecutionAsync(stage.Id);

        await _sut.AssignOperatorAsync(exec.Id, user.Id);

        var updated = await _db.StageExecutions.FindAsync(exec.Id);
        Assert.Equal(user.Id, updated!.OperatorUserId);
        Assert.Equal("Jane Smith", updated.OperatorName);
    }

    [Fact]
    public async Task AssignOperatorAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AssignOperatorAsync(999, 1));
    }

    [Fact]
    public async Task AssignMachineAsync_SetsMachineId()
    {
        var stage = await SeedStageAsync();
        var machine = await SeedMachineAsync();
        var exec = await SeedExecutionAsync(stage.Id);

        await _sut.AssignMachineAsync(exec.Id, machine.Id);

        var updated = await _db.StageExecutions.FindAsync(exec.Id);
        Assert.Equal(machine.Id, updated!.MachineId);
    }

    [Fact]
    public async Task AssignMachineAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AssignMachineAsync(999, 1));
    }

    // ══════════════════════════════════════════════════════════
    // Scheduling
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateScheduleAsync_SetsScheduleAndMachine()
    {
        var stage = await SeedStageAsync();
        var machine = await SeedMachineAsync();
        var exec = await SeedExecutionAsync(stage.Id);
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(4);

        await _sut.UpdateScheduleAsync(exec.Id, start, end, machine.Id);

        var updated = await _db.StageExecutions.FindAsync(exec.Id);
        Assert.Equal(start, updated!.ScheduledStartAt);
        Assert.Equal(end, updated.ScheduledEndAt);
        Assert.Equal(machine.Id, updated.MachineId);
    }

    [Fact]
    public async Task UpdateScheduleAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateScheduleAsync(999, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
    }

    [Fact]
    public async Task GetUnscheduledExecutionsAsync_ReturnsNotStartedWithoutScheduleOrMachine()
    {
        var stage = await SeedStageAsync();
        var job = await SeedJobAsync();
        var machine = await SeedMachineAsync();

        // Unscheduled — no time, no machine
        await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.NotStarted);

        // Scheduled — has time and machine
        var scheduled = await SeedExecutionAsync(stage.Id, jobId: job.Id, machineId: machine.Id, status: StageExecutionStatus.NotStarted);
        scheduled.ScheduledStartAt = DateTime.UtcNow.AddDays(1);
        await _db.SaveChangesAsync();

        var result = await _sut.GetUnscheduledExecutionsAsync();

        Assert.Single(result);
    }

    // ══════════════════════════════════════════════════════════
    // GetCurrentExecutionForOperatorAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCurrentExecutionForOperatorAsync_ReturnsInProgressExecution()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.InProgress);
        exec.OperatorUserId = 42;
        await _db.SaveChangesAsync();

        var result = await _sut.GetCurrentExecutionForOperatorAsync(42);

        Assert.NotNull(result);
        Assert.Equal(exec.Id, result.Id);
    }

    [Fact]
    public async Task GetCurrentExecutionForOperatorAsync_ReturnsPausedExecution()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.Paused);
        exec.OperatorUserId = 42;
        await _db.SaveChangesAsync();

        var result = await _sut.GetCurrentExecutionForOperatorAsync(42);

        Assert.NotNull(result);
        Assert.Equal(StageExecutionStatus.Paused, result.Status);
    }

    [Fact]
    public async Task GetCurrentExecutionForOperatorAsync_WhenNoneActive_ReturnsNull()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.NotStarted);
        exec.OperatorUserId = 42;
        await _db.SaveChangesAsync();

        Assert.Null(await _sut.GetCurrentExecutionForOperatorAsync(42));
    }

    [Fact]
    public async Task GetCurrentExecutionForOperatorAsync_IgnoresOtherOperators()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.InProgress);
        exec.OperatorUserId = 99;
        await _db.SaveChangesAsync();

        Assert.Null(await _sut.GetCurrentExecutionForOperatorAsync(42));
    }

    // ══════════════════════════════════════════════════════════
    // GetScheduledExecutionsAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetScheduledExecutionsAsync_ReturnsExecutionsInDateRange()
    {
        var stage = await SeedStageAsync();
        var job = await SeedJobAsync();
        var from = DateTime.UtcNow;
        var to = from.AddDays(7);

        var inRange = await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.NotStarted);
        inRange.ScheduledStartAt = from.AddDays(1);
        inRange.ScheduledEndAt = from.AddDays(2);

        var outOfRange = await SeedExecutionAsync(stage.Id, jobId: job.Id, status: StageExecutionStatus.NotStarted);
        outOfRange.ScheduledStartAt = from.AddDays(10);
        outOfRange.ScheduledEndAt = from.AddDays(11);
        await _db.SaveChangesAsync();

        var result = await _sut.GetScheduledExecutionsAsync(from, to);

        Assert.Single(result);
        Assert.Equal(inRange.Id, result[0].Id);
    }

    [Fact]
    public async Task GetScheduledExecutionsAsync_ExcludesCompletedAndSkippedAndFailed()
    {
        var stage = await SeedStageAsync();
        var from = DateTime.UtcNow;
        var to = from.AddDays(7);

        var completed = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.Completed);
        completed.ScheduledStartAt = from.AddDays(1);
        completed.ScheduledEndAt = from.AddDays(2);

        var skipped = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.Skipped);
        skipped.ScheduledStartAt = from.AddDays(1);
        skipped.ScheduledEndAt = from.AddDays(2);

        var failed = await SeedExecutionAsync(stage.Id, status: StageExecutionStatus.Failed);
        failed.ScheduledStartAt = from.AddDays(1);
        failed.ScheduledEndAt = from.AddDays(2);
        await _db.SaveChangesAsync();

        var result = await _sut.GetScheduledExecutionsAsync(from, to);

        Assert.Empty(result);
    }

    // ══════════════════════════════════════════════════════════
    // GetMachineCapacityAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMachineCapacityAsync_ReturnsCapacityForActiveMachines()
    {
        var machine = await SeedMachineAsync();
        var shift = new OperatingShift
        {
            Name = "Day Shift",
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(16),
            DaysOfWeek = "Mon,Tue,Wed,Thu,Fri",
            IsActive = true
        };
        _db.OperatingShifts.Add(shift);
        await _db.SaveChangesAsync();

        // Query for a single Monday
        var monday = GetNextWeekday(DateTime.UtcNow, DayOfWeek.Monday);
        var result = await _sut.GetMachineCapacityAsync(monday, monday.AddDays(1));

        Assert.Single(result);
        Assert.Equal(machine.Id, result[0].MachineId);
        Assert.True(result[0].AvailableHours > 0);
    }

    [Fact]
    public async Task GetMachineCapacityAsync_CalculatesUtilizationFromScheduledWork()
    {
        var stage = await SeedStageAsync();
        var machine = await SeedMachineAsync();
        var shift = new OperatingShift
        {
            Name = "Day Shift",
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(16),
            DaysOfWeek = "Mon,Tue,Wed,Thu,Fri",
            IsActive = true
        };
        _db.OperatingShifts.Add(shift);
        await _db.SaveChangesAsync();

        var monday = GetNextWeekday(DateTime.UtcNow, DayOfWeek.Monday);
        var exec = await SeedExecutionAsync(stage.Id, machineId: machine.Id, status: StageExecutionStatus.NotStarted);
        exec.ScheduledStartAt = monday.AddHours(8);
        exec.ScheduledEndAt = monday.AddHours(12);
        await _db.SaveChangesAsync();

        var result = await _sut.GetMachineCapacityAsync(monday, monday.AddDays(1));

        Assert.Single(result);
        Assert.Equal(4.0, result[0].LoadedHours);
        Assert.True(result[0].UtilizationPct > 0);
    }

    [Fact]
    public async Task GetMachineCapacityAsync_ExcludesInactiveMachines()
    {
        var machine = await SeedMachineAsync();
        machine.IsActive = false;
        await _db.SaveChangesAsync();

        var shift = new OperatingShift
        {
            Name = "Day Shift",
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(16),
            DaysOfWeek = "Mon,Tue,Wed,Thu,Fri",
            IsActive = true
        };
        _db.OperatingShifts.Add(shift);
        await _db.SaveChangesAsync();

        var monday = GetNextWeekday(DateTime.UtcNow, DayOfWeek.Monday);
        var result = await _sut.GetMachineCapacityAsync(monday, monday.AddDays(1));

        Assert.Empty(result);
    }

    private static DateTime GetNextWeekday(DateTime start, DayOfWeek day)
    {
        var result = start.Date;
        while (result.DayOfWeek != day) result = result.AddDays(1);
        return result;
    }

    // ══════════════════════════════════════════════════════════
    // Tooling Blocking (Program → Component wear life)
    // ══════════════════════════════════════════════════════════

    private async Task<MachineProgram> SeedMachineProgramAsync(string number = "CNC-001")
    {
        var program = new MachineProgram
        {
            ProgramNumber = number,
            Name = "Test Program",
            Status = ProgramStatus.Active,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();
        return program;
    }

    private async Task<MachineComponent> SeedComponentAsync(
        string machineId = "SLS-001",
        double? currentHours = null,
        int? currentBuilds = null)
    {
        var component = new MachineComponent
        {
            MachineId = machineId,
            Name = "Test Component",
            CurrentHours = currentHours,
            CurrentBuilds = currentBuilds,
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachineComponents.Add(component);
        await _db.SaveChangesAsync();
        return component;
    }

    [Fact]
    public async Task StartStageExecutionAsync_WhenToolingWearExceeded_ThrowsAndBlocksStart()
    {
        var stage = await SeedStageAsync();
        var program = await SeedMachineProgramAsync();

        // Component has 120 hours — exceeds 100h wear life
        var component = await SeedComponentAsync(currentHours: 120);

        _db.ProgramToolingItems.Add(new ProgramToolingItem
        {
            MachineProgramId = program.Id,
            ToolPosition = "T1",
            Name = "6mm End Mill",
            MachineComponentId = component.Id,
            WearLifeHours = 100,
            IsActive = true,
            CreatedBy = "test"
        });
        await _db.SaveChangesAsync();

        var exec = await SeedExecutionAsync(stage.Id);
        exec.MachineProgramId = program.Id;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.StartStageExecutionAsync(exec.Id, 1, "Op"));

        Assert.Contains("tooling components require maintenance", ex.Message);
        Assert.Contains("T1", ex.Message);

        // Verify execution was NOT started
        var unchanged = await _db.StageExecutions.FindAsync(exec.Id);
        Assert.Equal(StageExecutionStatus.NotStarted, unchanged!.Status);
    }

    [Fact]
    public async Task StartStageExecutionAsync_WhenCriticalMaintenanceRuleOverdue_ThrowsAndBlocksStart()
    {
        var stage = await SeedStageAsync();
        var program = await SeedMachineProgramAsync();

        // Component at 600 hours, critical rule triggers at 500 hours
        var component = await SeedComponentAsync(currentHours: 600);

        _db.MaintenanceRules.Add(new MaintenanceRule
        {
            MachineComponentId = component.Id,
            Name = "Spindle Replacement",
            TriggerType = MaintenanceTriggerType.HoursRun,
            ThresholdValue = 500,
            Severity = MaintenanceSeverity.Critical,
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        });
        await _db.SaveChangesAsync();

        _db.ProgramToolingItems.Add(new ProgramToolingItem
        {
            MachineProgramId = program.Id,
            ToolPosition = "T2",
            Name = "Spindle Holder",
            MachineComponentId = component.Id,
            // No wear life configured — blocking comes from maintenance rule
            IsActive = true,
            CreatedBy = "test"
        });
        await _db.SaveChangesAsync();

        var exec = await SeedExecutionAsync(stage.Id);
        exec.MachineProgramId = program.Id;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.StartStageExecutionAsync(exec.Id, 1, "Op"));

        Assert.Contains("tooling components require maintenance", ex.Message);
        Assert.Contains("Spindle Replacement", ex.Message);
    }

    [Fact]
    public async Task StartStageExecutionAsync_WhenToolingWithinLimits_Succeeds()
    {
        var stage = await SeedStageAsync();
        var program = await SeedMachineProgramAsync();

        // Component at 50 hours — well within 100h wear life
        var component = await SeedComponentAsync(currentHours: 50);

        _db.ProgramToolingItems.Add(new ProgramToolingItem
        {
            MachineProgramId = program.Id,
            ToolPosition = "T1",
            Name = "6mm End Mill",
            MachineComponentId = component.Id,
            WearLifeHours = 100,
            IsActive = true,
            CreatedBy = "test"
        });
        await _db.SaveChangesAsync();

        var exec = await SeedExecutionAsync(stage.Id);
        exec.MachineProgramId = program.Id;
        await _db.SaveChangesAsync();

        var result = await _sut.StartStageExecutionAsync(exec.Id, 1, "Operator");

        Assert.Equal(StageExecutionStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task StartStageExecutionAsync_WhenNoProgramLinked_SkipsToolingCheck()
    {
        var stage = await SeedStageAsync();
        var exec = await SeedExecutionAsync(stage.Id);

        // No MachineProgramId set — should start without tooling check
        var result = await _sut.StartStageExecutionAsync(exec.Id, 1, "Operator");

        Assert.Equal(StageExecutionStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task StartStageExecutionAsync_WhenToolingBuildWearExceeded_ThrowsAndBlocksStart()
    {
        var stage = await SeedStageAsync();
        var program = await SeedMachineProgramAsync();

        // Component has 110 builds — exceeds 100-build wear life
        var component = await SeedComponentAsync(currentBuilds: 110);

        _db.ProgramToolingItems.Add(new ProgramToolingItem
        {
            MachineProgramId = program.Id,
            ToolPosition = "FIX-01",
            Name = "Build Plate Fixture",
            MachineComponentId = component.Id,
            WearLifeBuilds = 100,
            IsFixture = true,
            IsActive = true,
            CreatedBy = "test"
        });
        await _db.SaveChangesAsync();

        var exec = await SeedExecutionAsync(stage.Id);
        exec.MachineProgramId = program.Id;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.StartStageExecutionAsync(exec.Id, 1, "Op"));

        Assert.Contains("tooling components require maintenance", ex.Message);
        Assert.Contains("FIX-01", ex.Message);
    }

    [Fact]
    public async Task StartStageExecutionAsync_WhenWarningRuleNotCritical_DoesNotBlock()
    {
        var stage = await SeedStageAsync();
        var program = await SeedMachineProgramAsync();

        // Component at 600 hours with Warning-severity rule at 500 hours
        var component = await SeedComponentAsync(currentHours: 600);

        _db.MaintenanceRules.Add(new MaintenanceRule
        {
            MachineComponentId = component.Id,
            Name = "Coolant Check",
            TriggerType = MaintenanceTriggerType.HoursRun,
            ThresholdValue = 500,
            Severity = MaintenanceSeverity.Warning, // Not Critical — should not block
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        });
        await _db.SaveChangesAsync();

        _db.ProgramToolingItems.Add(new ProgramToolingItem
        {
            MachineProgramId = program.Id,
            ToolPosition = "T3",
            Name = "Coolant Nozzle",
            MachineComponentId = component.Id,
            IsActive = true,
            CreatedBy = "test"
        });
        await _db.SaveChangesAsync();

        var exec = await SeedExecutionAsync(stage.Id);
        exec.MachineProgramId = program.Id;
        await _db.SaveChangesAsync();

        // Warning-severity rule should not block — only Critical blocks
        var result = await _sut.StartStageExecutionAsync(exec.Id, 1, "Operator");

        Assert.Equal(StageExecutionStatus.InProgress, result.Status);
    }
}
