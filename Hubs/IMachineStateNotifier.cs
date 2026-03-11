using Opcentrix_V3.Models;

namespace Opcentrix_V3.Hubs;

public interface IMachineStateNotifier
{
    Task SendMachineStateAsync(string tenantCode, MachineStateRecord state);
    Task SendMachineAlertAsync(string tenantCode, string machineId, string alertType, string message);
}
