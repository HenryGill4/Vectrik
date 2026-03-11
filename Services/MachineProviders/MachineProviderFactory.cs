using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;

namespace Opcentrix_V3.Services.MachineProviders;

public class MachineProviderFactory
{
    private readonly TenantDbContext _db;
    private readonly IServiceProvider _serviceProvider;

    public MachineProviderFactory(TenantDbContext db, IServiceProvider serviceProvider)
    {
        _db = db;
        _serviceProvider = serviceProvider;
    }

    public async Task<IMachineProvider> GetProviderAsync(string machineId)
    {
        var settings = await _db.MachineConnectionSettings
            .FirstOrDefaultAsync(s => s.MachineId == machineId);

        var providerType = settings?.ProviderType ?? "Mock";

        return providerType switch
        {
            // Future: "EOS" => new EosMachineProvider(...),
            // Future: "Trumpf" => new TrumpfMachineProvider(...),
            _ => new MockMachineProvider()
        };
    }
}
