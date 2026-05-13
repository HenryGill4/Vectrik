using Vectrik.Models;

namespace Vectrik.Services;

/// <summary>
/// Generates and resolves PlateUnload dispatches for machines that have the
/// RequireOperatorPlateUnload scheduling rule enabled. Plates from completed
/// builds occupy the cooldown chamber until an operator removes them; this
/// service drives that handoff through the operator dispatch queue.
/// </summary>
public interface IPlateUnloadDispatchService
{
    /// <summary>
    /// Create a PlateUnload dispatch for a completed build. No-op (returns null)
    /// when the machine doesn't have the RequireOperatorPlateUnload rule enabled
    /// or when a dispatch already exists for this program.
    /// </summary>
    Task<SetupDispatch?> CreatePlateUnloadDispatchAsync(
        int machineId, int machineProgramId, int? predecessorDispatchId = null);

    /// <summary>
    /// Mark the plate physically removed. Completes the dispatch and stamps
    /// MachineProgram.PlateUnloadedAt so the slot finder treats the chamber slot
    /// as free.
    /// </summary>
    Task<SetupDispatch> CompletePlateUnloadAsync(int dispatchId, int operatorUserId);
}
