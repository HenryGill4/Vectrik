using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

public class Shipment
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string ShipmentNumber { get; set; } = string.Empty;

    public int WorkOrderId { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Preparing;

    [MaxLength(200)]
    public string? CarrierName { get; set; }

    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    public int PackageCount { get; set; } = 1;

    [MaxLength(2000)]
    public string? PackingListJson { get; set; }

    [MaxLength(1000)]
    public string? ShipperNotes { get; set; }

    [MaxLength(100)]
    public string ShippedBy { get; set; } = string.Empty;

    public DateTime? ShippedAt { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public ICollection<ShipmentLine> Lines { get; set; } = new List<ShipmentLine>();
}
