using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IBuildPlanningService
{
    Task<List<BuildPackage>> GetAllPackagesAsync();
    Task<BuildPackage?> GetPackageByIdAsync(int id);
    Task<BuildPackage> CreatePackageAsync(BuildPackage package);
    Task<BuildPackage> UpdatePackageAsync(BuildPackage package);
    Task DeletePackageAsync(int id);
    Task<BuildPackagePart> AddPartToPackageAsync(int packageId, int partId, int quantity, int? workOrderLineId = null);
    Task RemovePartFromPackageAsync(int packagePartId);
    Task<BuildFileInfo?> GetBuildFileInfoAsync(int packageId);
    Task<BuildFileInfo> SaveBuildFileInfoAsync(BuildFileInfo info);
    Task<BuildFileInfo> GenerateSpoofBuildFileAsync(int packageId, string importedBy);

    // Build Plate Execution (CHUNK-09)
    Task UpdateBuildDurationFromSliceAsync(int buildPackageId);
    Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int buildPackageId, string createdBy);
    Task CreatePartStageExecutionsAsync(int buildPackageId, string createdBy);
    Task<BuildPackageRevision> CreateRevisionAsync(int buildPackageId, string changedBy, string? notes = null);

    // Build Plate UI (CHUNK-10)
    Task<List<BuildPackageRevision>> GetRevisionsAsync(int buildPackageId);
}
