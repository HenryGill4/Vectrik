using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services;
using Opcentrix_V3.Tests.Helpers;
using Xunit;

namespace Opcentrix_V3.Tests.Services;

public class BuildPlanningServiceTests : IDisposable
{
    private readonly TenantDbContext _db;

    // Fixed reference date for deterministic tests
    private static readonly DateTime BaseDate = new(2025, 7, 7, 8, 0, 0, DateTimeKind.Utc);

    public BuildPlanningServiceTests()
    {
        _db = TestDbContextFactory.Create();
    }

    public void Dispose() => _db.Dispose();

    // ── Stubs ─────────────────────────────────────────────────

    /// <summary>
    /// Number-sequence stub that returns incrementing values.
    /// </summary>
    private sealed class StubNumberSequenceService : INumberSequenceService
    {
        private int _counter;
        public Task<string> NextAsync(string entityType) =>
            Task.FromResult($"{entityType}-{Interlocked.Increment(ref _counter):D4}");
    }

    /// <summary>
    /// Process service stub that returns duration based on the ProcessStage's RunTimeMinutes.
    /// </summary>
    private sealed class ConfigurableProcessService : IManufacturingProcessService
    {
        public Task<ManufacturingProcess?> GetByPartIdAsync(int partId) => Task.FromResult<ManufacturingProcess?>(null);
        public Task<ManufacturingProcess?> GetByIdAsync(int id) => Task.FromResult<ManufacturingProcess?>(null);
        public Task<ManufacturingProcess> CreateAsync(ManufacturingProcess process) => Task.FromResult(process);
        public Task<ManufacturingProcess> UpdateAsync(ManufacturingProcess process) => Task.FromResult(process);
        public Task DeleteAsync(int id) => Task.CompletedTask;
        public Task<ProcessStage> AddStageAsync(ProcessStage stage) => Task.FromResult(stage);
        public Task<ProcessStage> UpdateStageAsync(ProcessStage stage) => Task.FromResult(stage);
        public Task RemoveStageAsync(int stageId) => Task.CompletedTask;
        public Task ReorderStagesAsync(int processId, List<int> stageIdsInOrder) => Task.CompletedTask;
        public Task<List<string>> ValidateProcessAsync(int processId) => Task.FromResult(new List<string>());
        public Task<ManufacturingProcess> CloneProcessAsync(int sourceProcessId, int targetPartId, string createdBy) => Task.FromResult(new ManufacturingProcess());
        public Task<ManufacturingProcess> CreateProcessFromApproachAsync(int partId, int approachId, string createdBy) => Task.FromResult(new ManufacturingProcess());

        public DurationResult CalculateStageDuration(ProcessStage stage, int partCount, int batchCount, double? buildConfigHours)
        {
            if (stage.DurationFromBuildConfig && buildConfigHours.HasValue)
            {
                var totalMin = buildConfigHours.Value * 60.0;
                return new DurationResult(0, totalMin, totalMin, "from build config");
            }

            double setup = stage.SetupTimeMinutes ?? 0;
            double run = stage.RunTimeMinutes ?? 0;
            return new DurationResult(setup, run, setup + run, "stub duration");
        }
    }

    /// <summary>
    /// Cost service stub that returns zero costs.
    /// </summary>
    private sealed class StubStageCostService : IStageCostService
    {
        public Task<List<StageCostProfile>> GetAllAsync() => Task.FromResult(new List<StageCostProfile>());
        public Task<StageCostProfile?> GetByStageIdAsync(int productionStageId) => Task.FromResult<StageCostProfile?>(null);
        public Task<StageCostProfile> SaveAsync(StageCostProfile profile) => Task.FromResult(profile);
        public Task DeleteAsync(int profileId) => Task.CompletedTask;
        public Task<StageCostEstimate> EstimateCostAsync(int productionStageId, double durationHours, int partCount, int batchCount = 1) =>
            Task.FromResult(new StageCostEstimate(0, 0, 0, 0, 0, 0, 0, 0, 0, false));
    }

    private BuildPlanningService CreateSut() =>
        new(_db, new StubNumberSequenceService(), new ConfigurableProcessService(), new StubBatchService(), new StubStageCostService());

    // ── Helpers ────────────────────────────────────────────────

