using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class PlateUnloadDispatchService : IPlateUnloadDispatchService
{
    private readonly TenantDbContext _db;
    private readonly ISetupDispatchService _dispatchService;

    public PlateUnloadDispatchService(TenantDbContext db, ISetupDispatchService dispatchService)
    {
        _db = db;
        _dispatchService = dispatchService;
    }

    public async Task<SetupDispatch?> CreatePlateUnloadDispatchAsync(
        int machineId, int machineProgramId, int? predecessorDispatchId = null)
    {
        var ruleEnabled = await _db.MachineSchedulingRules.AnyAsync(r =>
            r.MachineId == machineId
            && r.RuleType == SchedulingRuleType.RequireOperatorPlateUnload
            && r.IsEnabled);
        if (!ruleEnabled) return null;

        // De-dupe: don't generate a second dispatch if one already exists for this
        // program and is still open (Queued / Assigned / InProgress).
        var existing = await _db.SetupDispatches.AnyAsync(d =>
            d.MachineProgramId == machineProgramId
            && d.DispatchType == DispatchType.PlateUnload
            && d.Status != DispatchStatus.Completed
            && d.Status != DispatchStatus.Verified
            && d.Status != DispatchStatus.Cancelled);
        if (existing) return null;

        var dispatch = await _dispatchService.CreateManualDispatchAsync(
            machineId: machineId,
            type: DispatchType.PlateUnload,
            machineProgramId: machineProgramId,
            notes: "Remove the finished plate from the cooldown chamber to free the slot.");

        var entity = await _db.SetupDispatches.FindAsync(dispatch.Id);
        if (entity != null)
        {
            entity.PredecessorDispatchId = predecessorDispatchId;
            entity.Priority = 85; // Higher than PrintStart — the next build is blocked until this clears.
            entity.PriorityReason = "Plate unload — chamber slot is occupied; next auto-changeover blocked until removed.";
            await _db.SaveChangesAsync();
        }

        return (await _dispatchService.GetByIdAsync(dispatch.Id))!;
    }

    public async Task<SetupDispatch> CompletePlateUnloadAsync(int dispatchId, int operatorUserId)
    {
        var dispatch = await _dispatchService.GetByIdAsync(dispatchId)
            ?? throw new InvalidOperationException($"Dispatch {dispatchId} not found.");

        if (dispatch.DispatchType != DispatchType.PlateUnload)
            throw new InvalidOperationException("This method is only for PlateUnload dispatches.");

        if (dispatch.Status == DispatchStatus.Queued || dispatch.Status == DispatchStatus.Assigned)
            await _dispatchService.StartDispatchAsync(dispatchId, operatorUserId);

        var completed = await _dispatchService.CompleteDispatchAsync(dispatchId);

        if (completed.MachineProgramId.HasValue)
        {
            var program = await _db.MachinePrograms.FindAsync(completed.MachineProgramId.Value);
            if (program != null)
            {
                program.PlateUnloadedAt = DateTime.UtcNow;
                program.LastModifiedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        return completed;
    }
}
