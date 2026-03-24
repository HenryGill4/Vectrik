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
    private readonly INumberSequenceService _numberSeq;
    private readonly IStageCostService _costService;
    private readonly IMachineProgramService _programService;

    public BuildSchedulingService(
        TenantDbContext db,
        IBuildPlanningService buildPlanning,
        ISerialNumberService serialNumberService,
        ISchedulingService scheduling,
        IManufacturingProcessService processService,
        IBatchService batchService,
        INumberSequenceService numberSeq,
        IStageCostService costService,
        IMachineProgramService programService)
    {
        _db = db;
        _buildPlanning = buildPlanning;
        _serialNumberService = serialNumberService;
        _scheduling = scheduling;
        _processService = processService;
        _batchService = batchService;
        _numberSeq = numberSeq;
        _costService = costService;
        _programService = programService;
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

        #pragma warning disable CS0618 // Sliced is obsolete — needed for legacy packages
                if (package.Status != BuildPackageStatus.Ready && package.Status != BuildPackageStatus.Sliced)
                    throw new InvalidOperationException(
                        $"Build package must be in Ready or Sliced status to schedule. Current: {package.Status}");
        #pragma warning restore CS0618

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

    /// <inheritdoc />
    public async Task<BuildScheduleResult> ScheduleBuildRunAsync(
        int buildPackageId, int machineId, DateTime? startAfter = null)
    {
        var source = await _db.BuildPackages
            .Include(p => p.Parts)
            .FirstOrDefaultAsync(p => p.Id == buildPackageId)
            ?? throw new InvalidOperationException("Build package not found.");

        if (!source.IsSlicerDataEntered || !source.EstimatedDurationHours.HasValue)
            throw new InvalidOperationException("Slicer data must be entered before scheduling a run.");

        // Each run is a separate BuildPackage copy linked back to the source via
        // SourceBuildPackageId, so the WO view can track runs and part quantities.
        var copy = await _buildPlanning.CreateScheduledCopyAsync(buildPackageId, "Scheduler");

        return await ScheduleBuildAsync(copy.Id, machineId, startAfter);
    }

    /// <inheritdoc />
    public async Task<BuildScheduleResult> ScheduleBuildRunAutoMachineAsync(
        int buildPackageId, DateTime? startAfter = null)
    {
        var source = await _db.BuildPackages
            .FirstOrDefaultAsync(p => p.Id == buildPackageId)
            ?? throw new InvalidOperationException("Build package not found.");

        if (!source.IsSlicerDataEntered || !source.EstimatedDurationHours.HasValue)
            throw new InvalidOperationException("Slicer data must be entered before scheduling a run.");

        // Create a copy first so slot detection uses the copy's ID
        var copy = await _buildPlanning.CreateScheduledCopyAsync(buildPackageId, "Scheduler");

        var notBefore = startAfter ?? DateTime.UtcNow;
        var bestSlot = await FindBestBuildSlotAsync(
            copy.EstimatedDurationHours!.Value, notBefore, copy.Id);

        return await ScheduleBuildAsync(copy.Id, bestSlot.MachineId, startAfter);
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
        // Also includes program-based build executions (MachineProgramId) for unified scheduling.
        var existingBuildExecutions = await _db.StageExecutions
            .Where(e => e.MachineId == machine.Id
                && (e.BuildPackageId != null || e.MachineProgramId != null)
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

        // Continuous machines start whenever ready; shift-bound machines snap to shift starts
        var candidateStart = isContinuous ? notBefore : SnapToNextShiftStart(notBefore, shifts);
        var candidateEnd = isContinuous
            ? candidateStart.AddHours(durationHours)
            : AdvanceByWorkHours(candidateStart, durationHours, shifts);

        foreach (var block in blocks)
        {
            // Always apply changeover between consecutive builds — even same-build runs
            // need cool-down, powder extraction, and plate loading time.
            var blockEnd = changeoverMinutes > 0
                ? block.End.AddMinutes(changeoverMinutes)
                : block.End;

            // If our candidate fits before this block, we're done
            if (candidateEnd <= block.Start)
                break;

            // Otherwise, try starting after this block (including changeover)
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
        // Query all additive machines that are active and available for scheduling
        var slsMachines = await _db.Machines
            .Where(m => m.IsActive
                && m.IsAvailableForScheduling
                && m.IsAdditiveMachine)
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
#pragma warning disable CS0618 // Draft is obsolete — needed for legacy packages without templates
        package.Status = package.BuildTemplateId.HasValue
            ? BuildPackageStatus.Ready
            : BuildPackageStatus.Draft;
#pragma warning restore CS0618
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

    /// <inheritdoc />
    public async Task<BuildScheduleResult> ScheduleProgramBuildAsync(
        int machineProgramId, int machineId, DateTime? startAfter = null)
    {
        var program = await _db.MachinePrograms
            .Include(p => p.ProgramParts).ThenInclude(pp => pp.Part)
            .FirstOrDefaultAsync(p => p.Id == machineProgramId)
            ?? throw new InvalidOperationException("Machine program not found.");

        if (program.ProgramType != ProgramType.BuildPlate)
            throw new InvalidOperationException("Only BuildPlate programs can be scheduled as builds.");

        if (!program.EstimatedPrintHours.HasValue || program.EstimatedPrintHours <= 0)
            throw new InvalidOperationException("Enter slicer data (print duration) before scheduling.");

        if (!program.ProgramParts.Any())
            throw new InvalidOperationException("Add at least one part to the build plate before scheduling.");

        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var durationHours = program.EstimatedPrintHours.Value;
        var notBefore = startAfter ?? DateTime.UtcNow;

        var slot = await FindEarliestBuildSlotAsync(machine.Id, durationHours, notBefore);

        ChangeoverAnalysis? changeoverInfo = null;
        if (machine.AutoChangeoverEnabled)
            changeoverInfo = await AnalyzeChangeoverAsync(machine.Id, slot.PrintEnd);

        // Load ManufacturingProcess definitions for all parts on the plate
        var partIds = program.ProgramParts.Select(pp => pp.PartId).Distinct().ToList();
        var processes = await _db.ManufacturingProcesses
            .Include(p => p.Stages.OrderBy(s => s.ExecutionOrder))
                .ThenInclude(s => s.ProductionStage)
            .Include(p => p.Stages)
                .ThenInclude(s => s.MachineProgram)
            .Where(p => partIds.Contains(p.PartId) && p.IsActive)
            .ToListAsync();

        var firstProcess = processes.FirstOrDefault();

        // Create a build-level Job for this program run
        var job = new Job
        {
            JobNumber = await _numberSeq.NextAsync("Job"),
            PartId = program.ProgramParts.First().PartId,
            Scope = JobScope.Build,
            ManufacturingProcessId = firstProcess?.Id,
            Quantity = program.TotalPartCount,
            Status = JobStatus.Scheduled,
            Priority = JobPriority.Normal,
            ScheduledStart = slot.PrintStart,
            ScheduledEnd = slot.PrintEnd,
            EstimatedHours = durationHours,
            Notes = $"Build plate program: {program.Name ?? program.ProgramNumber}",
            CreatedBy = "Scheduler",
            LastModifiedBy = "Scheduler",
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        var allExecutions = new List<StageExecution>();
        var sortOrder = 0;

        // Create the print StageExecution linked to the MachineProgramId
        var printExecution = new StageExecution
        {
            JobId = job.Id,
            MachineProgramId = machineProgramId,
            MachineId = machine.Id,
            Status = StageExecutionStatus.NotStarted,
            EstimatedHours = durationHours,
            ScheduledStartAt = slot.PrintStart,
            ScheduledEndAt = slot.PrintEnd,
            SortOrder = sortOrder++,
            CreatedBy = "Scheduler",
            LastModifiedBy = "Scheduler",
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };
        _db.StageExecutions.Add(printExecution);
        allExecutions.Add(printExecution);

        // ── Downstream build-level stages (depowder, heat-treat, EDM, etc.) ──
        // Collect Build-level ProcessStages across all parts' processes,
        // excluding the print stage (DurationFromBuildConfig = true) which is already handled above.
        // Deduplicate by ProductionStageId so shared stages (e.g. depowder) run once for the whole plate.
        var downstreamBuildStages = processes
            .SelectMany(p => p.Stages)
            .Where(s => s.ProcessingLevel == ProcessingLevel.Build && !s.DurationFromBuildConfig)
            .GroupBy(s => s.ProductionStageId)
            .Select(g => g.First())
            .OrderBy(s => s.ExecutionOrder)
            .ToList();

        // Build machine lookup for stage machine resolution
        var machineLookup = await _db.Machines
            .Where(m => m.IsActive)
            .ToDictionaryAsync(m => m.MachineId, m => m.Id);

        var totalPartCount = program.TotalPartCount;
        var currentStart = slot.PrintEnd;

        foreach (var processStage in downstreamBuildStages)
        {
            var durationResult = _processService.CalculateStageDuration(
                processStage, totalPartCount, batchCount: 1, buildConfigHours: null);

            var estimatedHours = durationResult.TotalMinutes / 60.0;

            // Resolve machine: assigned → default from ProductionStage
            int? stageMachineId = null;
            if (processStage.AssignedMachineId.HasValue)
            {
                stageMachineId = processStage.AssignedMachineId.Value;
            }
            else if (!string.IsNullOrEmpty(processStage.ProductionStage.DefaultMachineId)
                     && machineLookup.TryGetValue(processStage.ProductionStage.DefaultMachineId, out var stageIntId))
            {
                stageMachineId = stageIntId;
            }

            // Shared-resource collision avoidance: post-print build stages (depowder,
            // heat-treat, wire EDM) go on shared machines. When multiple builds finish
            // printing around the same time, their post-print stages must queue on
            // the shared machine rather than overlap.
            if (stageMachineId.HasValue)
            {
                var duration = TimeSpan.FromHours(estimatedHours);
                var existingBlocks = await _db.StageExecutions
                    .Where(e => e.MachineId == stageMachineId
                        && e.ScheduledStartAt != null && e.ScheduledEndAt != null
                        && e.Status != StageExecutionStatus.Completed
                        && e.Status != StageExecutionStatus.Skipped
                        && e.Status != StageExecutionStatus.Failed
                        && e.ScheduledEndAt > currentStart)
                    .OrderBy(e => e.ScheduledStartAt)
                    .Select(e => new { e.ScheduledStartAt, e.ScheduledEndAt })
                    .ToListAsync();

                foreach (var block in existingBlocks)
                {
                    if (currentStart + duration <= block.ScheduledStartAt!.Value)
                        break;
                    if (block.ScheduledEndAt!.Value > currentStart)
                        currentStart = block.ScheduledEndAt!.Value;
                }
            }

            var scheduledEnd = currentStart.AddHours(estimatedHours);

            var costEstimate = await _costService.EstimateCostAsync(
                processStage.ProductionStageId, estimatedHours, totalPartCount, batchCount: 1);

            var execution = new StageExecution
            {
                JobId = job.Id,
                ProductionStageId = processStage.ProductionStageId,
                ProcessStageId = processStage.Id,
                MachineProgramId = processStage.MachineProgramId,
                MachineId = stageMachineId,
                Status = StageExecutionStatus.NotStarted,
                EstimatedHours = estimatedHours,
                SetupHours = durationResult.SetupMinutes / 60.0,
                EstimatedCost = costEstimate.TotalCost,
                MaterialCost = processStage.MaterialCost,
                QualityCheckRequired = processStage.RequiresQualityCheck,
                SortOrder = sortOrder++,
                ScheduledStartAt = currentStart,
                ScheduledEndAt = scheduledEnd,
                CreatedBy = "Scheduler",
                LastModifiedBy = "Scheduler",
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            _db.StageExecutions.Add(execution);
            allExecutions.Add(execution);

            currentStart = scheduledEnd;
        }

        await _db.SaveChangesAsync();

        // ── Per-part jobs (batch + part-level stages) ──
        var lastBuildStageEnd = currentStart;
        var perPartJobIds = await CreateProgramPartJobsAsync(program, processes, lastBuildStageEnd);

        // Auto-schedule each per-part job
        var autoScheduled = 0;
        foreach (var jobId in perPartJobIds)
        {
            try
            {
                await _scheduling.AutoScheduleJobWithDiagnosticsAsync(jobId, lastBuildStageEnd);
                autoScheduled++;
            }
            catch (Exception ex)
            {
                // Job could not be auto-scheduled — it will appear unscheduled in the queue
            }
        }

        // Update the build job's scheduled end to include downstream stages
        job.ScheduledEnd = currentStart;
        job.EstimatedHours = (currentStart - slot.PrintStart).TotalHours;
        await _db.SaveChangesAsync();

        var report = new ScheduleDiagnosticReport
        {
            Operation = "ScheduleProgramBuild",
            MachineId = machineId,
            BuildSlots =
            {
                new BuildSlotDiagnostic
                {
                    JobId = job.Id,
                    SlotStart = slot.PrintStart,
                    SlotEnd = slot.PrintEnd,
                    MachineId = machine.Id,
                    MachineName = machine.Name ?? machine.MachineId,
                    DurationHours = durationHours
                }
            },
            Warnings = { }
        };

        if (downstreamBuildStages.Count > 0)
            report.Warnings.Add($"{downstreamBuildStages.Count} downstream build stage(s) scheduled (depowder, EDM, etc.)");
        if (perPartJobIds.Count > 0)
            report.Warnings.Add($"{perPartJobIds.Count} per-part job(s) created ({autoScheduled} auto-scheduled)");

        // Detect changeover conflicts
        if (machine.AutoChangeoverEnabled)
        {
            var lookAhead = slot.PrintEnd.AddDays(7);
            var conflicts = await DetectChangeoverConflictsAsync(machine.Id, slot.PrintStart.AddDays(-1), lookAhead);
            foreach (var conflict in conflicts)
                report.Warnings.Add(conflict.Warning);
        }

        return new BuildScheduleResult(slot, changeoverInfo, allExecutions, report);
    }

    /// <summary>
    /// Creates per-part jobs (batch + part-level stages) for all parts on a build plate program.
    /// Mirrors BuildPlanningService.CreatePartStageExecutionsAsync but uses ProgramPart data.
    /// </summary>
    private async Task<List<int>> CreateProgramPartJobsAsync(
        MachineProgram program,
        List<ManufacturingProcess> processes,
        DateTime startAfter)
    {
        var processLookup = processes.ToDictionary(p => p.PartId, p => p);
        var createdJobIds = new List<int>();

        var machineLookup = await _db.Machines
            .Where(m => m.IsActive)
            .ToDictionaryAsync(m => m.Id, m => m);
        var machineIdLookup = await _db.Machines
            .Where(m => m.IsActive)
            .ToDictionaryAsync(m => m.MachineId, m => m);

        // Group parts by (PartId, WorkOrderLineId) — one job per part-type per WO line
        var partGroups = program.ProgramParts
            .GroupBy(pp => new { pp.PartId, pp.WorkOrderLineId })
            .ToList();

        foreach (var group in partGroups)
        {
            var representative = group.First();
            var totalQuantity = group.Sum(pp => pp.Quantity);

            if (!processLookup.TryGetValue(group.Key.PartId, out var process))
                continue;

            var batchStages = process.Stages
                .Where(s => s.ProcessingLevel == ProcessingLevel.Batch)
                .OrderBy(s => s.ExecutionOrder)
                .ToList();
            var partStages = process.Stages
                .Where(s => s.ProcessingLevel == ProcessingLevel.Part)
                .OrderBy(s => s.ExecutionOrder)
                .ToList();

            if (batchStages.Count == 0 && partStages.Count == 0) continue;

            var batchCapacity = process.DefaultBatchCapacity;
            var batchCount = (int)Math.Ceiling((double)totalQuantity / batchCapacity);

            // Calculate total estimated hours for the job
            var totalEstimatedHours = 0.0;
            foreach (var stage in batchStages)
            {
                var dur = _processService.CalculateStageDuration(stage, totalQuantity, batchCount, null);
                totalEstimatedHours += dur.TotalMinutes / 60.0;
            }
            foreach (var stage in partStages)
            {
                var dur = _processService.CalculateStageDuration(stage, totalQuantity, batchCount, null);
                totalEstimatedHours += dur.TotalMinutes / 60.0;
            }

            var partJob = new Job
            {
                JobNumber = await _numberSeq.NextAsync("Job"),
                PartId = group.Key.PartId,
                WorkOrderLineId = group.Key.WorkOrderLineId,
                Scope = JobScope.Part,
                ManufacturingProcessId = process.Id,
                Quantity = totalQuantity,
                Status = JobStatus.Scheduled,
                Priority = JobPriority.Normal,
                ScheduledStart = startAfter,
                ScheduledEnd = startAfter.AddHours(totalEstimatedHours),
                EstimatedHours = totalEstimatedHours,
                Notes = $"Post-build processing: {representative.Part.PartNumber} x{totalQuantity} (program {program.ProgramNumber})",
                CreatedBy = "Scheduler",
                LastModifiedBy = "Scheduler",
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
            _db.Jobs.Add(partJob);
            await _db.SaveChangesAsync();

            var sortOrder = 0;
            var currentStart = startAfter;

            // Batch-level stages
            foreach (var stage in batchStages)
            {
                var effectiveCapacity = stage.BatchCapacityOverride ?? batchCapacity;
                var stageBatchCount = (int)Math.Ceiling((double)totalQuantity / effectiveCapacity);
                var dur = _processService.CalculateStageDuration(stage, totalQuantity, stageBatchCount, null);
                var estimatedHours = dur.TotalMinutes / 60.0;

                int? stageMachineId = ResolveStageMachineForProgram(stage, machineLookup, machineIdLookup);
                var scheduledEnd = currentStart.AddHours(estimatedHours);

                var costEstimate = await _costService.EstimateCostAsync(
                    stage.ProductionStageId, estimatedHours, totalQuantity, stageBatchCount);

                var execution = new StageExecution
                {
                    JobId = partJob.Id,
                    ProductionStageId = stage.ProductionStageId,
                    ProcessStageId = stage.Id,
                    MachineProgramId = stage.MachineProgramId,
                    Status = StageExecutionStatus.NotStarted,
                    EstimatedHours = estimatedHours,
                    SetupHours = dur.SetupMinutes / 60.0,
                    EstimatedCost = costEstimate.TotalCost,
                    MaterialCost = stage.MaterialCost,
                    QualityCheckRequired = stage.RequiresQualityCheck,
                    BatchPartCount = totalQuantity,
                    MachineId = stageMachineId,
                    ScheduledStartAt = currentStart,
                    ScheduledEndAt = scheduledEnd,
                    SortOrder = sortOrder++,
                    CreatedBy = "Scheduler",
                    LastModifiedBy = "Scheduler",
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                };
                _db.StageExecutions.Add(execution);
                currentStart = scheduledEnd;
            }

            // Part-level stages
            foreach (var stage in partStages)
            {
                var dur = _processService.CalculateStageDuration(stage, totalQuantity, batchCount, null);
                var estimatedHours = dur.TotalMinutes / 60.0;

                int? stageMachineId = ResolveStageMachineForProgram(stage, machineLookup, machineIdLookup);
                var scheduledEnd = currentStart.AddHours(estimatedHours);

                var costEstimate = await _costService.EstimateCostAsync(
                    stage.ProductionStageId, estimatedHours, totalQuantity, batchCount);

                var execution = new StageExecution
                {
                    JobId = partJob.Id,
                    ProductionStageId = stage.ProductionStageId,
                    ProcessStageId = stage.Id,
                    MachineProgramId = stage.MachineProgramId,
                    Status = StageExecutionStatus.NotStarted,
                    EstimatedHours = estimatedHours,
                    SetupHours = dur.SetupMinutes / 60.0,
                    EstimatedCost = costEstimate.TotalCost,
                    MaterialCost = stage.MaterialCost,
                    QualityCheckRequired = stage.RequiresQualityCheck,
                    MachineId = stageMachineId,
                    ScheduledStartAt = currentStart,
                    ScheduledEndAt = scheduledEnd,
                    SortOrder = sortOrder++,
                    CreatedBy = "Scheduler",
                    LastModifiedBy = "Scheduler",
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                };
                _db.StageExecutions.Add(execution);
                currentStart = scheduledEnd;
            }

            partJob.ScheduledEnd = currentStart;
            partJob.EstimatedHours = (currentStart - startAfter).TotalHours;
            createdJobIds.Add(partJob.Id);
        }

        await _db.SaveChangesAsync();
        return createdJobIds;
    }

    /// <summary>
    /// Resolves the machine for a ProcessStage from its preferences.
    /// </summary>
    private static int? ResolveStageMachineForProgram(
        ProcessStage stage,
        Dictionary<int, Machine> byIntId,
        Dictionary<string, Machine> byStringId)
    {
        if (stage.AssignedMachineId.HasValue)
            return stage.AssignedMachineId.Value;

        if (!string.IsNullOrEmpty(stage.ProductionStage.DefaultMachineId)
            && byStringId.TryGetValue(stage.ProductionStage.DefaultMachineId, out var machine))
            return machine.Id;

        return null;
    }

    /// <inheritdoc />
    public async Task<BuildScheduleResult> ScheduleProgramBuildAutoMachineAsync(
        int machineProgramId, DateTime? startAfter = null)
    {
        var program = await _db.MachinePrograms
            .FirstOrDefaultAsync(p => p.Id == machineProgramId)
            ?? throw new InvalidOperationException("Machine program not found.");

        if (program.ProgramType != ProgramType.BuildPlate)
            throw new InvalidOperationException("Only BuildPlate programs can be scheduled as builds.");

        if (!program.EstimatedPrintHours.HasValue || program.EstimatedPrintHours <= 0)
            throw new InvalidOperationException("Enter slicer data (print duration) before scheduling.");

        var notBefore = startAfter ?? DateTime.UtcNow;
        var bestSlot = await FindBestBuildSlotAsync(program.EstimatedPrintHours.Value, notBefore);

        return await ScheduleProgramBuildAsync(machineProgramId, bestSlot.MachineId, startAfter);
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

                // Strict < because at exactly shiftEnd there are zero work hours remaining.
                if (from < shiftEnd)
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

    // ── Work Order → Job Scheduling ─────────────────────────────

    /// <inheritdoc />
    public async Task<WorkOrderScheduleResult> ScheduleFromWorkOrderLineAsync(
        int workOrderLineId, int? preferredMachineId = null, DateTime? startAfter = null)
    {
        var line = await _db.WorkOrderLines
            .Include(l => l.Part)
                .ThenInclude(p => p!.ManufacturingProcess)
                    .ThenInclude(mp => mp!.Stages.OrderBy(s => s.ExecutionOrder))
                        .ThenInclude(s => s.ProductionStage)
            .Include(l => l.Part)
                .ThenInclude(p => p!.ManufacturingProcess)
                    .ThenInclude(mp => mp!.Stages)
                        .ThenInclude(s => s.MachineProgram)
            .Include(l => l.Part)
                .ThenInclude(p => p!.ManufacturingApproach)
            .Include(l => l.WorkOrder)
            .FirstOrDefaultAsync(l => l.Id == workOrderLineId)
            ?? throw new InvalidOperationException($"Work order line {workOrderLineId} not found.");

        var part = line.Part ?? throw new InvalidOperationException("Part not found for work order line.");
        var process = part.ManufacturingProcess;
        var warnings = new List<string>();

        if (process == null)
            throw new InvalidOperationException($"Part {part.PartNumber} has no manufacturing process defined.");

        // Check if this is an additive part requiring a build plate
        if (part.ManufacturingApproach?.RequiresBuildPlate == true)
        {
            // For additive parts, we need an existing BuildPlate program
            var buildPlateProgram = await _db.MachinePrograms
                .Include(p => p.ProgramParts)
                .Where(p => p.ProgramType == ProgramType.BuildPlate
                    && p.Status == ProgramStatus.Active
                    && p.ProgramParts.Any(pp => pp.PartId == part.Id))
                .FirstOrDefaultAsync();

            if (buildPlateProgram != null)
            {
                // Schedule using the existing build plate program
                var machineId = preferredMachineId
                    ?? buildPlateProgram.MachineAssignments?.FirstOrDefault(a => a.IsPreferred)?.MachineId
                    ?? buildPlateProgram.MachineAssignments?.FirstOrDefault()?.MachineId
                    ?? buildPlateProgram.MachineId;

                if (!machineId.HasValue)
                    throw new InvalidOperationException("No machine assigned to the build plate program.");

                var buildResult = await ScheduleProgramBuildAsync(buildPlateProgram.Id, machineId.Value, startAfter);
                var buildJob = buildResult.BuildStageExecutions.FirstOrDefault()?.Job;

                return new WorkOrderScheduleResult(
                    JobId: buildJob?.Id ?? 0,
                    JobNumber: buildJob?.JobNumber ?? "N/A",
                    StageExecutions: buildResult.BuildStageExecutions,
                    ScheduledStart: buildResult.Slot.PrintStart,
                    ScheduledEnd: buildResult.Slot.PrintEnd,
                    Warnings: buildResult.Diagnostics?.Warnings ?? []);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Part {part.PartNumber} requires a build plate. Create a BuildPlate program first.");
            }
        }

        // Standard (non-additive) part: create a Job with stage executions
        var notBefore = startAfter ?? DateTime.UtcNow;
        var quantity = line.Quantity - line.ProducedQuantity;
        if (quantity <= 0)
            throw new InvalidOperationException("No remaining quantity to produce.");

        // Create the job
        var job = new Job
        {
            JobNumber = await _numberSeq.NextAsync("Job"),
            PartId = part.Id,
            WorkOrderLineId = line.Id,
            ManufacturingProcessId = process.Id,
            Scope = JobScope.Part,
            Quantity = quantity,
            Status = JobStatus.Scheduled,
            Priority = line.WorkOrder?.Priority ?? JobPriority.Normal,
            Notes = $"From WO {line.WorkOrder?.OrderNumber}: {part.PartNumber} × {quantity}",
            ScheduledStart = notBefore,
            CreatedBy = "Scheduler",
            LastModifiedBy = "Scheduler",
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        // Create stage executions from the manufacturing process
        var stages = process.Stages.OrderBy(s => s.ExecutionOrder).ToList();
        var executions = new List<StageExecution>();
        var currentStart = notBefore;
        var sortOrder = 0;

        var machineLookup = await _db.Machines
            .Where(m => m.IsActive)
            .ToDictionaryAsync(m => m.Id, m => m);
        var machineIdLookup = await _db.Machines
            .Where(m => m.IsActive && m.MachineId != null)
            .ToDictionaryAsync(m => m.MachineId!, m => m);

        foreach (var stage in stages)
        {
            // Resolve program for this stage
            var programId = stage.MachineProgramId;
            if (!programId.HasValue)
            {
                var bestProgram = await _programService.GetBestProgramForStageAsync(
                    part.Id, preferredMachineId, stage.ProductionStageId);
                programId = bestProgram?.Id;
            }

            // Calculate duration from program or stage defaults
            var durationResult = await _processService.CalculateStageDurationWithProgramAsync(
                stage, quantity, batchCount: 1, buildConfigHours: null, programId);
            var estimatedHours = durationResult.TotalMinutes / 60.0;

            // Resolve machine
            int? stageMachineId = preferredMachineId;
            if (!stageMachineId.HasValue)
                stageMachineId = ResolveStageMachineForProgram(stage, machineLookup, machineIdLookup);

            var costEstimate = await _costService.EstimateCostAsync(
                stage.ProductionStageId, estimatedHours, quantity, batchCount: 1);

            var scheduledEnd = currentStart.AddHours(estimatedHours);

            var execution = new StageExecution
            {
                JobId = job.Id,
                ProductionStageId = stage.ProductionStageId,
                ProcessStageId = stage.Id,
                MachineProgramId = programId,
                MachineId = stageMachineId,
                Status = StageExecutionStatus.NotStarted,
                EstimatedHours = estimatedHours,
                SetupHours = durationResult.SetupMinutes / 60.0,
                EstimatedCost = costEstimate.TotalCost,
                MaterialCost = stage.MaterialCost,
                QualityCheckRequired = stage.RequiresQualityCheck,
                ScheduledStartAt = currentStart,
                ScheduledEndAt = scheduledEnd,
                SortOrder = sortOrder++,
                CreatedBy = "Scheduler",
                LastModifiedBy = "Scheduler",
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            _db.StageExecutions.Add(execution);
            executions.Add(execution);
            currentStart = scheduledEnd;

            if (!programId.HasValue)
                warnings.Add($"Stage {stage.ProductionStage?.Name}: No program found, using stage defaults");
        }

        // Update job scheduled end
        job.ScheduledEnd = currentStart;
        job.EstimatedHours = (currentStart - notBefore).TotalHours;

        // Link job to work order line
        line.Jobs.Add(job);

        await _db.SaveChangesAsync();

        return new WorkOrderScheduleResult(
            JobId: job.Id,
            JobNumber: job.JobNumber!,
            StageExecutions: executions,
            ScheduledStart: notBefore,
            ScheduledEnd: currentStart,
            Warnings: warnings);
    }

    /// <inheritdoc />
    public async Task<StandardProgramScheduleResult> ScheduleStandardProgramAsync(
        int machineProgramId, int quantity, int? machineId = null, int? workOrderLineId = null, DateTime? startAfter = null)
    {
        var program = await _db.MachinePrograms
            .Include(p => p.Part)
                .ThenInclude(pa => pa!.ManufacturingProcess)
                    .ThenInclude(mp => mp!.Stages.OrderBy(s => s.ExecutionOrder))
                        .ThenInclude(s => s.ProductionStage)
            .Include(p => p.MachineAssignments)
            .FirstOrDefaultAsync(p => p.Id == machineProgramId)
            ?? throw new InvalidOperationException("Machine program not found.");

        if (program.ProgramType == ProgramType.BuildPlate)
            throw new InvalidOperationException("Use ScheduleProgramBuildAsync for BuildPlate programs.");

        var part = program.Part ?? throw new InvalidOperationException("Program has no linked part.");
        var process = part.ManufacturingProcess;
        var warnings = new List<string>();
        var notBefore = startAfter ?? DateTime.UtcNow;

        // Resolve machine: parameter → preferred → first assigned → program legacy FK
        var targetMachineId = machineId
            ?? program.MachineAssignments?.FirstOrDefault(a => a.IsPreferred)?.MachineId
            ?? program.MachineAssignments?.FirstOrDefault()?.MachineId
            ?? program.MachineId;

        // Get program duration
        var programDuration = await _programService.GetDurationFromProgramAsync(machineProgramId, quantity);
        if (programDuration == null)
            warnings.Add("Program has no duration data configured; using stage defaults");

        // Create job
        var job = new Job
        {
            JobNumber = await _numberSeq.NextAsync("Job"),
            PartId = part.Id,
            MachineId = targetMachineId,
            WorkOrderLineId = workOrderLineId,
            ManufacturingProcessId = process?.Id,
            Scope = JobScope.Part,
            Quantity = quantity,
            Status = JobStatus.Scheduled,
            Priority = JobPriority.Normal,
            Notes = $"Scheduled from program {program.ProgramNumber}",
            ScheduledStart = notBefore,
            CreatedBy = "Scheduler",
            LastModifiedBy = "Scheduler",
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        var executions = new List<StageExecution>();
        var currentStart = notBefore;
        var sortOrder = 0;

        // If we have a manufacturing process, create stage executions for each stage
        if (process?.Stages.Any() == true)
        {
            var machineLookup = await _db.Machines
                .Where(m => m.IsActive)
                .ToDictionaryAsync(m => m.Id, m => m);
            var machineIdLookup = await _db.Machines
                .Where(m => m.IsActive && m.MachineId != null)
                .ToDictionaryAsync(m => m.MachineId!, m => m);

            foreach (var stage in process.Stages.OrderBy(s => s.ExecutionOrder))
            {
                // For the stage matching this program's linked stage (or machine type), use program duration
                var usesProgramDuration = stage.MachineProgramId == machineProgramId
                    || stage.Id == program.ProcessStageId;

                var durationResult = usesProgramDuration && programDuration != null
                    ? new DurationResult(programDuration.SetupMinutes, programDuration.RunMinutes, programDuration.TotalMinutes, programDuration.Source)
                    : await _processService.CalculateStageDurationWithProgramAsync(stage, quantity, 1, null, stage.MachineProgramId);

                var estimatedHours = durationResult.TotalMinutes / 60.0;
                int? stageMachineId = usesProgramDuration ? targetMachineId : ResolveStageMachineForProgram(stage, machineLookup, machineIdLookup);

                var costEstimate = await _costService.EstimateCostAsync(
                    stage.ProductionStageId, estimatedHours, quantity, 1);

                var scheduledEnd = currentStart.AddHours(estimatedHours);

                var execution = new StageExecution
                {
                    JobId = job.Id,
                    ProductionStageId = stage.ProductionStageId,
                    ProcessStageId = stage.Id,
                    MachineProgramId = usesProgramDuration ? machineProgramId : stage.MachineProgramId,
                    MachineId = stageMachineId,
                    Status = StageExecutionStatus.NotStarted,
                    EstimatedHours = estimatedHours,
                    SetupHours = durationResult.SetupMinutes / 60.0,
                    EstimatedCost = costEstimate.TotalCost,
                    MaterialCost = stage.MaterialCost,
                    QualityCheckRequired = stage.RequiresQualityCheck,
                    ScheduledStartAt = currentStart,
                    ScheduledEndAt = scheduledEnd,
                    SortOrder = sortOrder++,
                    CreatedBy = "Scheduler",
                    LastModifiedBy = "Scheduler",
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                };

                _db.StageExecutions.Add(execution);
                executions.Add(execution);
                currentStart = scheduledEnd;
            }
        }
        else
        {
            // No process defined: create a single execution for this program
            var estimatedHours = programDuration != null
                ? programDuration.TotalMinutes / 60.0
                : 1.0;

            var scheduledEnd = currentStart.AddHours(estimatedHours);

            var execution = new StageExecution
            {
                JobId = job.Id,
                MachineProgramId = machineProgramId,
                MachineId = targetMachineId,
                Status = StageExecutionStatus.NotStarted,
                EstimatedHours = estimatedHours,
                SetupHours = programDuration?.SetupMinutes / 60.0 ?? 0,
                ScheduledStartAt = currentStart,
                ScheduledEndAt = scheduledEnd,
                SortOrder = 0,
                CreatedBy = "Scheduler",
                LastModifiedBy = "Scheduler",
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            _db.StageExecutions.Add(execution);
            executions.Add(execution);
            currentStart = scheduledEnd;
            warnings.Add("No manufacturing process defined; created single execution from program");
        }

        job.ScheduledEnd = currentStart;
        job.EstimatedHours = (currentStart - notBefore).TotalHours;
        await _db.SaveChangesAsync();

        return new StandardProgramScheduleResult(
            JobId: job.Id,
            JobNumber: job.JobNumber!,
            MachineProgramId: machineProgramId,
            StageExecutions: executions,
            ScheduledStart: notBefore,
            ScheduledEnd: currentStart,
            TotalDurationHours: (currentStart - notBefore).TotalHours,
            Warnings: warnings);
    }
}
