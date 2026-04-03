using System.ComponentModel.DataAnnotations;

namespace Vectrik.Models;

/// <summary>
/// A company-wide blackout date range (holiday, planned downtime, shutdown).
/// Shared across the organization — individual machines opt in via MachineBlackoutAssignment.
/// When a machine has a BlackoutPeriod scheduling rule enabled and is assigned to a blackout,
/// the scheduler will not place builds that overlap the blackout window.
/// </summary>
public class BlackoutPeriod
{
    public int Id { get; set; }

    /// <summary>Human-readable name, e.g. "Easter Weekend 2026", "Annual Shutdown".</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Blackout start (inclusive). Builds overlapping this window are blocked.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Blackout end (inclusive). The full window [StartDate, EndDate] is blocked.</summary>
    public DateTime EndDate { get; set; }

    /// <summary>Optional reason for the blackout.</summary>
    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    /// If true, this blackout recurs annually on the same month/day range.
    /// The scheduler compares month+day regardless of year.
    /// </summary>
    public bool IsRecurringAnnually { get; set; }

    /// <summary>Soft toggle — disabled blackouts are ignored by the scheduler.</summary>
    public bool IsActive { get; set; } = true;

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    // Navigation
    public virtual ICollection<MachineBlackoutAssignment> MachineAssignments { get; set; } = new List<MachineBlackoutAssignment>();
}
