using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class SchedulingService : ISchedulingService
{
    private readonly TenantDbContext _db;

    public SchedulingService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task AutoScheduleJobAsync(int jobId, DateTime? startAfter = null)
    {
        var job = await _db.Jobs
            .Include(j => j.Stages).ThenInclude(s => s.ProductionStage)
            .Include(j => j.Stages).ThenInclude(s => s.ProcessStage)
            .Include(j => j.Part)
            .FirstOrDefaultAsync(j => j.Id == jobId);

        if (job == null) throw new InvalidOperationException("Job not found.");

        var executions = job.Stages.OrderBy(s => s.SortOrder).ToList();
        if (!executions.Any()) return;

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        var allMachines = await _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling)
            .ToListAsync();

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

        var cursor = notBefore;

        foreach (var exec in executions)
        {
            if (exec.ProductionStage == null) continue;

            var duration = exec.EstimatedHours ?? exec.ProductionStage.DefaultDurationHours;
            var setupHours = exec.SetupHours ?? 0;
            var totalDuration = duration + setupHours;

            // Find the matching PartStageRequirement for machine preferences (legacy fallback)
            var requirement = routing.FirstOrDefault(r => r.ProductionStageId == exec.ProductionStageId);

            // Get capable machines, ordered by preference (ProcessStage takes priority over PartStageRequirement)
            var capableMachines = ResolveMachines(exec.ProductionStage, requirement, exec.ProcessStage, allMachines);

            if (!capableMachines.Any())
            {
                // No machine available — schedule without machine assignment
                var slotStart = SnapToNextShiftStart(cursor, shifts);
                var slotEnd = AdvanceByWorkHours(slotStart, totalDuration, shifts);
                exec.ScheduledStartAt = slotStart;
                exec.ScheduledEndAt = slotEnd;
                exec.MachineId = null;
                exec.LastModifiedDate = DateTime.UtcNow;
                cursor = slotEnd;
                continue;
            }

            // Try each capable machine — pick the one that finishes earliest
            ScheduleSlot? bestSlot = null;

            foreach (var machine in capableMachines)
            {
                var slot = await FindEarliestSlotOnMachine(machine.Id, totalDuration, cursor, shifts);
                if (bestSlot == null || slot.End < bestSlot.End)
                {
                    bestSlot = slot;
                }
            }

            exec.ScheduledStartAt = bestSlot!.Start;
            exec.ScheduledEndAt = bestSlot.End;
            exec.MachineId = bestSlot.MachineId;
            exec.LastModifiedDate = DateTime.UtcNow;

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
    }

    public async Task<StageExecution> AutoScheduleExecutionAsync(int executionId, DateTime? startAfter = null)
    {
        var exec = await _db.StageExecutions
            .Include(e => e.ProductionStage)
            .Include(e => e.ProcessStage)
            .Include(e => e.Job).ThenInclude(j => j!.Part)
            .FirstOrDefaultAsync(e => e.Id == executionId);

        if (exec == null) throw new InvalidOperationException("Stage execution not found.");

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        var allMachines = await _db.Machines.Where(m => m.IsActive && m.IsAvailableForScheduling).ToListAsync();

        var requirement = exec.Job != null
            ? await _db.PartStageRequirements.FirstOrDefaultAsync(r =>
                r.PartId == exec.Job.PartId && r.ProductionStageId == exec.ProductionStageId && r.IsActive)
            : null;

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

        var duration = exec.EstimatedHours ?? exec.ProductionStage?.DefaultDurationHours ?? 1.0;
        var setupHours = exec.SetupHours ?? 0;
        var totalDuration = duration + setupHours;

        var capableMachines = ResolveMachines(exec.ProductionStage!, requirement, exec.ProcessStage, allMachines);

        if (capableMachines.Any())
        {
            ScheduleSlot? bestSlot = null;
            foreach (var machine in capableMachines)
            {
                var slot = await FindEarliestSlotOnMachine(machine.Id, totalDuration, notBefore, shifts);
                if (bestSlot == null || slot.End < bestSlot.End)
                    bestSlot = slot;
            }

            exec.ScheduledStartAt = bestSlot!.Start;
            exec.ScheduledEndAt = bestSlot.End;
            exec.MachineId = bestSlot.MachineId;
        }
        else
        {
            var slotStart = SnapToNextShiftStart(notBefore, shifts);
            exec.ScheduledStartAt = slotStart;
            exec.ScheduledEndAt = AdvanceByWorkHours(slotStart, totalDuration, shifts);
        }

        exec.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return exec;
    }

    public async Task<ScheduleSlot> FindEarliestSlotAsync(int machineId, double durationHours, DateTime notBefore)
    {
        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
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
            catch
            {
                // Skip jobs that fail to schedule; continue with remaining
            }
        }

        return count;
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
        var candidate = SnapToNextShiftStart(notBefore, shifts);
        var candidateEnd = AdvanceByWorkHours(candidate, durationHours, shifts);

        foreach (var block in existing)
        {
            var blockStart = block.ScheduledStartAt!.Value;
            var blockEnd = block.ScheduledEndAt!.Value;

            // If our candidate fits before this block, we're done
            if (candidateEnd <= blockStart)
                break;

            // Otherwise, try starting after this block
            candidate = SnapToNextShiftStart(blockEnd, shifts);
            candidateEnd = AdvanceByWorkHours(candidate, durationHours, shifts);
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
        var stageCapable = stage.GetAssignedMachineIds();
        if (stageCapable.Any())
        {
            foreach (var sid in stageCapable)
            {
                if (machineLookup.TryGetValue(sid, out var m) && !result.Contains(m))
                    result.Add(m);
            }
        }

        // 7. Default machine for ProductionStage
        if (!string.IsNullOrEmpty(stage.DefaultMachineId)
            && machineLookup.TryGetValue(stage.DefaultMachineId, out var def) && !result.Contains(def))
        {
            result.Insert(0, def); // high priority
        }

        // 8. If still nothing, and stage doesn't require machine, use all machines
        if (!result.Any() && !stage.RequiresMachineAssignment)
        {
            result.AddRange(allMachines.OrderBy(m => m.Priority));
        }

        // 9. If stage requires assignment but nothing found, return whatever is capable
        if (!result.Any() && stage.RequiresMachineAssignment)
        {
            result.AddRange(allMachines
                .Where(m => stage.CanMachineExecuteStage(m.MachineId))
                .OrderBy(m => m.Priority));
        }

        return result;
    }

    private static DateTime SnapToNextShiftStart(DateTime from, List<OperatingShift> shifts)
    {
        if (!shifts.Any()) return from; // 24/7 operation

        for (int dayOffset = 0; dayOffset < 30; dayOffset++)
        {
            var checkDate = from.Date.AddDays(dayOffset);
            var dayName = checkDate.DayOfWeek.ToString()[..3];

            var dayShifts = shifts
                .Where(s => s.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.StartTime)
                .ToList();

            foreach (var shift in dayShifts)
            {
                var shiftStart = checkDate + shift.StartTime;
                var shiftEnd = checkDate + shift.EndTime;
                if (shift.EndTime <= shift.StartTime)
                    shiftEnd = shiftEnd.AddDays(1); // overnight shift

                // If we're before this shift ends, we can use it
                if (from <= shiftEnd)
                {
                    return from > shiftStart ? from : shiftStart;
                }
            }
        }

        return from; // fallback: no matching shifts found
    }

    private static DateTime AdvanceByWorkHours(DateTime from, double hours, List<OperatingShift> shifts)
    {
        if (!shifts.Any()) return from.AddHours(hours); // 24/7 operation

        var remaining = hours;
        var current = from;

        for (int dayOffset = 0; dayOffset < 90 && remaining > 0.001; dayOffset++)
        {
            var checkDate = current.Date;
            if (checkDate < from.Date) checkDate = from.Date;

            var dayName = checkDate.DayOfWeek.ToString()[..3];
            var dayShifts = shifts
                .Where(s => s.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.StartTime)
                .ToList();

            foreach (var shift in dayShifts)
            {
                if (remaining <= 0.001) break;

                var shiftStart = checkDate + shift.StartTime;
                var shiftEnd = checkDate + shift.EndTime;
                if (shift.EndTime <= shift.StartTime)
                    shiftEnd = shiftEnd.AddDays(1); // overnight shift

                if (current >= shiftEnd) continue; // past this shift

                var effectiveStart = current > shiftStart ? current : shiftStart;
                var availableHours = (shiftEnd - effectiveStart).TotalHours;

                if (availableHours <= 0) continue;

                if (remaining <= availableHours)
                {
                    return effectiveStart.AddHours(remaining);
                }

                remaining -= availableHours;
                current = shiftEnd;
            }

            // Move to next day
            current = checkDate.AddDays(1);
        }

        // Fallback: if shifts don't cover enough time, add remaining as calendar hours
        return current.AddHours(remaining);
    }
}
