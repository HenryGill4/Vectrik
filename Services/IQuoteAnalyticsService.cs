using Vectrik.Models.Enums;

namespace Vectrik.Services;

public interface IQuoteAnalyticsService
{
    /// <summary>Overall quote pipeline metrics for a date range.</summary>
    Task<QuotePipelineMetrics> GetPipelineMetricsAsync(DateTime from, DateTime to);

    /// <summary>Win/loss breakdown by reason code.</summary>
    Task<List<LossReasonBreakdown>> GetLossReasonsAsync(DateTime from, DateTime to);

    /// <summary>Win rate and quote performance by customer.</summary>
    Task<List<CustomerQuotePerformance>> GetPerformanceByCustomerAsync(DateTime from, DateTime to);

    /// <summary>
    /// Quote accuracy: compare quoted estimates to actual job costs for completed work orders
    /// that originated from quotes.
    /// </summary>
    Task<List<QuoteAccuracyRow>> GetQuoteAccuracyAsync(DateTime from, DateTime to);

    /// <summary>
    /// Stage-level correction factors: how much actual costs differ from estimates,
    /// averaged across completed jobs. Used to auto-adjust future quotes.
    /// </summary>
    Task<List<StageCorrectionFactor>> GetCorrectionFactorsAsync();

    /// <summary>
    /// Price sensitivity analysis: for lost quotes, what was the price gap vs. competitor
    /// or vs. customer expectation?
    /// </summary>
    Task<List<PriceSensitivityRow>> GetPriceSensitivityAsync(DateTime from, DateTime to);

    /// <summary>Quote volume and conversion trend by month.</summary>
    Task<List<QuoteTrendPoint>> GetQuoteTrendAsync(DateTime from, DateTime to);
}

// ── DTOs ────────────────────────────────────────────────────

public class QuotePipelineMetrics
{
    public int TotalQuotes { get; set; }
    public int DraftCount { get; set; }
    public int SentCount { get; set; }
    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }
    public int ExpiredCount { get; set; }

    public decimal TotalQuotedValue { get; set; }
    public decimal TotalAcceptedValue { get; set; }
    public decimal TotalRejectedValue { get; set; }

    /// <summary>Accepted / (Accepted + Rejected + Expired) * 100</summary>
    public decimal WinRatePct { get; set; }

    /// <summary>Average days from Sent to decision.</summary>
    public double AvgDecisionDays { get; set; }

    /// <summary>Average margin % across accepted quotes.</summary>
    public decimal AvgAcceptedMarginPct { get; set; }

    /// <summary>Average number of revisions on accepted quotes.</summary>
    public double AvgRevisionsToWin { get; set; }

    /// <summary>Overall estimated-vs-actual accuracy ratio (1.0 = perfect).</summary>
    public double OverallAccuracyRatio { get; set; }
}

public record LossReasonBreakdown(
    QuoteLossReason Reason,
    int Count,
    decimal TotalValue,
    decimal AvgPriceGapPct);

public record CustomerQuotePerformance(
    string CustomerName,
    int TotalQuotes,
    int WonCount,
    int LostCount,
    decimal WinRatePct,
    decimal TotalQuotedValue,
    decimal TotalWonValue,
    decimal AvgMarginPct,
    double AvgDecisionDays);

public record QuoteAccuracyRow(
    int QuoteId,
    string QuoteNumber,
    string CustomerName,
    int WorkOrderId,
    decimal QuotedEstimate,
    decimal ActualCost,
    decimal VarianceDollar,
    double VariancePct,
    decimal QuotedPrice,
    decimal ActualMarginPct);

public record StageCorrectionFactor(
    int StageId,
    string StageName,
    double AvgEstimatedHours,
    double AvgActualHours,
    double CorrectionFactor,
    int SampleCount,
    string Recommendation);

public record PriceSensitivityRow(
    int QuoteId,
    string QuoteNumber,
    string CustomerName,
    decimal QuotedPrice,
    decimal? CompetitorPrice,
    decimal? PriceGapPct,
    QuoteLossReason LossReason,
    string? LossNotes);

public record QuoteTrendPoint(
    string Period,
    int SentCount,
    int WonCount,
    int LostCount,
    decimal WinRatePct,
    decimal TotalQuotedValue,
    decimal TotalWonValue);
