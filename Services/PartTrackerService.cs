using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class PartTrackerService : IPartTrackerService
{
    private readonly TenantDbContext _db;

    public PartTrackerService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<PartTrackerResult> TrackByWorkOrderAsync(string orderNumber)
    {
        var wo = await _db.WorkOrders
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
            .Include(w => w.Lines)
                .ThenInclude(l => l.PartInstances)
                    .ThenInclude(pi => pi.CurrentStage)
            .Include(w => w.Lines)
                .ThenInclude(l => l.PartInstances)
                    .ThenInclude(pi => pi.StageLogs)
                        .ThenInclude(sl => sl.ProductionStage)
            .FirstOrDefaultAsync(w => w.OrderNumber == orderNumber);

        if (wo == null)
            return new PartTrackerResult { SearchTerm = orderNumber, SearchType = "WorkOrder" };

        var result = new PartTrackerResult
        {
            SearchTerm = orderNumber,
            SearchType = "WorkOrder",
            Lines = wo.Lines.Select(l => BuildLineTrack(l, wo.OrderNumber)).ToList()
        };

        return result;
    }

    public async Task<PartTrackerResult> TrackByPartNumberAsync(string partNumber)
    {
        var lines = await _db.WorkOrderLines
            .Include(l => l.WorkOrder)
            .Include(l => l.Part)
            .Include(l => l.PartInstances)
                .ThenInclude(pi => pi.CurrentStage)
            .Include(l => l.PartInstances)
                .ThenInclude(pi => pi.StageLogs)
                    .ThenInclude(sl => sl.ProductionStage)
            .Where(l => l.Part.PartNumber == partNumber)
            .ToListAsync();

        return new PartTrackerResult
        {
            SearchTerm = partNumber,
            SearchType = "PartNumber",
            Lines = lines.Select(l => BuildLineTrack(l, l.WorkOrder.OrderNumber)).ToList()
        };
    }

    public async Task<PartInstanceTrack?> TrackBySerialNumberAsync(string serialNumber)
    {
        var instance = await _db.PartInstances
            .Include(p => p.Part)
            .Include(p => p.CurrentStage)
            .Include(p => p.StageLogs)
                .ThenInclude(sl => sl.ProductionStage)
            .FirstOrDefaultAsync(p => p.SerialNumber == serialNumber
                || p.TemporaryTrackingId == serialNumber);

        if (instance == null) return null;

        return new PartInstanceTrack
        {
            PartInstanceId = instance.Id,
            SerialNumber = instance.DisplayIdentifier,
            PartNumber = instance.Part.PartNumber,
            CurrentStageName = instance.CurrentStage?.Name ?? "Not assigned",
            Status = instance.Status.ToString(),
            History = instance.StageLogs
                .OrderBy(sl => sl.StartedAt)
                .Select(sl => new StageLogEntry
                {
                    StageName = sl.ProductionStage?.Name ?? "Unknown",
                    StartedAt = sl.StartedAt,
                    CompletedAt = sl.CompletedAt,
                    OperatorName = sl.OperatorName,
                    DurationHours = sl.CompletedAt.HasValue
                        ? (sl.CompletedAt.Value - sl.StartedAt).TotalHours
                        : null
                }).ToList()
        };
    }

    public async Task<List<StagePipelineItem>> GetStagePipelineAsync()
    {
        var stages = await _db.ProductionStages
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

        var executions = await _db.StageExecutions
            .GroupBy(e => new { e.ProductionStageId, e.Status })
            .Select(g => new { g.Key.ProductionStageId, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        return stages.Select(s => new StagePipelineItem
        {
            StageId = s.Id,
            StageName = s.Name,
            StageColor = s.StageColor,
            PartsInQueue = executions
                .Where(e => e.ProductionStageId == s.Id && e.Status == StageExecutionStatus.NotStarted)
                .Sum(e => e.Count),
            PartsInProgress = executions
                .Where(e => e.ProductionStageId == s.Id && e.Status == StageExecutionStatus.InProgress)
                .Sum(e => e.Count),
            PartsCompleted = executions
                .Where(e => e.ProductionStageId == s.Id && e.Status == StageExecutionStatus.Completed)
                .Sum(e => e.Count)
        }).ToList();
    }

    private static WorkOrderLineTrack BuildLineTrack(WorkOrderLine line, string orderNumber)
    {
        return new WorkOrderLineTrack
        {
            WorkOrderLineId = line.Id,
            OrderNumber = orderNumber,
            PartNumber = line.Part.PartNumber,
            PartName = line.Part.Name,
            QuantityOrdered = line.Quantity,
            QuantityProduced = line.ProducedQuantity,
            QuantityShipped = line.ShippedQuantity,
            SerializedParts = line.PartInstances.Select(pi => new PartInstanceTrack
            {
                PartInstanceId = pi.Id,
                SerialNumber = pi.DisplayIdentifier,
                PartNumber = line.Part.PartNumber,
                CurrentStageName = pi.CurrentStage?.Name ?? "Not assigned",
                Status = pi.Status.ToString(),
                History = pi.StageLogs
                    .OrderBy(sl => sl.StartedAt)
                    .Select(sl => new StageLogEntry
                    {
                        StageName = sl.ProductionStage?.Name ?? "Unknown",
                        StartedAt = sl.StartedAt,
                        CompletedAt = sl.CompletedAt,
                        OperatorName = sl.OperatorName,
                        DurationHours = sl.CompletedAt.HasValue
                            ? (sl.CompletedAt.Value - sl.StartedAt).TotalHours
                            : null
                    }).ToList()
            }).ToList()
        };
    }
}
