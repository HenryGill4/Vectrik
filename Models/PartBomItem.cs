using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Opcentrix_V3.Models;

public class PartBomItem
{
    public int Id { get; set; }

    [Required]
    public int PartId { get; set; }
    public virtual Part Part { get; set; } = null!;

    public int? InventoryItemId { get; set; }
    public virtual InventoryItem? InventoryItem { get; set; }

    public int? MaterialId { get; set; }
    public virtual Material? Material { get; set; }

    [Column(TypeName = "decimal(10,4)")]
    public decimal QuantityRequired { get; set; }

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "each";

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
}
