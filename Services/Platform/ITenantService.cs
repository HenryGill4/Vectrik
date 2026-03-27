using Vectrik.Models;
using Vectrik.Models.Platform;

namespace Vectrik.Services.Platform;

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

    // Feature flags
    Task<Dictionary<string, bool>> GetTenantFeatureFlagsAsync(string tenantCode);
    Task SetTenantFeatureFlagsAsync(string tenantCode, Dictionary<string, bool> flags);
    List<(string Key, string Category, string Label)> GetAllFeatureKeys();
    Task<int> GetTenantUserCountAsync(string tenantCode);

    // Tenant user management
    Task<List<User>> GetTenantUsersAsync(string tenantCode);
    Task<User?> GetTenantUserByIdAsync(string tenantCode, int userId);
    Task<User> CreateTenantUserAsync(string tenantCode, User user, string password);
    Task UpdateTenantUserAsync(string tenantCode, User user);
    Task ResetTenantUserPasswordAsync(string tenantCode, int userId, string newPassword);
}
