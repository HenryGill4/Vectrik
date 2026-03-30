using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

/// <summary>
/// Program-centric scheduling for all machine types.
/// MachineProgram is the schedulable entity — replaces BuildPackage-based scheduling.
/// </summary>
public class ProgramSchedulingService : IProgramSchedulingService
{
    private readonly TenantDbContext _db;
    private readonly ISchedulingService _scheduling;
    private readonly IManufacturingProcessService _processService;
    private readonly IBatchService _batchService;
    private readonly INumberSequenceService _numberSeq;
    private readonly IStageCostService _costService;
    private readonly IMachineProgramService _programService;
    private readonly ISerialNumberService _serialNumberService;
    private readonly IDownstreamProgramService _downstreamService;

    public ProgramSchedulingService(
        TenantDbContext db,
        ISchedulingService scheduling,
        IManufacturingProcessService processService,
        IBatchService batchService,
        INumberSequenceService numberSeq,
        IStageCostService costService,
        IMachineProgramService programService,
        ISerialNumberService serialNumberService,
        IDownstreamProgramService downstreamService)
    {
        _db = db;
        _scheduling = scheduling;
        _processService = processService;
        _batchService = batchService;
        _numberSeq = numberSeq;
        _costService = costService;
        _programService = programService;
        _serialNumberService = serialNumberService;
        _downstreamService = downstreamService;
    }

    // ══════════════════════════════════════════════════════════
    // Build Plate (SLS) Scheduling
    // ══════════════════════════════════════════════════════════

    public async Task<ProgramScheduleResult> ScheduleBuildPlateAsync(
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

        // ── Reschedule: clean up existing job if this program was already scheduled ──
        if (program.ScheduledJobId.HasValue)
        {
            var oldJobId = program.ScheduledJobId.Value;

            // Delete per-part jobs that were created for this program's previous schedule
            // Per-part job notes use format: "Post-build processing: ... (program {ProgramNumber})"
            var programRef = $"(program {program.ProgramNumber ?? program.Name})";
            var perPartJobs = await _db.Jobs
                .Where(j => j.Notes != null && j.Notes.Contains(programRef) && j.Id != oldJobId)
                .ToListAsync();

            // Delete old stage executions for the build job
            var oldExecutions = await _db.StageExecutions
                .Where(se => se.JobId == oldJobId)
                .ToListAsync();
            _db.StageExecutions.RemoveRange(oldExecutions);

            // Delete per-part job stage executions and the jobs themselves
            if (perPartJobs.Any())
            {
                var oldPerPartJobIds = perPartJobs.Select(j => j.Id).ToList();
                var perPartExecutions = await _db.StageExecutions
                    .Where(se => se.JobId != null && oldPerPartJobIds.Contains(se.JobId.Value))
                    .ToListAsync();
                _db.StageExecutions.RemoveRange(perPartExecutions);
                _db.Jobs.RemoveRange(perPartJobs);
            }

            // Delete the old build job
            var oldJob = await _db.Jobs.FindAsync(oldJobId);
            if (oldJob != null)
                _db.Jobs.Remove(oldJob);

            // Clear the program's scheduling state so it can be re-scheduled fresh
            program.ScheduledJobId = null;
            program.ScheduledDate = null;
            program.ScheduleStatus = ProgramScheduleStatus.None;
            program.IsLocked = false;
            program.PredecessorProgramId = null;
            await _db.SaveChangesAsync();
        }

        // Validate downstream program readiness
        var downstreamValidation = await _downstreamService.ValidateDownstreamReadinessAsync(machineProgramId);
        if (!downstreamValidation.IsValid)
        {
            var missing = string.Join(", ", downstreamValidation.MissingPrograms.Select(m => m.StageName));
            throw new InvalidOperationException(
                $"Downstream programs missing for: {missing}. Assign or create programs before scheduling.");
        }

        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var durationHours = program.EstimatedPrintHours.Value;
        var notBefore = startAfter ?? DateTime.UtcNow;

        var slot = await FindEarliestSlotAsync(machine.Id, durationHours, notBefore, machineProgramId);

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
            PartId = program.ProgramParts.FirstOrDefault()?.PartId ?? 0,
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

        // Update program scheduling fields
        program.ScheduledDate = slot.PrintStart;
        program.ScheduleStatus = ProgramScheduleStatus.Scheduled;
        program.IsLocked = true;
        program.MachineId = machine.Id;
        program.ScheduledJobId = job.Id;
        program.LastModifiedDate = DateTime.UtcNow;

        // Link predecessor: find the last scheduled program on this machine
        var lastProgram = await _db.MachinePrograms
            .Where(p => p.MachineId == machine.Id
                && p.Id != program.Id
                && p.ProgramType == ProgramType.BuildPlate
                && (p.ScheduleStatus == ProgramScheduleStatus.Scheduled || p.ScheduleStatus == ProgramScheduleStatus.Printing))
            .OrderByDescending(p => p.ScheduledDate)
            .FirstOrDefaultAsync();

        if (lastProgram != null)
            program.PredecessorProgramId = lastProgram.Id;

        await _db.SaveChangesAsync();

        var allExecutions = new List<StageExecution>();
        var sortOrder = 0;

        // Create the print StageExecution linked to the MachineProgramId
        // Note: We need a ProductionStageId for SLS Print - look it up or create a default
        var printStageId = await GetOrCreatePrintStageIdAsync();

        var printExecution = new StageExecution
        {
            JobId = job.Id,
            ProductionStageId = printStageId,
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
        // These require operators and must respect shift schedules — parts are still
        // on the plate until Wire EDM cuts them off, so no weekend/overnight work.
        var downstreamBuildStages = processes
            .SelectMany(p => p.Stages)
            .Where(s => s.ProcessingLevel == ProcessingLevel.Build && !s.DurationFromBuildConfig)
            .GroupBy(s => s.ProductionStageId)
            .Select(g => g.First())
            .OrderBy(s => s.ExecutionOrder)
            .ToList();

        var machineLookup = await _db.Machines
            .Where(m => m.IsActive)
            .ToDictionaryAsync(m => m.MachineId, m => m.Id);

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        var totalPartCount = program.TotalPartCount;
        var currentStart = slot.PrintEnd;

        foreach (var processStage in downstreamBuildStages)
        {
            var durationResult = _processService.CalculateStageDuration(
                processStage, totalPartCount, batchCount: 1, buildConfigHours: null);

            var estimatedHours = durationResult.TotalMinutes / 60.0;

            int? stageMachineId = processStage.AssignedMachineId;
            if (!stageMachineId.HasValue && !string.IsNullOrEmpty(processStage.ProductionStage?.DefaultMachineId)
                && machineLookup.TryGetValue(processStage.ProductionStage.DefaultMachineId, out var stageIntId))
            {
                stageMachineId = stageIntId;
            }

            // Snap to next operator shift — downstream build stages require operators
            currentStart = ShiftTimeHelper.SnapToNextShiftStart(currentStart, shifts);

            // Shared-resource collision avoidance
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

                // Re-snap after collision avoidance may have moved us to off-shift
                currentStart = ShiftTimeHelper.SnapToNextShiftStart(currentStart, shifts);
            }

            // Advance by work hours only — skip non-shift periods
            var scheduledEnd = ShiftTimeHelper.AdvanceByWorkHours(currentStart, estimatedHours, shifts);

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
        foreach (var jobId in perPartJobIds)
        {
            try
            {
                await _scheduling.AutoScheduleJobWithDiagnosticsAsync(jobId, lastBuildStageEnd);
            }
            catch
            {
                // Job could not be auto-scheduled — it will appear unscheduled
            }
        }

        // Update the build job's scheduled end to include downstream stages
        job.ScheduledEnd = currentStart;
        job.EstimatedHours = (currentStart - slot.PrintStart).TotalHours;
        await _db.SaveChangesAsync();

        var report = new ScheduleDiagnosticReport
        {
            Operation = "ScheduleBuildPlate",
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
            }
        };

        if (downstreamBuildStages.Count > 0)
            report.Warnings.Add($"{downstreamBuildStages.Count} downstream build stage(s) scheduled");
        if (perPartJobIds.Count > 0)
            report.Warnings.Add($"{perPartJobIds.Count} per-part job(s) created");

        // Detect changeover conflicts
        if (machine.AutoChangeoverEnabled)
        {
            var lookAhead = slot.PrintEnd.AddDays(7);
            var conflicts = await DetectChangeoverConflictsAsync(machine.Id, slot.PrintStart.AddDays(-1), lookAhead);
            foreach (var conflict in conflicts)
                report.Warnings.Add(conflict.Warning);
        }

        return new ProgramScheduleResult(
            new ProgramScheduleSlot(slot.PrintStart, slot.PrintEnd, slot.ChangeoverStart, slot.ChangeoverEnd, slot.MachineId, slot.OperatorAvailableForChangeover),
            changeoverInfo, allExecutions, machineProgramId, program.Name ?? program.ProgramNumber, report);
    }