    /// <summary>
    /// Seeds a minimal scenario: SLS printer, shared depowder machine, a Part with a
    /// ManufacturingProcess containing two build-level stages (Print → Depowder),
    /// and a BuildPackage assigned to the SLS machine with that part.
    /// Returns IDs needed for assertions.
    /// </summary>
    private async Task<(int PackageId, int SlsMachineId, int DepowderMachineId, int DepowderStageId)> SeedBuildWithSharedStageAsync(
        double printHours = 20, double depowderMinutes = 72)
    {
        // Machines
        var slsMachine = new Machine
        {
            MachineId = "M4-1",
            Name = "EOS M4 #1",
            MachineType = "SLS",
            IsActive = true,
            IsAvailableForScheduling = true,
            IsAdditiveMachine = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        var depowderMachine = new Machine
        {
            MachineId = "DEPOWDER-1",
            Name = "Incineris Depowder",
            MachineType = "Post-Processing",
            IsActive = true,
            IsAvailableForScheduling = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Machines.AddRange(slsMachine, depowderMachine);
        await _db.SaveChangesAsync();

        // Production stages (catalog entries)
        var printStage = new ProductionStage
        {
            Name = "SLS Printing",
            StageSlug = "sls-printing",
            DisplayOrder = 1,
            DefaultMachineId = slsMachine.MachineId,
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        var depowderStage = new ProductionStage
        {
            Name = "Depowdering",
            StageSlug = "depowdering",
            DisplayOrder = 2,
            DefaultMachineId = depowderMachine.MachineId,
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ProductionStages.AddRange(printStage, depowderStage);
        await _db.SaveChangesAsync();

        // Part
        var part = new Part
        {
            PartNumber = "TEST-001",
            Name = "Test Part",
            Material = "Ti-6Al-4V"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        // Manufacturing process with two build-level stages
        var process = new ManufacturingProcess
        {
            PartId = part.Id,
            Name = "SLS Process",
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ManufacturingProcesses.Add(process);
        await _db.SaveChangesAsync();

        var printProcessStage = new ProcessStage
        {
            ManufacturingProcessId = process.Id,
            ProductionStageId = printStage.Id,
            ExecutionOrder = 1,
            ProcessingLevel = ProcessingLevel.Build,
            DurationFromBuildConfig = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        var depowderProcessStage = new ProcessStage
        {
            ManufacturingProcessId = process.Id,
            ProductionStageId = depowderStage.Id,
            ExecutionOrder = 2,
            ProcessingLevel = ProcessingLevel.Build,
            DurationFromBuildConfig = false,
            RunDurationMode = DurationMode.PerBuild,
            RunTimeMinutes = depowderMinutes,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ProcessStages.AddRange(printProcessStage, depowderProcessStage);
        await _db.SaveChangesAsync();

        // Build package assigned to SLS machine
        var package = new BuildPackage
        {
            Name = "Build-001",
            MachineId = slsMachine.Id,
            ScheduledDate = BaseDate,
            EstimatedDurationHours = printHours,
            Status = BuildPackageStatus.Scheduled,
            IsSlicerDataEntered = true,
            IsLocked = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.BuildPackages.Add(package);
        await _db.SaveChangesAsync();

        // Add part to the package
        _db.BuildPackageParts.Add(new BuildPackagePart
        {
            BuildPackageId = package.Id,
            PartId = part.Id,
            Quantity = 10
        });
        await _db.SaveChangesAsync();

        return (package.Id, slsMachine.Id, depowderMachine.Id, depowderStage.Id);
    }

    // ══════════════════════════════════════════════════════════
    // Shared-machine collision avoidance
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBuildStageExecutions_SharedMachineOccupied_QueuesAfterExistingBlock()
    {
        // Arrange: seed build with SLS print (20h) + depowder (72min) stages
        var (packageId, slsMachineId, depowderMachineId, _) =
            await SeedBuildWithSharedStageAsync(printHours: 20, depowderMinutes: 72);

        // Pre-existing depowder block from another run: occupies depowder machine
        // from BaseDate+20h to BaseDate+21.2h (the same window the new build would want)
        var existingBlockStart = BaseDate.AddHours(20);
        var existingBlockEnd = BaseDate.AddHours(21.2);
        _db.StageExecutions.Add(new StageExecution
        {
            MachineId = depowderMachineId,
            BuildPackageId = 999, // some other build
            ScheduledStartAt = existingBlockStart,
            ScheduledEndAt = existingBlockEnd,
            Status = StageExecutionStatus.NotStarted,
            SortOrder = 0,
            CreatedBy = "test",
            LastModifiedBy = "test"
        });
        await _db.SaveChangesAsync();

        var sut = CreateSut();

        // Act: create build stage executions starting at BaseDate
        var executions = await sut.CreateBuildStageExecutionsAsync(packageId, "test", startAfter: BaseDate);

        // Assert: should have 2 executions (print + depowder)
        Assert.Equal(2, executions.Count);

        var printExec = executions.First(e => e.MachineId == slsMachineId);
        var depowderExec = executions.First(e => e.MachineId == depowderMachineId);

        // Print starts at BaseDate as expected
        Assert.Equal(BaseDate, printExec.ScheduledStartAt);

        // Depowder must start AFTER the existing block ends, not overlapping it
        Assert.True(depowderExec.ScheduledStartAt >= existingBlockEnd,
            $"Depowder should start at or after {existingBlockEnd:HH:mm} but started at {depowderExec.ScheduledStartAt:HH:mm}");
    }

    [Fact]
    public async Task CreateBuildStageExecutions_NoExistingBlocks_DepowderFollowsPrintDirectly()
    {
        // Arrange: seed build with SLS print (20h) + depowder (72min) stages, no conflicts
        var (packageId, slsMachineId, depowderMachineId, _) =
            await SeedBuildWithSharedStageAsync(printHours: 20, depowderMinutes: 72);

        var sut = CreateSut();

        // Act
        var executions = await sut.CreateBuildStageExecutionsAsync(packageId, "test", startAfter: BaseDate);

        // Assert: depowder starts right after print ends (no gaps when machine is free)
        Assert.Equal(2, executions.Count);

        var printExec = executions.First(e => e.MachineId == slsMachineId);
        var depowderExec = executions.First(e => e.MachineId == depowderMachineId);

        Assert.Equal(BaseDate, printExec.ScheduledStartAt);
        Assert.Equal(printExec.ScheduledEndAt, depowderExec.ScheduledStartAt);
    }

    [Fact]
    public async Task CreateBuildStageExecutions_MultipleExistingBlocks_QueuesAfterLast()
    {
        // Arrange: seed build with SLS print (20h) + depowder (72min) stages
        var (packageId, slsMachineId, depowderMachineId, _) =
            await SeedBuildWithSharedStageAsync(printHours: 20, depowderMinutes: 72);

        // Two existing depowder blocks back-to-back from other runs
        var block1Start = BaseDate.AddHours(20);
        var block1End = BaseDate.AddHours(21.2);
        var block2Start = BaseDate.AddHours(21.2);
        var block2End = BaseDate.AddHours(22.4);

        _db.StageExecutions.AddRange(
            new StageExecution
            {
                MachineId = depowderMachineId,
                BuildPackageId = 998,
                ScheduledStartAt = block1Start,
                ScheduledEndAt = block1End,
                Status = StageExecutionStatus.NotStarted,
                SortOrder = 0,
                CreatedBy = "test",
                LastModifiedBy = "test"
            },
            new StageExecution
            {
                MachineId = depowderMachineId,
                BuildPackageId = 999,
                ScheduledStartAt = block2Start,
                ScheduledEndAt = block2End,
                Status = StageExecutionStatus.NotStarted,
                SortOrder = 0,
                CreatedBy = "test",
                LastModifiedBy = "test"
            });
        await _db.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var executions = await sut.CreateBuildStageExecutionsAsync(packageId, "test", startAfter: BaseDate);

        // Assert: depowder queues after BOTH existing blocks
        var depowderExec = executions.First(e => e.MachineId == depowderMachineId);
        Assert.True(depowderExec.ScheduledStartAt >= block2End,
            $"Depowder should start at or after {block2End} but started at {depowderExec.ScheduledStartAt}");
    }

    [Fact]
    public async Task CreateBuildStageExecutions_CompletedBlockIgnored_DoesNotDefer()
    {
        // Arrange: seed build with SLS print (20h) + depowder (72min) stages
        var (packageId, slsMachineId, depowderMachineId, _) =
            await SeedBuildWithSharedStageAsync(printHours: 20, depowderMinutes: 72);

        // An existing depowder block that's already completed — should be ignored
        _db.StageExecutions.Add(new StageExecution
        {
            MachineId = depowderMachineId,
            BuildPackageId = 999,
            ScheduledStartAt = BaseDate.AddHours(20),
            ScheduledEndAt = BaseDate.AddHours(21.2),
            Status = StageExecutionStatus.Completed,
            SortOrder = 0,
            CreatedBy = "test",
            LastModifiedBy = "test"
        });
        await _db.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var executions = await sut.CreateBuildStageExecutionsAsync(packageId, "test", startAfter: BaseDate);

        // Assert: depowder starts right after print (completed block is ignored)
        var printExec = executions.First(e => e.MachineId == slsMachineId);
        var depowderExec = executions.First(e => e.MachineId == depowderMachineId);

        Assert.Equal(printExec.ScheduledEndAt, depowderExec.ScheduledStartAt);
    }

    // ══════════════════════════════════════════════════════════
    // MachineProgramId stamping on StageExecutions
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Seeds a build with a ProcessStage linked to a MachineProgram, returns IDs for assertions.
    /// </summary>
    private async Task<(int PackageId, int ProgramId, int SlsMachineId)> SeedBuildWithProgramAsync()
    {
        // Machine
        var machine = new Machine
        {
            MachineId = "CNC-1",
            Name = "CNC Mill #1",
            MachineType = "CNC",
            IsActive = true,
            IsAvailableForScheduling = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();

        // Production stage
        var prodStage = new ProductionStage
        {
            Name = "CNC Milling",
            StageSlug = "cnc-milling",
            DisplayOrder = 1,
            DefaultMachineId = machine.MachineId,
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ProductionStages.Add(prodStage);
        await _db.SaveChangesAsync();

        // Part
        var part = new Part
        {
            PartNumber = "PROG-001",
            Name = "Program Test Part",
            Material = "Al-6061"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        // Manufacturing process with one build-level stage
        var process = new ManufacturingProcess
        {
            PartId = part.Id,
            Name = "CNC Process",
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ManufacturingProcesses.Add(process);
        await _db.SaveChangesAsync();

        var processStage = new ProcessStage
        {
            ManufacturingProcessId = process.Id,
            ProductionStageId = prodStage.Id,
            ExecutionOrder = 1,
            ProcessingLevel = ProcessingLevel.Build,
            DurationFromBuildConfig = false,
            RunDurationMode = DurationMode.PerBuild,
            RunTimeMinutes = 45,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ProcessStages.Add(processStage);
        await _db.SaveChangesAsync();

        // MachineProgram linked to the process stage
        var program = new MachineProgram
        {
            PartId = part.Id,
            MachineId = machine.Id,
            ProcessStageId = processStage.Id,
            ProgramNumber = "P-1001",
            Name = "Mill Op1",
            Version = 1,
            Status = ProgramStatus.Active,
            RunTimeMinutes = 42,
            SetupTimeMinutes = 5,
            IsActive = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        // Link the process stage to the program
        processStage.MachineProgramId = program.Id;
        await _db.SaveChangesAsync();

        // Build package
        var package = new BuildPackage
        {
            Name = "Build-Prog-001",
            MachineId = machine.Id,
            ScheduledDate = BaseDate,
            EstimatedDurationHours = 1,
            Status = BuildPackageStatus.Scheduled,
            IsSlicerDataEntered = true,
            IsLocked = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.BuildPackages.Add(package);
        await _db.SaveChangesAsync();

        _db.BuildPackageParts.Add(new BuildPackagePart
        {
            BuildPackageId = package.Id,
            PartId = part.Id,
            Quantity = 1
        });
        await _db.SaveChangesAsync();

        return (package.Id, program.Id, machine.Id);
    }

    [Fact]
    public async Task CreateBuildStageExecutions_StampsMachineProgramId_WhenProcessStageHasProgram()
    {
        // Arrange
        var (packageId, programId, _) = await SeedBuildWithProgramAsync();
        var sut = CreateSut();

        // Act
        var executions = await sut.CreateBuildStageExecutionsAsync(packageId, "test", startAfter: BaseDate);

        // Assert: the execution should carry the program ID from the ProcessStage
        Assert.Single(executions);
        Assert.Equal(programId, executions[0].MachineProgramId);
    }

    [Fact]
    public async Task CreateBuildStageExecutions_MachineProgramIdNull_WhenNoProgram()
    {
        // Arrange: use the shared-stage seed (no MachineProgram linked)
        var (packageId, _, _, _) = await SeedBuildWithSharedStageAsync();
        var sut = CreateSut();

        // Act
        var executions = await sut.CreateBuildStageExecutionsAsync(packageId, "test", startAfter: BaseDate);

        // Assert: no program linked → MachineProgramId stays null
        Assert.All(executions, e => Assert.Null(e.MachineProgramId));
    }
}
