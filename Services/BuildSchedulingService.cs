using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class BuildSchedulingService : IBuildSchedulingService
{
    private readonly TenantDbContext _db;
    private readonly IBuildPlanningService _buildPlanning;
    private readonly ISerialNumberService _serialNumberService;
    private readonly ISchedulingService _scheduling;

    public BuildSchedulingService(TenantDbContext db, IBuildPlanningService buildPlanning, ISerialNumberService serialNumberService, ISchedulingService scheduling)
    {
        _db = db;
        _buildPlanning = buildPlanning;
        _serialNumberService = serialNumberService;
        _scheduling = scheduling;
    }

    public async Task<BuildScheduleResult> ScheduleBuildAsync(
        int buildPackageId, int machineId, DateTime? startAfter = null)
    {
        var package = await _db.BuildPackages
            .Include(p => p.Parts)
            .FirstOrDefaultAsync(p => p.Id == buildPackageId)
            ?? throw new InvalidOperationException("Build package not found.");

        if (package.Status != BuildPackageStatus.Ready && package.Status != BuildPackageStatus.Sliced)
            throw new InvalidOperationException(
                $"Build package must be in Ready or Sliced status to schedule. Current: {package.Status}");

        if (!package.IsSlicerDataEntered || !package.EstimatedDurationHours.HasValue)
            throw new InvalidOperationException("Slicer data must be entered before scheduling.");

        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var durationHours = package.EstimatedDurationHours.Value;
        var notBefore = startAfter ?? DateTime.UtcNow;

        var slot = await FindEarliestBuildSlotAsync(machine.Id, durationHours, notBefore);

        ChangeoverAnalysis? changeoverInfo = null;
        if (machine.AutoChangeoverEnabled)
        {
            changeoverInfo = await AnalyzeChangeoverAsync(machine.Id, slot.PrintEnd);
        }

        // Update package
        package.ScheduledDate = slot.PrintStart;
        package.Status = BuildPackageStatus.Scheduled;
        package.IsLocked = true;
        package.MachineId = machine.Id;
        package.LastModifiedDate = DateTime.UtcNow;

        // Link predecessor: find the last scheduled/printing build on this machine
        var lastBuild = await _db.BuildPackages
            .Where(bp => bp.MachineId == machine.Id
                && bp.Id != package.Id
                && (bp.Status == BuildPackageStatus.Scheduled || bp.Status == BuildPackageStatus.Printing))
            .OrderByDescending(bp => bp.ScheduledDate)
            .FirstOrDefaultAsync();

        if (lastBuild != null)
            package.PredecessorBuildPackageId = lastBuild.Id;

        await _db.SaveChangesAsync();

        // Create build-level stage executions via the existing planning service
        var stageExecutions = await _buildPlanning.CreateBuildStageExecutionsAsync(buildPackageId, "Scheduler");

        // Prefill per-part stage executions: start after the last build-level stage ends
        var lastBuildStageEnd = stageExecutions
            .Where(e => e.ScheduledEndAt.HasValue)
            .MaxBy(e => e.ScheduledEndAt)?.ScheduledEndAt ?? slot.PrintEnd;

        var partJobIds = await _buildPlanning.CreatePartStageExecutionsAsync(
            buildPackageId, "Scheduler", lastBuildStageEnd);

        // Auto-schedule each per-part job to distribute across available machines
        foreach (var jobId in partJobIds)
        {
            try
            {
                await _scheduling.AutoScheduleJobAsync(jobId, lastBuildStageEnd);
            }
            catch
            {
                // Job could not be auto-scheduled — it will appear unscheduled in the Gantt
            }
        }

        // Create revision snapshot
        await _buildPlanning.CreateRevisionAsync(buildPackageId, "Scheduler",
            $"Scheduled on {machine.Name} starting {slot.PrintStart:g}; {partJobIds.Count} per-part job(s) prefilled");

        return new BuildScheduleResult(slot, changeoverInfo, stageExecutions);
    }

    public async Task<BuildScheduleSlot> FindEarliestBuildSlotAsync(
        int machineId, double durationHours, DateTime notBefore)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();

        // Get scheduled builds on this machine (not stage executions — builds ARE the schedule unit)
        var existingBuilds = await _db.BuildPackages
            .Where(bp => bp.MachineId == machine.Id
                && bp.ScheduledDate != null
                && bp.EstimatedDurationHours != null
                && bp.Status != BuildPackageStatus.Completed
                && bp.Status != BuildPackageStatus.Cancelled)
            .OrderBy(bp => bp.ScheduledDate)
            .Select(bp => new { bp.ScheduledDate, bp.EstimatedDurationHours })
            .ToListAsync();

        var changeoverMinutes = machine.AutoChangeoverEnabled ? machine.ChangeoverMinutes : 0;

        var candidateStart = SnapToNextShiftStart(notBefore, shifts);
        var candidateEnd = AdvanceByWorkHours(candidateStart, durationHours, shifts);

        foreach (var build in existingBuilds)
        {
            var buildStart = build.ScheduledDate!.Value;
            var buildEnd = AdvanceByWorkHours(buildStart, build.EstimatedDurationHours!.Value, shifts);
            var blockEnd = changeoverMinutes > 0
                ? buildEnd.AddMinutes(changeoverMinutes)
                : buildEnd;

            // If our candidate fits before this block, we're done
            if (candidateEnd <= buildStart)
                break;

            // Otherwise, try starting after this block (including changeover)
            candidateStart = SnapToNextShiftStart(blockEnd, shifts);
            candidateEnd = AdvanceByWorkHours(candidateStart, durationHours, shifts);
        }

        var changeoverStart = candidateEnd;
        var changeoverEnd = changeoverMinutes > 0
            ? candidateEnd.AddMinutes(changeoverMinutes)
            : candidateEnd;

        var operatorAvailable = await IsOperatorAvailableDuringWindowAsync(changeoverStart, changeoverEnd);

        return new BuildScheduleSlot(
            candidateStart, candidateEnd,
            changeoverStart, changeoverEnd,
            machineId, operatorAvailable);
    }

    public async Task<List<MachineTimelineEntry>> GetMachineTimelineAsync(
        int machineId, DateTime from, DateTime to)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();

        var builds = await _db.BuildPackages
            .Where(bp => bp.MachineId == machine.Id
                && bp.ScheduledDate != null
                && bp.Status != BuildPackageStatus.Cancelled)
            .OrderBy(bp => bp.ScheduledDate)
            .ToListAsync();

        var changeoverMinutes = machine.AutoChangeoverEnabled ? machine.ChangeoverMinutes : 0;
        var entries = new List<MachineTimelineEntry>();

        foreach (var build in builds)
        {
            var printStart = build.ScheduledDate!.Value;
            var printEnd = build.EstimatedDurationHours.HasValue
                ? AdvanceByWorkHours(printStart, build.EstimatedDurationHours.Value, shifts)
                : printStart;

            // Filter to requested window
            if (printEnd < from || printStart > to)
                continue;

            DateTime? changeoverStart = null;
            DateTime? changeoverEnd = null;

            if (changeoverMinutes > 0)
            {
                changeoverStart = printEnd;
                changeoverEnd = printEnd.AddMinutes(changeoverMinutes);
            }

            entries.Add(new MachineTimelineEntry(
                build.Id, build.Name,
                printStart, printEnd,
                changeoverStart, changeoverEnd,
                build.Status));
        }

        return entries;
    }

    public async Task<ChangeoverAnalysis> AnalyzeChangeoverAsync(int machineId, DateTime buildEndTime)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var changeoverStart = buildEndTime;
        var changeoverEnd = buildEndTime.AddMinutes(machine.ChangeoverMinutes);

        var operatorAvailable = await IsOperatorAvailableDuringWindowAsync(changeoverStart, changeoverEnd);

        string? suggestedAction = null;
        double? suggestedDuration = null;

        if (!operatorAvailable)
        {
            // Find next shift start after build end
            var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
            var nextShiftStart = FindNextShiftStart(buildEndTime, shifts);

            if (nextShiftStart.HasValue)
            {
                var hoursUntilShift = (nextShiftStart.Value - buildEndTime).TotalHours;
                if (hoursUntilShift > 0 && hoursUntilShift < 24)
                {
                    suggestedDuration = hoursUntilShift;
                    suggestedAction = $"Consider a {hoursUntilShift:F1}h build (double-stack) " +
                        $"to sync completion with shift start at {nextShiftStart.Value:t}";
                }
            }
        }

        return new ChangeoverAnalysis(
            operatorAvailable,
            changeoverStart, changeoverEnd,
            suggestedAction, suggestedDuration);
    }

    public async Task<List<StageExecution>> CreateBuildStageExecutionsAsync(
        int buildPackageId, string createdBy)
    {
        // Delegate to the existing BuildPlanningService which already handles this
        var executions = await _buildPlanning.CreateBuildStageExecutionsAsync(buildPackageId, createdBy);

        // Enhance with batch grouping info
        var package = await _db.BuildPackages
            .Include(p => p.Parts)
            .FirstOrDefaultAsync(p => p.Id == buildPackageId);

        if (package != null)
        {
            foreach (var exec in executions)
            {
                var stage = await _db.ProductionStages.FindAsync(exec.ProductionStageId);
                if (stage != null)
                {
                    exec.BatchGroupId = $"{stage.StageSlug.ToUpperInvariant()}-{buildPackageId}";
                    exec.BatchPartCount = package.TotalPartCount;
                }
            }

            await _db.SaveChangesAsync();
        }

        return executions;
    }

    public async Task<PlateReleaseResult> ReleasePlateAsync(int buildPackageId, string releasedBy)
    {
        var package = await _db.BuildPackages
            .Include(p => p.Parts).ThenInclude(pp => pp.Part)
            .Include(p => p.Parts).ThenInclude(pp => pp.WorkOrderLine)
            .FirstOrDefaultAsync(p => p.Id == buildPackageId)
            ?? throw new InvalidOperationException("Build package not found.");

        if (package.Status != BuildPackageStatus.PostPrint)
            throw new InvalidOperationException(
                $"Build package must be in PostPrint status to release. Current: {package.Status}");

        // Verify all build-level stage executions are completed
        var incompleteStages = await _db.StageExecutions
            .Where(e => e.BuildPackageId == buildPackageId
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped)
            .CountAsync();

        if (incompleteStages > 0)
            throw new InvalidOperationException(
                $"Cannot release plate: {incompleteStages} stage execution(s) are not yet completed.");

        var createdInstances = new List<PartInstance>();
        var createdJobs = new List<Job>();
        var instanceIndex = 0;

        foreach (var bpPart in package.Parts)
        {
            for (var i = 0; i < bpPart.Quantity; i++)
            {
                instanceIndex++;
                var trackingId = await _serialNumberService
                    .GenerateTemporaryTrackingIdAsync(buildPackageId, instanceIndex);

                var instance = new PartInstance
                {
                    PartId = bpPart.PartId,
                    BuildPackageId = buildPackageId,
                    WorkOrderLineId = bpPart.WorkOrderLineId ?? 0,
                    TemporaryTrackingId = trackingId,
                    Status = PartInstanceStatus.InProcess,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = releasedBy
                };

                _db.PartInstances.Add(instance);
                createdInstances.Add(instance);
            }
        }

        // Update package
        package.PlateReleasedAt = DateTime.UtcNow;
        package.Status = BuildPackageStatus.Completed;
        package.LastModifiedDate = DateTime.UtcNow;
        package.LastModifiedBy = releasedBy;

        // Complete the build-level Job (it was Scheduled when build was scheduled)
        if (package.ScheduledJobId.HasValue)
        {
            var buildJob = await _db.Jobs.FindAsync(package.ScheduledJobId.Value);
            if (buildJob != null && buildJob.Status != JobStatus.Completed)
            {
                buildJob.Status = JobStatus.Completed;
                buildJob.ActualEnd = DateTime.UtcNow;
                buildJob.LastModifiedDate = DateTime.UtcNow;
                buildJob.LastModifiedBy = releasedBy;
            }
        }

        await _db.SaveChangesAsync();

        // Per-part jobs: either reuse prefilled jobs from scheduling or create new ones
        var partJobIds = await _buildPlanning.CreatePartStageExecutionsAsync(
            buildPackageId, releasedBy, DateTime.UtcNow);

        // Load the per-part jobs
        var partJobs = await _db.Jobs
            .Where(j => partJobIds.Contains(j.Id))
            .ToListAsync();
        createdJobs.AddRange(partJobs);

        // Re-schedule per-part jobs that haven't been scheduled yet
        foreach (var job in createdJobs.Where(j => j.MachineId == null || j.ScheduledStart < DateTime.UtcNow.AddMinutes(-5)))
        {
            try
            {
                await _scheduling.AutoScheduleJobAsync(job.Id, DateTime.UtcNow);
            }
            catch
            {
                // Job could not be auto-scheduled — it will appear unscheduled
            }
        }

        await _buildPlanning.CreateRevisionAsync(buildPackageId, releasedBy,
            $"Plate released: {createdInstances.Count} part instance(s) created");

        return new PlateReleaseResult(
            buildPackageId, createdInstances, createdJobs, createdInstances.Count);
    }

    public async Task LockBuildAsync(int buildPackageId, string lockedBy)
    {
        var package = await _db.BuildPackages.FindAsync(buildPackageId)
            ?? throw new InvalidOperationException("Build package not found.");

        if (package.IsLocked)
            throw new InvalidOperationException("Build package is already locked.");

        package.IsLocked = true;
        package.LastModifiedDate = DateTime.UtcNow;
        package.LastModifiedBy = lockedBy;
        await _db.SaveChangesAsync();

        await _buildPlanning.CreateRevisionAsync(buildPackageId, lockedBy, "Build locked for scheduling");
    }

    public async Task UnlockBuildAsync(int buildPackageId, string unlockedBy, string reason)
    {
        var package = await _db.BuildPackages.FindAsync(buildPackageId)
            ?? throw new InvalidOperationException("Build package not found.");

        if (!package.IsLocked)
            throw new InvalidOperationException("Build package is not locked.");

        if (package.Status == BuildPackageStatus.Printing || package.Status == BuildPackageStatus.PostPrint)
            throw new InvalidOperationException("Cannot unlock a build that is currently printing or in post-print.");

        package.IsLocked = false;
        package.Status = BuildPackageStatus.Draft;
        package.ScheduledDate = null;
        package.PredecessorBuildPackageId = null;
        package.LastModifiedDate = DateTime.UtcNow;
        package.LastModifiedBy = unlockedBy;

        // Cancel outstanding build-level stage executions
        var activeExecutions = await _db.StageExecutions
            .Where(e => e.BuildPackageId == buildPackageId
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed)
            .ToListAsync();

        foreach (var exec in activeExecutions)
        {
            exec.Status = StageExecutionStatus.Skipped;
            exec.Notes = $"Auto-skipped: build unlocked by {unlockedBy} — {reason}";
            exec.CompletedAt = DateTime.UtcNow;
            exec.ActualEndAt = DateTime.UtcNow;
            exec.LastModifiedDate = DateTime.UtcNow;
        }

        // Cancel prefilled per-part jobs and their stage executions
        var perPartJobs = await _db.Jobs
            .Include(j => j.Stages)
            .Where(j => j.Notes != null && j.Notes.Contains(package.Name)
                && j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled)
            .ToListAsync();

        foreach (var job in perPartJobs)
        {
            job.Status = JobStatus.Cancelled;
            job.LastModifiedDate = DateTime.UtcNow;
            job.LastModifiedBy = unlockedBy;
            foreach (var stage in job.Stages.Where(s =>
                s.Status != StageExecutionStatus.Completed
                && s.Status != StageExecutionStatus.Skipped
                && s.Status != StageExecutionStatus.Failed))
            {
                stage.Status = StageExecutionStatus.Skipped;
                stage.Notes = $"Auto-skipped: build unlocked by {unlockedBy} — {reason}";
                stage.CompletedAt = DateTime.UtcNow;
                stage.ActualEndAt = DateTime.UtcNow;
                stage.LastModifiedDate = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();

        await _buildPlanning.CreateRevisionAsync(buildPackageId, unlockedBy, $"Build unlocked: {reason}");
    }

    // ── Private Helpers ─────────────────────────────────────────

    private async Task<bool> IsOperatorAvailableDuringWindowAsync(DateTime windowStart, DateTime windowEnd)
    {
        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        return IsWithinShiftWindow(windowStart, windowEnd, shifts);
    }

    private static bool IsWithinShiftWindow(DateTime windowStart, DateTime windowEnd, List<OperatingShift> shifts)
    {
        if (!shifts.Any()) return true; // 24/7 operation

        var checkDate = windowStart.Date;
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

            // Both changeover start and end must fall within a shift
            if (windowStart >= shiftStart && windowEnd <= shiftEnd)
                return true;
        }

        return false;
    }

    private static DateTime? FindNextShiftStart(DateTime after, List<OperatingShift> shifts)
    {
        if (!shifts.Any()) return null;

        for (int dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var checkDate = after.Date.AddDays(dayOffset);
            var dayName = checkDate.DayOfWeek.ToString()[..3];

            var dayShifts = shifts
                .Where(s => s.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.StartTime)
                .ToList();

            foreach (var shift in dayShifts)
            {
                var shiftStart = checkDate + shift.StartTime;
                if (shiftStart > after)
                    return shiftStart;
            }
        }

        return null;
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
                    shiftEnd = shiftEnd.AddDays(1);

                if (from <= shiftEnd)
                    return from > shiftStart ? from : shiftStart;
            }
        }

        return from; // fallback
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
                    shiftEnd = shiftEnd.AddDays(1);

                if (current >= shiftEnd) continue;

                var effectiveStart = current > shiftStart ? current : shiftStart;
                var availableHours = (shiftEnd - effectiveStart).TotalHours;

                if (availableHours <= 0) continue;

                if (remaining <= availableHours)
                    return effectiveStart.AddHours(remaining);

                remaining -= availableHours;
                current = shiftEnd;
            }

            current = checkDate.AddDays(1);
        }

        return current.AddHours(remaining);
    }
}
