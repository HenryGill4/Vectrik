using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services;

namespace Vectrik.Tests.Helpers;

/// <summary>
/// No-op stub for IWorkOrderService used by StageService tests.
/// The work order methods are not exercised by the operator workflow tests.
/// </summary>
internal sealed class StubWorkOrderService : IWorkOrderService
{
    public Task<List<WorkOrder>> GetAllWorkOrdersAsync(WorkOrderStatus? statusFilter = null) => Task.FromResult(new List<WorkOrder>());
    public Task<List<WorkOrder>> GetWorkOrdersByStatusesAsync(params WorkOrderStatus[] statuses) => Task.FromResult(new List<WorkOrder>());
    public Task<WorkOrder?> GetWorkOrderByIdAsync(int id) => Task.FromResult<WorkOrder?>(null);
    public Task<WorkOrder?> GetWorkOrderDetailAsync(int id) => Task.FromResult<WorkOrder?>(null);
    public Task<WorkOrder?> GetWorkOrderByNumberAsync(string orderNumber) => Task.FromResult<WorkOrder?>(null);
    public Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder) => Task.FromResult(workOrder);
    public Task<WorkOrder> UpdateWorkOrderAsync(WorkOrder workOrder) => Task.FromResult(workOrder);
    public Task<WorkOrderLine> AddLineAsync(int workOrderId, int partId, int quantity, string? notes = null) => Task.FromResult(new WorkOrderLine());
    public Task RemoveLineAsync(int lineId) => Task.CompletedTask;
    public Task<WorkOrder> UpdateStatusAsync(int workOrderId, WorkOrderStatus newStatus, string updatedBy) => Task.FromResult(new WorkOrder());
    public Task UpdateFulfillmentAsync(int workOrderLineId, int producedDelta, int shippedDelta) => Task.CompletedTask;
    public Task<string> GenerateOrderNumberAsync() => Task.FromResult("WO-TEST-001");
    public Task<Job> GenerateJobForLineAsync(int workOrderLineId, string createdBy) => Task.FromResult(new Job());
    public Task<Job?> GetJobDetailAsync(int jobId) => Task.FromResult<Job?>(null);
    public Task<List<WorkOrderComment>> GetCommentsAsync(int workOrderId) => Task.FromResult(new List<WorkOrderComment>());
    public Task<WorkOrderComment> AddCommentAsync(int workOrderId, string content, string authorName, int? authorUserId = null, int? parentCommentId = null) => Task.FromResult(new WorkOrderComment());
    public Task<WorkOrderComment> UpdateCommentAsync(int commentId, string newContent) => Task.FromResult(new WorkOrderComment());
    public Task DeleteCommentAsync(int commentId) => Task.CompletedTask;
}
