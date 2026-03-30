using Microsoft.EntityFrameworkCore;
using Vectrik.Data;

namespace Vectrik.Services;

public class DispatchGenerationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DispatchGenerationBackgroundService> _logger;

    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);

    public DispatchGenerationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DispatchGenerationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DispatchGenerationBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateForAllTenantsAsync(stoppingToken);
                await Task.Delay(DefaultInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-dispatch generation cycle.");
            }
        }
    }

    private async Task GenerateForAllTenantsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tenants = await platformDb.Tenants.Where(t => t.IsActive).ToListAsync(stoppingToken);

        foreach (var tenant in tenants)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await GenerateForTenantAsync(tenant.Code, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating dispatches for tenant {TenantCode}.", tenant.Code);
            }
        }
    }

    private async Task GenerateForTenantAsync(string tenantCode, CancellationToken stoppingToken)
    {
        // Create a scope and set the tenant context BEFORE resolving any tenant-scoped services.
        // This ensures TenantDbContextFactory (and all services that depend on it) use the correct tenant DB.
        using var scope = _scopeFactory.CreateScope();
        var tenantContext = (Platform.TenantContext)scope.ServiceProvider.GetRequiredService<Platform.ITenantContext>();
        tenantContext.TenantCode = tenantCode;

        var tenantDb = scope.ServiceProvider.GetRequiredService<TenantDbContext>();

        // Check if auto-dispatch is globally enabled
        var autoEnabled = await tenantDb.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "dispatch.auto_enabled", stoppingToken);
        if (autoEnabled?.Value?.Equals("true", StringComparison.OrdinalIgnoreCase) != true)
            return;

        var generationService = scope.ServiceProvider.GetRequiredService<IDispatchGenerationService>();

        var generated = await generationService.GenerateDispatchSuggestionsAsync();
        if (generated.Count > 0)
        {
            _logger.LogInformation(
                "Auto-generated {Count} dispatch suggestion(s) for tenant {TenantCode}.",
                generated.Count, tenantCode);
        }
    }
}
