using Opcentrix_V3.Models;
using Xunit;

namespace Opcentrix_V3.Tests.Models;

public class PartAdditiveBuildConfigTests
{
    // ── Stacking configuration properties ─────────────────────

    [Fact]
    public void HasStackingConfiguration_WhenStackingAllowedAndSingleDurationSet_ReturnsTrue()
    {
        var config = new PartAdditiveBuildConfig { AllowStacking = true, SingleStackDurationHours = 5 };

        Assert.True(config.HasStackingConfiguration);
    }

    [Fact]
    public void HasStackingConfiguration_WhenStackingNotAllowed_ReturnsFalse()
    {
        var config = new PartAdditiveBuildConfig { AllowStacking = false, SingleStackDurationHours = 5 };

        Assert.False(config.HasStackingConfiguration);
    }

    [Fact]
    public void HasStackingConfiguration_WhenNoDuration_ReturnsFalse()
    {
        var config = new PartAdditiveBuildConfig { AllowStacking = true };

        Assert.False(config.HasStackingConfiguration);
    }

    [Fact]
    public void EffectiveSingleDuration_ReturnsSingleStackDuration()
    {
        var config = new PartAdditiveBuildConfig { SingleStackDurationHours = 3.0 };

        Assert.Equal(3.0, config.EffectiveSingleDuration);
    }

    [Fact]
    public void EffectiveSingleDuration_WhenNotSet_ReturnsNull()
    {
        var config = new PartAdditiveBuildConfig();

        Assert.Null(config.EffectiveSingleDuration);
    }

    [Fact]
    public void HasValidDoubleStack_WhenAllFieldsSet_ReturnsTrue()
    {
        var config = new PartAdditiveBuildConfig
        {
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8,
            PlannedPartsPerBuildDouble = 4
        };

        Assert.True(config.HasValidDoubleStack);
    }

    [Fact]
    public void HasValidDoubleStack_WhenDisabled_ReturnsFalse()
    {
        var config = new PartAdditiveBuildConfig
        {
            EnableDoubleStack = false,
            DoubleStackDurationHours = 8,
            PlannedPartsPerBuildDouble = 4
        };

        Assert.False(config.HasValidDoubleStack);
    }

    [Fact]
    public void HasValidTripleStack_WhenAllFieldsSet_ReturnsTrue()
    {
        var config = new PartAdditiveBuildConfig
        {
            EnableTripleStack = true,
            TripleStackDurationHours = 12,
            PlannedPartsPerBuildTriple = 6
        };

        Assert.True(config.HasValidTripleStack);
    }

    // ── AvailableStackLevels ──────────────────────────────────

    [Fact]
    public void AvailableStackLevels_AlwaysIncludesLevel1()
    {
        var config = new PartAdditiveBuildConfig();

        Assert.Contains(1, config.AvailableStackLevels);
    }

    [Fact]
    public void AvailableStackLevels_IncludesLevel2WhenDoubleStackValid()
    {
        var config = new PartAdditiveBuildConfig
        {
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8,
            PlannedPartsPerBuildDouble = 4
        };

        Assert.Contains(2, config.AvailableStackLevels);
    }

    [Fact]
    public void AvailableStackLevels_IncludesLevel3WhenTripleStackValid()
    {
        var config = new PartAdditiveBuildConfig
        {
            EnableTripleStack = true,
            TripleStackDurationHours = 12,
            PlannedPartsPerBuildTriple = 6
        };

        Assert.Contains(3, config.AvailableStackLevels);
    }

    [Fact]
    public void AvailableStackLevels_AllThreeLevelsWhenBothEnabled()
    {
        var config = new PartAdditiveBuildConfig
        {
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8,
            PlannedPartsPerBuildDouble = 4,
            EnableTripleStack = true,
            TripleStackDurationHours = 12,
            PlannedPartsPerBuildTriple = 6
        };

        Assert.Equal([1, 2, 3], config.AvailableStackLevels);
    }

    // ── GetStackDuration ──────────────────────────────────────

    [Fact]
    public void GetStackDuration_Level1_ReturnsEffectiveSingleDuration()
    {
        var config = new PartAdditiveBuildConfig { SingleStackDurationHours = 5.0 };

        Assert.Equal(5.0, config.GetStackDuration(1));
    }

    [Fact]
    public void GetStackDuration_Level2_WhenValid_ReturnsDoubleDuration()
    {
        var config = new PartAdditiveBuildConfig
        {
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8.5,
            PlannedPartsPerBuildDouble = 4
        };

        Assert.Equal(8.5, config.GetStackDuration(2));
    }

    [Fact]
    public void GetStackDuration_Level2_WhenInvalid_ReturnsNull()
    {
        var config = new PartAdditiveBuildConfig { EnableDoubleStack = false };

        Assert.Null(config.GetStackDuration(2));
    }

