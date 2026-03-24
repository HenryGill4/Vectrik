using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services;
using Opcentrix_V3.Tests.Helpers;
using Xunit;

namespace Opcentrix_V3.Tests.Services;

public class JobServiceTests : IDisposable
{
    private readonly TenantDbContext _db;
    private readonly JobService _sut;

    public JobServiceTests()
    {
        _db = TestDbContextFactory.Create();
        var scheduler = new SchedulingService(_db, new StubMachineProgramService(), new ShiftManagementService(_db));
        var processService = new ManufacturingProcessService(_db);
        _sut = new JobService(_db, scheduler, processService);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────

    private async Task<Part> SeedPartAsync(string partNumber = "PN-001")
    {
        var part = new Part
        {
            PartNumber = partNumber,
            Name = "Test Part",
            Material = "Ti-6Al-4V",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();
        return part;
    }

    private async Task<ProductionStage> SeedStageAsync(string name = "SLS Printing", string slug = "sls", double duration = 4.0)
    {
        var stage = new ProductionStage
        {
            Name = name,
            StageSlug = slug,
            DefaultDurationHours = duration,
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
            IsActive = true,
            IsAvailableForScheduling = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    private async Task SeedRoutingAsync(int partId, params int[] stageIds)
    {
        for (int i = 0; i < stageIds.Length; i++)
        {
            _db.PartStageRequirements.Add(new PartStageRequirement
            {
                PartId = partId,
                ProductionStageId = stageIds[i],
                ExecutionOrder = i + 1,
                IsActive = true,
                CreatedBy = "test",
                LastModifiedBy = "test"
            });
        }
        await _db.SaveChangesAsync();
    }

    private Job CreateTestJob(int partId, DateTime? start = null) => new()
    {
        PartId = partId,
        Quantity = 1,
        ScheduledStart = start ?? DateTime.UtcNow,
        ScheduledEnd = (start ?? DateTime.UtcNow).AddHours(8),
        Status = JobStatus.Draft,
        CreatedBy = "test",
        LastModifiedBy = "test"
    };

    // ── CreateJobAsync ────────────────────────────────────────

    [Fact]
    public async Task CreateJobAsync_PersistsJobToDatabase()
    {
        var part = await SeedPartAsync();
        var job = CreateTestJob(part.Id);

        var result = await _sut.CreateJobAsync(job);

        Assert.True(result.Id > 0);
        Assert.Equal(1, await _db.Jobs.CountAsync());
    }

    [Fact]
    public async Task CreateJobAsync_HydratesPartNumberAndMaterial()
    {
        var part = await SeedPartAsync("TI-BRACKET-001");
        var job = CreateTestJob(part.Id);

        var result = await _sut.CreateJobAsync(job);

        Assert.Equal("TI-BRACKET-001", result.PartNumber);
        Assert.Equal("Ti-6Al-4V", result.SlsMaterial);
    }

    [Fact]
    public async Task CreateJobAsync_WhenPartNotFound_ThrowsInvalidOperationException()
    {
        var job = CreateTestJob(partId: 9999);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateJobAsync(job));
    }

    [Fact]
    public async Task CreateJobAsync_GeneratesStageExecutionsFromRouting()
    {
        var part = await SeedPartAsync();
        var stage1 = await SeedStageAsync("Stage 1", "stage-1", 2.0);
        var stage2 = await SeedStageAsync("Stage 2", "stage-2", 3.0);
        await SeedRoutingAsync(part.Id, stage1.Id, stage2.Id);

        var job = CreateTestJob(part.Id);
        var result = await _sut.CreateJobAsync(job);

        var executions = await _db.StageExecutions
            .Where(e => e.JobId == result.Id)
            .OrderBy(e => e.SortOrder)
            .ToListAsync();

        Assert.Equal(2, executions.Count);
        Assert.Equal(stage1.Id, executions[0].ProductionStageId);
        Assert.Equal(stage2.Id, executions[1].ProductionStageId);
        Assert.Equal(1, executions[0].SortOrder);
        Assert.Equal(2, executions[1].SortOrder);
    }

    [Fact]
    public async Task CreateJobAsync_WithStackLevel_HydratesDurationAndPartsPerBuild()
    {
        var part = await SeedPartAsync();
        var config = new PartAdditiveBuildConfig
        {
            PartId = part.Id,
            AllowStacking = true,
            SingleStackDurationHours = 5.0,
            PlannedPartsPerBuildSingle = 2,
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8.0,
            PlannedPartsPerBuildDouble = 4
        };
        _db.PartAdditiveBuildConfigs.Add(config);
        await _db.SaveChangesAsync();

        var job = CreateTestJob(part.Id);
        job.StackLevel = 2;
        var result = await _sut.CreateJobAsync(job);

        Assert.Equal(8.0, result.PlannedStackDurationHours);
        Assert.Equal(4, result.PartsPerBuild);
    }

    [Fact]
    public async Task CreateJobAsync_WhenOverlap_ThrowsInvalidOperationException()
    {
        var part = await SeedPartAsync();
        var machine = await SeedMachineAsync();

        var start = DateTime.UtcNow.AddDays(1);
        var existing = CreateTestJob(part.Id, start);
        existing.MachineId = machine.Id;
        existing.Status = JobStatus.Scheduled;
        await _sut.CreateJobAsync(existing);

        var overlapping = CreateTestJob(part.Id, start.AddHours(1));
        overlapping.MachineId = machine.Id;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateJobAsync(overlapping));
    }

    [Fact]
    public async Task CreateJobAsync_SetsAuditTimestamps()
    {
        var part = await SeedPartAsync();
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await _sut.CreateJobAsync(CreateTestJob(part.Id));

        Assert.True(result.CreatedDate >= before);
        Assert.True(result.LastModifiedDate >= before);
        Assert.NotNull(result.LastStatusChangeUtc);
    }

    // ── GetAllJobsAsync ───────────────────────────────────────

    [Fact]
    public async Task GetAllJobsAsync_ReturnsAllJobsOrderedByScheduledStart()
    {
        var part = await SeedPartAsync();
        var later = CreateTestJob(part.Id, DateTime.UtcNow.AddDays(2));
        var earlier = CreateTestJob(part.Id, DateTime.UtcNow.AddDays(1));
        await _sut.CreateJobAsync(later);
        await _sut.CreateJobAsync(earlier);

        var result = await _sut.GetAllJobsAsync();

        Assert.Equal(2, result.Count);
        Assert.True(result[0].ScheduledStart <= result[1].ScheduledStart);
    }

    [Fact]
    public async Task GetAllJobsAsync_WhenStatusFilter_ReturnsOnlyMatchingStatus()
    {
        var part = await SeedPartAsync();
        await _sut.CreateJobAsync(CreateTestJob(part.Id));

        var scheduledJob = CreateTestJob(part.Id, DateTime.UtcNow.AddDays(5));
        var created = await _sut.CreateJobAsync(scheduledJob);
        await _sut.UpdateStatusAsync(created.Id, JobStatus.Scheduled, "test");

        var result = await _sut.GetAllJobsAsync(JobStatus.Scheduled);

        Assert.Single(result);
        Assert.Equal(JobStatus.Scheduled, result[0].Status);
    }

    // ── GetJobByIdAsync ───────────────────────────────────────

    [Fact]
    public async Task GetJobByIdAsync_WhenExists_ReturnsJobWithIncludes()
    {
        var part = await SeedPartAsync();
        var job = await _sut.CreateJobAsync(CreateTestJob(part.Id));

        var result = await _sut.GetJobByIdAsync(job.Id);

        Assert.NotNull(result);
        Assert.NotNull(result.Part);
        Assert.Equal(part.PartNumber, result.Part.PartNumber);
    }

    [Fact]
    public async Task GetJobByIdAsync_WhenNotExists_ReturnsNull()
    {
        Assert.Null(await _sut.GetJobByIdAsync(999));
    }

    // ── UpdateJobAsync ────────────────────────────────────────

    [Fact]
    public async Task UpdateJobAsync_UpdatesFieldsAndTimestamp()
    {
        var part = await SeedPartAsync();
        var job = await _sut.CreateJobAsync(CreateTestJob(part.Id));
        job.Notes = "Updated notes";

        var result = await _sut.UpdateJobAsync(job);

        Assert.Equal("Updated notes", result.Notes);
    }

    // ── UpdateStatusAsync ─────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(999, JobStatus.InProgress, "test"));
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenInProgress_SetsActualStart()
    {
        var part = await SeedPartAsync();
        var job = await _sut.CreateJobAsync(CreateTestJob(part.Id));

        var result = await _sut.UpdateStatusAsync(job.Id, JobStatus.InProgress, "operator");

        Assert.Equal(JobStatus.InProgress, result.Status);
        Assert.NotNull(result.ActualStart);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenCompleted_SetsActualEnd()
    {
        var part = await SeedPartAsync();
        var job = await _sut.CreateJobAsync(CreateTestJob(part.Id));
        await _sut.UpdateStatusAsync(job.Id, JobStatus.InProgress, "operator");

        var result = await _sut.UpdateStatusAsync(job.Id, JobStatus.Completed, "operator");

        Assert.Equal(JobStatus.Completed, result.Status);
        Assert.NotNull(result.ActualEnd);
    }

    [Fact]
    public async Task UpdateStatusAsync_UpdatesLastModifiedByAndTimestamp()
    {
        var part = await SeedPartAsync();
        var job = await _sut.CreateJobAsync(CreateTestJob(part.Id));
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await _sut.UpdateStatusAsync(job.Id, JobStatus.Scheduled, "admin-user");

        Assert.Equal("admin-user", result.LastModifiedBy);
        Assert.True(result.LastStatusChangeUtc >= before);
    }

    // ── DeleteJobAsync (Cancel) ───────────────────────────────

    [Fact]
    public async Task DeleteJobAsync_CancelsJobInsteadOfRemovingIt()
    {
        var part = await SeedPartAsync();
        var job = await _sut.CreateJobAsync(CreateTestJob(part.Id));

        await _sut.DeleteJobAsync(job.Id);

        var deleted = await _db.Jobs.FindAsync(job.Id);
        Assert.NotNull(deleted);
        Assert.Equal(JobStatus.Cancelled, deleted.Status);
    }

    [Fact]
    public async Task DeleteJobAsync_SkipsOutstandingStageExecutions()
    {
        var part = await SeedPartAsync();
        var stage = await SeedStageAsync();
        await SeedRoutingAsync(part.Id, stage.Id);

        var job = await _sut.CreateJobAsync(CreateTestJob(part.Id));

        await _sut.DeleteJobAsync(job.Id);

        var executions = await _db.StageExecutions
            .Where(e => e.JobId == job.Id)
            .ToListAsync();

        Assert.All(executions, e =>
        {
            Assert.Equal(StageExecutionStatus.Skipped, e.Status);
            Assert.Contains("job cancelled", e.Notes, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task DeleteJobAsync_WhenNotExists_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteJobAsync(999));
    }

    // ── HasOverlapAsync ───────────────────────────────────────

    [Fact]
    public async Task HasOverlapAsync_WhenOverlap_ReturnsTrue()
    {
        var part = await SeedPartAsync();
        var machine = await SeedMachineAsync();
        var start = DateTime.UtcNow.AddDays(1);

        var existing = CreateTestJob(part.Id, start);
        existing.MachineId = machine.Id;
        existing.ScheduledEnd = start.AddHours(8);
        existing.Status = JobStatus.Scheduled;
        await _sut.CreateJobAsync(existing);

        // Overlaps in the middle
        Assert.True(await _sut.HasOverlapAsync(machine.Id, start.AddHours(2), start.AddHours(6)));
    }

    [Fact]
    public async Task HasOverlapAsync_WhenNoOverlap_ReturnsFalse()
    {
        var part = await SeedPartAsync();
        var machine = await SeedMachineAsync();
        var start = DateTime.UtcNow.AddDays(1);

        var existing = CreateTestJob(part.Id, start);
        existing.MachineId = machine.Id;
        existing.ScheduledEnd = start.AddHours(4);
        existing.Status = JobStatus.Scheduled;
        await _sut.CreateJobAsync(existing);

        // After existing job
        Assert.False(await _sut.HasOverlapAsync(machine.Id, start.AddHours(5), start.AddHours(8)));
    }

    [Fact]
    public async Task HasOverlapAsync_ExcludesCancelledJobs()
    {
        var part = await SeedPartAsync();
        var machine = await SeedMachineAsync();
        var start = DateTime.UtcNow.AddDays(1);

        var cancelled = CreateTestJob(part.Id, start);
        cancelled.MachineId = machine.Id;
        cancelled.Status = JobStatus.Cancelled;
        _db.Jobs.Add(cancelled);
        await _db.SaveChangesAsync();

        Assert.False(await _sut.HasOverlapAsync(machine.Id, start, start.AddHours(8)));
    }

    [Fact]
    public async Task HasOverlapAsync_WhenExcludeJobId_IgnoresSelf()
    {
        var part = await SeedPartAsync();
        var machine = await SeedMachineAsync();
        var start = DateTime.UtcNow.AddDays(1);

        var job = CreateTestJob(part.Id, start);
        job.MachineId = machine.Id;
        job.Status = JobStatus.Scheduled;
        var created = await _sut.CreateJobAsync(job);

        Assert.False(await _sut.HasOverlapAsync(
            machine.Id, start, start.AddHours(8), excludeJobId: created.Id));
    }

    // ── GetJobsForSchedulerAsync ──────────────────────────────

    [Fact]
    public async Task GetJobsForSchedulerAsync_ReturnsJobsInRange()
    {
        var part = await SeedPartAsync();
        var baseDate = DateTime.UtcNow.AddDays(10);

        var inRange = CreateTestJob(part.Id, baseDate);
        inRange.ScheduledEnd = baseDate.AddHours(8);
        await _sut.CreateJobAsync(inRange);

        var outOfRange = CreateTestJob(part.Id, baseDate.AddDays(20));
        outOfRange.ScheduledEnd = baseDate.AddDays(20).AddHours(8);
        await _sut.CreateJobAsync(outOfRange);

        var result = await _sut.GetJobsForSchedulerAsync(baseDate.AddHours(-1), baseDate.AddHours(10));

        Assert.Single(result);
    }

    [Fact]
    public async Task GetJobsForSchedulerAsync_ExcludesCancelledJobs()
    {
        var part = await SeedPartAsync();
        var start = DateTime.UtcNow.AddDays(10);

        var cancelled = CreateTestJob(part.Id, start);
        cancelled.Status = JobStatus.Cancelled;
        _db.Jobs.Add(cancelled);
        await _db.SaveChangesAsync();

        var result = await _sut.GetJobsForSchedulerAsync(start.AddHours(-1), start.AddHours(10));

        Assert.Empty(result);
    }

    // ── GetJobsByMachineAsync ─────────────────────────────────

    [Fact]
    public async Task GetJobsByMachineAsync_ReturnsJobsForMachine()
    {
        var part = await SeedPartAsync();
        var machine = await SeedMachineAsync();

        var job1 = CreateTestJob(part.Id);
        job1.MachineId = machine.Id;
        _db.Jobs.Add(job1);

        var job2 = CreateTestJob(part.Id);
        job2.MachineId = 99999;
        _db.Jobs.Add(job2);
        await _db.SaveChangesAsync();

        var result = await _sut.GetJobsByMachineAsync(machine.Id);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetJobsByMachineAsync_FiltersDateRange()
    {
        var part = await SeedPartAsync();
        var machine = await SeedMachineAsync();
        var baseDate = DateTime.UtcNow.AddDays(10);

        var inRange = CreateTestJob(part.Id, baseDate);
        inRange.MachineId = machine.Id;
        inRange.ScheduledEnd = baseDate.AddHours(4);
        _db.Jobs.Add(inRange);

        var outOfRange = CreateTestJob(part.Id, baseDate.AddDays(30));
        outOfRange.MachineId = machine.Id;
        outOfRange.ScheduledEnd = baseDate.AddDays(30).AddHours(4);
        _db.Jobs.Add(outOfRange);
        await _db.SaveChangesAsync();

        var result = await _sut.GetJobsByMachineAsync(
            machine.Id, from: baseDate.AddHours(-1), to: baseDate.AddHours(10));

        Assert.Single(result);
    }
}
