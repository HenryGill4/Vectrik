using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class ExternalOperationService : IExternalOperationService
{
    private readonly TenantDbContext _db;

    public ExternalOperationService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<ExternalOperation> CreateAsync(ExternalOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (string.IsNullOrWhiteSpace(operation.VendorName))
            throw new ArgumentException("Vendor name is required.");

        var existing = await _db.ExternalOperations
            .AnyAsync(e => e.StageExecutionId == operation.StageExecutionId);
        if (existing)
            throw new InvalidOperationException($"An external operation already exists for stage execution {operation.StageExecutionId}.");

        operation.CreatedDate = DateTime.UtcNow;
        operation.LastModifiedDate = DateTime.UtcNow;

        _db.ExternalOperations.Add(operation);
        await _db.SaveChangesAsync();
        return operation;
    }

    public async Task<ExternalOperation> UpdateAsync(ExternalOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var existing = await _db.ExternalOperations.FindAsync(operation.Id)
            ?? throw new InvalidOperationException($"ExternalOperation {operation.Id} not found.");

        existing.VendorName = operation.VendorName;
        existing.VendorContact = operation.VendorContact;
        existing.PurchaseOrderNumber = operation.PurchaseOrderNumber;
        existing.EstimatedTurnaroundDays = operation.EstimatedTurnaroundDays;
        existing.RequiresAtfNotification = operation.RequiresAtfNotification;
        existing.Quantity = operation.Quantity;
        existing.Notes = operation.Notes;
        existing.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<ExternalOperation?> GetByStageExecutionAsync(int stageExecutionId)
    {
        return await _db.ExternalOperations
            .Include(e => e.StageExecution)
                .ThenInclude(se => se.ProductionStage)
            .FirstOrDefaultAsync(e => e.StageExecutionId == stageExecutionId);
    }

    public async Task<List<ExternalOperation>> GetPendingShipmentsAsync()
    {
        return await _db.ExternalOperations
            .Include(e => e.StageExecution)
                .ThenInclude(se => se.ProductionStage)
            .Where(e => e.ShipDate == null)
            .OrderBy(e => e.CreatedDate)
            .ToListAsync();
    }

    public async Task<List<ExternalOperation>> GetAwaitingReturnAsync()
    {
        return await _db.ExternalOperations
            .Include(e => e.StageExecution)
                .ThenInclude(se => se.ProductionStage)
            .Where(e => e.ShipDate != null && e.ActualReturnDate == null)
            .OrderBy(e => e.ExpectedReturnDate)
            .ToListAsync();
    }

    public async Task RecordShipmentAsync(int id, DateTime shipDate, string? trackingNumber, string? poNumber)
    {
        var op = await _db.ExternalOperations.FindAsync(id)
            ?? throw new InvalidOperationException($"ExternalOperation {id} not found.");

        op.ShipDate = shipDate;
        op.OutboundTrackingNumber = trackingNumber;
        if (!string.IsNullOrWhiteSpace(poNumber))
            op.PurchaseOrderNumber = poNumber;

        if (op.EstimatedTurnaroundDays.HasValue)
            op.ExpectedReturnDate = shipDate.AddDays(op.EstimatedTurnaroundDays.Value);

        op.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task RecordReceiptAsync(int id, DateTime receiveDate, int receivedQuantity, string? trackingNumber)
    {
        var op = await _db.ExternalOperations.FindAsync(id)
            ?? throw new InvalidOperationException($"ExternalOperation {id} not found.");

        op.ActualReturnDate = receiveDate;
        op.ReceivedQuantity = receivedQuantity;
        op.ReturnTrackingNumber = trackingNumber;

        // Calculate actual turnaround and update EMA
        if (op.ShipDate.HasValue)
        {
            op.ActualTurnaroundDays = (receiveDate - op.ShipDate.Value).TotalDays;
            UpdateTurnaroundEma(op);
        }

        op.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task NotifyAtfShipAsync(int id, DateTime notificationDate)
    {
        var op = await _db.ExternalOperations.FindAsync(id)
            ?? throw new InvalidOperationException($"ExternalOperation {id} not found.");

        op.AtfShipNotificationDate = notificationDate;
        op.AtfShipNotified = true;
        op.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task NotifyAtfReceiveAsync(int id, DateTime notificationDate)
    {
        var op = await _db.ExternalOperations.FindAsync(id)
            ?? throw new InvalidOperationException($"ExternalOperation {id} not found.");

        op.AtfReceiveNotificationDate = notificationDate;
        op.AtfReceiveNotified = true;
        op.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Updates the exponential moving average for vendor turnaround time.
    /// Uses the same EMA pattern as PartStageRequirement.
    /// </summary>
    private static void UpdateTurnaroundEma(ExternalOperation op)
    {
        if (!op.ActualTurnaroundDays.HasValue)
            return;

        op.TurnaroundSampleCount++;
        const double alpha = 0.3;

        if (op.AverageTurnaroundDays.HasValue)
        {
            op.AverageTurnaroundDays = alpha * op.ActualTurnaroundDays.Value
                + (1 - alpha) * op.AverageTurnaroundDays.Value;
        }
        else
        {
            op.AverageTurnaroundDays = op.ActualTurnaroundDays.Value;
        }
    }
}
