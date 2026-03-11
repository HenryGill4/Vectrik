namespace Opcentrix_V3.Services;

public interface IAnalyticsService
{
    Task<DashboardKpis> GetDashboardKpisAsync();
    Task<decimal> CalculateJobCostAsync(int jobId);
    Task<decimal> CalculateWorkOrderCostAsync(int workOrderId);
    Task<List<MachineUtilization>> GetMachineUtilizationAsync(DateTime from, DateTime to);
    Task<double> GetOnTimeDeliveryRateAsync(int days = 30);
    Task<double> GetScrapRateAsync(int days = 30);
}

public class DashboardKpis
{
    public int ActiveJobs { get; set; }
    public int ActiveWorkOrders { get; set; }
    public int OverdueWorkOrders { get; set; }
    public double AverageUtilization { get; set; }
    public double OnTimeDeliveryRate { get; set; }
    public double ScrapRate { get; set; }
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
