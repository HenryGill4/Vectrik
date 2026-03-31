using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public class SchedulingService : ISchedulingService
{
    private readonly TenantDbContext _db;
    private readonly IMachineProgramService _programService;
    private readonly IShiftManagementService _shiftService;
    private readonly ILogger<SchedulingService> _logger;

    public SchedulingService(TenantDbContext db, IMachineProgramService programService, IShiftManagementService shiftService, ILogger<SchedulingService> logger)
    {
        _db = db;
        _programService = programService;
        _shiftService = shiftService;
        _logger = logger;
    }

    public async Task AutoScheduleJobAsync(int jobId, DateTime? startAfter = null)
    {
        await AutoScheduleJobCoreAsync(jobId, startAfter, diagnostics: null);
    }

    public async Task<JobScheduleDiagnostic> AutoScheduleJobWithDiagnosticsAsync(int jobId, DateTime? startAfter = null)
    {
        var diag = new JobScheduleDiagnostic();
        diag = await AutoScheduleJobCoreAsync(jobId, startAfter, diag) ?? diag;
        return diag;
    }

    private async Task<JobScheduleDiagnostic?> AutoScheduleJobCoreAsync(int jobId, DateTime? startAfter, JobScheduleDiagnostic? diagnostics)
    {
        var job = await _db.Jobs
            .Include(j => j.Stages).ThenInclude(s => s.ProductionStage)
            .Include(j => j.Stages).ThenInclude(s => s.ProcessStage)
            .Include(j => j.Part)
            .FirstOrDefaultAsync(j => j.Id == jobId);

        if (job == null) throw new InvalidOperationException("Job not found.");

        if (diagnostics is not null)
        {
            diagnostics = diagnostics with
            {
                JobId = job.Id,
                JobNumber = job.JobNumber,
                Scope = job.Scope.ToString(),
                PartNumber = job.Part?.PartNumber ?? "Unknown"
            };
        }

        var executions = job.Stages.OrderBy(s => s.SortOrder).ToList();
        if (!executions.Any()) return diagnostics;

        if (diagnostics is not null)
            diagnostics = diagnostics with { ExecutionCount = executions.Count };

        var allMachines = await _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling)
            .ToListAsync();
        var machineNames = allMachines.ToDictionary(m => m.Id, m => m.Name ?? m.MachineId);

        // Load per-machine shift map (falls back to all active shifts for machines without assignments)
        var machineShiftMap = await _shiftService.GetMachineShiftMapAsync(allMachines.Select(m => m.Id));
        // Keep a global fallback for unassigned-machine scheduling
        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();

        // Load part stage requirements for machine preference resolution (legacy fallback)
        var routing = await _db.PartStageRequirements
            .Where(r => r.PartId == job.PartId && r.IsActive)
            .ToListAsync();

        // Resolve predecessor chain: if job has a predecessor, don't start before it ends
        var notBefore = startAfter ?? job.ScheduledStart;
        if (job.PredecessorJobId.HasValue)
        {
            var pred = await _db.Jobs.FindAsync(job.PredecessorJobId.Value);
            if (pred != null)
            {
                var predEnd = pred.ScheduledEnd;
                var gap = TimeSpan.FromHours(job.UpstreamGapHours ?? 0);
                var predConstraint = predEnd + gap;
                if (predConstraint > notBefore)
                    notBefore = predConstraint;
            }
        }

        // Recover ProcessStage references for legacy-created executions that have ProcessStageId = null
        // This allows re-scheduling old jobs to benefit from new-system machine routing
        if (job.ManufacturingProcessId.HasValue)
        {
            var processStageLookup = (await _db.ProcessStages
                .Where(ps => ps.ManufacturingProcessId == job.ManufacturingProcessId.Value)
                .ToListAsync())
                .GroupBy(ps => ps.ProductionStageId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var exec in executions.Where(e => e.ProcessStage == null && e.ProcessStageId == null))
            {
                if (processStageLookup.TryGetValue(exec.ProductionStageId, out var matchedStage))
                {
                    exec.ProcessStage = matchedStage; // in-memory only, not saved to DB
                }
            }
        }

        var cursor = notBefore;

        foreach (var exec in executions)
        {
            if (exec.ProductionStage == null) continue;

            // ── Resolve MachineProgramId and Duration from Program ──
            // Priority: ProcessStage.MachineProgramId → auto-select best program → stage defaults
            int? programIdToUse = exec.MachineProgramId ?? exec.ProcessStage?.MachineProgramId;
            double? programDurationHours = null;
            string? durationSource = null;

            // If no program linked, try to auto-select one for this part/stage
            if (!programIdToUse.HasValue && job.PartId > 0)
            {
                var bestProgram = await _programService.GetBestProgramForStageAsync(
                    job.PartId, machineId: null, exec.ProductionStageId);
                if (bestProgram != null)
                    programIdToUse = bestProgram.Id;
            }

            // Query program duration if we have a program
            if (programIdToUse.HasValue)
            {
                var programDuration = await _programService.GetDurationFromProgramAsync(
                    programIdToUse.Value, job.Quantity);

                if (programDuration != null)
                {
                    programDurationHours = programDuration.TotalMinutes / 60.0;
                    durationSource = programDuration.Source;

                    // Link program to execution for tracking and learning
                    exec.MachineProgramId = programIdToUse;
                }
            }

            // Calculate duration: program duration → existing EstimatedHours → stage default
            var duration = programDurationHours ?? exec.EstimatedHours ?? exec.ProductionStage.DefaultDurationHours;
            var setupHours = exec.SetupHours ?? 0;
            var totalDuration = duration + setupHours;

            // Find the matching PartStageRequirement for machine preferences (legacy fallback)
            var requirement = routing.FirstOrDefault(r => r.ProductionStageId == exec.ProductionStageId);

            // Get capable machines, ordered by preference (ProcessStage takes priority over PartStageRequirement)
            var capableMachines = ResolveMachines(exec.ProductionStage, requirement, exec.ProcessStage, allMachines);

            var execDiag = diagnostics is not null ? new ExecutionScheduleDiagnostic
            {
                ExecutionId = exec.Id,
                StageName = exec.ProductionStage.Name,
                SortOrder = exec.SortOrder,
                TotalDurationHours = totalDuration,
                CursorBefore = cursor,
                CandidateMachines = capableMachines.Select(m => m.Name ?? m.MachineId).ToList(),
                MachineResolutionPath = DescribeMachineResolutionPath(exec.ProcessStage, requirement, exec.ProductionStage, capableMachines)
            } : null;

            if (!capableMachines.Any())
            {
                // No machine available — schedule without machine assignment
                var slotStart = ShiftTimeHelper.SnapToNextShiftStart(cursor, shifts);
                var slotEnd = ShiftTimeHelper.AdvanceByWorkHours(slotStart, totalDuration, shifts);
                exec.ScheduledStartAt = slotStart;
                exec.ScheduledEndAt = slotEnd;
                exec.MachineId = null;
                exec.LastModifiedDate = DateTime.UtcNow;

                if (execDiag is not null)
                {
                    execDiag = execDiag with
                    {
                        ScheduledStart = slotStart,
                        ScheduledEnd = slotEnd,
                        CursorAfter = slotEnd,
                        Issues = ["No capable machines found — scheduled as Unassigned"]
                    };
                    diagnostics!.Executions.Add(execDiag);
                }

                cursor = slotEnd;
                continue;
            }

            // Try each capable machine — pick the one that finishes earliest.
            // For Part-level stages with setup changeover, prefer machines already set up for this part.
            ScheduleSlot? bestSlot = null;
            var setupChangeover = exec.ProcessStage?.SetupChangeoverMinutes ?? 0;

            foreach (var machine in capableMachines)
            {
                var mShifts = machineShiftMap.GetValueOrDefault(machine.Id, shifts);
                var effectiveDuration = totalDuration;

                // CNC setup affinity: check if this machine is already set up for this part
                if (setupChangeover > 0 && exec.ProcessStage?.ProcessingLevel == ProcessingLevel.Part)
                {
                    var lastPartOnMachine = await _db.StageExecutions
                        .Where(e => e.MachineId == machine.Id && e.Job != null
                            && e.Status != StageExecutionStatus.Failed
                            && e.ScheduledEndAt != null)
                        .OrderByDescending(e => e.ScheduledEndAt)
                        .Select(e => e.Job!.PartId)
                        .FirstOrDefaultAsync();

                    if (lastPartOnMachine > 0 && lastPartOnMachine != job.PartId)
                        effectiveDuration += setupChangeover / 60.0; // Convert minutes to hours
                }

                var slot = await FindEarliestSlotOnMachine(machine.Id, effectiveDuration, cursor, mShifts);
                if (bestSlot == null || slot.End < bestSlot.End)
                {
                    bestSlot = slot;
                }
            }

            exec.ScheduledStartAt = bestSlot!.Start;
            exec.ScheduledEndAt = bestSlot.End;
            exec.MachineId = bestSlot.MachineId;
            exec.LastModifiedDate = DateTime.UtcNow;

            if (execDiag is not null)
            {
                execDiag = execDiag with
                {
                    AssignedMachineId = bestSlot.MachineId,
                    AssignedMachineName = machineNames.GetValueOrDefault(bestSlot.MachineId, "Unknown"),
                    ScheduledStart = bestSlot.Start,
                    ScheduledEnd = bestSlot.End,
                    CursorAfter = bestSlot.End
                };
                diagnostics!.Executions.Add(execDiag);
            }

            // Next stage can't start before this one ends (sequential within job)
            cursor = bestSlot.End;
        }

        // Update job's overall scheduled window
        var firstStart = executions.Where(e => e.ScheduledStartAt.HasValue).MinBy(e => e.ScheduledStartAt)?.ScheduledStartAt;
        var lastEnd = executions.Where(e => e.ScheduledEndAt.HasValue).MaxBy(e => e.ScheduledEndAt)?.ScheduledEndAt;

        if (firstStart.HasValue) job.ScheduledStart = firstStart.Value;
        if (lastEnd.HasValue)
        {
            job.ScheduledEnd = lastEnd.Value;
            job.EstimatedHours = (job.ScheduledEnd - job.ScheduledStart).TotalHours;
        }

        job.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return diagnostics;
    }

    public async Task<StageExecution> AutoScheduleExecutionAsync(int executionId, DateTime? startAfter = null)
    {
        var exec = await _db.StageExecutions
            .Include(e => e.ProductionStage)
            .Include(e => e.ProcessStage)
            .Include(e => e.Job).ThenInclude(j => j!.Part)
            .FirstOrDefaultAsync(e => e.Id == executionId);

        if (exec == null) throw new InvalidOperationException("Stage execution not found.");

        var allMachines = await _db.Machines.Where(m => m.IsActive && m.IsAvailableForScheduling).ToListAsync();
        var machineShiftMap = await _shiftService.GetMachineShiftMapAsync(allMachines.Select(m => m.Id));
        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();

        var requirement = exec.Job != null
            ? await _db.PartStageRequirements.FirstOrDefaultAsync(r =>
                r.PartId == exec.Job.PartId && r.ProductionStageId == exec.ProductionStageId && r.IsActive)
            : null;

        // Recover ProcessStage for legacy-created execution missing ProcessStageId
        if (exec.ProcessStage == null && exec.ProcessStageId == null && exec.Job?.ManufacturingProcessId.HasValue == true)
        {
            exec.ProcessStage = await _db.ProcessStages
                .FirstOrDefaultAsync(ps =>
                    ps.ManufacturingProcessId == exec.Job.ManufacturingProcessId.Value
                    && ps.ProductionStageId == exec.ProductionStageId);
        }

        var notBefore = startAfter ?? DateTime.UtcNow;

        // Check if there's a previous stage in the same job that must finish first
        if (exec.JobId.HasValue)
        {
            var prevExec = await _db.StageExecutions
                .Where(e => e.JobId == exec.JobId && e.SortOrder < exec.SortOrder)
                .OrderByDescending(e => e.SortOrder)
                .FirstOrDefaultAsync();
            if (prevExec?.ScheduledEndAt > notBefore)
                notBefore = prevExec.ScheduledEndAt.Value;
        }

        // ── Resolve MachineProgramId and Duration from Program ──
        int? programIdToUse = exec.MachineProgramId ?? exec.ProcessStage?.MachineProgramId;
        double? programDurationHours = null;

        // If no program linked and we have a part, try to auto-select one
        if (!programIdToUse.HasValue && exec.Job?.PartId > 0)
        {
            var bestProgram = await _programService.GetBestProgramForStageAsync(
                exec.Job.PartId, machineId: null, exec.ProductionStageId);
            if (bestProgram != null)
                programIdToUse = bestProgram.Id;
        }

        // Query program duration if we have a program
        if (programIdToUse.HasValue)
        {
            var quantity = exec.Job?.Quantity ?? 1;
            var programDuration = await _programService.GetDurationFromProgramAsync(
                programIdToUse.Value, quantity);

            if (programDuration != null)
            {
                programDurationHours = programDuration.TotalMinutes / 60.0;
                exec.MachineProgramId = programIdToUse;
            }
        }

        var duration = programDurationHours ?? exec.EstimatedHours ?? exec.ProductionStage?.DefaultDurationHours ?? 1.0;
        var setupHours = exec.SetupHours ?? 0;
        var totalDuration = duration + setupHours;

        if (exec.ProductionStage == null)
            return exec;

        var capableMachines = ResolveMachines(exec.ProductionStage, requirement, exec.ProcessStage, allMachines);

        if (capableMachines.Any())
        {
            ScheduleSlot? bestSlot = null;
            foreach (var machine in capableMachines)
            {
                var mShifts = machineShiftMap.GetValueOrDefault(machine.Id, shifts);
                var slot = await FindEarliestSlotOnMachine(machine.Id, totalDuration, notBefore, mShifts);
                if (bestSlot == null || slot.End < bestSlot.End)
                    bestSlot = slot;
            }

            exec.ScheduledStartAt = bestSlot!.Start;
            exec.ScheduledEndAt = bestSlot.End;
            exec.MachineId = bestSlot.MachineId;
        }
        else
        {
            var slotStart = ShiftTimeHelper.SnapToNextShiftStart(notBefore, shifts);
            exec.ScheduledStartAt = slotStart;
            exec.ScheduledEndAt = ShiftTimeHelper.AdvanceByWorkHours(slotStart, totalDuration, shifts);
        }

        exec.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return exec;
    }

    public async Task<ScheduleSlot> FindEarliestSlotAsync(int machineId, double durationHours, DateTime notBefore)
    {
        var shifts = await _shiftService.GetEffectiveShiftsForMachineAsync(machineId);
        return await FindEarliestSlotOnMachine(machineId, durationHours, notBefore, shifts);
    }

    public async Task<List<Machine>> GetCapableMachinesAsync(int productionStageId, PartStageRequirement? requirement = null)
    {
        var stage = await _db.ProductionStages.FindAsync(productionStageId);
        if (stage == null) return new List<Machine>();

        var allMachines = await _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling)
            .ToListAsync();

        return ResolveMachines(stage, requirement, processStage: null, allMachines);
    }

    public async Task<int> AutoScheduleAllAsync()
    {
        // Find all unscheduled or machine-less executions
        var unscheduled = await _db.StageExecutions
            .Include(e => e.Job)
            .Include(e => e.ProductionStage)
            .Where(e => e.Status == StageExecutionStatus.NotStarted
                && (e.ScheduledStartAt == null || e.MachineId == null))
            .ToListAsync();

        // Group by job and schedule job-by-job, ordered by priority
        var jobIds = unscheduled
            .Where(e => e.JobId.HasValue)
            .Select(e => e.JobId!.Value)
            .Distinct()
            .ToList();

        var jobs = await _db.Jobs
            .Where(j => jobIds.Contains(j.Id))
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.ScheduledStart)
            .ToListAsync();

        int count = 0;
        foreach (var job in jobs)
        {
            try
            {
                await AutoScheduleJobAsync(job.Id);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Job {JobId} could not be auto-scheduled; skipping", job.Id);
            }
        }

        return count;
    }

    public async Task<int> RescheduleAllPendingAsync()
    {
        // Clear schedules for all not-started executions so AutoScheduleAll picks them up
        var pending = await _db.StageExecutions
            .Where(e => e.Status == StageExecutionStatus.NotStarted
                && e.ScheduledStartAt != null)
            .ToListAsync();

        foreach (var exec in pending)
        {
            exec.ScheduledStartAt = null;
            exec.ScheduledEndAt = null;
        }

        await _db.SaveChangesAsync();

        // Re-schedule everything with current shift definitions
        return await AutoScheduleAllAsync();
    }

    // ── Private Helpers ─────────────────────────────────────────

    private async Task<ScheduleSlot> FindEarliestSlotOnMachine(
        int machineId, double durationHours, DateTime notBefore, List<OperatingShift> shifts)
    {
        // Get all existing scheduled work on this machine that hasn't completed
        var existing = await _db.StageExecutions
            .Where(e => e.MachineId == machineId
                && e.ScheduledStartAt != null
                && e.ScheduledEndAt != null
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed
                && e.ScheduledEndAt > notBefore)
            .OrderBy(e => e.ScheduledStartAt)
            .Select(e => new { e.ScheduledStartAt, e.ScheduledEndAt })
            .ToListAsync();

        // Snap to next shift start
        var candidate = ShiftTimeHelper.SnapToNextShiftStart(notBefore, shifts);
        var candidateEnd = ShiftTimeHelper.AdvanceByWorkHours(candidate, durationHours, shifts);

        foreach (var block in existing)
        {
            var blockStart = block.ScheduledStartAt!.Value;
            var blockEnd = block.ScheduledEndAt!.Value;

            if (candidateEnd <= blockStart)
                break;

            candidate = ShiftTimeHelper.SnapToNextShiftStart(blockEnd, shifts);
            candidateEnd = ShiftTimeHelper.AdvanceByWorkHours(candidate, durationHours, shifts);
        }

        return new ScheduleSlot(candidate, candidateEnd, machineId);
    }

    private List<Machine> ResolveMachines(
        ProductionStage stage, PartStageRequirement? requirement, ProcessStage? processStage, List<Machine> allMachines)
    {
        var machineLookup = allMachines.ToDictionary(m => m.MachineId, m => m);
        var machineIdLookup = allMachines.ToDictionary(m => m.Id, m => m);
        var result = new List<Machine>();

        // 1. ProcessStage specific assignment (highest priority — new system)
        if (processStage != null && processStage.AssignedMachineId.HasValue
            && processStage.RequiresSpecificMachine)
        {
            if (machineIdLookup.TryGetValue(processStage.AssignedMachineId.Value, out var specific))
                return new List<Machine> { specific };
        }

        // 2. ProcessStage preferred machines
        if (processStage != null && !string.IsNullOrEmpty(processStage.PreferredMachineIds))
        {
            var preferredIds = processStage.PreferredMachineIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pid in preferredIds)
            {
                if (int.TryParse(pid, out var intId) && machineIdLookup.TryGetValue(intId, out var m))
                    result.Add(m);
            }
        }

        // 3. ProcessStage assigned machine (non-required)
        if (processStage != null && processStage.AssignedMachineId.HasValue
            && !processStage.RequiresSpecificMachine)
        {
            if (machineIdLookup.TryGetValue(processStage.AssignedMachineId.Value, out var m) && !result.Contains(m))
                result.Insert(0, m);
        }

        // 4. Specific assignment from PartStageRequirement (legacy fallback)
        if (!result.Any() && requirement != null && !string.IsNullOrEmpty(requirement.AssignedMachineId)
            && requirement.RequiresSpecificMachine)
        {
            if (machineLookup.TryGetValue(requirement.AssignedMachineId, out var specific))
                return new List<Machine> { specific };
        }

        // 5. Preferred machines from PartStageRequirement (legacy fallback)
        if (!result.Any() && requirement != null && !string.IsNullOrEmpty(requirement.PreferredMachineIds))
        {
            var preferredIds = requirement.PreferredMachineIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pid in preferredIds)
            {
                if (machineLookup.TryGetValue(pid, out var m))
                    result.Add(m);
            }
        }

        // 6. Assigned machines from the ProductionStage definition
        var stageCapableIds = stage.GetAssignedMachineIntIds();
        if (stageCapableIds.Any())
        {
            foreach (var intId in stageCapableIds)
            {
                if (machineIdLookup.TryGetValue(intId, out var m) && !result.Contains(m))
                    result.Add(m);
            }
        }

        // 7. Default machine for ProductionStage
        if (!string.IsNullOrEmpty(stage.DefaultMachineId)
            && machineLookup.TryGetValue(stage.DefaultMachineId, out var def) && !result.Contains(def))
        {
            result.Insert(0, def); // high priority
        }

        // Additive machines (SLS/Additive) should only receive build-level work;
        // exclude them from fallback candidates for batch/part-level stages.
        // When processStage is null (unknown context), include all machines.
        var isNonBuildLevel = processStage is not null
            && processStage.ProcessingLevel != ProcessingLevel.Build;
        var fallbackMachines = isNonBuildLevel
            ? allMachines.Where(m => !m.IsAdditiveMachine).ToList()
            : allMachines;

        // 8. If stage requires assignment and nothing found, restrict to assigned machines only
        if (!result.Any() && stage.RequiresMachineAssignment)
        {
            var capableIntIds = stage.GetAssignedMachineIntIds();
            if (capableIntIds.Count > 0)
            {
                result.AddRange(fallbackMachines
                    .Where(m => capableIntIds.Contains(m.Id))
                    .OrderBy(m => m.Priority));
            }
        }

        // 9. If still nothing — return empty so execution is scheduled as "Unassigned".
        // This prevents wrong-machine assignments (e.g., Laser Engraving on Depowder).
        // Operators or admins assign machines manually, or configure the stage properly.

        return result;
    }

    /// <summary>
    /// Describes which resolution step produced the machine list, for diagnostics.
    /// </summary>
    private static string DescribeMachineResolutionPath(
        ProcessStage? processStage, PartStageRequirement? requirement, ProductionStage stage, List<Machine> resolved)
    {
        if (!resolved.Any())
            return "No machines resolved (steps 1-8 all empty)";

        var steps = new List<string>();
        if (processStage is { AssignedMachineId: not null, RequiresSpecificMachine: true })
            steps.Add("Step1:ProcessStage-Required");
        if (processStage is not null && !string.IsNullOrEmpty(processStage.PreferredMachineIds))
            steps.Add("Step2:ProcessStage-Preferred");
        if (processStage is { AssignedMachineId: not null, RequiresSpecificMachine: false })
            steps.Add("Step3:ProcessStage-Assigned");
        if (requirement is { RequiresSpecificMachine: true } && !string.IsNullOrEmpty(requirement.AssignedMachineId))
            steps.Add("Step4:Legacy-Required");
        if (requirement is not null && !string.IsNullOrEmpty(requirement.PreferredMachineIds))
            steps.Add("Step5:Legacy-Preferred");
        if (stage.GetAssignedMachineIntIds().Count > 0)
            steps.Add("Step6:ProductionStage-Assigned");
        if (!string.IsNullOrEmpty(stage.DefaultMachineId))
            steps.Add("Step7:ProductionStage-Default");
        if (stage.RequiresMachineAssignment)
            steps.Add("Step8:RequiresAssignment-Fallback");

        return steps.Count > 0
            ? string.Join(" → ", steps)
            : "Resolved via unknown path";
    }

    // Shift time helpers are now in ShiftTimeHelper.cs (shared with ProgramSchedulingService)

    /// <inheritdoc />
    public async Task<ScheduleClearResult> ClearAllScheduleDataAsync()
    {
        // 1. Clear scheduling fields on all non-completed stage executions
        var executions = await _db.StageExecutions
            .Where(se => se.Status != StageExecutionStatus.Completed
                      && se.Status != StageExecutionStatus.Failed)
            .ToListAsync();

        foreach (var se in executions)
        {
            se.ScheduledStartAt = null;
            se.ScheduledEndAt = null;
            se.MachineId = null;
            se.Status = StageExecutionStatus.NotStarted;
        }

        // 2. Reset jobs that haven't actually started
        var jobs = await _db.Jobs
            .Where(j => j.Status == JobStatus.Scheduled || j.Status == JobStatus.Draft)
            .ToListAsync();

        foreach (var job in jobs)
        {
            job.ScheduledStart = default;
            job.ScheduledEnd = default;
            job.Status = JobStatus.Draft;
        }

        // 3. Unlock scheduled programs so they can be re-scheduled
        var lockedPrograms = await _db.MachinePrograms
            .Where(p => p.IsLocked
                && p.ScheduleStatus != ProgramScheduleStatus.Completed
                && p.ScheduleStatus != ProgramScheduleStatus.Printing)
            .ToListAsync();

        foreach (var prog in lockedPrograms)
        {
            prog.IsLocked = false;
            prog.ScheduleStatus = ProgramScheduleStatus.Ready;
            prog.ScheduledDate = null;
            prog.ScheduledJobId = null;
            prog.PredecessorProgramId = null;
        }

        await _db.SaveChangesAsync();

        return new ScheduleClearResult(executions.Count, jobs.Count, lockedPrograms.Count);
    }

    /// <inheritdoc />
    public async Task<DataDeleteResult> DeleteAllSchedulingDataAsync()
    {
        // Delete in FK-safe order: children first, parents last

        // 1. Stage executions (references Jobs and MachinePrograms)
        var execCount = await _db.StageExecutions.CountAsync();
        _db.StageExecutions.RemoveRange(_db.StageExecutions);
        await _db.SaveChangesAsync();

        // 2. Part instance stage logs, then part instances
        _db.PartInstanceStageLogs.RemoveRange(_db.PartInstanceStageLogs);
        var instanceCount = await _db.PartInstances.CountAsync();
        _db.PartInstances.RemoveRange(_db.PartInstances);
        await _db.SaveChangesAsync();

        // 3. Batch assignments, then batches
        _db.BatchPartAssignments.RemoveRange(_db.BatchPartAssignments);
        var batchCount = await _db.ProductionBatches.CountAsync();
        _db.ProductionBatches.RemoveRange(_db.ProductionBatches);
        await _db.SaveChangesAsync();

        // 4. Jobs (references Parts — keep parts)
        var jobCount = await _db.Jobs.CountAsync();
        _db.JobNotes.RemoveRange(_db.JobNotes);
        _db.Jobs.RemoveRange(_db.Jobs);
        await _db.SaveChangesAsync();

        // 5. Machine Programs and all children (cascade covers ProgramParts,
        //    Files, ToolingItems, Feedbacks but we clear join tables explicitly)
        _db.MachineProgramAssignments.RemoveRange(_db.MachineProgramAssignments);
        _db.ProgramRevisions.RemoveRange(_db.ProgramRevisions);
        _db.ProgramFeedbacks.RemoveRange(_db.ProgramFeedbacks);
        _db.ProgramToolingItems.RemoveRange(_db.ProgramToolingItems);
        _db.MachineProgramFiles.RemoveRange(_db.MachineProgramFiles);
        _db.ProgramParts.RemoveRange(_db.ProgramParts);
        var programCount = await _db.MachinePrograms.CountAsync();
        _db.MachinePrograms.RemoveRange(_db.MachinePrograms);
        await _db.SaveChangesAsync();

        // 6. Clear any ProcessStage FK references to deleted programs
        //    and re-flag stages that have machine assignments as needing program setup
        var linkedStages = await _db.ProcessStages
            .Where(s => s.MachineProgramId != null)
            .ToListAsync();
        foreach (var s in linkedStages)
        {
            s.MachineProgramId = null;
            // Re-flag for program setup if the stage has machines assigned
            if (!string.IsNullOrWhiteSpace(s.PreferredMachineIds) || s.AssignedMachineId.HasValue)
                s.ProgramSetupRequired = true;
        }
        await _db.SaveChangesAsync();

        return new DataDeleteResult(execCount, jobCount, programCount, instanceCount, batchCount);
    }

    /// <inheritdoc />
    public async Task<DataDeleteResult> DeleteAllSchedulingAndWorkOrderDataAsync()
    {
        // First delete all scheduling data (FK-safe order)
        var baseResult = await DeleteAllSchedulingDataAsync();

        // Then delete work order children and work orders
        _db.WorkOrderComments.RemoveRange(_db.WorkOrderComments);
        await _db.SaveChangesAsync();

        // WorkOrderLines reference Jobs (already deleted above)
        var woCount = await _db.WorkOrders.CountAsync();
        _db.WorkOrderLines.RemoveRange(_db.WorkOrderLines);
        _db.WorkOrders.RemoveRange(_db.WorkOrders);
        await _db.SaveChangesAsync();

        return baseResult with { WorkOrdersDeleted = woCount };
    }

    public async Task<DatabaseStats> GetDatabaseStatsAsync()
    {
        return new DatabaseStats(
            Machines: await _db.Machines.CountAsync(),
            ProductionStages: await _db.ProductionStages.CountAsync(),
            ManufacturingProcesses: await _db.ManufacturingProcesses.CountAsync(),
            Parts: await _db.Parts.CountAsync(),
            Jobs: await _db.Jobs.CountAsync(),
            StageExecutions: await _db.StageExecutions.CountAsync(),
            MachinePrograms: await _db.MachinePrograms.CountAsync(),
            ProductionBatches: await _db.ProductionBatches.CountAsync(),
            WorkOrders: await _db.WorkOrders.CountAsync());
    }
}
