using System.ComponentModel.DataAnnotations;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class PartInstance
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string SerialNumber { get; set; } = string.Empty;

    public int WorkOrderLineId { get; set; }
    public int PartId { get; set; }
    public int? CurrentStageId { get; set; }
    public int? BuildPackageId { get; set; }

    public PartInstanceStatus Status { get; set; } = PartInstanceStatus.InProcess;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual WorkOrderLine WorkOrderLine { get; set; } = null!;
    public virtual Part Part { get; set; } = null!;
    public virtual ProductionStage? CurrentStage { get; set; }
    public virtual BuildPackage? BuildPackage { get; set; }
    public virtual ICollection<PartInstanceStageLog> StageLogs { get; set; } = new List<PartInstanceStageLog>();
    public virtual ICollection<QCInspection> Inspections { get; set; } = new List<QCInspection>();
}
