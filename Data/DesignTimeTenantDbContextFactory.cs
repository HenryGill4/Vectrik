using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vectrik.Data;

/// <summary>
/// Used by EF Core CLI tools (dotnet ef migrations add / update) to create
/// a TenantDbContext at design time. The connection string points to a
/// throw-away SQLite file that is never used at runtime.
/// </summary>
public class DesignTimeTenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlite("Data Source=data/design-time.db")
            .Options;

        return new TenantDbContext(options);
    }
}
