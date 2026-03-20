using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

/// <summary>
/// Manages external vendor operations (coating, outsourced heat-treat, etc.)
/// with PO tracking, shipping, ATF notifications, and turnaround EMA.
/// </summary>
public interface IExternalOperationService
{
    Task<ExternalOperation> CreateAsync(ExternalOperation operation);
    Task<ExternalOperation> UpdateAsync(ExternalOperation operation);
    Task<ExternalOperation?> GetByStageExecutionAsync(int stageExecutionId);
    Task<List<ExternalOperation>> GetPendingShipmentsAsync();
    Task<List<ExternalOperation>> GetAwaitingReturnAsync();
    Task RecordShipmentAsync(int id, DateTime shipDate, string? trackingNumber, string? poNumber);
    Task RecordReceiptAsync(int id, DateTime receiveDate, int receivedQuantity, string? trackingNumber);
    Task NotifyAtfShipAsync(int id, DateTime notificationDate);
    Task NotifyAtfReceiveAsync(int id, DateTime notificationDate);
}