    public async Task<ProgramScheduleResult> ScheduleBuildPlateAutoMachineAsync(
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
        var bestSlot = await FindBestSlotAsync(program.EstimatedPrintHours.Value, notBefore, machineType: "SLS");

        // Pass the slot's actual start time so ScheduleBuildPlateAsync uses the same slot
        return await ScheduleBuildPlateAsync(machineProgramId, bestSlot.MachineId, bestSlot.Slot.PrintStart);
    }

    // ══════════════════════════════════════════════════════════
    // Cascade Rescheduling
    // ══════════════════════════════════════════════════════════

    public async Task<CascadeResult> CascadeRescheduleAsync(int machineId, int insertedProgramId)
    {
        var inserted = await _db.MachinePrograms.FindAsync(insertedProgramId);
        if (inserted?.ScheduledDate == null || !inserted.EstimatedPrintHours.HasValue)
            return new CascadeResult(0, new List<string>());

        var machine = await _db.Machines.FindAsync(machineId);
        var changeoverMinutes = (machine?.AutoChangeoverEnabled == true) ? machine.ChangeoverMinutes : 0;

        var insertedEnd = inserted.ScheduledDate.Value.AddHours(inserted.EstimatedPrintHours.Value);
        var blockingEnd = insertedEnd.AddMinutes(changeoverMinutes);

        // Get all other scheduled builds on this machine, ordered by start time
        var programsOnMachine = await _db.MachinePrograms
            .Where(p => p.MachineId == machineId
                && p.Id != insertedProgramId
                && p.ProgramType == ProgramType.BuildPlate
                && p.ScheduledDate != null
                && (p.ScheduleStatus == ProgramScheduleStatus.Scheduled
                    || p.ScheduleStatus == ProgramScheduleStatus.Ready))
            .OrderBy(p => p.ScheduledDate)
            .ToListAsync();

        var shifted = new List<string>();
        const int maxCascade = 20;

        foreach (var prog in programsOnMachine)
        {
            if (shifted.Count >= maxCascade) break;

            var progStart = prog.ScheduledDate!.Value;
            var progDuration = prog.EstimatedPrintHours ?? 0;
            var progEnd = progStart.AddHours(progDuration);

            // If this program starts after the blocking window, no conflict — done
            if (progStart >= blockingEnd) break;

            // If this program ends before the inserted one starts, skip
            if (progEnd <= inserted.ScheduledDate.Value) continue;

            // Skip locked/in-progress builds — can't move them
            if (prog.ScheduleStatus == ProgramScheduleStatus.Printing
                || prog.ScheduleStatus == ProgramScheduleStatus.PostPrint
                || prog.ScheduleStatus == ProgramScheduleStatus.Completed)
                continue;

            // Overlap detected — reschedule this build after the blocking window
            await ScheduleBuildPlateAsync(prog.Id, machineId, blockingEnd);
            shifted.Add(prog.Name ?? prog.ProgramNumber ?? $"Build #{prog.Id}");

            // Update blocking end: this shifted build now occupies time after blockingEnd
            blockingEnd = blockingEnd.AddHours(progDuration).AddMinutes(changeoverMinutes);
        }

        return new CascadeResult(shifted.Count, shifted);
    }

    public async Task<ProgramScheduleResult> ScheduleBuildPlateRunAsync(
        int machineProgramId, int machineId, DateTime? startAfter = null)
    {
        var source = await _db.MachinePrograms
            .Include(p => p.ProgramParts)
            .FirstOrDefaultAsync(p => p.Id == machineProgramId)
            ?? throw new InvalidOperationException("Machine program not found.");

        if (!source.HasSlicerData)
            throw new InvalidOperationException("Slicer data must be entered before scheduling a run.");

        // Create a copy of the program for this run
        var copy = await CreateScheduledCopyAsync(machineProgramId, "Scheduler");

        return await ScheduleBuildPlateAsync(copy.Id, machineId, startAfter);
    }

    public async Task<ProgramScheduleResult> ScheduleBuildPlateRunAutoMachineAsync(
        int machineProgramId, DateTime? startAfter = null)
    {
        var source = await _db.MachinePrograms
            .FirstOrDefaultAsync(p => p.Id == machineProgramId)
            ?? throw new InvalidOperationException("Machine program not found.");

        if (!source.HasSlicerData)
            throw new InvalidOperationException("Slicer data must be entered before scheduling a run.");

        var copy = await CreateScheduledCopyAsync(machineProgramId, "Scheduler");

        var notBefore = startAfter ?? DateTime.UtcNow;
        var bestSlot = await FindBestSlotAsync(copy.EstimatedPrintHours!.Value, notBefore, machineType: "SLS");

        return await ScheduleBuildPlateAsync(copy.Id, bestSlot.MachineId, startAfter);
    }

    // ══════════════════════════════════════════════════════════
    // Standard Program Scheduling
    // ══════════════════════════════════════════════════════════

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
            throw new InvalidOperationException("Use ScheduleBuildPlateAsync for BuildPlate programs.");

        var part = program.Part ?? throw new InvalidOperationException("Program has no linked part.");
        var process = part.ManufacturingProcess;
        var warnings = new List<string>();
        var notBefore = startAfter ?? DateTime.UtcNow;

        var targetMachineId = machineId
            ?? program.MachineAssignments?.FirstOrDefault(a => a.IsPreferred)?.MachineId
            ?? program.MachineAssignments?.FirstOrDefault()?.MachineId
            ?? program.MachineId;

        var programDuration = await _programService.GetDurationFromProgramAsync(machineProgramId, quantity);
        if (programDuration == null)
            warnings.Add("Program has no duration data configured; using stage defaults");

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

