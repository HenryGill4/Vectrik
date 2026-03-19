using Opcentrix_V3.Models;
using Xunit;

namespace Opcentrix_V3.Tests.Models;

public class PartModelTests
{
    // ── Per-part hour computed properties ──────────────────────

    [Fact]
    public void SlsPerPartHours_WhenBothValuesSet_ReturnsCorrectRatio()
    {
        var part = new Part { SlsBuildDurationHours = 10, SlsPartsPerBuild = 5 };

        Assert.Equal(2.0, part.SlsPerPartHours);
    }

    [Fact]
    public void SlsPerPartHours_WhenDurationMissing_ReturnsNull()
    {
        var part = new Part { SlsPartsPerBuild = 5 };

        Assert.Null(part.SlsPerPartHours);
    }

    [Fact]
    public void SlsPerPartHours_WhenPartsPerBuildMissing_ReturnsNull()
    {
        var part = new Part { SlsBuildDurationHours = 10 };

        Assert.Null(part.SlsPerPartHours);
    }

    [Fact]
    public void SlsPerPartHours_WhenPartsPerBuildZero_ReturnsNull()
    {
        var part = new Part { SlsBuildDurationHours = 10, SlsPartsPerBuild = 0 };

        Assert.Null(part.SlsPerPartHours);
    }

    [Fact]
    public void DepowderingPerPartHours_WhenBothValuesSet_ReturnsCorrectRatio()
    {
        var part = new Part { DepowderingDurationHours = 4, DepowderingPartsPerBatch = 8 };

        Assert.Equal(0.5, part.DepowderingPerPartHours);
    }

    [Fact]
    public void DepowderingPerPartHours_WhenMissing_ReturnsNull()
    {
        var part = new Part();

        Assert.Null(part.DepowderingPerPartHours);
    }

    [Fact]
    public void HeatTreatmentPerPartHours_WhenBothValuesSet_ReturnsCorrectRatio()
    {
        var part = new Part { HeatTreatmentDurationHours = 6, HeatTreatmentPartsPerBatch = 3 };

        Assert.Equal(2.0, part.HeatTreatmentPerPartHours);
    }

    [Fact]
    public void WireEdmPerPartHours_WhenBothValuesSet_ReturnsCorrectRatio()
    {
        var part = new Part { WireEdmDurationHours = 8, WireEdmPartsPerSession = 4 };

        Assert.Equal(2.0, part.WireEdmPerPartHours);
    }

    // ── Stacking configuration properties ─────────────────────

    [Fact]
    public void HasStackingConfiguration_WhenStackingAllowedAndSingleDurationSet_ReturnsTrue()
    {
        var part = new Part { AllowStacking = true, SingleStackDurationHours = 5 };

        Assert.True(part.HasStackingConfiguration);
    }

    [Fact]
    public void HasStackingConfiguration_WhenStackingNotAllowed_ReturnsFalse()
    {
        var part = new Part { AllowStacking = false, SingleStackDurationHours = 5 };

        Assert.False(part.HasStackingConfiguration);
    }

    [Fact]
    public void HasStackingConfiguration_WhenNoDuration_ReturnsFalse()
    {
        var part = new Part { AllowStacking = true };

        Assert.False(part.HasStackingConfiguration);
    }

    [Fact]
    public void EffectiveSingleDuration_PrefersStackDurationOverEstimate()
    {
        var part = new Part { SingleStackDurationHours = 3.0, StageEstimateSingle = 7.0 };

        Assert.Equal(3.0, part.EffectiveSingleDuration);
    }

    [Fact]
    public void EffectiveSingleDuration_FallsBackToStageEstimate()
    {
        var part = new Part { StageEstimateSingle = 7.0 };

        Assert.Equal(7.0, part.EffectiveSingleDuration);
    }

    [Fact]
    public void EffectiveSingleDuration_WhenNeitherSet_ReturnsNull()
    {
        var part = new Part();

        Assert.Null(part.EffectiveSingleDuration);
    }

    [Fact]
    public void HasValidDoubleStack_WhenAllFieldsSet_ReturnsTrue()
    {
        var part = new Part
        {
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8,
            PartsPerBuildDouble = 4
        };

        Assert.True(part.HasValidDoubleStack);
    }

    [Fact]
    public void HasValidDoubleStack_WhenDisabled_ReturnsFalse()
    {
        var part = new Part
        {
            EnableDoubleStack = false,
            DoubleStackDurationHours = 8,
            PartsPerBuildDouble = 4
        };

        Assert.False(part.HasValidDoubleStack);
    }

