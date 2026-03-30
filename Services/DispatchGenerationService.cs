using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Hubs;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services.Platform;

namespace Vectrik.Services;

public class DispatchGenerationService : IDispatchGenerationService
{
    private readonly TenantDbContext _db;
    private readonly ISetupDispatchService _dispatchService;
    private readonly IDispatchScoringService _scoringService;
    private readonly IDispatchLearningService _learningService;
    private readonly IDispatchNotifier _notifier;
    private readonly ITenantContext _tenantContext;

    public DispatchGenerationService(
        TenantDbContext db,
        ISetupDispatchService dispatchService,
        IDispatchScoringService scoringService,
        IDispatchLearningService learningService,
        IDispatchNotifier notifier,
        ITenantContext tenantContext)
    {
        _db = db;
        _dispatchService = dispatchService;
        _scoringService = scoringService;
        _learningService = learningService;
        _notifier = notifier;
        _tenantContext = tenantContext;
    }

    public async Task<List<SetupDispatch>> GenerateDispatchSuggestionsAsync(int? machineId = null)
    {
        var created = new List<SetupDispatch>();

        // Get machines with auto-dispatch enabled
        var machines = await _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling)
            .ToListAsync();

        if (machineId.HasValue)
            machines = machines.Where(m => m.Id == machineId.Value).ToList();

        foreach (var machine in machines)
        {
            var config = await GetConfigForMachineAsync(machine.Id);
            if (!config.AutoDispatchEnabled) continue;

            // Check queue depth
            var activeCount = await _db.SetupDispatches
                .CountAsync(d => d.MachineId == machine.Id
                    && d.Status != DispatchStatus.Completed
                    && d.Status != DispatchStatus.Cancelled);
            if (activeCount >= config.MaxQueueDepth) continue;

            // Find candidate stage executions
            var candidates = await _db.StageExecutions
                .Include(se => se.Job)
                    .ThenInclude(j => j!.WorkOrderLine)
                        .ThenInclude(wol => wol!.WorkOrder)
                .Include(se => se.MachineProgram)
                .Where(se => se.MachineId == machine.Id
                    && se.Status == StageExecutionStatus.NotStarted
                    && se.SetupDispatchId == null)
                .ToListAsync();

            if (candidates.Count == 0) continue;

            // Batch grouping: group by MachineProgram within window
            var groups = candidates
                .Where(se => se.MachineProgramId.HasValue)
                .GroupBy(se => se.MachineProgramId!.Value)
                .ToList();

            // Create temporary dispatches for scoring — detect tool mismatches
            var scoreCandidates = new List<SetupDispatch>();

            // Load current machine's tooling for changeover analysis
            List<ProgramToolingItem>? currentTools = null;
            if (machine.CurrentProgramId.HasValue)
            {
                currentTools = await _db.Set<ProgramToolingItem>()
                    .Where(t => t.MachineProgramId == machine.CurrentProgramId.Value && t.IsActive)
                    .ToListAsync();
            }

            foreach (var group in groups)
            {
                var firstExecution = group.First();
                var isChangeover = machine.CurrentProgramId.HasValue
                    && machine.CurrentProgramId.Value != group.Key;

                // Analyze tool changes if this is a changeover
                string? toolingRequired = null;
                double? changeoverMinutes = null;
                if (isChangeover && currentTools != null && currentTools.Count > 0)
                {
                    var targetTools = await _db.Set<ProgramToolingItem>()
                        .Where(t => t.MachineProgramId == group.Key && t.IsActive)
                        .ToListAsync();

                    if (targetTools.Count > 0)
                    {
                        var currentByPos = currentTools.ToDictionary(t => t.ToolPosition, t => t.Name);
                        var changes = new List<string>();
                        foreach (var target in targetTools)
                        {
                            if (!currentByPos.TryGetValue(target.ToolPosition, out var currentName))
                                changes.Add($"Add {target.ToolPosition}: {target.Name}");
                            else if (!currentName.Equals(target.Name, StringComparison.OrdinalIgnoreCase))
                                changes.Add($"Swap {target.ToolPosition}: {currentName} → {target.Name}");
                        }
                        // Check for tools to remove (in current but not in target)
                        var targetPositions = targetTools.Select(t => t.ToolPosition).ToHashSet();
                        foreach (var cur in currentTools.Where(t => !targetPositions.Contains(t.ToolPosition)))
                            changes.Add($"Remove {cur.ToolPosition}: {cur.Name}");

                        if (changes.Count > 0)
                        {
                            toolingRequired = string.Join("; ", changes);
                            changeoverMinutes = 5 + (changes.Count * 2.5); // ~2.5 min per tool change + 5 min base
                        }
                    }
                }

                var estSetup = changeoverMinutes
                    ?? firstExecution.MachineProgram?.ActualAverageSetupMinutes
                    ?? firstExecution.MachineProgram?.SetupTimeMinutes
                    ?? 30;

                var tempDispatch = new SetupDispatch
                {
                    MachineId = machine.Id,
                    MachineProgramId = group.Key,
                    StageExecutionId = firstExecution.Id,
                    JobId = firstExecution.JobId,
                    PartId = firstExecution.Job?.PartId,
                    DispatchType = isChangeover ? DispatchType.Changeover : DispatchType.Setup,
                    ChangeoverFromProgramId = isChangeover ? machine.CurrentProgramId : null,
                    ChangeoverToProgramId = isChangeover ? group.Key : null,
                    ToolingRequired = toolingRequired,
                    EstimatedSetupMinutes = estSetup
                };
                scoreCandidates.Add(tempDispatch);
            }

            if (scoreCandidates.Count == 0) continue;

            // Score and rank
            var ranked = await _scoringService.ScoreAndRankAsync(scoreCandidates);

            // Changeover optimization: prefer same program as current
            var sorted = ranked
                .OrderByDescending(r =>
                    r.Dispatch.MachineProgramId == machine.CurrentProgramId ? 1000 : 0)
                .ThenByDescending(r => r.Score.FinalScore)
                .ToList();

            var slotsToFill = config.MaxQueueDepth - activeCount;

            foreach (var (candidate, score) in sorted.Take(slotsToFill))
            {
                // Find the actual execution to link
                var execution = candidates.FirstOrDefault(se =>
                    se.MachineProgramId == candidate.MachineProgramId
                    && se.SetupDispatchId == null);
                if (execution == null) continue;

                var dispatch = await _dispatchService.CreateManualDispatchAsync(
                    machineId: machine.Id,
                    type: candidate.DispatchType,
                    machineProgramId: candidate.MachineProgramId,
                    stageExecutionId: execution.Id,
                    jobId: execution.JobId,
                    partId: execution.Job?.PartId,
                    estimatedSetupMinutes: candidate.EstimatedSetupMinutes,
                    notes: $"Auto-generated. Score: {score.FinalScore}");

                // Mark as auto-generated and set changeover/tooling fields
                var entity = await _db.SetupDispatches.FindAsync(dispatch.Id);
                if (entity != null)
                {
                    entity.IsAutoGenerated = true;
                    entity.ScoreBreakdownJson = score.ScoreBreakdownJson;
                    entity.PriorityReason = score.PriorityReason;
                    entity.ChangeoverFromProgramId = candidate.ChangeoverFromProgramId;
                    entity.ChangeoverToProgramId = candidate.ChangeoverToProgramId;
                    if (!string.IsNullOrEmpty(candidate.ToolingRequired))
                        entity.ToolingRequired = candidate.ToolingRequired;

                    if (config.RequiresSchedulerApproval)
                    {
                        entity.Status = DispatchStatus.Deferred;
                        entity.Notes = string.IsNullOrEmpty(entity.Notes)
                            ? "Awaiting scheduler approval"
                            : $"{entity.Notes}\nAwaiting scheduler approval";
                    }

                    await _db.SaveChangesAsync();
                }

                await _dispatchService.UpdateDispatchPriorityAsync(dispatch.Id, score.FinalScore,
                    score.PriorityReason, score.ScoreBreakdownJson);

                // Auto-assign best operator if configured
                if (config.AutoAssignOperator && entity?.Status != DispatchStatus.Deferred)
                {
                    var bestOperator = await _learningService.SuggestBestOperatorAsync(
                        machine.Id, candidate.MachineProgramId);
                    if (bestOperator.HasValue)
                    {
                        try { await _dispatchService.AssignOperatorAsync(dispatch.Id, bestOperator.Value); }
                        catch { /* Assignment failures shouldn't block generation */ }
                    }
                }

                created.Add(dispatch);
            }
        }

