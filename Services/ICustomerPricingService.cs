using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public interface ICustomerPricingService
{
    // ── Customer CRUD ────────────────────────────────────────
    Task<List<Customer>> GetAllCustomersAsync(bool includeInactive = false);
    Task<Customer?> GetCustomerByIdAsync(int id);
    Task<Customer?> FindByNameAsync(string name);
    Task<Customer> SaveCustomerAsync(Customer customer);
    Task DeleteCustomerAsync(int id);

    // ── Pricing Rules ────────────────────────────────────────
    Task<List<CustomerPricingRule>> GetRulesForCustomerAsync(int customerId);
    Task<CustomerPricingRule?> GetRuleAsync(int customerId, int partId, int quantity);
    Task<CustomerPricingRule> SaveRuleAsync(CustomerPricingRule rule);
    Task DeleteRuleAsync(int ruleId);

    // ── Contracts ────────────────────────────────────────────
    Task<List<PricingContract>> GetContractsForCustomerAsync(int customerId);
    Task<List<PricingContract>> GetActiveContractsAsync();
    Task<PricingContract?> GetContractByIdAsync(int id);
    Task<PricingContract> SaveContractAsync(PricingContract contract);

    // ── Price Resolution ─────────────────────────────────────
    /// <summary>
    /// Resolves the best price for a customer+part+quantity combo by evaluating:
    /// 1. Customer-specific negotiated price rules (highest priority)
    /// 2. Active contract blanket discounts
    /// 3. Customer tier default discount
    /// 4. Falls back to standard PartPricing sell price
    /// Returns the resolved price with explanation of which rule applied.
    /// </summary>
    Task<CustomerPriceResult> ResolveCustomerPriceAsync(int customerId, int partId, int quantity, decimal standardPrice);

    /// <summary>
    /// Gets a summary of a customer's pricing profile for display in the quote editor.
    /// </summary>
    Task<CustomerPricingSummary> GetCustomerPricingSummaryAsync(int customerId);
}

/// <summary>Result of customer price resolution with full audit trail.</summary>
public class CustomerPriceResult
{
    public decimal ResolvedPrice { get; set; }
    public decimal StandardPrice { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal DiscountAmount { get; set; }
    public string PriceSource { get; set; } = string.Empty;
    public string? RuleDescription { get; set; }
    public int? AppliedRuleId { get; set; }
    public int? AppliedContractId { get; set; }
    public bool HasCustomerPricing { get; set; }
}

/// <summary>Customer pricing profile for quote editor display.</summary>
public class CustomerPricingSummary
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public CustomerTier Tier { get; set; }
    public decimal DefaultDiscountPct { get; set; }
    public decimal? DefaultMarginPct { get; set; }
    public int PaymentTermDays { get; set; }
    public int ActiveRuleCount { get; set; }
    public int ActiveContractCount { get; set; }
    public PricingContract? BestContract { get; set; }
    public decimal TotalQuotedValue { get; set; }
    public decimal TotalWonValue { get; set; }
    public int QuoteCount { get; set; }
    public decimal WinRate { get; set; }
}
