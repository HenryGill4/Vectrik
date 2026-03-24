using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class PartInstance
{
    public int Id { get; set; }

    /// <summary>
    /// Official serial number, assigned at laser engraving stage.
    /// Null until <see cref="IsSerialAssigned"/> is true.
    /// </summary>
    [MaxLength(50)]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Auto-generated tracking ID assigned at plate release (before official serial).
    /// Format: "TMP-{buildPackageId}-{index:D4}".
    /// </summary>
    [Required, MaxLength(50)]
    public string TemporaryTrackingId { get; set; } = string.Empty;

    /// <summary>
    /// True after the official serial number is assigned at laser engraving.
    /// </summary>
    public bool IsSerialAssigned { get; set; }

    /// <summary>
    /// Returns the official serial if assigned, otherwise the temporary tracking ID.
    /// </summary>
    [NotMapped]
    public string DisplayIdentifier => SerialNumber ?? TemporaryTrackingId;

    public int WorkOrderLineId { get; set; }
    public int PartId { get; set; }
    public int? CurrentStageId { get; set; }
    /// <summary>
    /// FK to the ProductionBatch this part instance is currently assigned to.
    /// </summary>
    public int? CurrentBatchId { get; set; }

    public PartInstanceStatus Status { get; set; } = PartInstanceStatus.InProcess;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual WorkOrderLine WorkOrderLine { get; set; } = null!;
    public virtual Part Part { get; set; } = null!;
    public virtual ProductionStage? CurrentStage { get; set; }
    public virtual ProductionBatch? CurrentBatch { get; set; }
    public virtual ICollection<PartInstanceStageLog> StageLogs { get; set; } = new List<PartInstanceStageLog>();
    public virtual ICollection<QCInspection> Inspections { get; set; } = new List<QCInspection>();
}
