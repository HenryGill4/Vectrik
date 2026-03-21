using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class BuildPlanningService : IBuildPlanningService
{
    private readonly TenantDbContext _db;
    private readonly INumberSequenceService _numberSeq;

    public BuildPlanningService(TenantDbContext db, INumberSequenceService numberSeq)
    {
        _db = db;
        _numberSeq = numberSeq;
    }

    public async Task<List<BuildPackage>> GetAllPackagesAsync()
    {
        return await _db.BuildPackages
            .Include(p => p.Machine)
            .Include(p => p.Parts)
                .ThenInclude(pp => pp.Part)
                    .ThenInclude(part => part.AdditiveBuildConfig)
            .Include(p => p.Parts)
                .ThenInclude(pp => pp.Part)
                    .ThenInclude(part => part.MaterialEntity)
            .Include(p => p.Parts)
                .ThenInclude(pp => pp.Part)
                    .ThenInclude(part => part.StageRequirements)
                        .ThenInclude(sr => sr.ProductionStage)
            .Include(p => p.Parts)
                .ThenInclude(pp => pp.WorkOrderLine)
                    .ThenInclude(wl => wl!.WorkOrder)
            .Include(p => p.BuildFileInfo)
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();
    }

    public async Task<List<BuildPackage>> GetBuildsForPartAsync(int partId)
    {
        return await _db.BuildPackages
            .Include(p => p.Machine)
            .Include(p => p.Parts)
                .ThenInclude(pp => pp.Part)
            .Where(p => p.Parts.Any(pp => pp.PartId == partId))
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();
    }

    public async Task<BuildPackage?> GetPackageByIdAsync(int id)
    {
        return await _db.BuildPackages
            .Include(p => p.Machine)
            .Include(p => p.Parts)
                .ThenInclude(pp => pp.Part)
            .Include(p => p.Parts)
                .ThenInclude(pp => pp.WorkOrderLine)
                    .ThenInclude(wl => wl!.WorkOrder)
            .Include(p => p.BuildFileInfo)
            .Include(p => p.ScheduledJob)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<BuildPackage> CreatePackageAsync(BuildPackage package)
    {
        package.CreatedDate = DateTime.UtcNow;
        package.LastModifiedDate = DateTime.UtcNow;
        _db.BuildPackages.Add(package);
        await _db.SaveChangesAsync();
        return package;
    }

    public async Task<BuildPackage> UpdatePackageAsync(BuildPackage package)
    {
        // Detect status transition to Scheduled → auto-create build stage executions
        var entry = _db.Entry(package);
        var previousStatus = entry.State == EntityState.Detached
            ? (await _db.BuildPackages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == package.Id))?.Status
            : entry.OriginalValues.GetValue<BuildPackageStatus>(nameof(BuildPackage.Status));

        package.LastModifiedDate = DateTime.UtcNow;
        _db.BuildPackages.Update(package);
        await _db.SaveChangesAsync();

        if (previousStatus != BuildPackageStatus.Scheduled && package.Status == BuildPackageStatus.Scheduled)
        {
            // Auto-create build-level stage executions when package is scheduled
            var existingBuildExecutions = await _db.StageExecutions
                .AnyAsync(e => e.BuildPackageId == package.Id);
            if (!existingBuildExecutions)
                await CreateBuildStageExecutionsAsync(package.Id, package.LastModifiedBy);
        }

        return package;
    }

    public async Task DeletePackageAsync(int id)
    {
        var package = await _db.BuildPackages.FindAsync(id);
        if (package == null) throw new InvalidOperationException("Build package not found.");
        package.Status = BuildPackageStatus.Cancelled;
        package.LastModifiedDate = DateTime.UtcNow;

        // Cancel outstanding build-level stage executions tied to this build package
        var activeExecutions = await _db.StageExecutions
            .Where(e => e.BuildPackageId == id
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed)
            .ToListAsync();

        foreach (var exec in activeExecutions)
        {
            exec.Status = StageExecutionStatus.Skipped;
            exec.Notes = "Auto-skipped: build package cancelled";
            exec.CompletedAt = DateTime.UtcNow;
            exec.ActualEndAt = DateTime.UtcNow;
            exec.LastModifiedDate = DateTime.UtcNow;
        }

        // Cancel prefilled per-part jobs
        var perPartJobs = await _db.Jobs
            .Include(j => j.Stages)
            .Where(j => j.Notes != null && j.Notes.Contains(package.Name)
                && j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled)
            .ToListAsync();

        foreach (var job in perPartJobs)
        {
            job.Status = JobStatus.Cancelled;
            job.LastModifiedDate = DateTime.UtcNow;
            foreach (var stage in job.Stages.Where(s =>
                s.Status != StageExecutionStatus.Completed
                && s.Status != StageExecutionStatus.Skipped
                && s.Status != StageExecutionStatus.Failed))
            {
                stage.Status = StageExecutionStatus.Skipped;
                stage.Notes = "Auto-skipped: build package cancelled";
                stage.CompletedAt = DateTime.UtcNow;
                stage.ActualEndAt = DateTime.UtcNow;
                stage.LastModifiedDate = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<BuildPackage> CreateScheduledCopyAsync(int sourcePackageId, string createdBy, int? workOrderLineId = null)
    {
        var source = await _db.BuildPackages
            .Include(p => p.Parts)
            .Include(p => p.BuildFileInfo)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == sourcePackageId)
            ?? throw new InvalidOperationException("Source build package not found.");

        // Determine copy suffix — check existing copies to avoid name collisions
        var baseName = source.Name;
        var existingNames = await _db.BuildPackages
            .Where(p => p.Name.StartsWith(baseName))
            .Select(p => p.Name)
            .ToListAsync();

        var copyNumber = 1;
        string newName;
        do
        {
            newName = $"{baseName} (Run {copyNumber})";
            copyNumber++;
        } while (existingNames.Contains(newName));

        var clone = new BuildPackage
        {
            Name = newName,
            Description = source.Description,
            MachineId = source.MachineId,
            Status = BuildPackageStatus.Draft,
            Material = source.Material,
            EstimatedDurationHours = source.EstimatedDurationHours,
            Notes = source.Notes,
            IsSlicerDataEntered = source.IsSlicerDataEntered,
            BuildParameters = source.BuildParameters,
            DepowderingHours = source.DepowderingHours,
            HeatTreatmentHours = source.HeatTreatmentHours,
            WireEdmHours = source.WireEdmHours,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        // If source had slicer data, the copy starts as Sliced so it's ready to schedule faster
        if (source.IsSlicerDataEntered)
            clone.Status = BuildPackageStatus.Sliced;

        _db.BuildPackages.Add(clone);
        await _db.SaveChangesAsync();

        // Clone parts — override WO line link if a specific line was provided
        foreach (var sp in source.Parts)
        {
            _db.BuildPackageParts.Add(new BuildPackagePart
            {
                BuildPackageId = clone.Id,
                PartId = sp.PartId,
                Quantity = sp.Quantity,
                StackLevel = sp.StackLevel,
                SlicerNotes = sp.SlicerNotes,
                WorkOrderLineId = workOrderLineId ?? sp.WorkOrderLineId
            });
        }

        // Clone build file info (slicer metadata)
        if (source.BuildFileInfo != null)
        {
            _db.BuildFileInfos.Add(new BuildFileInfo
            {
                BuildPackageId = clone.Id,
                FileName = source.BuildFileInfo.FileName,
                LayerCount = source.BuildFileInfo.LayerCount,
                BuildHeightMm = source.BuildFileInfo.BuildHeightMm,
                EstimatedPrintTimeHours = source.BuildFileInfo.EstimatedPrintTimeHours,
                EstimatedPowderKg = source.BuildFileInfo.EstimatedPowderKg,
                PartPositionsJson = source.BuildFileInfo.PartPositionsJson,
                SlicerSoftware = source.BuildFileInfo.SlicerSoftware,
                SlicerVersion = source.BuildFileInfo.SlicerVersion,
                ImportedBy = createdBy,
                ImportedDate = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        await CreateRevisionAsync(clone.Id, createdBy, $"Scheduled run from build \"{source.Name}\" (#{source.Id})");

        return clone;
    }

    public async Task<BuildPackagePart> AddPartToPackageAsync(int packageId, int partId, int quantity, int? workOrderLineId = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

        var part = new BuildPackagePart
        {
            BuildPackageId = packageId,
            PartId = partId,
            Quantity = quantity,
            WorkOrderLineId = workOrderLineId
        };

        _db.BuildPackageParts.Add(part);
        await _db.SaveChangesAsync();

        await CreateRevisionAsync(packageId, "System", $"Added part {partId} x{quantity}");

        return part;
    }

    public async Task RemovePartFromPackageAsync(int packagePartId)
    {
        var part = await _db.BuildPackageParts.FindAsync(packagePartId);
        if (part == null) throw new InvalidOperationException("Build package part not found.");
        var packageId = part.BuildPackageId;
        _db.BuildPackageParts.Remove(part);
        await _db.SaveChangesAsync();

        await CreateRevisionAsync(packageId, "System", $"Removed part entry {packagePartId}");
    }

    public async Task<BuildPackagePart> UpdatePartInPackageAsync(int packagePartId, int quantity, int stackLevel, string? slicerNotes = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(stackLevel, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stackLevel, 3);

        var part = await _db.BuildPackageParts.FindAsync(packagePartId)
            ?? throw new InvalidOperationException("Build package part not found.");

        var changed = part.Quantity != quantity || part.StackLevel != stackLevel || part.SlicerNotes != slicerNotes;
        part.Quantity = quantity;
        part.StackLevel = stackLevel;
        part.SlicerNotes = slicerNotes;
        await _db.SaveChangesAsync();

        if (changed)
            await CreateRevisionAsync(part.BuildPackageId, "System",
                $"Updated part entry {packagePartId}: qty={quantity}, stack={stackLevel}x");

        return part;
    }

    public async Task<BuildFileInfo?> GetBuildFileInfoAsync(int packageId)
    {
        return await _db.BuildFileInfos
            .FirstOrDefaultAsync(f => f.BuildPackageId == packageId);
    }

    public async Task<BuildFileInfo> SaveBuildFileInfoAsync(BuildFileInfo info)
    {
        var existing = await _db.BuildFileInfos
            .FirstOrDefaultAsync(f => f.BuildPackageId == info.BuildPackageId);

        if (existing != null)
        {
            existing.FileName = info.FileName;
            existing.LayerCount = info.LayerCount;
            existing.BuildHeightMm = info.BuildHeightMm;
            existing.EstimatedPrintTimeHours = info.EstimatedPrintTimeHours;
            existing.EstimatedPowderKg = info.EstimatedPowderKg;
            existing.PartPositionsJson = info.PartPositionsJson;
            existing.SlicerSoftware = info.SlicerSoftware;
            existing.SlicerVersion = info.SlicerVersion;
            existing.ImportedDate = DateTime.UtcNow;
            existing.ImportedBy = info.ImportedBy;
            await _db.SaveChangesAsync();

            await UpdateBuildDurationFromSliceAsync(info.BuildPackageId);
            await CreateRevisionAsync(info.BuildPackageId, info.ImportedBy, "Build file updated");

            return existing;
        }

        info.ImportedDate = DateTime.UtcNow;
        _db.BuildFileInfos.Add(info);
        await _db.SaveChangesAsync();

        await UpdateBuildDurationFromSliceAsync(info.BuildPackageId);
        await CreateRevisionAsync(info.BuildPackageId, info.ImportedBy, "Build file imported");

        return info;
    }

    public async Task<BuildFileInfo> GenerateSpoofBuildFileAsync(int packageId, string importedBy)
    {
        var package = await _db.BuildPackages
            .Include(p => p.Parts)
            .FirstOrDefaultAsync(p => p.Id == packageId);

        if (package == null) throw new InvalidOperationException("Build package not found.");

        var random = new Random();
        var totalParts = package.Parts.Sum(p => p.Quantity);
        var layerCount = random.Next(800, 3500);
        var buildHeight = Math.Round((decimal)(layerCount * 0.03), 2); // ~30µm layers
        var printTime = Math.Round((decimal)(layerCount * 0.012 + totalParts * 0.5), 1);
        var powderKg = Math.Round(buildHeight * 0.045m + totalParts * 0.15m, 2);

        var positions = package.Parts.Select((p, i) => new
        {
            partId = p.PartId,
            x = Math.Round(25.0 + (i % 5) * 45.0, 1),
            y = Math.Round(25.0 + (i / 5) * 45.0, 1),
            rotation = random.Next(0, 4) * 90
        }).ToList();

        var info = new BuildFileInfo
        {
            BuildPackageId = packageId,
            FileName = $"build_{packageId:D4}_spoof.cli",
            LayerCount = layerCount,
            BuildHeightMm = buildHeight,
            EstimatedPrintTimeHours = printTime,
            EstimatedPowderKg = powderKg,
            PartPositionsJson = JsonSerializer.Serialize(positions),
            SlicerSoftware = "Manual",
            SlicerVersion = "Spoof Generator v1.0",
            ImportedBy = importedBy,
            ImportedDate = DateTime.UtcNow
        };

        return await SaveBuildFileInfoAsync(info);
    }

    // ── Build Plate Execution (CHUNK-09) ────────────────────────

    public async Task UpdateBuildDurationFromSliceAsync(int buildPackageId)
    {
        var package = await _db.BuildPackages
            .Include(p => p.Parts)
            .Include(p => p.BuildFileInfo)
            .FirstOrDefaultAsync(p => p.Id == buildPackageId);

        if (package?.BuildFileInfo?.EstimatedPrintTimeHours == null) return;

        var printHours = (double)package.BuildFileInfo.EstimatedPrintTimeHours.Value;
        package.EstimatedDurationHours = printHours;
        package.LastModifiedDate = DateTime.UtcNow;

        // Update any linked SLS printing stage execution with new duration
        var slsStage = await _db.ProductionStages
            .FirstOrDefaultAsync(s => s.StageSlug == "sls-printing");
        if (slsStage != null)
        {
            var slsExecutions = await _db.StageExecutions
                .Where(e => e.BuildPackageId == buildPackageId
                    && e.ProductionStageId == slsStage.Id
                    && e.Status != StageExecutionStatus.Completed
                    && e.Status != StageExecutionStatus.Failed)
                .ToListAsync();

            foreach (var exec in slsExecutions)
            {
                exec.EstimatedHours = printHours;
                exec.LastModifiedDate = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int buildPackageId, string createdBy)
    {
        var package = await _db.BuildPackages
            .Include(p => p.BuildFileInfo)
            .Include(p => p.ScheduledJob)
            .Include(p => p.Parts)
            .FirstOrDefaultAsync(p => p.Id == buildPackageId);

        if (package == null) throw new InvalidOperationException("Build package not found.");

        var buildStages = await _db.ProductionStages
            .Where(s => s.IsBuildLevelStage && s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

        // Determine which build-level stages the parts actually require.
        // The print stage is always needed; post-print stages are only created
        // when at least one part in the build has that stage in its routing.
        var partIds = package.Parts.Select(p => p.PartId).Distinct().ToList();
        var requiredStageIds = await _db.PartStageRequirements
            .Where(r => partIds.Contains(r.PartId) && r.IsActive && r.IsRequired)
            .Select(r => r.ProductionStageId)
            .Distinct()
            .ToListAsync();
        var requiredStageIdSet = requiredStageIds.ToHashSet();

        // Also include stages that have a custom duration set on the package
        // (user explicitly configured them, so they should be scheduled).
        var packageConfiguredStages = buildStages
            .Where(s => s.StageSlug switch
            {
                "depowdering" => package.DepowderingHours.HasValue,
                "heat-treatment" => package.HeatTreatmentHours.HasValue,
                "wire-edm" => package.WireEdmHours.HasValue,
                _ => false
            })
            .Select(s => s.Id)
            .ToHashSet();

        buildStages = buildStages
            .Where(s => s.StageSlug == "sls-printing"
                || requiredStageIdSet.Contains(s.Id)
                || packageConfiguredStages.Contains(s.Id))
            .ToList();

        // Use the package's ScheduledJob, or create one if it doesn't exist
        var job = package.ScheduledJob;
        if (job == null)
        {
            var firstPart = await _db.BuildPackageParts
                .Where(bp => bp.BuildPackageId == buildPackageId)
                .Select(bp => bp.Part)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException(
                    "Cannot create build executions — the package has no parts. Add at least one part first.");

            job = new Job
            {
                JobNumber = await _numberSeq.NextAsync("Job"),
                PartId = firstPart.Id,
                Quantity = package.TotalPartCount,
                Status = JobStatus.Scheduled,
                Priority = JobPriority.Normal,
                ScheduledStart = package.ScheduledDate ?? DateTime.UtcNow,
                ScheduledEnd = (package.ScheduledDate ?? DateTime.UtcNow)
                    .AddHours(package.EstimatedDurationHours ?? 24),
                EstimatedHours = package.EstimatedDurationHours ?? 24,
                Notes = $"Build plate: {package.Name}",
                CreatedBy = createdBy,
                LastModifiedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
            _db.Jobs.Add(job);
            await _db.SaveChangesAsync();

            package.ScheduledJobId = job.Id;
            package.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // Build a lookup from string MachineId → int Id for stage machine resolution
        var machineLookup = await _db.Machines
            .Where(m => m.IsActive)
            .ToDictionaryAsync(m => m.MachineId, m => m.Id);

        // The build's SLS machine is already an int FK
        int? buildMachineIntId = package.MachineId;

        var executions = new List<StageExecution>();
        var sortOrder = 0;
        var currentStart = package.ScheduledDate ?? DateTime.UtcNow;

        foreach (var stage in buildStages)
        {
            var estimatedHours = GetBuildStageDuration(stage, package);

            // Resolve the correct machine for this stage:
            // The print stage uses the build's assigned machine;
            // other stages fall back to their configured default machine.
            int? machineId = null;
            if (stage.StageSlug == "sls-printing")
            {
                machineId = buildMachineIntId;
            }
            else if (!string.IsNullOrEmpty(stage.DefaultMachineId)
                     && machineLookup.TryGetValue(stage.DefaultMachineId, out var stageIntId))
            {
                machineId = stageIntId;
            }

            var scheduledEnd = currentStart.AddHours(estimatedHours);

            var execution = new StageExecution
            {
                JobId = job.Id,
                ProductionStageId = stage.Id,
                BuildPackageId = buildPackageId,
                Status = StageExecutionStatus.NotStarted,
                EstimatedHours = estimatedHours,
                SetupHours = stage.DefaultSetupMinutes / 60.0,
                QualityCheckRequired = stage.RequiresQualityCheck,
                SortOrder = sortOrder++,
                MachineId = machineId,
                ScheduledStartAt = currentStart,
                ScheduledEndAt = scheduledEnd,
                CreatedBy = createdBy,
                LastModifiedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            _db.StageExecutions.Add(execution);
            executions.Add(execution);

            // Next stage starts after this one ends
            currentStart = scheduledEnd;
        }

        await _db.SaveChangesAsync();
        return executions;
    }

    /// <summary>
    /// Resolves the estimated duration for a build-level stage.
    /// Uses package-specific overrides when set, slice data for the print stage,
    /// and falls back to the stage's default duration.
    /// </summary>
    private static double GetBuildStageDuration(ProductionStage stage, BuildPackage package)
    {
        return stage.StageSlug switch
        {
            "sls-printing" =>
                package.BuildFileInfo?.EstimatedPrintTimeHours is { } printHours ? (double)printHours
                : package.EstimatedDurationHours ?? stage.DefaultDurationHours,
            "depowdering" => package.DepowderingHours ?? stage.DefaultDurationHours,
            "heat-treatment" => package.HeatTreatmentHours ?? stage.DefaultDurationHours,
            "wire-edm" => package.WireEdmHours ?? stage.DefaultDurationHours,
            _ => stage.DefaultDurationHours
        };
    }

    public async Task<List<int>> CreatePartStageExecutionsAsync(int buildPackageId, string createdBy, DateTime? startAfter = null)
    {
        var package = await _db.BuildPackages
            .Include(p => p.Parts)
                .ThenInclude(pp => pp.Part)
            .FirstOrDefaultAsync(p => p.Id == buildPackageId);

        if (package == null) throw new InvalidOperationException("Build package not found.");

        // Check if per-part jobs already exist for this build (idempotent)
        var existingJobIds = await _db.Jobs
            .Where(j => j.Notes != null && j.Notes.Contains(package.Name)
                && j.Stages.Any(s => s.BuildPackageId == null))
            .Select(j => j.Id)
            .ToListAsync();

        // Also check for jobs linked via BuildPackagePart WorkOrderLineIds
        var woLineIds = package.Parts
            .Where(p => p.WorkOrderLineId.HasValue)
            .Select(p => p.WorkOrderLineId!.Value)
            .Distinct()
            .ToList();
        var partIds = package.Parts.Select(p => p.PartId).Distinct().ToList();

        var existingPartJobs = await _db.Jobs
            .Where(j => partIds.Contains(j.PartId)
                && j.Notes != null && j.Notes.Contains(package.Name))
            .Select(j => j.Id)
            .ToListAsync();

        if (existingPartJobs.Count > 0)
            return existingPartJobs;

        // Load all build-level stage IDs to skip
        var buildLevelStageIds = await _db.ProductionStages
            .Where(s => s.IsBuildLevelStage)
            .Select(s => s.Id)
            .ToListAsync();

        // Group parts by (PartId, WorkOrderLineId) so we create 1 job per part-type per WO line
        var partGroups = package.Parts
            .GroupBy(p => new { p.PartId, p.WorkOrderLineId })
            .ToList();

        var createdJobIds = new List<int>();
        var jobStartTime = startAfter ?? DateTime.UtcNow;

        foreach (var group in partGroups)
        {
            var representative = group.First();
            var totalQuantity = group.Sum(p => p.Quantity);

            // Load the part's routing
            var routing = await _db.PartStageRequirements
                .Where(r => r.PartId == group.Key.PartId && r.IsActive)
                .OrderBy(r => r.ExecutionOrder)
                .Include(r => r.ProductionStage)
                .ToListAsync();

            // Filter out build-level stages (already handled as build executions)
            var partStages = routing.Where(r => !buildLevelStageIds.Contains(r.ProductionStageId)).ToList();
            if (partStages.Count == 0) continue;

            var estimatedHours = partStages.Sum(r => r.GetEffectiveEstimatedHours()) * totalQuantity;

            var job = new Job
            {
                JobNumber = await _numberSeq.NextAsync("Job"),
                PartId = group.Key.PartId,
                WorkOrderLineId = group.Key.WorkOrderLineId,
                Quantity = totalQuantity,
                Status = JobStatus.Scheduled,
                Priority = JobPriority.Normal,
                ScheduledStart = jobStartTime,
                ScheduledEnd = jobStartTime.AddHours(estimatedHours),
                EstimatedHours = estimatedHours,
                Notes = $"Post-build processing: {representative.Part.PartNumber} x{totalQuantity} (build plate {package.Name})",
                CreatedBy = createdBy,
                LastModifiedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
            _db.Jobs.Add(job);
            await _db.SaveChangesAsync(); // Need ID for stage executions

            var sortOrder = 0;
            foreach (var req in partStages)
            {
                var execution = new StageExecution
                {
                    JobId = job.Id,
                    ProductionStageId = req.ProductionStageId,
                    Status = StageExecutionStatus.NotStarted,
                    EstimatedHours = req.GetEffectiveEstimatedHours() * totalQuantity,
                    EstimatedCost = req.EstimatedCost,
                    MaterialCost = req.MaterialCost,
                    SetupHours = req.SetupTimeMinutes.HasValue ? req.SetupTimeMinutes.Value / 60.0 : req.ProductionStage.DefaultSetupMinutes / 60.0,
                    QualityCheckRequired = req.ProductionStage.RequiresQualityCheck,
                    SortOrder = sortOrder++,
                    CreatedBy = createdBy,
                    LastModifiedBy = createdBy,
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                };
                _db.StageExecutions.Add(execution);
            }

            createdJobIds.Add(job.Id);
        }

        await _db.SaveChangesAsync();
        return createdJobIds;
    }

    public async Task<BuildPackageRevision> CreateRevisionAsync(int buildPackageId, string changedBy, string? notes = null)
    {
        var package = await _db.BuildPackages
            .Include(p => p.Parts)
                .ThenInclude(pp => pp.Part)
            .FirstOrDefaultAsync(p => p.Id == buildPackageId);

        if (package == null) throw new InvalidOperationException("Build package not found.");

        var nextRevision = (package.CurrentRevision ?? 0) + 1;

        var partsSnapshot = package.Parts.Select(p => new
        {
            p.PartId,
            PartNumber = p.Part?.PartNumber,
            p.Quantity,
            p.WorkOrderLineId
        }).ToList();

        var revision = new BuildPackageRevision
        {
            BuildPackageId = buildPackageId,
            RevisionNumber = nextRevision,
            RevisionDate = DateTime.UtcNow,
            ChangedBy = changedBy,
            ChangeNotes = notes,
            PartsSnapshotJson = JsonSerializer.Serialize(partsSnapshot),
            ParametersSnapshotJson = package.BuildParameters
        };

        _db.BuildPackageRevisions.Add(revision);

        package.CurrentRevision = nextRevision;
        package.LastModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return revision;
    }

    public async Task<List<BuildPackageRevision>> GetRevisionsAsync(int buildPackageId)
    {
        return await _db.BuildPackageRevisions
            .Where(r => r.BuildPackageId == buildPackageId)
            .OrderByDescending(r => r.RevisionNumber)
            .ToListAsync();
    }
}
