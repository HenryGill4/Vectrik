using Vectrik.Data;

namespace Vectrik.Services;

public interface IDataSeedingService
{
    /// <summary>Seeds everything including demo/test data (for demo tenants).</summary>
    Task SeedAsync(TenantDbContext tenantDb);

    /// <summary>Seeds only infrastructure data: stages, machines, materials, shifts, settings, processes. No demo data or test users.</summary>
    Task SeedCoreAsync(TenantDbContext tenantDb);

    /// <summary>Creates a single admin user for a new tenant.</summary>
    Task SeedTenantAdminAsync(TenantDbContext tenantDb, string fullName, string email, string password);

    /// <summary>
    /// Idempotent: adds missing machines/stages and updates Priority on existing.
    /// Safe to call from the Debug page on existing databases.
    /// </summary>
    Task<SeedResult> EnsureSeedDataAsync(TenantDbContext db);
}

public record SeedResult(int MachinesAdded, int MachinesUpdated, int StagesAdded, int StagesUpdated);
