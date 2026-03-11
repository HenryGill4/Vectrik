using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class QuoteService : IQuoteService
{
    private readonly TenantDbContext _db;
    private readonly IWorkOrderService _workOrderService;

    public QuoteService(TenantDbContext db, IWorkOrderService workOrderService)
    {
        _db = db;
        _workOrderService = workOrderService;
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
        var year = DateTime.UtcNow.Year;
        var prefix = $"Q-{year}-";

        var lastQuote = await _db.Quotes
            .Where(q => q.QuoteNumber.StartsWith(prefix))
            .OrderByDescending(q => q.QuoteNumber)
            .FirstOrDefaultAsync();

        var nextNumber = 1;
        if (lastQuote != null)
        {
            var suffix = lastQuote.QuoteNumber.Replace(prefix, "");
            if (int.TryParse(suffix, out var lastNum))
                nextNumber = lastNum + 1;
        }

        return $"{prefix}{nextNumber:D4}";
    }
}
