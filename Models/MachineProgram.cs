using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

/// <summary>
/// Represents a machine program (G-code, EDM recipe, laser path, SLS build plate layout, etc.)
/// that defines how a part (or plate of parts) is manufactured on a machine.
/// Programs can be part-specific, machine-specific, or both. Supports versioning,
/// file attachments, machine-type-specific parameters, and SLS build plate management.
/// </summary>
public class MachineProgram
{
    public int Id { get; set; }

    /// <summary>
    /// Standard = CNC/EDM/laser per-part programs; BuildPlate = SLS multi-part plate programs.
    /// </summary>
    public ProgramType ProgramType { get; set; } = ProgramType.Standard;

    /// <summary>
    /// FK to Part — null for multi-part fixture or BuildPlate programs (use ProgramParts instead).
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
    /// FK to WorkInstruction — optional linked operator work instructions.
    /// </summary>
    public int? WorkInstructionId { get; set; }

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

    // ── Learning / EMA ───────────────────────────────────────
    /// <summary>
    /// Exponential Moving Average of actual run duration across all executions using this program.
    /// </summary>
    public double? ActualAverageDurationMinutes { get; set; }

    public int? ActualSampleCount { get; set; }

    /// <summary>
    /// "Manual" (user-entered times) or "Auto" (EMA-learned from actual runs).
    /// </summary>
    [MaxLength(50)]
    public string? EstimateSource { get; set; }

    /// <summary>
    /// Total number of completed runs that used this program version.
    /// </summary>
    public int TotalRunCount { get; set; }

    // ── Audit ────────────────────────────────────────────────
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LastModifiedBy { get; set; }

    // ── SLS Build Plate Fields (nullable — only used for BuildPlate programs) ──

    /// <summary>Slicer output: total layer count for the build.</summary>
    public int? LayerCount { get; set; }

    /// <summary>Slicer output: total build height in mm.</summary>
    public double? BuildHeightMm { get; set; }

    /// <summary>Slicer output: estimated print duration in hours.</summary>
    public double? EstimatedPrintHours { get; set; }

    /// <summary>Slicer output: estimated powder consumption in kg.</summary>
    public double? EstimatedPowderKg { get; set; }

    /// <summary>Slicer output file name (e.g. "MyBuild_v3.sli").</summary>
    [MaxLength(200)]
    public string? SlicerFileName { get; set; }

    /// <summary>Slicer software used (e.g. "Magics", "Materialise Build Processor").</summary>
    [MaxLength(100)]
    public string? SlicerSoftware { get; set; }

    /// <summary>Version of the slicer software.</summary>
    [MaxLength(50)]
    public string? SlicerVersion { get; set; }

    /// <summary>JSON: per-part positions on the build plate from the slicer.</summary>
    public string? PartPositionsJson { get; set; }

    /// <summary>FK to Material — plate-level material constraint (all parts share the same material).</summary>
    public int? MaterialId { get; set; }

    // ── Post-Processing Program Links (BuildPlate only) ──────

    /// <summary>FK to the depowdering program that follows this SLS build.</summary>
    public int? DepowderProgramId { get; set; }

    /// <summary>FK to the EDM/wire-cut program for support removal after this SLS build.</summary>
    public int? EdmProgramId { get; set; }

    // ── Scheduling Lifecycle (BuildPlate programs — replaces BuildPackage) ──────

    /// <summary>
    /// Schedule status for BuildPlate programs. Standard programs use ProgramStatus.
    /// </summary>
    public ProgramScheduleStatus ScheduleStatus { get; set; } = ProgramScheduleStatus.None;

    /// <summary>
    /// Scheduled start date/time for this program run on the assigned machine.
    /// </summary>
    public DateTime? ScheduledDate { get; set; }

    /// <summary>
    /// When the print actually started on the machine.
    /// </summary>
    public DateTime? PrintStartedAt { get; set; }

    /// <summary>
    /// When the print completed (build finished, ready for depowdering).
    /// </summary>
    public DateTime? PrintCompletedAt { get; set; }

