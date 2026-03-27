using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Vectrik.Models.Enums;

namespace Vectrik.Models;

public class PartBomItem
{
    public int Id { get; set; }

    [Required]
    public int PartId { get; set; }
    public virtual Part Part { get; set; } = null!;

    /// <summary>
    /// Discriminator: what type of item this BOM line references.
    /// </summary>
    public BomItemType ItemType { get; set; } = BomItemType.RawMaterial;

    // --- Raw Material reference ---
    public int? MaterialId { get; set; }
    public virtual Material? Material { get; set; }

    // --- Inventory Item reference ---
    public int? InventoryItemId { get; set; }
    public virtual InventoryItem? InventoryItem { get; set; }

    // --- Sub-Part (assembly) reference ---
    /// <summary>
    /// FK to Part.Id — when this BOM item is another manufactured part (assembly).
    /// </summary>
    public int? ChildPartId { get; set; }
    public virtual Part? ChildPart { get; set; }

    [Column(TypeName = "decimal(10,4)")]
    public decimal QuantityRequired { get; set; }

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "each";

    /// <summary>
    /// Override cost per unit. When null, cost is resolved from the linked
    /// Material (CostPerKg), InventoryItem (UnitCost), or ChildPart (recursive BOM cost).
    /// </summary>
    [Column(TypeName = "decimal(10,4)")]
    public decimal? UnitCost { get; set; }

    /// <summary>
    /// Scrap/waste allowance percentage (e.g. 5 = 5%).
    /// Extended cost = UnitCost × Qty × (1 + ScrapFactorPct/100).
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal ScrapFactorPct { get; set; }

    /// <summary>
    /// Optional reference designator for assemblies (e.g. "R1", "C3", "BRACKET-LH").
    /// </summary>
    [MaxLength(100)]
    public string? ReferenceDesignator { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    // --- Computed cost helpers ---

    /// <summary>
    /// Returns the cost per single unit of this BOM item.
    /// Priority: UnitCost override → Material.CostPerKg → InventoryItem.UnitCost → 0 (sub-parts resolved externally).
    /// </summary>
    public decimal GetResolvedUnitCost()
    {
        if (UnitCost.HasValue)
            return UnitCost.Value;

        return ItemType switch
        {
            BomItemType.RawMaterial => Material?.CostPerKg ?? 0m,
            BomItemType.InventoryItem => InventoryItem?.UnitCost ?? 0m,
            // Sub-part cost must be resolved by the service (recursive BOM tree walk)
            BomItemType.SubPart => 0m,
            _ => 0m
        };
    }

    /// <summary>
    /// Extended cost = resolved unit cost × quantity × (1 + scrap factor).
    /// For sub-parts, pass the resolved child cost as <paramref name="childPartCost"/>.
    /// </summary>
    public decimal GetExtendedCost(decimal? childPartCost = null)
    {
        var unitCost = childPartCost ?? GetResolvedUnitCost();
        var scrapMultiplier = 1m + (ScrapFactorPct / 100m);
        return unitCost * QuantityRequired * scrapMultiplier;
    }
}