        if (process?.Stages.Any() == true)
        {
            var machineLookupById = await _db.Machines
                .Where(m => m.IsActive)
                .ToDictionaryAsync(m => m.Id, m => m);
            var machineIdLookup = await _db.Machines
                .Where(m => m.IsActive && m.MachineId != null)
                .ToDictionaryAsync(m => m.MachineId!, m => m);

            foreach (var stage in process.Stages.OrderBy(s => s.ExecutionOrder))
            {
                var usesProgramDuration = stage.MachineProgramId == machineProgramId
                    || stage.Id == program.ProcessStageId;

                var durationResult = usesProgramDuration && programDuration != null
                    ? new DurationResult(programDuration.SetupMinutes, programDuration.RunMinutes, programDuration.TotalMinutes, programDuration.Source)
                    : await _processService.CalculateStageDurationWithProgramAsync(stage, quantity, 1, null, stage.MachineProgramId);

                var estimatedHours = durationResult.TotalMinutes / 60.0;
                int? stageMachineId = usesProgramDuration ? targetMachineId : ResolveStageMachine(stage, machineLookupById, machineIdLookup);

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
            var estimatedHours = programDuration != null
                ? programDuration.TotalMinutes / 60.0
                : 1.0;

            var scheduledEnd = currentStart.AddHours(estimatedHours);
            var defaultStageId = await GetOrCreateDefaultStageIdAsync(program.MachineType ?? "CNC");

            var execution = new StageExecution
            {
                JobId = job.Id,
                ProductionStageId = defaultStageId,
                MachineProgramId = machineProgramId,
                MachineId = targetMachineId,
                Status = StageExecutionStatus.NotStarted,
                EstimatedHours = estimatedHours,
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

        job.ScheduledEnd = currentStart;
        job.EstimatedHours = (currentStart - notBefore).TotalHours;
        await _db.SaveChangesAsync();

        return new StandardProgramScheduleResult(
            job.Id, job.JobNumber!, machineProgramId, executions,
            notBefore, currentStart, job.EstimatedHours, warnings);
    }

    // ══════════════════════════════════════════════════════════
    // Work Order Integration
    // ══════════════════════════════════════════════════════════

    public async Task<WorkOrderScheduleResult> ScheduleFromWorkOrderLineAsync(
        int workOrderLineId, int? preferredMachineId = null, DateTime? startAfter = null)
    {
        var line = await _db.WorkOrderLines
            .Include(l => l.Part)
                .ThenInclude(p => p!.ManufacturingProcess)
                    .ThenInclude(mp => mp!.Stages.OrderBy(s => s.ExecutionOrder))
                        .ThenInclude(s => s.ProductionStage)
            .Include(l => l.Part)
                .ThenInclude(p => p!.ManufacturingApproach)
            .Include(l => l.WorkOrder)
            .FirstOrDefaultAsync(l => l.Id == workOrderLineId)
            ?? throw new InvalidOperationException($"Work order line {workOrderLineId} not found.");

        var part = line.Part ?? throw new InvalidOperationException("Part not found for work order line.");
        var warnings = new List<string>();

        // Check if this is an additive part requiring a build plate
        if (part.ManufacturingApproach?.RequiresBuildPlate == true)
        {
            var buildPlateProgram = await _db.MachinePrograms
                .Include(p => p.ProgramParts)
                .Include(p => p.MachineAssignments)
                .Where(p => p.ProgramType == ProgramType.BuildPlate
                    && p.Status == ProgramStatus.Active
                    && p.ProgramParts.Any(pp => pp.PartId == part.Id))
                .FirstOrDefaultAsync();

            if (buildPlateProgram != null)
            {
                var machineId = preferredMachineId
                    ?? buildPlateProgram.MachineAssignments?.FirstOrDefault(a => a.IsPreferred)?.MachineId
                    ?? buildPlateProgram.MachineAssignments?.FirstOrDefault()?.MachineId
                    ?? buildPlateProgram.MachineId;

                if (!machineId.HasValue)
                    throw new InvalidOperationException("No machine assigned to the build plate program.");

                var buildResult = await ScheduleBuildPlateAsync(buildPlateProgram.Id, machineId.Value, startAfter);
                var buildJob = buildResult.StageExecutions.FirstOrDefault()?.Job;

                return new WorkOrderScheduleResult(
                    buildJob?.Id ?? 0,
                    buildJob?.JobNumber ?? "N/A",
                    buildResult.StageExecutions,
                    buildResult.Slot.PrintStart,
                    buildResult.Slot.PrintEnd,
                    buildResult.Diagnostics?.Warnings ?? []);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Part {part.PartNumber} requires a build plate. Create a BuildPlate program first.");
            }
        }

        // Standard part: create a Job with stage executions
        var process = part.ManufacturingProcess
            ?? throw new InvalidOperationException($"Part {part.PartNumber} has no manufacturing process defined.");

        var notBefore = startAfter ?? DateTime.UtcNow;
        var quantity = line.Quantity - line.ProducedQuantity;
        if (quantity <= 0)
            throw new InvalidOperationException("No remaining quantity to produce.");

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

        var stages = process.Stages.OrderBy(s => s.ExecutionOrder).ToList();
        var executions = new List<StageExecution>();
        var currentStart = notBefore;
        var sortOrder = 0;

        // ── Prefetch: batch-load machines, programs, and cost profiles to avoid N+1 queries ──
        var allActiveMachines = await _db.Machines.Where(m => m.IsActive).ToListAsync();
        var machineLookupById = allActiveMachines.ToDictionary(m => m.Id, m => m);
        var machineIdLookup = allActiveMachines
            .Where(m => m.MachineId != null)
            .ToDictionary(m => m.MachineId!, m => m);

        // Prefetch all programs for this part (avoids per-stage GetBestProgramForStageAsync DB hits)
        var partPrograms = await _db.MachinePrograms
            .Include(p => p.MachineAssignments)
            .Include(p => p.ProgramParts)
            .Where(p => p.Status == ProgramStatus.Active
                && (p.PartId == part.Id || p.ProgramParts.Any(pp => pp.PartId == part.Id)))
            .ToListAsync();

        // Prefetch all ProcessStages for stage→program linkage
        var processStageIds = stages.Select(s => s.Id).ToList();
        var linkedProcessStages = await _db.ProcessStages
            .Where(ps => processStageIds.Contains(ps.Id))
            .ToDictionaryAsync(ps => ps.Id, ps => ps);

        // Prefetch cost profiles for all stages in one query
        var stageIds = stages.Select(s => s.ProductionStageId).Distinct().ToList();
        var costProfiles = await _db.StageCostProfiles
            .Where(p => stageIds.Contains(p.ProductionStageId))
            .ToDictionaryAsync(p => p.ProductionStageId, p => p);
        var productionStages = await _db.ProductionStages
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s);

        foreach (var stage in stages)
        {
            var programId = stage.MachineProgramId;
            if (!programId.HasValue)
            {
                // Resolve from prefetched programs instead of DB query per stage
                var candidates = partPrograms
                    .Where(p => p.ProcessStageId != null
                        && linkedProcessStages.ContainsKey(p.ProcessStageId.Value)
                        && linkedProcessStages[p.ProcessStageId.Value].ProductionStageId == stage.ProductionStageId)
                    .ToList();
                if (!candidates.Any())
                    candidates = partPrograms.ToList(); // fallback: any program for this part

                var bestProgram = candidates
                    .Where(p => p.EstimateSource == "Auto" && p.ActualSampleCount > 0)
                    .OrderByDescending(p => p.ActualSampleCount)
                    .FirstOrDefault()
                    ?? candidates.FirstOrDefault();
                programId = bestProgram?.Id;
            }

            var durationResult = await _processService.CalculateStageDurationWithProgramAsync(
                stage, quantity, batchCount: 1, buildConfigHours: null, programId);
            var estimatedHours = durationResult.TotalMinutes / 60.0;

            int? stageMachineId = preferredMachineId ?? ResolveStageMachine(stage, machineLookupById, machineIdLookup);

            // Estimate cost from prefetched profiles instead of DB query per stage
            decimal estimatedCost;
            if (costProfiles.TryGetValue(stage.ProductionStageId, out var profile))
            {
                var laborCost = profile.LaborCostPerHour * (decimal)estimatedHours;
                var equipmentCost = profile.EquipmentCostPerHour * (decimal)estimatedHours;
                var overheadCost = profile.OverheadCostPerHour * (decimal)estimatedHours;
                var partCosts = profile.PerPartCost * quantity;
                var toolingCost = profile.ToolingCostPerRun;
                var directTimeCost = laborCost + equipmentCost + overheadCost;
                var overheadMarkup = directTimeCost * (decimal)(profile.OverheadPercent / 100);
                estimatedCost = directTimeCost + overheadMarkup + partCosts + toolingCost;
            }
            else
            {
                var fallbackRate = productionStages.GetValueOrDefault(stage.ProductionStageId)?.DefaultHourlyRate ?? 85m;
                estimatedCost = fallbackRate * (decimal)estimatedHours;
            }

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
                EstimatedCost = estimatedCost,
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

        job.ScheduledEnd = currentStart;
        job.EstimatedHours = (currentStart - notBefore).TotalHours;
        line.Jobs.Add(job);
        await _db.SaveChangesAsync();

        return new WorkOrderScheduleResult(job.Id, job.JobNumber!, executions, notBefore, currentStart, warnings);
    }

    public async Task<List<MachineProgram>> GetAvailableProgramsForPartAsync(int partId)
    {
        // Find BuildPlate programs that contain this part and are available for scheduling:
        // 1. Unscheduled (None/Ready) programs
        // 2. Source/master programs (reusable templates) regardless of schedule status
        return await _db.MachinePrograms
            .Include(p => p.ProgramParts)
            .Include(p => p.MachineAssignments)
            .Where(p => p.ProgramType == ProgramType.BuildPlate
                && p.Status == ProgramStatus.Active
                && p.ProgramParts.Any(pp => pp.PartId == partId)
                && (
                    (p.ScheduleStatus == ProgramScheduleStatus.None
                        || p.ScheduleStatus == ProgramScheduleStatus.Ready)
                    || (p.SourceProgramId == null && p.EstimatedPrintHours != null
                        && p.ScheduleStatus != ProgramScheduleStatus.Cancelled)
                ))
            .OrderByDescending(p => p.LastModifiedDate)
            .ToListAsync();
    }

    public async Task<List<MachineProgram>> GetAvailableBuildPlateProgramsAsync()
    {
        // Return BuildPlate programs that are either:
        // 1. Unscheduled (None/Ready) — traditional availability check
        // 2. Source/master programs (no SourceProgramId) with slicer data — these are
        //    reusable templates that ScheduleBuildPlateRunAsync creates copies from.
        //    They may be in Scheduled status from previous runs but remain available.
        return await _db.MachinePrograms
            .Include(p => p.ProgramParts).ThenInclude(pp => pp.Part)
            .Include(p => p.MachineAssignments).ThenInclude(a => a.Machine)
            .Where(p => p.ProgramType == ProgramType.BuildPlate
                && (p.Status == ProgramStatus.Active || p.Status == ProgramStatus.Draft)
                && (
                    // Unscheduled programs
                    (p.ScheduleStatus == ProgramScheduleStatus.None
                        || p.ScheduleStatus == ProgramScheduleStatus.Ready)
                    // Source/master programs with slicer data (reusable templates)
                    || (p.SourceProgramId == null && p.EstimatedPrintHours != null
                        && p.ScheduleStatus != ProgramScheduleStatus.Cancelled)
                ))
            .OrderByDescending(p => p.LastModifiedDate)
            .ToListAsync();
    }

    // ══════════════════════════════════════════════════════════
    // Slot Finding
    // ══════════════════════════════════════════════════════════

    public async Task<ProgramScheduleSlot> FindEarliestSlotAsync(
        int machineId, double durationHours, DateTime notBefore, int? forProgramId = null)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        var isContinuous = machine.AutoChangeoverEnabled;

        // Query program-based executions — exclude the program being rescheduled
        // so its old executions (not yet deleted) don't block its new slot.
        var existingExecutions = await _db.StageExecutions
            .Where(e => e.MachineId == machine.Id
                && e.MachineProgramId != null
                && (!forProgramId.HasValue || e.MachineProgramId != forProgramId.Value)
                && e.ScheduledStartAt != null
                && e.ScheduledEndAt != null
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed)
            .OrderBy(e => e.ScheduledStartAt)
            .Select(e => new { e.ScheduledStartAt, e.ScheduledEndAt, e.MachineProgramId })
            .ToListAsync();

        // Also include scheduled programs that don't yet have stage executions.
        // Only include programs that are truly committed to the timeline:
        //   - Must have ScheduledJobId (fully scheduled with job + stage executions), OR
        //   - Must be Printing/PostPrint (actively on the machine)
        // Programs in "Scheduled" status without a ScheduledJobId are incomplete
        // artifacts that should NOT block machine time.
        // Exclude the program being rescheduled (forProgramId) to avoid self-blocking.
        var existingPrograms = await _db.MachinePrograms
            .Where(p => p.MachineId == machine.Id
                && p.ProgramType == ProgramType.BuildPlate
                && p.ScheduledDate != null
                && p.EstimatedPrintHours != null
                && p.ScheduleStatus != ProgramScheduleStatus.Completed
                && p.ScheduleStatus != ProgramScheduleStatus.Cancelled
                && p.ScheduleStatus != ProgramScheduleStatus.None
                && p.ScheduleStatus != ProgramScheduleStatus.Ready
                && (p.ScheduledJobId != null
                    || p.ScheduleStatus == ProgramScheduleStatus.Printing
                    || p.ScheduleStatus == ProgramScheduleStatus.PostPrint)
                && (!forProgramId.HasValue || p.Id != forProgramId.Value))
            .OrderBy(p => p.ScheduledDate)
            .Select(p => new { p.Id, p.ScheduledDate, p.EstimatedPrintHours, p.SourceProgramId })
            .ToListAsync();

        var blocks = existingExecutions
            .Select(e => (Start: e.ScheduledStartAt!.Value, End: e.ScheduledEndAt!.Value, ProgramId: e.MachineProgramId!.Value))
            .ToList();

        foreach (var prog in existingPrograms)
        {
            var progStart = prog.ScheduledDate!.Value;
            var progEnd = isContinuous
                ? progStart.AddHours(prog.EstimatedPrintHours!.Value)
                : ShiftTimeHelper.AdvanceByWorkHours(progStart, prog.EstimatedPrintHours!.Value, shifts);
            if (!blocks.Any(b => b.Start == progStart))
                blocks.Add((progStart, progEnd, prog.Id));
        }

        blocks = blocks.OrderBy(b => b.Start).ToList();

        var changeoverMinutes = machine.AutoChangeoverEnabled ? machine.ChangeoverMinutes : 0;
        var operatorUnloadMinutes = machine.AutoChangeoverEnabled ? machine.OperatorUnloadMinutes : 0;
        var plateCapacity = machine.BuildPlateCapacity;
        var candidateStart = isContinuous ? notBefore : ShiftTimeHelper.SnapToNextShiftStart(notBefore, shifts);
        var candidateEnd = isContinuous
            ? candidateStart.AddHours(durationHours)
            : ShiftTimeHelper.AdvanceByWorkHours(candidateStart, durationHours, shifts);

        // Track consecutive off-shift changeovers to model cooldown chamber state.
        // With BuildPlateCapacity N, the chamber can hold N builds in cooldown.
        // The operator empties the chamber when they arrive at the start of their shift.
        // Only when MORE than N consecutive off-shift changeovers occur does the
        // chamber overflow and the machine go DOWN (the (N+1)th build has nowhere to go).
        var consecutiveOffShiftChangeovers = 0;

        foreach (var block in blocks)
        {
            var autoChangeoverEnd = changeoverMinutes > 0
                ? block.End.AddMinutes(changeoverMinutes)
                : block.End;

            DateTime blockEnd;
            if (changeoverMinutes > 0)
            {
                var changeoverInShift = ShiftTimeHelper.IsWithinShiftWindow(block.End, autoChangeoverEnd, shifts);

                if (changeoverInShift)
                {
                    // Operator present during changeover → they empty the chamber → reset counter
                    consecutiveOffShiftChangeovers = 0;
                    blockEnd = autoChangeoverEnd;
                }
                else
                {
                    consecutiveOffShiftChangeovers++;

                    if (consecutiveOffShiftChangeovers > plateCapacity)
                    {
                        // Chamber overflow — machine DOWN until operator arrives and unloads
                        var nextShift = ShiftTimeHelper.FindNextShiftStart(block.End, shifts);
                        blockEnd = (nextShift ?? autoChangeoverEnd).AddMinutes(operatorUnloadMinutes);
                        consecutiveOffShiftChangeovers = 0; // operator empties everything on arrival
                    }
                    else
                    {
                        // Chamber has space — auto-changeover works, just add swap time
                        blockEnd = autoChangeoverEnd;
                    }
                }
            }
            else
            {
                blockEnd = block.End;
            }

            if (candidateEnd <= block.Start)
                break;

            candidateStart = isContinuous ? blockEnd : ShiftTimeHelper.SnapToNextShiftStart(blockEnd, shifts);
            candidateEnd = isContinuous
                ? candidateStart.AddHours(durationHours)
                : ShiftTimeHelper.AdvanceByWorkHours(candidateStart, durationHours, shifts);
        }

        var changeoverStart = candidateEnd;
        var changeoverEnd = changeoverMinutes > 0
            ? candidateEnd.AddMinutes(changeoverMinutes)
            : candidateEnd;

        var operatorAvailable = await IsOperatorAvailableDuringWindowAsync(changeoverStart, changeoverEnd);

        // Compute downtime: only when chamber would be full on THIS build's changeover.
        // If the previous changeover was during shift (operator emptied chamber),
        // this overnight changeover is automatic — no downtime.
        DateTime? downtimeStart = null, downtimeEnd = null;
        if (!operatorAvailable && changeoverMinutes > 0)
        {
            // Check if the chamber would be full by counting how many consecutive
            // off-shift changeovers precede this one (including this one)
            var offShiftRun = 1; // this changeover is off-shift
            for (var i = blocks.Count - 1; i >= 0; i--)
            {
                var prevEnd = blocks[i].End;
                var prevChangeoverEnd = prevEnd.AddMinutes(changeoverMinutes);
                if (ShiftTimeHelper.IsWithinShiftWindow(prevEnd, prevChangeoverEnd, shifts))
                    break; // previous was in-shift, chamber was emptied
                offShiftRun++;
            }

            if (offShiftRun > plateCapacity)
            {
                // Chamber overflow — machine will go DOWN
                downtimeStart = changeoverEnd;
                var nextShift = ShiftTimeHelper.FindNextShiftStart(changeoverEnd, shifts);
                downtimeEnd = (nextShift ?? changeoverEnd).AddMinutes(operatorUnloadMinutes);
            }
            // else: chamber has space, auto-changeover works overnight — no downtime
        }

        return new ProgramScheduleSlot(
            candidateStart, candidateEnd,
            changeoverStart, changeoverEnd,
            machineId, operatorAvailable,
            downtimeStart, downtimeEnd);
    }

    public async Task<BestProgramSlot> FindBestSlotAsync(
        double durationHours, DateTime notBefore, string? machineType = null, int? forProgramId = null)
    {
        var query = _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling);

        if (!string.IsNullOrEmpty(machineType))
        {
            if (machineType == "SLS" || machineType == "Additive")
                query = query.Where(m => m.IsAdditiveMachine);
            else
                query = query.Where(m => m.MachineType == machineType);
        }

        var machines = await query
            .OrderBy(m => m.Priority)
            .ThenBy(m => m.Id)
            .ToListAsync();

        if (machines.Count == 0)
            throw new InvalidOperationException($"No machines of type '{machineType}' are available for scheduling.");

        BestProgramSlot? best = null;

        foreach (var machine in machines)
        {
            var slot = await FindEarliestSlotAsync(machine.Id, durationHours, notBefore, forProgramId);
            if (best is null || slot.PrintStart < best.Slot.PrintStart)
                best = new BestProgramSlot(slot, machine.Id, machine.Name ?? machine.MachineId);
        }

        return best!;
    }

