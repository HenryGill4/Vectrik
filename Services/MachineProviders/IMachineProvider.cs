using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services.MachineProviders;

public interface IMachineProvider
{
    string ProviderType { get; }
    Task<MachineStateRecord> GetCurrentStateAsync(string machineId);
    Task<bool> TestConnectionAsync(string machineId);
}
