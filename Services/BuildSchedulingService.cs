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
    private readonly IManufacturingProcessService _processService;
    private readonly IBatchService _batchService;

    public BuildSchedulingService(
        TenantDbContext db,
        IBuildPlanningService buildPlanning,
        ISerialNumberService serialNumberService,
        ISchedulingService scheduling,
        IManufacturingProcessService processService,
        IBatchService batchService)
    {
        _db = db;
        _buildPlanning = buildPlanning;
        _serialNumberService = serialNumberService;
        _scheduling = scheduling;
        _processService = processService;
        _batchService = batchService;
    }

    public async Task<BuildScheduleResult> ScheduleBuildAsync(
        int buildPackageId, int machineId, DateTime? startAfter = null)
    {
        var report = new ScheduleDiagnosticReport
        {
            Operation = "ScheduleBuild",
            BuildPackageId = buildPackageId,
            MachineId = machineId
        };

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

        var slot = await FindEarliestBuildSlotAsync(machine.Id, durationHours, notBefore, buildPackageId);

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
        var stageExecutions = await _buildPlanning.CreateBuildStageExecutionsAsync(buildPackageId, "Scheduler", startAfter: slot.PrintStart);

        // Collect build slot diagnostic
        var buildJob = stageExecutions.FirstOrDefault()?.JobId;
        if (buildJob.HasValue)
        {
            report.BuildSlots.Add(new BuildSlotDiagnostic
            {
                JobId = buildJob.Value,
                SlotStart = slot.PrintStart,
                SlotEnd = slot.PrintEnd,
                MachineId = machine.Id,
                MachineName = machine.Name ?? machine.MachineId,
                DurationHours = durationHours
            });
        }

        // Prefill per-part stage executions: start after the last build-level stage ends
        var lastBuildStageEnd = stageExecutions
            .Where(e => e.ScheduledEndAt.HasValue)
            .MaxBy(e => e.ScheduledEndAt)?.ScheduledEndAt ?? slot.PrintEnd;

        var partJobIds = await _buildPlanning.CreatePartStageExecutionsAsync(
            buildPackageId, "Scheduler", lastBuildStageEnd);

        // Auto-schedule each per-part job with diagnostics
        var autoScheduled = 0;
        foreach (var jobId in partJobIds)
        {
            try
            {
                var jobDiag = await _scheduling.AutoScheduleJobWithDiagnosticsAsync(jobId, lastBuildStageEnd);
                report.Jobs.Add(jobDiag);
                autoScheduled++;
            }
            catch (Exception ex)
            {
                report.Warnings.Add($"Job {jobId} failed to auto-schedule: {ex.Message}");
            }
        }

        // Create revision snapshot
        await _buildPlanning.CreateRevisionAsync(buildPackageId, "Scheduler",
            $"Scheduled on {machine.Name} starting {slot.PrintStart:g}; {partJobIds.Count} per-part job(s) prefilled ({autoScheduled} auto-scheduled to machines)");

        // Detect changeover conflicts (cooldown chamber stacking)
        if (machine.AutoChangeoverEnabled)
        {
            var lookAhead = slot.PrintEnd.AddDays(7);
            var conflicts = await DetectChangeoverConflictsAsync(machine.Id, slot.PrintStart.AddDays(-1), lookAhead);
            foreach (var conflict in conflicts)
            {
                report.Warnings.Add(conflict.Warning);
            }
        }

        return new BuildScheduleResult(slot, changeoverInfo, stageExecutions, report);
    }

    public async Task<BuildScheduleResult> ScheduleBuildRunAsync(
        int buildPackageId, int machineId, DateTime? startAfter = null)
    {
        var report = new ScheduleDiagnosticReport
        {
            Operation = "ScheduleBuildRun",
            BuildPackageId = buildPackageId,
            MachineId = machineId
        };

        var package = await _db.BuildPackages
            .Include(p => p.Parts)
            .Include(p => p.BuildFileInfo)
            .FirstOrDefaultAsync(p => p.Id == buildPackageId)
            ?? throw new InvalidOperationException("Build package not found.");

        if (!package.IsSlicerDataEntered || !package.EstimatedDurationHours.HasValue)
            throw new InvalidOperationException("Slicer data must be entered before scheduling a run.");

        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var durationHours = package.EstimatedDurationHours.Value;
        var notBefore = startAfter ?? DateTime.UtcNow;

        var slot = await FindEarliestBuildSlotAsync(machine.Id, durationHours, notBefore, buildPackageId);

        ChangeoverAnalysis? changeoverInfo = null;
        if (machine.AutoChangeoverEnabled)
        {
            changeoverInfo = await AnalyzeChangeoverAsync(machine.Id, slot.PrintEnd);
        }

        // Ensure the build package is marked as scheduled and locked
        if (package.Status is BuildPackageStatus.Ready or BuildPackageStatus.Sliced)
        {
            package.Status = BuildPackageStatus.Scheduled;
            package.IsLocked = true;
            package.MachineId ??= machine.Id;
        }
        package.ScheduledDate ??= slot.PrintStart;
        package.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Create build-level stage executions for this run (always new Job)
        var stageExecutions = await _buildPlanning.CreateBuildStageExecutionsAsync(buildPackageId, "Scheduler", forceNewJob: true, startAfter: slot.PrintStart);

        // Collect build slot diagnostic
        var buildJob = stageExecutions.FirstOrDefault()?.JobId;
        if (buildJob.HasValue)
        {
            report.BuildSlots.Add(new BuildSlotDiagnostic
            {
                JobId = buildJob.Value,
                SlotStart = slot.PrintStart,
                SlotEnd = slot.PrintEnd,
                MachineId = machine.Id,
                MachineName = machine.Name ?? machine.MachineId,
                DurationHours = durationHours
            });
        }

        // Per-part stage executions: start after the last build-level stage ends
        var lastBuildStageEnd = stageExecutions
            .Where(e => e.ScheduledEndAt.HasValue)
            .MaxBy(e => e.ScheduledEndAt)?.ScheduledEndAt ?? slot.PrintEnd;

        var partJobIds = await _buildPlanning.CreatePartStageExecutionsAsync(
            buildPackageId, "Scheduler", lastBuildStageEnd, forceNewJobs: true);

        // Auto-schedule each per-part job with diagnostics
        var autoScheduled = 0;
        foreach (var jobId in partJobIds)
        {
            try
            {
                var jobDiag = await _scheduling.AutoScheduleJobWithDiagnosticsAsync(jobId, lastBuildStageEnd);
                report.Jobs.Add(jobDiag);
                autoScheduled++;
            }
            catch (Exception ex)
            {
                report.Warnings.Add($"Job {jobId} failed to auto-schedule: {ex.Message}");
            }
        }

        // Determine run number for revision note
        var existingRunJobs = await _db.Jobs
            .CountAsync(j => j.Scope == JobScope.Build
                && j.Stages.Any(s => s.BuildPackageId == buildPackageId));

        await _buildPlanning.CreateRevisionAsync(buildPackageId, "Scheduler",
            $"Run #{existingRunJobs} on {machine.Name} starting {slot.PrintStart:g}; " +
            $"{partJobIds.Count} per-part job(s) ({autoScheduled} auto-scheduled)");

        // Detect changeover conflicts (cooldown chamber stacking)
        if (machine.AutoChangeoverEnabled)
        {
            var lookAhead = slot.PrintEnd.AddDays(7);
            var conflicts = await DetectChangeoverConflictsAsync(machine.Id, slot.PrintStart.AddDays(-1), lookAhead);
            foreach (var conflict in conflicts)
            {
                report.Warnings.Add(conflict.Warning);
            }
        }

        return new BuildScheduleResult(slot, changeoverInfo, stageExecutions, report);
    }

    /// <inheritdoc />
    public async Task<BuildScheduleResult> ScheduleBuildRunAutoMachineAsync(
        int buildPackageId, DateTime? startAfter = null)
    {
        var package = await _db.BuildPackages
            .FirstOrDefaultAsync(p => p.Id == buildPackageId)
            ?? throw new InvalidOperationException("Build package not found.");

        if (!package.IsSlicerDataEntered || !package.EstimatedDurationHours.HasValue)
            throw new InvalidOperationException("Slicer data must be entered before scheduling a run.");

        var notBefore = startAfter ?? DateTime.UtcNow;
        var bestSlot = await FindBestBuildSlotAsync(
            package.EstimatedDurationHours.Value, notBefore, buildPackageId);

        return await ScheduleBuildRunAsync(buildPackageId, bestSlot.MachineId, startAfter);
    }

    public async Task<BuildScheduleSlot> FindEarliestBuildSlotAsync(
        int machineId, double durationHours, DateTime notBefore, int? forBuildPackageId = null)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();

        // SLS machines with auto-changeover run continuously 24/7.
        // The auto plate change system swaps plates mechanically —
        // prints are NOT constrained to operator shift boundaries.
        var isContinuous = machine.AutoChangeoverEnabled;

        // Query build-level stage executions on this machine for collision detection.
        // Each ScheduleBuildRunAsync call creates a separate Job with its own StageExecution
        // rows, so this correctly detects ALL scheduled runs — not just a single BuildPackage row.
        // Include BuildPackageId so we can skip changeover between same-build consecutive runs.
        var existingBuildExecutions = await _db.StageExecutions
            .Where(e => e.MachineId == machine.Id
                && e.BuildPackageId != null
                && e.ScheduledStartAt != null
                && e.ScheduledEndAt != null
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed)
            .OrderBy(e => e.ScheduledStartAt)
            .Select(e => new { e.ScheduledStartAt, e.ScheduledEndAt, e.BuildPackageId })
            .ToListAsync();

        // Also include builds from BuildPackages that don't yet have stage executions
        var existingBuildPackages = await _db.BuildPackages
            .Where(bp => bp.MachineId == machine.Id
                && bp.ScheduledDate != null
                && bp.EstimatedDurationHours != null
                && bp.Status != BuildPackageStatus.Completed
                && bp.Status != BuildPackageStatus.Cancelled)
            .OrderBy(bp => bp.ScheduledDate)
            .Select(bp => new { bp.Id, bp.ScheduledDate, bp.EstimatedDurationHours, bp.SourceBuildPackageId })
            .ToListAsync();

        // Merge both sources into a unified block list (start, end, buildPackageId)
        var blocks = existingBuildExecutions
            .Select(e => (Start: e.ScheduledStartAt!.Value, End: e.ScheduledEndAt!.Value, BuildPackageId: e.BuildPackageId!.Value))
            .ToList();

        foreach (var bp in existingBuildPackages)
        {
            var bpStart = bp.ScheduledDate!.Value;
            var bpEnd = isContinuous
                ? bpStart.AddHours(bp.EstimatedDurationHours!.Value)
                : AdvanceByWorkHours(bpStart, bp.EstimatedDurationHours!.Value, shifts);
            // Only add if no stage execution already covers this window (avoid double-counting)
            if (!blocks.Any(b => b.Start == bpStart))
                blocks.Add((bpStart, bpEnd, bp.Id));
        }

        blocks = blocks.OrderBy(b => b.Start).ToList();

        var changeoverMinutes = machine.AutoChangeoverEnabled ? machine.ChangeoverMinutes : 0;

        // Resolve the "build family" for same-build detection: a scheduled copy shares a
        // SourceBuildPackageId with other copies of the same build file. Runs created via
        // ScheduleBuildRunAsync reuse the same BuildPackageId directly.
        var sameBuildIds = new HashSet<int>();
        if (forBuildPackageId.HasValue)
        {
            sameBuildIds.Add(forBuildPackageId.Value);

            // If the incoming build is a copy, its siblings share the same source
            var sourceBp = await _db.BuildPackages
                .Where(bp => bp.Id == forBuildPackageId.Value)
                .Select(bp => bp.SourceBuildPackageId)
                .FirstOrDefaultAsync();

            if (sourceBp.HasValue)
            {
                sameBuildIds.Add(sourceBp.Value);
                // Also include all other copies from the same source
                var siblingIds = await _db.BuildPackages
                    .Where(bp => bp.SourceBuildPackageId == sourceBp.Value)
                    .Select(bp => bp.Id)
                    .ToListAsync();
                foreach (var id in siblingIds) sameBuildIds.Add(id);
            }
            else
            {
                // The incoming build IS the source — include all its copies
                var copyIds = await _db.BuildPackages
                    .Where(bp => bp.SourceBuildPackageId == forBuildPackageId.Value)
                    .Select(bp => bp.Id)
                    .ToListAsync();
                foreach (var id in copyIds) sameBuildIds.Add(id);
            }
        }

        // Continuous machines start whenever ready; shift-bound machines snap to shift starts
        var candidateStart = isContinuous ? notBefore : SnapToNextShiftStart(notBefore, shifts);
        var candidateEnd = isContinuous
            ? candidateStart.AddHours(durationHours)
            : AdvanceByWorkHours(candidateStart, durationHours, shifts);

        foreach (var block in blocks)
        {
            // Same-build consecutive runs (same material, same plate layout) don't need
            // changeover — the auto plate change system can swap identical plates immediately.
            var needsChangeover = changeoverMinutes > 0
                && !sameBuildIds.Contains(block.BuildPackageId);

            var blockEnd = needsChangeover
                ? block.End.AddMinutes(changeoverMinutes)
                : block.End;

            // If our candidate fits before this block, we're done
            if (candidateEnd <= block.Start)
                break;

            // Otherwise, try starting after this block (including changeover if needed)
            candidateStart = isContinuous ? blockEnd : SnapToNextShiftStart(blockEnd, shifts);
            candidateEnd = isContinuous
                ? candidateStart.AddHours(durationHours)
                : AdvanceByWorkHours(candidateStart, durationHours, shifts);
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

    /// <inheritdoc />
    public async Task<BestBuildSlot> FindBestBuildSlotAsync(
        double durationHours, DateTime notBefore, int? forBuildPackageId = null)
    {
        // Query all SLS/additive machines that are active and available for scheduling
        var slsMachines = await _db.Machines
            .Where(m => m.IsActive
                && m.IsAvailableForScheduling
                && (m.MachineType == "SLS" || m.MachineType == "Additive"))
            .OrderBy(m => m.Priority)
            .ThenBy(m => m.Id)
            .ToListAsync();

        if (slsMachines.Count == 0)
            throw new InvalidOperationException("No SLS machines are available for scheduling.");

        BestBuildSlot? best = null;

        foreach (var machine in slsMachines)
        {
            var slot = await FindEarliestBuildSlotAsync(
                machine.Id, durationHours, notBefore, forBuildPackageId);

            if (best is null || slot.PrintStart < best.Slot.PrintStart)
            {
                best = new BestBuildSlot(slot, machine.Id, machine.Name ?? machine.MachineId);
            }
        }

        return best!;
    }

    public async Task<List<MachineTimelineEntry>> GetMachineTimelineAsync(
        int machineId, DateTime from, DateTime to)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        // Query build-level StageExecutions on this machine — each multi-run
        // creates its own StageExecution with distinct ScheduledStartAt/EndAt.
        var buildExecutions = await _db.StageExecutions
            .Include(e => e.BuildPackage)
            .Where(e => e.MachineId == machineId
                && e.BuildPackageId != null
                && e.ScheduledStartAt != null
                && e.ScheduledEndAt != null
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed)
            .OrderBy(e => e.ScheduledStartAt)
            .ToListAsync();

        var changeoverMinutes = machine.AutoChangeoverEnabled ? machine.ChangeoverMinutes : 0;
        var entries = new List<MachineTimelineEntry>();

        foreach (var exec in buildExecutions)
        {
            var printStart = exec.ScheduledStartAt!.Value;
            var printEnd = exec.ScheduledEndAt!.Value;

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

            var buildName = exec.BuildPackage?.Name ?? $"Build #{exec.BuildPackageId}";
            var buildStatus = exec.BuildPackage?.Status ?? BuildPackageStatus.Scheduled;

            entries.Add(new MachineTimelineEntry(
                exec.BuildPackageId!.Value, buildName,
                printStart, printEnd,
                changeoverStart, changeoverEnd,
                buildStatus,
                exec.Id));
        }

        // Fallback: include BuildPackages that have no StageExecutions yet
        var coveredBuildIds = entries.Select(e => e.BuildPackageId).ToHashSet();
        var orphanBuilds = await _db.BuildPackages
            .Where(bp => bp.MachineId == machineId
                && bp.ScheduledDate != null
                && bp.Status != BuildPackageStatus.Cancelled
                && !coveredBuildIds.Contains(bp.Id))
            .OrderBy(bp => bp.ScheduledDate)
            .ToListAsync();

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();

        foreach (var build in orphanBuilds)
        {
            var printStart = build.ScheduledDate!.Value;
            var printEnd = build.EstimatedDurationHours.HasValue
                ? AdvanceByWorkHours(printStart, build.EstimatedDurationHours.Value, shifts)
                : printStart;

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

        var sorted = entries.OrderBy(e => e.PrintStart).ToList();

        // Mark entries involved in changeover conflicts
        if (machine.AutoChangeoverEnabled && sorted.Count >= 2)
        {
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var current = sorted[i];
                var next = sorted[i + 1];

                if (current.ChangeoverEnd == null) continue;

                // Check: can an operator empty the cooldown chamber before the next build's
                // changeover begins? If the preceding changeover falls outside shifts AND
                // the next build starts before an operator could arrive, the machine stalls.
                var operatorCanService = IsWithinShiftWindow(
                    current.ChangeoverStart!.Value, current.ChangeoverEnd.Value, shifts);

                if (!operatorCanService)
                {
                    // Find when an operator is next available
                    var nextShift = FindNextShiftStart(current.ChangeoverStart!.Value, shifts);

                    // Conflict: next build's changeover will also need the cooldown station,
                    // but the previous plate hasn't been removed yet
                    if (nextShift == null || next.PrintEnd >= nextShift.Value)
                    {
                        sorted[i] = current with { HasChangeoverConflict = true };
                        sorted[i + 1] = next with { HasChangeoverConflict = true };
                    }
                }
            }
        }

        return sorted;
    }

    /// <inheritdoc />
    public async Task<List<ChangeoverConflict>> DetectChangeoverConflictsAsync(
        int machineId, DateTime from, DateTime to)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        if (!machine.AutoChangeoverEnabled)
            return [];

        var timeline = await GetMachineTimelineAsync(machineId, from, to);
        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        var conflicts = new List<ChangeoverConflict>();

        for (int i = 0; i < timeline.Count - 1; i++)
        {
            var current = timeline[i];
            var next = timeline[i + 1];

            if (current.ChangeoverStart == null || current.ChangeoverEnd == null)
                continue;

            var precedingOpAvailable = IsWithinShiftWindow(
                current.ChangeoverStart.Value, current.ChangeoverEnd.Value, shifts);

            var followingOpAvailable = next.ChangeoverStart.HasValue && next.ChangeoverEnd.HasValue
                && IsWithinShiftWindow(next.ChangeoverStart.Value, next.ChangeoverEnd.Value, shifts);

            if (!precedingOpAvailable)
            {
                var nextShift = FindNextShiftStart(current.ChangeoverStart.Value, shifts);
                var operatorArrival = nextShift ?? current.ChangeoverEnd.Value;

                // If the next build finishes before an operator can empty the cooldown
                // chamber from the preceding build, the auto plate change will fail.
                if (next.PrintEnd >= operatorArrival)
                {
                    var warning = $"Cooldown chamber conflict on {machine.Name}: " +
                        $"{current.BuildName} finishes changeover at {current.ChangeoverEnd.Value:MM/dd HH:mm} " +
                        $"(outside operator hours). Next operator available at {operatorArrival:MM/dd HH:mm}, " +
                        $"but {next.BuildName} finishes at {next.PrintEnd:MM/dd HH:mm} — " +
                        $"cooldown station will still be occupied. Machine will be down until an operator arrives.";

                    conflicts.Add(new ChangeoverConflict(
                        machine.Id, machine.Name,
                        current.BuildName,
                        current.ChangeoverStart.Value, current.ChangeoverEnd.Value,
                        next.BuildName, next.PrintStart,
                        precedingOpAvailable, followingOpAvailable,
                        warning));
                }
            }
        }

        return conflicts;
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

        // Create PartInstances
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

        // Complete the build-level Job
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

        // Create production batches via IBatchService if ManufacturingProcess exists
        var partIds = package.Parts.Select(p => p.PartId).Distinct().ToList();
        var processes = await _db.ManufacturingProcesses
            .Where(p => partIds.Contains(p.PartId) && p.IsActive)
            .ToDictionaryAsync(p => p.PartId, p => p);

        if (processes.Any())
        {
            // Determine batch capacity from the first available process
            var primaryProcess = processes.Values.First();
            var batchCapacity = primaryProcess.DefaultBatchCapacity;

            var batches = await _batchService.CreateBatchesFromBuildAsync(
                buildPackageId, batchCapacity, releasedBy);

            // Assign part instances to batches
            var batchIndex = 0;
            var partsAssignedToBatch = 0;
            foreach (var instance in createdInstances)
            {
                if (batchIndex < batches.Count)
                {
                    var currentBatch = batches[batchIndex];
                    await _batchService.AssignPartToBatchAsync(
                        instance.Id, currentBatch.Id,
                        $"Initial batch from build #{buildPackageId}",
                        releasedBy);

                    partsAssignedToBatch++;
                    if (partsAssignedToBatch >= currentBatch.Capacity && batchIndex < batches.Count - 1)
                    {
                        batchIndex++;
                        partsAssignedToBatch = 0;
                    }
                }
            }
        }

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
            $"Plate released: {createdInstances.Count} part instance(s) created, batches assigned");

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
