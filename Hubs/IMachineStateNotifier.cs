using Vectrik.Models;

namespace Vectrik.Hubs;

public interface IMachineStateNotifier
{
    Task SendMachineStateAsync(string tenantCode, MachineStateRecord state);
    Task SendMachineAlertAsync(string tenantCode, string machineId, string alertType, string message);
}
