using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public interface IQuoteService
{
    Task<List<Quote>> GetAllQuotesAsync(QuoteStatus? statusFilter = null);
    Task<Quote?> GetQuoteByIdAsync(int id);
    Task<Quote> CreateQuoteAsync(Quote quote);
    Task<Quote> UpdateQuoteAsync(Quote quote);
    Task<QuoteLine> AddLineAsync(int quoteId, int partId, int quantity, decimal quotedPricePerPart);
    Task RemoveLineAsync(int lineId);
    Task<Quote> UpdateStatusAsync(int quoteId, QuoteStatus newStatus);
    Task<WorkOrder> ConvertToWorkOrderAsync(int quoteId, string createdBy);
    Task<decimal> CalculateEstimatedCostAsync(int partId);
    Task<string> GenerateQuoteNumberAsync();
}
