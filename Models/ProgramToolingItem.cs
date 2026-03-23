using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Maintenance;

namespace Opcentrix_V3.Models;

/// <summary>
/// A structured tooling or fixture entry for a MachineProgram.
/// Links to a MachineComponent for wear-life tracking and maintenance blocking.
/// When the linked component has critical/overdue maintenance alerts, the scheduling
/// system prevents operators from starting jobs that require this program.
/// </summary>
public class ProgramToolingItem
{
    public int Id { get; set; }

    [Required]
    public int MachineProgramId { get; set; }

    /// <summary>
    /// Tool position in the magazine (e.g., "T1", "T2") or fixture slot name.
    /// </summary>
    [Required, MaxLength(20)]
    public string ToolPosition { get; set; } = string.Empty;

    /// <summary>
    /// Descriptive name (e.g., "6mm End Mill", "M6 Tap", "Fixture-A-07").
    /// </summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional link to a tracked MachineComponent for wear life monitoring.
    /// When set, component maintenance alerts will block job starts.
    /// </summary>
    public int? MachineComponentId { get; set; }

    /// <summary>
    /// Whether this entry is a fixture rather than a cutting tool.
    /// </summary>
    public bool IsFixture { get; set; }

    // ── Wear Life Configuration ──────────────────────────────

    /// <summary>
    /// Expected tool life in operating hours before replacement is needed.
    /// </summary>
    public double? WearLifeHours { get; set; }

    /// <summary>
    /// Expected tool life in completed builds/cycles before replacement.
    /// </summary>
    public int? WearLifeBuilds { get; set; }

    /// <summary>
    /// Percentage of wear life at which to trigger an early warning (0–100).
    /// </summary>
    [Range(0, 100)]
    public int WarningThresholdPercent { get; set; } = 80;

    /// <summary>
    /// Part number for reordering this tool/fixture.
    /// </summary>
    [MaxLength(100)]
    public string? SparePartNumber { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Display order within the program's tooling list.
    /// </summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [MaxLength(100)]
    public string? LastModifiedBy { get; set; }

    // ── Navigation ───────────────────────────────────────────
    public virtual MachineProgram MachineProgram { get; set; } = null!;
    public virtual MachineComponent? MachineComponent { get; set; }

    // ── Computed ─────────────────────────────────────────────

    /// <summary>
    /// Returns the wear percentage based on the linked component's current hours or builds
    /// against this item's configured wear life. Returns null when no component is linked
    /// or no wear life is configured.
    /// </summary>
    [NotMapped]
    public double? WearPercent
    {
        get
        {
            if (MachineComponent is null) return null;

            if (WearLifeHours.HasValue && WearLifeHours.Value > 0 && MachineComponent.CurrentHours.HasValue)
                return (MachineComponent.CurrentHours.Value / WearLifeHours.Value) * 100;

            if (WearLifeBuilds.HasValue && WearLifeBuilds.Value > 0 && MachineComponent.CurrentBuilds.HasValue)
                return ((double)MachineComponent.CurrentBuilds.Value / WearLifeBuilds.Value) * 100;

            return null;
        }
    }
}
