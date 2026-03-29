using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class QuoteAnalyticsService : IQuoteAnalyticsService
{
    private readonly TenantDbContext _db;

    public QuoteAnalyticsService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<QuotePipelineMetrics> GetPipelineMetricsAsync(DateTime from, DateTime to)
    {
        var quotes = await _db.Quotes
            .Where(q => q.CreatedDate >= from && q.CreatedDate <= to)
            .ToListAsync();

        var metrics = new QuotePipelineMetrics
        {
            TotalQuotes = quotes.Count,
            DraftCount = quotes.Count(q => q.Status == QuoteStatus.Draft),
            SentCount = quotes.Count(q => q.Status == QuoteStatus.Sent),
            AcceptedCount = quotes.Count(q => q.Status == QuoteStatus.Accepted),
            RejectedCount = quotes.Count(q => q.Status == QuoteStatus.Rejected),
            ExpiredCount = quotes.Count(q => q.Status == QuoteStatus.Expired),
            TotalQuotedValue = quotes.Sum(q => q.QuotedPrice ?? 0),
            TotalAcceptedValue = quotes
                .Where(q => q.Status == QuoteStatus.Accepted)
                .Sum(q => q.QuotedPrice ?? 0),
            TotalRejectedValue = quotes
                .Where(q => q.Status == QuoteStatus.Rejected)
                .Sum(q => q.QuotedPrice ?? 0),
        };

        // Win rate: Accepted / (Accepted + Rejected + Expired)
        var decided = metrics.AcceptedCount + metrics.RejectedCount + metrics.ExpiredCount;
        metrics.WinRatePct = decided > 0
            ? Math.Round((decimal)metrics.AcceptedCount / decided * 100, 1)
            : 0;

        // Average decision days
        var withDecision = quotes.Where(q => q.DecisionDays.HasValue).ToList();
        metrics.AvgDecisionDays = withDecision.Count > 0
            ? Math.Round(withDecision.Average(q => q.DecisionDays!.Value), 1)
            : 0;

        // Average margin on accepted quotes
        var accepted = quotes.Where(q => q.Status == QuoteStatus.Accepted && q.QuotedPrice > 0).ToList();
        metrics.AvgAcceptedMarginPct = accepted.Count > 0
            ? Math.Round(accepted.Average(q =>
                (q.QuotedPrice!.Value - q.TotalEstimatedCost) / q.QuotedPrice.Value * 100), 1)
            : 0;

        // Average revisions to win
        metrics.AvgRevisionsToWin = accepted.Count > 0
            ? Math.Round(accepted.Average(q => (double)q.RevisionNumber), 1)
            : 0;

        // Overall accuracy ratio from completed work orders
        metrics.OverallAccuracyRatio = await CalculateOverallAccuracyAsync();

        return metrics;
    }

    public async Task<List<LossReasonBreakdown>> GetLossReasonsAsync(DateTime from, DateTime to)
    {
        var rejected = await _db.Quotes
            .Where(q => q.Status == QuoteStatus.Rejected
                && q.CreatedDate >= from && q.CreatedDate <= to)
            .ToListAsync();

        return rejected
            .GroupBy(q => q.LossReason)
            .Select(g =>
            {
                var quotedValues = g.Where(q => q.QuotedPrice > 0 && q.CompetitorPrice > 0).ToList();
                var avgGap = quotedValues.Count > 0
                    ? (double)quotedValues.Average(q =>
                        (q.QuotedPrice!.Value - q.CompetitorPrice!.Value) / q.QuotedPrice.Value * 100)
                    : 0;

                return new LossReasonBreakdown(
                    Reason: g.Key,
                    Count: g.Count(),
                    TotalValue: g.Sum(q => q.QuotedPrice ?? 0),
                    AvgPriceGapPct: Math.Round((decimal)avgGap, 1));
            })
            .OrderByDescending(x => x.Count)
            .ToList();
    }

    public async Task<List<CustomerQuotePerformance>> GetPerformanceByCustomerAsync(DateTime from, DateTime to)
    {
        var quotes = await _db.Quotes
            .Where(q => q.CreatedDate >= from && q.CreatedDate <= to
                && q.Status != QuoteStatus.Draft)
            .ToListAsync();

        return quotes
            .GroupBy(q => q.CustomerName)
            .Select(g =>
            {
                var won = g.Count(q => q.Status == QuoteStatus.Accepted);
                var lost = g.Count(q => q.Status == QuoteStatus.Rejected || q.Status == QuoteStatus.Expired);
                var decided = won + lost;
                var wonQuotes = g.Where(q => q.Status == QuoteStatus.Accepted && q.QuotedPrice > 0).ToList();
                var avgMargin = wonQuotes.Count > 0
                    ? wonQuotes.Average(q => (q.QuotedPrice!.Value - q.TotalEstimatedCost) / q.QuotedPrice.Value * 100)
                    : 0;
                var withDecision = g.Where(q => q.DecisionDays.HasValue).ToList();
                var avgDays = withDecision.Count > 0 ? withDecision.Average(q => q.DecisionDays!.Value) : 0;

                return new CustomerQuotePerformance(
                    CustomerName: g.Key,
                    TotalQuotes: g.Count(),
                    WonCount: won,
                    LostCount: lost,
                    WinRatePct: decided > 0 ? Math.Round((decimal)won / decided * 100, 1) : 0,
                    TotalQuotedValue: g.Sum(q => q.QuotedPrice ?? 0),
                    TotalWonValue: wonQuotes.Sum(q => q.QuotedPrice ?? 0),
                    AvgMarginPct: Math.Round(avgMargin, 1),
                    AvgDecisionDays: Math.Round(avgDays, 1));
            })
            .OrderByDescending(x => x.TotalWonValue)
            .ToList();
    }

    public async Task<List<QuoteAccuracyRow>> GetQuoteAccuracyAsync(DateTime from, DateTime to)
    {
        // Find quotes that were converted to work orders, where the WO has completed jobs
        var acceptedQuotes = await _db.Quotes
            .Include(q => q.ConvertedWorkOrder)
            .Where(q => q.Status == QuoteStatus.Accepted
                && q.ConvertedWorkOrderId.HasValue
                && q.CreatedDate >= from && q.CreatedDate <= to)
            .ToListAsync();

        var results = new List<QuoteAccuracyRow>();

        foreach (var quote in acceptedQuotes)
        {
            if (quote.ConvertedWorkOrderId == null) continue;

            // Get actual costs from completed jobs for this work order
            var jobs = await _db.Jobs
                .Include(j => j.Stages)
                .Where(j => j.WorkOrderLine != null
                    && j.WorkOrderLine.WorkOrderId == quote.ConvertedWorkOrderId
                    && j.Status == JobStatus.Completed)
                .ToListAsync();

            if (jobs.Count == 0) continue;

            var actualCost = jobs.Sum(j =>
                j.Stages
                    .Where(s => s.Status == StageExecutionStatus.Completed)
                    .Sum(s => s.ActualCost ?? s.EstimatedCost ?? 0));

            var quotedEstimate = quote.TotalEstimatedCost;
            var variance = actualCost - quotedEstimate;
            var variancePct = quotedEstimate > 0
                ? (double)(variance / quotedEstimate * 100) : 0;

            var quotedPrice = quote.QuotedPrice ?? 0;
            var actualMargin = quotedPrice > 0
                ? (quotedPrice - actualCost) / quotedPrice * 100 : 0;

            results.Add(new QuoteAccuracyRow(
                QuoteId: quote.Id,
                QuoteNumber: quote.QuoteNumber,
                CustomerName: quote.CustomerName,
                WorkOrderId: quote.ConvertedWorkOrderId.Value,
                QuotedEstimate: quotedEstimate,
                ActualCost: actualCost,
                VarianceDollar: variance,
                VariancePct: Math.Round(variancePct, 1),
                QuotedPrice: quotedPrice,
                ActualMarginPct: Math.Round(actualMargin, 1)));
        }

        return results.OrderByDescending(r => Math.Abs(r.VariancePct)).ToList();
    }

    public async Task<List<StageCorrectionFactor>> GetCorrectionFactorsAsync()
    {
        // Aggregate estimated vs actual hours per production stage across all completed executions
        var stages = await _db.ProductionStages
            .Where(s => s.IsActive)
            .ToListAsync();

        var results = new List<StageCorrectionFactor>();

        foreach (var stage in stages)
        {
            var executions = await _db.StageExecutions
                .Where(e => e.ProductionStageId == stage.Id
                    && e.Status == StageExecutionStatus.Completed
                    && e.EstimatedHours.HasValue && e.EstimatedHours > 0
                    && e.ActualHours.HasValue && e.ActualHours > 0)
                .Select(e => new { e.EstimatedHours, e.ActualHours })
                .ToListAsync();

            if (executions.Count < 3) continue; // Need minimum data

            var avgEstimated = executions.Average(e => e.EstimatedHours!.Value);
            var avgActual = executions.Average(e => e.ActualHours!.Value);
            var factor = avgEstimated > 0 ? avgActual / avgEstimated : 1.0;

            var recommendation = factor switch
            {
                > 1.2 => $"Estimates are {(factor - 1) * 100:F0}% too low. Increase estimates by {(factor - 1) * 100:F0}%.",
                > 1.05 => $"Estimates are slightly optimistic ({(factor - 1) * 100:F0}% under). Minor adjustment recommended.",
                < 0.8 => $"Estimates are {(1 - factor) * 100:F0}% too high. Reduce estimates by {(1 - factor) * 100:F0}%.",
                < 0.95 => $"Estimates are slightly conservative ({(1 - factor) * 100:F0}% over). Consider tightening.",
                _ => "Estimates are accurate (within 5%)."
            };

            results.Add(new StageCorrectionFactor(
                StageId: stage.Id,
                StageName: stage.Name,
                AvgEstimatedHours: Math.Round(avgEstimated, 2),
                AvgActualHours: Math.Round(avgActual, 2),
                CorrectionFactor: Math.Round(factor, 3),
                SampleCount: executions.Count,
                Recommendation: recommendation));
        }

        return results.OrderByDescending(r => Math.Abs(r.CorrectionFactor - 1.0)).ToList();
    }

    public async Task<List<PriceSensitivityRow>> GetPriceSensitivityAsync(DateTime from, DateTime to)
    {
        var rejected = await _db.Quotes
            .Where(q => q.Status == QuoteStatus.Rejected
                && q.CreatedDate >= from && q.CreatedDate <= to)
            .OrderByDescending(q => q.CreatedDate)
            .ToListAsync();

        return rejected.Select(q =>
        {
            decimal? gapPct = null;
            if (q.QuotedPrice > 0 && q.CompetitorPrice > 0)
                gapPct = Math.Round((q.QuotedPrice.Value - q.CompetitorPrice.Value) / q.QuotedPrice.Value * 100, 1);

            return new PriceSensitivityRow(
                QuoteId: q.Id,
                QuoteNumber: q.QuoteNumber,
                CustomerName: q.CustomerName,
                QuotedPrice: q.QuotedPrice ?? 0,
                CompetitorPrice: q.CompetitorPrice,
                PriceGapPct: gapPct,
                LossReason: q.LossReason,
                LossNotes: q.LossNotes);
        }).ToList();
    }

    public async Task<List<QuoteTrendPoint>> GetQuoteTrendAsync(DateTime from, DateTime to)
    {
        var quotes = await _db.Quotes
            .Where(q => q.CreatedDate >= from && q.CreatedDate <= to
                && q.Status != QuoteStatus.Draft)
            .ToListAsync();

        return quotes
            .GroupBy(q => q.CreatedDate.ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var won = g.Count(q => q.Status == QuoteStatus.Accepted);
                var lost = g.Count(q => q.Status == QuoteStatus.Rejected || q.Status == QuoteStatus.Expired);
                var decided = won + lost;

                return new QuoteTrendPoint(
                    Period: g.Key,
                    SentCount: g.Count(),
                    WonCount: won,
                    LostCount: lost,
                    WinRatePct: decided > 0 ? Math.Round((decimal)won / decided * 100, 1) : 0,
                    TotalQuotedValue: g.Sum(q => q.QuotedPrice ?? 0),
                    TotalWonValue: g.Where(q => q.Status == QuoteStatus.Accepted).Sum(q => q.QuotedPrice ?? 0));
            })
            .ToList();
    }

    private async Task<double> CalculateOverallAccuracyAsync()
    {
        var data = await _db.StageExecutions
            .Where(e => e.Status == StageExecutionStatus.Completed
                && e.EstimatedCost.HasValue && e.EstimatedCost > 0
                && e.ActualCost.HasValue && e.ActualCost > 0)
            .Select(e => new { e.EstimatedCost, e.ActualCost })
            .Take(1000) // Cap for performance
            .ToListAsync();

        if (data.Count == 0) return 1.0;

        var totalEstimated = data.Sum(d => d.EstimatedCost!.Value);
        var totalActual = data.Sum(d => d.ActualCost!.Value);

        return totalEstimated > 0
            ? Math.Round((double)(totalActual / totalEstimated), 3)
            : 1.0;
    }
}
