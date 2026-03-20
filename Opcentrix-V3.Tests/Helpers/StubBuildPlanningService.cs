using Opcentrix_V3.Models;
using Opcentrix_V3.Services;

namespace Opcentrix_V3.Tests.Helpers;

/// <summary>
/// No-op stub for IBuildPlanningService used by StageService tests.
/// The build planning methods are not exercised by the operator workflow tests.
/// </summary>
internal sealed class StubBuildPlanningService : IBuildPlanningService
{
    public Task<List<BuildPackage>> GetAllPackagesAsync() => Task.FromResult(new List<BuildPackage>());
    public Task<BuildPackage?> GetPackageByIdAsync(int id) => Task.FromResult<BuildPackage?>(null);
    public Task<BuildPackage> CreatePackageAsync(BuildPackage package) => Task.FromResult(package);
    public Task<BuildPackage> UpdatePackageAsync(BuildPackage package) => Task.FromResult(package);
    public Task DeletePackageAsync(int id) => Task.CompletedTask;
    public Task<BuildPackagePart> AddPartToPackageAsync(int packageId, int partId, int quantity, int? workOrderLineId = null)
        => Task.FromResult(new BuildPackagePart());
    public Task<BuildPackagePart> UpdatePartInPackageAsync(int packagePartId, int quantity, int stackLevel, string? slicerNotes = null)
        => Task.FromResult(new BuildPackagePart());
    public Task RemovePartFromPackageAsync(int packagePartId) => Task.CompletedTask;
    public Task<BuildFileInfo?> GetBuildFileInfoAsync(int packageId) => Task.FromResult<BuildFileInfo?>(null);
    public Task<BuildFileInfo> SaveBuildFileInfoAsync(BuildFileInfo info) => Task.FromResult(info);
    public Task<BuildFileInfo> GenerateSpoofBuildFileAsync(int packageId, string importedBy) => Task.FromResult(new BuildFileInfo());
    public Task UpdateBuildDurationFromSliceAsync(int buildPackageId) => Task.CompletedTask;
    public Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int buildPackageId, string createdBy)
        => Task.FromResult(new List<StageExecution>());
    public Task CreatePartStageExecutionsAsync(int buildPackageId, string createdBy) => Task.CompletedTask;
    public Task<BuildPackageRevision> CreateRevisionAsync(int buildPackageId, string changedBy, string? notes = null)
        => Task.FromResult(new BuildPackageRevision());
    public Task<List<BuildPackageRevision>> GetRevisionsAsync(int buildPackageId)
        => Task.FromResult(new List<BuildPackageRevision>());
}
