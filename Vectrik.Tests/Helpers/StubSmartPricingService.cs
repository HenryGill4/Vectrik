using Vectrik.Models;
using Vectrik.Services;

namespace Vectrik.Tests.Helpers;

internal sealed class StubSmartPricingService : ISmartPricingService
{
    public Task<PartSignature> RefreshSignatureAsync(int partId) => Task.FromResult(new PartSignature { PartId = partId });
    public Task<int> RefreshAllSignaturesAsync() => Task.FromResult(0);
    public Task<List<SimilarPartMatch>> FindSimilarPartsAsync(int partId, int k = 5) => Task.FromResult(new List<SimilarPartMatch>());
    public Task<List<SimilarPartMatch>> FindSimilarByAttributesAsync(double weightKg, string materialCategory, int stageCount, double estimatedHours, bool isAdditive, int k = 5)
        => Task.FromResult(new List<SimilarPartMatch>());
    public Task<SmartPriceRecommendation> GetSmartPriceAsync(int partId, int quantity, decimal targetMarginPct = 25)
        => Task.FromResult(new SmartPriceRecommendation());
    public Task<ComplexityAssessment> AssessComplexityAsync(int partId)
        => Task.FromResult(new ComplexityAssessment { Score = 5, Tier = "Moderate" });
}
