using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Hubs;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services.Platform;

namespace Vectrik.Services;

public class SetupDispatchService : ISetupDispatchService
{
    private readonly TenantDbContext _db;
    private readonly INumberSequenceService _numberSequence;
    private readonly IDispatchNotifier _notifier;
    private readonly ITenantContext _tenantContext;
    private readonly IServiceProvider _serviceProvider;

    public SetupDispatchService(
        TenantDbContext db,
        INumberSequenceService numberSequence,
        IDispatchNotifier notifier,
        ITenantContext tenantContext,
        IServiceProvider serviceProvider)
    {
        _db = db;
        _numberSequence = numberSequence;
        _notifier = notifier;
        _tenantContext = tenantContext;
        _serviceProvider = serviceProvider;
    }

    // ── CRUD & Lifecycle ─────────────────────────────────────

    public async Task<SetupDispatch> CreateManualDispatchAsync(
        int machineId,
        DispatchType type,
        int? machineProgramId = null,
        int? stageExecutionId = null,
        int? jobId = null,
        int? partId = null,
        int? requestedByUserId = null,
        double? estimatedSetupMinutes = null,
        string? notes = null)
    {
        var dispatchNumber = await _numberSequence.NextAsync("Dispatch");

        // Determine changeover info
        var machine = await _db.Machines.FindAsync(machineId);
        int? changeoverFromProgramId = machine?.CurrentProgramId;

        var dispatch = new SetupDispatch
        {
            DispatchNumber = dispatchNumber,
            MachineId = machineId,
            MachineProgramId = machineProgramId,
            StageExecutionId = stageExecutionId,
            JobId = jobId,
            PartId = partId,
            DispatchType = type,
            Status = DispatchStatus.Queued,
            RequestedByUserId = requestedByUserId,
            EstimatedSetupMinutes = estimatedSetupMinutes,
            ChangeoverFromProgramId = changeoverFromProgramId,
            ChangeoverToProgramId = machineProgramId,
            Notes = notes,
            QueuedAt = DateTime.UtcNow,
            CreatedBy = requestedByUserId?.ToString()
        };

        // Auto-populate estimated setup from program if not provided
        if (!estimatedSetupMinutes.HasValue && machineProgramId.HasValue)
        {
            var program = await _db.MachinePrograms.FindAsync(machineProgramId.Value);
            if (program != null)
            {
                dispatch.EstimatedSetupMinutes = program.ActualAverageSetupMinutes ?? program.SetupTimeMinutes;
                dispatch.ToolingRequired = program.ToolingRequired;
                dispatch.FixtureRequired = program.FixtureRequired;
                dispatch.WorkInstructionId = program.WorkInstructionId;
            }
        }

        // Determine if changeover
        if (changeoverFromProgramId.HasValue && machineProgramId.HasValue
            && changeoverFromProgramId != machineProgramId)
        {
            dispatch.DispatchType = DispatchType.Changeover;
        }

        _db.SetupDispatches.Add(dispatch);
        await _db.SaveChangesAsync();

        // Link to stage execution if provided
        if (stageExecutionId.HasValue)
        {
            var execution = await _db.StageExecutions.FindAsync(stageExecutionId.Value);
            if (execution != null)
            {
                execution.SetupDispatchId = dispatch.Id;
                await _db.SaveChangesAsync();
            }
        }

        await NotifyAsync(d => _notifier.SendDispatchCreatedAsync(d, dispatch));
        return dispatch;
    }

    public async Task<SetupDispatch> AssignOperatorAsync(int dispatchId, int operatorUserId)
    {
        var dispatch = await GetRequiredAsync(dispatchId);
        ValidateTransition(dispatch, DispatchStatus.Assigned);

        dispatch.AssignedOperatorId = operatorUserId;
        dispatch.AssignedAt = DateTime.UtcNow;
        dispatch.Status = DispatchStatus.Assigned;
        dispatch.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await NotifyAsync(d => _notifier.SendDispatchStatusChangedAsync(d, dispatch));
        return dispatch;
    }

