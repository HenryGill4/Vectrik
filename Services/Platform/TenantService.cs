using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Platform;
using Opcentrix_V3.Services.Auth;

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

        // Use an explicit connection so PRAGMA foreign_keys = OFF survives across
        // all commands executed by MigrateAsync (SQLite table-rebuild migrations
        // can fail with 'FOREIGN KEY constraint failed' when FK enforcement is on).
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = OFF;";
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlite(connection)
            .Options;

        using var tenantDb = new TenantDbContext(options);
        await tenantDb.Database.MigrateAsync();

        // Re-enable FK enforcement for seeding and normal operation
        await tenantDb.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        // Use DataSeedingService when available (B16), for now just ensure DB exists
        var seedingService = _serviceProvider.GetService<IDataSeedingService>();
        if (seedingService != null)
        {
            await seedingService.SeedAsync(tenantDb);
        }
    }

    // --- Tenant user management ---

    private TenantDbContext CreateTenantDbContext(string tenantCode)
    {
        var dbPath = Path.Combine("data", "tenants", $"{tenantCode}.db");
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new TenantDbContext(options);
    }

    public async Task<List<User>> GetTenantUsersAsync(string tenantCode)
    {
        using var db = CreateTenantDbContext(tenantCode);
        return await db.Users.OrderBy(u => u.Username).ToListAsync();
    }

    public async Task<User?> GetTenantUserByIdAsync(string tenantCode, int userId)
    {
        using var db = CreateTenantDbContext(tenantCode);
        return await db.Users.FindAsync(userId);
    }

    public async Task<User> CreateTenantUserAsync(string tenantCode, User user, string password)
    {
        var authService = _serviceProvider.GetRequiredService<IAuthService>();
        user.PasswordHash = authService.HashPassword(password);
        user.CreatedDate = DateTime.UtcNow;
        user.LastModifiedDate = DateTime.UtcNow;

        using var db = CreateTenantDbContext(tenantCode);
        var exists = await db.Users.AnyAsync(u => u.Username == user.Username);
        if (exists)
            throw new InvalidOperationException($"Username '{user.Username}' already exists in this tenant.");

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task UpdateTenantUserAsync(string tenantCode, User user)
    {
        using var db = CreateTenantDbContext(tenantCode);
        var existing = await db.Users.FindAsync(user.Id);
        if (existing == null) throw new InvalidOperationException("User not found.");

        existing.FullName = user.FullName;
        existing.Email = user.Email;
        existing.Role = user.Role;
        existing.Department = user.Department;
        existing.IsActive = user.IsActive;
        existing.AssignedStageIds = user.AssignedStageIds;
        existing.LastModifiedDate = DateTime.UtcNow;
        existing.LastModifiedBy = user.LastModifiedBy;

        await db.SaveChangesAsync();
    }

    public async Task ResetTenantUserPasswordAsync(string tenantCode, int userId, string newPassword)
    {
        var authService = _serviceProvider.GetRequiredService<IAuthService>();

        using var db = CreateTenantDbContext(tenantCode);
        var user = await db.Users.FindAsync(userId);
        if (user == null) throw new InvalidOperationException("User not found.");

        user.PasswordHash = authService.HashPassword(newPassword);
        user.LastModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
