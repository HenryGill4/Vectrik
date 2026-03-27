using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vectrik.Models;

/// <summary>
/// 1:0..1 with Part. Holds all SLS/additive stacking and batch configuration.
/// Only created for parts whose ManufacturingApproach.IsAdditive == true.
/// Values here are PLANNING GUIDELINES — actual per-build counts come from the
/// slicer and are recorded on BuildPackagePart.Quantity.
/// </summary>
public class PartAdditiveBuildConfig
{
    public int Id { get; set; }

    [Required]
    public int PartId { get; set; }
    public virtual Part Part { get; set; } = null!;

    // ── Stacking Guidelines ──────────────────────────────────
    public bool AllowStacking { get; set; }
    public int MaxStackCount { get; set; } = 1;

    [Range(0.1, 500)]
    public double? SingleStackDurationHours { get; set; }

    [Range(0.1, 500)]
    public double? DoubleStackDurationHours { get; set; }

    [Range(0.1, 500)]
    public double? TripleStackDurationHours { get; set; }

    // Planning guideline: typical parts per build at each stack level.
    // Range widened to 500 for high-count plates (76+ suppressors).
    [Required, Range(1, 500)]
    public int PlannedPartsPerBuildSingle { get; set; } = 1;

    [Range(1, 500)]
    public int? PlannedPartsPerBuildDouble { get; set; }

    [Range(1, 500)]
    public int? PlannedPartsPerBuildTriple { get; set; }

    public bool EnableDoubleStack { get; set; }
    public bool EnableTripleStack { get; set; }

    // ── Computed (NotMapped) ─────────────────────────────────

    [NotMapped]
    public bool HasStackingConfiguration => AllowStacking && SingleStackDurationHours.HasValue;

    [NotMapped]
    public double? EffectiveSingleDuration => SingleStackDurationHours;

    [NotMapped]
    public bool HasValidDoubleStack => EnableDoubleStack
        && DoubleStackDurationHours.HasValue && PlannedPartsPerBuildDouble.HasValue;

    [NotMapped]
    public bool HasValidTripleStack => EnableTripleStack
        && TripleStackDurationHours.HasValue && PlannedPartsPerBuildTriple.HasValue;

    [NotMapped]
    public List<int> AvailableStackLevels
    {
        get
        {
            var levels = new List<int> { 1 };
            if (HasValidDoubleStack) levels.Add(2);
            if (HasValidTripleStack) levels.Add(3);
            return levels;
        }
    }

    public double? GetStackDuration(int level) => level switch
    {
        1 => EffectiveSingleDuration,
        2 => HasValidDoubleStack ? DoubleStackDurationHours : null,
        3 => HasValidTripleStack ? TripleStackDurationHours : null,
        _ => null
    };

    /// <summary>
    /// Returns total parts produced per build at the given stack level.
    /// E.g. 10 positions × 2 stack = 20 total parts per build.
    /// </summary>
    public int? GetPartsPerBuild(int level) => level switch
    {
        1 => PlannedPartsPerBuildSingle,
        2 => PlannedPartsPerBuildDouble,
        3 => PlannedPartsPerBuildTriple,
        _ => null
    };

    /// <summary>
    /// Returns the number of physical plate positions for the given stack level.
    /// PlannedPartsPerBuild stores TOTAL parts (positions × stack), so divide by level.
    /// </summary>
    public int GetPositionsPerBuild(int level)
    {
        var totalParts = GetPartsPerBuild(level);
        if (!totalParts.HasValue) return PlannedPartsPerBuildSingle;
        if (level <= 1) return totalParts.Value;
        return (int)Math.Ceiling((double)totalParts.Value / level);
    }

    public List<string> ValidateStackingConfiguration()
    {
        var errors = new List<string>();
        if (!AllowStacking) return errors;

        if (!SingleStackDurationHours.HasValue)
            errors.Add("Single stack duration is required when stacking is enabled.");

        if (EnableDoubleStack)
        {
            if (!DoubleStackDurationHours.HasValue)
                errors.Add("Double stack duration is required when double stacking is enabled.");
            if (!PlannedPartsPerBuildDouble.HasValue)
                errors.Add("Parts per build (double) is required when double stacking is enabled.");
        }

        if (EnableTripleStack)
        {
            if (!TripleStackDurationHours.HasValue)
                errors.Add("Triple stack duration is required when triple stacking is enabled.");
            if (!PlannedPartsPerBuildTriple.HasValue)
                errors.Add("Parts per build (triple) is required when triple stacking is enabled.");
        }

        return errors;
    }

    public int GetRecommendedStackLevel(int quantity)
    {
        if (!AllowStacking || quantity <= 0) return 1;

        var bestLevel = 1;
        var bestEfficiency = double.MaxValue;

        foreach (var level in AvailableStackLevels)
        {
            var duration = GetStackDuration(level);
            var partsPerBuild = GetPartsPerBuild(level);
            if (!duration.HasValue || !partsPerBuild.HasValue || partsPerBuild.Value == 0) continue;

            var builds = Math.Ceiling((double)quantity / partsPerBuild.Value);
            var totalHours = builds * duration.Value;
            var hoursPerPart = totalHours / quantity;

            if (hoursPerPart < bestEfficiency)
            {
                bestEfficiency = hoursPerPart;
                bestLevel = level;
            }
        }

        return bestLevel;
    }

    public double? CalculateStackEfficiency(int level, int quantity)
    {
        var duration = GetStackDuration(level);
        var partsPerBuild = GetPartsPerBuild(level);
        if (!duration.HasValue || !partsPerBuild.HasValue || partsPerBuild.Value == 0 || quantity <= 0) return null;

        var builds = Math.Ceiling((double)quantity / partsPerBuild.Value);
        return (builds * duration.Value) / quantity;
    }
}
