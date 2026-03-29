using Microsoft.AspNetCore.SignalR;

namespace Vectrik.Hubs;

public class DispatchHub : Hub
{
    public async Task JoinTenantGroup(string tenantCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dispatch:{tenantCode}");
    }

    public async Task LeaveTenantGroup(string tenantCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dispatch:{tenantCode}");
    }

    public async Task JoinMachineGroup(string tenantCode, int machineId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dispatch:{tenantCode}:machine:{machineId}");
    }

    public async Task LeaveMachineGroup(string tenantCode, int machineId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dispatch:{tenantCode}:machine:{machineId}");
    }

    public async Task JoinOperatorGroup(string tenantCode, int operatorId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dispatch:{tenantCode}:operator:{operatorId}");
    }

    public async Task LeaveOperatorGroup(string tenantCode, int operatorId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dispatch:{tenantCode}:operator:{operatorId}");
    }

    public async Task JoinRoleGroup(string tenantCode, int roleId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dispatch:{tenantCode}:role:{roleId}");
    }

    public async Task LeaveRoleGroup(string tenantCode, int roleId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dispatch:{tenantCode}:role:{roleId}");
    }
}
