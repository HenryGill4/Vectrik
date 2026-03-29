namespace Vectrik.Models;

/// <summary>
/// Per-operator per-machine proficiency profile, auto-populated via EMA learning.
/// Same pattern as LearningService — exponential moving average of setup times.
/// </summary>
public class OperatorSetupProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public int MachineId { get; set; }

    /// <summary>Optional: proficiency for a specific program on this machine.</summary>
    public int? MachineProgramId { get; set; }

    /// <summary>EMA of setup duration (minutes).</summary>
    public double? AverageSetupMinutes { get; set; }

    public int SampleCount { get; set; }

    /// <summary>EMA-smoothed variance (minutes²).</summary>
    public double? VarianceMinutes { get; set; }

    /// <summary>Fastest recorded setup (minutes).</summary>
    public double? FastestSetupMinutes { get; set; }

    /// <summary>
    /// Auto-calculated proficiency: 1=Novice, 2=Learning, 3=Competent, 4=Advanced, 5=Expert.
    /// Based on comparison to machine median setup time.
    /// </summary>
    public int ProficiencyLevel { get; set; } = 1;

    /// <summary>Admin flag: this operator is preferred for this machine.</summary>
    public bool IsPreferred { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ───────────────────────────────────────────

    public virtual User User { get; set; } = null!;
    public virtual Machine Machine { get; set; } = null!;
    public virtual MachineProgram? MachineProgram { get; set; }
}
