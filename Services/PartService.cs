using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class PartService : IPartService
{
    private readonly TenantDbContext _db;
    private readonly IBuildTemplateService _buildTemplateService;

    public PartService(TenantDbContext db, IBuildTemplateService buildTemplateService)
    {
        _db = db;
        _buildTemplateService = buildTemplateService;
    }

    public async Task<List<Part>> GetAllPartsAsync(bool activeOnly = true)
    {
        var query = _db.Parts
            .Include(p => p.MaterialEntity)
            .Include(p => p.ManufacturingApproach)
            .Include(p => p.AdditiveBuildConfig)
            .Include(p => p.StageRequirements)
            .Include(p => p.Drawings)
            .Include(p => p.ManufacturingProcess!)
                .ThenInclude(mp => mp.Stages)
            .AsQueryable();
        if (activeOnly)
            query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.PartNumber).ToListAsync();
    }

    public async Task<Part?> GetPartByIdAsync(int id)
    {
        return await _db.Parts
            .Include(p => p.MaterialEntity)
            .Include(p => p.ManufacturingApproach)
            .Include(p => p.AdditiveBuildConfig)
            .Include(p => p.StageRequirements)
                .ThenInclude(sr => sr.ProductionStage)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Part?> GetPartByNumberAsync(string partNumber)
    {
        return await _db.Parts
            .Include(p => p.AdditiveBuildConfig)
            .Include(p => p.StageRequirements)
            .FirstOrDefaultAsync(p => p.PartNumber == partNumber);
    }

    public async Task<Part> CreatePartAsync(Part part)
    {
        part.CreatedDate = DateTime.UtcNow;
        part.LastModifiedDate = DateTime.UtcNow;

        _db.Parts.Add(part);
        await _db.SaveChangesAsync();
        return part;
    }

    public async Task<Part> UpdatePartAsync(Part part)
    {
        part.LastModifiedDate = DateTime.UtcNow;
        _db.Parts.Update(part);
        await _db.SaveChangesAsync();

        await _buildTemplateService.InvalidateTemplatesForPartAsync(part.Id);

        return part;
    }

    public async Task DeletePartAsync(int id)
    {
        var part = await _db.Parts.FindAsync(id);
        if (part == null) throw new InvalidOperationException("Part not found.");
        part.IsActive = false;
        part.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<List<PartStageRequirement>> GetStageRequirementsAsync(int partId)
    {
        return await _db.PartStageRequirements
            .Include(r => r.ProductionStage)
            .Where(r => r.PartId == partId && r.IsActive)
            .OrderBy(r => r.ExecutionOrder)
            .ToListAsync();
    }

    public async Task<PartStageRequirement> AddStageRequirementAsync(PartStageRequirement requirement)
    {
        requirement.CreatedDate = DateTime.UtcNow;
        requirement.LastModifiedDate = DateTime.UtcNow;

        _db.PartStageRequirements.Add(requirement);
        await _db.SaveChangesAsync();
        return requirement;
    }

    public async Task<PartStageRequirement> UpdateStageRequirementAsync(PartStageRequirement requirement)
    {
        requirement.LastModifiedDate = DateTime.UtcNow;
        _db.PartStageRequirements.Update(requirement);
        await _db.SaveChangesAsync();
        return requirement;
    }

    public async Task RemoveStageRequirementAsync(int requirementId)
    {
        var req = await _db.PartStageRequirements.FindAsync(requirementId);
        if (req == null) throw new InvalidOperationException("Stage requirement not found.");
        req.IsActive = false;
        req.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<List<string>> ValidatePartAsync(Part part)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(part.PartNumber))
            errors.Add("Part number is required.");
        if (string.IsNullOrWhiteSpace(part.Name))
            errors.Add("Part name is required.");
        if (string.IsNullOrWhiteSpace(part.Material))
            errors.Add("Material is required.");

        // Duplicate PartNumber check
        if (!string.IsNullOrWhiteSpace(part.PartNumber))
        {
            var exists = await _db.Parts
                .Where(p => p.PartNumber == part.PartNumber && p.Id != part.Id)
                .AnyAsync();
            if (exists)
                errors.Add($"Part number '{part.PartNumber}' already exists.");
        }

        if (part.AdditiveBuildConfig != null)
            errors.AddRange(part.AdditiveBuildConfig.ValidateStackingConfiguration());

        return errors;
    }

    // PDM Extensions

    public async Task<Part?> GetPartDetailAsync(int id)
    {
        return await _db.Parts
            .Include(p => p.MaterialEntity)
            .Include(p => p.ManufacturingApproach)
            .Include(p => p.AdditiveBuildConfig)
            .Include(p => p.StageRequirements)
                .ThenInclude(sr => sr.ProductionStage)
            .Include(p => p.Drawings)
            .Include(p => p.RevisionHistory.OrderByDescending(r => r.RevisionDate))
            .Include(p => p.Notes.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.CreatedDate))
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PartRevisionHistory> BumpRevisionAsync(int partId, string newRevision, string changeDescription, string createdBy)
    {
        var part = await _db.Parts
            .Include(p => p.StageRequirements)
            .FirstOrDefaultAsync(p => p.Id == partId)
            ?? throw new InvalidOperationException("Part not found.");

        var routingSnapshot = System.Text.Json.JsonSerializer.Serialize(
            part.StageRequirements.Select(sr => new { sr.ProductionStageId, sr.ExecutionOrder, sr.IsRequired }).ToList());

        var history = new PartRevisionHistory
        {
            PartId = partId,
            Revision = newRevision,
            PreviousRevision = part.Revision,
            ChangeDescription = changeDescription,
            RawMaterialSpec = part.RawMaterialSpec,
            DrawingNumber = part.DrawingNumber,
            RoutingSnapshot = routingSnapshot,
            RevisionDate = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        part.Revision = newRevision;
        part.RevisionDate = DateTime.UtcNow;
        part.LastModifiedDate = DateTime.UtcNow;
        part.LastModifiedBy = createdBy;

        _db.PartRevisionHistories.Add(history);
        await _db.SaveChangesAsync();
        return history;
    }

    public async Task<List<Part>> SearchPartsAsync(string searchTerm, bool activeOnly = true)
    {
        var query = _db.Parts
            .Include(p => p.MaterialEntity)
            .Include(p => p.ManufacturingApproach)
            .Include(p => p.AdditiveBuildConfig)
            .Include(p => p.StageRequirements)
            .Include(p => p.Drawings)
            .Include(p => p.ManufacturingProcess!)
                .ThenInclude(mp => mp.Stages)
            .AsQueryable();
        if (activeOnly)
            query = query.Where(p => p.IsActive);

        var term = searchTerm.ToLower();
        query = query.Where(p =>
            p.PartNumber.ToLower().Contains(term) ||
            p.Name.ToLower().Contains(term) ||
            (p.CustomerPartNumber != null && p.CustomerPartNumber.ToLower().Contains(term)) ||
            (p.DrawingNumber != null && p.DrawingNumber.ToLower().Contains(term)) ||
            (p.Description != null && p.Description.ToLower().Contains(term)));

        return await query.OrderBy(p => p.PartNumber).Take(50).ToListAsync();
    }

    public async Task<List<PartNote>> GetNotesAsync(int partId)
    {
        return await _db.PartNotes
            .Where(n => n.PartId == partId)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedDate)
            .ToListAsync();
    }

    public async Task<PartNote> AddNoteAsync(PartNote note)
    {
        note.CreatedDate = DateTime.UtcNow;
        _db.PartNotes.Add(note);
        await _db.SaveChangesAsync();
        return note;
    }

    public async Task<PartNote> UpdateNoteAsync(PartNote note)
    {
        note.LastModifiedDate = DateTime.UtcNow;
        _db.PartNotes.Update(note);
        await _db.SaveChangesAsync();
        return note;
    }

    public async Task DeleteNoteAsync(int noteId)
    {
        var note = await _db.PartNotes.FindAsync(noteId);
        if (note == null) throw new InvalidOperationException("Note not found.");
        _db.PartNotes.Remove(note);
        await _db.SaveChangesAsync();
    }

    // Usage & Cloning

    public async Task<PartUsageSummary> GetPartUsageSummaryAsync(int partId)
    {
        var summary = new PartUsageSummary();

        summary.ActiveWorkOrderLines = await _db.WorkOrderLines
            .Include(l => l.WorkOrder)
            .Where(l => l.PartId == partId && l.WorkOrder.Status != Models.Enums.WorkOrderStatus.Cancelled)
            .OrderByDescending(l => l.WorkOrder.CreatedDate)
            .Take(20)
            .ToListAsync();

        summary.ActiveJobs = await _db.Jobs
            .Include(j => j.Stages)
            .Where(j => j.PartId == partId && j.Status != Models.Enums.JobStatus.Cancelled)
            .OrderByDescending(j => j.CreatedDate)
            .Take(20)
            .ToListAsync();

        summary.RecentQuoteLines = await _db.QuoteLines
            .Include(l => l.Quote)
            .Where(l => l.PartId == partId)
            .OrderByDescending(l => l.Quote.CreatedDate)
            .Take(20)
            .ToListAsync();

        summary.NcrCount = await _db.NonConformanceReports.CountAsync(n => n.PartId == partId);
        summary.InspectionCount = await _db.QCInspections.CountAsync(i => i.PartId == partId);
        summary.SpcDataPointCount = await _db.SpcDataPoints.CountAsync(s => s.PartId == partId);

        return summary;
    }

    public async Task<Part> ClonePartAsync(int sourcePartId, string newPartNumber, string createdBy)
    {
        var source = await _db.Parts
            .Include(p => p.AdditiveBuildConfig)
            .Include(p => p.StageRequirements)
                .ThenInclude(sr => sr.ProductionStage)
            .FirstOrDefaultAsync(p => p.Id == sourcePartId)
            ?? throw new InvalidOperationException("Source part not found.");

        // Check uniqueness of new part number
        var exists = await _db.Parts.AnyAsync(p => p.PartNumber == newPartNumber);
        if (exists)
            throw new InvalidOperationException($"Part number '{newPartNumber}' already exists.");

        var clone = new Part
        {
            PartNumber = newPartNumber,
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            Material = source.Material,
            MaterialId = source.MaterialId,
            ManufacturingApproachId = source.ManufacturingApproachId,
            CustomerPartNumber = source.CustomerPartNumber,
            DrawingNumber = source.DrawingNumber,
            Revision = "A",
            EstimatedWeightKg = source.EstimatedWeightKg,
            RawMaterialSpec = source.RawMaterialSpec,
            CustomFieldValues = source.CustomFieldValues,
            ItarClassification = source.ItarClassification,
            IsDefensePart = source.IsDefensePart,
            IsActive = true,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        _db.Parts.Add(clone);
        await _db.SaveChangesAsync();

        // Deep-copy additive build config if present
        if (source.AdditiveBuildConfig != null)
        {
            var src = source.AdditiveBuildConfig;
            var clonedConfig = new PartAdditiveBuildConfig
            {
                PartId = clone.Id,
                AllowStacking = src.AllowStacking,
                MaxStackCount = src.MaxStackCount,
                SingleStackDurationHours = src.SingleStackDurationHours,
                DoubleStackDurationHours = src.DoubleStackDurationHours,
                TripleStackDurationHours = src.TripleStackDurationHours,
                PlannedPartsPerBuildSingle = src.PlannedPartsPerBuildSingle,
                PlannedPartsPerBuildDouble = src.PlannedPartsPerBuildDouble,
                PlannedPartsPerBuildTriple = src.PlannedPartsPerBuildTriple,
                EnableDoubleStack = src.EnableDoubleStack,
                EnableTripleStack = src.EnableTripleStack
            };
            _db.PartAdditiveBuildConfigs.Add(clonedConfig);
        }

        // Deep-copy stage requirements
        foreach (var sr in source.StageRequirements.Where(s => s.IsActive))
        {
            var clonedReq = new PartStageRequirement
            {
                PartId = clone.Id,
                ProductionStageId = sr.ProductionStageId,
                ExecutionOrder = sr.ExecutionOrder,
                IsRequired = sr.IsRequired,
                IsActive = true,
                AllowParallelExecution = sr.AllowParallelExecution,
                IsBlocking = sr.IsBlocking,
                EstimatedMinutes = sr.EstimatedMinutes,
                SetupTimeMinutes = sr.SetupTimeMinutes,
                HourlyRateOverride = sr.HourlyRateOverride,
                EstimatedCost = sr.EstimatedCost,
                MaterialCost = sr.MaterialCost,
                AssignedMachineId = sr.AssignedMachineId,
                RequiresSpecificMachine = sr.RequiresSpecificMachine,
                PreferredMachineIds = sr.PreferredMachineIds,
                StageParameters = sr.StageParameters,
                RequiredMaterials = sr.RequiredMaterials,
                RequiredTooling = sr.RequiredTooling,
                QualityRequirements = sr.QualityRequirements,
                SpecialInstructions = sr.SpecialInstructions,
                RequirementNotes = sr.RequirementNotes,
                EstimateSource = "Manual",
                CreatedBy = createdBy,
                LastModifiedBy = createdBy
            };
            _db.PartStageRequirements.Add(clonedReq);
        }

        await _db.SaveChangesAsync();
        return clone;
    }

    // BOM

    public async Task<List<PartBomItem>> GetBomItemsAsync(int partId)
    {
        return await _db.PartBomItems
            .Include(b => b.Material)
            .Include(b => b.InventoryItem)
            .Where(b => b.PartId == partId && b.IsActive)
            .OrderBy(b => b.SortOrder)
            .ToListAsync();
    }

    public async Task<PartBomItem> AddBomItemAsync(PartBomItem item)
    {
        item.CreatedDate = DateTime.UtcNow;
        item.LastModifiedDate = DateTime.UtcNow;
        _db.PartBomItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<PartBomItem> UpdateBomItemAsync(PartBomItem item)
    {
        item.LastModifiedDate = DateTime.UtcNow;
        _db.PartBomItems.Update(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task RemoveBomItemAsync(int itemId)
    {
        var item = await _db.PartBomItems.FindAsync(itemId);
        if (item == null) throw new InvalidOperationException("BOM item not found.");
        item.IsActive = false;
        item.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // --- BOM Tree, Costing & Where-Used ---

    public async Task<BomTreeNode> GetBomTreeAsync(int partId, int maxDepth = 10)
    {
        var part = await _db.Parts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == partId)
            ?? throw new InvalidOperationException($"Part {partId} not found.");

        var visited = new HashSet<int>();
        return await BuildBomNodeAsync(part.Id, part.PartNumber, part.Name, 1m, 0, maxDepth, visited);
    }

    private async Task<BomTreeNode> BuildBomNodeAsync(
        int partId, string partNumber, string partName,
        decimal quantityPer, int level, int maxDepth, HashSet<int> visited)
    {
        var node = new BomTreeNode
        {
            PartId = partId,
            PartNumber = partNumber,
            PartName = partName,
            QuantityPer = quantityPer,
            Level = level
        };

        if (level >= maxDepth || !visited.Add(partId))
            return node; // max depth or circular ref — stop expanding

        var bomItems = await _db.PartBomItems
            .Include(b => b.Material)
            .Include(b => b.InventoryItem)
            .Include(b => b.ChildPart)
            .Where(b => b.PartId == partId && b.IsActive)
            .OrderBy(b => b.SortOrder)
            .AsNoTracking()
            .ToListAsync();

        node.BomItems = bomItems;

        // Recursively expand sub-parts
        foreach (var item in bomItems.Where(b => b.ItemType == Models.Enums.BomItemType.SubPart && b.ChildPartId.HasValue))
        {
            var childPart = item.ChildPart;
            if (childPart == null) continue;

            var childNode = await BuildBomNodeAsync(
                childPart.Id, childPart.PartNumber, childPart.Name,
                item.QuantityRequired, level + 1, maxDepth, visited);

            node.Children.Add(childNode);
        }

        // Calculate total material cost for this node
        decimal directCost = 0m;
        foreach (var item in bomItems)
        {
            if (item.ItemType == Models.Enums.BomItemType.SubPart)
            {
                var childNode = node.Children.FirstOrDefault(c => c.PartId == item.ChildPartId);
                if (childNode != null)
                    directCost += item.GetExtendedCost(childNode.TotalMaterialCost);
            }
            else
            {
                directCost += item.GetExtendedCost();
            }
        }
        node.TotalMaterialCost = directCost;

        visited.Remove(partId); // allow same part in different branches
        return node;
    }

    public async Task<BomCostSummary> CalculateBomCostAsync(int partId)
    {
        var tree = await GetBomTreeAsync(partId);

        var rawMaterialCost = 0m;
        var inventoryItemCost = 0m;
        var subPartCost = 0m;
        var totalLines = 0;

        AccumulateCosts(tree, ref rawMaterialCost, ref inventoryItemCost, ref subPartCost, ref totalLines);

        return new BomCostSummary(
            partId,
            rawMaterialCost,
            inventoryItemCost,
            subPartCost,
            tree.TotalMaterialCost,
            totalLines,
            GetTreeDepth(tree));
    }

    private static void AccumulateCosts(BomTreeNode node,
        ref decimal rawMaterial, ref decimal inventory, ref decimal subPart, ref int lineCount)
    {
        foreach (var item in node.BomItems)
        {
            lineCount++;
            switch (item.ItemType)
            {
                case Models.Enums.BomItemType.RawMaterial:
                    rawMaterial += item.GetExtendedCost();
                    break;
                case Models.Enums.BomItemType.InventoryItem:
                    inventory += item.GetExtendedCost();
                    break;
                case Models.Enums.BomItemType.SubPart:
                    var childNode = node.Children.FirstOrDefault(c => c.PartId == item.ChildPartId);
                    if (childNode != null)
                        subPart += item.GetExtendedCost(childNode.TotalMaterialCost);
                    break;
            }
        }

        foreach (var child in node.Children)
            AccumulateCosts(child, ref rawMaterial, ref inventory, ref subPart, ref lineCount);
    }

    private static int GetTreeDepth(BomTreeNode node)
    {
        if (node.Children.Count == 0) return 0;
        return 1 + node.Children.Max(GetTreeDepth);
    }

    public async Task<List<WhereUsedEntry>> GetWhereUsedAsync(int partId)
    {
        return await _db.PartBomItems
            .Include(b => b.Part)
            .Where(b => b.ChildPartId == partId && b.IsActive && b.ItemType == Models.Enums.BomItemType.SubPart)
            .Select(b => new WhereUsedEntry(
                b.PartId,
                b.Part.PartNumber,
                b.Part.Name,
                b.QuantityRequired,
                b.ReferenceDesignator))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> WouldCreateCircularBomAsync(int parentPartId, int childPartId)
    {
        if (parentPartId == childPartId) return true;

        // Walk the child's BOM tree to see if parentPartId appears anywhere
        var visited = new HashSet<int> { parentPartId };
        return await HasAncestorAsync(childPartId, visited);
    }

    private async Task<bool> HasAncestorAsync(int partId, HashSet<int> ancestors)
    {
        if (ancestors.Contains(partId)) return true;

        var childPartIds = await _db.PartBomItems
            .Where(b => b.PartId == partId && b.IsActive
                && b.ItemType == Models.Enums.BomItemType.SubPart
                && b.ChildPartId.HasValue)
            .Select(b => b.ChildPartId!.Value)
            .ToListAsync();

        foreach (var childId in childPartIds)
        {
            if (await HasAncestorAsync(childId, ancestors))
                return true;
        }

        return false;
    }
}
