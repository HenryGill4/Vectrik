using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public interface IBuildTemplateService
{
    // CRUD
    Task<List<BuildTemplate>> GetAllAsync(BuildTemplateStatus? statusFilter = null);
    Task<BuildTemplate?> GetByIdAsync(int id);
    Task<BuildTemplate> CreateAsync(BuildTemplate template);
    Task<BuildTemplate> UpdateAsync(BuildTemplate template);
    Task ArchiveAsync(int templateId);

    // Template parts
    Task<BuildTemplatePart> AddPartAsync(int templateId, int partId, int quantity, int stackLevel = 1, string? positionNotes = null);
    Task<BuildTemplatePart> UpdatePartAsync(int templatePartId, int quantity, int stackLevel, string? positionNotes = null);
    Task RemovePartAsync(int templatePartId);

    // Slicer metadata
    /// <summary>
    /// Update slicer output metadata on the build file (file name, layers, height, powder, positions).
    /// </summary>
    Task<BuildTemplate> UpdateSlicerMetadataAsync(int templateId, string? fileName, int? layerCount,
        double? buildHeightMm, double? estimatedPowderKg, string? partPositionsJson,
        string? slicerSoftware, string? slicerVersion);

    // Certification
    Task<BuildTemplate> CertifyAsync(int templateId, string certifiedBy);

    /// <summary>
    /// Recertify a template after modifications. Auto-creates a revision snapshot of the previous state.
    /// </summary>
    Task<BuildTemplate> RecertifyAsync(int templateId, string certifiedBy, string? changeNotes = null);

    // Part lookup
    Task<List<BuildTemplate>> GetTemplatesForPartAsync(int partId, bool certifiedOnly = true);
    Task<List<BuildTemplate>> GetTemplatesNeedingRecertificationAsync();

    /// <summary>
    /// Flag all certified templates containing the given part as needing recertification.
    /// </summary>
    Task InvalidateTemplatesForPartAsync(int partId);

    /// <summary>
    /// Compute a deterministic hash from the part versions included in a template.
    /// </summary>
    string ComputePartVersionHash(IEnumerable<Part> parts);

    // Revision history
    /// <summary>Get all revision snapshots for a template, newest first.</summary>
    Task<List<BuildTemplateRevision>> GetRevisionsAsync(int templateId);

    // Instantiation
    /// <summary>
    /// Creates a new MachineProgram from a certified template, copying all part entries,
    /// slicer metadata, and material settings. Increments the template's UseCount.
    /// </summary>
    Task<MachineProgram> InstantiateAsync(int templateId, int machineId, string createdBy, int? workOrderLineId = null);

    /// <summary>
    /// Returns templates whose parts overlap with the given demand part IDs,
    /// sorted by match percentage (most relevant first).
    /// </summary>
    Task<List<BuildTemplate>> GetTemplatesWithDemandMatchAsync(List<int> demandPartIds);
}
