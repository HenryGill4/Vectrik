using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

public class MaterialRequest
{
    public int Id { get; set; }

    public int JobId { get; set; }
    public Job Job { get; set; } = null!;

    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityRequested { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? QuantityIssued { get; set; }

    public int? LotId { get; set; }
    public InventoryLot? IssuedFromLot { get; set; }

    public MaterialRequestStatus Status { get; set; } = MaterialRequestStatus.Pending;

    [Required, MaxLength(100)]
    public string RequestedByUserId { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FulfilledAt { get; set; }
}
