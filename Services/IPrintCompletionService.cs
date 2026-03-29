using Vectrik.Models;

namespace Vectrik.Services;

public interface IPrintCompletionService
{
    /// <summary>
    /// Creates a Teardown dispatch with mandatory inspection checklist when print completes.
    /// </summary>
    Task<SetupDispatch> CreatePrintCompletionDispatchAsync(int machineId, int machineProgramId);

    /// <summary>
    /// Validates inspection checklist. If all required items pass, completes the dispatch
    /// and transitions MachineProgram to Completed. If any fail, blocks the dispatch.
    /// </summary>
    Task<SetupDispatch> CompleteInspectionAsync(int dispatchId, string inspectionChecklistJson, int operatorUserId);

    /// <summary>Returns the default 6-item SLS post-print inspection checklist.</summary>
    List<InspectionChecklistItem> GetDefaultSlsInspectionChecklist();
}
