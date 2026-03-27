using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

/// <summary>
/// Program planning service for BuildPlate (SLS) programs.
/// Handles CRUD, part assignment, slicer data, revisions, and scheduled copies.
/// </summary>
public class ProgramPlanningService : IProgramPlanningService
{
    private readonly TenantDbContext _db;
    private readonly INumberSequenceService _numberSeq;
    private readonly IMachineProgramService _programService;

    public ProgramPlanningService(
        TenantDbContext db,
        INumberSequenceService numberSeq,
        IMachineProgramService programService)
    {
        _db = db;
        _numberSeq = numberSeq;
        _programService = programService;
    }

    // ═══════════════════════════════════════════════════════════
    // Build Plate CRUD
    // ═══════════════════════════════════════════════════════════

    public async Task<List<MachineProgram>> GetAllBuildPlateProgramsAsync()
    {
        return await _db.MachinePrograms
            .Include(p => p.Machine)
            .Include(p => p.Material)
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.Part)
                    .ThenInclude(part => part.AdditiveBuildConfig)
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.Part)
                    .ThenInclude(part => part.MaterialEntity)
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.WorkOrderLine)
                    .ThenInclude(wl => wl!.WorkOrder)
            .Include(p => p.ScheduledJob)
            .Where(p => p.ProgramType == ProgramType.BuildPlate)
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();
    }

    public async Task<List<MachineProgram>> GetBuildPlatesForPartAsync(int partId)
    {
        return await _db.MachinePrograms
            .Include(p => p.Machine)
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.Part)
            .Where(p => p.ProgramType == ProgramType.BuildPlate
                && p.ProgramParts.Any(pp => pp.PartId == partId))
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();
    }

    public async Task<MachineProgram?> GetBuildPlateByIdAsync(int id)
    {
        return await _db.MachinePrograms
            .Include(p => p.Machine)
            .Include(p => p.Material)
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.Part)
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.WorkOrderLine)
                    .ThenInclude(wl => wl!.WorkOrder)
            .Include(p => p.ScheduledJob)
            .Include(p => p.SourceProgram)
            .Include(p => p.PredecessorProgram)
            .FirstOrDefaultAsync(p => p.Id == id && p.ProgramType == ProgramType.BuildPlate);
    }

    public async Task<MachineProgram> CreateBuildPlateAsync(MachineProgram program, string createdBy)
    {
        program.ProgramType = ProgramType.BuildPlate;
        program.Status = ProgramStatus.Draft;
        program.ScheduleStatus = ProgramScheduleStatus.None;
        program.CreatedBy = createdBy;
        program.LastModifiedBy = createdBy;
        program.CreatedDate = DateTime.UtcNow;
        program.LastModifiedDate = DateTime.UtcNow;

        if (string.IsNullOrEmpty(program.ProgramNumber))
            program.ProgramNumber = await _numberSeq.NextAsync("BuildPlate");

        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();

        await CreateRevisionAsync(program.Id, createdBy, "Created build plate program");
        return program;
    }

    public async Task<MachineProgram> UpdateBuildPlateAsync(MachineProgram program, string modifiedBy)
    {
        var existing = await _db.MachinePrograms.FindAsync(program.Id)
            ?? throw new InvalidOperationException("Program not found.");

        if (existing.IsLocked)
            throw new InvalidOperationException("Cannot modify a locked program.");

        existing.Name = program.Name;
        existing.Description = program.Description;
        existing.MachineId = program.MachineId;
        existing.MaterialId = program.MaterialId;
        existing.Notes = program.Notes;
        existing.LastModifiedBy = modifiedBy;
        existing.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task LinkProgramPartsToWorkOrdersAsync(int programId, Dictionary<int, int?> partIdToWorkOrderLineId)
    {
        var program = await _db.MachinePrograms
            .Include(p => p.ProgramParts)
            .FirstOrDefaultAsync(p => p.Id == programId)
            ?? throw new InvalidOperationException("Program not found.");

        foreach (var pp in program.ProgramParts)
        {
            if (partIdToWorkOrderLineId.TryGetValue(pp.PartId, out var woLineId) && woLineId.HasValue)
                pp.WorkOrderLineId = woLineId;
        }

        program.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteBuildPlateAsync(int programId)
    {
        var program = await _db.MachinePrograms.FindAsync(programId)
            ?? throw new InvalidOperationException("Program not found.");

        if (program.IsLocked)
            throw new InvalidOperationException("Cannot delete a locked program.");

        if (program.ScheduleStatus != ProgramScheduleStatus.None && 
            program.ScheduleStatus != ProgramScheduleStatus.Ready)
            throw new InvalidOperationException("Cannot delete a scheduled program.");

        program.Status = ProgramStatus.Archived;
        program.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteBuildWithDownstreamAsync(int programId, string deletedBy)
    {
        var program = await _db.MachinePrograms
            .Include(p => p.ProgramParts)
            .FirstOrDefaultAsync(p => p.Id == programId)
            ?? throw new InvalidOperationException("Program not found.");

        // 1. Cancel all stage executions linked to this program (by MachineProgramId or by build JobId)
        var buildJobId = program.ScheduledJobId;
        var executions = await _db.StageExecutions
            .Where(e => (e.MachineProgramId == programId || (buildJobId.HasValue && e.JobId == buildJobId.Value))
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Failed)
            .ToListAsync();

        foreach (var exec in executions)
        {
            exec.Status = StageExecutionStatus.Skipped;
            exec.Notes = $"Cancelled: build deleted by {deletedBy}";
            exec.CompletedAt = DateTime.UtcNow;
            exec.LastModifiedDate = DateTime.UtcNow;
        }

        // 2. Cancel the build-level job
        if (program.ScheduledJobId.HasValue)
        {
            var buildJob = await _db.Jobs.FindAsync(program.ScheduledJobId.Value);
            if (buildJob != null && buildJob.Status != JobStatus.Completed)
            {
                buildJob.Status = JobStatus.Cancelled;
                buildJob.LastModifiedDate = DateTime.UtcNow;
                buildJob.LastModifiedBy = deletedBy;
            }
        }

        // 3. Cancel downstream per-part jobs created from this program
        var partIds = program.ProgramParts.Select(pp => pp.PartId).Distinct().ToList();
        if (partIds.Any())
        {
            // Match by Notes containing the program number (set during CreateProgramPartJobsAsync),
            // or by time-window from the build job as a fallback
            var programNumber = program.ProgramNumber ?? "";
            var notePattern = !string.IsNullOrEmpty(programNumber)
                ? $"(program {programNumber})"
                : null;

            var query = _db.Jobs
                .Where(j => partIds.Contains(j.PartId)
                    && j.Scope == JobScope.Part
                    && j.Status != JobStatus.Completed
                    && j.Status != JobStatus.Cancelled);

            List<Job> downstreamJobs;
            if (notePattern != null)
            {
                // Primary: match by program reference in Notes
                downstreamJobs = await query
                    .Where(j => j.Notes != null && j.Notes.Contains(notePattern))
                    .ToListAsync();
            }
            else if (program.ScheduledJobId.HasValue)
            {
                // Fallback: time-window from build job (generous 60s window for auto-schedule loops)
                var buildJob = await _db.Jobs.FindAsync(program.ScheduledJobId.Value);
                if (buildJob != null)
                {
                    downstreamJobs = await query
                        .Where(j => j.CreatedDate >= buildJob.CreatedDate.AddSeconds(-1)
                            && j.CreatedDate <= buildJob.CreatedDate.AddSeconds(60))
                        .ToListAsync();
                }
                else
                {
                    downstreamJobs = [];
                }
            }
            else
            {
                downstreamJobs = [];
            }

            foreach (var job in downstreamJobs)
            {
                job.Status = JobStatus.Cancelled;
                job.LastModifiedDate = DateTime.UtcNow;
                job.LastModifiedBy = deletedBy;

                // Cancel their stage executions too
                var jobExecs = await _db.StageExecutions
                    .Where(e => e.JobId == job.Id
                        && e.Status != StageExecutionStatus.Completed
                        && e.Status != StageExecutionStatus.Failed)
                    .ToListAsync();

                foreach (var exec in jobExecs)
                {
                    exec.Status = StageExecutionStatus.Skipped;
                    exec.Notes = $"Cancelled: upstream build deleted by {deletedBy}";
                    exec.CompletedAt = DateTime.UtcNow;
                    exec.LastModifiedDate = DateTime.UtcNow;
                }
            }
        }

        // 4. Unlock and archive the program
        program.IsLocked = false;
        program.ScheduleStatus = ProgramScheduleStatus.Cancelled;
        program.Status = ProgramStatus.Archived;
        program.LastModifiedDate = DateTime.UtcNow;
        program.LastModifiedBy = deletedBy;

        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // Scheduled Copies (Print Runs)
    // ═══════════════════════════════════════════════════════════

    public async Task<MachineProgram> CreateScheduledCopyAsync(int sourceProgramId, string createdBy, int? workOrderLineId = null)
    {
        var source = await _db.MachinePrograms
            .Include(p => p.ProgramParts)
            .FirstOrDefaultAsync(p => p.Id == sourceProgramId)
            ?? throw new InvalidOperationException("Source program not found.");

        if (!source.HasSlicerData)
            throw new InvalidOperationException("Source program must have slicer data before creating a scheduled copy.");

        // Generate run number
        var runCount = await _db.MachinePrograms
            .CountAsync(p => p.SourceProgramId == sourceProgramId);
        var runNumber = runCount + 1;

        var copy = new MachineProgram
        {
            ProgramType = source.ProgramType,
            PartId = source.PartId,
            MachineId = source.MachineId,
            MachineType = source.MachineType,
            ProgramNumber = $"{source.ProgramNumber}-RUN{runNumber}",
            Name = $"{source.Name} (Run #{runNumber})",
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
                WorkOrderLineId = workOrderLineId ?? srcPart.WorkOrderLineId
            });
        }

        await _db.SaveChangesAsync();
        await CreateRevisionAsync(copy.Id, createdBy, $"Created as run #{runNumber} from {source.ProgramNumber}");

        return copy;
    }

    public async Task<List<MachineProgram>> GetRunsForSourceProgramAsync(int sourceProgramId)
    {
        return await _db.MachinePrograms
            .Include(p => p.Machine)
            .Include(p => p.ScheduledJob)
            .Where(p => p.SourceProgramId == sourceProgramId)
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // Part Assignment
    // ═══════════════════════════════════════════════════════════

    public async Task<ProgramPart> AddPartToProgramAsync(int programId, int partId, int quantity, int? workOrderLineId = null)
    {
        var program = await _db.MachinePrograms.FindAsync(programId)
            ?? throw new InvalidOperationException("Program not found.");

        if (program.IsLocked)
            throw new InvalidOperationException("Cannot add parts to a locked program.");

        // Check for duplicate
        var existing = await _db.ProgramParts
            .FirstOrDefaultAsync(pp => pp.MachineProgramId == programId && pp.PartId == partId);

        if (existing != null)
        {
            existing.Quantity += quantity;
            await _db.SaveChangesAsync();
            await CreateRevisionAsync(programId, program.LastModifiedBy ?? "System",
                $"Updated quantity for part #{partId} to {existing.Quantity}");
            return existing;
        }

        var programPart = new ProgramPart
        {
            MachineProgramId = programId,
            PartId = partId,
            Quantity = quantity,
            WorkOrderLineId = workOrderLineId,
            StackLevel = 1
        };

        _db.ProgramParts.Add(programPart);
        program.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await CreateRevisionAsync(programId, program.LastModifiedBy ?? "System",
            $"Added part #{partId} x{quantity}");

        return programPart;
    }

    public async Task<ProgramPart> UpdateProgramPartAsync(int programPartId, int quantity, int stackLevel, string? positionNotes = null)
    {
        var programPart = await _db.ProgramParts
            .Include(pp => pp.MachineProgram)
            .FirstOrDefaultAsync(pp => pp.Id == programPartId)
            ?? throw new InvalidOperationException("Program part not found.");

        if (programPart.MachineProgram.IsLocked)
            throw new InvalidOperationException("Cannot modify parts on a locked program.");

        programPart.Quantity = quantity;
        programPart.StackLevel = stackLevel;
        if (positionNotes != null)
            programPart.PositionNotes = positionNotes;

        programPart.MachineProgram.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return programPart;
    }

    public async Task RemoveProgramPartAsync(int programPartId)
    {
        var programPart = await _db.ProgramParts
            .Include(pp => pp.MachineProgram)
            .FirstOrDefaultAsync(pp => pp.Id == programPartId)
            ?? throw new InvalidOperationException("Program part not found.");

        if (programPart.MachineProgram.IsLocked)
            throw new InvalidOperationException("Cannot remove parts from a locked program.");

        _db.ProgramParts.Remove(programPart);
        programPart.MachineProgram.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await CreateRevisionAsync(programPart.MachineProgramId, programPart.MachineProgram.LastModifiedBy ?? "System",
            $"Removed part #{programPart.PartId}");
    }

    // ═══════════════════════════════════════════════════════════
    // Slicer Data
    // ═══════════════════════════════════════════════════════════

    public async Task UpdateSlicerDataAsync(
        int programId,
        double? estimatedPrintHours,
        int? layerCount = null,
        double? buildHeightMm = null,
        double? estimatedPowderKg = null,
        string? slicerFileName = null,
        string? slicerSoftware = null,
        string? slicerVersion = null,
        string? partPositionsJson = null)
    {
        var program = await _db.MachinePrograms.FindAsync(programId)
            ?? throw new InvalidOperationException("Program not found.");

        // Slicer metadata is allowed on locked programs — the lock prevents
        // structural changes (adding/removing parts), not metadata updates.
        // This enables the scheduling wizard to add missing slicer data to
        // existing programs that were locked before slicing was complete.

        program.EstimatedPrintHours = estimatedPrintHours;
        program.LayerCount = layerCount ?? program.LayerCount;
        program.BuildHeightMm = buildHeightMm ?? program.BuildHeightMm;
        program.EstimatedPowderKg = estimatedPowderKg ?? program.EstimatedPowderKg;
        program.SlicerFileName = slicerFileName ?? program.SlicerFileName;
        program.SlicerSoftware = slicerSoftware ?? program.SlicerSoftware;
        program.SlicerVersion = slicerVersion ?? program.SlicerVersion;
        program.PartPositionsJson = partPositionsJson ?? program.PartPositionsJson;
        program.LastModifiedDate = DateTime.UtcNow;

        // Transition status to Ready if it has parts and slicer data
        if (program.ScheduleStatus == ProgramScheduleStatus.None &&
            program.EstimatedPrintHours.HasValue &&
            await _db.ProgramParts.AnyAsync(pp => pp.MachineProgramId == programId))
        {
            program.ScheduleStatus = ProgramScheduleStatus.Ready;
            program.Status = ProgramStatus.Active;
        }

        await _db.SaveChangesAsync();

        await CreateRevisionAsync(programId, program.LastModifiedBy ?? "System",
            $"Updated slicer data: {estimatedPrintHours:F1}h, {layerCount} layers");
    }

    public async Task UpdateDurationFromSliceAsync(int programId)
    {
        var program = await _db.MachinePrograms.FindAsync(programId)
            ?? throw new InvalidOperationException("Program not found.");

        // Duration already set from EstimatedPrintHours — nothing extra to compute
        if (!program.EstimatedPrintHours.HasValue)
            throw new InvalidOperationException("No slicer duration data available.");

        program.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // Revisions / History
    // ═══════════════════════════════════════════════════════════

    public async Task<ProgramRevision> CreateRevisionAsync(int programId, string changedBy, string? notes = null)
    {
        var program = await _db.MachinePrograms
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.Part)
            .FirstOrDefaultAsync(p => p.Id == programId)
            ?? throw new InvalidOperationException("Program not found.");

        var lastRevision = await _db.ProgramRevisions
            .Where(r => r.MachineProgramId == programId)
            .OrderByDescending(r => r.RevisionNumber)
            .FirstOrDefaultAsync();

        var nextRevisionNumber = (lastRevision?.RevisionNumber ?? 0) + 1;

        // Snapshot part list
        var partList = program.ProgramParts.Select(pp => new
        {
            PartId = pp.PartId,
            PartNumber = pp.Part?.PartNumber,
            Quantity = pp.Quantity,
            StackLevel = pp.StackLevel
        });
        var partListJson = JsonSerializer.Serialize(partList);

        var revision = new ProgramRevision
        {
            MachineProgramId = programId,
            RevisionNumber = nextRevisionNumber,
            ChangeNotes = notes,
            PartsSnapshotJson = partListJson,
            ChangedBy = changedBy,
            RevisionDate = DateTime.UtcNow
        };

        _db.ProgramRevisions.Add(revision);
        await _db.SaveChangesAsync();

        return revision;
    }

    public async Task<List<ProgramRevision>> GetRevisionsAsync(int programId)
    {
        return await _db.ProgramRevisions
            .Where(r => r.MachineProgramId == programId)
            .OrderByDescending(r => r.RevisionNumber)
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // Name Generation
    // ═══════════════════════════════════════════════════════════

    public async Task<string> GenerateProgramNameAsync(List<int> partIds, int machineId = 0, string? template = null)
    {
        template ??= "{PARTS} - {DATE}";

        var parts = await _db.Parts
            .Where(p => partIds.Contains(p.Id))
            .Select(p => p.PartNumber)
            .ToListAsync();

        var partsString = parts.Count switch
        {
            0 => "Empty",
            1 => parts[0],
            2 => $"{parts[0]}, {parts[1]}",
            _ => $"{parts[0]} +{parts.Count - 1}"
        };

        string machineName = "";
        if (machineId > 0)
        {
            var machine = await _db.Machines.FindAsync(machineId);
            machineName = machine?.Name ?? machine?.MachineId ?? "";
        }

        var seqNumber = await _numberSeq.NextAsync("BuildPlate");
        var materialName = "";
        if (partIds.Count > 0)
        {
            var firstPart = await _db.Parts
                .Include(p => p.MaterialEntity)
                .FirstOrDefaultAsync(p => p.Id == partIds[0]);
            materialName = firstPart?.MaterialEntity?.Name ?? "";
        }

        return template
            .Replace("{PARTS}", partsString)
            .Replace("{MACHINE}", machineName)
            .Replace("{DATE}", DateTime.UtcNow.ToString("yyMMdd"))
            .Replace("{SEQ}", seqNumber)
            .Replace("{MATERIAL}", materialName);
    }
}
