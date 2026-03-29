using Vectrik.Models;

namespace Vectrik.Services;

public interface IPrintStartDispatchService
{
    /// <summary>
    /// Creates a PrintStart dispatch with pre-print checklist after BuildPlateLoad completes.
    /// </summary>
    Task<SetupDispatch> CreatePrintStartDispatchAsync(int machineId, int machineProgramId, int? predecessorDispatchId = null);

    /// <summary>
    /// Validates checklist, records PrintStartedAt, transitions ScheduleStatus to Printing.
    /// </summary>
    Task<SetupDispatch> CompletePrintStartAsync(int dispatchId, int operatorUserId);

    /// <summary>Returns the default 5-item pre-print checklist for SLS machines.</summary>
    List<SignOffChecklistItem> GetDefaultPrePrintChecklist();
}
