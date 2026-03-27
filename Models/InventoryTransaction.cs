using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

public class InventoryTransaction
{
    public int Id { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;

    public TransactionType TransactionType { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityBefore { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityAfter { get; set; }

    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }

    public int? LotId { get; set; }
    public InventoryLot? Lot { get; set; }

    public int? JobId { get; set; }
    public int? PurchaseOrderLineId { get; set; }

    [Required, MaxLength(100)]
    public string PerformedByUserId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Reference { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime TransactedAt { get; set; } = DateTime.UtcNow;
}
