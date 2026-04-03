namespace Vectrik.Models;

/// <summary>
/// Single-row configuration for SLS build scheduling and stack selection algorithm weights.
/// Controls how ProgramSchedulingService and BuildAdvisorService score scheduling options.
/// </summary>
public class SchedulingWeights
{
    public int Id { get; set; }

    // ── ProgramScheduling: ComputeOptionScore ────────────────

    /// <summary>Base score for every scheduling option (0-100).</summary>
    public int BaseScore { get; set; } = 50;

    /// <summary>Bonus when operator is available for changeover.</summary>
    public int ChangeoverAlignmentBonus { get; set; } = 30;

    /// <summary>Points deducted per hour of machine downtime.</summary>
    public int DowntimePenaltyPerHour { get; set; } = 3;

    /// <summary>Maximum downtime penalty cap (absolute value).</summary>
    public int MaxDowntimePenalty { get; set; } = 40;

    /// <summary>Bonus when build starts within 4 hours.</summary>
    public int EarlinessBonus4h { get; set; } = 20;

    /// <summary>Bonus when build starts within 24 hours.</summary>
    public int EarlinessBonus24h { get; set; } = 10;

    /// <summary>Maximum penalty for overproduction (100% excess).</summary>
    public int OverproductionPenaltyMax { get; set; } = 20;

    /// <summary>Bonus when build spans a weekend cleanly.</summary>
    public int WeekendOptimizationBonus { get; set; } = 25;

    /// <summary>Extra bonus for shift-aligned scheduling options.</summary>
    public int ShiftAlignedBonus { get; set; } = 15;

    // ── BuildAdvisor: SelectStackLevel ───────────────────────

    /// <summary>Bonus for stack levels with changeover during operator hours.</summary>
    public int StackChangeoverBonus { get; set; } = 30;

    /// <summary>Bonus for stack levels that match remaining demand without overproduction.</summary>
    public int StackDemandFitBonus { get; set; } = 30;

    /// <summary>Multiplier for parts-per-hour efficiency scoring.</summary>
    public decimal StackEfficiencyMultiplier { get; set; } = 5m;

    // ── Audit ────────────────────────────────────────────────

    public DateTime LastModifiedDate { get; set; }
    public string? LastModifiedBy { get; set; }
}
