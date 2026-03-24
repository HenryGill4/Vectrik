using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models.Platform;
using Opcentrix_V3.Services.Platform;

namespace Opcentrix_V3.Services;

public class TenantFeatureService : ITenantFeatureService
{
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantContext _tenantContext;
    private Dictionary<string, bool>? _cache;

    public TenantFeatureService(PlatformDbContext platformDb, ITenantContext tenantContext)
    {
        _platformDb = platformDb;
        _tenantContext = tenantContext;
    }

    public async Task InitializeAsync()
    {
        var tenantCode = _tenantContext.TenantCode;
        if (string.IsNullOrEmpty(tenantCode))
        {
            _cache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var flags = await _platformDb.TenantFeatureFlags
            .Where(f => f.TenantCode == tenantCode)
            .ToListAsync();

        _cache = flags.ToDictionary(f => f.FeatureKey, f => f.IsEnabled, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsEnabled(string featureKey)
    {
        if (_cache == null)
        {
            // Synchronous fallback — load cache if not yet initialized
            var tenantCode = _tenantContext.TenantCode;
            if (string.IsNullOrEmpty(tenantCode)) return false;

            _cache = _platformDb.TenantFeatureFlags
                .Where(f => f.TenantCode == tenantCode)
                .ToDictionary(f => f.FeatureKey, f => f.IsEnabled, StringComparer.OrdinalIgnoreCase);
        }

        return _cache.TryGetValue(featureKey, out var enabled) && enabled;
    }

    public async Task<List<(string Key, bool Enabled)>> GetAllFeaturesAsync()
    {
        var tenantCode = _tenantContext.TenantCode;
        if (string.IsNullOrEmpty(tenantCode))
            return new List<(string, bool)>();

        var flags = await _platformDb.TenantFeatureFlags
            .Where(f => f.TenantCode == tenantCode)
            .OrderBy(f => f.FeatureKey)
            .ToListAsync();

        return flags.Select(f => (f.FeatureKey, f.IsEnabled)).ToList();
    }

    public async Task SetFeatureAsync(string featureKey, bool enabled)
    {
        var tenantCode = _tenantContext.TenantCode;
        if (string.IsNullOrEmpty(tenantCode)) return;

        var flag = await _platformDb.TenantFeatureFlags
            .FirstOrDefaultAsync(f => f.TenantCode == tenantCode && f.FeatureKey == featureKey);

        if (flag == null)
        {
            flag = new TenantFeatureFlag
            {
                TenantCode = tenantCode,
                FeatureKey = featureKey,
                IsEnabled = enabled,
                EnabledAt = enabled ? DateTime.UtcNow : null
            };
            _platformDb.TenantFeatureFlags.Add(flag);
        }
        else
        {
            flag.IsEnabled = enabled;
            flag.EnabledAt = enabled ? DateTime.UtcNow : null;
        }

        await _platformDb.SaveChangesAsync();

        // Update cache
        if (_cache != null)
            _cache[featureKey] = enabled;
    }
}
