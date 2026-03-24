using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

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

        return await ScheduleBuildPlateAsync(machineProgramId, bestSlot.MachineId, startAfter);
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

        var machineLookupById = await _db.Machines
            .Where(m => m.IsActive)
            .ToDictionaryAsync(m => m.Id, m => m);
        var machineIdLookup = await _db.Machines
            .Where(m => m.IsActive && m.MachineId != null)
            .ToDictionaryAsync(m => m.MachineId!, m => m);

        foreach (var stage in stages)
        {
            var programId = stage.MachineProgramId;
            if (!programId.HasValue)
            {
                var bestProgram = await _programService.GetBestProgramForStageAsync(
                    part.Id, preferredMachineId, stage.ProductionStageId);
                programId = bestProgram?.Id;
            }

            var durationResult = await _processService.CalculateStageDurationWithProgramAsync(
                stage, quantity, batchCount: 1, buildConfigHours: null, programId);
            var estimatedHours = durationResult.TotalMinutes / 60.0;

            int? stageMachineId = preferredMachineId ?? ResolveStageMachine(stage, machineLookupById, machineIdLookup);

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

        job.ScheduledEnd = currentStart;
        job.EstimatedHours = (currentStart - notBefore).TotalHours;
        line.Jobs.Add(job);
        await _db.SaveChangesAsync();

        return new WorkOrderScheduleResult(job.Id, job.JobNumber!, executions, notBefore, currentStart, warnings);
    }

    public async Task<List<MachineProgram>> GetAvailableProgramsForPartAsync(int partId)
    {
        // Find BuildPlate programs that:
        // 1. Contain this part
        // 2. Are in None or Ready status (not already scheduled/completed)
        // 3. Have the part as an active ProgramPart entry
        return await _db.MachinePrograms
            .Include(p => p.ProgramParts)
            .Include(p => p.MachineAssignments)
            .Where(p => p.ProgramType == ProgramType.BuildPlate
                && (p.ScheduleStatus == ProgramScheduleStatus.None
                    || p.ScheduleStatus == ProgramScheduleStatus.Ready)
                && p.Status == ProgramStatus.Active
                && p.ProgramParts.Any(pp => pp.PartId == partId))
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

        // Query program-based executions
        var existingExecutions = await _db.StageExecutions
            .Where(e => e.MachineId == machine.Id
                && e.MachineProgramId != null
                && e.ScheduledStartAt != null
                && e.ScheduledEndAt != null
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed)
            .OrderBy(e => e.ScheduledStartAt)
            .Select(e => new { e.ScheduledStartAt, e.ScheduledEndAt, e.MachineProgramId })
            .ToListAsync();

        // Also include scheduled programs that don't yet have stage executions
        var existingPrograms = await _db.MachinePrograms
            .Where(p => p.MachineId == machine.Id
                && p.ProgramType == ProgramType.BuildPlate
                && p.ScheduledDate != null
                && p.EstimatedPrintHours != null
                && p.ScheduleStatus != ProgramScheduleStatus.Completed
                && p.ScheduleStatus != ProgramScheduleStatus.Cancelled)
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
        var candidateStart = isContinuous ? notBefore : ShiftTimeHelper.SnapToNextShiftStart(notBefore, shifts);
        var candidateEnd = isContinuous
            ? candidateStart.AddHours(durationHours)
            : ShiftTimeHelper.AdvanceByWorkHours(candidateStart, durationHours, shifts);

        foreach (var block in blocks)
        {
            var blockEnd = changeoverMinutes > 0
                ? block.End.AddMinutes(changeoverMinutes)
                : block.End;

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

        return new ProgramScheduleSlot(
            candidateStart, candidateEnd,
            changeoverStart, changeoverEnd,
            machineId, operatorAvailable);
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
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed)
            .OrderBy(e => e.ScheduledStartAt)
            .ToListAsync();

        var changeoverMinutes = machine.AutoChangeoverEnabled ? machine.ChangeoverMinutes : 0;
        var entries = new List<ProgramTimelineEntry>();

        foreach (var exec in programExecutions)
        {
            var printStart = exec.ScheduledStartAt!.Value;
            var printEnd = exec.ScheduledEndAt!.Value;

            if (printEnd < from || printStart > to)
                continue;

            DateTime? changeoverStart = null;
            DateTime? changeoverEnd = null;

            if (changeoverMinutes > 0)
            {
                changeoverStart = printEnd;
                changeoverEnd = printEnd.AddMinutes(changeoverMinutes);
            }

            var programName = exec.MachineProgram?.Name ?? exec.MachineProgram?.ProgramNumber ?? $"Program #{exec.MachineProgramId}";
            var programType = exec.MachineProgram?.ProgramType ?? ProgramType.Standard;
            var scheduleStatus = exec.MachineProgram?.ScheduleStatus ?? ProgramScheduleStatus.Scheduled;

            entries.Add(new ProgramTimelineEntry(
                exec.MachineProgramId!.Value, programName, programType,
                printStart, printEnd,
                changeoverStart, changeoverEnd,
                scheduleStatus, exec.Id));
        }

        // Fallback: include programs without stage executions
        var coveredProgramIds = entries.Select(e => e.MachineProgramId).ToHashSet();
        var orphanPrograms = await _db.MachinePrograms
            .Where(p => p.MachineId == machineId
                && p.ProgramType == ProgramType.BuildPlate
                && p.ScheduledDate != null
                && p.ScheduleStatus != ProgramScheduleStatus.Cancelled
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
            var shifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
            var nextShiftStart = ShiftTimeHelper.FindNextShiftStart(buildEndTime, shifts);

            if (nextShiftStart.HasValue)
            {
                var hoursUntilShift = (nextShiftStart.Value - buildEndTime).TotalHours;
                if (hoursUntilShift > 0 && hoursUntilShift < 24)
                {
                    suggestedDuration = hoursUntilShift;
                    suggestedAction = $"Consider a {hoursUntilShift:F1}h build (double-stack) to sync completion with shift start at {nextShiftStart.Value:t}";
                }
            }
        }

        return new ChangeoverAnalysis(operatorAvailable, changeoverStart, changeoverEnd, suggestedAction, suggestedDuration);
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

        // Copy program parts
        foreach (var srcPart in source.ProgramParts)
        {
            _db.ProgramParts.Add(new ProgramPart
            {
                MachineProgramId = copy.Id,
                PartId = srcPart.PartId,
                Quantity = srcPart.Quantity,
                StackLevel = srcPart.StackLevel,
                PositionNotes = srcPart.PositionNotes,
                WorkOrderLineId = srcPart.WorkOrderLineId
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
        var processLookup = processes.ToDictionary(p => p.PartId, p => p);
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

            var batchCapacity = process.DefaultBatchCapacity;
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
        var printStage = await _db.ProductionStages.FirstOrDefaultAsync(s => s.StageSlug == "sls-print");
        if (printStage != null) return printStage.Id;

        printStage = new ProductionStage
        {
            Name = "SLS Print",
            StageSlug = "sls-print",
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
}
