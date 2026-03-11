using Opcentrix_V3.Data;

namespace Opcentrix_V3.Services;

public interface IDataSeedingService
{
    Task SeedAsync(TenantDbContext tenantDb);
}