    [Fact]
    public void HasValidTripleStack_WhenAllFieldsSet_ReturnsTrue()
    {
        var part = new Part
        {
            EnableTripleStack = true,
            TripleStackDurationHours = 12,
            PartsPerBuildTriple = 6
        };

        Assert.True(part.HasValidTripleStack);
    }

    // ── AvailableStackLevels ──────────────────────────────────

    [Fact]
    public void AvailableStackLevels_AlwaysIncludesLevel1()
    {
        var part = new Part();

        Assert.Contains(1, part.AvailableStackLevels);
    }

    [Fact]
    public void AvailableStackLevels_IncludesLevel2WhenDoubleStackValid()
    {
        var part = new Part
        {
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8,
            PartsPerBuildDouble = 4
        };

        Assert.Contains(2, part.AvailableStackLevels);
    }

    [Fact]
    public void AvailableStackLevels_IncludesLevel3WhenTripleStackValid()
    {
        var part = new Part
        {
            EnableTripleStack = true,
            TripleStackDurationHours = 12,
            PartsPerBuildTriple = 6
        };

        Assert.Contains(3, part.AvailableStackLevels);
    }

    [Fact]
    public void AvailableStackLevels_AllThreeLevelsWhenBothEnabled()
    {
        var part = new Part
        {
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8,
            PartsPerBuildDouble = 4,
            EnableTripleStack = true,
            TripleStackDurationHours = 12,
            PartsPerBuildTriple = 6
        };

        Assert.Equal([1, 2, 3], part.AvailableStackLevels);
    }

    // ── GetStackDuration ──────────────────────────────────────

    [Fact]
    public void GetStackDuration_Level1_ReturnsEffectiveSingleDuration()
    {
        var part = new Part { SingleStackDurationHours = 5.0 };

        Assert.Equal(5.0, part.GetStackDuration(1));
    }

    [Fact]
    public void GetStackDuration_Level2_WhenValid_ReturnsDoubleDuration()
    {
        var part = new Part
        {
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8.5,
            PartsPerBuildDouble = 4
        };

        Assert.Equal(8.5, part.GetStackDuration(2));
    }

    [Fact]
    public void GetStackDuration_Level2_WhenInvalid_ReturnsNull()
    {
        var part = new Part { EnableDoubleStack = false };

        Assert.Null(part.GetStackDuration(2));
    }

    [Fact]
    public void GetStackDuration_Level3_WhenValid_ReturnsTripleDuration()
    {
        var part = new Part
        {
            EnableTripleStack = true,
            TripleStackDurationHours = 12.0,
            PartsPerBuildTriple = 6
        };

        Assert.Equal(12.0, part.GetStackDuration(3));
    }

    [Fact]
    public void GetStackDuration_InvalidLevel_ReturnsNull()
    {
        var part = new Part();

        Assert.Null(part.GetStackDuration(4));
        Assert.Null(part.GetStackDuration(0));
        Assert.Null(part.GetStackDuration(-1));
    }

    // ── GetPartsPerBuild ──────────────────────────────────────

