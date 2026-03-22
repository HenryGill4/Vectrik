using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

/// <summary>
/// Represents a machine program (G-code, EDM recipe, laser path, etc.) that defines
/// how a specific part is manufactured on a specific machine at a specific process stage.
/// Programs can be part-specific, machine-specific, or both. Supports versioning,
/// file attachments, and machine-type-specific parameters.
/// </summary>
public class MachineProgram
{
    public int Id { get; set; }

    /// <summary>
    /// FK to Part — null for multi-part fixture programs.
    /// </summary>
    public int? PartId { get; set; }

    /// <summary>
    /// FK to Machine — specific machine, or null for machine-type-level programs.
    /// </summary>
    public int? MachineId { get; set; }

    /// <summary>
    /// Machine type for when no specific machine is assigned (e.g., "CNC", "EDM", "Laser").
    /// </summary>
    [MaxLength(50)]
    public string? MachineType { get; set; }

    /// <summary>
    /// FK to ProcessStage — optional link to a specific stage in a manufacturing process.
    /// </summary>
    public int? ProcessStageId { get; set; }

    /// <summary>
    /// Short identifier (e.g., "CNC-001", "EDM-A42").
    /// </summary>
    [Required, MaxLength(50)]
    public string ProgramNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int Version { get; set; } = 1;

    public ProgramStatus Status { get; set; } = ProgramStatus.Draft;

    // ── Duration Estimates ────────────────────────────────────
    public double? SetupTimeMinutes { get; set; }

    public double? RunTimeMinutes { get; set; }

    /// <summary>
    /// Full cycle time for CNC programs (includes load/unload/cut).
    /// </summary>
    public double? CycleTimeMinutes { get; set; }

    // ── Tooling & Fixtures ───────────────────────────────────
    /// <summary>
    /// Required tooling (e.g., "T1: 6mm End Mill, T2: M6 Tap").
    /// </summary>
    [MaxLength(500)]
    public string? ToolingRequired { get; set; }

    /// <summary>
    /// Required fixture (e.g., "Fixture-A-07").
    /// </summary>
    [MaxLength(200)]
    public string? FixtureRequired { get; set; }

    // ── Parameters ───────────────────────────────────────────
    /// <summary>
    /// JSON bag for machine-type-specific parameters (feeds, speeds, wire type, etc.).
    /// </summary>
    public string? Parameters { get; set; }

    // ── Operator Notes ───────────────────────────────────────
    [MaxLength(2000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    // ── Audit ────────────────────────────────────────────────
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LastModifiedBy { get; set; }

    // ── Navigation ───────────────────────────────────────────
    public virtual Part? Part { get; set; }
    public virtual Machine? Machine { get; set; }
    public virtual ProcessStage? ProcessStage { get; set; }
    public virtual ICollection<MachineProgramFile> Files { get; set; } = new List<MachineProgramFile>();

    // ── Computed ─────────────────────────────────────────────
    [NotMapped]
    public bool HasFiles => Files?.Any() == true;

    [NotMapped]
    public string StatusDisplay => Status switch
    {
        ProgramStatus.Draft => "Draft",
        ProgramStatus.Active => "Active",
        ProgramStatus.Superseded => "Superseded",
        ProgramStatus.Archived => "Archived",
        _ => Status.ToString()
    };
}
