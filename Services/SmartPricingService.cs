using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class SmartPricingService : ISmartPricingService
{
    private readonly TenantDbContext _db;
    private readonly IPricingEngineService _pricingEngine;

    public SmartPricingService(TenantDbContext db, IPricingEngineService pricingEngine)
    {
        _db = db;
        _pricingEngine = pricingEngine;
    }

    // ── Signature Management ────────────────────────────────

    public async Task<PartSignature> RefreshSignatureAsync(int partId)
    {
        var part = await _db.Parts
            .Include(p => p.MaterialEntity)
            .Include(p => p.BomItems)
            .Include(p => p.AdditiveBuildConfig)
            .Include(p => p.ManufacturingProcess)
                .ThenInclude(mp => mp!.Stages)
            .FirstOrDefaultAsync(p => p.Id == partId)
            ?? throw new InvalidOperationException($"Part {partId} not found.");

        var signature = await _db.PartSignatures
            .FirstOrDefaultAsync(s => s.PartId == partId);

        if (signature == null)
        {
            signature = new PartSignature { PartId = partId };
            _db.PartSignatures.Add(signature);
        }

        // Physical attributes
        signature.WeightKg = part.EstimatedWeightKg ?? 0;
        signature.MaterialName = part.Material ?? "";
        signature.MaterialCategory = part.MaterialEntity?.Category ?? "Unknown";
        signature.MaterialCostPerKg = part.MaterialEntity?.CostPerKg ?? 0;

        // Process attributes
        var process = part.ManufacturingProcess;
        if (process != null)
        {
            signature.StageCount = process.Stages.Count;
            signature.TotalEstimatedHours = process.Stages.Sum(s =>
                (s.RunTimeMinutes ?? 0) / 60.0 + (s.SetupTimeMinutes ?? 0) / 60.0);
            signature.TotalSetupMinutes = process.Stages.Sum(s => s.SetupTimeMinutes ?? 0);
            signature.ManufacturingApproachId = process.ManufacturingApproachId ?? 0;
        }
        else
        {
            // Fallback to legacy PartStageRequirements
            var reqs = await _db.PartStageRequirements
                .Where(r => r.PartId == partId && r.IsActive)
                .ToListAsync();
            signature.StageCount = reqs.Count;
            signature.TotalEstimatedHours = reqs.Sum(r => r.GetEffectiveEstimatedHours());
            signature.TotalSetupMinutes = reqs.Sum(r => r.SetupTimeMinutes ?? 0);
        }

        // BOM
        signature.BomItemCount = part.BomItems?.Count ?? 0;

        // Additive / stacking
        var buildConfig = part.AdditiveBuildConfig;
        signature.IsAdditive = buildConfig != null;
        signature.HasStacking = buildConfig?.HasStackingConfiguration ?? false;
        signature.MaxStackLevel = buildConfig?.AvailableStackLevels.Max() ?? 1;
        signature.PlannedPartsPerBuild = buildConfig?.PlannedPartsPerBuildSingle ?? 1;

        // Estimated cost from pricing engine
        try
        {
            var breakdown = await _pricingEngine.CalculatePartCostAsync(partId, 1);
            signature.EstimatedCostPerPart = breakdown.TotalCost;
        }
        catch
        {
            signature.EstimatedCostPerPart = 0;
        }

        // Sell price from PartPricing
        var pricing = await _db.PartPricings.FirstOrDefaultAsync(p => p.PartId == partId);
        signature.LastSellPrice = pricing?.SellPricePerUnit ?? 0;

        // Actual cost data from completed jobs
        await RefreshActualCostDataAsync(signature, partId);

        // Complexity score
        signature.ComplexityScore = CalculateComplexityScore(signature);

        signature.LastUpdated = DateTime.UtcNow;
        signature.IsStale = false;

        await _db.SaveChangesAsync();
        return signature;
    }

    public async Task<int> RefreshAllSignaturesAsync()
    {
        var activePartIds = await _db.Parts
            .Where(p => p.IsActive)
            .Select(p => p.Id)
            .ToListAsync();

        var count = 0;
        foreach (var partId in activePartIds)
        {
            try
            {
                await RefreshSignatureAsync(partId);
                count++;
            }
            catch
            {
                // Skip parts that fail — don't block the batch
            }
        }
        return count;
    }

    private async Task RefreshActualCostDataAsync(PartSignature signature, int partId)
    {
        var completedJobs = await _db.Jobs
            .Include(j => j.Stages)
            .Where(j => j.PartId == partId && j.Status == JobStatus.Completed)
            .ToListAsync();

        if (completedJobs.Count == 0)
        {
            signature.CompletedJobCount = 0;
            return;
        }

        signature.CompletedJobCount = completedJobs.Count;
        signature.AverageJobQuantity = (int)completedJobs.Average(j => j.Quantity > 0 ? j.Quantity : 1);

        // Calculate actual cost per part across all completed jobs
        var totalActualCost = 0m;
        var totalActualHours = 0.0;
        var totalParts = 0;

        foreach (var job in completedJobs)
        {
            var qty = job.Quantity > 0 ? job.Quantity : 1;
            totalParts += qty;

            var jobCost = job.Stages
                .Where(s => s.Status == StageExecutionStatus.Completed)
                .Sum(s => s.ActualCost ?? s.EstimatedCost ?? 0);
            totalActualCost += jobCost;

            var jobHours = job.Stages
                .Where(s => s.Status == StageExecutionStatus.Completed)
                .Sum(s => s.ActualHours ?? s.EstimatedHours ?? 0);
            totalActualHours += jobHours;
        }

        signature.ActualCostPerPart = totalParts > 0 ? totalActualCost / totalParts : 0;
        signature.ActualHoursPerPart = totalParts > 0 ? totalActualHours / totalParts : 0;

        // Cost accuracy ratio
        if (signature.EstimatedCostPerPart > 0 && signature.ActualCostPerPart > 0)
            signature.CostAccuracyRatio = (double)(signature.ActualCostPerPart / signature.EstimatedCostPerPart);

        // Actual margin
        if (signature.LastSellPrice > 0 && signature.ActualCostPerPart > 0)
            signature.ActualMarginPct = (double)((signature.LastSellPrice - signature.ActualCostPerPart) / signature.LastSellPrice * 100);
    }

    // ── Similar Part Matching (k-NN) ────────────────────────

    public async Task<List<SimilarPartMatch>> FindSimilarPartsAsync(int partId, int k = 5)
    {
        var targetSig = await _db.PartSignatures
            .FirstOrDefaultAsync(s => s.PartId == partId);

        if (targetSig == null)
            targetSig = await RefreshSignatureAsync(partId);

        return await FindSimilarBySignatureAsync(targetSig, partId, k);
    }

    public async Task<List<SimilarPartMatch>> FindSimilarByAttributesAsync(
        double weightKg, string materialCategory, int stageCount,
        double estimatedHours, bool isAdditive, int k = 5)
    {
        var synthetic = new PartSignature
        {
            WeightKg = weightKg,
            MaterialCategory = materialCategory,
            StageCount = stageCount,
            TotalEstimatedHours = estimatedHours,
            IsAdditive = isAdditive
        };

        return await FindSimilarBySignatureAsync(synthetic, excludePartId: null, k);
    }

    private async Task<List<SimilarPartMatch>> FindSimilarBySignatureAsync(
        PartSignature target, int? excludePartId, int k)
    {
        var allSignatures = await _db.PartSignatures
            .Include(s => s.Part)
            .Where(s => s.PartId != (excludePartId ?? -1) && !s.IsStale)
            .ToListAsync();

        if (allSignatures.Count == 0)
            return new List<SimilarPartMatch>();

        // Calculate weighted Euclidean distance for each signature
        var scored = allSignatures
            .Select(s => new
            {
                Signature = s,
                Distance = CalculateDistance(target, s)
            })
            .OrderBy(x => x.Distance)
            .Take(k)
            .ToList();

        // Convert distance to similarity score (0-100)
        var maxDist = scored.Max(x => x.Distance);
        if (maxDist == 0) maxDist = 1;

        return scored.Select(x => new SimilarPartMatch(
            PartId: x.Signature.PartId,
            PartNumber: x.Signature.Part?.PartNumber ?? "",
            PartName: x.Signature.Part?.Name ?? "",
            MaterialName: x.Signature.MaterialName,
            SimilarityScore: Math.Round(Math.Max(0, (1 - x.Distance / (maxDist * 1.5)) * 100), 1),
            ActualCostPerPart: x.Signature.ActualCostPerPart,
            EstimatedCostPerPart: x.Signature.EstimatedCostPerPart,
            LastSellPrice: x.Signature.LastSellPrice,
            ActualHoursPerPart: x.Signature.ActualHoursPerPart,
            CompletedJobCount: x.Signature.CompletedJobCount,
            ComplexityScore: x.Signature.ComplexityScore,
            CostAccuracyRatio: x.Signature.CostAccuracyRatio
        )).ToList();
    }

    /// <summary>
    /// Weighted Euclidean distance across normalized feature dimensions.
    /// Features are normalized by their typical range to prevent any one
    /// dimension from dominating.
    /// </summary>
    private static double CalculateDistance(PartSignature a, PartSignature b)
    {
        // Feature weights — tuned for manufacturing cost relevance
        const double wWeight = 1.5;       // Physical size matters a lot
        const double wMaterial = 2.0;     // Material is a strong cost driver
        const double wStageCount = 1.5;   // Process complexity
        const double wHours = 2.0;        // Direct time = direct cost
        const double wSetup = 1.0;        // Setup contributes to cost
        const double wAdditive = 1.5;     // Additive vs conventional is fundamental
        const double wBom = 0.5;          // BOM complexity
        const double wComplexity = 1.0;   // Composite complexity signal

        var dist = 0.0;

        // Weight (normalized by typical range 0-50 kg)
        dist += wWeight * Sq(Normalize(a.WeightKg, b.WeightKg, 50));

        // Material match (0 = same category, 1 = different)
        var materialMatch = string.Equals(a.MaterialCategory, b.MaterialCategory, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        dist += wMaterial * Sq(materialMatch);

        // Stage count (normalized by typical range 0-15)
        dist += wStageCount * Sq(Normalize(a.StageCount, b.StageCount, 15));

        // Total estimated hours (normalized by typical range 0-100)
        dist += wHours * Sq(Normalize(a.TotalEstimatedHours, b.TotalEstimatedHours, 100));

        // Setup minutes (normalized by typical range 0-300)
        dist += wSetup * Sq(Normalize(a.TotalSetupMinutes, b.TotalSetupMinutes, 300));

        // Additive match (0 = same, 1 = different)
        var additiveMatch = a.IsAdditive == b.IsAdditive ? 0 : 1;
        dist += wAdditive * Sq(additiveMatch);

        // BOM item count (normalized by typical range 0-30)
        dist += wBom * Sq(Normalize(a.BomItemCount, b.BomItemCount, 30));

        // Complexity score (normalized by range 0-10)
        dist += wComplexity * Sq(Normalize(a.ComplexityScore, b.ComplexityScore, 10));

        return Math.Sqrt(dist);
    }

    private static double Normalize(double a, double b, double range)
        => range > 0 ? Math.Abs(a - b) / range : 0;

    private static double Sq(double x) => x * x;

    // ── Smart Price Recommendation ──────────────────────────

    public async Task<SmartPriceRecommendation> GetSmartPriceAsync(
        int partId, int quantity, decimal targetMarginPct = 25)
    {
        var recommendation = new SmartPriceRecommendation();

        // 1. Parametric estimate (bottom-up from process definition)
        var breakdown = await _pricingEngine.CalculatePartCostAsync(partId, quantity);
        recommendation.ParametricCostPerPart = quantity > 0
            ? Math.Round(breakdown.TotalCost / quantity, 2) : 0;

        // 2. Find similar parts
        var similarParts = await FindSimilarPartsAsync(partId, 5);
        recommendation.SimilarParts = similarParts;

        // 3. Similar-part weighted cost estimate
        var partsWithActuals = similarParts
            .Where(s => s.CompletedJobCount > 0 && s.ActualCostPerPart > 0)
            .ToList();

        if (partsWithActuals.Count > 0)
        {
            // Weight by similarity score and job count
            var totalWeight = 0.0;
            var weightedCost = 0m;

            foreach (var match in partsWithActuals)
            {
                var weight = match.SimilarityScore * Math.Log2(match.CompletedJobCount + 1);
                totalWeight += weight;
                weightedCost += match.ActualCostPerPart * (decimal)weight;
            }

            if (totalWeight > 0)
                recommendation.SimilarPartCostPerPart = Math.Round(weightedCost / (decimal)totalWeight, 2);
        }

        // 4. Blend parametric + similar-part estimates
        if (recommendation.SimilarPartCostPerPart.HasValue && recommendation.SimilarPartCostPerPart > 0)
        {
            // Weight depends on how much historical data we have
            var historicalWeight = Math.Min(0.6, partsWithActuals.Count * 0.15);
            var parametricWeight = 1.0 - historicalWeight;

            recommendation.AiEstimatedCostPerPart = Math.Round(
                recommendation.ParametricCostPerPart * (decimal)parametricWeight +
                recommendation.SimilarPartCostPerPart.Value * (decimal)historicalWeight, 2);
        }
        else
        {
            recommendation.AiEstimatedCostPerPart = recommendation.ParametricCostPerPart;
        }

        // 5. Historical accuracy ratio — adjust for systematic bias
        var accuracyRatios = partsWithActuals
            .Where(s => s.CostAccuracyRatio > 0)
            .Select(s => s.CostAccuracyRatio)
            .ToList();

        if (accuracyRatios.Count > 0)
        {
            recommendation.HistoricalAccuracyRatio = Math.Round(accuracyRatios.Average(), 3);

            if (recommendation.HistoricalAccuracyRatio > 1.1)
                recommendation.AccuracyAdjustmentNote =
                    $"Similar parts average {((recommendation.HistoricalAccuracyRatio - 1) * 100):F0}% over estimate. Consider padding.";
            else if (recommendation.HistoricalAccuracyRatio < 0.9)
                recommendation.AccuracyAdjustmentNote =
                    $"Similar parts average {((1 - recommendation.HistoricalAccuracyRatio) * 100):F0}% under estimate. Estimates may be conservative.";
        }

        // 6. Recommended sell price
        recommendation.RecommendedSellPrice = targetMarginPct < 100
            ? Math.Round(recommendation.AiEstimatedCostPerPart / (1 - targetMarginPct / 100m), 2)
            : 0;

        // 7. Confidence scoring
        recommendation.ConfidenceScore = CalculateConfidence(
            recommendation.ParametricCostPerPart,
            recommendation.SimilarPartCostPerPart,
            partsWithActuals.Count,
            similarParts.Count > 0 ? similarParts.Max(s => s.SimilarityScore) : 0);

        recommendation.ConfidenceLabel = recommendation.ConfidenceScore switch
        {
            >= 80 => "High",
            >= 50 => "Moderate",
            >= 25 => "Low",
            _ => "Very Low"
        };

        // 8. Complexity assessment
        recommendation.Complexity = await AssessComplexityAsync(partId);

        return recommendation;
    }

    private static int CalculateConfidence(
        decimal parametricCost, decimal? similarPartCost,
        int matchesWithActuals, double bestSimilarityScore)
    {
        var score = 0;

        // Base: do we have a parametric estimate at all?
        if (parametricCost > 0) score += 20;

        // Historical data available
        score += matchesWithActuals switch
        {
            >= 4 => 30,
            3 => 25,
            2 => 18,
            1 => 10,
            _ => 0
        };

        // Quality of similarity matches
        if (bestSimilarityScore >= 80) score += 25;
        else if (bestSimilarityScore >= 60) score += 18;
        else if (bestSimilarityScore >= 40) score += 10;
        else if (bestSimilarityScore > 0) score += 5;

        // Parametric and similar-part estimates agree
        if (similarPartCost.HasValue && similarPartCost > 0 && parametricCost > 0)
        {
            var ratio = (double)(similarPartCost.Value / parametricCost);
            if (ratio is > 0.8 and < 1.2) score += 25;      // Close agreement
            else if (ratio is > 0.6 and < 1.4) score += 15;  // Reasonable agreement
            else score += 5;                                   // Divergent — still counts for something
        }

        return Math.Min(100, score);
    }

    // ── Complexity Assessment ────────────────────────────────

    public async Task<ComplexityAssessment> AssessComplexityAsync(int partId)
    {
        var sig = await _db.PartSignatures.FirstOrDefaultAsync(s => s.PartId == partId);
        if (sig == null)
            sig = await RefreshSignatureAsync(partId);

        var assessment = new ComplexityAssessment();
        assessment.Score = sig.ComplexityScore;
        assessment.Tier = sig.ComplexityScore switch
        {
            <= 3 => "Simple",
            <= 5 => "Moderate",
            <= 7.5 => "Complex",
            _ => "Extreme"
        };

        assessment.Factors = BuildComplexityFactors(sig);
        return assessment;
    }

    /// <summary>
    /// Calculates a composite complexity score (1-10) from the part's attributes.
    /// This is the core "intelligence" — weights are tuned for SLS manufacturing.
    /// </summary>
    private static double CalculateComplexityScore(PartSignature sig)
    {
        var factors = BuildComplexityFactors(sig);
        var totalWeight = factors.Sum(f => f.Weight);
        if (totalWeight == 0) return 1;

        var weightedScore = factors.Sum(f => f.Score * f.Weight) / totalWeight;
        return Math.Round(Math.Clamp(weightedScore, 1, 10), 1);
    }

    private static List<ComplexityFactor> BuildComplexityFactors(PartSignature sig)
    {
        var factors = new List<ComplexityFactor>();

        // 1. Process complexity — more stages = more complex
        var stageScore = sig.StageCount switch
        {
            <= 2 => 2.0,
            3 or 4 => 4.0,
            5 or 6 => 6.0,
            7 or 8 => 7.5,
            _ => 9.0
        };
        factors.Add(new ComplexityFactor("Process Stages", stageScore, 2.0,
            $"{sig.StageCount} production stages"));

        // 2. Total processing time — longer = harder
        var timeScore = sig.TotalEstimatedHours switch
        {
            <= 1 => 2.0,
            <= 4 => 3.5,
            <= 10 => 5.0,
            <= 24 => 6.5,
            <= 48 => 8.0,
            _ => 9.5
        };
        factors.Add(new ComplexityFactor("Processing Time", timeScore, 2.0,
            $"{sig.TotalEstimatedHours:F1} total hours"));

        // 3. Setup intensity — high setup time relative to run time = complex tooling
        var setupRatio = sig.TotalEstimatedHours > 0
            ? sig.TotalSetupMinutes / 60.0 / sig.TotalEstimatedHours
            : 0;
        var setupScore = setupRatio switch
        {
            <= 0.1 => 2.0,
            <= 0.25 => 4.0,
            <= 0.5 => 6.0,
            <= 0.75 => 7.5,
            _ => 9.0
        };
        factors.Add(new ComplexityFactor("Setup Intensity", setupScore, 1.5,
            $"{setupRatio:P0} setup vs. run time"));

        // 4. Material premium — exotic/expensive materials add complexity
        var materialScore = (double)sig.MaterialCostPerKg switch
        {
            <= 20 => 2.0,     // Commodity materials
            <= 80 => 4.0,     // Standard metals
            <= 200 => 6.0,    // Specialty alloys
            <= 500 => 8.0,    // Titanium, Inconel
            _ => 9.5           // Exotic
        };
        factors.Add(new ComplexityFactor("Material Premium", materialScore, 1.0,
            $"{sig.MaterialCostPerKg:C}/kg — {sig.MaterialName}"));

        // 5. BOM complexity — more components = more assembly/tracking
        var bomScore = sig.BomItemCount switch
        {
            0 => 1.0,
            <= 3 => 3.0,
            <= 8 => 5.0,
            <= 15 => 7.0,
            _ => 9.0
        };
        factors.Add(new ComplexityFactor("BOM Complexity", bomScore, 1.0,
            $"{sig.BomItemCount} BOM items"));

        // 6. Additive manufacturing premium
        if (sig.IsAdditive)
        {
            var additiveScore = sig.HasStacking ? 7.0 : 5.0;
            if (sig.MaxStackLevel >= 3) additiveScore = 8.5;
            factors.Add(new ComplexityFactor("Additive Process", additiveScore, 1.5,
                sig.HasStacking
                    ? $"SLS with {sig.MaxStackLevel}x stacking"
                    : "SLS additive manufacturing"));
        }

        // 7. Part weight — heavier parts tend to need more machine time and careful handling
        var weightScore = sig.WeightKg switch
        {
            0 => 1.0,          // No weight data
            <= 0.5 => 3.0,     // Small parts
            <= 2 => 4.5,       // Medium
            <= 10 => 6.0,      // Large
            <= 50 => 7.5,      // Very large
            _ => 9.0            // Extremely large
        };
        factors.Add(new ComplexityFactor("Part Size", weightScore, 1.0,
            sig.WeightKg > 0 ? $"{sig.WeightKg:F2} kg" : "Weight not specified"));

        return factors;
    }
}