    /// <summary>
    /// When all parts were released from the plate (post-processing complete).
    /// </summary>
    public DateTime? PlateReleasedAt { get; set; }

    /// <summary>
    /// Locks the program for scheduling — prevents part changes once scheduled.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// FK to the preceding program in the machine schedule chain.
    /// Used for changeover tracking.
    /// </summary>
    public int? PredecessorProgramId { get; set; }

    /// <summary>
    /// FK to the source program this run was copied from.
    /// Null for original/source programs; set for scheduled copies created via CreateScheduledCopyAsync.
    /// </summary>
    public int? SourceProgramId { get; set; }

    /// <summary>
    /// Optional FK to a Job when this program is scheduled through the job system.
    /// </summary>
    public int? ScheduledJobId { get; set; }

    // ── Navigation ───────────────────────────────────────────
    public virtual Part? Part { get; set; }
    public virtual Machine? Machine { get; set; }
    public virtual ProcessStage? ProcessStage { get; set; }
    public virtual Material? Material { get; set; }
    public virtual WorkInstruction? WorkInstruction { get; set; }
    public virtual MachineProgram? DepowderProgram { get; set; }
    public virtual MachineProgram? EdmProgram { get; set; }
    public virtual MachineProgram? PredecessorProgram { get; set; }
    public virtual MachineProgram? SourceProgram { get; set; }
    public virtual Job? ScheduledJob { get; set; }
    public virtual ICollection<MachineProgramFile> Files { get; set; } = new List<MachineProgramFile>();
    public virtual ICollection<ProgramToolingItem> ToolingItems { get; set; } = new List<ProgramToolingItem>();
    public virtual ICollection<ProgramFeedback> Feedbacks { get; set; } = new List<ProgramFeedback>();
    public virtual ICollection<MachineProgramAssignment> MachineAssignments { get; set; } = new List<MachineProgramAssignment>();
    public virtual ICollection<ProgramPart> ProgramParts { get; set; } = new List<ProgramPart>();

    // ── Computed ─────────────────────────────────────────────
    [NotMapped]
    public bool HasFiles => Files?.Any() == true;

    [NotMapped]
    public bool IsBuildPlate => ProgramType == ProgramType.BuildPlate;

    [NotMapped]
    public bool HasSlicerData => LayerCount.HasValue && EstimatedPrintHours.HasValue;

    /// <summary>True when both depowder and EDM post-processing programs are linked.</summary>
    [NotMapped]
    public bool HasPostProcessingPrograms => DepowderProgramId.HasValue && EdmProgramId.HasValue;

    [NotMapped]
    public int TotalPartCount => ProgramParts?.Sum(p => p.Quantity) ?? 0;

    [NotMapped]
    public int UniquePartCount => ProgramParts?.Select(p => p.PartId).Distinct().Count() ?? 0;

    /// <summary>
    /// True if this program is a scheduled copy/run created from a source program file.
    /// </summary>
    [NotMapped]
    public bool IsScheduledCopy => SourceProgramId.HasValue;

    /// <summary>
    /// True when the BuildPlate program is ready to be scheduled (has parts and slicer data).
    /// </summary>
    [NotMapped]
    public bool IsReadyToSchedule => IsBuildPlate && HasSlicerData && ProgramParts?.Any() == true;

    [NotMapped]
    public string StatusDisplay => Status switch
    {
        ProgramStatus.Draft => "Draft",
        ProgramStatus.Active => "Active",
        ProgramStatus.Superseded => "Superseded",
        ProgramStatus.Archived => "Archived",
        _ => Status.ToString()
    };

    [NotMapped]
    public string ScheduleStatusDisplay => ScheduleStatus switch
    {
        ProgramScheduleStatus.None => "Not Scheduled",
        ProgramScheduleStatus.Ready => "Ready",
        ProgramScheduleStatus.Scheduled => "Scheduled",
        ProgramScheduleStatus.Printing => "Printing",
        ProgramScheduleStatus.PostPrint => "Post-Print",
        ProgramScheduleStatus.Completed => "Completed",
        ProgramScheduleStatus.Cancelled => "Cancelled",
        _ => ScheduleStatus.ToString()
    };
}
