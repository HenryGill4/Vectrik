using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class WorkOrderService : IWorkOrderService
{
    private readonly TenantDbContext _db;

    public WorkOrderService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<WorkOrder>> GetAllWorkOrdersAsync(WorkOrderStatus? statusFilter = null)
    {
        var query = _db.WorkOrders
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
            .AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(w => w.Status == statusFilter.Value);

        return await query.OrderByDescending(w => w.OrderDate).ToListAsync();
    }

    public async Task<WorkOrder?> GetWorkOrderByIdAsync(int id)
    {
        return await _db.WorkOrders
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Jobs)
            .Include(w => w.Lines)
                .ThenInclude(l => l.PartInstances)
            .Include(w => w.Quote)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<WorkOrder?> GetWorkOrderByNumberAsync(string orderNumber)
    {
        return await _db.WorkOrders
            .Include(w => w.Lines)
            .FirstOrDefaultAsync(w => w.OrderNumber == orderNumber);
    }

    public async Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder)
    {
        if (string.IsNullOrWhiteSpace(workOrder.OrderNumber))
            workOrder.OrderNumber = await GenerateOrderNumberAsync();

        workOrder.CreatedDate = DateTime.UtcNow;
        workOrder.LastModifiedDate = DateTime.UtcNow;

        _db.WorkOrders.Add(workOrder);
        await _db.SaveChangesAsync();
        return workOrder;
    }

    public async Task<WorkOrder> UpdateWorkOrderAsync(WorkOrder workOrder)
    {
        workOrder.LastModifiedDate = DateTime.UtcNow;
        _db.WorkOrders.Update(workOrder);
        await _db.SaveChangesAsync();
        return workOrder;
    }

    public async Task<WorkOrderLine> AddLineAsync(int workOrderId, int partId, int quantity, string? notes = null)
    {
        var line = new WorkOrderLine
        {
            WorkOrderId = workOrderId,
            PartId = partId,
            Quantity = quantity,
            Notes = notes,
            Status = WorkOrderStatus.Draft
        };

        _db.WorkOrderLines.Add(line);
        await _db.SaveChangesAsync();
        return line;
    }

    public async Task RemoveLineAsync(int lineId)
    {
        var line = await _db.WorkOrderLines.FindAsync(lineId);
        if (line == null) throw new InvalidOperationException("Work order line not found.");
        _db.WorkOrderLines.Remove(line);
        await _db.SaveChangesAsync();
    }

    public async Task<WorkOrder> UpdateStatusAsync(int workOrderId, WorkOrderStatus newStatus, string updatedBy)
    {
        var wo = await _db.WorkOrders
            .Include(w => w.Lines)
            .FirstOrDefaultAsync(w => w.Id == workOrderId);

        if (wo == null) throw new InvalidOperationException("Work order not found.");

        wo.Status = newStatus;
        wo.LastModifiedDate = DateTime.UtcNow;
        wo.LastModifiedBy = updatedBy;

        // When releasing, update all lines to Released
        if (newStatus == WorkOrderStatus.Released)
        {
            foreach (var line in wo.Lines.Where(l => l.Status == WorkOrderStatus.Draft))
                line.Status = WorkOrderStatus.Released;
        }

        await _db.SaveChangesAsync();
        return wo;
    }

    public async Task UpdateFulfillmentAsync(int workOrderLineId, int producedDelta, int shippedDelta)
    {
        var line = await _db.WorkOrderLines
            .Include(l => l.WorkOrder)
            .FirstOrDefaultAsync(l => l.Id == workOrderLineId);

        if (line == null) throw new InvalidOperationException("Work order line not found.");

        line.ProducedQuantity += producedDelta;
        line.ShippedQuantity += shippedDelta;

        // Auto-update line status
        if (line.ShippedQuantity >= line.Quantity)
            line.Status = WorkOrderStatus.Complete;
        else if (line.ProducedQuantity > 0)
            line.Status = WorkOrderStatus.InProgress;

        // Check if all lines complete → mark WO complete
        var allLines = await _db.WorkOrderLines
            .Where(l => l.WorkOrderId == line.WorkOrderId)
            .ToListAsync();

        if (allLines.All(l => l.Status == WorkOrderStatus.Complete))
        {
            line.WorkOrder.Status = WorkOrderStatus.Complete;
            line.WorkOrder.LastModifiedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<string> GenerateOrderNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"WO-{year}-";

        var lastOrder = await _db.WorkOrders
            .Where(w => w.OrderNumber.StartsWith(prefix))
            .OrderByDescending(w => w.OrderNumber)
            .FirstOrDefaultAsync();

        var nextNumber = 1;
        if (lastOrder != null)
        {
            var suffix = lastOrder.OrderNumber.Replace(prefix, "");
            if (int.TryParse(suffix, out var lastNum))
                nextNumber = lastNum + 1;
        }

        return $"{prefix}{nextNumber:D4}";
    }
}
