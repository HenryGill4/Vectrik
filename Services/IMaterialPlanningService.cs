namespace Opcentrix_V3.Services;

public interface IMaterialPlanningService
{
    Task<List<MaterialRequirement>> GetRequirementsFromOpenJobsAsync();
    Task<List<ReorderSuggestion>> GetReorderSuggestionsAsync();
    Task<MaterialAvailabilityReport> CheckJobMaterialAvailabilityAsync(int jobId);
}

public record MaterialRequirement(
    int ItemId,
    string ItemName,
    string ItemNumber,
    decimal RequiredQty,
    decimal AvailableQty,
    decimal ShortfallQty,
    DateTime? RequiredByDate);

public record ReorderSuggestion(
    int ItemId,
    string ItemName,
    string ItemNumber,
    decimal CurrentQty,
    decimal ReorderPoint,
    decimal SuggestedOrderQty,
    string Reason);

public record MaterialAvailabilityReport(
    int JobId,
    string JobInfo,
    List<MaterialAvailabilityLine> Lines,
    bool AllAvailable);

public record MaterialAvailabilityLine(
    int ItemId,
    string ItemName,
    decimal RequiredQty,
    decimal AvailableQty,
    bool IsSufficient);
