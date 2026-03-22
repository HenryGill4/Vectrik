using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IBuildPlanningService
{
    Task<List<BuildPackage>> GetAllPackagesAsync();
    Task<List<BuildPackage>> GetBuildsForPartAsync(int partId);
    Task<BuildPackage?> GetPackageByIdAsync(int id);
    Task<BuildPackage> CreatePackageAsync(BuildPackage package);
    Task<BuildPackage> UpdatePackageAsync(BuildPackage package);
    Task DeletePackageAsync(int id);

    /// <summary>
    /// Create a scheduled copy of an existing build package (parts, slicer data, build file info).
    /// The original build stays at its current status — it represents the build file.
    /// Each copy is a separate print run that flows through the scheduling pipeline.
    /// </summary>
    Task<BuildPackage> CreateScheduledCopyAsync(int sourcePackageId, string createdBy, int? workOrderLineId = null);

    Task<BuildPackagePart> AddPartToPackageAsync(int packageId, int partId, int quantity, int? workOrderLineId = null);
    Task<BuildPackagePart> UpdatePartInPackageAsync(int packagePartId, int quantity, int stackLevel, string? slicerNotes = null);
    Task RemovePartFromPackageAsync(int packagePartId);
    Task<BuildFileInfo?> GetBuildFileInfoAsync(int packageId);
    Task<BuildFileInfo> SaveBuildFileInfoAsync(BuildFileInfo info);
    Task<BuildFileInfo> GenerateSpoofBuildFileAsync(int packageId, string importedBy);

    // Build Plate Execution (CHUNK-09)
    Task UpdateBuildDurationFromSliceAsync(int buildPackageId);
    Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int buildPackageId, string createdBy);

    /// <summary>
    /// Create per-part jobs and stage executions for parts in a build package.
    /// Returns the created Job IDs so the caller can auto-schedule them.
    /// </summary>
    /// <param name="startAfter">Earliest start time for per-part jobs (e.g. last build-level stage end).</param>
    Task<List<int>> CreatePartStageExecutionsAsync(int buildPackageId, string createdBy, DateTime? startAfter = null);
    Task<BuildPackageRevision> CreateRevisionAsync(int buildPackageId, string changedBy, string? notes = null);

    // Build Plate UI (CHUNK-10)
    Task<List<BuildPackageRevision>> GetRevisionsAsync(int buildPackageId);

    /// <summary>
    /// Generates a formatted build name using token-based template.
    /// Tokens: {PARTS}, {MACHINE}, {DATE}, {SEQ}, {MATERIAL}
    /// Name is driven by the selected parts; machine is optional context.
    /// </summary>
    Task<string> GenerateBuildNameAsync(List<int> partIds, int machineId = 0, string? template = null);
}
