using System.ComponentModel.DataAnnotations;

namespace Opcentrix_V3.Models;

public class ExternalOperation
{
    public int Id { get; set; }

    [Required]
    public int StageExecutionId { get; set; }
    public virtual StageExecution StageExecution { get; set; } = null!;

    // Vendor
    [Required, MaxLength(200)]
    public string VendorName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? VendorContact { get; set; }

    // Purchase Order
    [MaxLength(100)]
    public string? PurchaseOrderNumber { get; set; }

    // Shipping
    public DateTime? ShipDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }

    [MaxLength(100)]
    public string? OutboundTrackingNumber { get; set; }

    [MaxLength(100)]
    public string? ReturnTrackingNumber { get; set; }

    // Turnaround tracking (auto-adjusting EMA)
    public double? EstimatedTurnaroundDays { get; set; }
    public double? ActualTurnaroundDays { get; set; }
    public double? AverageTurnaroundDays { get; set; }
    public int TurnaroundSampleCount { get; set; }

    // ATF Compliance (ITAR/defense parts)
    public bool RequiresAtfNotification { get; set; }
    public DateTime? AtfShipNotificationDate { get; set; }
    public DateTime? AtfReceiveNotificationDate { get; set; }
    public bool AtfShipNotified { get; set; }
    public bool AtfReceiveNotified { get; set; }

    // Status
    public int Quantity { get; set; }
    public int? ReceivedQuantity { get; set; }

    public string? Notes { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
}
