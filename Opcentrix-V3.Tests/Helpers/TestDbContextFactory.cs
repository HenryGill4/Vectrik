using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;

namespace Opcentrix_V3.Tests.Helpers;

internal static class TestDbContextFactory
{
    /// <summary>
    /// Creates an InMemory TenantDbContext with a unique database name per call.
    /// </summary>
    internal static TenantDbContext Create(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new TenantDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