    public async Task<SetupDispatch> StartDispatchAsync(int dispatchId, int? operatorUserId = null)
    {
        var dispatch = await GetRequiredAsync(dispatchId);
        ValidateTransition(dispatch, DispatchStatus.InProgress);

        if (operatorUserId.HasValue && !dispatch.AssignedOperatorId.HasValue)
        {
            dispatch.AssignedOperatorId = operatorUserId;
            dispatch.AssignedAt = DateTime.UtcNow;
        }

        dispatch.Status = DispatchStatus.InProgress;
        dispatch.StartedAt = DateTime.UtcNow;
        dispatch.LastModifiedDate = DateTime.UtcNow;

        // Update machine setup state
        var machine = await _db.Machines.FindAsync(dispatch.MachineId);
        if (machine != null)
        {
            machine.SetupState = dispatch.IsChangeover ? MachineSetupState.ChangingOver : MachineSetupState.AwaitingSetup;
            machine.LastModifiedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await NotifyAsync(d => _notifier.SendDispatchStatusChangedAsync(d, dispatch));
        return dispatch;
    }

    public async Task<SetupDispatch> RequestVerificationAsync(int dispatchId)
    {
        var dispatch = await GetRequiredAsync(dispatchId);
        ValidateTransition(dispatch, DispatchStatus.PendingVerification);

        dispatch.Status = DispatchStatus.PendingVerification;
        dispatch.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await NotifyAsync(d => _notifier.SendDispatchStatusChangedAsync(d, dispatch));
        return dispatch;
    }

    public async Task<SetupDispatch> VerifyDispatchAsync(int dispatchId, int verifiedByUserId)
    {
        var dispatch = await GetRequiredAsync(dispatchId);
        ValidateTransition(dispatch, DispatchStatus.Verified);

        dispatch.Status = DispatchStatus.Verified;
        dispatch.VerifiedAt = DateTime.UtcNow;
        dispatch.LastModifiedDate = DateTime.UtcNow;
        dispatch.LastModifiedBy = verifiedByUserId.ToString();

        await _db.SaveChangesAsync();
        await NotifyAsync(d => _notifier.SendDispatchStatusChangedAsync(d, dispatch));
        return dispatch;
    }

    public async Task<SetupDispatch> CompleteDispatchAsync(int dispatchId, double? actualSetupMinutes = null, double? actualChangeoverMinutes = null)
    {
        var dispatch = await GetRequiredAsync(dispatchId);
        // Allow completing from InProgress, PendingVerification, or Verified
        if (dispatch.Status is not (DispatchStatus.InProgress or DispatchStatus.PendingVerification or DispatchStatus.Verified))
            throw new InvalidOperationException($"Cannot complete dispatch in {dispatch.Status} state.");

        // Gate: validate inspection checklist if present
        if (!string.IsNullOrEmpty(dispatch.InspectionChecklistJson))
        {
            var items = JsonSerializer.Deserialize<List<InspectionChecklistItem>>(dispatch.InspectionChecklistJson);
            var incomplete = items?.Where(i => i.Required && !i.Inspected).ToList();
            if (incomplete?.Count > 0)
                throw new InvalidOperationException(
                    $"Cannot complete: {incomplete.Count} required inspection item(s) not yet completed.");
        }

        dispatch.Status = DispatchStatus.Completed;
        dispatch.CompletedAt = DateTime.UtcNow;
        dispatch.LastModifiedDate = DateTime.UtcNow;

        // Calculate actual duration if not provided
        if (actualSetupMinutes.HasValue)
            dispatch.ActualSetupMinutes = actualSetupMinutes;
        else if (dispatch.StartedAt.HasValue)
            dispatch.ActualSetupMinutes = (DateTime.UtcNow - dispatch.StartedAt.Value).TotalMinutes;

        if (actualChangeoverMinutes.HasValue)
            dispatch.ActualChangeoverMinutes = actualChangeoverMinutes;

        // Update machine state
        var machine = await _db.Machines.FindAsync(dispatch.MachineId);
        if (machine != null)
        {
            machine.CurrentProgramId = dispatch.MachineProgramId;
            machine.SetupState = MachineSetupState.SetUp;
            machine.LastSetupChangeAt = DateTime.UtcNow;
            machine.LastModifiedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Write history record
        await WriteHistoryAsync(dispatch);

        // Trigger EMA learning (resolve lazily to avoid circular dependency)
        try
        {
            var learningService = _serviceProvider.GetService<IDispatchLearningService>();
            if (learningService != null)
                await learningService.ProcessCompletedDispatchAsync(dispatchId);
        }
        catch { /* Learning failures shouldn't break dispatch completion */ }

        await NotifyAsync(d => _notifier.SendDispatchStatusChangedAsync(d, dispatch));
        return dispatch;
    }

    public async Task<SetupDispatch> CancelDispatchAsync(int dispatchId, string? reason = null)
    {
        var dispatch = await GetRequiredAsync(dispatchId);
        if (dispatch.Status is DispatchStatus.Completed or DispatchStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel dispatch in {dispatch.Status} state.");

        dispatch.Status = DispatchStatus.Cancelled;
        dispatch.LastModifiedDate = DateTime.UtcNow;
        if (reason != null)
            dispatch.Notes = string.IsNullOrEmpty(dispatch.Notes)
                ? $"Cancelled: {reason}"
                : $"{dispatch.Notes}\nCancelled: {reason}";

        // Unlink from stage execution
        if (dispatch.StageExecutionId.HasValue)
        {
            var execution = await _db.StageExecutions.FindAsync(dispatch.StageExecutionId.Value);
            if (execution?.SetupDispatchId == dispatch.Id)
                execution.SetupDispatchId = null;
        }

        await _db.SaveChangesAsync();
        await NotifyAsync(d => _notifier.SendDispatchStatusChangedAsync(d, dispatch));
        return dispatch;
    }

    public async Task<SetupDispatch> DeferDispatchAsync(int dispatchId, string? reason = null)
    {
        var dispatch = await GetRequiredAsync(dispatchId);
        if (dispatch.Status is DispatchStatus.Completed or DispatchStatus.Cancelled)
            throw new InvalidOperationException($"Cannot defer dispatch in {dispatch.Status} state.");

        dispatch.Status = DispatchStatus.Deferred;
        dispatch.LastModifiedDate = DateTime.UtcNow;
        if (reason != null)
            dispatch.Notes = string.IsNullOrEmpty(dispatch.Notes)
                ? $"Deferred: {reason}"
                : $"{dispatch.Notes}\nDeferred: {reason}";

        await _db.SaveChangesAsync();
        await NotifyAsync(d => _notifier.SendDispatchStatusChangedAsync(d, dispatch));
        return dispatch;
    }

    // ── Queries ──────────────────────────────────────────────

    public async Task<SetupDispatch?> GetByIdAsync(int dispatchId)
    {
        return await _db.SetupDispatches
            .Include(d => d.Machine)
            .Include(d => d.MachineProgram)
            .Include(d => d.Part)
            .Include(d => d.AssignedOperator)
            .Include(d => d.Job)
            .FirstOrDefaultAsync(d => d.Id == dispatchId);
    }

    public async Task<SetupDispatch?> GetByNumberAsync(string dispatchNumber)
    {
        return await _db.SetupDispatches
            .Include(d => d.Machine)
            .Include(d => d.MachineProgram)
            .Include(d => d.Part)
            .Include(d => d.AssignedOperator)
            .FirstOrDefaultAsync(d => d.DispatchNumber == dispatchNumber);
    }

    public async Task<List<SetupDispatch>> GetMachineQueueAsync(int machineId)
    {
        var dispatches = await _db.SetupDispatches
            .Include(d => d.MachineProgram)
            .Include(d => d.Part)
            .Include(d => d.AssignedOperator)
            .Include(d => d.Job)
            .Where(d => d.MachineId == machineId && d.Status != DispatchStatus.Completed
                && d.Status != DispatchStatus.Cancelled)
            .ToListAsync();

        return dispatches.OrderByDescending(d => d.Priority).ThenBy(d => d.QueuedAt).ToList();
    }

    public async Task<List<SetupDispatch>> GetOperatorQueueAsync(int operatorUserId)
    {
        var dispatches = await _db.SetupDispatches
            .Include(d => d.Machine)
            .Include(d => d.MachineProgram)
            .Include(d => d.Part)
            .Include(d => d.Job)
            .Where(d => d.AssignedOperatorId == operatorUserId
                && d.Status != DispatchStatus.Completed
                && d.Status != DispatchStatus.Cancelled)
            .ToListAsync();

        return dispatches.OrderByDescending(d => d.Priority).ThenBy(d => d.QueuedAt).ToList();
    }

    public async Task<List<SetupDispatch>> GetActiveDispatchesAsync()
    {
        var dispatches = await _db.SetupDispatches
            .Include(d => d.Machine)
            .Include(d => d.MachineProgram)
            .Include(d => d.Part)
            .Include(d => d.AssignedOperator)
            .Where(d => d.Status != DispatchStatus.Completed
                && d.Status != DispatchStatus.Cancelled)
            .ToListAsync();

        return dispatches.OrderByDescending(d => d.Priority).ThenBy(d => d.QueuedAt).ToList();
    }

    public async Task<DispatchDashboardData> GetDashboardDataAsync()
    {
        var machines = await _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling)
            .OrderBy(m => m.Name)
            .ToListAsync();

        var activeDispatches = await _db.SetupDispatches
            .Include(d => d.Machine)
            .Include(d => d.MachineProgram)
            .Include(d => d.Part)
            .Include(d => d.AssignedOperator)
            .Include(d => d.Job)
            .Where(d => d.Status != DispatchStatus.Completed && d.Status != DispatchStatus.Cancelled)
            .ToListAsync();

        var lanes = new List<MachineLaneData>();
        foreach (var machine in machines)
        {
            var machineDispatches = activeDispatches
                .Where(d => d.MachineId == machine.Id)
                .OrderByDescending(d => d.Priority)
                .ThenBy(d => d.QueuedAt)
                .ToList();

            lanes.Add(new MachineLaneData
            {
                Machine = machine,
                CurrentDispatch = machineDispatches.FirstOrDefault(d =>
                    d.Status is DispatchStatus.InProgress or DispatchStatus.PendingVerification),
                QueuedDispatches = machineDispatches
                    .Where(d => d.Status is DispatchStatus.Queued or DispatchStatus.Assigned)
                    .ToList()
            });
        }

        var unassigned = activeDispatches
            .Where(d => !d.AssignedOperatorId.HasValue && d.Status == DispatchStatus.Queued)
            .ToList();

        return new DispatchDashboardData
        {
            MachineLanes = lanes,
            UnassignedDispatches = unassigned,
            TotalActive = activeDispatches.Count,
            TotalQueued = activeDispatches.Count(d => d.Status == DispatchStatus.Queued),
            TotalInProgress = activeDispatches.Count(d => d.Status == DispatchStatus.InProgress)
        };
    }

    // ── Phase 2 Extensions ──────────────────────────────────

    public async Task<List<SetupDispatch>> GetActiveDispatchesByTypeAsync(DispatchType type)
    {
        var dispatches = await _db.SetupDispatches
            .Include(d => d.Machine)
            .Include(d => d.MachineProgram)
            .Include(d => d.Part)
            .Include(d => d.AssignedOperator)
            .Where(d => d.DispatchType == type
                && d.Status != DispatchStatus.Completed
                && d.Status != DispatchStatus.Cancelled)
            .ToListAsync();

        return dispatches.OrderByDescending(d => d.Priority).ThenBy(d => d.QueuedAt).ToList();
    }

    public async Task UpdateDispatchPriorityAsync(int dispatchId, int priority, string? reason = null, string? scoreBreakdownJson = null)
    {
        var dispatch = await GetRequiredAsync(dispatchId);
        dispatch.Priority = Math.Clamp(priority, 1, 100);
        if (reason != null) dispatch.PriorityReason = reason;
        if (scoreBreakdownJson != null) dispatch.ScoreBreakdownJson = scoreBreakdownJson;
        dispatch.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await NotifyAsync(d => _notifier.SendQueueReprioritizedAsync(d, dispatch.MachineId));
    }

    public async Task<List<SetupDispatch>> GetDispatchesByRoleAsync(int roleId)
    {
        var dispatches = await _db.SetupDispatches
            .Include(d => d.Machine)
            .Include(d => d.MachineProgram)
            .Include(d => d.Part)
            .Include(d => d.AssignedOperator)
            .Include(d => d.TargetRole)
            .Where(d => d.TargetRoleId == roleId
                && d.Status != DispatchStatus.Completed
                && d.Status != DispatchStatus.Cancelled)
            .ToListAsync();

        return dispatches.OrderByDescending(d => d.Priority).ThenBy(d => d.QueuedAt).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────

    private async Task<SetupDispatch> GetRequiredAsync(int dispatchId)
    {
        return await _db.SetupDispatches.FindAsync(dispatchId)
            ?? throw new InvalidOperationException($"Dispatch {dispatchId} not found.");
    }

    private static void ValidateTransition(SetupDispatch dispatch, DispatchStatus target)
    {
        var valid = target switch
        {
            DispatchStatus.Assigned => dispatch.Status is DispatchStatus.Queued,
            DispatchStatus.InProgress => dispatch.Status is DispatchStatus.Queued or DispatchStatus.Assigned,
            DispatchStatus.PendingVerification => dispatch.Status is DispatchStatus.InProgress,
            DispatchStatus.Verified => dispatch.Status is DispatchStatus.PendingVerification,
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException(
                $"Cannot transition dispatch from {dispatch.Status} to {target}.");
    }

    private async Task WriteHistoryAsync(SetupDispatch dispatch)
    {
        var history = new SetupHistory
        {
            SetupDispatchId = dispatch.Id,
            MachineId = dispatch.MachineId,
            MachineProgramId = dispatch.MachineProgramId,
            PartId = dispatch.PartId,
            OperatorUserId = dispatch.AssignedOperatorId,
            SetupDurationMinutes = dispatch.ActualSetupMinutes ?? 0,
            ChangeoverDurationMinutes = dispatch.ActualChangeoverMinutes,
            WasChangeover = dispatch.IsChangeover,
            PreviousProgramId = dispatch.ChangeoverFromProgramId,
            ToolingUsedJson = dispatch.ToolingRequired,
            CompletedAt = DateTime.UtcNow
        };

        _db.SetupHistories.Add(history);
        await _db.SaveChangesAsync();
    }

    private async Task NotifyAsync(Func<string, Task> action)
    {
        var tenantCode = _tenantContext.TenantCode;
        if (!string.IsNullOrEmpty(tenantCode))
        {
            try { await action(tenantCode); }
            catch { /* SignalR failures shouldn't break dispatch operations */ }
        }
    }
}