    [Fact]
    public void GetStackDuration_Level3_WhenValid_ReturnsTripleDuration()
    {
        var config = new PartAdditiveBuildConfig
        {
            EnableTripleStack = true,
            TripleStackDurationHours = 12.0,
            PlannedPartsPerBuildTriple = 6
        };

        Assert.Equal(12.0, config.GetStackDuration(3));
    }

    [Fact]
    public void GetStackDuration_InvalidLevel_ReturnsNull()
    {
        var config = new PartAdditiveBuildConfig();

        Assert.Null(config.GetStackDuration(4));
        Assert.Null(config.GetStackDuration(0));
        Assert.Null(config.GetStackDuration(-1));
    }

    // ── GetPartsPerBuild ──────────────────────────────────────

    [Fact]
    public void GetPartsPerBuild_Level1_ReturnsPlannedPartsPerBuildSingle()
    {
        var config = new PartAdditiveBuildConfig { PlannedPartsPerBuildSingle = 76 };

        Assert.Equal(76, config.GetPartsPerBuild(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void GetPartsPerBuild_InvalidLevel_ReturnsNull(int level)
    {
        var config = new PartAdditiveBuildConfig();

        Assert.Null(config.GetPartsPerBuild(level));
    }

    // ── ValidateStackingConfiguration ─────────────────────────

    [Fact]
    public void ValidateStackingConfiguration_WhenStackingDisabled_ReturnsNoErrors()
    {
        var config = new PartAdditiveBuildConfig { AllowStacking = false };

        Assert.Empty(config.ValidateStackingConfiguration());
    }

    [Fact]
    public void ValidateStackingConfiguration_WhenStackingEnabledButNoSingleDuration_ReturnsError()
    {
        var config = new PartAdditiveBuildConfig { AllowStacking = true };

        var errors = config.ValidateStackingConfiguration();

        Assert.Contains(errors, e => e.Contains("Single stack duration"));
    }

    [Fact]
    public void ValidateStackingConfiguration_WhenDoubleEnabledButMissingFields_ReturnsErrors()
    {
        var config = new PartAdditiveBuildConfig
        {
            AllowStacking = true,
            SingleStackDurationHours = 5,
            EnableDoubleStack = true
        };

        var errors = config.ValidateStackingConfiguration();

        Assert.Contains(errors, e => e.Contains("Double stack duration"));
        Assert.Contains(errors, e => e.Contains("Parts per build (double)"));
    }

    [Fact]
    public void ValidateStackingConfiguration_WhenTripleEnabledButMissingFields_ReturnsErrors()
    {
        var config = new PartAdditiveBuildConfig
        {
            AllowStacking = true,
            SingleStackDurationHours = 5,
            EnableTripleStack = true
        };

        var errors = config.ValidateStackingConfiguration();

        Assert.Contains(errors, e => e.Contains("Triple stack duration"));
        Assert.Contains(errors, e => e.Contains("Parts per build (triple)"));
    }

    [Fact]
    public void ValidateStackingConfiguration_WhenFullyConfigured_ReturnsNoErrors()
    {
        var config = new PartAdditiveBuildConfig
        {
            AllowStacking = true,
            SingleStackDurationHours = 5,
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8,
            PlannedPartsPerBuildDouble = 4,
            EnableTripleStack = true,
            TripleStackDurationHours = 12,
            PlannedPartsPerBuildTriple = 6
        };

        Assert.Empty(config.ValidateStackingConfiguration());
    }

    // ── GetRecommendedStackLevel ──────────────────────────────

    [Fact]
    public void GetRecommendedStackLevel_WhenStackingDisabled_Returns1()
    {
        var config = new PartAdditiveBuildConfig { AllowStacking = false };

        Assert.Equal(1, config.GetRecommendedStackLevel(10));
    }

    [Fact]
    public void GetRecommendedStackLevel_WhenQuantityZeroOrNegative_Returns1()
    {
        var config = new PartAdditiveBuildConfig { AllowStacking = true, SingleStackDurationHours = 5, PlannedPartsPerBuildSingle = 2 };

        Assert.Equal(1, config.GetRecommendedStackLevel(0));
        Assert.Equal(1, config.GetRecommendedStackLevel(-1));
    }

    [Fact]
    public void GetRecommendedStackLevel_PicksMostEfficientLevel()
    {
        var config = new PartAdditiveBuildConfig
        {
            AllowStacking = true,
            SingleStackDurationHours = 5,
            PlannedPartsPerBuildSingle = 1,      // 5 hrs/part
            EnableDoubleStack = true,
            DoubleStackDurationHours = 6,
            PlannedPartsPerBuildDouble = 3,       // 2 hrs/part — best
            EnableTripleStack = true,
            TripleStackDurationHours = 10,
            PlannedPartsPerBuildTriple = 4         // 2.5 hrs/part
        };

        Assert.Equal(2, config.GetRecommendedStackLevel(6));
    }

    // ── CalculateStackEfficiency ──────────────────────────────

    [Fact]
    public void CalculateStackEfficiency_WhenValidInputs_ReturnsHoursPerPart()
    {
        var config = new PartAdditiveBuildConfig
        {
            SingleStackDurationHours = 5,
            PlannedPartsPerBuildSingle = 2
        };

        // quantity=4 → ceil(4/2)=2 builds * 5 hrs = 10 hrs / 4 = 2.5
        Assert.Equal(2.5, config.CalculateStackEfficiency(1, 4));
    }

    [Fact]
    public void CalculateStackEfficiency_WhenQuantityZero_ReturnsNull()
    {
        var config = new PartAdditiveBuildConfig { SingleStackDurationHours = 5, PlannedPartsPerBuildSingle = 2 };

        Assert.Null(config.CalculateStackEfficiency(1, 0));
    }

    [Fact]
    public void CalculateStackEfficiency_WhenNegativeQuantity_ReturnsNull()
    {
        var config = new PartAdditiveBuildConfig { SingleStackDurationHours = 5, PlannedPartsPerBuildSingle = 2 };

        Assert.Null(config.CalculateStackEfficiency(1, -1));
    }

    [Fact]
    public void CalculateStackEfficiency_WhenMissingDuration_ReturnsNull()
    {
        var config = new PartAdditiveBuildConfig { PlannedPartsPerBuildSingle = 2 };

        Assert.Null(config.CalculateStackEfficiency(1, 4));
    }

    [Fact]
    public void CalculateStackEfficiency_WhenInvalidLevel_ReturnsNull()
    {
        var config = new PartAdditiveBuildConfig();

        Assert.Null(config.CalculateStackEfficiency(99, 4));
    }

    // ── GetPositionsPerBuild ───────────────────────────────────

    [Fact]
    public void GetPositionsPerBuild_SingleStack_ReturnsSameAsGetPartsPerBuild()
    {
        var config = new PartAdditiveBuildConfig { PlannedPartsPerBuildSingle = 10 };

        Assert.Equal(10, config.GetPositionsPerBuild(1));
        Assert.Equal(config.GetPartsPerBuild(1), config.GetPositionsPerBuild(1));
    }

    [Fact]
    public void GetPositionsPerBuild_DoubleStack_DividesTotalByTwo()
    {
        var config = new PartAdditiveBuildConfig
        {
            PlannedPartsPerBuildSingle = 10,
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8,
            PlannedPartsPerBuildDouble = 20  // 10 positions × 2 stack = 20 total
        };

        Assert.Equal(10, config.GetPositionsPerBuild(2));
    }

    [Fact]
    public void GetPositionsPerBuild_TripleStack_DividesTotalByThree()
    {
        var config = new PartAdditiveBuildConfig
        {
            PlannedPartsPerBuildSingle = 10,
            EnableTripleStack = true,
            TripleStackDurationHours = 12,
            PlannedPartsPerBuildTriple = 30  // 10 positions × 3 stack = 30 total
        };

        Assert.Equal(10, config.GetPositionsPerBuild(3));
    }

    [Fact]
    public void GetPositionsPerBuild_OddNumber_RoundsUp()
    {
        var config = new PartAdditiveBuildConfig
        {
            PlannedPartsPerBuildSingle = 5,
            EnableDoubleStack = true,
            DoubleStackDurationHours = 8,
            PlannedPartsPerBuildDouble = 15  // 15 / 2 = 7.5 → ceiling = 8
        };

        Assert.Equal(8, config.GetPositionsPerBuild(2));
    }

    [Fact]
    public void GetPositionsPerBuild_NoConfigForLevel_ReturnsSingleDefault()
    {
        var config = new PartAdditiveBuildConfig { PlannedPartsPerBuildSingle = 10 };

        // Level 2 not configured → GetPartsPerBuild returns null → falls back to Single
        Assert.Equal(10, config.GetPositionsPerBuild(2));
    }

    // ── Default values ────────────────────────────────────────

    [Fact]
    public void NewConfig_HasCorrectDefaults()
    {
        var config = new PartAdditiveBuildConfig();

        Assert.Equal(1, config.PlannedPartsPerBuildSingle);
        Assert.Equal(1, config.MaxStackCount);
        Assert.False(config.AllowStacking);
        Assert.False(config.EnableDoubleStack);
        Assert.False(config.EnableTripleStack);
    }

    // ── Part.AdditiveBuildConfig navigation ───────────────────

    [Fact]
    public void NewPart_HasNullAdditiveBuildConfig()
    {
        var part = new Part();

        Assert.Null(part.AdditiveBuildConfig);
    }

    [Fact]
    public void NewPart_HasCorrectDefaults()
    {
        var part = new Part();

        Assert.True(part.IsActive);
        Assert.Equal("Ti-6Al-4V Grade 5", part.Material);
        Assert.Null(part.ManufacturingApproachId);
        Assert.Null(part.ManufacturingApproach);
        Assert.False(part.IsDefensePart);
    }
}
