using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Hubs;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services.Platform;

namespace Vectrik.Services;

public class PrintCompletionService : IPrintCompletionService
{
    private readonly TenantDbContext _db;
    private readonly ISetupDispatchService _dispatchService;
    private readonly IDispatchNotifier _notifier;
    private readonly ITenantContext _tenantContext;

    public PrintCompletionService(
        TenantDbContext db,
        ISetupDispatchService dispatchService,
        IDispatchNotifier notifier,
        ITenantContext tenantContext)
    {
        _db = db;
        _dispatchService = dispatchService;
        _notifier = notifier;
        _tenantContext = tenantContext;
    }

    public async Task<SetupDispatch> CreatePrintCompletionDispatchAsync(int machineId, int machineProgramId)
    {
        var checklist = GetDefaultSlsInspectionChecklist();
        var checklistJson = JsonSerializer.Serialize(checklist);

        var dispatch = await _dispatchService.CreateManualDispatchAsync(
            machineId: machineId,
            type: DispatchType.Teardown,
            machineProgramId: machineProgramId,
            notes: "Print complete. Perform post-print inspection before releasing build.");

        // Set inspection checklist
        var entity = await _db.SetupDispatches.FindAsync(dispatch.Id);
        if (entity != null)
        {
            entity.InspectionChecklistJson = checklistJson;
            entity.Priority = 75;
            entity.PriorityReason = "Post-print inspection — build ready for review";
            await _db.SaveChangesAsync();
        }

        // Transition program to PostPrint
        var program = await _db.MachinePrograms.FindAsync(machineProgramId);
        if (program != null)
        {
            if (!program.PrintCompletedAt.HasValue)
                program.PrintCompletedAt = DateTime.UtcNow;
            program.ScheduleStatus = ProgramScheduleStatus.PostPrint;
            program.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return (await _dispatchService.GetByIdAsync(dispatch.Id))!;
    }

    public async Task<SetupDispatch> CompleteInspectionAsync(
        int dispatchId, string inspectionChecklistJson, int operatorUserId)
    {
        var dispatch = await _dispatchService.GetByIdAsync(dispatchId)
            ?? throw new InvalidOperationException($"Dispatch {dispatchId} not found.");

        if (dispatch.DispatchType != DispatchType.Teardown)
            throw new InvalidOperationException("This method is only for Teardown dispatches.");

        // Parse and validate inspection checklist
        var items = JsonSerializer.Deserialize<List<InspectionChecklistItem>>(inspectionChecklistJson)
            ?? throw new InvalidOperationException("Invalid inspection checklist JSON.");

        var uninspected = items.Where(i => i.Required && !i.Inspected).ToList();
        if (uninspected.Count > 0)
            throw new InvalidOperationException(
                $"Cannot complete: {uninspected.Count} required inspection item(s) not yet inspected.");

        var failed = items.Where(i => i.Required && i.Passed == false).ToList();

        // Save the updated checklist back to the dispatch
        var entity = await _db.SetupDispatches.FindAsync(dispatchId);
        if (entity != null)
        {
            entity.InspectionChecklistJson = inspectionChecklistJson;
            entity.LastModifiedDate = DateTime.UtcNow;
            entity.LastModifiedBy = operatorUserId.ToString();
            await _db.SaveChangesAsync();
        }

        if (failed.Count > 0)
        {
            // Block the dispatch — inspection failed
            if (entity != null)
            {
                entity.Status = DispatchStatus.Blocked;
                entity.Notes = string.IsNullOrEmpty(entity.Notes)
                    ? $"Inspection FAILED: {string.Join(", ", failed.Select(f => f.Title))}. Consider creating NCR."
                    : $"{entity.Notes}\nInspection FAILED: {string.Join(", ", failed.Select(f => f.Title))}. Consider creating NCR.";
                entity.LastModifiedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            var tenantCode = _tenantContext.TenantCode;
            if (!string.IsNullOrEmpty(tenantCode))
            {
                try
                {
                    await _notifier.SendUrgentDispatchAsync(tenantCode, dispatch.MachineId,
                        $"Inspection FAILED on {dispatch.Machine?.Name ?? "machine"}: {string.Join(", ", failed.Select(f => f.Title))}");
                }
                catch { /* SignalR failures shouldn't break operations */ }
            }

            return (await _dispatchService.GetByIdAsync(dispatchId))!;
        }

        // All passed — start and complete the dispatch
        if (dispatch.Status == DispatchStatus.Queued || dispatch.Status == DispatchStatus.Assigned)
            await _dispatchService.StartDispatchAsync(dispatchId, operatorUserId);

        var completed = await _dispatchService.CompleteDispatchAsync(dispatchId);

        // Transition MachineProgram to Completed
        if (completed.MachineProgramId.HasValue)
        {
            var program = await _db.MachinePrograms.FindAsync(completed.MachineProgramId.Value);
            if (program != null)
            {
                program.ScheduleStatus = ProgramScheduleStatus.Completed;
                program.LastModifiedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        return completed;
    }

    public List<InspectionChecklistItem> GetDefaultSlsInspectionChecklist()
    {
        return new List<InspectionChecklistItem>
        {
            new() { StepId = 1, Title = "Visual inspection — no obvious defects, warping, delamination", Required = true },
            new() { StepId = 2, Title = "Powder removal — build chamber and plate cleaned", Required = true },
            new() { StepId = 3, Title = "Part count — all parts present and accounted for", Required = true },
            new() { StepId = 4, Title = "Dimensional spot-check — key dimensions within tolerance", Required = true },
            new() { StepId = 5, Title = "Build plate condition — no damage, cracks, or excessive wear", Required = true },
            new() { StepId = 6, Title = "Photos uploaded — before/after plate removal", Required = false }
        };
    }
}
