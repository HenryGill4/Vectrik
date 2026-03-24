namespace Opcentrix_V3.Services;

/// <summary>
/// Generates demand-driven build suggestions by analyzing outstanding work order demand
/// and matching against certified build templates.
/// </summary>
public interface IBuildSuggestionService
{
    /// <summary>
    /// Generate build suggestions based on outstanding WO demand.
    /// Checks certified templates, suggests full/partial plates, mixed builds.
    /// </summary>
    Task<BuildSuggestionResult> GetSuggestionsAsync();
}

public record BuildSuggestionResult(
    List<TemplateSuggestion> TemplateSuggestions,
    List<MixedBuildSuggestion> MixedBuildSuggestions);

public record TemplateSuggestion(
    int BuildTemplateId,
    string TemplateName,
    int PartId,
    string PartNumber,
    int SuggestedQuantity,
    double EstimatedDurationHours,
    int UseCount,
    List<WorkOrderReference> FulfillsWorkOrders,
    string Rationale);

public record WorkOrderReference(
    int WorkOrderId,
    string OrderNumber,
    int WorkOrderLineId,
    int QuantityFulfilled,
    DateTime DueDate);

public record MixedBuildSuggestion(
    List<MixedBuildLine> Parts,
    string? MatchingTemplateName,
    int? MatchingTemplateId,
    double EstimatedDurationHours,
    List<WorkOrderReference> FulfillsWorkOrders,
    string Rationale);

public record MixedBuildLine(
    int PartId,
    string PartNumber,
    int SuggestedQuantity,
    string MaterialName);
