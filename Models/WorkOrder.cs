using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class WorkOrder
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CustomerPO { get; set; }

    [MaxLength(200)]
    public string? CustomerEmail { get; set; }

    [MaxLength(50)]
    public string? CustomerPhone { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }

    // Shipping & Promise Dates
    public DateTime? ShipByDate { get; set; }
    public DateTime? PromisedDate { get; set; }
    public DateTime? ActualShipDate { get; set; }

    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Draft;
    public JobPriority Priority { get; set; } = JobPriority.Normal;

    public int? QuoteId { get; set; }

    public string? Notes { get; set; }

    // Approval
    [MaxLength(100)]
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public int? WorkflowInstanceId { get; set; }

    // DLMS / Defense
    [MaxLength(100)]
    public string? ContractNumber { get; set; }

    [MaxLength(50)]
    public string? ContractLineItem { get; set; }

    public bool IsDefenseContract { get; set; }

    // Custom Fields
    public string CustomFieldValues { get; set; } = "{}";

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    [MaxLength(100)]
    public string LastModifiedBy { get; set; } = string.Empty;

    // Navigation
    public virtual ICollection<WorkOrderLine> Lines { get; set; } = new List<WorkOrderLine>();
    public virtual ICollection<WorkOrderComment> Comments { get; set; } = new List<WorkOrderComment>();
    public virtual Quote? Quote { get; set; }
    public virtual WorkflowInstance? WorkflowInstance { get; set; }
}

public class WorkOrderLine
{
    public int Id { get; set; }

    [Required]
    public int WorkOrderId { get; set; }

    [Required]
    public int PartId { get; set; }

    [Required, Range(1, 10000)]
    public int Quantity { get; set; }

    public int ProducedQuantity { get; set; }
    public int ShippedQuantity { get; set; }

    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Draft;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public virtual WorkOrder WorkOrder { get; set; } = null!;
    public virtual Part Part { get; set; } = null!;
    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
    public virtual ICollection<PartInstance> PartInstances { get; set; } = new List<PartInstance>();
    public virtual ICollection<BuildPackagePart> BuildPackageParts { get; set; } = new List<BuildPackagePart>();
    public virtual ICollection<ProgramPart> ProgramParts { get; set; } = new List<ProgramPart>();
}
