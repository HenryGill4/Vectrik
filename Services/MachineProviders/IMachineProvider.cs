using Vectrik.Models;

namespace Vectrik.Services.MachineProviders;

public interface IMachineProvider
{
    string ProviderType { get; }
    Task<MachineStateRecord> GetCurrentStateAsync(string machineId);
    Task<bool> TestConnectionAsync(string machineId);
}
