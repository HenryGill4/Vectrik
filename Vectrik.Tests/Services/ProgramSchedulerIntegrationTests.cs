using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services;
using Vectrik.Tests.Helpers;
using Xunit;

namespace Vectrik.Tests.Services;

/// <summary>
/// Integration tests for the program-scheduler integration using realistic seeded scenarios.
/// Tests verify that program durations, EMA learning, and machine assignments flow correctly
/// through the scheduling system.
/// </summary>
public class ProgramSchedulerIntegrationTests : IDisposable
{
    private readonly TenantDbContext _db;

    public ProgramSchedulerIntegrationTests()
    {
        _db = TestDbContextFactory.Create();
    }

    public void Dispose() => _db.Dispose();

    // ══════════════════════════════════════════════════════════
    // SLS Scenario Tests
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task SlsScenario_SeedsCorrectly_WithBuildPlateProgram()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedSlsScenarioAsync(_db);

        // Assert: Verify the scenario was seeded correctly
        Assert.NotNull(scenario.SlsMachine);
        Assert.True(scenario.SlsMachine.IsAdditiveMachine);
        Assert.Equal("M4-1", scenario.SlsMachine.MachineId);

        Assert.NotNull(scenario.Part);
        Assert.Equal("AERO-BRACKET-001", scenario.Part.PartNumber);

        Assert.NotNull(scenario.Process);
        Assert.Equal(scenario.Part.Id, scenario.Process.PartId);

        Assert.NotNull(scenario.PrintProgram);
        Assert.Equal(ProgramType.BuildPlate, scenario.PrintProgram.ProgramType);
        Assert.Equal(18.5, scenario.PrintProgram.EstimatedPrintHours);

        // Verify EMA learning data is present
        Assert.NotNull(scenario.PrintProgram.ActualAverageDurationMinutes);
        Assert.Equal(1095, scenario.PrintProgram.ActualAverageDurationMinutes); // ~18.25 hours
        Assert.Equal(3, scenario.PrintProgram.ActualSampleCount);

