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

    public async Task<WorkOrder?> GetWorkOrderDetailAsync(int id)
    {
        return await _db.WorkOrders
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Jobs)
                    .ThenInclude(j => j.Stages)
                        .ThenInclude(s => s.ProductionStage)
            .Include(w => w.Lines)
                .ThenInclude(l => l.PartInstances)
            .Include(w => w.Comments.Where(c => c.ParentCommentId == null))
                .ThenInclude(c => c.Replies)
            .Include(w => w.Quote)
            .AsSplitQuery()
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

        // When releasing, update all lines to Released and auto-generate jobs
        if (newStatus == WorkOrderStatus.Released)
        {
            foreach (var line in wo.Lines.Where(l => l.Status == WorkOrderStatus.Draft))
            {
                line.Status = WorkOrderStatus.Released;
            }

            // Auto-generate jobs for lines that don't have them yet
            foreach (var line in wo.Lines)
            {
                var hasJobs = await _db.Jobs.AnyAsync(j => j.WorkOrderLineId == line.Id);
                if (!hasJobs)
                    await GenerateJobsForLineAsync(line.Id, updatedBy);
            }
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

    // --- Job Generation from Part Routing ---

    public async Task<List<Job>> GenerateJobsForLineAsync(int workOrderLineId, string createdBy)
    {
        var line = await _db.WorkOrderLines
            .Include(l => l.Part)
            .FirstOrDefaultAsync(l => l.Id == workOrderLineId)
            ?? throw new InvalidOperationException("Work order line not found.");

        var routing = await _db.PartStageRequirements
            .Include(r => r.ProductionStage)
            .Where(r => r.PartId == line.PartId && r.IsActive)
            .OrderBy(r => r.ExecutionOrder)
            .ToListAsync();

        if (routing.Count == 0)
            return new List<Job>();

        var jobs = new List<Job>();
        Job? previousJob = null;

        foreach (var stage in routing)
        {
            var estimatedHours = stage.EstimatedHours ?? stage.ProductionStage.DefaultDurationHours;
            var job = new Job
            {
                PartId = line.PartId,
                WorkOrderLineId = line.Id,
                PartNumber = line.Part.PartNumber,
                Quantity = line.Quantity,
                EstimatedHours = estimatedHours,
                MachineId = stage.AssignedMachineId,
                Status = JobStatus.Draft,
                Priority = JobPriority.Normal,
                PredecessorJobId = previousJob?.Id,
                ScheduledStart = DateTime.UtcNow,
                ScheduledEnd = DateTime.UtcNow.AddHours(estimatedHours),
                CreatedBy = createdBy,
                LastModifiedBy = createdBy,
                Notes = $"Auto-generated for {line.Part.PartNumber} — Stage: {stage.ProductionStage.Name}"
            };

            _db.Jobs.Add(job);
            await _db.SaveChangesAsync(); // Save to get Id for predecessor chain

            // Create stage execution for this job
            var execution = new StageExecution
            {
                JobId = job.Id,
                ProductionStageId = stage.ProductionStageId,
                EstimatedHours = estimatedHours,
                EstimatedCost = stage.EstimatedCost,
                MaterialCost = stage.MaterialCost,
                SetupHours = stage.SetupTimeMinutes.HasValue ? stage.SetupTimeMinutes.Value / 60.0 : null,
                QualityCheckRequired = stage.ProductionStage.RequiresQualityCheck
            };

            _db.StageExecutions.Add(execution);
            await _db.SaveChangesAsync();

            jobs.Add(job);
            previousJob = job;
        }

        return jobs;
    }

    public async Task<Job?> GetJobDetailAsync(int jobId)
    {
        return await _db.Jobs
            .Include(j => j.Part)
            .Include(j => j.WorkOrderLine)
                .ThenInclude(l => l!.WorkOrder)
            .Include(j => j.Stages)
                .ThenInclude(s => s.ProductionStage)
            .Include(j => j.Stages)
                .ThenInclude(s => s.Operator)
            .Include(j => j.OperatorUser)
            .Include(j => j.JobNotes)
            .FirstOrDefaultAsync(j => j.Id == jobId);
    }

    // --- Comments ---

    public async Task<List<WorkOrderComment>> GetCommentsAsync(int workOrderId)
    {
        return await _db.WorkOrderComments
            .Where(c => c.WorkOrderId == workOrderId && c.ParentCommentId == null)
            .Include(c => c.Replies)
            .Include(c => c.AuthorUser)
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();
    }

    public async Task<WorkOrderComment> AddCommentAsync(int workOrderId, string content, string authorName, int? authorUserId = null, int? parentCommentId = null)
    {
        var comment = new WorkOrderComment
        {
            WorkOrderId = workOrderId,
            Content = content,
            AuthorName = authorName,
            AuthorUserId = authorUserId,
            ParentCommentId = parentCommentId
        };

        _db.WorkOrderComments.Add(comment);
        await _db.SaveChangesAsync();
        return comment;
    }

    public async Task DeleteCommentAsync(int commentId)
    {
        var comment = await _db.WorkOrderComments.FindAsync(commentId);
        if (comment != null)
        {
            _db.WorkOrderComments.Remove(comment);
            await _db.SaveChangesAsync();
        }
    }
}
