using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class CustomerPricingService : ICustomerPricingService
{
    private readonly TenantDbContext _db;

    public CustomerPricingService(TenantDbContext db)
    {
        _db = db;
    }

    // ── Customer CRUD ────────────────────────────────────────

    public async Task<List<Customer>> GetAllCustomersAsync(bool includeInactive = false)
    {
        var query = _db.Customers.AsQueryable();
        if (!includeInactive)
            query = query.Where(c => c.IsActive);
        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id)
    {
        return await _db.Customers
            .Include(c => c.PricingRules.Where(r => r.ExpirationDate == null || r.ExpirationDate > DateTime.UtcNow))
                .ThenInclude(r => r.Part)
            .Include(c => c.Contracts.Where(ct => ct.Status == ContractStatus.Active))
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Customer?> FindByNameAsync(string name)
    {
        return await _db.Customers
            .FirstOrDefaultAsync(c => c.Name == name);
    }

    public async Task<Customer> SaveCustomerAsync(Customer customer)
    {
        if (customer.Id == 0)
        {
            _db.Customers.Add(customer);
        }
        else
        {
            customer.LastModifiedDate = DateTime.UtcNow;
            _db.Customers.Update(customer);
        }
        await _db.SaveChangesAsync();
        return customer;
    }

    public async Task DeleteCustomerAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer != null)
        {
            customer.IsActive = false;
            customer.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    // ── Pricing Rules ────────────────────────────────────────

    public async Task<List<CustomerPricingRule>> GetRulesForCustomerAsync(int customerId)
    {
        return await _db.CustomerPricingRules
            .Include(r => r.Part)
            .Where(r => r.CustomerId == customerId)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Part.PartNumber)
            .ToListAsync();
    }

    public async Task<CustomerPricingRule?> GetRuleAsync(int customerId, int partId, int quantity)
    {
        var now = DateTime.UtcNow;
        return await _db.CustomerPricingRules
            .Where(r => r.CustomerId == customerId
                     && r.PartId == partId
                     && r.MinQuantity <= quantity
                     && (r.MaxQuantity == null || r.MaxQuantity >= quantity)
                     && r.EffectiveDate <= now
                     && (r.ExpirationDate == null || r.ExpirationDate > now))
            .OrderBy(r => r.Priority)
            .FirstOrDefaultAsync();
    }

    public async Task<CustomerPricingRule> SaveRuleAsync(CustomerPricingRule rule)
    {
        if (rule.Id == 0)
            _db.CustomerPricingRules.Add(rule);
        else
            _db.CustomerPricingRules.Update(rule);
        await _db.SaveChangesAsync();
        return rule;
    }

    public async Task DeleteRuleAsync(int ruleId)
    {
        var rule = await _db.CustomerPricingRules.FindAsync(ruleId);
        if (rule != null)
        {
            _db.CustomerPricingRules.Remove(rule);
            await _db.SaveChangesAsync();
        }
    }

    // ── Contracts ────────────────────────────────────────────

    public async Task<List<PricingContract>> GetContractsForCustomerAsync(int customerId)
    {
        return await _db.PricingContracts
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync();
    }

    public async Task<List<PricingContract>> GetActiveContractsAsync()
    {
        var now = DateTime.UtcNow;
        return await _db.PricingContracts
            .Include(c => c.Customer)
            .Where(c => c.Status == ContractStatus.Active && c.StartDate <= now && c.EndDate >= now)
            .OrderBy(c => c.Customer.Name)
            .ToListAsync();
    }

    public async Task<PricingContract?> GetContractByIdAsync(int id)
    {
        return await _db.PricingContracts
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<PricingContract> SaveContractAsync(PricingContract contract)
    {
        if (contract.Id == 0)
            _db.PricingContracts.Add(contract);
        else
            _db.PricingContracts.Update(contract);
        await _db.SaveChangesAsync();
        return contract;
    }

    // ── Price Resolution ─────────────────────────────────────

    public async Task<CustomerPriceResult> ResolveCustomerPriceAsync(
        int customerId, int partId, int quantity, decimal standardPrice)
    {
        var result = new CustomerPriceResult
        {
            StandardPrice = standardPrice,
            ResolvedPrice = standardPrice,
            PriceSource = "Standard"
        };

        if (customerId <= 0 || standardPrice <= 0)
            return result;

        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null)
            return result;

        result.HasCustomerPricing = true;

        // 1. Check for customer+part-specific negotiated pricing rules
        var rule = await GetRuleAsync(customerId, partId, quantity);
        if (rule != null)
        {
            if (rule.NegotiatedPricePerUnit.HasValue && rule.NegotiatedPricePerUnit > 0)
            {
                // Fixed negotiated price takes highest priority
                result.ResolvedPrice = rule.NegotiatedPricePerUnit.Value;
                result.DiscountAmount = standardPrice - result.ResolvedPrice;
                result.DiscountPct = standardPrice > 0
                    ? Math.Round(result.DiscountAmount / standardPrice * 100, 2)
                    : 0;
                result.PriceSource = "Negotiated Price";
                result.RuleDescription = $"Customer-specific price for qty {rule.MinQuantity}+";
                result.AppliedRuleId = rule.Id;
                return result;
            }

            if (rule.DiscountPct > 0)
            {
                // Rule-specific discount
                result.DiscountPct = rule.DiscountPct;
                result.DiscountAmount = Math.Round(standardPrice * rule.DiscountPct / 100m, 2);
                result.ResolvedPrice = standardPrice - result.DiscountAmount;
                result.PriceSource = "Customer Part Rule";
                result.RuleDescription = $"{rule.DiscountPct}% discount for qty {rule.MinQuantity}+";
                result.AppliedRuleId = rule.Id;
                return result;
            }
        }

        // 2. Check active contracts for blanket discounts
        var now = DateTime.UtcNow;
        var contract = await _db.PricingContracts
            .Where(c => c.CustomerId == customerId
                     && c.Status == ContractStatus.Active
                     && c.StartDate <= now && c.EndDate >= now
                     && c.BlanketDiscountPct > 0)
            .OrderByDescending(c => c.BlanketDiscountPct)
            .FirstOrDefaultAsync();

        if (contract != null)
        {
            result.DiscountPct = contract.BlanketDiscountPct;
            result.DiscountAmount = Math.Round(standardPrice * contract.BlanketDiscountPct / 100m, 2);
            result.ResolvedPrice = standardPrice - result.DiscountAmount;
            result.PriceSource = "Contract";
            result.RuleDescription = $"Contract {contract.ContractNumber}: {contract.BlanketDiscountPct}% blanket discount";
            result.AppliedContractId = contract.Id;
            return result;
        }

        // 3. Fall back to customer tier default discount
        if (customer.DefaultDiscountPct > 0)
        {
            result.DiscountPct = customer.DefaultDiscountPct;
            result.DiscountAmount = Math.Round(standardPrice * customer.DefaultDiscountPct / 100m, 2);
            result.ResolvedPrice = standardPrice - result.DiscountAmount;
            result.PriceSource = $"{customer.Tier} Tier";
            result.RuleDescription = $"{customer.Tier} tier default: {customer.DefaultDiscountPct}% discount";
            return result;
        }

        return result;
    }

    // ── Customer Summary ─────────────────────────────────────

    public async Task<CustomerPricingSummary> GetCustomerPricingSummaryAsync(int customerId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null)
            return new CustomerPricingSummary();

        var now = DateTime.UtcNow;

        var activeRules = await _db.CustomerPricingRules
            .CountAsync(r => r.CustomerId == customerId
                          && r.EffectiveDate <= now
                          && (r.ExpirationDate == null || r.ExpirationDate > now));

        var activeContracts = await _db.PricingContracts
            .Where(c => c.CustomerId == customerId
                     && c.Status == ContractStatus.Active
                     && c.StartDate <= now && c.EndDate >= now)
            .ToListAsync();

        // Quote history for this customer
        var quotes = await _db.Quotes
            .Where(q => q.CustomerId == customerId)
            .ToListAsync();

        var wonQuotes = quotes.Where(q => q.Status == QuoteStatus.Accepted).ToList();
        var totalQuoted = quotes.Sum(q => q.QuotedPrice ?? 0);
        var totalWon = wonQuotes.Sum(q => q.QuotedPrice ?? 0);

        return new CustomerPricingSummary
        {
            CustomerId = customerId,
            CustomerName = customer.Name,
            Tier = customer.Tier,
            DefaultDiscountPct = customer.DefaultDiscountPct,
            DefaultMarginPct = customer.DefaultMarginPct,
            PaymentTermDays = customer.PaymentTermDays,
            ActiveRuleCount = activeRules,
            ActiveContractCount = activeContracts.Count,
            BestContract = activeContracts.OrderByDescending(c => c.BlanketDiscountPct).FirstOrDefault(),
            TotalQuotedValue = totalQuoted,
            TotalWonValue = totalWon,
            QuoteCount = quotes.Count,
            WinRate = quotes.Count > 0 ? Math.Round((decimal)wonQuotes.Count / quotes.Count * 100, 1) : 0
        };
    }
}
