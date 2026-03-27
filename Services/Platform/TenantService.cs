using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Platform;
using Vectrik.Services.Auth;

namespace Vectrik.Services.Platform;

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
        var dataRoot = Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } home
            ? Path.Combine(home, "data") : "data";
        var dbPath = Path.Combine(dataRoot, "tenants", $"{tenantCode}.db");
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

    // --- Feature flags ---

    private static readonly List<(string Key, string Category, string Label)> _allFeatureKeys = new()
    {
        // Modules
        ("module.quoting", "Modules", "Quoting"),
        ("module.workorders", "Modules", "Work Orders"),
        ("module.shopfloor", "Modules", "Shop Floor"),
        ("module.quality", "Modules", "Quality"),
        ("module.inventory", "Modules", "Inventory"),
        ("module.analytics", "Modules", "Analytics"),
        ("module.pdm", "Modules", "PDM"),
        ("module.costing", "Modules", "Costing"),
        ("module.tools", "Modules", "Tool Management"),
        ("module.maintenance", "Modules", "Maintenance"),
        ("module.purchasing", "Modules", "Purchasing"),
        ("module.timeclock", "Modules", "Time Clock"),
        ("module.documents", "Modules", "Documents"),
        ("module.shipping", "Modules", "Shipping"),
        ("module.crm", "Modules", "CRM"),
        ("module.compliance", "Modules", "Compliance"),
        ("module.training", "Modules", "Training"),
        // Additive
        ("sls", "Additive", "SLS / LPBF"),
        ("dlms", "Additive", "DLMS"),
        ("dlms.iuid", "Additive", "DLMS — IUID"),
        ("dlms.wawf", "Additive", "DLMS — WAWF"),
        ("dlms.gfm", "Additive", "DLMS — GFM"),
        ("dlms.cdrl", "Additive", "DLMS — CDRL"),
        // Advanced
        ("advanced.spc", "Advanced", "SPC"),
        ("advanced.workflows", "Advanced", "Workflows"),
        ("advanced.custom_fields", "Advanced", "Custom Fields"),
    };

    public List<(string Key, string Category, string Label)> GetAllFeatureKeys() => _allFeatureKeys;

    public async Task<Dictionary<string, bool>> GetTenantFeatureFlagsAsync(string tenantCode)
    {
        var flags = await _platformDb.TenantFeatureFlags
            .Where(f => f.TenantCode == tenantCode)
            .ToListAsync();

        var result = new Dictionary<string, bool>();
        foreach (var key in _allFeatureKeys)
        {
            var flag = flags.FirstOrDefault(f => f.FeatureKey == key.Key);
            result[key.Key] = flag?.IsEnabled ?? false;
        }
        return result;
    }

    public async Task SetTenantFeatureFlagsAsync(string tenantCode, Dictionary<string, bool> flags)
    {
        var existing = await _platformDb.TenantFeatureFlags
            .Where(f => f.TenantCode == tenantCode)
            .ToListAsync();

        foreach (var (key, enabled) in flags)
        {
            var flag = existing.FirstOrDefault(f => f.FeatureKey == key);
            if (flag != null)
            {
                flag.IsEnabled = enabled;
                if (enabled && flag.EnabledAt == null) flag.EnabledAt = DateTime.UtcNow;
                if (!enabled) flag.EnabledAt = null;
            }
            else
            {
                _platformDb.TenantFeatureFlags.Add(new Models.Platform.TenantFeatureFlag
                {
                    TenantCode = tenantCode,
                    FeatureKey = key,
                    IsEnabled = enabled,
                    EnabledAt = enabled ? DateTime.UtcNow : null,
                    CreatedDate = DateTime.UtcNow
                });
            }
        }
        await _platformDb.SaveChangesAsync();
    }

    public async Task<int> GetTenantUserCountAsync(string tenantCode)
    {
        using var db = CreateTenantDbContext(tenantCode);
        return await db.Users.CountAsync(u => u.IsActive);
    }

    // --- Tenant user management ---

    private TenantDbContext CreateTenantDbContext(string tenantCode)
    {
        var dataRoot = Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } home
            ? Path.Combine(home, "data") : "data";
        var dbPath = Path.Combine(dataRoot, "tenants", $"{tenantCode}.db");
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
