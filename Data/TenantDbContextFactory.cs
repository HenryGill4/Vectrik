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

        if (string.IsNullOrEmpty(tenantCode))
            throw new InvalidOperationException("Tenant code is not set. Cannot create tenant database context.");

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
