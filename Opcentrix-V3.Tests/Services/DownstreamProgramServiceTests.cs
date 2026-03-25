using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;
using Opcentrix_V3.Services;
using Opcentrix_V3.Tests.Helpers;
using Xunit;

namespace Opcentrix_V3.Tests.Services;

/// <summary>
/// Tests for DownstreamProgramService — validates downstream stage requirements,
/// readiness checks, and placeholder program creation for BuildPlate programs.
/// </summary>
public class DownstreamProgramServiceTests : IDisposable
{
    private readonly TenantDbContext _db;
    private readonly DownstreamProgramService _sut;

    public DownstreamProgramServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _sut = new DownstreamProgramService(_db, new StubMachineProgramService());
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>Creates a part with a manufacturing process containing a print stage + downstream stages.</summary>
    private async Task<(MachineProgram BuildPlate, ManufacturingProcess Process, List<ProcessStage> DownstreamStages)>
        SetupBuildPlateWithDownstreamAsync(
            int downstreamCount = 2,
            bool assignPrograms = false,
            bool markOptional = false)
    {
        var part = new Part
        {
            PartNumber = "PART-DS-001",
            Name = "Downstream Test Part",
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.Parts.Add(part);
        await _db.SaveChangesAsync();

        // Create production stages (catalog entries)
        var printProdStage = new ProductionStage
        {
            Name = "SLS Print",
            StageSlug = "sls-print",
            Department = "SLS",
            DefaultDurationHours = 12,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ProductionStages.Add(printProdStage);

        var depowderProdStage = new ProductionStage
        {
            Name = "Depowder",
            StageSlug = "depowder",
            Department = "Depowder",
            DefaultDurationHours = 2,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ProductionStages.Add(depowderProdStage);

        var edmProdStage = new ProductionStage
        {
            Name = "Wire EDM",
            StageSlug = "wire-edm",
            Department = "EDM",
            DefaultDurationHours = 4,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ProductionStages.Add(edmProdStage);

        var finishProdStage = new ProductionStage
        {
            Name = "Finishing",
            StageSlug = "finishing",
            Department = "Finishing",
            DefaultDurationHours = 1,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ProductionStages.Add(finishProdStage);
        await _db.SaveChangesAsync();

        // Manufacturing process
        var process = new ManufacturingProcess
        {
            PartId = part.Id,
            Name = "SLS Full Process",
            IsActive = true,
            DefaultBatchCapacity = 10,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ManufacturingProcesses.Add(process);
        await _db.SaveChangesAsync();

        // Print stage (Build level, execution order 1)
        var printStage = new ProcessStage
        {
            ManufacturingProcessId = process.Id,
            ProductionStageId = printProdStage.Id,
            ExecutionOrder = 1,
            ProcessingLevel = ProcessingLevel.Build,
            DurationFromBuildConfig = true,
            IsRequired = true,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.ProcessStages.Add(printStage);

        // Downstream stages
        var downstreamStages = new List<ProcessStage>();
        var prodStages = new[] { depowderProdStage, edmProdStage, finishProdStage };

        for (int i = 0; i < Math.Min(downstreamCount, prodStages.Length); i++)
        {
            var stage = new ProcessStage
            {
                ManufacturingProcessId = process.Id,
                ProductionStageId = prodStages[i].Id,
                ExecutionOrder = i + 2,
                ProcessingLevel = i == 0 ? ProcessingLevel.Build : ProcessingLevel.Batch,
                RunTimeMinutes = prodStages[i].DefaultDurationHours * 60,
                IsRequired = !markOptional,
                AllowSkip = markOptional,
                CreatedBy = "test",
                LastModifiedBy = "test"
            };
            _db.ProcessStages.Add(stage);
            downstreamStages.Add(stage);
        }
        await _db.SaveChangesAsync();

        // Optionally assign programs to downstream stages
        if (assignPrograms)
        {
            foreach (var ds in downstreamStages)
            {
                var prog = new MachineProgram
                {
                    Name = $"Auto-{ds.ProductionStageId}",
                    ProgramType = ProgramType.Standard,
                    Status = ProgramStatus.Active,
                    ScheduleStatus = ProgramScheduleStatus.None,
                    ProcessStageId = ds.Id,
                    CreatedBy = "test",
                    LastModifiedBy = "test"
                };
                _db.MachinePrograms.Add(prog);
                await _db.SaveChangesAsync();

                ds.MachineProgramId = prog.Id;
            }
            await _db.SaveChangesAsync();
        }

        // Build plate program with the part
        var buildPlate = new MachineProgram
        {
            Name = "Test Build Plate",
            ProgramType = ProgramType.BuildPlate,
            Status = ProgramStatus.Active,
            ScheduleStatus = ProgramScheduleStatus.Ready,
            EstimatedPrintHours = 12,
            CreatedBy = "test",
            LastModifiedBy = "test"
        };
        _db.MachinePrograms.Add(buildPlate);
        await _db.SaveChangesAsync();

        var programPart = new ProgramPart
        {
            MachineProgramId = buildPlate.Id,
            PartId = part.Id,
            Quantity = 5,
            StackLevel = 1
        };
        _db.ProgramParts.Add(programPart);
        await _db.SaveChangesAsync();

        return (buildPlate, process, downstreamStages);
    }

    // ══════════════════════════════════════════════════════════
    // GetRequiredProgramsAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRequiredPrograms_ReturnsDownstreamStages_AfterPrintStage()
    {
        // Arrange
        var (buildPlate, _, _) = await SetupBuildPlateWithDownstreamAsync(downstreamCount: 2);

        // Act
        var requirements = await _sut.GetRequiredProgramsAsync(buildPlate.Id);

        // Assert
        Assert.Equal(2, requirements.Count);
        Assert.Equal("Depowder", requirements[0].StageName);
        Assert.Equal("Wire EDM", requirements[1].StageName);
    }

    [Fact]
    public async Task GetRequiredPrograms_ReturnsEmpty_WhenNoProgramExists()
    {
        // Act
        var requirements = await _sut.GetRequiredProgramsAsync(9999);

        // Assert
        Assert.Empty(requirements);
    }

    [Fact]
    public async Task GetRequiredPrograms_ShowsAssignedProgram_WhenLinked()
    {
        // Arrange
        var (buildPlate, _, _) = await SetupBuildPlateWithDownstreamAsync(
            downstreamCount: 2, assignPrograms: true);

        // Act
        var requirements = await _sut.GetRequiredProgramsAsync(buildPlate.Id);

        // Assert: both stages should have assigned programs
        Assert.All(requirements, r => Assert.True(r.AssignedProgramId.HasValue));
    }

    [Fact]
    public async Task GetRequiredPrograms_OrderedByExecutionOrder()
    {
        // Arrange
        var (buildPlate, _, _) = await SetupBuildPlateWithDownstreamAsync(downstreamCount: 3);

        // Act
        var requirements = await _sut.GetRequiredProgramsAsync(buildPlate.Id);

        // Assert
        Assert.Equal(3, requirements.Count);
        Assert.True(requirements[0].ExecutionOrder < requirements[1].ExecutionOrder);
        Assert.True(requirements[1].ExecutionOrder < requirements[2].ExecutionOrder);
    }

    [Fact]
    public async Task GetRequiredPrograms_HasDefaultParameters_WhenRunTimeSet()
    {
        // Arrange: downstream stages have RunTimeMinutes set
        var (buildPlate, _, _) = await SetupBuildPlateWithDownstreamAsync(downstreamCount: 1);

        // Act
        var requirements = await _sut.GetRequiredProgramsAsync(buildPlate.Id);

        // Assert
        Assert.Single(requirements);
        Assert.True(requirements[0].HasDefaultParameters);
    }

    // ══════════════════════════════════════════════════════════
    // ValidateDownstreamReadinessAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task ValidateReadiness_IsValid_WhenAllProgramsAssigned()
    {
        // Arrange
        var (buildPlate, _, _) = await SetupBuildPlateWithDownstreamAsync(
            downstreamCount: 2, assignPrograms: true);

        // Act
        var result = await _sut.ValidateDownstreamReadinessAsync(buildPlate.Id);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingPrograms);
    }

    [Fact]
    public async Task ValidateReadiness_IsValid_WhenRequiredStagesHaveDefaults()
    {
        // Arrange: required stages without programs but WITH default durations configured
        var (buildPlate, _, _) = await SetupBuildPlateWithDownstreamAsync(
            downstreamCount: 2, assignPrograms: false);

        // Act
        var result = await _sut.ValidateDownstreamReadinessAsync(buildPlate.Id);

        // Assert: valid because stages have RunTimeMinutes (default parameters)
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingPrograms);
    }

    [Fact]
    public async Task ValidateReadiness_IsValid_WhenOptionalStagesMissPrograms()
    {
        // Arrange: optional stages (not required, allow skip)
        var (buildPlate, _, _) = await SetupBuildPlateWithDownstreamAsync(
            downstreamCount: 2, assignPrograms: false, markOptional: true);

        // Act
        var result = await _sut.ValidateDownstreamReadinessAsync(buildPlate.Id);

        // Assert: valid because stages are optional
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingPrograms);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task ValidateReadiness_WarnsAboutDefaultsAvailable()
    {
        // Arrange: required stages with RunTimeMinutes but no program
        var (buildPlate, _, _) = await SetupBuildPlateWithDownstreamAsync(
            downstreamCount: 1, assignPrograms: false);

        // Act
        var result = await _sut.ValidateDownstreamReadinessAsync(buildPlate.Id);

        // Assert: valid because defaults exist, but warns about using defaults
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("default parameters"));
    }

    // ══════════════════════════════════════════════════════════
    // CreatePlaceholderProgramsAsync
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task CreatePlaceholders_CreatesPrograms_ForSpecifiedStages()
    {
        // Arrange
        var (buildPlate, _, downstreamStages) = await SetupBuildPlateWithDownstreamAsync(
            downstreamCount: 2, assignPrograms: false);

        var stageIds = downstreamStages.Select(s => s.Id).ToList();

        // Act
        var created = await _sut.CreatePlaceholderProgramsAsync(buildPlate.Id, stageIds, "test-user");

        // Assert
        Assert.Equal(2, created.Count);
        Assert.All(created, p =>
        {
            Assert.Equal(ProgramType.Standard, p.ProgramType);
            Assert.Equal(ProgramStatus.Active, p.Status);
            Assert.StartsWith("Test Build Plate-", p.Name);
            Assert.StartsWith("AUTO-", p.ProgramNumber);
        });
    }

    [Fact]
    public async Task CreatePlaceholders_LinksPrograms_ToProcessStages()
    {
        // Arrange
        var (buildPlate, _, downstreamStages) = await SetupBuildPlateWithDownstreamAsync(
            downstreamCount: 2, assignPrograms: false);

        var stageIds = downstreamStages.Select(s => s.Id).ToList();

        // Act
        await _sut.CreatePlaceholderProgramsAsync(buildPlate.Id, stageIds, "test-user");

        // Assert: process stages should now have MachineProgramId set
        foreach (var stageId in stageIds)
        {
            var stage = await _db.ProcessStages.FindAsync(stageId);
            Assert.NotNull(stage);
            Assert.True(stage!.MachineProgramId.HasValue,
                $"Stage {stageId} should have MachineProgramId after placeholder creation");
            Assert.False(stage.ProgramSetupRequired);
        }
    }

    [Fact]
    public async Task CreatePlaceholders_ReturnsEmpty_WhenBuildPlateNotFound()
    {
        // Act
        var created = await _sut.CreatePlaceholderProgramsAsync(9999, [1, 2], "test-user");

        // Assert
        Assert.Empty(created);
    }

    [Fact]
    public async Task CreatePlaceholders_UsesDuration_FromStageConfig()
    {
        // Arrange
        var (buildPlate, _, downstreamStages) = await SetupBuildPlateWithDownstreamAsync(
            downstreamCount: 1, assignPrograms: false);

        var stage = downstreamStages.First();
        var stageIds = new List<int> { stage.Id };

        // Act
        var created = await _sut.CreatePlaceholderProgramsAsync(buildPlate.Id, stageIds, "test-user");

        // Assert: duration should come from RunTimeMinutes
        Assert.Single(created);
        var expectedHours = stage.RunTimeMinutes!.Value / 60.0;
        Assert.Equal(expectedHours, created[0].EstimatedPrintHours);
    }

    // ══════════════════════════════════════════════════════════
    // Full Validation → Placeholder → Re-validation Flow
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task FullFlow_ValidateWithDefaults_AlreadyValid()
    {
        // Arrange: stages with defaults but no programs
        var (buildPlate, _, downstreamStages) = await SetupBuildPlateWithDownstreamAsync(
            downstreamCount: 2, assignPrograms: false);

        // Act: validation passes because stages have default durations
        var result = await _sut.ValidateDownstreamReadinessAsync(buildPlate.Id);
        Assert.True(result.IsValid);

        // Placeholders can still be created proactively
        var stageIds = downstreamStages.Select(s => s.Id).ToList();
        var placeholders = await _sut.CreatePlaceholderProgramsAsync(buildPlate.Id, stageIds, "test-user");
        Assert.Equal(2, placeholders.Count);

        // Re-validate — still valid, now with explicit programs
        var revalidation = await _sut.ValidateDownstreamReadinessAsync(buildPlate.Id);
        Assert.True(revalidation.IsValid);
        Assert.Empty(revalidation.MissingPrograms);
    }
}
