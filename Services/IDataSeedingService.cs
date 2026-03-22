using Opcentrix_V3.Data;

namespace Opcentrix_V3.Services;

public interface IDataSeedingService
{
    Task SeedAsync(TenantDbContext tenantDb);

    /// <summary>
    /// Idempotent: adds missing machines/stages and updates Priority on existing.
    /// Safe to call from the Debug page on existing databases.
    /// </summary>
    Task<SeedResult> EnsureSeedDataAsync(TenantDbContext db);
}

public record SeedResult(int MachinesAdded, int MachinesUpdated, int StagesAdded, int StagesUpdated);
