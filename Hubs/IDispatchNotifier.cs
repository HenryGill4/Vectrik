using Vectrik.Models;

namespace Vectrik.Hubs;

public interface IDispatchNotifier
{
    Task SendDispatchCreatedAsync(string tenantCode, SetupDispatch dispatch);
    Task SendDispatchStatusChangedAsync(string tenantCode, SetupDispatch dispatch);
    Task SendQueueReprioritizedAsync(string tenantCode, int machineId);
    Task SendUrgentDispatchAsync(string tenantCode, int machineId, string message);
    Task SendChangeoverCountdownAsync(string tenantCode, int machineId, int minutesRemaining);
}
