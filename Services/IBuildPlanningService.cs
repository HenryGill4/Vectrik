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

    [Obsolete("Use BuildTemplateService for slicer metadata. Kept for legacy BuildFileInfo reads.")]
    Task<BuildFileInfo?> GetBuildFileInfoAsync(int packageId);

    [Obsolete("Use BuildTemplateService.UpdateSlicerMetadataAsync instead.")]
    Task<BuildFileInfo> SaveBuildFileInfoAsync(BuildFileInfo info);

    [Obsolete("Use BuildTemplateService.UpdateSlicerMetadataAsync instead.")]
    Task<BuildFileInfo> GenerateSpoofBuildFileAsync(int packageId, string importedBy);

    // Build Plate Execution (CHUNK-09)
    Task UpdateBuildDurationFromSliceAsync(int buildPackageId);

    /// <summary>
    /// Create build-level stage executions for a build package.
    /// When forceNewJob is true, always creates a new Job (used for additional print runs
    /// of the same build file). When false, reuses the package's existing ScheduledJob.
    /// </summary>
    /// <param name="startAfter">Explicit start time for build-level executions (e.g. the slot found by FindEarliestBuildSlotAsync). Falls back to package.ScheduledDate when null.</param>
    Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int buildPackageId, string createdBy, bool forceNewJob = false, DateTime? startAfter = null);

    /// <summary>
    /// Create per-part jobs and stage executions for parts in a build package.
    /// Returns the created Job IDs so the caller can auto-schedule them.
    /// When forceNewJobs is true, always creates new jobs even if matching ones exist
    /// (used for additional print runs).
    /// </summary>
    /// <param name="startAfter">Earliest start time for per-part jobs (e.g. last build-level stage end).</param>
    Task<List<int>> CreatePartStageExecutionsAsync(int buildPackageId, string createdBy, DateTime? startAfter = null, bool forceNewJobs = false);
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
