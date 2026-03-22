using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

/// <summary>
/// A specific stage within a ManufacturingProcess. References a global ProductionStage
/// for catalog metadata (name, icon, color) but adds per-process configuration for
/// processing level, compound duration, batch settings, and machine preferences.
/// </summary>
public class ProcessStage
{
    public int Id { get; set; }

    [Required]
    public int ManufacturingProcessId { get; set; }

    /// <summary>
    /// FK to the global ProductionStage catalog for name, icon, color, department.
    /// </summary>
    [Required]
    public int ProductionStageId { get; set; }

    /// <summary>
    /// Execution order within the process (1–100).
    /// </summary>
    [Range(1, 100)]
    public int ExecutionOrder { get; set; }

    // ── Processing Level ─────────────────────────────────────
    /// <summary>
    /// Whether this stage operates at Build, Batch, or Part level.
    /// </summary>
    public ProcessingLevel ProcessingLevel { get; set; } = ProcessingLevel.Part;

    // ── Compound Duration ────────────────────────────────────
    /// <summary>
    /// Setup duration mode (None, PerBuild, PerBatch, PerPart).
    /// </summary>
    public DurationMode SetupDurationMode { get; set; } = DurationMode.None;

    public double? SetupTimeMinutes { get; set; }

    /// <summary>
    /// Run duration mode (None, PerBuild, PerBatch, PerPart).
    /// </summary>
    public DurationMode RunDurationMode { get; set; } = DurationMode.PerPart;

    public double? RunTimeMinutes { get; set; }

    /// <summary>
    /// For printing stages: pull duration from slicer/build config data instead of fixed minutes.
    /// </summary>
    public bool DurationFromBuildConfig { get; set; }

    // ── Batch Settings ───────────────────────────────────────
    /// <summary>
    /// Override the ManufacturingProcess.DefaultBatchCapacity for this stage.
    /// </summary>
    [Range(1, 10000)]
    public int? BatchCapacityOverride { get; set; }

    public bool AllowRebatching { get; set; }

    /// <summary>
    /// Attempt machine-driven batch consolidation at this stage.
    /// </summary>
    public bool ConsolidateBatchesAtStage { get; set; }

    // ── Machine ──────────────────────────────────────────────
    public int? AssignedMachineId { get; set; }
    public bool RequiresSpecificMachine { get; set; }

    /// <summary>
    /// Comma-separated Machine.Id values for preferred machines at this stage.
    /// </summary>
    [MaxLength(500)]
    public string? PreferredMachineIds { get; set; }

    /// <summary>
    /// FK to MachineProgram — optional link to the program to use at this stage.
    /// </summary>
    public int? MachineProgramId { get; set; }

    // ── Cost ─────────────────────────────────────────────────
    [Column(TypeName = "decimal(8,2)")]
    public decimal? HourlyRateOverride { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? MaterialCost { get; set; }

    // ── Workflow Flags ───────────────────────────────────────
    public bool IsRequired { get; set; } = true;
    public bool IsBlocking { get; set; } = true;
    public bool AllowParallelExecution { get; set; }
    public bool AllowSkip { get; set; }
    public bool RequiresQualityCheck { get; set; }
    public bool RequiresSerialNumber { get; set; }

    // ── External Operations ──────────────────────────────────
    public bool IsExternalOperation { get; set; }
    public double? ExternalTurnaroundDays { get; set; }

    // ── Custom / JSON Config ─────────────────────────────────
    /// <summary>JSON bag for stage-specific parameters (e.g., temperature, gas flow).</summary>
    public string? StageParameters { get; set; }

    /// <summary>JSON array of required material specs for this stage.</summary>
    public string? RequiredMaterials { get; set; }

    [MaxLength(500)]
    public string? RequiredTooling { get; set; }

    /// <summary>JSON array of quality requirements (dimensions, tolerances).</summary>
    public string? QualityRequirements { get; set; }

    [MaxLength(1000)]
    public string? SpecialInstructions { get; set; }

    // ── Learning / EMA ───────────────────────────────────────
    public double? ActualAverageDurationMinutes { get; set; }
    public int? ActualSampleCount { get; set; }

    [MaxLength(50)]
    public string? EstimateSource { get; set; }

    // ── Audit ────────────────────────────────────────────────
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [MaxLength(100)]
    public string? LastModifiedBy { get; set; }

    // Navigation
    public virtual ManufacturingProcess ManufacturingProcess { get; set; } = null!;
    public virtual ProductionStage ProductionStage { get; set; } = null!;
    public virtual Machine? AssignedMachine { get; set; }
    public virtual MachineProgram? MachineProgram { get; set; }
}
