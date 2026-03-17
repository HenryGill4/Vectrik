using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IPartService
{
    Task<List<Part>> GetAllPartsAsync(bool activeOnly = true);
    Task<Part?> GetPartByIdAsync(int id);
    Task<Part?> GetPartByNumberAsync(string partNumber);
    Task<Part> CreatePartAsync(Part part);
    Task<Part> UpdatePartAsync(Part part);
    Task DeletePartAsync(int id);
    Task<List<PartStageRequirement>> GetStageRequirementsAsync(int partId);
    Task<PartStageRequirement> AddStageRequirementAsync(PartStageRequirement requirement);
    Task<PartStageRequirement> UpdateStageRequirementAsync(PartStageRequirement requirement);
    Task RemoveStageRequirementAsync(int requirementId);
    Task<List<string>> ValidatePartAsync(Part part);

    // PDM Extensions
    Task<Part?> GetPartDetailAsync(int id);
    Task<PartRevisionHistory> BumpRevisionAsync(int partId, string newRevision, string changeDescription, string createdBy);
    Task<List<Part>> SearchPartsAsync(string searchTerm, bool activeOnly = true);
    Task<List<PartNote>> GetNotesAsync(int partId);
    Task<PartNote> AddNoteAsync(PartNote note);
    Task<PartNote> UpdateNoteAsync(PartNote note);
    Task DeleteNoteAsync(int noteId);

    // Usage & Cloning
    Task<PartUsageSummary> GetPartUsageSummaryAsync(int partId);
    Task<Part> ClonePartAsync(int sourcePartId, string newPartNumber, string createdBy);
}

public class PartUsageSummary
{
    public List<WorkOrderLine> ActiveWorkOrderLines { get; set; } = new();
    public List<Job> ActiveJobs { get; set; } = new();
    public List<QuoteLine> RecentQuoteLines { get; set; } = new();
    public int NcrCount { get; set; }
    public int InspectionCount { get; set; }
    public int SpcDataPointCount { get; set; }
}
