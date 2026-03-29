using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services.Platform;

namespace Vectrik.Services;

public class PrintStartDispatchService : IPrintStartDispatchService
{
    private readonly TenantDbContext _db;
    private readonly ISetupDispatchService _dispatchService;
    private readonly ITenantContext _tenantContext;

    public PrintStartDispatchService(
        TenantDbContext db,
        ISetupDispatchService dispatchService,
        ITenantContext tenantContext)
    {
        _db = db;
        _dispatchService = dispatchService;
        _tenantContext = tenantContext;
    }

    public async Task<SetupDispatch> CreatePrintStartDispatchAsync(
        int machineId, int machineProgramId, int? predecessorDispatchId = null)
    {
        var checklist = GetDefaultPrePrintChecklist();
        var checklistJson = JsonSerializer.Serialize(checklist);

        var dispatch = await _dispatchService.CreateManualDispatchAsync(
            machineId: machineId,
            type: DispatchType.PrintStart,
            machineProgramId: machineProgramId,
            notes: "Confirm pre-print checklist and start the print.");

        // Set checklist and predecessor link
        var entity = await _db.SetupDispatches.FindAsync(dispatch.Id);
        if (entity != null)
        {
            entity.PrePrintChecklistJson = checklistJson;
            entity.PredecessorDispatchId = predecessorDispatchId;
            entity.Priority = 80; // Print start is high priority once plate is loaded
            entity.PriorityReason = "Print start — plate loaded, ready to begin";
            await _db.SaveChangesAsync();
        }

        return (await _dispatchService.GetByIdAsync(dispatch.Id))!;
    }

    public async Task<SetupDispatch> CompletePrintStartAsync(int dispatchId, int operatorUserId)
    {
        var dispatch = await _dispatchService.GetByIdAsync(dispatchId)
            ?? throw new InvalidOperationException($"Dispatch {dispatchId} not found.");

        if (dispatch.DispatchType != DispatchType.PrintStart)
            throw new InvalidOperationException("This method is only for PrintStart dispatches.");

        // Validate pre-print checklist
        if (!string.IsNullOrEmpty(dispatch.PrePrintChecklistJson))
        {
            var items = JsonSerializer.Deserialize<List<SignOffChecklistItem>>(dispatch.PrePrintChecklistJson);
            var incomplete = items?.Where(i => i.Required && !i.SignedOff).ToList();
            if (incomplete?.Count > 0)
                throw new InvalidOperationException(
                    $"Cannot start print: {incomplete.Count} required checklist item(s) not signed off.");
        }

        // Start dispatch if not already started
        if (dispatch.Status == DispatchStatus.Queued || dispatch.Status == DispatchStatus.Assigned)
            await _dispatchService.StartDispatchAsync(dispatchId, operatorUserId);

        // Complete the dispatch
        var completed = await _dispatchService.CompleteDispatchAsync(dispatchId);

        // Update MachineProgram
        if (completed.MachineProgramId.HasValue)
        {
            var program = await _db.MachinePrograms.FindAsync(completed.MachineProgramId.Value);
            if (program != null)
            {
                program.PrintStartedAt = DateTime.UtcNow;
                program.ScheduleStatus = ProgramScheduleStatus.Printing;
                program.LastModifiedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        // Update machine state to Running
        var machine = await _db.Machines.FindAsync(completed.MachineId);
        if (machine != null)
        {
            machine.SetupState = MachineSetupState.Running;
            machine.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return completed;
    }

    public List<SignOffChecklistItem> GetDefaultPrePrintChecklist()
    {
        return new List<SignOffChecklistItem>
        {
            new() { StepId = 1, Title = "Build enclosure closed and sealed", Required = true },
            new() { StepId = 2, Title = "Powder level sufficient for build", Required = true },
            new() { StepId = 3, Title = "Inert gas flow verified", Required = true },
            new() { StepId = 4, Title = "Bed temperature at target", Required = true },
            new() { StepId = 5, Title = "Build file loaded and verified", Required = true }
        };
    }
}
