using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class QuoteService : IQuoteService
{
    private readonly TenantDbContext _db;
    private readonly IWorkOrderService _workOrderService;
    private readonly INumberSequenceService _numberSeq;

    public QuoteService(TenantDbContext db, IWorkOrderService workOrderService, INumberSequenceService numberSeq)
    {
        _db = db;
        _workOrderService = workOrderService;
        _numberSeq = numberSeq;
    }

    public async Task<List<Quote>> GetAllQuotesAsync(QuoteStatus? statusFilter = null)
    {
        var query = _db.Quotes
            .Include(q => q.Lines)
                .ThenInclude(l => l.Part)
            .AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(q => q.Status == statusFilter.Value);

        return await query.OrderByDescending(q => q.CreatedDate).ToListAsync();
    }

    public async Task<Quote?> GetQuoteByIdAsync(int id)
    {
        return await _db.Quotes
            .Include(q => q.Lines)
                .ThenInclude(l => l.Part)
            .Include(q => q.ConvertedWorkOrder)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<Quote?> GetQuoteDetailAsync(int id)
    {
        return await _db.Quotes
            .Include(q => q.Lines)
                .ThenInclude(l => l.Part)
            .Include(q => q.Revisions.OrderByDescending(r => r.RevisionNumber))
            .Include(q => q.ConvertedWorkOrder)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<Quote> CreateQuoteAsync(Quote quote)
    {
        if (string.IsNullOrWhiteSpace(quote.QuoteNumber))
            quote.QuoteNumber = await GenerateQuoteNumberAsync();

        quote.CreatedDate = DateTime.UtcNow;

        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync();
        return quote;
    }

    public async Task<Quote> UpdateQuoteAsync(Quote quote)
    {
        _db.Quotes.Update(quote);
        await _db.SaveChangesAsync();
        return quote;
    }

    public async Task<QuoteLine> AddLineAsync(int quoteId, int partId, int quantity, decimal quotedPricePerPart)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

        var estimatedCost = await CalculateEstimatedCostAsync(partId);

        var line = new QuoteLine
        {
            QuoteId = quoteId,
            PartId = partId,
            Quantity = quantity,
            EstimatedCostPerPart = estimatedCost,
            QuotedPricePerPart = quotedPricePerPart
        };

        _db.QuoteLines.Add(line);

        // Update quote totals
        var quote = await _db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote != null)
        {
            await _db.SaveChangesAsync();
            // Recalculate after adding the line
            quote.TotalEstimatedCost = quote.Lines.Sum(l => l.EstimatedCostPerPart * l.Quantity);
            quote.QuotedPrice = quote.Lines.Sum(l => l.QuotedPricePerPart * l.Quantity);
            quote.Markup = quote.QuotedPrice - quote.TotalEstimatedCost;
        }

        await _db.SaveChangesAsync();
        return line;
    }

    public async Task RemoveLineAsync(int lineId)
    {
        var line = await _db.QuoteLines.FindAsync(lineId);
        if (line == null) throw new InvalidOperationException("Quote line not found.");

        var quoteId = line.QuoteId;
        _db.QuoteLines.Remove(line);
        await _db.SaveChangesAsync();

        // Recalculate quote totals
        var quote = await _db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote != null)
        {
            quote.TotalEstimatedCost = quote.Lines.Sum(l => l.EstimatedCostPerPart * l.Quantity);
            quote.QuotedPrice = quote.Lines.Sum(l => l.QuotedPricePerPart * l.Quantity);
            quote.Markup = quote.QuotedPrice - quote.TotalEstimatedCost;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<Quote> UpdateStatusAsync(int quoteId, QuoteStatus newStatus)
    {
        var quote = await _db.Quotes.FindAsync(quoteId);
        if (quote == null) throw new InvalidOperationException("Quote not found.");

        quote.Status = newStatus;
        await _db.SaveChangesAsync();
        return quote;
    }

    public async Task<WorkOrder> ConvertToWorkOrderAsync(int quoteId, string createdBy)
    {
        var quote = await _db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote == null) throw new InvalidOperationException("Quote not found.");
        if (quote.Status != QuoteStatus.Accepted)
            throw new InvalidOperationException("Only accepted quotes can be converted to work orders.");

        var wo = new WorkOrder
        {
            CustomerName = quote.CustomerName,
            CustomerEmail = quote.CustomerEmail,
            CustomerPhone = quote.CustomerPhone,
            OrderDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = WorkOrderStatus.Draft,
            Priority = JobPriority.Normal,
            QuoteId = quote.Id,
            ContractNumber = quote.ContractNumber,
            IsDefenseContract = quote.IsDefenseContract,
            Notes = $"Converted from quote {quote.QuoteNumber}",
            CreatedBy = createdBy,
            LastModifiedBy = createdBy
        };

        wo = await _workOrderService.CreateWorkOrderAsync(wo);

        // Add lines from quote
        foreach (var ql in quote.Lines)
        {
            await _workOrderService.AddLineAsync(wo.Id, ql.PartId, ql.Quantity, ql.Notes);
        }

        // Update quote
        quote.Status = QuoteStatus.Accepted;
        quote.ConvertedWorkOrderId = wo.Id;
        await _db.SaveChangesAsync();

        return wo;
    }

    public async Task<decimal> CalculateEstimatedCostAsync(int partId)
    {
        var requirements = await _db.PartStageRequirements
            .Include(r => r.ProductionStage)
            .Where(r => r.PartId == partId && r.IsActive)
            .ToListAsync();

        if (!requirements.Any())
            return 0;

        return requirements.Sum(r => r.CalculateTotalEstimatedCost());
    }

    public async Task<string> GenerateQuoteNumberAsync()
    {
        return await _numberSeq.NextAsync("Quote");
    }

    public async Task<QuoteLine> UpdateLineAsync(QuoteLine line)
    {
        _db.QuoteLines.Update(line);
        await _db.SaveChangesAsync();
        await RecalculateTotalsAsync(line.QuoteId);
        return line;
    }

    public async Task RecalculateTotalsAsync(int quoteId)
    {
        var quote = await _db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote == null) return;

        quote.TotalEstimatedCost = quote.Lines.Sum(l => l.EstimatedCostPerPart * l.Quantity);
        quote.QuotedPrice = quote.Lines.Sum(l => l.QuotedPricePerPart * l.Quantity);
        quote.Markup = quote.QuotedPrice - quote.TotalEstimatedCost;
        quote.EstimatedLaborCost = quote.Lines.Sum(l => (decimal)l.LaborMinutes / 60m * l.Quantity);
        quote.EstimatedMaterialCost = quote.Lines.Sum(l => l.MaterialCostEach * l.Quantity);
        quote.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<QuoteRevision> CreateRevisionAsync(int quoteId, string changeNotes, string createdBy)
    {
        var quote = await _db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quoteId)
            ?? throw new InvalidOperationException("Quote not found.");

        var linesSnapshot = System.Text.Json.JsonSerializer.Serialize(
            quote.Lines.Select(l => new
            {
                l.PartId,
                l.Quantity,
                l.EstimatedCostPerPart,
                l.QuotedPricePerPart,
                l.LaborMinutes,
                l.SetupMinutes,
                l.MaterialCostEach,
                l.OutsideProcessCost,
                l.Notes
            }).ToList());

        var revision = new QuoteRevision
        {
            QuoteId = quoteId,
            RevisionNumber = quote.RevisionNumber,
            TotalEstimatedCost = quote.TotalEstimatedCost,
            QuotedPrice = quote.QuotedPrice,
            EstimatedLaborCost = quote.EstimatedLaborCost,
            EstimatedMaterialCost = quote.EstimatedMaterialCost,
            EstimatedOverheadCost = quote.EstimatedOverheadCost,
            TargetMarginPct = quote.TargetMarginPct,
            LinesSnapshot = linesSnapshot,
            ChangeNotes = changeNotes,
            CreatedBy = createdBy
        };

        quote.RevisionNumber++;
        quote.LastModifiedBy = createdBy;
        quote.LastModifiedDate = DateTime.UtcNow;

        _db.QuoteRevisions.Add(revision);
        await _db.SaveChangesAsync();
        return revision;
    }

    // RFQ methods

    public async Task<List<RfqRequest>> GetRfqRequestsAsync(string? statusFilter = null)
    {
        var query = _db.RfqRequests.AsQueryable();
        if (!string.IsNullOrEmpty(statusFilter))
            query = query.Where(r => r.Status == statusFilter);
        return await query.OrderByDescending(r => r.SubmittedDate).ToListAsync();
    }

    public async Task<RfqRequest?> GetRfqByIdAsync(int id)
    {
        return await _db.RfqRequests
            .Include(r => r.ConvertedQuote)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<RfqRequest> SubmitRfqAsync(RfqRequest rfq)
    {
        rfq.SubmittedDate = DateTime.UtcNow;
        rfq.Status = "New";
        _db.RfqRequests.Add(rfq);
        await _db.SaveChangesAsync();
        return rfq;
    }

    public async Task<Quote> ConvertRfqToQuoteAsync(int rfqId, string createdBy)
    {
        var rfq = await _db.RfqRequests.FindAsync(rfqId)
            ?? throw new InvalidOperationException("RFQ not found.");

        var quote = new Quote
        {
            QuoteNumber = await GenerateQuoteNumberAsync(),
            CustomerName = rfq.CompanyName,
            CustomerEmail = rfq.Email,
            CustomerPhone = rfq.Phone,
            Status = QuoteStatus.Draft,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = createdBy,
            Notes = $"Converted from RFQ #{rfq.Id}: {rfq.Description}"
        };

        _db.Quotes.Add(quote);

        rfq.Status = "Quoted";
        rfq.ReviewedBy = createdBy;
        rfq.ReviewedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        rfq.ConvertedQuoteId = quote.Id;
        await _db.SaveChangesAsync();

        return quote;
    }

    public async Task DeclineRfqAsync(int rfqId, string reason, string declinedBy)
    {
        var rfq = await _db.RfqRequests.FindAsync(rfqId)
            ?? throw new InvalidOperationException("RFQ not found.");

        rfq.Status = "Declined";
        rfq.DeclineReason = reason;
        rfq.ReviewedBy = declinedBy;
        rfq.ReviewedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
