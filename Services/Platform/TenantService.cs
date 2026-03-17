using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models.Platform;

namespace Opcentrix_V3.Services.Platform;

public class TenantService : ITenantService
{
    private readonly PlatformDbContext _platformDb;
    private readonly IServiceProvider _serviceProvider;

    public TenantService(PlatformDbContext platformDb, IServiceProvider serviceProvider)
    {
        _platformDb = platformDb;
        _serviceProvider = serviceProvider;
    }

    public async Task<List<Tenant>> GetAllTenantsAsync()
    {
        return await _platformDb.Tenants.OrderBy(t => t.CompanyName).ToListAsync();
    }

    public async Task<Tenant?> GetTenantByIdAsync(int id)
    {
        return await _platformDb.Tenants.FindAsync(id);
    }

    public async Task<Tenant?> GetTenantByCodeAsync(string code)
    {
        return await _platformDb.Tenants.FirstOrDefaultAsync(t => t.Code == code);
    }

    public async Task<Tenant> CreateTenantAsync(string code, string companyName, string createdBy, string? logoUrl = null, string? primaryColor = null)
    {
        var normalizedCode = code.ToLowerInvariant().Trim();

        var existing = await _platformDb.Tenants.AnyAsync(t => t.Code == normalizedCode);
        if (existing)
            throw new InvalidOperationException($"Tenant with code '{normalizedCode}' already exists.");

        var tenant = new Tenant
        {
            Code = normalizedCode,
            CompanyName = companyName,
            CreatedBy = createdBy,
            LogoUrl = logoUrl,
            PrimaryColor = primaryColor,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        _platformDb.Tenants.Add(tenant);
        await _platformDb.SaveChangesAsync();

        // Create and seed the tenant database
        await SeedTenantDatabaseAsync(normalizedCode);

        return tenant;
    }

    public async Task<Tenant> UpdateTenantAsync(Tenant tenant)
    {
        _platformDb.Tenants.Update(tenant);
        await _platformDb.SaveChangesAsync();
        return tenant;
    }

    public async Task DeactivateTenantAsync(int id)
    {
        var tenant = await _platformDb.Tenants.FindAsync(id);
        if (tenant == null) throw new InvalidOperationException("Tenant not found.");
        tenant.IsActive = false;
        await _platformDb.SaveChangesAsync();
    }

    public async Task ActivateTenantAsync(int id)
    {
        var tenant = await _platformDb.Tenants.FindAsync(id);
        if (tenant == null) throw new InvalidOperationException("Tenant not found.");
        tenant.IsActive = true;
        await _platformDb.SaveChangesAsync();
    }

    public async Task SeedTenantDatabaseAsync(string tenantCode)
    {
        var dbPath = Path.Combine("data", "tenants", $"{tenantCode}.db");
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        using var tenantDb = new TenantDbContext(options);
        await tenantDb.Database.MigrateAsync();

        // Use DataSeedingService when available (B16), for now just ensure DB exists
        var seedingService = _serviceProvider.GetService<IDataSeedingService>();
        if (seedingService != null)
        {
            await seedingService.SeedAsync(tenantDb);
        }

        await Task.CompletedTask;
    }
}
