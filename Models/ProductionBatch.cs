using System.ComponentModel.DataAnnotations;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

/// <summary>
/// A physical batch (crate/tray) of parts moving through post-print manufacturing stages.
/// Tracks capacity, current stage, and machine assignment. Parts are assigned/removed via
/// immutable <see cref="BatchPartAssignment"/> records for ITAR traceability.
/// </summary>
public class ProductionBatch
{
    public int Id { get; set; }

    /// <summary>
    /// Unique, auto-generated batch number (e.g., "BATCH-0042-001").
    /// </summary>
    [Required, MaxLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    /// <summary>
    /// FK to the build package that originally produced the parts in this batch.
    /// Null for batches created outside a build workflow.
    /// </summary>
    public int? OriginBuildPackageId { get; set; }

    /// <summary>
    /// Human-readable label for the physical container (e.g., "Crate A", "Tray #3").
    /// </summary>
    [MaxLength(100)]
    public string? ContainerLabel { get; set; }

    [Range(1, 10000)]
    public int Capacity { get; set; }

    /// <summary>
    /// Denormalized part count for fast display. Updated on assign/remove.
    /// </summary>
    [Range(0, 10000)]
    public int CurrentPartCount { get; set; }

    public BatchStatus Status { get; set; } = BatchStatus.Open;

    /// <summary>
    /// FK to the ProcessStage this batch is currently at (or heading to).
    /// </summary>
    public int? CurrentProcessStageId { get; set; }

    /// <summary>
    /// FK to the machine currently assigned to process this batch.
    /// </summary>
    public int? AssignedMachineId { get; set; }

    /// <summary>
    /// FK to the active StageExecution for this batch, if any.
    /// </summary>
    public int? StageExecutionId { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LastModifiedBy { get; set; }

    // Navigation
    public virtual BuildPackage? OriginBuildPackage { get; set; }
    public virtual ProcessStage? CurrentProcessStage { get; set; }
    public virtual Machine? AssignedMachine { get; set; }
    public virtual StageExecution? StageExecution { get; set; }
    public virtual ICollection<BatchPartAssignment> PartAssignments { get; set; } = new List<BatchPartAssignment>();
}
