using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class MaterialPlanningService : IMaterialPlanningService
{
    private readonly TenantDbContext _db;

    public MaterialPlanningService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<MaterialRequirement>> GetRequirementsFromOpenJobsAsync()
    {
        var pendingRequests = await _db.MaterialRequests
            .Where(r => r.Status == MaterialRequestStatus.Pending || r.Status == MaterialRequestStatus.PartiallyFulfilled)
            .Include(r => r.InventoryItem)
            .Include(r => r.Job)
            .ToListAsync();

        var grouped = pendingRequests
            .GroupBy(r => r.InventoryItemId)
            .Select(g =>
            {
                var item = g.First().InventoryItem;
                var totalRequired = g.Sum(r => r.QuantityRequested - (r.QuantityIssued ?? 0));
                var earliestDue = g
                    .Where(r => r.Job != null)
                    .Select(r => r.Job.ScheduledEnd)
                    .OrderBy(d => d)
                    .FirstOrDefault();

                return new MaterialRequirement(
                    ItemId: item.Id,
                    ItemName: item.Name,
                    ItemNumber: item.ItemNumber,
                    RequiredQty: totalRequired,
                    AvailableQty: item.AvailableQty,
                    ShortfallQty: Math.Max(0, totalRequired - item.AvailableQty),
                    RequiredByDate: earliestDue == default ? null : earliestDue);
            })
            .OrderByDescending(r => r.ShortfallQty)
            .ToList();

        return grouped;
    }

    public async Task<List<ReorderSuggestion>> GetReorderSuggestionsAsync()
    {
        var lowStockItems = await _db.InventoryItems
            .Where(i => i.IsActive && i.ReorderPoint > 0 && i.CurrentStockQty <= i.ReorderPoint)
            .ToListAsync();

        return lowStockItems.Select(item => new ReorderSuggestion(
            ItemId: item.Id,
            ItemName: item.Name,
            ItemNumber: item.ItemNumber,
            CurrentQty: item.CurrentStockQty,
            ReorderPoint: item.ReorderPoint,
            SuggestedOrderQty: item.ReorderQuantity > 0 ? item.ReorderQuantity : item.ReorderPoint * 2 - item.CurrentStockQty,
            Reason: item.CurrentStockQty == 0 ? "Out of stock" : "Below reorder point"
        )).OrderBy(s => s.CurrentQty).ToList();
    }

    public async Task<MaterialAvailabilityReport> CheckJobMaterialAvailabilityAsync(int jobId)
    {
        var job = await _db.Jobs
            .Include(j => j.Part)
            .FirstOrDefaultAsync(j => j.Id == jobId);

        if (job == null)
            return new MaterialAvailabilityReport(jobId, "Unknown", new(), true);

        var requests = await _db.MaterialRequests
            .Where(r => r.JobId == jobId)
            .Include(r => r.InventoryItem)
            .ToListAsync();

        var lines = requests.Select(r => new MaterialAvailabilityLine(
            ItemId: r.InventoryItemId,
            ItemName: r.InventoryItem.Name,
            RequiredQty: r.QuantityRequested,
            AvailableQty: r.InventoryItem.AvailableQty,
            IsSufficient: r.InventoryItem.AvailableQty >= r.QuantityRequested
        )).ToList();

        return new MaterialAvailabilityReport(
            JobId: jobId,
            JobInfo: $"Job #{jobId} — {job.Part?.PartNumber ?? "N/A"}",
            Lines: lines,
            AllAvailable: lines.All(l => l.IsSufficient));
    }
}
