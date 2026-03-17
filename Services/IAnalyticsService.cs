using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IAnalyticsService
{
    // Existing
    Task<DashboardKpis> GetDashboardKpisAsync();
    Task<decimal> CalculateJobCostAsync(int jobId);
    Task<decimal> CalculateWorkOrderCostAsync(int workOrderId);
    Task<List<MachineUtilization>> GetMachineUtilizationAsync(DateTime from, DateTime to);
    Task<double> GetOnTimeDeliveryRateAsync(int days = 30);
    Task<double> GetScrapRateAsync(int days = 30);

    // Module 07 additions
    Task<double> GetFirstPassYieldPctAsync(DateTime from, DateTime to);
    Task<List<OnTimeDeliveryRow>> GetOnTimeDeliveryDetailsAsync(DateTime from, DateTime to, string? customerFilter = null);
    Task<List<JobStatusCount>> GetJobStatusBreakdownAsync();
    Task<List<DailyDataPoint>> GetDailyOutputAsync(DateTime from, DateTime to);
    Task<List<DailyDataPoint>> GetDailyScrapRateAsync(DateTime from, DateTime to);
    Task<List<NcrCategoryCount>> GetNcrByCategoryAsync(DateTime from, DateTime to);
    Task<List<ScrapByPartRow>> GetScrapByPartAsync(DateTime from, DateTime to);
    Task<List<CostAnalysisRow>> GetCostAnalysisAsync(DateTime from, DateTime to, string? customerFilter = null);
    Task<CostSummary> GetCostSummaryAsync(DateTime from, DateTime to);
    Task<List<SearchResult>> SearchAsync(string query, int maxResults = 25);

    // Saved reports & dashboards
    Task<List<SavedReport>> GetSavedReportsAsync(string userId);
    Task<SavedReport> SaveReportAsync(SavedReport report);
    Task DeleteReportAsync(int id);
}

public class DashboardKpis
{
    public int ActiveJobs { get; set; }
    public int ActiveWorkOrders { get; set; }
    public int OverdueWorkOrders { get; set; }
    public double AverageUtilization { get; set; }
    public double OnTimeDeliveryRate { get; set; }
    public double ScrapRate { get; set; }
    public double FirstPassYield { get; set; }
    public int OpenNcrs { get; set; }
    public decimal AverageCostPerPart { get; set; }
    public int MaintenanceAlerts { get; set; }
    public List<StagePipelineItem> Pipeline { get; set; } = new();
}

public class MachineUtilization
{
    public string MachineId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public double UtilizationPercent { get; set; }
    public double TotalScheduledHours { get; set; }
    public double TotalActualHours { get; set; }
}

public class OnTimeDeliveryRow
{
    public int WorkOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int DeltaDays { get; set; }
    public bool IsOnTime { get; set; }
}

public class JobStatusCount
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DailyDataPoint
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
}

public class NcrCategoryCount
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ScrapByPartRow
{
    public string PartNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public int TotalProduced { get; set; }
    public int Scrapped { get; set; }
    public double ScrapRatePct { get; set; }
}

public class CostAnalysisRow
{
    public int JobId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal QuotedCost { get; set; }
    public decimal ActualCost { get; set; }
    public decimal VarianceDollar { get; set; }
    public double VariancePct { get; set; }
    public double MarginPct { get; set; }
}

public class CostSummary
{
    public decimal TotalQuoted { get; set; }
    public decimal TotalActual { get; set; }
    public double AvgVariancePct { get; set; }
    public double AvgMarginPct { get; set; }
}

public class SearchResult
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