        return created;
    }

    public async Task<SetupDispatch> ApproveAutoDispatchAsync(int dispatchId, int approvedByUserId)
    {
        var dispatch = await _db.SetupDispatches.FindAsync(dispatchId)
            ?? throw new InvalidOperationException($"Dispatch {dispatchId} not found.");

        if (dispatch.Status != DispatchStatus.Deferred)
            throw new InvalidOperationException($"Cannot approve dispatch in {dispatch.Status} state. Must be Deferred.");

        dispatch.Status = DispatchStatus.Queued;
        dispatch.LastModifiedDate = DateTime.UtcNow;
        dispatch.LastModifiedBy = approvedByUserId.ToString();
        dispatch.Notes = string.IsNullOrEmpty(dispatch.Notes)
            ? $"Approved by user {approvedByUserId}"
            : $"{dispatch.Notes}\nApproved by user {approvedByUserId}";

        await _db.SaveChangesAsync();

        var tenantCode = _tenantContext.TenantCode;
        if (!string.IsNullOrEmpty(tenantCode))
        {
            try { await _notifier.SendDispatchStatusChangedAsync(tenantCode, dispatch); }
            catch { /* SignalR failures shouldn't break operations */ }
        }

        return dispatch;
    }

    public async Task<SetupDispatch> RejectAutoDispatchAsync(int dispatchId, int rejectedByUserId, string? reason = null)
    {
        return await _dispatchService.CancelDispatchAsync(dispatchId,
            reason ?? $"Rejected by user {rejectedByUserId}");
    }

    private async Task<DispatchConfiguration> GetConfigForMachineAsync(int machineId)
    {
        var config = await _db.DispatchConfigurations
            .FirstOrDefaultAsync(c => c.MachineId == machineId);

        config ??= await _db.DispatchConfigurations
            .FirstOrDefaultAsync(c => c.MachineId == null);

        return config ?? new DispatchConfiguration();
    }
}
