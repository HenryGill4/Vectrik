using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Models;

public class InventoryItem
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string ItemNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public InventoryItemType ItemType { get; set; }

    public int? MaterialId { get; set; }
    public Material? Material { get; set; }

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "each";

    [Column(TypeName = "decimal(18,4)")]
    public decimal CurrentStockQty { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReservedQty { get; set; }

    [NotMapped]
    public decimal AvailableQty => CurrentStockQty - ReservedQty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReorderPoint { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReorderQuantity { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? UnitCost { get; set; }

    public bool TrackLots { get; set; }
    public bool TrackSerials { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<InventoryLot> Lots { get; set; } = new List<InventoryLot>();
}
