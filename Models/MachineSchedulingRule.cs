using System.ComponentModel.DataAnnotations;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

/// <summary>
/// A scheduling constraint rule attached to a specific machine.
/// Rules are enforced as hard blocks — the scheduler will skip slots that violate any enabled rule
/// and advance to the next valid slot, up to a configurable search horizon.
/// </summary>
public class MachineSchedulingRule
{
    public int Id { get; set; }

    /// <summary>FK to the machine this rule applies to.</summary>
    public int MachineId { get; set; }

    /// <summary>The type of scheduling constraint this rule enforces.</summary>
    public SchedulingRuleType RuleType { get; set; }

    /// <summary>Human-readable label for this rule (auto-generated from type but editable).</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional explanation of why this rule exists.</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Toggle on/off without deleting the rule.</summary>
    public bool IsEnabled { get; set; } = true;

    // ── RequireOperatorForChangeover parameters ──────────────
    // No extra fields needed — enforcement uses shift data from OperatingShifts.
    // When enabled, the scheduler blocks any slot whose changeover window falls
    // entirely outside operator shift windows, preventing builds from being
    // scheduled when no one is available to empty the cooldown chamber.

    // ── MaxConsecutiveBuilds parameters ──────────────────────

    /// <summary>
    /// Maximum number of builds allowed back-to-back on this machine before
    /// a forced break is required. Only used when RuleType == MaxConsecutiveBuilds.
    /// </summary>
    public int? MaxConsecutiveBuilds { get; set; }

    /// <summary>
    /// Minimum break duration (hours) required after MaxConsecutiveBuilds is reached.
    /// Gives time for maintenance, inspection, or operator rest.
    /// Only used when RuleType == MaxConsecutiveBuilds.
    /// </summary>
    public double? MinBreakHours { get; set; }

    // ── BlackoutPeriod parameters ────────────────────────────
    // Uses MachineBlackoutAssignment join table — no inline fields needed.
    // When a BlackoutPeriod rule is enabled, the scheduler checks all assigned
    // blackout periods and blocks builds that overlap them.

    // ── Audit ────────────────────────────────────────────────

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual Machine Machine { get; set; } = null!;
}
