using Vectrik.Models;
using Vectrik.Models.Maintenance;

namespace Vectrik.Services;

public interface IMaintenanceDispatchService
{
    /// <summary>
    /// Evaluates maintenance rules and auto-generates Maintenance-type dispatches
    /// for machines with imminent or overdue maintenance.
    /// </summary>
    Task<List<SetupDispatch>> GenerateMaintenanceDispatchesAsync();

    /// <summary>
    /// Checks if a specific machine has any active maintenance dispatches or
    /// blocking work orders that should prevent job starts.
    /// </summary>
    Task<MaintenanceBlockResult> CheckMaintenanceBlockAsync(int machineId);

    /// <summary>
    /// Checks if a program's tooling has any critical wear that should block dispatch.
    /// Returns tooling items that are at or above their warning threshold.
    /// </summary>
    Task<List<ToolingWearAlert>> CheckToolingWearAsync(int machineProgramId);

    /// <summary>
    /// Links a completed maintenance dispatch back to its work order
    /// and updates machine maintenance tracking fields.
    /// </summary>
    Task HandleMaintenanceCompletionAsync(int dispatchId);
}

public class MaintenanceBlockResult
{
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public List<MaintenanceAlert> ActiveAlerts { get; set; } = new();
    public List<MaintenanceWorkOrder> BlockingWorkOrders { get; set; } = new();
}

public class ToolingWearAlert
{
    public int ProgramToolingItemId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ToolPosition { get; set; } = string.Empty;
    public double WearPercent { get; set; }
    public int WarningThreshold { get; set; }
    public bool IsCritical { get; set; }
    public string? ComponentName { get; set; }
}
