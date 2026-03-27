using Microsoft.AspNetCore.SignalR;

namespace Vectrik.Hubs;

public class MachineStateHub : Hub
{
    public async Task JoinTenantGroup(string tenantCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantCode);
    }

    public async Task LeaveTenantGroup(string tenantCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tenantCode);
    }

    public async Task JoinMachineGroup(string tenantCode, string machineId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"{tenantCode}:{machineId}");
    }

    public async Task LeaveMachineGroup(string tenantCode, string machineId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{tenantCode}:{machineId}");
    }
}
