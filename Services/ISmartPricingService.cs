using Vectrik.Models;

namespace Vectrik.Services;

public interface ISmartPricingService
{
    /// <summary>
    /// Builds or refreshes the PartSignature for a part, pulling latest process
    /// config and actual job data. Called when a job completes or process changes.
    /// </summary>
    Task<PartSignature> RefreshSignatureAsync(int partId);

    /// <summary>Refreshes signatures for all active parts.</summary>
    Task<int> RefreshAllSignaturesAsync();

    /// <summary>
    /// Finds the k most similar parts by feature distance, returning their
    /// actual costs and process details for comparison.
    /// </summary>
    Task<List<SimilarPartMatch>> FindSimilarPartsAsync(int partId, int k = 5);

    /// <summary>
    /// Finds similar parts using ad-hoc attributes (for parts not yet in the system).
    /// </summary>
    Task<List<SimilarPartMatch>> FindSimilarByAttributesAsync(
        double weightKg, string materialCategory, int stageCount,
        double estimatedHours, bool isAdditive, int k = 5);

    /// <summary>
    /// Returns a full smart pricing recommendation combining similar-part data,
    /// parametric estimate, and confidence scoring.
    /// </summary>
    Task<SmartPriceRecommendation> GetSmartPriceAsync(int partId, int quantity, decimal targetMarginPct = 25);

    /// <summary>Calculates the complexity score for a part (1-10 scale).</summary>
    Task<ComplexityAssessment> AssessComplexityAsync(int partId);
}

/// <summary>A historically similar part with its actual cost data.</summary>
public record SimilarPartMatch(
    int PartId,
    string PartNumber,
    string PartName,
    string MaterialName,
    double SimilarityScore,
    decimal ActualCostPerPart,
    decimal EstimatedCostPerPart,
    decimal LastSellPrice,
    double ActualHoursPerPart,
    int CompletedJobCount,
    double ComplexityScore,
    double CostAccuracyRatio);

/// <summary>Full smart pricing recommendation with confidence scoring.</summary>
public class SmartPriceRecommendation
{
    /// <summary>Parametric estimate from PricingEngine (bottom-up from process definition).</summary>
    public decimal ParametricCostPerPart { get; set; }

    /// <summary>Similar-part weighted average cost (from historical actuals).</summary>
    public decimal? SimilarPartCostPerPart { get; set; }

    /// <summary>Blended AI estimate: weighted combination of parametric + similar-part data.</summary>
    public decimal AiEstimatedCostPerPart { get; set; }

    /// <summary>Recommended sell price using the AI estimate + target margin.</summary>
    public decimal RecommendedSellPrice { get; set; }

    /// <summary>Confidence level 0-100. Higher = more historical data backs this estimate.</summary>
    public int ConfidenceScore { get; set; }

    /// <summary>Human-readable confidence label.</summary>
    public string ConfidenceLabel { get; set; } = string.Empty;

    /// <summary>The similar parts used to inform the estimate.</summary>
    public List<SimilarPartMatch> SimilarParts { get; set; } = new();

    /// <summary>Complexity assessment for this part.</summary>
    public ComplexityAssessment Complexity { get; set; } = new();

    /// <summary>
    /// Average historical cost accuracy ratio for similar parts.
    /// &lt;1 means actuals tend to be below estimates (estimates are conservative).
    /// &gt;1 means actuals tend to exceed estimates (estimates are optimistic).
    /// </summary>
    public double HistoricalAccuracyRatio { get; set; }

    /// <summary>Suggested adjustment to apply based on historical accuracy.</summary>
    public string? AccuracyAdjustmentNote { get; set; }
}

/// <summary>Complexity assessment for a part.</summary>
public class ComplexityAssessment
{
    /// <summary>Overall complexity score 1-10.</summary>
    public double Score { get; set; }

    /// <summary>Human-readable tier: Simple, Moderate, Complex, Extreme.</summary>
    public string Tier { get; set; } = "Unknown";

    /// <summary>Individual factor scores that contribute to the overall score.</summary>
    public List<ComplexityFactor> Factors { get; set; } = new();
}

/// <summary>Individual factor contributing to complexity score.</summary>
public record ComplexityFactor(
    string Name,
    double Score,
    double Weight,
    string Explanation);