    // ══════════════════════════════════════════════════════════
    // Timeline & Analysis
    // ══════════════════════════════════════════════════════════

    public async Task<List<ProgramTimelineEntry>> GetMachineTimelineAsync(int machineId, DateTime from, DateTime to)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var programExecutions = await _db.StageExecutions
            .Include(e => e.MachineProgram)
            .Where(e => e.MachineId == machineId
                && e.MachineProgramId != null
                && e.ScheduledStartAt != null
                && e.ScheduledEndAt != null
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed)
            .OrderBy(e => e.ScheduledStartAt)
            .ToListAsync();

        var changeoverMinutes = machine.AutoChangeoverEnabled ? machine.ChangeoverMinutes : 0;
        var plateCapacity = machine.BuildPlateCapacity;
        var timelineShifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        var entries = new List<ProgramTimelineEntry>();
        var consecutiveOffShift = 0;

        foreach (var exec in programExecutions)
        {
            // Prefer actual times for completed builds, fall back to scheduled times
            var printStart = exec.ActualStartAt ?? exec.ScheduledStartAt!.Value;
            var printEnd = exec.ActualEndAt ?? exec.ScheduledEndAt!.Value;

            if (printEnd < from || printStart > to)
                continue;

            DateTime? changeoverStart = null;
            DateTime? changeoverEnd = null;
            DateTime? downtimeStart = null;
            DateTime? downtimeEnd = null;

            if (changeoverMinutes > 0)
            {
                changeoverStart = printEnd;
                changeoverEnd = printEnd.AddMinutes(changeoverMinutes);

                var changeoverInShift = ShiftTimeHelper.IsWithinShiftWindow(changeoverStart.Value, changeoverEnd.Value, timelineShifts);

                if (changeoverInShift)
                {
                    consecutiveOffShift = 0;
                }
                else
                {
                    consecutiveOffShift++;
                    // Chamber overflow — machine goes DOWN until operator arrives
                    if (consecutiveOffShift > plateCapacity)
                    {
                        downtimeStart = changeoverEnd;
                        var nextShift = ShiftTimeHelper.FindNextShiftStart(changeoverEnd.Value, timelineShifts);
                        downtimeEnd = (nextShift ?? changeoverEnd).Value.AddMinutes(machine.OperatorUnloadMinutes);
                        consecutiveOffShift = 0;
                    }
                }
            }

            var programName = exec.MachineProgram?.Name ?? exec.MachineProgram?.ProgramNumber ?? $"Program #{exec.MachineProgramId}";
            var programType = exec.MachineProgram?.ProgramType ?? ProgramType.Standard;
            var scheduleStatus = exec.MachineProgram?.ScheduleStatus ?? ProgramScheduleStatus.Scheduled;

            entries.Add(new ProgramTimelineEntry(
                exec.MachineProgramId!.Value, programName, programType,
                printStart, printEnd,
                changeoverStart, changeoverEnd,
                scheduleStatus, exec.Id,
                HasChangeoverConflict: downtimeStart.HasValue,
                DowntimeStart: downtimeStart, DowntimeEnd: downtimeEnd));
        }