    [Fact]
    public void GetPartsPerBuild_Level1_ReturnsPartsPerBuildSingle()
    {
        var part = new Part { PartsPerBuildSingle = 3 };

        Assert.Equal(3, part.GetPartsPerBuild(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void GetPartsPerBuild_InvalidLevel_ReturnsNull(int level)
    {
        var part = new Part();

        Assert.Null(part.GetPartsPerBuild(level));
    }

    // ── ValidateStackingConfiguration ─────────────────────────

    [Fact]
    public void ValidateStackingConfiguration_WhenStackingDisabled_ReturnsNoErrors()
    {
        var part = new Part { AllowStacking = false };

        Assert.Empty(part.ValidateStackingConfiguration());
    }

    [Fact]
    public void ValidateStackingConfiguration_WhenStackingEnabledButNoSingleDuration_ReturnsError()
    {
        var part = new Part { AllowStacking = true };

        var errors = part.ValidateStackingConfiguration();

        Assert.Contains(errors, e => e.Contains("Single stack duration"));
    }

    [Fact]
    public void ValidateStackingConfiguration_WhenDoubleEnabledButMissingFields_ReturnsErrors()
    {
        var part = new Part
        {
            AllowStacking = true,
            SingleStackDurationHours = 5,
            EnableDoubleStack = true
            // Missing DoubleStackDurationHours and PartsPerBuildDouble
        };

        var errors = part.ValidateStackingConfiguration();

        Assert.Contains(errors, e => e.Contains("Double stack duration"));
        Assert.Contains(errors, e => e.Contains("Parts per build (double)"));
    }

    [Fact]
    public void ValidateStackingConfiguration_WhenTripleEnabledButMissingFields_ReturnsErrors()
    {
        var part = new Part
        {
            AllowStacking = true,
            SingleStackDurationHours = 5,
            EnableTripleStack = true
            // Missing TripleStackDurationHours and PartsPerBuildTriple
        };

        var errors = part.ValidateStackingConfiguration();

        Assert.Contains(errors, e => e.Contains("Triple stack duration"));
        Assert.Contains(errors, e => e.Contains("Parts per build (triple)"));
    }

    [Fact]
    public void ValidateStackingConfiguration_WhenFullyConfigured_ReturnsNoErrors()
    {
        var part = new Part
        {
            AllowStacking = true,
            SingleStackDurationHours = 5,
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8,
            PartsPerBuildDouble = 4,
            EnableTripleStack = true,
            TripleStackDurationHours = 12,
            PartsPerBuildTriple = 6
        };

        Assert.Empty(part.ValidateStackingConfiguration());
    }

    // ── GetRecommendedStackLevel ──────────────────────────────

    [Fact]
    public void GetRecommendedStackLevel_WhenStackingDisabled_Returns1()
    {
        var part = new Part { AllowStacking = false };

        Assert.Equal(1, part.GetRecommendedStackLevel(10));
    }

    [Fact]
    public void GetRecommendedStackLevel_WhenQuantityZeroOrNegative_Returns1()
    {
        var part = new Part { AllowStacking = true, SingleStackDurationHours = 5, PartsPerBuildSingle = 2 };

        Assert.Equal(1, part.GetRecommendedStackLevel(0));
        Assert.Equal(1, part.GetRecommendedStackLevel(-1));
    }

    [Fact]
    public void GetRecommendedStackLevel_PicksMostEfficientLevel()
    {
        var part = new Part
        {
            AllowStacking = true,
            SingleStackDurationHours = 5,
            PartsPerBuildSingle = 1,      // 5 hrs/part
            EnableDoubleStack = true,
            DoubleStackDurationHours = 6,
            PartsPerBuildDouble = 3,       // 2 hrs/part — best
            EnableTripleStack = true,
            TripleStackDurationHours = 10,
            PartsPerBuildTriple = 4         // 2.5 hrs/part
        };

        Assert.Equal(2, part.GetRecommendedStackLevel(6));
    }

    // ── CalculateStackEfficiency ──────────────────────────────

    [Fact]
    public void CalculateStackEfficiency_WhenValidInputs_ReturnsHoursPerPart()
    {
        var part = new Part
        {
            SingleStackDurationHours = 5,
            PartsPerBuildSingle = 2
        };

        // quantity=4 → ceil(4/2)=2 builds * 5 hrs = 10 hrs / 4 = 2.5
        Assert.Equal(2.5, part.CalculateStackEfficiency(1, 4));
    }

    [Fact]
    public void CalculateStackEfficiency_WhenQuantityZero_ReturnsNull()
    {
        var part = new Part { SingleStackDurationHours = 5, PartsPerBuildSingle = 2 };

        Assert.Null(part.CalculateStackEfficiency(1, 0));
    }

    [Fact]
    public void CalculateStackEfficiency_WhenNegativeQuantity_ReturnsNull()
    {
        var part = new Part { SingleStackDurationHours = 5, PartsPerBuildSingle = 2 };

        Assert.Null(part.CalculateStackEfficiency(1, -1));
    }

    [Fact]
    public void CalculateStackEfficiency_WhenMissingDuration_ReturnsNull()
    {
        var part = new Part { PartsPerBuildSingle = 2 };

        Assert.Null(part.CalculateStackEfficiency(1, 4));
    }

    [Fact]
    public void CalculateStackEfficiency_WhenInvalidLevel_ReturnsNull()
    {
        var part = new Part();

        Assert.Null(part.CalculateStackEfficiency(99, 4));
    }

    // ── Default values ────────────────────────────────────────

    [Fact]
    public void NewPart_HasCorrectDefaults()
    {
        var part = new Part();

        Assert.True(part.IsActive);
        Assert.Equal(1, part.PartsPerBuildSingle);
        Assert.Equal(1, part.MaxStackCount);
        Assert.Equal("Ti-6Al-4V Grade 5", part.Material);
        Assert.Equal("CNC Machining", part.ManufacturingApproach);
        Assert.False(part.AllowStacking);
        Assert.False(part.EnableDoubleStack);
        Assert.False(part.EnableTripleStack);
        Assert.False(part.IsDefensePart);
    }
}
