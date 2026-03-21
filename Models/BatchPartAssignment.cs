using System.ComponentModel.DataAnnotations;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

/// <summary>
/// Immutable history record tracking every assignment and removal of a part instance
/// to/from a production batch. Provides full traceability chain for ITAR/defense compliance.
/// </summary>
public class BatchPartAssignment
{
    public int Id { get; set; }

    [Required]
    public int ProductionBatchId { get; set; }

    [Required]
    public int PartInstanceId { get; set; }

    public BatchAssignmentAction Action { get; set; }

    /// <summary>
    /// Human-readable reason for this assignment/removal
    /// (e.g., "Initial batch from build #42", "Consolidated for CNC").
    /// </summary>
    [MaxLength(200)]
    public string? Reason { get; set; }

    /// <summary>
    /// Snapshot of the ProcessStage.Id at the time of this assignment action.
    /// </summary>
    public int? AtProcessStageId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string PerformedBy { get; set; } = string.Empty;

    // Navigation
    public virtual ProductionBatch ProductionBatch { get; set; } = null!;
    public virtual PartInstance PartInstance { get; set; } = null!;
    public virtual ProcessStage? AtProcessStage { get; set; }
}