        // Fallback: include programs without stage executions but only if they
        // are truly committed to the timeline (have a job, or are Printing/PostPrint).
        // Programs in "Scheduled" status without a ScheduledJobId are incomplete
        // and should NOT appear on the timeline.
        var coveredProgramIds = entries.Select(e => e.MachineProgramId).ToHashSet();
        var orphanPrograms = await _db.MachinePrograms
            .Where(p => p.MachineId == machineId
                && p.ProgramType == ProgramType.BuildPlate
                && p.ScheduledDate != null
                && p.ScheduleStatus != ProgramScheduleStatus.Cancelled
                && p.ScheduleStatus != ProgramScheduleStatus.None
                && p.ScheduleStatus != ProgramScheduleStatus.Ready
                && (p.ScheduledJobId != null
                    || p.ScheduleStatus == ProgramScheduleStatus.Printing
                    || p.ScheduleStatus == ProgramScheduleStatus.PostPrint)
                && !coveredProgramIds.Contains(p.Id))
            .OrderBy(p => p.ScheduledDate)
            .ToListAsync();

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();

        foreach (var prog in orphanPrograms)
        {
            var printStart = prog.ScheduledDate!.Value;
            var printEnd = prog.EstimatedPrintHours.HasValue
                ? ShiftTimeHelper.AdvanceByWorkHours(printStart, prog.EstimatedPrintHours.Value, shifts)
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

            entries.Add(new ProgramTimelineEntry(
                prog.Id, prog.Name ?? prog.ProgramNumber, prog.ProgramType,
                printStart, printEnd,
                changeoverStart, changeoverEnd,
                prog.ScheduleStatus));
        }

