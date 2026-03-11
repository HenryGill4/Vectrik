using Opcentrix_V3.Models.Platform;

namespace Opcentrix_V3.Services.Platform;

public interface ITenantService
{
    Task<List<Tenant>> GetAllTenantsAsync();
    Task<Tenant?> GetTenantByIdAsync(int id);
    Task<Tenant?> GetTenantByCodeAsync(string code);
    Task<Tenant> CreateTenantAsync(string code, string companyName, string createdBy, string? logoUrl = null, string? primaryColor = null);
    Task<Tenant> UpdateTenantAsync(Tenant tenant);
    Task DeactivateTenantAsync(int id);
    Task ActivateTenantAsync(int id);
    Task SeedTenantDatabaseAsync(string tenantCode);
}
