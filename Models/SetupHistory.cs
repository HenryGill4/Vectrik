using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

/// <summary>
/// Immutable ledger entry recording actual setup/changeover times.
/// Feeds the learning engine (EMA) and provides an audit trail.
/// Append-only — never update or delete rows.
/// </summary>
public class SetupHistory
{
    public int Id { get; set; }

    public int SetupDispatchId { get; set; }
    public int MachineId { get; set; }
    public int? MachineProgramId { get; set; }
    public int? PartId { get; set; }
    public int? OperatorUserId { get; set; }

    /// <summary>Actual time from dispatch start to setup complete (minutes).</summary>
    public double SetupDurationMinutes { get; set; }

    /// <summary>Changeover duration if applicable (minutes).</summary>
    public double? ChangeoverDurationMinutes { get; set; }

    /// <summary>True if this was a changeover (different program from previous).</summary>
    public bool WasChangeover { get; set; }

    /// <summary>The program that was on the machine before this setup.</summary>
    public int? PreviousProgramId { get; set; }

    /// <summary>JSON: list of tooling items used during this setup.</summary>
    public string? ToolingUsedJson { get; set; }

    /// <summary>Quality result after setup verification (pass/fail/conditional).</summary>
    [MaxLength(50)]
    public string? QualityResult { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public int? ShiftId { get; set; }

    // ── Navigation ───────────────────────────────────────────

    public virtual SetupDispatch SetupDispatch { get; set; } = null!;
    public virtual Machine Machine { get; set; } = null!;
    public virtual MachineProgram? MachineProgram { get; set; }
    public virtual User? Operator { get; set; }
    public virtual OperatingShift? Shift { get; set; }
}