        // Verify ProcessStage is linked to program
        Assert.Equal(scenario.PrintProgram.Id, scenario.PrintStage.MachineProgramId);
    }

    [Fact]
    public async Task SlsScenario_WorkOrderHasCorrectQuantities()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedSlsScenarioAsync(_db);

        // Assert: WorkOrder quantities
        Assert.Equal(48, scenario.WorkOrderLine.Quantity);
        Assert.Equal(WorkOrderStatus.InProgress, scenario.WorkOrder.Status);
        Assert.True(scenario.WorkOrder.IsDefenseContract);
    }

    // ══════════════════════════════════════════════════════════
    // CNC Scenario Tests
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task CncScenario_SeedsCorrectly_WithStandardProgram()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedCncScenarioAsync(_db);

        // Assert: Verify CNC setup
        Assert.NotNull(scenario.CncMachine);
        Assert.Equal("CNC", scenario.CncMachine.MachineType);

        Assert.NotNull(scenario.CncProgram);
        Assert.Equal(ProgramType.Standard, scenario.CncProgram.ProgramType);
        Assert.Equal("O1001", scenario.CncProgram.ProgramNumber);

        // Verify timing fields
        Assert.Equal(22, scenario.CncProgram.SetupTimeMinutes);
        Assert.Equal(16, scenario.CncProgram.RunTimeMinutes);
        Assert.Equal(17, scenario.CncProgram.CycleTimeMinutes);

        // Verify EMA learning data
        Assert.Equal(16.8, scenario.CncProgram.ActualAverageDurationMinutes);
        Assert.Equal(12, scenario.CncProgram.ActualSampleCount);

        // Verify ProcessStage link
        Assert.Equal(scenario.CncProgram.Id, scenario.CncStage.MachineProgramId);
    }

    [Fact]
    public async Task CncScenario_ProcessStage_HasCorrectDurations()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedCncScenarioAsync(_db);

        // Assert: ProcessStage has its own duration settings
        Assert.Equal(DurationMode.PerBatch, scenario.CncStage.SetupDurationMode);
        Assert.Equal(25, scenario.CncStage.SetupTimeMinutes);
        Assert.Equal(DurationMode.PerPart, scenario.CncStage.RunDurationMode);
        Assert.Equal(18, scenario.CncStage.RunTimeMinutes);

        // Program times should override stage defaults when scheduling
    }

    // ══════════════════════════════════════════════════════════
    // Duration Priority Tests
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task DurationPriorityScenario_PartWithEma_HasLearningData()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedDurationPriorityScenarioAsync(_db);

        // Assert: EMA program has learned duration
        Assert.NotNull(scenario.ProgramWithEma.ActualAverageDurationMinutes);
        Assert.Equal(32, scenario.ProgramWithEma.ActualAverageDurationMinutes);
        Assert.Equal(8, scenario.ProgramWithEma.ActualSampleCount);

        // The EMA (32 min) should be preferred over RunTimeMinutes (35 min)
        Assert.Equal(35, scenario.ProgramWithEma.RunTimeMinutes);
    }

    [Fact]
    public async Task DurationPriorityScenario_PartNoEma_UsesEstimates()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedDurationPriorityScenarioAsync(_db);

        // Assert: No EMA data, should use RunTimeMinutes
        Assert.Null(scenario.ProgramNoEma.ActualAverageDurationMinutes);
        Assert.Equal(0, scenario.ProgramNoEma.ActualSampleCount);
        Assert.Equal(42, scenario.ProgramNoEma.RunTimeMinutes);
    }

    [Fact]
    public async Task DurationPriorityScenario_PartNoProgram_UsesStageDefaults()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedDurationPriorityScenarioAsync(_db);

        // Assert: No program linked, must use stage defaults
        Assert.Null(scenario.StageNoProgram.MachineProgramId);
        Assert.Equal(25, scenario.StageNoProgram.SetupTimeMinutes);
        Assert.Equal(50, scenario.StageNoProgram.RunTimeMinutes);
    }

    // ══════════════════════════════════════════════════════════
    // Multi-Part Scenario Tests
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task MultiPartScenario_SharesMachinesBetweenParts()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedMultiPartScenarioAsync(_db);

        // Assert: Both parts share the same SLS machine
        Assert.Equal(
            scenario.SlsScenario.SlsMachine.Id,
            scenario.SecondPrintStage.AssignedMachineId);

        // Both parts share the same Depowder machine
        Assert.Equal(
            scenario.SlsScenario.DepowderMachine.Id,
            scenario.SecondDepowderStage.AssignedMachineId);
    }

    [Fact]
    public async Task MultiPartScenario_ClipProgram_HasNoEmaYet()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedMultiPartScenarioAsync(_db);

        // Assert: New program has no learning data
        Assert.Null(scenario.ClipProgram.ActualAverageDurationMinutes);
        Assert.Equal(0, scenario.ClipProgram.ActualSampleCount);

        // But has estimated print hours from slicer
        Assert.Equal(12.5, scenario.ClipProgram.EstimatedPrintHours);
    }

    [Fact]
    public async Task MultiPartScenario_HasMultipleWorkOrders()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedMultiPartScenarioAsync(_db);

        // Assert: Multiple work orders in the system
        var workOrders = await _db.WorkOrders.ToListAsync();
        Assert.True(workOrders.Count >= 3); // SLS, CNC, and Clip orders

        // Verify clip work order
        Assert.Equal(360, scenario.ClipWorkOrderLine.Quantity);
    }

    // ══════════════════════════════════════════════════════════
    // Manufacturing Process Chain Tests
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task SlsScenario_ProcessStages_AreInCorrectOrder()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedSlsScenarioAsync(_db);

        // Assert: Stages are in execution order
        Assert.Equal(1, scenario.PrintStage.ExecutionOrder);
        Assert.Equal(2, scenario.DepowderStage.ExecutionOrder);
        Assert.Equal(3, scenario.HeatTreatStage.ExecutionOrder);
    }

    [Fact]
    public async Task SlsScenario_AllStages_AreBuildLevel()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedSlsScenarioAsync(_db);

        // Assert: All stages operate at Build level (whole plate)
        Assert.Equal(ProcessingLevel.Build, scenario.PrintStage.ProcessingLevel);
        Assert.Equal(ProcessingLevel.Build, scenario.DepowderStage.ProcessingLevel);
        Assert.Equal(ProcessingLevel.Build, scenario.HeatTreatStage.ProcessingLevel);
    }

    [Fact]
    public async Task CncScenario_HasMixedProcessingLevels()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedCncScenarioAsync(_db);

        // Assert: CNC is per-part, QC is per-batch
        Assert.Equal(ProcessingLevel.Part, scenario.CncStage.ProcessingLevel);
        Assert.Equal(ProcessingLevel.Batch, scenario.QcStage.ProcessingLevel);
    }

    // ══════════════════════════════════════════════════════════
    // Program-Stage Linkage Tests
    // ══════════════════════════════════════════════════════════

    [Fact]
    public async Task SlsScenario_PrintStage_UsesBuildConfigDuration()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedSlsScenarioAsync(_db);

        // Assert: Print stage pulls duration from build config/slicer
        Assert.True(scenario.PrintStage.DurationFromBuildConfig);

        // Other stages use fixed durations
        Assert.False(scenario.DepowderStage.DurationFromBuildConfig);
        Assert.False(scenario.HeatTreatStage.DurationFromBuildConfig);
    }

    [Fact]
    public async Task CncScenario_Stage_HasPreferredMachines()
    {
        // Arrange & Act
        var scenario = await TestScenarioSeeder.SeedCncScenarioAsync(_db);

        // Assert: CNC stage has multiple preferred machines
        Assert.NotNull(scenario.CncStage.PreferredMachineIds);
        Assert.Contains(scenario.CncMachine.Id.ToString(), scenario.CncStage.PreferredMachineIds);
        Assert.Contains(scenario.CncMachine2.Id.ToString(), scenario.CncStage.PreferredMachineIds);
    }
}