        return entries.OrderBy(e => e.PrintStart).ToList();
    }

    public async Task<List<ScheduleOption>> GenerateScheduleOptionsAsync(
        int machineId, double baseDurationHours, DateTime notBefore,
        PartAdditiveBuildConfig? buildConfig = null, int demandQuantity = 0)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        var options = new List<ScheduleOption>();

        // Collect stack levels to evaluate
        var stackOptions = new List<(int Level, double Duration, int PartsPerBuild)>
        {
            (1, baseDurationHours, buildConfig?.PlannedPartsPerBuildSingle ?? 1)
        };

        if (buildConfig != null)
        {
            if (buildConfig.HasValidDoubleStack && buildConfig.DoubleStackDurationHours.HasValue)
                stackOptions.Add((2, buildConfig.DoubleStackDurationHours.Value, buildConfig.PlannedPartsPerBuildDouble ?? 1));
            if (buildConfig.HasValidTripleStack && buildConfig.TripleStackDurationHours.HasValue)
                stackOptions.Add((3, buildConfig.TripleStackDurationHours.Value, buildConfig.PlannedPartsPerBuildTriple ?? 1));
        }

        foreach (var (level, duration, partsPerBuild) in stackOptions)
        {
            // Option 1: Earliest available slot
            var slot = await FindEarliestSlotAsync(machineId, duration, notBefore);
            var score = ComputeOptionScore(slot, duration, level, partsPerBuild, demandQuantity, shifts, machine);

            options.Add(new ScheduleOption(
                Label: level == 1 ? "Earliest (Single)" : level == 2 ? "Earliest (Double Stack)" : "Earliest (Triple Stack)",
                Description: $"{slot.PrintStart:MMM d HH:mm} — {slot.PrintEnd:MMM d HH:mm} ({duration:F1}h, {partsPerBuild} parts)",
                Slot: slot,
                StackLevel: level,
                PartsPerBuild: partsPerBuild,
                DurationHours: duration,
                ChangeoverAligned: slot.OperatorAvailableForChangeover,
                IsWeekendOptimal: IsWeekendOptimal(slot, shifts),
                RecommendationScore: score));

            // Option 2: Shift-aligned slot (if changeover isn't already aligned)
            if (!slot.OperatorAvailableForChangeover && machine.AutoChangeoverEnabled)
            {
                var aligned = await FindShiftAlignedSlotAsync(machineId, duration, notBefore, shifts, machine);
                if (aligned != null)
                {
                    var alignedScore = ComputeOptionScore(aligned, duration, level, partsPerBuild, demandQuantity, shifts, machine);
                    alignedScore += 15; // Bonus for being aligned

                    options.Add(new ScheduleOption(
                        Label: level == 1 ? "Shift-Aligned (Single)" : level == 2 ? "Shift-Aligned (Double)" : "Shift-Aligned (Triple)",
                        Description: $"{aligned.PrintStart:MMM d HH:mm} — {aligned.PrintEnd:MMM d HH:mm} (changeover during operator hours)",
                        Slot: aligned,
                        StackLevel: level,
                        PartsPerBuild: partsPerBuild,
                        DurationHours: duration,
                        ChangeoverAligned: true,
                        IsWeekendOptimal: IsWeekendOptimal(aligned, shifts),
                        RecommendationScore: alignedScore));
                }
            }
        }

        // Deduplicate options with identical start times
        options = options
            .GroupBy(o => new { o.Slot.PrintStart, o.StackLevel })
            .Select(g => g.OrderByDescending(o => o.RecommendationScore).First())
            .OrderByDescending(o => o.RecommendationScore)
            .ToList();

        return options;
    }

    /// <summary>
    /// Finds a slot where buildEnd + changeover falls within a shift window.
    /// Tries shifting the start forward to align the changeover with the next shift.
    /// </summary>
    private async Task<ProgramScheduleSlot?> FindShiftAlignedSlotAsync(
        int machineId, double durationHours, DateTime notBefore,
        List<OperatingShift> shifts, Machine machine)
    {
        var changeoverMinutes = machine.ChangeoverMinutes;

        // Strategy: find when the next shift starts after the earliest possible end,
        // then work backward to find a start time where end + changeover = shift start
        var earliestSlot = await FindEarliestSlotAsync(machineId, durationHours, notBefore);
        var nextShift = ShiftTimeHelper.FindNextShiftStart(earliestSlot.PrintEnd, shifts);

        if (nextShift == null) return null;

        // Target: build should end such that changeover completes at or just after shift start
        var targetEnd = nextShift.Value.AddMinutes(-changeoverMinutes);
        if (targetEnd <= notBefore) return null;

        var targetStart = machine.AutoChangeoverEnabled
            ? targetEnd.AddHours(-durationHours)
            : targetEnd; // For shift-constrained, we'd need reverse-advance logic

        if (targetStart < notBefore) return null;

        // Verify this slot is actually available
        var verifySlot = await FindEarliestSlotAsync(machineId, durationHours, targetStart);
        if (Math.Abs((verifySlot.PrintStart - targetStart).TotalMinutes) < 30) // Within 30min tolerance
            return verifySlot;

        return null;
    }

    private int ComputeOptionScore(ProgramScheduleSlot slot, double durationHours,
        int stackLevel, int partsPerBuild, int demandQuantity,
        List<OperatingShift> shifts, Machine machine)
    {
        var score = 50; // Base score

        // Changeover alignment: +30 if operator available
        if (slot.OperatorAvailableForChangeover)
            score += 30;

        // Downtime penalty: penalize options that cause machine downtime
        // Cost = downtimeHours * ~3 points per hour (weekend = ~36h downtime = -108 pts, capped)
        if (!slot.OperatorAvailableForChangeover && slot.DowntimeStart.HasValue && slot.DowntimeEnd.HasValue)
        {
            var downtimeHours = (slot.DowntimeEnd.Value - slot.DowntimeStart.Value).TotalHours;
            score -= Math.Min((int)(downtimeHours * 3), 40); // Cap at -40 to avoid always negative
        }

        // Earliness: +20 if starts within 4 hours, scales down
        var hoursFromNow = (slot.PrintStart - DateTime.UtcNow).TotalHours;
        if (hoursFromNow < 4) score += 20;
        else if (hoursFromNow < 24) score += 10;

        // Demand fit: penalty if parts per build > remaining demand (overproduction)
        if (demandQuantity > 0 && partsPerBuild > demandQuantity)
        {
            var overproduction = (double)(partsPerBuild - demandQuantity) / partsPerBuild;
            score -= (int)(overproduction * 20); // Up to -20 for 100% overproduction
        }

        // Weekend optimization: bonus if a longer build spans the weekend cleanly
        if (IsWeekendOptimal(slot, shifts))
            score += 25;

        return Math.Clamp(score, 0, 100);
    }

    private static bool IsWeekendOptimal(ProgramScheduleSlot slot, List<OperatingShift> shifts)
    {
        if (!shifts.Any()) return false;
        var startDay = slot.PrintStart.DayOfWeek;
        var endDay = slot.PrintEnd.DayOfWeek;
        // Build spans a weekend if it starts Fri/Sat and ends Mon/Tue
        var spansWeekend = (startDay is DayOfWeek.Friday or DayOfWeek.Saturday)
            && (endDay is DayOfWeek.Monday or DayOfWeek.Tuesday);
        return spansWeekend && slot.OperatorAvailableForChangeover;
    }

    public async Task<List<BuildSequenceSuggestion>> SuggestBuildSequenceAsync(
        int machineId, List<BuildCandidate> candidates, DateTime horizonStart, DateTime horizonEnd)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        var changeoverMinutes = machine.AutoChangeoverEnabled ? machine.ChangeoverMinutes : 0;
        var suggestions = new List<BuildSequenceSuggestion>();

        var cursor = horizonStart;
        var remaining = candidates.ToDictionary(c => c.PartId, c => c.DemandQuantity);

        // Greedy: place builds one at a time, choosing the best stack level for each slot
        for (int iteration = 0; iteration < 50 && remaining.Values.Any(v => v > 0); iteration++)
        {
            if (cursor >= horizonEnd) break;

            // Find the next available slot on this machine
            var slot = await FindEarliestSlotAsync(machineId, 1, cursor); // 1h placeholder
            cursor = slot.PrintStart;
            if (cursor >= horizonEnd) break;

            // Determine if this is a weekend/overnight slot
            var startDay = cursor.DayOfWeek;
            var isPreWeekend = startDay is DayOfWeek.Friday or DayOfWeek.Saturday;

            // Pick the candidate with highest remaining demand
            var bestCandidate = candidates
                .Where(c => remaining.GetValueOrDefault(c.PartId, 0) > 0)
                .OrderByDescending(c => remaining[c.PartId])
                .FirstOrDefault();

            if (bestCandidate == null) break;

            // Choose stack level
            int stackLevel;
            string rationale;

            if (isPreWeekend && bestCandidate.StackOptions.Count > 1)
            {
                // Weekend: use ShiftTimeHelper to find the level that spans the weekend
                stackLevel = ShiftTimeHelper.SuggestWeekendStackLevel(
                    cursor, changeoverMinutes, shifts,
                    bestCandidate.StackOptions.Select(s => (s.Level, s.DurationHours, s.PartsPerBuild)).ToList());
                rationale = "Weekend-optimized: changeover aligns with Monday shift";
            }
            else if (changeoverMinutes > 0)
            {
                // Weekday: find duration that aligns changeover with next shift
                var alignedDuration = ShiftTimeHelper.FindChangeoverAlignedDuration(cursor, changeoverMinutes, shifts);
                if (alignedDuration != null)
                {
                    // Pick the stack level closest to the aligned duration
                    stackLevel = bestCandidate.StackOptions
                        .OrderBy(s => Math.Abs(s.DurationHours - alignedDuration.Value))
                        .First().Level;
                    rationale = $"Changeover-aligned: builds end so changeover falls during operator hours";
                }
                else
                {
                    stackLevel = bestCandidate.StackOptions[0].Level;
                    rationale = "Default stack level";
                }
            }
            else
            {
                stackLevel = bestCandidate.StackOptions[0].Level;
                rationale = "No changeover constraint";
            }

            var stackOpt = bestCandidate.StackOptions.FirstOrDefault(s => s.Level == stackLevel);
            if (stackOpt == default) stackOpt = bestCandidate.StackOptions[0];

            // Check demand: don't overproduce excessively
            var demandLeft = remaining[bestCandidate.PartId];
            if (stackOpt.PartsPerBuild > demandLeft * 1.5 && bestCandidate.StackOptions.Count > 1)
            {
                // Try a smaller stack to avoid overproduction
                var smaller = bestCandidate.StackOptions
                    .Where(s => s.PartsPerBuild <= demandLeft * 1.2)
                    .OrderByDescending(s => s.PartsPerBuild)
                    .FirstOrDefault();
                if (smaller != default)
                {
                    stackOpt = smaller;
                    stackLevel = smaller.Level;
                    rationale += " (reduced to limit overproduction)";
                }
            }

            // Calculate actual slot for this duration
            var actualSlot = await FindEarliestSlotAsync(machineId, stackOpt.DurationHours, cursor);

            suggestions.Add(new BuildSequenceSuggestion(
                bestCandidate.PartId,
                bestCandidate.PartNumber,
                stackLevel,
                stackOpt.PartsPerBuild,
                stackOpt.DurationHours,
                actualSlot.PrintStart,
                actualSlot.PrintEnd,
                actualSlot.OperatorAvailableForChangeover,
                rationale));

            remaining[bestCandidate.PartId] -= stackOpt.PartsPerBuild;
            cursor = actualSlot.ChangeoverEnd;
        }

        return suggestions;
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

        double? downtimeHours = null;

        if (!operatorAvailable)
        {
            var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
            var plateCapacity = machine.BuildPlateCapacity;

            // Count consecutive off-shift changeovers preceding this one
            // Only flag downtime if the chamber would overflow (> capacity)
            var offShiftRun = 1; // this changeover is off-shift
            var recentBuilds = await _db.MachinePrograms
                .Where(p => p.MachineId == machineId
                    && p.ProgramType == ProgramType.BuildPlate
                    && p.ScheduledDate != null
                    && p.ScheduleStatus != ProgramScheduleStatus.Cancelled
                    && p.ScheduledDate < buildEndTime)
                .OrderByDescending(p => p.ScheduledDate)
                .Take(plateCapacity + 1)
                .ToListAsync();

            foreach (var prev in recentBuilds)
            {
                var prevEnd = prev.ScheduledDate!.Value.AddHours(prev.EstimatedPrintHours ?? 0);
                var prevChangeoverEnd = prevEnd.AddMinutes(machine.ChangeoverMinutes);
                if (ShiftTimeHelper.IsWithinShiftWindow(prevEnd, prevChangeoverEnd, shifts))
                    break; // previous changeover was in-shift, operator emptied chamber
                offShiftRun++;
            }

            if (offShiftRun > plateCapacity)
            {
                // Chamber overflow — machine goes DOWN
                var nextShiftStart = ShiftTimeHelper.FindNextShiftStart(buildEndTime, shifts);
                if (nextShiftStart.HasValue)
                {
                    var machineReadyAt = nextShiftStart.Value.AddMinutes(machine.OperatorUnloadMinutes);
                    downtimeHours = (machineReadyAt - changeoverEnd).TotalHours;

                    var hoursUntilShift = (nextShiftStart.Value - buildEndTime).TotalHours;
                    if (hoursUntilShift > 0 && hoursUntilShift < 24)
                    {
                        suggestedDuration = hoursUntilShift;
                        var downtimeCost = downtimeHours.Value * (double)machine.HourlyRate;
                        suggestedAction = $"Consider a {hoursUntilShift:F1}h build (double-stack) to sync completion with shift start at {nextShiftStart.Value:t}. " +
                            $"Current schedule causes {downtimeHours.Value:F1}h downtime (${downtimeCost:F0} lost).";
                    }
                }
            }
            // else: chamber has room — auto-changeover works, operator empties on arrival
        }

        return new ChangeoverAnalysis(operatorAvailable, changeoverStart, changeoverEnd, suggestedAction, suggestedDuration, downtimeHours);
    }

    public async Task<List<ProgramChangeoverConflict>> DetectChangeoverConflictsAsync(int machineId, DateTime from, DateTime to)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        if (!machine.AutoChangeoverEnabled)
            return [];

        var timeline = await GetMachineTimelineAsync(machineId, from, to);
        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        var conflicts = new List<ProgramChangeoverConflict>();

        for (int i = 0; i < timeline.Count - 1; i++)
        {
            var current = timeline[i];
            var next = timeline[i + 1];

            if (current.ChangeoverStart == null || current.ChangeoverEnd == null)
                continue;

            var precedingOpAvailable = ShiftTimeHelper.IsWithinShiftWindow(current.ChangeoverStart.Value, current.ChangeoverEnd.Value, shifts);
            var followingOpAvailable = next.ChangeoverStart.HasValue && next.ChangeoverEnd.HasValue
                && ShiftTimeHelper.IsWithinShiftWindow(next.ChangeoverStart.Value, next.ChangeoverEnd.Value, shifts);

            if (!precedingOpAvailable)
            {
                var nextShift = ShiftTimeHelper.FindNextShiftStart(current.ChangeoverStart.Value, shifts);
                var operatorArrival = nextShift ?? current.ChangeoverEnd.Value;

                if (next.PrintEnd >= operatorArrival)
                {
                    var warning = $"Cooldown chamber conflict on {machine.Name}: " +
                        $"{current.ProgramName} finishes changeover at {current.ChangeoverEnd.Value:MM/dd HH:mm} " +
                        $"(outside operator hours). Next operator available at {operatorArrival:MM/dd HH:mm}, " +
                        $"but {next.ProgramName} finishes at {next.PrintEnd:MM/dd HH:mm} — machine will be down.";

                    conflicts.Add(new ProgramChangeoverConflict(
                        machine.Id, machine.Name,
                        current.ProgramName,
                        current.ChangeoverStart.Value, current.ChangeoverEnd.Value,
                        next.ProgramName, next.PrintStart,
                        precedingOpAvailable, followingOpAvailable,
                        warning));
                }
            }
        }

        return conflicts;
    }

    // ══════════════════════════════════════════════════════════
    // Stage Execution Management
    // ══════════════════════════════════════════════════════════

    public async Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int machineProgramId, string createdBy)
    {
        var program = await _db.MachinePrograms
            .Include(p => p.ProgramParts)
            .FirstOrDefaultAsync(p => p.Id == machineProgramId)
            ?? throw new InvalidOperationException("Machine program not found.");

        if (program.ScheduledJobId == null)
            throw new InvalidOperationException("Program must be scheduled before creating stage executions.");

        var job = await _db.Jobs.FindAsync(program.ScheduledJobId)
            ?? throw new InvalidOperationException("Scheduled job not found.");

        var executions = await _db.StageExecutions
            .Where(e => e.JobId == job.Id)
            .OrderBy(e => e.SortOrder)
            .ToListAsync();

        foreach (var exec in executions)
        {
            var stage = await _db.ProductionStages.FindAsync(exec.ProductionStageId);
            if (stage != null)
            {
                exec.BatchGroupId = $"{stage.StageSlug.ToUpperInvariant()}-{machineProgramId}";
                exec.BatchPartCount = program.TotalPartCount;
            }
        }

        await _db.SaveChangesAsync();
        return executions;
    }

    public async Task<ProgramPlateReleaseResult> ReleasePlateAsync(int machineProgramId, string releasedBy)
    {
        var program = await _db.MachinePrograms
            .Include(p => p.ProgramParts).ThenInclude(pp => pp.Part)
            .Include(p => p.ProgramParts).ThenInclude(pp => pp.WorkOrderLine)
            .FirstOrDefaultAsync(p => p.Id == machineProgramId)
            ?? throw new InvalidOperationException("Machine program not found.");

        if (program.ScheduleStatus != ProgramScheduleStatus.PostPrint)
            throw new InvalidOperationException($"Program must be in PostPrint status to release. Current: {program.ScheduleStatus}");

        // Verify all stage executions are completed
        var executions = await _db.StageExecutions
            .Where(e => e.MachineProgramId == machineProgramId
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped)
            .CountAsync();

        if (executions > 0)
            throw new InvalidOperationException($"Cannot release plate: {executions} stage execution(s) not yet completed.");

        var createdInstances = new List<PartInstance>();
        var createdJobs = new List<Job>();
        var instanceIndex = 0;

        foreach (var progPart in program.ProgramParts)
        {
            for (var i = 0; i < progPart.Quantity; i++)
            {
                instanceIndex++;
                var trackingId = await _serialNumberService.GenerateTemporaryTrackingIdAsync(machineProgramId, instanceIndex);

                var instance = new PartInstance
                {
                    PartId = progPart.PartId,
                    WorkOrderLineId = progPart.WorkOrderLineId ?? 0,
                    TemporaryTrackingId = trackingId,
                    Status = PartInstanceStatus.InProcess,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = releasedBy
                };

                _db.PartInstances.Add(instance);
                createdInstances.Add(instance);
            }
        }

        program.PlateReleasedAt = DateTime.UtcNow;
        program.ScheduleStatus = ProgramScheduleStatus.Completed;
        program.LastModifiedDate = DateTime.UtcNow;
        program.LastModifiedBy = releasedBy;

        if (program.ScheduledJobId.HasValue)
        {
            var buildJob = await _db.Jobs.FindAsync(program.ScheduledJobId.Value);
            if (buildJob != null && buildJob.Status != JobStatus.Completed)
            {
                buildJob.Status = JobStatus.Completed;
                buildJob.ActualEnd = DateTime.UtcNow;
                buildJob.LastModifiedDate = DateTime.UtcNow;
                buildJob.LastModifiedBy = releasedBy;
            }
        }

        await _db.SaveChangesAsync();

        return new ProgramPlateReleaseResult(machineProgramId, createdInstances, createdJobs, createdInstances.Count);
    }

    // ══════════════════════════════════════════════════════════
    // Program Locking
    // ══════════════════════════════════════════════════════════

    public async Task LockProgramAsync(int machineProgramId, string lockedBy)
    {
        var program = await _db.MachinePrograms.FindAsync(machineProgramId)
            ?? throw new InvalidOperationException("Machine program not found.");

        if (program.IsLocked)
            throw new InvalidOperationException("Program is already locked.");

        program.IsLocked = true;
        program.LastModifiedDate = DateTime.UtcNow;
        program.LastModifiedBy = lockedBy;
        await _db.SaveChangesAsync();
    }

    public async Task UnlockProgramAsync(int machineProgramId, string unlockedBy, string reason)
    {
        var program = await _db.MachinePrograms.FindAsync(machineProgramId)
            ?? throw new InvalidOperationException("Machine program not found.");

        if (!program.IsLocked)
            throw new InvalidOperationException("Program is not locked.");

        if (program.ScheduleStatus == ProgramScheduleStatus.Printing || program.ScheduleStatus == ProgramScheduleStatus.PostPrint)
            throw new InvalidOperationException("Cannot unlock a program that is printing or in post-print.");

        program.IsLocked = false;
        program.ScheduleStatus = ProgramScheduleStatus.Ready;
        program.ScheduledDate = null;
        program.PredecessorProgramId = null;
        program.LastModifiedDate = DateTime.UtcNow;
        program.LastModifiedBy = unlockedBy;

        // Cancel outstanding stage executions
        var activeExecutions = await _db.StageExecutions
            .Where(e => e.MachineProgramId == machineProgramId
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed)
            .ToListAsync();

        foreach (var exec in activeExecutions)
        {
            exec.Status = StageExecutionStatus.Skipped;
            exec.Notes = $"Auto-skipped: program unlocked by {unlockedBy} — {reason}";
            exec.CompletedAt = DateTime.UtcNow;
            exec.ActualEndAt = DateTime.UtcNow;
            exec.LastModifiedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    // ══════════════════════════════════════════════════════════
    // Private Helpers
    // ══════════════════════════════════════════════════════════

    private async Task<MachineProgram> CreateScheduledCopyAsync(int sourceProgramId, string createdBy)
    {
        var source = await _db.MachinePrograms
            .Include(p => p.ProgramParts)
            .FirstOrDefaultAsync(p => p.Id == sourceProgramId)
            ?? throw new InvalidOperationException("Source program not found.");

        var copy = new MachineProgram
        {
            ProgramType = source.ProgramType,
            PartId = source.PartId,
            MachineId = source.MachineId,
            MachineType = source.MachineType,
            ProgramNumber = $"{source.ProgramNumber}-RUN",
            Name = $"{source.Name} (Run)",
            Description = source.Description,
            Version = source.Version,
            Status = ProgramStatus.Active,
            ScheduleStatus = ProgramScheduleStatus.Ready,
            EstimatedPrintHours = source.EstimatedPrintHours,
            LayerCount = source.LayerCount,
            BuildHeightMm = source.BuildHeightMm,
            EstimatedPowderKg = source.EstimatedPowderKg,
            SlicerFileName = source.SlicerFileName,
            SlicerSoftware = source.SlicerSoftware,
            SlicerVersion = source.SlicerVersion,
            PartPositionsJson = source.PartPositionsJson,
            MaterialId = source.MaterialId,
            DepowderProgramId = source.DepowderProgramId,
            EdmProgramId = source.EdmProgramId,
            SourceProgramId = sourceProgramId,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        _db.MachinePrograms.Add(copy);
        await _db.SaveChangesAsync();

        // Copy program parts — do NOT copy WorkOrderLineId since each scheduled run
        // gets its own WO fulfillment links via LinkProgramPartsToWorkOrdersAsync
        foreach (var srcPart in source.ProgramParts)
        {
            _db.ProgramParts.Add(new ProgramPart
            {
                MachineProgramId = copy.Id,
                PartId = srcPart.PartId,
                Quantity = srcPart.Quantity,
                StackLevel = srcPart.StackLevel,
                PositionNotes = srcPart.PositionNotes
            });
        }

        await _db.SaveChangesAsync();
        return copy;
    }

    private async Task<List<int>> CreateProgramPartJobsAsync(
        MachineProgram program,
        List<ManufacturingProcess> processes,
        DateTime startAfter)
    {
        // Use first active process per part (a part could have multiple process versions)
        var processLookup = processes
            .GroupBy(p => p.PartId)
            .ToDictionary(g => g.Key, g => g.First());
        var createdJobIds = new List<int>();

        var machineLookupById = await _db.Machines.Where(m => m.IsActive).ToDictionaryAsync(m => m.Id, m => m);
        var machineIdLookup = await _db.Machines.Where(m => m.IsActive).ToDictionaryAsync(m => m.MachineId, m => m);

        var partGroups = program.ProgramParts
            .GroupBy(pp => new { pp.PartId, pp.WorkOrderLineId })
            .ToList();

        foreach (var group in partGroups)
        {
            var representative = group.First();
            var totalQuantity = group.Sum(pp => pp.Quantity);

            if (!processLookup.TryGetValue(group.Key.PartId, out var process))
                continue;

            var batchStages = process.Stages.Where(s => s.ProcessingLevel == ProcessingLevel.Batch).OrderBy(s => s.ExecutionOrder).ToList();
            var partStages = process.Stages.Where(s => s.ProcessingLevel == ProcessingLevel.Part).OrderBy(s => s.ExecutionOrder).ToList();

            if (batchStages.Count == 0 && partStages.Count == 0) continue;

            var batchCapacity = Math.Max(1, process.DefaultBatchCapacity);
            var batchCount = (int)Math.Ceiling((double)totalQuantity / batchCapacity);

            var totalEstimatedHours = 0.0;
            foreach (var stage in batchStages.Concat(partStages))
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

            foreach (var stage in batchStages.Concat(partStages))
            {
                var effectiveCapacity = stage.BatchCapacityOverride ?? batchCapacity;
                var stageBatchCount = (int)Math.Ceiling((double)totalQuantity / effectiveCapacity);
                var dur = _processService.CalculateStageDuration(stage, totalQuantity, stageBatchCount, null);
                var estimatedHours = dur.TotalMinutes / 60.0;

                int? stageMachineId = ResolveStageMachine(stage, machineLookupById, machineIdLookup);
                var scheduledEnd = currentStart.AddHours(estimatedHours);

                var costEstimate = await _costService.EstimateCostAsync(stage.ProductionStageId, estimatedHours, totalQuantity, stageBatchCount);

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

            partJob.ScheduledEnd = currentStart;
            partJob.EstimatedHours = (currentStart - startAfter).TotalHours;
            createdJobIds.Add(partJob.Id);
        }

        await _db.SaveChangesAsync();
        return createdJobIds;
    }

    private static int? ResolveStageMachine(ProcessStage stage, Dictionary<int, Machine> byIntId, Dictionary<string, Machine> byStringId)
    {
        if (stage.AssignedMachineId.HasValue)
            return stage.AssignedMachineId.Value;

        if (!string.IsNullOrEmpty(stage.ProductionStage?.DefaultMachineId)
            && byStringId.TryGetValue(stage.ProductionStage.DefaultMachineId, out var machine))
            return machine.Id;

        return null;
    }

    private async Task<int> GetOrCreatePrintStageIdAsync()
    {
        // Check both slugs: "sls-printing" (seed data) and "sls-print" (legacy)
        var printStage = await _db.ProductionStages.FirstOrDefaultAsync(
            s => s.StageSlug == "sls-printing" || s.StageSlug == "sls-print");
        if (printStage != null) return printStage.Id;

        printStage = new ProductionStage
        {
            Name = "SLS Printing",
            StageSlug = "sls-printing",
            IsActive = true
        };
        _db.ProductionStages.Add(printStage);
        await _db.SaveChangesAsync();
        return printStage.Id;
    }

    private async Task<int> GetOrCreateDefaultStageIdAsync(string machineType)
    {
        var slug = $"{machineType.ToLower()}-run";
        var stage = await _db.ProductionStages.FirstOrDefaultAsync(s => s.StageSlug == slug);
        if (stage != null) return stage.Id;

        stage = new ProductionStage
        {
            Name = $"{machineType} Run",
            StageSlug = slug,
            IsActive = true
        };
        _db.ProductionStages.Add(stage);
        await _db.SaveChangesAsync();
        return stage.Id;
    }

    private async Task<bool> IsOperatorAvailableDuringWindowAsync(DateTime windowStart, DateTime windowEnd)
    {
        var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        return ShiftTimeHelper.IsWithinShiftWindow(windowStart, windowEnd, shifts);
    }

    // Shift time helpers are now in ShiftTimeHelper.cs (shared with SchedulingService)

    // ══════════════════════════════════════════════════════════
    // Smart Reschedule
    // ══════════════════════════════════════════════════════════

    public async Task<SmartRescheduleResult> SmartRescheduleBuildsAsync(
        string userName,
        IProgress<(int current, int total, string status)>? progress = null)
    {
        var actions = new List<SmartRescheduleAction>();

        // Get all movable builds — Scheduled or Ready BuildPlates with slicer data
        var movableBuilds = await _db.MachinePrograms
            .Include(p => p.Machine)
            .Where(p => p.ProgramType == ProgramType.BuildPlate
                && p.ScheduledDate != null
                && p.EstimatedPrintHours != null && p.EstimatedPrintHours > 0
                && (p.ScheduleStatus == ProgramScheduleStatus.Scheduled
                    || p.ScheduleStatus == ProgramScheduleStatus.Ready))
            .OrderBy(p => p.ScheduledDate)
            .ToListAsync();

        if (!movableBuilds.Any())
            return new SmartRescheduleResult(0, 0, 0, 0, 0, [], 0);

        var total = movableBuilds.Count;
        progress?.Report((0, total, $"Unlocking {total} build(s)..."));

        // Step 1: Unlock all movable builds so they can be rescheduled fresh
        foreach (var build in movableBuilds)
        {
            try
            {
                if (build.IsLocked)
                    await UnlockProgramAsync(build.Id, userName, "Smart reschedule");
            }
            catch { /* Already unlocked or can't unlock — skip */ }
        }

        // Step 2: Re-schedule each build to the best available slot across all SLS machines.
        var buildsToSchedule = await _db.MachinePrograms
            .Include(p => p.Machine)
            .Where(p => p.ProgramType == ProgramType.BuildPlate
                && p.EstimatedPrintHours != null && p.EstimatedPrintHours > 0
                && (p.ScheduleStatus == ProgramScheduleStatus.Ready
                    || p.ScheduleStatus == ProgramScheduleStatus.None))
            .OrderBy(p => p.CreatedDate)
            .ToListAsync();

        // Build a machine name lookup so we don't need extra queries per build
        var machineNames = await _db.Machines
            .Where(m => m.IsActive)
            .ToDictionaryAsync(m => m.Id, m => m.Name);

        var scheduled = 0;
        for (var i = 0; i < buildsToSchedule.Count; i++)
        {
            var build = buildsToSchedule[i];

            // Report progress BEFORE the heavy work (safe — not mid-DB-operation)
            progress?.Report((i, buildsToSchedule.Count,
                $"Scheduling {i + 1} of {buildsToSchedule.Count}: {build.Name ?? build.ProgramNumber}..."));
            await Task.Yield(); // Keep Blazor circuit alive between builds

            try
            {
                var oldMachineName = build.MachineId.HasValue && machineNames.TryGetValue(build.MachineId.Value, out var mn)
                    ? mn : "Unscheduled";
                var oldStart = build.ScheduledDate ?? DateTime.MinValue;

                // Find best slot across ALL SLS machines
                var result = await ScheduleBuildPlateAutoMachineAsync(build.Id);

                var newMachineName = machineNames.GetValueOrDefault(result.Slot.MachineId, "?");
                var changeoverSafe = result.Slot.OperatorAvailableForChangeover;

                actions.Add(new SmartRescheduleAction(
                    build.Id, build.Name ?? build.ProgramNumber ?? $"Build #{build.Id}",
                    "Scheduled", oldMachineName, newMachineName, oldStart, result.Slot.PrintStart,
                    changeoverSafe ? "Changeover safe" : "Changeover outside shift"));

                scheduled++;
            }
            catch (Exception ex)
            {
                actions.Add(new SmartRescheduleAction(
                    build.Id, build.Name ?? build.ProgramNumber ?? $"Build #{build.Id}",
                    "Failed", "", "", DateTime.MinValue, DateTime.MinValue,
                    $"Failed: {ex.Message}"));
            }
        }

        var conflictsFree = actions.Count(a => a.Reason.Contains("safe"));

        progress?.Report((buildsToSchedule.Count, buildsToSchedule.Count, "Complete"));

        return new SmartRescheduleResult(
            total, scheduled, 0, conflictsFree, 0, actions, 0);
    }

    // ── Draft Programs (Engineer → Scheduler Handoff) ────────

    public async Task<List<MachineProgram>> GetDraftProgramsAwaitingScheduleAsync()
    {
        return await _db.MachinePrograms
            .Include(p => p.ProgramParts).ThenInclude(pp => pp.Part)
            .Include(p => p.Machine)
            .Where(p => p.ProgramType == ProgramType.BuildPlate
                && p.ScheduleStatus == ProgramScheduleStatus.None
                && p.Status == ProgramStatus.Draft)
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();
    }
}
