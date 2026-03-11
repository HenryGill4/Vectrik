using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public interface IWorkOrderService
{
    Task<List<WorkOrder>> GetAllWorkOrdersAsync(WorkOrderStatus? statusFilter = null);
    Task<WorkOrder?> GetWorkOrderByIdAsync(int id);
    Task<WorkOrder?> GetWorkOrderByNumberAsync(string orderNumber);
    Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder);
    Task<WorkOrder> UpdateWorkOrderAsync(WorkOrder workOrder);
    Task<WorkOrderLine> AddLineAsync(int workOrderId, int partId, int quantity, string? notes = null);
    Task RemoveLineAsync(int lineId);
    Task<WorkOrder> UpdateStatusAsync(int workOrderId, WorkOrderStatus newStatus, string updatedBy);
    Task UpdateFulfillmentAsync(int workOrderLineId, int producedDelta, int shippedDelta);
    Task<string> GenerateOrderNumberAsync();
}
