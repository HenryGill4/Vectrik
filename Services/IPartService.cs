using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IPartService
{
    Task<List<Part>> GetAllPartsAsync(bool activeOnly = true);
    Task<Part?> GetPartByIdAsync(int id);
    Task<Part?> GetPartByNumberAsync(string partNumber);
    Task<Part> CreatePartAsync(Part part);
    Task<Part> UpdatePartAsync(Part part);
    Task DeletePartAsync(int id);
    [Obsolete("Use IManufacturingProcessService.GetByPartIdAsync to load process stages.")]
    Task<List<PartStageRequirement>> GetStageRequirementsAsync(int partId);
    [Obsolete("Use IManufacturingProcessService.AddStageAsync to add stages to a manufacturing process.")]
    Task<PartStageRequirement> AddStageRequirementAsync(PartStageRequirement requirement);
    [Obsolete("Use IManufacturingProcessService.UpdateStageAsync to update process stages.")]
    Task<PartStageRequirement> UpdateStageRequirementAsync(PartStageRequirement requirement);
    [Obsolete("Use IManufacturingProcessService.RemoveStageAsync to remove stages from a manufacturing process.")]
    Task RemoveStageRequirementAsync(int requirementId);
    Task<List<string>> ValidatePartAsync(Part part);

    // PDM Extensions
    Task<Part?> GetPartDetailAsync(int id);
    Task<PartRevisionHistory> BumpRevisionAsync(int partId, string newRevision, string changeDescription, string createdBy);
    Task<List<Part>> SearchPartsAsync(string searchTerm, bool activeOnly = true);
    Task<List<PartNote>> GetNotesAsync(int partId);
    Task<PartNote> AddNoteAsync(PartNote note);
    Task<PartNote> UpdateNoteAsync(PartNote note);
    Task DeleteNoteAsync(int noteId);

    // Usage & Cloning
    Task<PartUsageSummary> GetPartUsageSummaryAsync(int partId);
    Task<Part> ClonePartAsync(int sourcePartId, string newPartNumber, string createdBy);

    // BOM
    Task<List<PartBomItem>> GetBomItemsAsync(int partId);
    Task<PartBomItem> AddBomItemAsync(PartBomItem item);
    Task<PartBomItem> UpdateBomItemAsync(PartBomItem item);
    Task RemoveBomItemAsync(int itemId);

    // BOM Costing & Assembly
    /// <summary>
    /// Builds the full BOM tree for a part, resolving sub-part references recursively.
    /// Detects and prevents circular references.
    /// </summary>
    Task<BomTreeNode> GetBomTreeAsync(int partId, int maxDepth = 10);

    /// <summary>
    /// Calculates the total BOM material cost for one unit of a part.
    /// Recursively rolls up sub-part costs from the BOM tree.
    /// </summary>
    Task<BomCostSummary> CalculateBomCostAsync(int partId);

    /// <summary>
    /// Reverse lookup: finds all parent assemblies that reference this part in their BOM.
    /// </summary>
    Task<List<WhereUsedEntry>> GetWhereUsedAsync(int partId);

    /// <summary>
    /// Validates that adding childPartId to parentPartId's BOM won't create a circular reference.
    /// </summary>
    Task<bool> WouldCreateCircularBomAsync(int parentPartId, int childPartId);
}

/// <summary>
/// Represents one node in a multi-level BOM tree.
/// </summary>
public class BomTreeNode
{
    public int PartId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public decimal QuantityPer { get; set; } = 1m;
    public int Level { get; set; }
    public bool IsLeaf => Children.Count == 0 && BomItems.All(b => b.ItemType != Opcentrix_V3.Models.Enums.BomItemType.SubPart);

    /// <summary>Direct BOM items for this part (materials, inventory items, and sub-part references).</summary>
    public List<PartBomItem> BomItems { get; set; } = [];

    /// <summary>Expanded sub-part children (recursive BOM tree nodes).</summary>
    public List<BomTreeNode> Children { get; set; } = [];

    /// <summary>Total material cost for one unit of this part (rolled up from children + direct items).</summary>
    public decimal TotalMaterialCost { get; set; }
}

/// <summary>
/// Summary of BOM material costs for a single part.
/// </summary>
public record BomCostSummary(
    int PartId,
    decimal RawMaterialCost,
    decimal InventoryItemCost,
    decimal SubPartCost,
    decimal TotalBomCost,
    int TotalLineItems,
    int TreeDepth);

/// <summary>
/// Reverse BOM lookup entry: shows which parent assembly uses a part.
/// </summary>
public record WhereUsedEntry(
    int ParentPartId,
    string ParentPartNumber,
    string ParentPartName,
    decimal QuantityPer,
    string? ReferenceDesignator);

public class PartUsageSummary
{
    public List<WorkOrderLine> ActiveWorkOrderLines { get; set; } = new();
    public List<Job> ActiveJobs { get; set; } = new();
    public List<QuoteLine> RecentQuoteLines { get; set; } = new();
    public int NcrCount { get; set; }
    public int InspectionCount { get; set; }
    public int SpcDataPointCount { get; set; }
}
