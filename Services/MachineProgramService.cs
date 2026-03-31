using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Vectrik.Data;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Models.Maintenance;
using Vectrik.Services.Platform;

namespace Vectrik.Services;

public class MachineProgramService : IMachineProgramService
{
    private readonly TenantDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IWebHostEnvironment _env;

    public MachineProgramService(TenantDbContext db, ITenantContext tenant, IWebHostEnvironment env)
    {
        _db = db;
        _tenant = tenant;
        _env = env;
    }

    // ── CRUD ─────────────────────────────────────────────────

    public async Task<MachineProgram?> GetByIdAsync(int id)
    {
        return await _db.MachinePrograms
            .Include(p => p.Part)
            .Include(p => p.Machine)
            .Include(p => p.ProcessStage)
                .ThenInclude(ps => ps!.ProductionStage)
            .Include(p => p.Files)
            .Include(p => p.ToolingItems.Where(t => t.IsActive))
                .ThenInclude(t => t.MachineComponent)
            .Include(p => p.Feedbacks.OrderByDescending(f => f.SubmittedAt).Take(10))
            .Include(p => p.MachineAssignments)
                .ThenInclude(a => a.Machine)
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.Part)
            .Include(p => p.Material)
            .Include(p => p.DepowderProgram)
            .Include(p => p.EdmProgram)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<MachineProgram>> GetAllAsync()
    {
        return await _db.MachinePrograms
            .Include(p => p.Part)
            .Include(p => p.Machine)
            .Include(p => p.Files)
            .Include(p => p.MachineAssignments)
                .ThenInclude(a => a.Machine)
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.Part)
            .Include(p => p.Material)
            .OrderBy(p => p.ProgramNumber)
            .ToListAsync();
    }

    public async Task<List<MachineProgram>> GetProgramsForPartAsync(int partId)
    {
        return await _db.MachinePrograms
            .Include(p => p.Machine)
            .Include(p => p.ProcessStage)
                .ThenInclude(ps => ps!.ProductionStage)
            .Include(p => p.Files)
            .Where(p => p.PartId == partId)
            .OrderBy(p => p.ProgramNumber)
            .ToListAsync();
    }

    public async Task<List<MachineProgram>> GetProgramsForMachineAsync(int machineId)
    {
        return await _db.MachinePrograms
            .Include(p => p.Part)
            .Include(p => p.ProcessStage)
                .ThenInclude(ps => ps!.ProductionStage)
            .Include(p => p.Files)
            .Include(p => p.MachineAssignments)
                .ThenInclude(a => a.Machine)
            .Where(p => p.MachineAssignments.Any(a => a.MachineId == machineId)
                        || p.MachineId == machineId)
            .OrderBy(p => p.ProgramNumber)
            .ToListAsync();
    }

    public async Task<MachineProgram?> GetProgramForStageAsync(int processStageId)
    {
        return await _db.MachinePrograms
            .Include(p => p.Part)
            .Include(p => p.Machine)
            .Include(p => p.Files)
            .FirstOrDefaultAsync(p => p.ProcessStageId == processStageId && p.Status == ProgramStatus.Active);
    }

    public async Task<List<MachineProgram>> GetActiveProgramsAsync(int? partId = null, int? machineId = null, int? processStageId = null)
    {
        var query = _db.MachinePrograms
            .Include(p => p.Part)
            .Include(p => p.Machine)
            .Include(p => p.ProcessStage)
                .ThenInclude(ps => ps!.ProductionStage)
            .Include(p => p.MachineAssignments)
                .ThenInclude(a => a.Machine)
            .Where(p => p.Status == ProgramStatus.Active);

        if (partId.HasValue)
            query = query.Where(p => p.PartId == partId.Value);
        if (machineId.HasValue)
            query = query.Where(p => p.MachineAssignments.Any(a => a.MachineId == machineId.Value)
                                     || p.MachineId == machineId.Value);
        if (processStageId.HasValue)
            query = query.Where(p => p.ProcessStageId == processStageId.Value);

        return await query.OrderBy(p => p.ProgramNumber).ToListAsync();
    }

    public async Task<MachineProgram> CreateAsync(MachineProgram program, string createdBy)
    {
        ArgumentNullException.ThrowIfNull(program);

        program.CreatedBy = createdBy;
        program.LastModifiedBy = createdBy;
        program.CreatedDate = DateTime.UtcNow;
        program.LastModifiedDate = DateTime.UtcNow;
        program.Version = 1;

        _db.MachinePrograms.Add(program);
        await _db.SaveChangesAsync();
        return program;
    }

    public async Task UpdateAsync(MachineProgram program, string modifiedBy)
    {
        ArgumentNullException.ThrowIfNull(program);

        program.LastModifiedBy = modifiedBy;
        program.LastModifiedDate = DateTime.UtcNow;

        _db.MachinePrograms.Update(program);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var program = await _db.MachinePrograms
            .Include(p => p.Files)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (program == null) return;

        // Delete physical files
        foreach (var file in program.Files)
        {
            DeletePhysicalFile(file.FilePath);
        }

        _db.MachinePrograms.Remove(program);
        await _db.SaveChangesAsync();
    }

    // ── Versioning ───────────────────────────────────────────

    public async Task<MachineProgram> CreateNewVersionAsync(int programId, string createdBy)
    {
        var source = await _db.MachinePrograms
            .Include(p => p.Files)
            .Include(p => p.ToolingItems.Where(t => t.IsActive))
            .FirstOrDefaultAsync(p => p.Id == programId)
            ?? throw new InvalidOperationException($"Program {programId} not found.");

        // Mark old version as superseded
        source.Status = ProgramStatus.Superseded;
        source.LastModifiedBy = createdBy;
        source.LastModifiedDate = DateTime.UtcNow;

        // Create new version
        var newVersion = new MachineProgram
        {
            PartId = source.PartId,
            MachineId = source.MachineId,
            MachineType = source.MachineType,
            ProcessStageId = source.ProcessStageId,
            ProgramNumber = source.ProgramNumber,
            Name = source.Name,
            Description = source.Description,
            Version = source.Version + 1,
            Status = ProgramStatus.Draft,
            SetupTimeMinutes = source.SetupTimeMinutes,
            RunTimeMinutes = source.RunTimeMinutes,
            CycleTimeMinutes = source.CycleTimeMinutes,
            ToolingRequired = source.ToolingRequired,
            FixtureRequired = source.FixtureRequired,
            Parameters = source.Parameters,
            Notes = source.Notes,
            ProgramType = source.ProgramType,
            MaterialId = source.MaterialId,
            DepowderProgramId = source.DepowderProgramId,
            EdmProgramId = source.EdmProgramId,
            EstimatedPrintHours = source.EstimatedPrintHours,
            LayerCount = source.LayerCount,
            BuildHeightMm = source.BuildHeightMm,
            EstimatedPowderKg = source.EstimatedPowderKg,
            SlicerFileName = source.SlicerFileName,
            SlicerSoftware = source.SlicerSoftware,
            SlicerVersion = source.SlicerVersion,
            PartPositionsJson = source.PartPositionsJson,
            IsActive = true,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        _db.MachinePrograms.Add(newVersion);
        await _db.SaveChangesAsync();

        // Copy machine assignments to new version
        var sourceAssignments = await _db.MachineProgramAssignments
            .Where(a => a.MachineProgramId == programId)
            .ToListAsync();
        foreach (var srcAssign in sourceAssignments)
        {
            _db.MachineProgramAssignments.Add(new MachineProgramAssignment
            {
                MachineProgramId = newVersion.Id,
                MachineId = srcAssign.MachineId,
                IsPreferred = srcAssign.IsPreferred,
                Notes = srcAssign.Notes,
                AssignedBy = createdBy,
                AssignedDate = DateTime.UtcNow
            });
        }

        // Copy tooling items to new version
        foreach (var srcItem in source.ToolingItems)
        {
            var copy = new ProgramToolingItem
            {
                MachineProgramId = newVersion.Id,
                ToolPosition = srcItem.ToolPosition,
                Name = srcItem.Name,
                MachineComponentId = srcItem.MachineComponentId,
                IsFixture = srcItem.IsFixture,
                WearLifeHours = srcItem.WearLifeHours,
                WearLifeBuilds = srcItem.WearLifeBuilds,
                WarningThresholdPercent = srcItem.WarningThresholdPercent,
                SparePartNumber = srcItem.SparePartNumber,
                Notes = srcItem.Notes,
                SortOrder = srcItem.SortOrder,
                IsActive = true,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
            _db.ProgramToolingItems.Add(copy);
        }

        if (source.ToolingItems.Any())
            await _db.SaveChangesAsync();

        return newVersion;
    }

    // ── File Management ──────────────────────────────────────

    public async Task<MachineProgramFile> UploadFileAsync(int programId, string fileName, Stream fileStream, long fileSize, string uploadedBy, string? description = null)
    {
        var relativePath = GetUploadPath(programId, fileName);
        var fullPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));

        var dir = Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // Compute hash while writing
        string? hash;
        using (var fs = new FileStream(fullPath, FileMode.Create))
        {
            using var sha256 = SHA256.Create();
            using var hashStream = new CryptoStream(fs, sha256, CryptoStreamMode.Write);
            await fileStream.CopyToAsync(hashStream);
            await hashStream.FlushFinalBlockAsync();
            hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
        }

        var ext = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(ext)) ext = "Other";

        var file = new MachineProgramFile
        {
            MachineProgramId = programId,
            FileName = fileName,
            FilePath = relativePath,
            FileType = ext,
            FileSizeBytes = fileSize,
            FileHash = hash,
            Description = description,
            UploadedBy = uploadedBy,
            UploadedDate = DateTime.UtcNow
        };

        _db.MachineProgramFiles.Add(file);
        await _db.SaveChangesAsync();
        return file;
    }

    public async Task<List<MachineProgramFile>> GetFilesAsync(int programId)
    {
        return await _db.MachineProgramFiles
            .Where(f => f.MachineProgramId == programId)
            .OrderByDescending(f => f.IsPrimary)
            .ThenByDescending(f => f.UploadedDate)
            .ToListAsync();
    }

    public async Task<MachineProgramFile?> GetFileByIdAsync(int fileId)
    {
        return await _db.MachineProgramFiles.FindAsync(fileId);
    }

    public async Task DeleteFileAsync(int fileId)
    {
        var file = await _db.MachineProgramFiles.FindAsync(fileId);
        if (file == null) return;

        DeletePhysicalFile(file.FilePath);

        _db.MachineProgramFiles.Remove(file);
        await _db.SaveChangesAsync();
    }

    public async Task SetPrimaryFileAsync(int programId, int fileId)
    {
        var files = await _db.MachineProgramFiles.Where(f => f.MachineProgramId == programId).ToListAsync();
        foreach (var f in files)
        {
            f.IsPrimary = f.Id == fileId;
        }
        await _db.SaveChangesAsync();
    }

    // ── Cloning ──────────────────────────────────────────────

    public async Task<MachineProgram> CloneProgramAsync(int sourceProgramId, int? targetPartId, int? targetMachineId, string createdBy)
    {
        var source = await _db.MachinePrograms
            .FirstOrDefaultAsync(p => p.Id == sourceProgramId)
            ?? throw new InvalidOperationException($"Program {sourceProgramId} not found.");

        var clone = new MachineProgram
        {
            PartId = targetPartId ?? source.PartId,
            MachineId = targetMachineId ?? source.MachineId,
            MachineType = source.MachineType,
            ProcessStageId = null, // Must be manually re-linked
            ProgramNumber = $"{source.ProgramNumber}-COPY",
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            Version = 1,
            Status = ProgramStatus.Draft,
            SetupTimeMinutes = source.SetupTimeMinutes,
            RunTimeMinutes = source.RunTimeMinutes,
            CycleTimeMinutes = source.CycleTimeMinutes,
            ToolingRequired = source.ToolingRequired,
            FixtureRequired = source.FixtureRequired,
            Parameters = source.Parameters,
            Notes = source.Notes,
            ProgramType = source.ProgramType,
            MaterialId = source.MaterialId,
            DepowderProgramId = source.DepowderProgramId,
            EdmProgramId = source.EdmProgramId,
            EstimatedPrintHours = source.EstimatedPrintHours,
            LayerCount = source.LayerCount,
            BuildHeightMm = source.BuildHeightMm,
            EstimatedPowderKg = source.EstimatedPowderKg,
            SlicerFileName = source.SlicerFileName,
            SlicerSoftware = source.SlicerSoftware,
            SlicerVersion = source.SlicerVersion,
            PartPositionsJson = source.PartPositionsJson,
            IsActive = true,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        _db.MachinePrograms.Add(clone);
        await _db.SaveChangesAsync();

        // Copy machine assignments from source
        var sourceAssignments = await _db.MachineProgramAssignments
            .Where(a => a.MachineProgramId == sourceProgramId)
            .ToListAsync();
        foreach (var srcAssign in sourceAssignments)
        {
            _db.MachineProgramAssignments.Add(new MachineProgramAssignment
            {
                MachineProgramId = clone.Id,
                MachineId = srcAssign.MachineId,
                IsPreferred = srcAssign.IsPreferred,
                Notes = srcAssign.Notes,
                AssignedBy = createdBy,
                AssignedDate = DateTime.UtcNow
            });
        }
        if (sourceAssignments.Count > 0)
            await _db.SaveChangesAsync();

        return clone;
    }

    // ── Parameter Templates ──────────────────────────────────

    public string GetDefaultParametersForMachineType(string machineType)
    {
        return machineType.ToUpperInvariant() switch
        {
            "CNC" or "CNC MILL" or "CNC LATHE" => """
                {
                  "spindleSpeedRpm": 0,
                  "feedRateMmPerMin": 0,
                  "depthOfCutMm": 0,
                  "coolantType": "Flood",
                  "toolChangeCount": 0,
                  "materialRemovalRateCm3PerMin": 0
                }
                """,
            "EDM" or "WIRE EDM" => """
                {
                  "wireTypeMm": "Brass 0.25mm",
                  "gapVoltageMv": 0,
                  "dischargeCurrent": 0,
                  "flushPressureBar": 0,
                  "wireConsumedMeters": 0,
                  "cutSpeedMmPerMin": 0
                }
                """,
            "LASER" or "LASER ENGRAVING" or "LASER CUTTING" => """
                {
                  "laserPowerWatts": 0,
                  "scanSpeedMmPerSec": 0,
                  "pulseFrequencyHz": 0,
                  "focalLengthMm": 0,
                  "assistGas": "Nitrogen",
                  "passCount": 1
                }
                """,
            "SLS" or "ADDITIVE" or "3D PRINTING" or "DMLS" or "SLM" or "MJF" or "EBM" => """
                {
                  "layerThicknessMm": 0.1,
                  "laserPowerWatts": 0,
                  "scanSpeedMmPerSec": 0,
                  "hatchSpacingMm": 0,
                  "buildChamberTempC": 0,
                  "printerModel": ""
                }
                """,
            "TURNING" or "CNC TURNING" => """
                {
                  "spindleSpeedRpm": 0,
                  "feedRateMmPerRev": 0,
                  "depthOfCutMm": 0,
                  "toolNoseRadiusMm": 0,
                  "coolantType": "Flood",
                  "chuckType": "3-Jaw"
                }
                """,
            _ => """
                {
                  "param1": "",
                  "param2": "",
                  "notes": ""
                }
                """
        };
    }

    // ── Tooling Management ─────────────────────────────────────

    public async Task<List<ProgramToolingItem>> GetToolingItemsAsync(int programId)
    {
        return await _db.ProgramToolingItems
            .Include(t => t.MachineComponent)
                .ThenInclude(c => c!.MaintenanceRules)
            .Where(t => t.MachineProgramId == programId && t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.ToolPosition)
            .ToListAsync();
    }

    public async Task<ProgramToolingItem> SaveToolingItemAsync(ProgramToolingItem item, string modifiedBy)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.LastModifiedBy = modifiedBy;
        item.LastModifiedDate = DateTime.UtcNow;

        if (item.Id == 0)
        {
            item.CreatedBy = modifiedBy;
            item.CreatedDate = DateTime.UtcNow;
            _db.ProgramToolingItems.Add(item);
        }
        else
        {
            _db.ProgramToolingItems.Update(item);
        }

        await _db.SaveChangesAsync();
        return item;
    }

    public async Task DeleteToolingItemAsync(int toolingItemId)
    {
        var item = await _db.ProgramToolingItems.FindAsync(toolingItemId);
        if (item is null) return;

        item.IsActive = false;
        item.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<List<ToolingReadinessAlert>> CheckToolingReadinessAsync(int programId)
    {
        var alerts = new List<ToolingReadinessAlert>();

        var toolingItems = await _db.ProgramToolingItems
            .Include(t => t.MachineComponent)
                .ThenInclude(c => c!.MaintenanceRules)
            .Where(t => t.MachineProgramId == programId && t.IsActive && t.MachineComponentId.HasValue)
            .ToListAsync();

        foreach (var item in toolingItems)
        {
            var component = item.MachineComponent;
            if (component is null || !component.IsActive) continue;

            // Check wear percentage from the tooling item's configured wear life
            var wearPercent = item.WearPercent;
            if (wearPercent.HasValue && wearPercent.Value >= item.WarningThresholdPercent)
            {
                var isOverdue = wearPercent.Value >= 100;
                alerts.Add(new ToolingReadinessAlert
                {
                    ToolingItemId = item.Id,
                    ToolPosition = item.ToolPosition,
                    ToolName = item.Name,
                    ComponentName = component.Name,
                    MachineComponentId = component.Id,
                    WearPercent = wearPercent.Value,
                    IsOverdue = isOverdue,
                    IsBlocking = isOverdue,
                    Message = isOverdue
                        ? $"{item.ToolPosition} ({item.Name}) has exceeded its wear life — component '{component.Name}' must be replaced before starting."
                        : $"{item.ToolPosition} ({item.Name}) is at {wearPercent.Value:F0}% wear — component '{component.Name}' approaching replacement threshold."
                });
            }

            // Also check maintenance rules on the linked component for critical alerts
            foreach (var rule in component.MaintenanceRules.Where(r => r.IsActive))
            {
                double currentValue = rule.TriggerType switch
                {
                    MaintenanceTriggerType.HoursRun => component.CurrentHours ?? 0,
                    MaintenanceTriggerType.BuildsCompleted => component.CurrentBuilds ?? 0,
                    _ => 0
                };

                var rulePercent = rule.ThresholdValue > 0 ? (currentValue / rule.ThresholdValue) * 100 : 0;
                if (rulePercent >= 100)
                {
                    // Only add if not already covered by wear percent alert
                    if (!alerts.Any(a => a.MachineComponentId == component.Id && a.IsBlocking))
                    {
                        alerts.Add(new ToolingReadinessAlert
                        {
                            ToolingItemId = item.Id,
                            ToolPosition = item.ToolPosition,
                            ToolName = item.Name,
                            ComponentName = component.Name,
                            MachineComponentId = component.Id,
                            WearPercent = rulePercent,
                            IsOverdue = true,
                            IsBlocking = rule.Severity == MaintenanceSeverity.Critical,
                            Message = $"Maintenance rule '{rule.Name}' is overdue for component '{component.Name}' at {item.ToolPosition}."
                        });
                    }
                }
            }
        }

        return alerts.OrderByDescending(a => a.IsBlocking).ThenByDescending(a => a.WearPercent).ToList();
    }

    // ── Program Feedback ─────────────────────────────────────

    public async Task<List<ProgramFeedback>> GetFeedbackAsync(int programId, ProgramFeedbackStatus? statusFilter = null)
    {
        var query = _db.ProgramFeedbacks
            .Include(f => f.StageExecution)
            .Where(f => f.MachineProgramId == programId);

        if (statusFilter.HasValue)
            query = query.Where(f => f.Status == statusFilter.Value);

        return await query.OrderByDescending(f => f.SubmittedAt).ToListAsync();
    }

    public async Task<ProgramFeedback> SubmitFeedbackAsync(ProgramFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);

        feedback.SubmittedAt = DateTime.UtcNow;
        feedback.Status = ProgramFeedbackStatus.New;
        _db.ProgramFeedbacks.Add(feedback);
        await _db.SaveChangesAsync();
        return feedback;
    }

    public async Task<ProgramFeedback> ReviewFeedbackAsync(int feedbackId, ProgramFeedbackStatus newStatus, string reviewedBy, string? resolution = null)
    {
        var feedback = await _db.ProgramFeedbacks.FindAsync(feedbackId)
            ?? throw new InvalidOperationException($"Feedback {feedbackId} not found.");

        feedback.Status = newStatus;
        feedback.ReviewedBy = reviewedBy;
        feedback.ReviewedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(resolution))
            feedback.Resolution = resolution;

        await _db.SaveChangesAsync();
        return feedback;
    }

    // ── Execution History ────────────────────────────────────

    public async Task<List<StageExecution>> GetExecutionHistoryAsync(int programId, int maxResults = 20)
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.Machine)
            .Include(e => e.ProductionStage)
            .Where(e => e.MachineProgramId == programId
                && (e.Status == StageExecutionStatus.Completed || e.Status == StageExecutionStatus.Failed))
            .OrderByDescending(e => e.CompletedAt)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<List<StageExecution>> GetProgramRunsAsync(int programId)
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
            .Include(e => e.Machine)
            .Include(e => e.ProductionStage)
            .Where(e => e.MachineProgramId == programId
                && e.Job != null
                && e.Job.Scope == JobScope.Build)
            .OrderByDescending(e => e.ScheduledStartAt ?? e.CreatedDate)
            .ToListAsync();
    }

    public async Task<Dictionary<int, int>> GetUnresolvedFeedbackCountsAsync(List<int> programIds)
    {
        if (programIds.Count == 0) return new();

        return await _db.ProgramFeedbacks
            .Where(f => programIds.Contains(f.MachineProgramId)
                && f.Status != ProgramFeedbackStatus.Resolved
                && f.Status != ProgramFeedbackStatus.WontFix)
            .GroupBy(f => f.MachineProgramId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    // ── Helpers ───────────────────────────────────────────────

    // ── Machine Assignments ──────────────────────────────────

    public async Task<List<MachineProgramAssignment>> GetMachineAssignmentsAsync(int programId)
    {
        return await _db.MachineProgramAssignments
            .Include(a => a.Machine)
            .Where(a => a.MachineProgramId == programId)
            .OrderByDescending(a => a.IsPreferred)
            .ThenBy(a => a.Machine.Name)
            .ToListAsync();
    }

    public async Task<MachineProgramAssignment> AssignMachineAsync(int programId, int machineId, string assignedBy, bool isPreferred = false, string? notes = null)
    {
        var exists = await _db.MachineProgramAssignments
            .AnyAsync(a => a.MachineProgramId == programId && a.MachineId == machineId);
        if (exists)
            throw new InvalidOperationException($"Machine {machineId} is already assigned to program {programId}.");

        // Auto-prefer if this is the first assignment
        var hasAny = await _db.MachineProgramAssignments.AnyAsync(a => a.MachineProgramId == programId);
        if (!hasAny) isPreferred = true;

        var assignment = new MachineProgramAssignment
        {
            MachineProgramId = programId,
            MachineId = machineId,
            IsPreferred = isPreferred,
            Notes = notes,
            AssignedBy = assignedBy,
            AssignedDate = DateTime.UtcNow
        };

        if (isPreferred)
        {
            var others = await _db.MachineProgramAssignments
                .Where(a => a.MachineProgramId == programId && a.IsPreferred)
                .ToListAsync();
            foreach (var o in others) o.IsPreferred = false;
        }

        _db.MachineProgramAssignments.Add(assignment);
        await _db.SaveChangesAsync();
        return assignment;
    }

    public async Task UnassignMachineAsync(int programId, int machineId)
    {
        var assignment = await _db.MachineProgramAssignments
            .FirstOrDefaultAsync(a => a.MachineProgramId == programId && a.MachineId == machineId);
        if (assignment is null) return;

        var wasPreferred = assignment.IsPreferred;
        _db.MachineProgramAssignments.Remove(assignment);
        await _db.SaveChangesAsync();

        // Promote next assignment if the removed one was preferred
        if (wasPreferred)
        {
            var next = await _db.MachineProgramAssignments
                .Where(a => a.MachineProgramId == programId)
                .OrderBy(a => a.AssignedDate)
                .FirstOrDefaultAsync();
            if (next is not null)
            {
                next.IsPreferred = true;
                await _db.SaveChangesAsync();
            }
        }
    }

    public async Task SetPreferredMachineAsync(int programId, int machineId)
    {
        var assignments = await _db.MachineProgramAssignments
            .Where(a => a.MachineProgramId == programId)
            .ToListAsync();

        foreach (var a in assignments)
            a.IsPreferred = a.MachineId == machineId;

        await _db.SaveChangesAsync();
    }

    public async Task SyncMachineAssignmentsAsync(int programId, List<int> machineIds, string assignedBy)
    {
        ArgumentNullException.ThrowIfNull(machineIds);

        var existing = await _db.MachineProgramAssignments
            .Where(a => a.MachineProgramId == programId)
            .ToListAsync();

        // Remove assignments not in new list
        var toRemove = existing.Where(e => !machineIds.Contains(e.MachineId)).ToList();
        _db.MachineProgramAssignments.RemoveRange(toRemove);

        // Add new assignments
        var existingMachineIds = existing.Select(e => e.MachineId).ToHashSet();
        foreach (var mid in machineIds.Where(m => !existingMachineIds.Contains(m)))
        {
            _db.MachineProgramAssignments.Add(new MachineProgramAssignment
            {
                MachineProgramId = programId,
                MachineId = mid,
                AssignedBy = assignedBy,
                AssignedDate = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        // Ensure at least one preferred if any remain
        var remaining = await _db.MachineProgramAssignments
            .Where(a => a.MachineProgramId == programId)
            .ToListAsync();
        if (remaining.Count > 0 && !remaining.Any(a => a.IsPreferred))
        {
            remaining.First().IsPreferred = true;
            await _db.SaveChangesAsync();
        }
    }

    // ── Build Plate Operations (SLS programs) ────────────────

    public async Task<List<MachineProgram>> GetBuildPlateProgramsAsync(ProgramStatus? statusFilter = null)
    {
        var query = _db.MachinePrograms
            .Include(p => p.ProgramParts).ThenInclude(pp => pp.Part)
            .Include(p => p.MachineAssignments).ThenInclude(a => a.Machine)
            .Include(p => p.Material)
            .Where(p => p.ProgramType == ProgramType.BuildPlate);

        if (statusFilter.HasValue)
            query = query.Where(p => p.Status == statusFilter.Value);

        return await query
            .OrderByDescending(p => p.LastModifiedDate)
            .ToListAsync();
    }

    public async Task<List<ProgramPart>> GetProgramPartsAsync(int programId)
    {
        return await _db.ProgramParts
            .Include(pp => pp.Part)
            .Include(pp => pp.WorkOrderLine)
            .Where(pp => pp.MachineProgramId == programId)
            .OrderBy(pp => pp.Part.PartNumber)
            .ToListAsync();
    }

    public async Task<ProgramPart> AddProgramPartAsync(int programId, int partId, int quantity, int stackLevel = 1, int? workOrderLineId = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(stackLevel, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stackLevel, 3);

        var program = await _db.MachinePrograms.FindAsync(programId)
            ?? throw new InvalidOperationException($"Program {programId} not found.");

        var entry = new ProgramPart
        {
            MachineProgramId = programId,
            PartId = partId,
            Quantity = quantity,
            StackLevel = stackLevel,
            WorkOrderLineId = workOrderLineId
        };

        _db.ProgramParts.Add(entry);

        program.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return entry;
    }

    public async Task<ProgramPart> UpdateProgramPartAsync(int programPartId, int quantity, int stackLevel, string? positionNotes = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(stackLevel, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stackLevel, 3);

        var entry = await _db.ProgramParts.FindAsync(programPartId)
            ?? throw new InvalidOperationException("Program part entry not found.");

        entry.Quantity = quantity;
        entry.StackLevel = stackLevel;
        entry.PositionNotes = positionNotes;

        var program = await _db.MachinePrograms.FindAsync(entry.MachineProgramId);
        if (program != null) program.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task RemoveProgramPartAsync(int programPartId)
    {
        var entry = await _db.ProgramParts.FindAsync(programPartId)
            ?? throw new InvalidOperationException("Program part entry not found.");

        var programId = entry.MachineProgramId;
        _db.ProgramParts.Remove(entry);

        var program = await _db.MachinePrograms.FindAsync(programId);
        if (program != null) program.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task UpdateSlicerDataAsync(int programId, double? estimatedPrintHours, int? layerCount = null,
        double? buildHeightMm = null, double? estimatedPowderKg = null,
        string? slicerFileName = null, string? slicerSoftware = null, string? slicerVersion = null,
        string? partPositionsJson = null)
    {
        var program = await _db.MachinePrograms.FindAsync(programId)
            ?? throw new InvalidOperationException($"Program {programId} not found.");

        if (program.ProgramType != ProgramType.BuildPlate)
            throw new InvalidOperationException("Slicer data only applies to BuildPlate programs.");

        program.EstimatedPrintHours = estimatedPrintHours;
        program.LayerCount = layerCount;
        program.BuildHeightMm = buildHeightMm;
        program.EstimatedPowderKg = estimatedPowderKg;
        program.SlicerFileName = slicerFileName;
        program.SlicerSoftware = slicerSoftware;
        program.SlicerVersion = slicerVersion;
        program.PartPositionsJson = partPositionsJson;
        program.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task UpdatePostProcessingLinksAsync(int buildPlateProgramId, int? depowderProgramId, int? edmProgramId, string modifiedBy)
    {
        var program = await _db.MachinePrograms.FindAsync(buildPlateProgramId)
            ?? throw new InvalidOperationException($"Program {buildPlateProgramId} not found.");

        if (program.ProgramType != ProgramType.BuildPlate)
            throw new InvalidOperationException("Post-processing links only apply to BuildPlate programs.");

        program.DepowderProgramId = depowderProgramId;
        program.EdmProgramId = edmProgramId;
        program.LastModifiedBy = modifiedBy;
        program.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<List<MachineProgram>> GetPostProcessingCandidatesAsync(string machineType)
    {
        var normalised = (machineType ?? "").ToUpperInvariant().Replace("-", "").Replace(" ", "");

        return await _db.MachinePrograms
            .Include(p => p.MachineAssignments).ThenInclude(a => a.Machine)
            .Where(p => p.ProgramType == ProgramType.Standard
                        && (p.Status == ProgramStatus.Active || p.Status == ProgramStatus.Draft))
            .Where(p => p.MachineType != null
                        && p.MachineType.ToUpper().Replace("-", "").Replace(" ", "") == normalised)
            .OrderBy(p => p.ProgramNumber)
            .ToListAsync();
    }

    // ── Duration & Program Selection ─────────────────────────

    public async Task<ProgramDurationResult?> GetDurationFromProgramAsync(int programId, int quantity = 1)
    {
        var program = await _db.MachinePrograms.FindAsync(programId);
        if (program == null) return null;

        // For BuildPlate programs, use EstimatedPrintHours (slicer data)
        if (program.ProgramType == ProgramType.BuildPlate)
        {
            if (!program.EstimatedPrintHours.HasValue || program.EstimatedPrintHours <= 0)
                return null;

            var printMinutes = program.EstimatedPrintHours.Value * 60;
            return new ProgramDurationResult(
                TotalMinutes: printMinutes,
                SetupMinutes: 0,
                RunMinutes: printMinutes,
                CycleMinutes: 0,
                Source: "BuildPlate.Slicer",
                IsLearned: false);
        }

        // For Standard programs, check for learned EMA data first
        if (program.EstimateSource == "Auto" && program.ActualAverageDurationMinutes.HasValue)
        {
            // Learned duration — scale by quantity
            var learnedPerPart = program.ActualAverageDurationMinutes.Value;
            var setupMin = program.SetupTimeMinutes ?? 0;
            // If we have learned data, the ActualAverageDurationMinutes is per-part including setup share
            // So for multiple parts: setup once + (learned per-part - setup share) * quantity
            // Approximation: assume learned value is inclusive of amortized setup
            var totalMinutes = learnedPerPart * quantity;
            return new ProgramDurationResult(
                TotalMinutes: totalMinutes,
                SetupMinutes: setupMin,
                RunMinutes: totalMinutes - setupMin,
                CycleMinutes: program.CycleTimeMinutes ?? 0,
                Source: $"Learned (EMA from {program.ActualSampleCount ?? 0} runs)",
                IsLearned: true);
        }

        // Fall back to configured duration fields
        var setup = program.SetupTimeMinutes ?? 0;
        var run = (program.RunTimeMinutes ?? 0) * quantity;
        var cycle = (program.CycleTimeMinutes ?? 0) * quantity;

        // If no duration data at all, return null
        if (setup == 0 && run == 0 && cycle == 0)
            return null;

        // RunTimeMinutes is typically the cutting/processing time per part
        // CycleTimeMinutes is total time from part-in to part-out
        // If both are set, use CycleTimeMinutes; if only RunTimeMinutes, use that
        var perPartTime = (program.CycleTimeMinutes ?? program.RunTimeMinutes ?? 0) * quantity;
        var total = setup + perPartTime;

        return new ProgramDurationResult(
            TotalMinutes: total,
            SetupMinutes: setup,
            RunMinutes: run,
            CycleMinutes: cycle,
            Source: "Program.Configured",
            IsLearned: false);
    }

    public async Task<MachineProgram?> GetBestProgramForStageAsync(int partId, int? machineId = null, int? productionStageId = null)
    {
        // Build query for active programs matching the part
        var query = _db.MachinePrograms
            .Include(p => p.MachineAssignments)
            .Where(p => p.Status == ProgramStatus.Active);

        // Filter by part: either PartId FK match or in ProgramParts
        query = query.Where(p => 
            p.PartId == partId 
            || p.ProgramParts.Any(pp => pp.PartId == partId));

        // Filter by machine if specified (via MachineAssignments or legacy MachineId)
        if (machineId.HasValue)
        {
            query = query.Where(p => 
                p.MachineAssignments.Any(a => a.MachineId == machineId.Value)
                || p.MachineId == machineId.Value);
        }

        // Filter by linked ProcessStage if specified
        if (productionStageId.HasValue)
        {
            query = query.Where(p => 
                p.ProcessStageId != null && _db.ProcessStages
                    .Any(ps => ps.Id == p.ProcessStageId && ps.ProductionStageId == productionStageId.Value));
        }

        var candidates = await query.ToListAsync();

        if (candidates.Count == 0)
            return null;

        // Selection priority:
        // 1. Programs with learned data (EstimateSource == "Auto") and higher sample counts
        // 2. Programs that are preferred on the target machine
        // 3. First active match

        var withLearning = candidates
            .Where(p => p.EstimateSource == "Auto" && p.ActualSampleCount > 0)
            .OrderByDescending(p => p.ActualSampleCount)
            .FirstOrDefault();

        if (withLearning != null)
            return withLearning;

        // Check for preferred machine assignment if machineId is specified
        if (machineId.HasValue)
        {
            var preferredOnMachine = candidates
                .FirstOrDefault(p => p.MachineAssignments
                    .Any(a => a.MachineId == machineId.Value && a.IsPreferred));

            if (preferredOnMachine != null)
                return preferredOnMachine;
        }

        // Return first active program
        return candidates.FirstOrDefault();
    }

    // ── Private Helpers ──────────────────────────────────────

    private string GetUploadPath(int programId, string fileName)
    {
        var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Path.GetFileName(fileName)}";
        return $"/uploads/programs/{_tenant.TenantCode}/{programId}/{safeName}";
    }

    private void DeletePhysicalFile(string relativePath)
    {
        var fullPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
