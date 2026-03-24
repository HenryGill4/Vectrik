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

    // Profit & Revenue Analytics
    Task<ProfitSummary> GetProfitSummaryAsync(DateTime from, DateTime to);
    Task<List<ProfitByPartRow>> GetProfitByPartAsync(DateTime from, DateTime to);
    Task<List<ProfitByCustomerRow>> GetProfitByCustomerAsync(DateTime from, DateTime to);
    Task<List<DailyProfitPoint>> GetProfitTrendAsync(DateTime from, DateTime to);
    Task<List<UnprofitableJobRow>> GetUnprofitableJobsAsync(DateTime from, DateTime to, int maxResults = 20);
    Task<PartCostBreakdown> GetPartCostBreakdownAsync(int partId, int quantity = 60);
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

// ── Profit & Revenue DTOs ──────────────────────────────────

public class ProfitSummary
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossMarginPct { get; set; }
    public decimal TotalMaterialCost { get; set; }
    public decimal TotalLaborCost { get; set; }
    public decimal TotalOverheadCost { get; set; }
    public int CompletedJobs { get; set; }
    public int TotalPartsProduced { get; set; }
    public decimal AverageProfitPerPart { get; set; }
    public int UnprofitableJobCount { get; set; }
}

public class ProfitByPartRow
{
    public int PartId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public int TotalProduced { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Profit { get; set; }
    public decimal MarginPct { get; set; }
    public decimal SellPricePerUnit { get; set; }
    public decimal CostPerUnit { get; set; }
    public bool HasPricing { get; set; }
}

public class ProfitByCustomerRow
{
    public string CustomerName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int TotalParts { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Profit { get; set; }
    public decimal MarginPct { get; set; }
}

public class DailyProfitPoint
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public int PartsProduced { get; set; }
}

public class UnprofitableJobRow
{
    public int JobId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Loss { get; set; }
    public decimal MarginPct { get; set; }
    public DateTime CompletedDate { get; set; }
}

public class PartCostBreakdown
{
    public int PartId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public decimal SellPricePerUnit { get; set; }
    public decimal MaterialCostPerUnit { get; set; }
    public decimal ManufacturingCostPerUnit { get; set; }
    public decimal TotalCostPerUnit { get; set; }
    public decimal ProfitPerUnit { get; set; }
    public decimal MarginPct { get; set; }
    public decimal TargetMarginPct { get; set; }
    public int Quantity { get; set; }
    public List<StageCostRow> StageBreakdown { get; set; } = new();
}

public class StageCostRow
{
    public string StageName { get; set; } = string.Empty;
    public string StageIcon { get; set; } = string.Empty;
    public string StageColor { get; set; } = string.Empty;
    public string ProcessingLevel { get; set; } = string.Empty;
    public double DurationMinutes { get; set; }
    public decimal LaborCost { get; set; }
    public decimal EquipmentCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal PerPartCost { get; set; }
    public decimal ExternalCost { get; set; }
    public decimal TotalCost { get; set; }
    public decimal CostPerPart { get; set; }
}
