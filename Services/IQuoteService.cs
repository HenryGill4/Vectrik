using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public interface IQuoteService
{
    Task<List<Quote>> GetAllQuotesAsync(QuoteStatus? statusFilter = null);
    Task<Quote?> GetQuoteByIdAsync(int id);
    Task<Quote?> GetQuoteDetailAsync(int id);
    Task<Quote> CreateQuoteAsync(Quote quote);
    Task<Quote> UpdateQuoteAsync(Quote quote);
    Task<QuoteLine> AddLineAsync(int quoteId, int partId, int quantity, decimal quotedPricePerPart);
    Task<QuoteLine> UpdateLineAsync(QuoteLine line);
    Task RemoveLineAsync(int lineId);
    Task<Quote> UpdateStatusAsync(int quoteId, QuoteStatus newStatus);
    Task<WorkOrder> ConvertToWorkOrderAsync(int quoteId, string createdBy);
    Task<decimal> CalculateEstimatedCostAsync(int partId);
    Task<string> GenerateQuoteNumberAsync();
    Task<QuoteRevision> CreateRevisionAsync(int quoteId, string changeNotes, string createdBy);
    Task RecalculateTotalsAsync(int quoteId);

    // RFQ
    Task<List<RfqRequest>> GetRfqRequestsAsync(string? statusFilter = null);
    Task<RfqRequest?> GetRfqByIdAsync(int id);
    Task<RfqRequest> SubmitRfqAsync(RfqRequest rfq);
    Task<Quote> ConvertRfqToQuoteAsync(int rfqId, string createdBy);
    Task DeclineRfqAsync(int rfqId, string reason, string declinedBy);
}
