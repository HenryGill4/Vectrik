using Microsoft.AspNetCore.SignalR;
using Vectrik.Models;

namespace Vectrik.Hubs;

public class DispatchNotifier : IDispatchNotifier
{
    private readonly IHubContext<DispatchHub> _hubContext;

    public DispatchNotifier(IHubContext<DispatchHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendDispatchCreatedAsync(string tenantCode, SetupDispatch dispatch)
    {
        var payload = new { dispatch.Id, dispatch.DispatchNumber, dispatch.MachineId, dispatch.Status, dispatch.DispatchType, dispatch.Priority };

        await _hubContext.Clients.Group($"dispatch:{tenantCode}")
            .SendAsync("DispatchCreated", payload);

        await _hubContext.Clients.Group($"dispatch:{tenantCode}:machine:{dispatch.MachineId}")
            .SendAsync("DispatchCreated", payload);

        if (dispatch.AssignedOperatorId.HasValue)
        {
            await _hubContext.Clients.Group($"dispatch:{tenantCode}:operator:{dispatch.AssignedOperatorId}")
                .SendAsync("DispatchCreated", payload);
        }
    }

    public async Task SendDispatchStatusChangedAsync(string tenantCode, SetupDispatch dispatch)
    {
        var payload = new { dispatch.Id, dispatch.DispatchNumber, dispatch.MachineId, dispatch.Status, dispatch.AssignedOperatorId, timestamp = DateTime.UtcNow };

        await _hubContext.Clients.Group($"dispatch:{tenantCode}")
            .SendAsync("DispatchStatusChanged", payload);

        await _hubContext.Clients.Group($"dispatch:{tenantCode}:machine:{dispatch.MachineId}")
            .SendAsync("DispatchStatusChanged", payload);

        if (dispatch.AssignedOperatorId.HasValue)
        {
            await _hubContext.Clients.Group($"dispatch:{tenantCode}:operator:{dispatch.AssignedOperatorId}")
                .SendAsync("DispatchStatusChanged", payload);
        }
    }

    public async Task SendQueueReprioritizedAsync(string tenantCode, int machineId)
    {
        await _hubContext.Clients.Group($"dispatch:{tenantCode}")
            .SendAsync("QueueReprioritized", new { machineId, timestamp = DateTime.UtcNow });

        await _hubContext.Clients.Group($"dispatch:{tenantCode}:machine:{machineId}")
            .SendAsync("QueueReprioritized", new { machineId, timestamp = DateTime.UtcNow });
    }

    public async Task SendUrgentDispatchAsync(string tenantCode, int machineId, string message)
    {
        await _hubContext.Clients.Group($"dispatch:{tenantCode}")
            .SendAsync("UrgentDispatch", new { machineId, message, timestamp = DateTime.UtcNow });
    }

    public async Task SendChangeoverCountdownAsync(string tenantCode, int machineId, int minutesRemaining)
    {
        await _hubContext.Clients.Group($"dispatch:{tenantCode}:machine:{machineId}")
            .SendAsync("ChangeoverCountdown", new { machineId, minutesRemaining, timestamp = DateTime.UtcNow });
    }
}
