using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

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

    /// <summary>
    /// Create a BuildPackage (run) from a certified template, optionally linked to a work order line.
    /// The run is created in Ready status with BuildTemplateId set.
    /// </summary>
    Task<BuildPackage> InstantiateAsync(int templateId, int machineId, string createdBy, int? workOrderLineId = null);

    /// <summary>
    /// Create a draft template from a completed build for re-use.
    /// Copies slicer metadata from the build's associated BuildFileInfo (if any).
    /// </summary>
    Task<BuildTemplate> CreateFromBuildPackageAsync(int buildPackageId, string createdBy);

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
}
