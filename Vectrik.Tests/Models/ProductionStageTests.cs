using Vectrik.Models;
using Xunit;

namespace Vectrik.Tests.Models;

public class ProductionStageTests
{
    // ── GetAssignedMachineIds ─────────────────────────────────

    [Fact]
    public void GetAssignedMachineIds_WhenNull_ReturnsEmptyList()
    {
        var stage = new ProductionStage { AssignedMachineIds = null };

        Assert.Empty(stage.GetAssignedMachineIds());
    }

    [Fact]
    public void GetAssignedMachineIds_WhenEmpty_ReturnsEmptyList()
    {
        var stage = new ProductionStage { AssignedMachineIds = "" };

        Assert.Empty(stage.GetAssignedMachineIds());
    }

    [Fact]
    public void GetAssignedMachineIds_ParsesCommaSeparated()
    {
        var stage = new ProductionStage { AssignedMachineIds = "SLS-001, CNC-001, EDM-001" };

        var ids = stage.GetAssignedMachineIds();

        Assert.Equal(3, ids.Count);
        Assert.Equal("SLS-001", ids[0]);
        Assert.Equal("CNC-001", ids[1]);
        Assert.Equal("EDM-001", ids[2]);
    }

    [Fact]
    public void SetAssignedMachineIds_SerializesToCommaSeparated()
    {
        var stage = new ProductionStage();

        stage.SetAssignedMachineIds(["SLS-001", "CNC-001"]);

        Assert.Equal("SLS-001,CNC-001", stage.AssignedMachineIds);
    }

    // ── CanMachineExecuteStage ────────────────────────────────

    [Fact]
    public void CanMachineExecuteStage_WhenNoAssignedMachines_AllowsAny()
    {
        var stage = new ProductionStage { AssignedMachineIds = null };

        Assert.True(stage.CanMachineExecuteStage("ANY-MACHINE"));
    }

    [Fact]
    public void CanMachineExecuteStage_WhenAssigned_AllowsOnlyAssigned()
    {
        var stage = new ProductionStage { AssignedMachineIds = "SLS-001,SLS-002" };

        Assert.True(stage.CanMachineExecuteStage("SLS-001"));
        Assert.True(stage.CanMachineExecuteStage("SLS-002"));
        Assert.False(stage.CanMachineExecuteStage("CNC-001"));
    }

    // ── GetTotalEstimatedCost ─────────────────────────────────

    [Fact]
    public void GetTotalEstimatedCost_CalculatesCorrectly()
    {
        var stage = new ProductionStage
        {
            DefaultHourlyRate = 100.00m,
            DefaultDurationHours = 2.5,
            DefaultMaterialCost = 30.00m
        };

        // 100 * 2.5 + 30 = 280
        Assert.Equal(280.00m, stage.GetTotalEstimatedCost());
    }

    // ── CustomFields ──────────────────────────────────────────

    [Fact]
    public void GetCustomFields_WhenDefault_ReturnsEmptyList()
    {
        var stage = new ProductionStage();

        Assert.Empty(stage.GetCustomFields());
    }

    [Fact]
    public void SetAndGetCustomFields_RoundTrips()
    {
        var stage = new ProductionStage();
        var fields = new List<CustomFieldDefinition>
        {
            new() { Name = "Temperature", FieldType = "number" }
        };

        stage.SetCustomFields(fields);
        var result = stage.GetCustomFields();

        Assert.Single(result);
        Assert.Equal("Temperature", result[0].Name);
    }

    // ── Defaults ──────────────────────────────────────────────

    [Fact]
    public void NewStage_HasCorrectDefaults()
    {
        var stage = new ProductionStage();

        Assert.True(stage.IsActive);
        Assert.True(stage.RequiresQualityCheck);
        Assert.False(stage.RequiresApproval);
        Assert.False(stage.AllowSkip);
        Assert.False(stage.IsOptional);
        Assert.False(stage.RequiresMachineAssignment);
        Assert.Equal(30, stage.DefaultSetupMinutes);
        Assert.Equal(85.00m, stage.DefaultHourlyRate);
        Assert.Equal(1.0, stage.DefaultDurationHours);
    }
}
