using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class WorkOrderService : IWorkOrderService
{
    private readonly TenantDbContext _db;
    private readonly ISchedulingService _scheduler;
    private readonly INumberSequenceService _numberSeq;

    public WorkOrderService(TenantDbContext db, ISchedulingService scheduler, INumberSequenceService numberSeq)
    {
        _db = db;
        _scheduler = scheduler;
        _numberSeq = numberSeq;
    }

    public async Task<List<WorkOrder>> GetAllWorkOrdersAsync(WorkOrderStatus? statusFilter = null)
    {
        var query = _db.WorkOrders
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p.ManufacturingApproach)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p.AdditiveBuildConfig)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Jobs)
            .Include(w => w.Lines)
                .ThenInclude(l => l.ProgramParts)
                    .ThenInclude(pp => pp.MachineProgram)
            .AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(w => w.Status == statusFilter.Value);

        return await query.OrderByDescending(w => w.OrderDate).ToListAsync();
    }

    public async Task<List<WorkOrder>> GetWorkOrdersByStatusesAsync(params WorkOrderStatus[] statuses)
    {
        var query = _db.WorkOrders
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p.ManufacturingApproach)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p.AdditiveBuildConfig)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Jobs)
            .Include(w => w.Lines)
                .ThenInclude(l => l.ProgramParts)
                    .ThenInclude(pp => pp.MachineProgram)
            .AsQueryable();

        if (statuses.Length > 0)
            query = query.Where(w => statuses.Contains(w.Status));

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
                    .ThenInclude(p => p.ManufacturingApproach)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p.MaterialEntity)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p.ManufacturingProcess)
                        .ThenInclude(mp => mp!.Stages.OrderBy(s => s.ExecutionOrder))
                            .ThenInclude(ps => ps.ProductionStage)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p.ManufacturingProcess)
                        .ThenInclude(mp => mp!.Stages)
                            .ThenInclude(ps => ps.AssignedMachine)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Jobs)
                    .ThenInclude(j => j.Stages)
                        .ThenInclude(s => s.ProductionStage)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Jobs)
                    .ThenInclude(j => j.Stages)
                        .ThenInclude(s => s.ProcessStage)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Jobs)
                    .ThenInclude(j => j.Stages)
                        .ThenInclude(s => s.Machine)
            .Include(w => w.Lines)
                .ThenInclude(l => l.PartInstances)
            .Include(w => w.Lines)
                .ThenInclude(l => l.ProgramParts)
                    .ThenInclude(pp => pp.MachineProgram)
            .Include(w => w.Lines)
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p.AdditiveBuildConfig)
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
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

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
                .ThenInclude(l => l.Part)
                    .ThenInclude(p => p.ManufacturingApproach)
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

            // Auto-generate jobs only for non-build-plate lines (CNC path).
            // SLS/additive parts that require a build plate stay in Pending —
            // they get jobs when a MachineProgram is created and plate is released.
            foreach (var line in wo.Lines)
            {
                var requiresBuildPlate = line.Part?.ManufacturingApproach?.RequiresBuildPlate == true;
                if (requiresBuildPlate)
                    continue;

                var hasJobs = await _db.Jobs.AnyAsync(j => j.WorkOrderLineId == line.Id);
                if (!hasJobs)
                    await GenerateJobForLineAsync(line.Id, updatedBy);
            }
        }

        await _db.SaveChangesAsync();
        return wo;
    }

    public async Task UpdateFulfillmentAsync(int workOrderLineId, int producedDelta, int shippedDelta)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(producedDelta);
        ArgumentOutOfRangeException.ThrowIfNegative(shippedDelta);

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
        return await _numberSeq.NextAsync("WorkOrder");
    }

    // --- Job Generation from Part Routing ---

    public async Task<Job> GenerateJobForLineAsync(int workOrderLineId, string createdBy)
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
            throw new InvalidOperationException(
                $"Part '{line.Part.PartNumber}' has no active routing. " +
                "Add stage requirements in Parts → Edit before generating jobs.");

        // Build machine lookup: string MachineId → int Id for stage machine resolution
        var machineLookup = await _db.Machines
            .Where(m => m.IsActive)
            .ToDictionaryAsync(m => m.MachineId, m => m.Id);

        // Single job for the entire routing
        var totalEstHours = routing.Sum(r => r.GetEffectiveEstimatedHours());

        var job = new Job
        {
            JobNumber = await _numberSeq.NextAsync("Job"),
            PartId = line.PartId,
            WorkOrderLineId = line.Id,
            PartNumber = line.Part.PartNumber,
            SlsMaterial = line.Part.Material,
            Quantity = line.Quantity,
            EstimatedHours = totalEstHours,
            Status = JobStatus.Draft,
            Priority = JobPriority.Normal,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy,
            Notes = $"Auto-generated for {line.Part.PartNumber}"
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(); // Save to get job.Id

        // Create all StageExecutions in one batch (no Save per iteration)
        foreach (var stage in routing)
        {
            int? machineIntId = null;
            if (!string.IsNullOrEmpty(stage.AssignedMachineId)
                && machineLookup.TryGetValue(stage.AssignedMachineId, out var smid))
            {
                machineIntId = smid;
            }

            _db.StageExecutions.Add(new StageExecution
            {
                JobId = job.Id,
                ProductionStageId = stage.ProductionStageId,
                SortOrder = stage.ExecutionOrder,
                EstimatedHours = stage.GetEffectiveEstimatedHours(),
                EstimatedCost = stage.EstimatedCost,
                MaterialCost = stage.MaterialCost,
                SetupHours = stage.SetupTimeMinutes.HasValue ? stage.SetupTimeMinutes.Value / 60.0 : null,
                QualityCheckRequired = stage.ProductionStage.RequiresQualityCheck,
                MachineId = machineIntId,
                CreatedBy = createdBy,
                LastModifiedBy = createdBy
            });
        }

        await _db.SaveChangesAsync(); // Single batch save for all stages

        // Auto-schedule: assign optimal machines and non-overlapping time slots
        await _scheduler.AutoScheduleJobAsync(job.Id);

        // Reload to get updated schedule times from auto-scheduler
        await _db.Entry(job).ReloadAsync();

        return job;
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
            .Include(j => j.DelayLogs)
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

    public async Task<WorkOrderComment> UpdateCommentAsync(int commentId, string newContent)
    {
        var comment = await _db.WorkOrderComments.FindAsync(commentId)
            ?? throw new InvalidOperationException("Comment not found.");

        comment.Content = newContent;
        comment.EditedDate = DateTime.UtcNow;
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
