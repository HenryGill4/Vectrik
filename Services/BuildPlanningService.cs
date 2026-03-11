using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class BuildPlanningService : IBuildPlanningService
{
    private readonly TenantDbContext _db;

    public BuildPlanningService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<BuildPackage>> GetAllPackagesAsync()
    {
        return await _db.BuildPackages
            .Include(p => p.Parts)
                .ThenInclude(pp => pp.Part)
            .Include(p => p.BuildFileInfo)
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();
    }

    public async Task<BuildPackage?> GetPackageByIdAsync(int id)
    {
        return await _db.BuildPackages
            .Include(p => p.Parts)
                .ThenInclude(pp => pp.Part)
            .Include(p => p.BuildFileInfo)
            .Include(p => p.ScheduledJob)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<BuildPackage> CreatePackageAsync(BuildPackage package)
    {
        package.CreatedDate = DateTime.UtcNow;
        package.LastModifiedDate = DateTime.UtcNow;
        _db.BuildPackages.Add(package);
        await _db.SaveChangesAsync();
        return package;
    }

    public async Task<BuildPackage> UpdatePackageAsync(BuildPackage package)
    {
        package.LastModifiedDate = DateTime.UtcNow;
        _db.BuildPackages.Update(package);
        await _db.SaveChangesAsync();
        return package;
    }

    public async Task DeletePackageAsync(int id)
    {
        var package = await _db.BuildPackages.FindAsync(id);
        if (package == null) throw new InvalidOperationException("Build package not found.");
        package.Status = BuildPackageStatus.Cancelled;
        package.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<BuildPackagePart> AddPartToPackageAsync(int packageId, int partId, int quantity, int? workOrderLineId = null)
    {
        var part = new BuildPackagePart
        {
            BuildPackageId = packageId,
            PartId = partId,
            Quantity = quantity,
            WorkOrderLineId = workOrderLineId
        };

        _db.BuildPackageParts.Add(part);
        await _db.SaveChangesAsync();
        return part;
    }

    public async Task RemovePartFromPackageAsync(int packagePartId)
    {
        var part = await _db.BuildPackageParts.FindAsync(packagePartId);
        if (part == null) throw new InvalidOperationException("Build package part not found.");
        _db.BuildPackageParts.Remove(part);
        await _db.SaveChangesAsync();
    }

    public async Task<BuildFileInfo?> GetBuildFileInfoAsync(int packageId)
    {
        return await _db.BuildFileInfos
            .FirstOrDefaultAsync(f => f.BuildPackageId == packageId);
    }

    public async Task<BuildFileInfo> SaveBuildFileInfoAsync(BuildFileInfo info)
    {
        var existing = await _db.BuildFileInfos
            .FirstOrDefaultAsync(f => f.BuildPackageId == info.BuildPackageId);

        if (existing != null)
        {
            existing.FileName = info.FileName;
            existing.LayerCount = info.LayerCount;
            existing.BuildHeightMm = info.BuildHeightMm;
            existing.EstimatedPrintTimeHours = info.EstimatedPrintTimeHours;
            existing.EstimatedPowderKg = info.EstimatedPowderKg;
            existing.PartPositionsJson = info.PartPositionsJson;
            existing.SlicerSoftware = info.SlicerSoftware;
            existing.SlicerVersion = info.SlicerVersion;
            existing.ImportedDate = DateTime.UtcNow;
            existing.ImportedBy = info.ImportedBy;
            await _db.SaveChangesAsync();
            return existing;
        }

        info.ImportedDate = DateTime.UtcNow;
        _db.BuildFileInfos.Add(info);
        await _db.SaveChangesAsync();
        return info;
    }

    public async Task<BuildFileInfo> GenerateSpoofBuildFileAsync(int packageId, string importedBy)
    {
        var package = await _db.BuildPackages
            .Include(p => p.Parts)
            .FirstOrDefaultAsync(p => p.Id == packageId);

        if (package == null) throw new InvalidOperationException("Build package not found.");

        var random = new Random();
        var totalParts = package.Parts.Sum(p => p.Quantity);
        var layerCount = random.Next(800, 3500);
        var buildHeight = Math.Round((decimal)(layerCount * 0.03), 2); // ~30µm layers
        var printTime = Math.Round((decimal)(layerCount * 0.012 + totalParts * 0.5), 1);
        var powderKg = Math.Round(buildHeight * 0.045m + totalParts * 0.15m, 2);

        var positions = package.Parts.Select((p, i) => new
        {
            partId = p.PartId,
            x = Math.Round(25.0 + (i % 5) * 45.0, 1),
            y = Math.Round(25.0 + (i / 5) * 45.0, 1),
            rotation = random.Next(0, 4) * 90
        }).ToList();

        var info = new BuildFileInfo
        {
            BuildPackageId = packageId,
            FileName = $"build_{packageId:D4}_spoof.cli",
            LayerCount = layerCount,
            BuildHeightMm = buildHeight,
            EstimatedPrintTimeHours = printTime,
            EstimatedPowderKg = powderKg,
            PartPositionsJson = JsonSerializer.Serialize(positions),
            SlicerSoftware = "Manual",
            SlicerVersion = "Spoof Generator v1.0",
            ImportedBy = importedBy,
            ImportedDate = DateTime.UtcNow
        };

        return await SaveBuildFileInfoAsync(info);
    }
}
