using Opcentrix_V3.Models;
using Xunit;

namespace Opcentrix_V3.Tests.Models;

public class PartStageRequirementTests
{
    // ── GetEffectiveEstimatedHours ────────────────────────────

    [Fact]
    public void GetEffectiveEstimatedHours_WhenAutoAndActualAverage_ReturnsActual()
    {
        var req = new PartStageRequirement
        {
            EstimateSource = "Auto",
            ActualAverageDurationHours = 2.5,
            EstimatedMinutes = 180 // 3.0 hours
        };

        Assert.Equal(2.5, req.GetEffectiveEstimatedHours());
    }

    [Fact]
    public void GetEffectiveEstimatedHours_WhenManual_ReturnsMinutesConvertedToHours()
    {
        var req = new PartStageRequirement
        {
            EstimateSource = "Manual",
            ActualAverageDurationHours = 2.5,
            EstimatedMinutes = 180 // 3.0 hours
        };

        Assert.Equal(3.0, req.GetEffectiveEstimatedHours());
    }

    [Fact]
    public void GetEffectiveEstimatedHours_WhenNoEstimate_FallsBackToStageDefault()
    {
        var stage = new ProductionStage { DefaultDurationHours = 4.0 };
        var req = new PartStageRequirement
        {
            ProductionStage = stage,
            EstimateSource = "Manual"
        };

        Assert.Equal(4.0, req.GetEffectiveEstimatedHours());
    }

    [Fact]
    public void GetEffectiveEstimatedHours_WhenNothingSet_Returns1()
    {
        var req = new PartStageRequirement { EstimateSource = "Manual" };

        Assert.Equal(1.0, req.GetEffectiveEstimatedHours());
    }

    // ── GetEffectiveHourlyRate ────────────────────────────────

    [Fact]
    public void GetEffectiveHourlyRate_WhenOverrideSet_ReturnsOverride()
    {
        var req = new PartStageRequirement { HourlyRateOverride = 120.00m };

        Assert.Equal(120.00m, req.GetEffectiveHourlyRate());
    }

    [Fact]
    public void GetEffectiveHourlyRate_WhenNoOverride_FallsBackToStageRate()
    {
        var stage = new ProductionStage { DefaultHourlyRate = 95.00m };
        var req = new PartStageRequirement { ProductionStage = stage };

        Assert.Equal(95.00m, req.GetEffectiveHourlyRate());
    }

    [Fact]
    public void GetEffectiveHourlyRate_WhenNothingSet_Returns85()
    {
        var req = new PartStageRequirement();

        Assert.Equal(85.00m, req.GetEffectiveHourlyRate());
    }

    // ── CalculateTotalEstimatedCost ───────────────────────────

    [Fact]
    public void CalculateTotalEstimatedCost_IncludesMaterialCost()
    {
        var req = new PartStageRequirement
        {
            HourlyRateOverride = 100.00m,
            EstimatedMinutes = 120, // 2.0 hours
            EstimateSource = "Manual",
            MaterialCost = 50.00m
        };

        // 100 * 2 + 50 = 250
        Assert.Equal(250.00m, req.CalculateTotalEstimatedCost());
    }

    // ── Machine preference helpers ────────────────────────────

    [Fact]
    public void GetPreferredMachineIds_WhenEmpty_ReturnsEmptyList()
    {
        var req = new PartStageRequirement();

        Assert.Empty(req.GetPreferredMachineIds());
    }

    [Fact]
    public void GetPreferredMachineIds_ParsesCommaSeparatedValues()
    {
        var req = new PartStageRequirement { PreferredMachineIds = "SLS-001, SLS-002, CNC-001" };

        var ids = req.GetPreferredMachineIds();

        Assert.Equal(3, ids.Count);
        Assert.Contains("SLS-001", ids);
        Assert.Contains("SLS-002", ids);
        Assert.Contains("CNC-001", ids);
    }

    [Fact]
    public void SetPreferredMachineIds_SerializesToCommaSeparated()
    {
        var req = new PartStageRequirement();

        req.SetPreferredMachineIds(["SLS-001", "SLS-002"]);

        Assert.Equal("SLS-001,SLS-002", req.PreferredMachineIds);
    }

    // ── CanMachineExecute ─────────────────────────────────────

    [Fact]
    public void CanMachineExecute_WhenSpecificMachineRequired_OnlyAllowsAssigned()
    {
        var req = new PartStageRequirement
        {
            RequiresSpecificMachine = true,
            AssignedMachineId = "SLS-001"
        };

        Assert.True(req.CanMachineExecute("SLS-001"));
        Assert.False(req.CanMachineExecute("SLS-002"));
    }

    [Fact]
    public void CanMachineExecute_WhenNoPreferences_AllowsAnyMachine()
    {
        var req = new PartStageRequirement();

        Assert.True(req.CanMachineExecute("ANY-MACHINE"));
    }

    [Fact]
    public void CanMachineExecute_WhenPreferredSet_AllowsOnlyPreferred()
    {
        var req = new PartStageRequirement { PreferredMachineIds = "SLS-001,CNC-001" };

        Assert.True(req.CanMachineExecute("SLS-001"));
        Assert.True(req.CanMachineExecute("CNC-001"));
        Assert.False(req.CanMachineExecute("EDM-001"));
    }

    // ── GetBestMachineId ──────────────────────────────────────

    [Fact]
    public void GetBestMachineId_WhenSpecificRequired_ReturnsAssigned()
    {
        var req = new PartStageRequirement
        {
            RequiresSpecificMachine = true,
            AssignedMachineId = "SLS-001"
        };

        Assert.Equal("SLS-001", req.GetBestMachineId());
    }

    [Fact]
    public void GetBestMachineId_WhenPreferred_ReturnsFirstPreferred()
    {
        var req = new PartStageRequirement { PreferredMachineIds = "CNC-001,SLS-001" };

        Assert.Equal("CNC-001", req.GetBestMachineId());
    }

    [Fact]
    public void GetBestMachineId_WhenNothingSet_ReturnsNull()
    {
        var req = new PartStageRequirement();

        Assert.Null(req.GetBestMachineId());
    }

    // ── CustomFieldValues ─────────────────────────────────────

    [Fact]
    public void GetCustomFieldValues_WhenEmpty_ReturnsEmptyDictionary()
    {
        var req = new PartStageRequirement();

        Assert.Empty(req.GetCustomFieldValues());
    }

    [Fact]
    public void SetAndGetCustomFieldValue_RoundTrips()
    {
        var req = new PartStageRequirement();

        req.SetCustomFieldValue("temperature", 250);
        var value = req.GetCustomFieldValue<int>("temperature");

        Assert.Equal(250, value);
    }

    // ── Defaults ──────────────────────────────────────────────

    [Fact]
    public void NewRequirement_HasCorrectDefaults()
    {
        var req = new PartStageRequirement();

        Assert.Equal(1, req.ExecutionOrder);
        Assert.True(req.IsRequired);
        Assert.True(req.IsActive);
        Assert.True(req.IsBlocking);
        Assert.False(req.AllowParallelExecution);
        Assert.False(req.RequiresSpecificMachine);
        Assert.Equal("Manual", req.EstimateSource);
        Assert.Equal(0, req.ActualSampleCount);
    }
}
