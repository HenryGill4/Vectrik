using Vectrik.Models;

namespace Vectrik.Services;

public interface IPartPricingService
{
    /// <summary>Gets the pricing for a specific part, or null if none exists.</summary>
    Task<PartPricing?> GetByPartIdAsync(int partId);

    /// <summary>Gets all part pricings with their associated Part.</summary>
    Task<List<PartPricing>> GetAllAsync();

    /// <summary>Creates or updates the pricing for a part.</summary>
    Task<PartPricing> SaveAsync(PartPricing pricing);

    /// <summary>Deletes the pricing for a part.</summary>
    Task DeleteAsync(int pricingId);

    /// <summary>
    /// Calculates full profitability for a part given its pricing and manufacturing process cost.
    /// </summary>
    Task<PartProfitability> CalculateProfitabilityAsync(int partId, int quantity = 1);

    /// <summary>Gets pricing coverage stats across all active parts.</summary>
    Task<PricingCoverageStats> GetCoverageStatsAsync();
}

/// <summary>Profitability breakdown for a single part.</summary>
public record PartProfitability(
    int PartId,
    string PartNumber,
    string PartName,
    decimal SellPricePerUnit,
    decimal ManufacturingCostPerUnit,
    decimal MaterialCostPerUnit,
    decimal TotalCostPerUnit,
    decimal ProfitPerUnit,
    decimal MarginPct,
    decimal TargetMarginPct,
    bool MeetsTarget,
    bool HasPricing,
    bool HasProcess);

/// <summary>Coverage stats for pricing across all parts.</summary>
public record PricingCoverageStats(
    int TotalActiveParts,
    int PartsWithPricing,
    int PartsWithProcess,
    int PartsFullyConfigured,
    decimal AverageMarginPct,
    decimal AverageSellPrice);
