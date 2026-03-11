using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Services.Platform;

namespace Opcentrix_V3.Data;

public class TenantDbContextFactory
{
    private readonly ITenantContext _tenantContext;

    public TenantDbContextFactory(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public TenantDbContext CreateDbContext()
    {
        var tenantCode = _tenantContext.TenantCode;

        // When no tenant is resolved (unauthenticated requests, SSR prerender),
        // return an in-memory context so DI doesn't blow up. Services will
        // simply return empty results.
        if (string.IsNullOrEmpty(tenantCode))
        {
            var fallbackOptions = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase("__no_tenant__")
                .Options;
            return new TenantDbContext(fallbackOptions);
        }

        var dbPath = Path.Combine("data", "tenants", $"{tenantCode}.db");
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        var context = new TenantDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
