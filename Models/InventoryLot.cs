using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class InventoryLot
{
    public int Id { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;

    [Required, MaxLength(100)]
    public string LotNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedQty { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CurrentQty { get; set; }

    public int? StockLocationId { get; set; }
    public StockLocation? Location { get; set; }

    public int? PurchaseOrderLineId { get; set; }

    public DateTime ReceivedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public LotStatus Status { get; set; } = LotStatus.Available;

    [MaxLength(50)]
    public string? InspectionStatus { get; set; }
}
