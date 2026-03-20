using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public interface IWorkOrderService
{
    Task<List<WorkOrder>> GetAllWorkOrdersAsync(WorkOrderStatus? statusFilter = null);

    /// <summary>
    /// Retrieves work orders matching any of the specified statuses in a single query.
    /// </summary>
    Task<List<WorkOrder>> GetWorkOrdersByStatusesAsync(params WorkOrderStatus[] statuses);

    Task<WorkOrder?> GetWorkOrderByIdAsync(int id);
    Task<WorkOrder?> GetWorkOrderDetailAsync(int id);
    Task<WorkOrder?> GetWorkOrderByNumberAsync(string orderNumber);
    Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder);
    Task<WorkOrder> UpdateWorkOrderAsync(WorkOrder workOrder);
    Task<WorkOrderLine> AddLineAsync(int workOrderId, int partId, int quantity, string? notes = null);
    Task RemoveLineAsync(int lineId);
    Task<WorkOrder> UpdateStatusAsync(int workOrderId, WorkOrderStatus newStatus, string updatedBy);
    Task UpdateFulfillmentAsync(int workOrderLineId, int producedDelta, int shippedDelta);
    Task<string> GenerateOrderNumberAsync();

    // Job generation from routing
    Task<Job> GenerateJobForLineAsync(int workOrderLineId, string createdBy);
    Task<Job?> GetJobDetailAsync(int jobId);

    // Comments
    Task<List<WorkOrderComment>> GetCommentsAsync(int workOrderId);
    Task<WorkOrderComment> AddCommentAsync(int workOrderId, string content, string authorName, int? authorUserId = null, int? parentCommentId = null);
    Task<WorkOrderComment> UpdateCommentAsync(int commentId, string newContent);
    Task DeleteCommentAsync(int commentId);
}
