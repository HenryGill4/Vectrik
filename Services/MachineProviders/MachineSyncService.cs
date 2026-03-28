using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Hubs;
using Vectrik.Models;

namespace Vectrik.Services.MachineProviders;

public class MachineSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MachineSyncService> _logger;

    public MachineSyncService(IServiceScopeFactory scopeFactory, ILogger<MachineSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MachineSyncService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllMachinesAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during machine state polling.");
            }
        }
    }

    private async Task PollAllMachinesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tenants = await platformDb.Tenants.Where(t => t.IsActive).ToListAsync(stoppingToken);

        foreach (var tenant in tenants)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await PollTenantMachinesAsync(scope, tenant.Code, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error polling machines for tenant {TenantCode}.", tenant.Code);
            }
        }
    }

    private async Task PollTenantMachinesAsync(IServiceScope scope, string tenantCode, CancellationToken stoppingToken)
    {
        using var tenantDb = TenantDbContextFactory.CreateDbContext(tenantCode);

        var connectionSettings = await tenantDb.MachineConnectionSettings
            .Where(c => c.IsEnabled)
            .ToListAsync(stoppingToken);

        if (connectionSettings.Count == 0) return;

        var factory = new MachineProviderFactory(tenantDb, scope.ServiceProvider);
        var notifier = scope.ServiceProvider.GetRequiredService<IMachineStateNotifier>();

        foreach (var settings in connectionSettings)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var provider = await factory.GetProviderAsync(settings.MachineId);
                var state = await provider.GetCurrentStateAsync(settings.MachineId);

                // Save state record
                tenantDb.MachineStateRecords.Add(state);
                await tenantDb.SaveChangesAsync(stoppingToken);

                // Notify via SignalR
                await notifier.SendMachineStateAsync(tenantCode, state);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error polling machine {MachineId} for tenant {TenantCode}.",
                    settings.MachineId, tenantCode);
            }
        }
    }
}
