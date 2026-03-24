using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly TenantDbContext _db;
    private readonly IPartTrackerService _partTracker;
    private readonly IMaintenanceService _maintenance;
    private readonly IStageCostService _costService;
    private readonly IPartPricingService _pricingService;
    private readonly IManufacturingProcessService _processService;

    public AnalyticsService(
        TenantDbContext db,
        IPartTrackerService partTracker,
        IMaintenanceService maintenance,
        IStageCostService costService,
        IPartPricingService pricingService,
        IManufacturingProcessService processService)
    {
        _db = db;
        _partTracker = partTracker;
        _maintenance = maintenance;
        _costService = costService;
        _pricingService = pricingService;
        _processService = processService;
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
        var fpYield = await GetFirstPassYieldPctAsync(now.AddDays(-30), now);

        var openNcrs = await _db.NonConformanceReports
            .CountAsync(n => n.Status == NcrStatus.Open || n.Status == NcrStatus.InReview);

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
            FirstPassYield = fpYield,
            OpenNcrs = openNcrs,
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
                // Use cost profile for detailed breakdown; falls back to DefaultHourlyRate
                var hours = exec.ActualHours ?? exec.EstimatedHours ?? 0;
                var partCount = exec.BatchPartCount ?? 1;
                var estimate = await _costService.EstimateCostAsync(
                    exec.ProductionStageId, hours, partCount);
                totalCost += estimate.TotalCost + (exec.MaterialCost ?? 0);
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
                .Where(j => j.MachineId == machine.Id
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

    public async Task<double> GetFirstPassYieldPctAsync(DateTime from, DateTime to)
    {
        var inspections = await _db.QCInspections
            .Where(i => i.InspectionDate >= from && i.InspectionDate <= to
                && i.OverallResult != InspectionResult.Pending)
            .ToListAsync();

        if (inspections.Count == 0) return 100.0;

        var passed = inspections.Count(i => i.OverallResult == InspectionResult.Pass);
        return (double)passed / inspections.Count * 100;
    }

    public async Task<List<OnTimeDeliveryRow>> GetOnTimeDeliveryDetailsAsync(DateTime from, DateTime to, string? customerFilter = null)
    {
        var query = _db.WorkOrders
            .Where(w => w.Status == WorkOrderStatus.Complete
                && w.LastModifiedDate >= from && w.LastModifiedDate <= to);

        if (!string.IsNullOrWhiteSpace(customerFilter))
            query = query.Where(w => w.CustomerName.Contains(customerFilter));

        var wos = await query.OrderByDescending(w => w.LastModifiedDate).ToListAsync();

        return wos.Select(w =>
        {
            var delta = (w.DueDate - w.LastModifiedDate).Days;
            return new OnTimeDeliveryRow
            {
                WorkOrderId = w.Id,
                OrderNumber = w.OrderNumber,
                CustomerName = w.CustomerName,
                DueDate = w.DueDate,
                CompletedDate = w.LastModifiedDate,
                DeltaDays = delta,
                IsOnTime = w.LastModifiedDate <= w.DueDate
            };
        }).ToList();
    }

    public async Task<List<JobStatusCount>> GetJobStatusBreakdownAsync()
    {
        var jobs = await _db.Jobs
            .GroupBy(j => j.Status)
            .Select(g => new JobStatusCount { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        return jobs;
    }

    public async Task<List<DailyDataPoint>> GetDailyOutputAsync(DateTime from, DateTime to)
    {
        var jobs = await _db.Jobs
            .Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= from && j.ActualEnd <= to)
            .ToListAsync();

        var grouped = jobs
            .GroupBy(j => j.ActualEnd!.Value.Date)
            .Select(g => new DailyDataPoint { Date = g.Key, Value = g.Sum(j => j.ProducedQuantity) })
            .OrderBy(d => d.Date)
            .ToList();

        return grouped;
    }

    public async Task<List<DailyDataPoint>> GetDailyScrapRateAsync(DateTime from, DateTime to)
    {
        var jobs = await _db.Jobs
            .Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= from && j.ActualEnd <= to)
            .ToListAsync();

        var grouped = jobs
            .GroupBy(j => j.ActualEnd!.Value.Date)
            .Select(g =>
            {
                var produced = g.Sum(j => j.ProducedQuantity);
                var defects = g.Sum(j => j.DefectQuantity);
                return new DailyDataPoint
                {
                    Date = g.Key,
                    Value = produced > 0 ? (double)defects / produced * 100 : 0
                };
            })
            .OrderBy(d => d.Date)
            .ToList();

        return grouped;
    }

    public async Task<List<NcrCategoryCount>> GetNcrByCategoryAsync(DateTime from, DateTime to)
    {
        var ncrs = await _db.NonConformanceReports
            .Where(n => n.ReportedAt >= from && n.ReportedAt <= to)
            .GroupBy(n => n.Type)
            .Select(g => new NcrCategoryCount { Category = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        return ncrs;
    }

    public async Task<List<ScrapByPartRow>> GetScrapByPartAsync(DateTime from, DateTime to)
    {
        var jobs = await _db.Jobs
            .Include(j => j.Part)
            .Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= from && j.ActualEnd <= to)
            .ToListAsync();

        var grouped = jobs
            .GroupBy(j => new { j.PartId, j.Part.PartNumber, j.Part.Name })
            .Select(g =>
            {
                var produced = g.Sum(j => j.ProducedQuantity);
                var defects = g.Sum(j => j.DefectQuantity);
                return new ScrapByPartRow
                {
                    PartNumber = g.Key.PartNumber ?? "",
                    PartName = g.Key.Name,
                    TotalProduced = produced,
                    Scrapped = defects,
                    ScrapRatePct = produced > 0 ? (double)defects / produced * 100 : 0
                };
            })
            .OrderByDescending(r => r.ScrapRatePct)
            .ToList();

        return grouped;
    }

    public async Task<List<CostAnalysisRow>> GetCostAnalysisAsync(DateTime from, DateTime to, string? customerFilter = null)
    {
        var query = _db.Jobs
            .Include(j => j.Part)
            .Include(j => j.WorkOrderLine!)
                .ThenInclude(l => l.WorkOrder)
                    .ThenInclude(w => w!.Quote)
            .Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= from && j.ActualEnd <= to);

        var jobs = await query.ToListAsync();

        if (!string.IsNullOrWhiteSpace(customerFilter))
            jobs = jobs.Where(j => j.WorkOrderLine?.WorkOrder?.CustomerName?.Contains(customerFilter, StringComparison.OrdinalIgnoreCase) == true).ToList();

        var rows = new List<CostAnalysisRow>();
        foreach (var job in jobs)
        {
            var actualCost = await CalculateJobCostAsync(job.Id);
            var quotedCost = job.Quantity * (decimal)job.EstimatedHours * 100m; // simplified estimate
            if (job.WorkOrderLine?.WorkOrder?.Quote != null)
                quotedCost = job.WorkOrderLine.WorkOrder.Quote.TotalEstimatedCost;

            var variance = actualCost - quotedCost;
            var variancePct = quotedCost > 0 ? (double)(variance / quotedCost) * 100 : 0;
            var margin = quotedCost > 0 ? (double)((quotedCost - actualCost) / quotedCost) * 100 : 0;

            rows.Add(new CostAnalysisRow
            {
                JobId = job.Id,
                PartNumber = job.Part?.PartNumber ?? job.PartNumber ?? "",
                CustomerName = job.WorkOrderLine?.WorkOrder?.CustomerName ?? "",
                QuotedCost = quotedCost,
                ActualCost = actualCost,
                VarianceDollar = variance,
                VariancePct = variancePct,
                MarginPct = margin
            });
        }

        return rows;
    }

    public async Task<CostSummary> GetCostSummaryAsync(DateTime from, DateTime to)
    {
        var rows = await GetCostAnalysisAsync(from, to);
        if (rows.Count == 0)
            return new CostSummary();

        return new CostSummary
        {
            TotalQuoted = rows.Sum(r => r.QuotedCost),
            TotalActual = rows.Sum(r => r.ActualCost),
            AvgVariancePct = rows.Average(r => r.VariancePct),
            AvgMarginPct = rows.Average(r => r.MarginPct)
        };
    }

    public async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 25)
    {
        if (string.IsNullOrWhiteSpace(query)) return new();

        var results = new List<SearchResult>();
        var q = query.ToLower();

        // Work Orders
        var wos = await _db.WorkOrders
            .Where(w => w.OrderNumber.ToLower().Contains(q) || w.CustomerName.ToLower().Contains(q))
            .Take(maxResults)
            .ToListAsync();
        results.AddRange(wos.Select(w => new SearchResult
        {
            EntityType = "Work Order",
            EntityId = w.Id,
            DisplayText = $"{w.OrderNumber} — {w.CustomerName}",
            Url = $"/workorders/{w.Id}"
        }));

        // Parts
        var parts = await _db.Parts
            .Where(p => p.PartNumber!.ToLower().Contains(q) || p.Name.ToLower().Contains(q))
            .Take(maxResults)
            .ToListAsync();
        results.AddRange(parts.Select(p => new SearchResult
        {
            EntityType = "Part",
            EntityId = p.Id,
            DisplayText = $"{p.PartNumber} — {p.Name}",
            Url = $"/parts/{p.Id}"
        }));

        // Jobs
        var jobs = await _db.Jobs
            .Include(j => j.Part)
            .Where(j => j.PartNumber!.ToLower().Contains(q)
                || (j.Part != null && j.Part.Name.ToLower().Contains(q)))
            .Take(maxResults)
            .ToListAsync();
        results.AddRange(jobs.Select(j => new SearchResult
        {
            EntityType = "Job",
            EntityId = j.Id,
            DisplayText = $"Job #{j.Id} — {j.PartNumber ?? j.Part?.Name}",
            Url = $"/workorders/{j.WorkOrderLineId}"
        }));

        // NCRs
        var ncrs = await _db.NonConformanceReports
            .Where(n => n.NcrNumber.ToLower().Contains(q) || n.Description.ToLower().Contains(q))
            .Take(maxResults)
            .ToListAsync();
        results.AddRange(ncrs.Select(n => new SearchResult
        {
            EntityType = "NCR",
            EntityId = n.Id,
            DisplayText = $"{n.NcrNumber} — {n.Description}",
            Url = "/quality/ncr"
        }));

        // Quotes
        var quotes = await _db.Quotes
            .Where(qo => qo.QuoteNumber.ToLower().Contains(q) || qo.CustomerName.ToLower().Contains(q))
            .Take(maxResults)
            .ToListAsync();
        results.AddRange(quotes.Select(qo => new SearchResult
        {
            EntityType = "Quote",
            EntityId = qo.Id,
            DisplayText = $"{qo.QuoteNumber} — {qo.CustomerName}",
            Url = $"/quotes/{qo.Id}"
        }));

        return results.Take(maxResults).ToList();
    }

    public async Task<List<SavedReport>> GetSavedReportsAsync(string userId)
    {
        return await _db.SavedReports
            .Where(r => r.CreatedByUserId == userId || r.IsShared)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<SavedReport> SaveReportAsync(SavedReport report)
    {
        if (report.Id == 0)
            _db.SavedReports.Add(report);
        else
            _db.SavedReports.Update(report);

        await _db.SaveChangesAsync();
        return report;
    }

    public async Task DeleteReportAsync(int id)
    {
        var report = await _db.SavedReports.FindAsync(id);
        if (report != null)
        {
            _db.SavedReports.Remove(report);
            await _db.SaveChangesAsync();
        }
    }

    // ── Profit & Revenue Analytics ────────────────────────────────

    public async Task<ProfitSummary> GetProfitSummaryAsync(DateTime from, DateTime to)
    {
        var jobs = await _db.Jobs
            .Include(j => j.Part)
            .Include(j => j.WorkOrderLine!)
                .ThenInclude(l => l.WorkOrder)
            .Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= from && j.ActualEnd <= to)
            .ToListAsync();

        var pricings = await _db.PartPricings.ToListAsync();
        var pricingLookup = pricings.ToDictionary(p => p.PartId);

        var totalRevenue = 0m;
        var totalCost = 0m;
        var totalMaterial = 0m;
        var totalLabor = 0m;
        var totalOverhead = 0m;
        var totalParts = 0;
        var unprofitableCount = 0;

        foreach (var job in jobs)
        {
            var produced = job.ProducedQuantity;
            totalParts += produced;

            var jobCost = await CalculateJobCostAsync(job.Id);
            totalCost += jobCost;

            // Revenue from pricing or quote
            var revenue = 0m;
            if (pricingLookup.TryGetValue(job.PartId, out var pricing))
            {
                revenue = pricing.SellPricePerUnit * produced;
                totalMaterial += pricing.MaterialCostPerUnit * produced;
            }
            else if (job.WorkOrderLine?.WorkOrder?.Quote != null)
            {
                var quoteLines = await _db.QuoteLines
                    .Where(ql => ql.QuoteId == job.WorkOrderLine.WorkOrder.QuoteId && ql.PartId == job.PartId)
                    .FirstOrDefaultAsync();
                revenue = quoteLines != null ? quoteLines.QuotedPricePerPart * produced : 0;
            }

            totalRevenue += revenue;

            // Cost category breakdown from stage executions
            var executions = await _db.StageExecutions
                .Where(e => e.JobId == job.Id && e.Status == StageExecutionStatus.Completed)
                .ToListAsync();

            foreach (var exec in executions)
            {
                var hours = exec.ActualHours ?? exec.EstimatedHours ?? 0;
                var partCount = exec.BatchPartCount ?? 1;
                var estimate = await _costService.EstimateCostAsync(exec.ProductionStageId, hours, partCount);
                totalLabor += estimate.LaborCost;
                totalOverhead += estimate.OverheadCost;
            }

            if (revenue < jobCost) unprofitableCount++;
        }

        var grossProfit = totalRevenue - totalCost;
        var grossMargin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0;
        var avgProfitPerPart = totalParts > 0 ? grossProfit / totalParts : 0;

        return new ProfitSummary
        {
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            GrossProfit = grossProfit,
            GrossMarginPct = grossMargin,
            TotalMaterialCost = totalMaterial,
            TotalLaborCost = totalLabor,
            TotalOverheadCost = totalOverhead,
            CompletedJobs = jobs.Count,
            TotalPartsProduced = totalParts,
            AverageProfitPerPart = avgProfitPerPart,
            UnprofitableJobCount = unprofitableCount
        };
    }

    public async Task<List<ProfitByPartRow>> GetProfitByPartAsync(DateTime from, DateTime to)
    {
        var jobs = await _db.Jobs
            .Include(j => j.Part)
            .Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= from && j.ActualEnd <= to)
            .ToListAsync();

        var pricings = await _db.PartPricings.ToListAsync();
        var pricingLookup = pricings.ToDictionary(p => p.PartId);

        var grouped = jobs.GroupBy(j => new { j.PartId, j.Part.PartNumber, j.Part.Name });
        var rows = new List<ProfitByPartRow>();

        foreach (var group in grouped)
        {
            var produced = group.Sum(j => j.ProducedQuantity);
            var totalCost = 0m;
            foreach (var job in group)
                totalCost += await CalculateJobCostAsync(job.Id);

            var hasPricing = pricingLookup.TryGetValue(group.Key.PartId, out var pricing);
            var sellPrice = pricing?.SellPricePerUnit ?? 0;
            var revenue = sellPrice * produced;
            var profit = revenue - totalCost;
            var margin = revenue > 0 ? (profit / revenue) * 100 : 0;
            var costPerUnit = produced > 0 ? totalCost / produced : 0;

            rows.Add(new ProfitByPartRow
            {
                PartId = group.Key.PartId,
                PartNumber = group.Key.PartNumber ?? "",
                PartName = group.Key.Name,
                TotalProduced = produced,
                TotalRevenue = revenue,
                TotalCost = totalCost,
                Profit = profit,
                MarginPct = margin,
                SellPricePerUnit = sellPrice,
                CostPerUnit = costPerUnit,
                HasPricing = hasPricing
            });
        }

        return rows.OrderByDescending(r => Math.Abs(r.Profit)).ToList();
    }

    public async Task<List<ProfitByCustomerRow>> GetProfitByCustomerAsync(DateTime from, DateTime to)
    {
        var jobs = await _db.Jobs
            .Include(j => j.Part)
            .Include(j => j.WorkOrderLine!)
                .ThenInclude(l => l.WorkOrder)
            .Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= from && j.ActualEnd <= to
                && j.WorkOrderLineId != null)
            .ToListAsync();

        var pricings = await _db.PartPricings.ToListAsync();
        var pricingLookup = pricings.ToDictionary(p => p.PartId);

        var grouped = jobs
            .Where(j => j.WorkOrderLine?.WorkOrder != null)
            .GroupBy(j => j.WorkOrderLine!.WorkOrder.CustomerName);

        var rows = new List<ProfitByCustomerRow>();

        foreach (var group in grouped)
        {
            var produced = group.Sum(j => j.ProducedQuantity);
            var orderIds = group.Select(j => j.WorkOrderLine!.WorkOrderId).Distinct().Count();
            var totalCost = 0m;
            var totalRevenue = 0m;

            foreach (var job in group)
            {
                totalCost += await CalculateJobCostAsync(job.Id);
                if (pricingLookup.TryGetValue(job.PartId, out var pricing))
                    totalRevenue += pricing.SellPricePerUnit * job.ProducedQuantity;
            }

            var profit = totalRevenue - totalCost;
            var margin = totalRevenue > 0 ? (profit / totalRevenue) * 100 : 0;

            rows.Add(new ProfitByCustomerRow
            {
                CustomerName = group.Key,
                OrderCount = orderIds,
                TotalParts = produced,
                TotalRevenue = totalRevenue,
                TotalCost = totalCost,
                Profit = profit,
                MarginPct = margin
            });
        }

        return rows.OrderByDescending(r => r.TotalRevenue).ToList();
    }

    public async Task<List<DailyProfitPoint>> GetProfitTrendAsync(DateTime from, DateTime to)
    {
        var jobs = await _db.Jobs
            .Include(j => j.Part)
            .Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= from && j.ActualEnd <= to)
            .ToListAsync();

        var pricings = await _db.PartPricings.ToListAsync();
        var pricingLookup = pricings.ToDictionary(p => p.PartId);

        var dailyData = new Dictionary<DateTime, DailyProfitPoint>();

        foreach (var job in jobs)
        {
            var date = job.ActualEnd!.Value.Date;
            if (!dailyData.ContainsKey(date))
                dailyData[date] = new DailyProfitPoint { Date = date };

            var point = dailyData[date];
            var cost = await CalculateJobCostAsync(job.Id);
            point.Cost += cost;
            point.PartsProduced += job.ProducedQuantity;

            if (pricingLookup.TryGetValue(job.PartId, out var pricing))
                point.Revenue += pricing.SellPricePerUnit * job.ProducedQuantity;
        }

        foreach (var point in dailyData.Values)
            point.Profit = point.Revenue - point.Cost;

        return dailyData.Values.OrderBy(p => p.Date).ToList();
    }

    public async Task<List<UnprofitableJobRow>> GetUnprofitableJobsAsync(DateTime from, DateTime to, int maxResults = 20)
    {
        var jobs = await _db.Jobs
            .Include(j => j.Part)
            .Include(j => j.WorkOrderLine!)
                .ThenInclude(l => l.WorkOrder)
            .Where(j => j.Status == JobStatus.Completed && j.ActualEnd >= from && j.ActualEnd <= to)
            .ToListAsync();

        var pricings = await _db.PartPricings.ToListAsync();
        var pricingLookup = pricings.ToDictionary(p => p.PartId);

        var unprofitable = new List<UnprofitableJobRow>();

        foreach (var job in jobs)
        {
            var cost = await CalculateJobCostAsync(job.Id);
            var revenue = 0m;

            if (pricingLookup.TryGetValue(job.PartId, out var pricing))
                revenue = pricing.SellPricePerUnit * job.ProducedQuantity;

            if (revenue < cost && revenue > 0)
            {
                var loss = cost - revenue;
                var margin = revenue > 0 ? ((revenue - cost) / revenue) * 100 : -100;

                unprofitable.Add(new UnprofitableJobRow
                {
                    JobId = job.Id,
                    PartNumber = job.Part?.PartNumber ?? job.PartNumber ?? "",
                    CustomerName = job.WorkOrderLine?.WorkOrder?.CustomerName ?? "",
                    Revenue = revenue,
                    Cost = cost,
                    Loss = loss,
                    MarginPct = margin,
                    CompletedDate = job.ActualEnd!.Value
                });
            }
        }

        return unprofitable.OrderByDescending(r => r.Loss).Take(maxResults).ToList();
    }

    public async Task<PartCostBreakdown> GetPartCostBreakdownAsync(int partId, int quantity = 60)
    {
        var part = await _db.Parts.FindAsync(partId);
        if (part == null)
            throw new InvalidOperationException($"Part {partId} not found.");

        var pricing = await _pricingService.GetByPartIdAsync(partId);
        var process = await _processService.GetByPartIdAsync(partId);

        var breakdown = new PartCostBreakdown
        {
            PartId = partId,
            PartNumber = part.PartNumber,
            PartName = part.Name,
            SellPricePerUnit = pricing?.SellPricePerUnit ?? 0,
            MaterialCostPerUnit = pricing?.MaterialCostPerUnit ?? 0,
            TargetMarginPct = pricing?.TargetMarginPct ?? 25,
            Quantity = quantity
        };

        if (process != null)
        {
            var batchCapacity = process.DefaultBatchCapacity;
            var batchCount = batchCapacity > 0
                ? (int)Math.Ceiling((double)quantity / batchCapacity)
                : 1;

            var totalMfgCost = 0m;

            foreach (var stage in process.Stages.OrderBy(s => s.ExecutionOrder))
            {
                var batchCountForStage = stage.ProcessingLevel == Models.Enums.ProcessingLevel.Build ? 1 : batchCount;
                var duration = _processService.CalculateStageDuration(stage, quantity, batchCountForStage, buildConfigHours: null);
                var hours = duration.TotalMinutes / 60.0;

                var estimate = await _costService.EstimateCostAsync(
                    stage.ProductionStageId, hours, quantity, batchCountForStage);

                totalMfgCost += estimate.TotalCost;

                breakdown.StageBreakdown.Add(new StageCostRow
                {
                    StageName = stage.ProductionStage.Name,
                    StageIcon = stage.ProductionStage.StageIcon ?? "",
                    StageColor = stage.ProductionStage.StageColor ?? "",
                    ProcessingLevel = stage.ProcessingLevel.ToString(),
                    DurationMinutes = duration.TotalMinutes,
                    LaborCost = estimate.LaborCost,
                    EquipmentCost = estimate.EquipmentCost,
                    OverheadCost = estimate.OverheadCost,
                    PerPartCost = estimate.PerPartCost,
                    ExternalCost = estimate.ExternalCost,
                    TotalCost = estimate.TotalCost,
                    CostPerPart = estimate.CostPerPart
                });
            }

            breakdown.ManufacturingCostPerUnit = quantity > 0 ? totalMfgCost / quantity : 0;
        }

        breakdown.TotalCostPerUnit = breakdown.ManufacturingCostPerUnit + breakdown.MaterialCostPerUnit;
        breakdown.ProfitPerUnit = breakdown.SellPricePerUnit - breakdown.TotalCostPerUnit;
        breakdown.MarginPct = breakdown.SellPricePerUnit > 0
            ? (breakdown.ProfitPerUnit / breakdown.SellPricePerUnit) * 100
            : 0;

        return breakdown;
    }
}
