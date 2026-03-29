using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public interface ISetupDispatchService
{
    // ── CRUD & Lifecycle ─────────────────────────────────────

    Task<SetupDispatch> CreateManualDispatchAsync(
        int machineId,
        DispatchType type,
        int? machineProgramId = null,
        int? stageExecutionId = null,
        int? jobId = null,
        int? partId = null,
        int? requestedByUserId = null,
        double? estimatedSetupMinutes = null,
        string? notes = null);

    Task<SetupDispatch> AssignOperatorAsync(int dispatchId, int operatorUserId);
    Task<SetupDispatch> StartDispatchAsync(int dispatchId, int? operatorUserId = null);
    Task<SetupDispatch> RequestVerificationAsync(int dispatchId);
    Task<SetupDispatch> VerifyDispatchAsync(int dispatchId, int verifiedByUserId);
    Task<SetupDispatch> CompleteDispatchAsync(int dispatchId, double? actualSetupMinutes = null, double? actualChangeoverMinutes = null);
    Task<SetupDispatch> CancelDispatchAsync(int dispatchId, string? reason = null);
    Task<SetupDispatch> DeferDispatchAsync(int dispatchId, string? reason = null);

    // ── Queries ──────────────────────────────────────────────

    Task<SetupDispatch?> GetByIdAsync(int dispatchId);
    Task<SetupDispatch?> GetByNumberAsync(string dispatchNumber);
    Task<List<SetupDispatch>> GetMachineQueueAsync(int machineId);
    Task<List<SetupDispatch>> GetOperatorQueueAsync(int operatorUserId);
    Task<List<SetupDispatch>> GetActiveDispatchesAsync();
    Task<DispatchDashboardData> GetDashboardDataAsync();
}

/// <summary>DTO for the dispatch board dashboard.</summary>
public class DispatchDashboardData
{
    public List<MachineLaneData> MachineLanes { get; set; } = new();
    public List<SetupDispatch> UnassignedDispatches { get; set; } = new();
    public int TotalActive { get; set; }
    public int TotalQueued { get; set; }
    public int TotalInProgress { get; set; }
}

public class MachineLaneData
{
    public Machine Machine { get; set; } = null!;
    public SetupDispatch? CurrentDispatch { get; set; }
    public List<SetupDispatch> QueuedDispatches { get; set; } = new();
}
