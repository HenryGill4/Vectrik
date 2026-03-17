using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Services.Platform;

namespace Opcentrix_V3.Data;

public class TenantDbContextFactory
{
    private readonly ITenantContext _tenantContext;

    // Track which tenant DBs have already been migrated this app lifetime
    // to avoid calling Migrate() on every single request.
    private static readonly HashSet<string> _migratedTenants = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _migrateLock = new();

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

        // Apply pending migrations once per tenant per app lifetime.
        if (!_migratedTenants.Contains(tenantCode))
        {
            lock (_migrateLock)
            {
                if (!_migratedTenants.Contains(tenantCode))
                {
                    context.Database.Migrate();
                    _migratedTenants.Add(tenantCode);
                }
            }
        }

        return context;
    }

    /// <summary>
    /// Create a context for a specific tenant by code (used during seeding
    /// and cross-tenant operations where ITenantContext is not available).
    /// </summary>
    public static TenantDbContext CreateDbContext(string tenantCode)
    {
        var dbPath = Path.Combine("data", "tenants", $"{tenantCode}.db");
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        var context = new TenantDbContext(options);

        if (!_migratedTenants.Contains(tenantCode))
        {
            lock (_migrateLock)
            {
                if (!_migratedTenants.Contains(tenantCode))
                {
                    context.Database.Migrate();
                    _migratedTenants.Add(tenantCode);
                }
            }
        }

        return context;
    }
}
