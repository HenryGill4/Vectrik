using Microsoft.AspNetCore.SignalR;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Hubs;

public class MachineStateNotifier : IMachineStateNotifier
{
    private readonly IHubContext<MachineStateHub> _hubContext;

    public MachineStateNotifier(IHubContext<MachineStateHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendMachineStateAsync(string tenantCode, MachineStateRecord state)
    {
        // Send to all clients watching this tenant
        await _hubContext.Clients.Group(tenantCode)
            .SendAsync("ReceiveMachineState", state);

        // Send to clients watching this specific machine
        await _hubContext.Clients.Group($"{tenantCode}:{state.MachineId}")
            .SendAsync("ReceiveMachineState", state);
    }

    public async Task SendMachineAlertAsync(string tenantCode, string machineId, string alertType, string message)
    {
        await _hubContext.Clients.Group(tenantCode)
            .SendAsync("ReceiveMachineAlert", new { machineId, alertType, message, timestamp = DateTime.UtcNow });
    }
}
