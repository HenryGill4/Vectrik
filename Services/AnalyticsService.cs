using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly TenantDbContext _db;
    private readonly IPartTrackerService _partTracker;
    private readonly IMaintenanceService _maintenance;

    public AnalyticsService(TenantDbContext db, IPartTrackerService partTracker, IMaintenanceService maintenance)
    {
        _db = db;
        _partTracker = partTracker;
        _maintenance = maintenance;
    }

    public async Task<DashboardKpis> GetDashboardKpisAsync()
    {
        var now = DateTime.UtcNow;

        var activeJobs = await _db.Jobs
            .CountAsync(j => j.Status == JobStatus.InProgress || j.Status == JobStatus.Scheduled);

        var activeWOs = await _db.WorkOrders
            .CountAsync(w => w.Status == WorkOrderStatus.Released || w.Status == WorkOrderStatus.InProgress);

        var overdueWOs = await _db.WorkOrders
            .CountAsync(w => w.DueDate < now
                && w.Status != WorkOrderStatus.Complete
                && w.Status != WorkOrderStatus.Cancelled);

        var utilization = await GetMachineUtilizationAsync(now.AddDays(-7), now);
        var avgUtil = utilization.Count > 0 ? utilization.Average(u => u.UtilizationPercent) : 0;

        var onTimeRate = await GetOnTimeDeliveryRateAsync(30);
        var scrapRate = await GetScrapRateAsync(30);

        var completedJobs = await _db.Jobs
            .Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= now.AddDays(-30))
            .ToListAsync();

        var avgCost = 0m;
        if (completedJobs.Count > 0)
        {
            var totalCost = 0m;
            foreach (var job in completedJobs)
                totalCost += await CalculateJobCostAsync(job.Id);
            var totalParts = completedJobs.Sum(j => j.ProducedQuantity);
            avgCost = totalParts > 0 ? totalCost / totalParts : 0;
        }

        var alerts = await _maintenance.EvaluateMaintenanceRulesAsync();
        var pipeline = await _partTracker.GetStagePipelineAsync();

        return new DashboardKpis
        {
            ActiveJobs = activeJobs,
            ActiveWorkOrders = activeWOs,
            OverdueWorkOrders = overdueWOs,
            AverageUtilization = avgUtil,
            OnTimeDeliveryRate = onTimeRate,
            ScrapRate = scrapRate,
            AverageCostPerPart = avgCost,
            MaintenanceAlerts = alerts.Count,
            Pipeline = pipeline
        };
    }

    public async Task<decimal> CalculateJobCostAsync(int jobId)
    {
        var executions = await _db.StageExecutions
            .Include(e => e.ProductionStage)
            .Where(e => e.JobId == jobId && e.Status == StageExecutionStatus.Completed)
            .ToListAsync();

        var totalCost = 0m;

        foreach (var exec in executions)
        {
            if (exec.ActualCost.HasValue)
            {
                totalCost += exec.ActualCost.Value;
            }
            else
            {
                // Calculate: ActualHours × HourlyRate + MaterialCost
                var hours = exec.ActualHours ?? exec.EstimatedHours ?? 0;
                var rate = exec.ProductionStage.DefaultHourlyRate;
                var materialCost = exec.MaterialCost ?? 0;
                totalCost += (decimal)hours * rate + materialCost;
            }
        }

        return totalCost;
    }

    public async Task<decimal> CalculateWorkOrderCostAsync(int workOrderId)
    {
        var lines = await _db.WorkOrderLines
            .Include(l => l.Jobs)
            .Where(l => l.WorkOrderId == workOrderId)
            .ToListAsync();

        var totalCost = 0m;
        foreach (var line in lines)
        {
            foreach (var job in line.Jobs)
                totalCost += await CalculateJobCostAsync(job.Id);
        }

        return totalCost;
    }

    public async Task<List<MachineUtilization>> GetMachineUtilizationAsync(DateTime from, DateTime to)
    {
        var machines = await _db.Machines
            .Where(m => m.IsActive)
            .ToListAsync();

        var totalHours = (to - from).TotalHours;
        if (totalHours <= 0) return new();

        var results = new List<MachineUtilization>();

        foreach (var machine in machines)
        {
            var jobs = await _db.Jobs
                .Where(j => j.MachineId == machine.MachineId
                    && j.Status != JobStatus.Cancelled
                    && j.ScheduledStart < to
                    && j.ScheduledEnd > from)
                .ToListAsync();

            var scheduledHours = jobs.Sum(j =>
            {
                var start = j.ScheduledStart < from ? from : j.ScheduledStart;
                var end = j.ScheduledEnd > to ? to : j.ScheduledEnd;
                return (end - start).TotalHours;
            });

            var actualHours = jobs
                .Where(j => j.ActualStart.HasValue)
                .Sum(j =>
                {
                    var start = j.ActualStart!.Value < from ? from : j.ActualStart.Value;
                    var end = j.ActualEnd ?? to;
                    if (end > to) end = to;
                    return (end - start).TotalHours;
                });

            results.Add(new MachineUtilization
            {
                MachineId = machine.MachineId,
                MachineName = machine.Name,
                UtilizationPercent = totalHours > 0 ? (scheduledHours / totalHours) * 100 : 0,
                TotalScheduledHours = scheduledHours,
                TotalActualHours = actualHours
            });
        }

        return results;
    }

    public async Task<double> GetOnTimeDeliveryRateAsync(int days = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var completedWOs = await _db.WorkOrders
            .Where(w => w.Status == WorkOrderStatus.Complete && w.LastModifiedDate >= cutoff)
            .ToListAsync();

        if (completedWOs.Count == 0) return 100.0;

        var onTime = completedWOs.Count(w => w.LastModifiedDate <= w.DueDate);
        return (double)onTime / completedWOs.Count * 100;
    }

    public async Task<double> GetScrapRateAsync(int days = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var jobs = await _db.Jobs
            .Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= cutoff)
            .ToListAsync();

        var totalProduced = jobs.Sum(j => j.ProducedQuantity);
        var totalDefects = jobs.Sum(j => j.DefectQuantity);

        if (totalProduced == 0) return 0;
        return (double)totalDefects / totalProduced * 100;
    }
}
