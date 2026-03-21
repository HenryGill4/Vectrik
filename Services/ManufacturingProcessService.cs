using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class ManufacturingProcessService : IManufacturingProcessService
{
    private readonly TenantDbContext _db;

    public ManufacturingProcessService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<ManufacturingProcess?> GetByPartIdAsync(int partId)
    {
        return await _db.ManufacturingProcesses
            .Include(p => p.Stages.OrderBy(s => s.ExecutionOrder))
                .ThenInclude(s => s.ProductionStage)
            .Include(p => p.PlateReleaseStage)
            .FirstOrDefaultAsync(p => p.PartId == partId);
    }

    public async Task<ManufacturingProcess?> GetByIdAsync(int id)
    {
        return await _db.ManufacturingProcesses
            .Include(p => p.Stages.OrderBy(s => s.ExecutionOrder))
                .ThenInclude(s => s.ProductionStage)
            .Include(p => p.PlateReleaseStage)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<ManufacturingProcess> CreateAsync(ManufacturingProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);

        var existingProcess = await _db.ManufacturingProcesses
            .AnyAsync(p => p.PartId == process.PartId);
        if (existingProcess)
            throw new InvalidOperationException($"Part {process.PartId} already has a manufacturing process.");

        _db.ManufacturingProcesses.Add(process);
        await _db.SaveChangesAsync();
        return process;
    }

    public async Task<ManufacturingProcess> UpdateAsync(ManufacturingProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);

        process.LastModifiedDate = DateTime.UtcNow;
        process.Version++;
        _db.ManufacturingProcesses.Update(process);
        await _db.SaveChangesAsync();
        return process;
    }

    public async Task DeleteAsync(int id)
    {
        var process = await _db.ManufacturingProcesses.FindAsync(id);
        if (process is null) throw new InvalidOperationException("Manufacturing process not found.");
        process.IsActive = false;
        process.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<ProcessStage> AddStageAsync(ProcessStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        // Auto-assign execution order if not set
        if (stage.ExecutionOrder <= 0)
        {
            var maxOrder = await _db.ProcessStages
                .Where(s => s.ManufacturingProcessId == stage.ManufacturingProcessId)
                .MaxAsync(s => (int?)s.ExecutionOrder) ?? 0;
            stage.ExecutionOrder = maxOrder + 1;
        }

        _db.ProcessStages.Add(stage);
        await _db.SaveChangesAsync();
        return stage;
    }

    public async Task<ProcessStage> UpdateStageAsync(ProcessStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        stage.LastModifiedDate = DateTime.UtcNow;
        _db.ProcessStages.Update(stage);
        await _db.SaveChangesAsync();
        return stage;
    }

    public async Task RemoveStageAsync(int stageId)
    {
        var stage = await _db.ProcessStages.FindAsync(stageId);
        if (stage is null) throw new InvalidOperationException("Process stage not found.");

        // Check if this stage is the plate release trigger
        var process = await _db.ManufacturingProcesses
            .FirstOrDefaultAsync(p => p.PlateReleaseStageId == stageId);
        if (process is not null)
        {
            process.PlateReleaseStageId = null;
        }

        _db.ProcessStages.Remove(stage);
        await _db.SaveChangesAsync();
    }

    public async Task ReorderStagesAsync(int processId, List<int> stageIdsInOrder)
    {
        ArgumentNullException.ThrowIfNull(stageIdsInOrder);

        var stages = await _db.ProcessStages
            .Where(s => s.ManufacturingProcessId == processId)
            .ToListAsync();

        for (int i = 0; i < stageIdsInOrder.Count; i++)
        {
            var stage = stages.FirstOrDefault(s => s.Id == stageIdsInOrder[i]);
            if (stage is not null)
            {
                stage.ExecutionOrder = i + 1;
                stage.LastModifiedDate = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<string>> ValidateProcessAsync(int processId)
    {
        var errors = new List<string>();

        var process = await _db.ManufacturingProcesses
            .Include(p => p.Stages)
            .FirstOrDefaultAsync(p => p.Id == processId);

        if (process is null)
        {
            errors.Add("Manufacturing process not found.");
            return errors;
        }

        if (!process.Stages.Any())
        {
            errors.Add("Process has no stages defined.");
            return errors;
        }

        // Check for duplicate execution orders
        var duplicateOrders = process.Stages
            .GroupBy(s => s.ExecutionOrder)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        foreach (var order in duplicateOrders)
        {
            errors.Add($"Duplicate execution order: {order}.");
        }

        // If process has build-level stages, plate release should be defined
        var hasBuildStages = process.Stages.Any(s => s.ProcessingLevel == ProcessingLevel.Build);
        if (hasBuildStages && process.PlateReleaseStageId is null)
        {
            errors.Add("Process has build-level stages but no plate release trigger stage defined.");
        }

        // Validate plate release stage is part of this process
        if (process.PlateReleaseStageId.HasValue)
        {
            var plateReleaseInProcess = process.Stages.Any(s => s.Id == process.PlateReleaseStageId.Value);
            if (!plateReleaseInProcess)
            {
                errors.Add("Plate release stage is not part of this process.");
            }
        }

        // Check stages with run duration but no time configured
        foreach (var stage in process.Stages.Where(s => s.RunDurationMode != DurationMode.None && s.RunTimeMinutes is null && !s.DurationFromBuildConfig))
        {
            errors.Add($"Stage at order {stage.ExecutionOrder} has run duration mode set but no run time configured.");
        }

        return errors;
    }

    public DurationResult CalculateStageDuration(ProcessStage stage, int partCount, int batchCount, double? buildConfigHours)
    {
        ArgumentNullException.ThrowIfNull(stage);

        if (stage.DurationFromBuildConfig && buildConfigHours.HasValue)
        {
            var totalFromConfig = buildConfigHours.Value * 60.0;
            return new DurationResult(0, totalFromConfig, totalFromConfig, $"{buildConfigHours.Value:F1}h from build config");
        }

        double setupMinutes = CalculateModeMinutes(stage.SetupDurationMode, stage.SetupTimeMinutes ?? 0, partCount, batchCount);
        double runMinutes = CalculateModeMinutes(stage.RunDurationMode, stage.RunTimeMinutes ?? 0, partCount, batchCount);
        double total = setupMinutes + runMinutes;

        var breakdown = BuildBreakdownString(stage, setupMinutes, runMinutes, partCount, batchCount);
        return new DurationResult(setupMinutes, runMinutes, total, breakdown);
    }

    public async Task<ManufacturingProcess> CloneProcessAsync(int sourceProcessId, int targetPartId, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy is required.", nameof(createdBy));

        var source = await GetByIdAsync(sourceProcessId)
            ?? throw new InvalidOperationException("Source process not found.");

        var existingTarget = await _db.ManufacturingProcesses
            .AnyAsync(p => p.PartId == targetPartId);
        if (existingTarget)
            throw new InvalidOperationException($"Part {targetPartId} already has a manufacturing process.");

        var clone = new ManufacturingProcess
        {
            PartId = targetPartId,
            ManufacturingApproachId = source.ManufacturingApproachId,
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            DefaultBatchCapacity = source.DefaultBatchCapacity,
            IsActive = true,
            Version = 1,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy
        };

        _db.ManufacturingProcesses.Add(clone);
        await _db.SaveChangesAsync();

        // Clone stages — track which cloned stage maps to the plate release trigger
        ProcessStage? plateReleaseClone = null;
        foreach (var sourceStage in source.Stages.OrderBy(s => s.ExecutionOrder))
        {
            var cloneStage = new ProcessStage
            {
                ManufacturingProcessId = clone.Id,
                ProductionStageId = sourceStage.ProductionStageId,
                ExecutionOrder = sourceStage.ExecutionOrder,
                ProcessingLevel = sourceStage.ProcessingLevel,
                SetupDurationMode = sourceStage.SetupDurationMode,
                SetupTimeMinutes = sourceStage.SetupTimeMinutes,
                RunDurationMode = sourceStage.RunDurationMode,
                RunTimeMinutes = sourceStage.RunTimeMinutes,
                DurationFromBuildConfig = sourceStage.DurationFromBuildConfig,
                BatchCapacityOverride = sourceStage.BatchCapacityOverride,
                AllowRebatching = sourceStage.AllowRebatching,
                ConsolidateBatchesAtStage = sourceStage.ConsolidateBatchesAtStage,
                AssignedMachineId = sourceStage.AssignedMachineId,
                RequiresSpecificMachine = sourceStage.RequiresSpecificMachine,
                PreferredMachineIds = sourceStage.PreferredMachineIds,
                HourlyRateOverride = sourceStage.HourlyRateOverride,
                MaterialCost = sourceStage.MaterialCost,
                IsRequired = sourceStage.IsRequired,
                IsBlocking = sourceStage.IsBlocking,
                AllowParallelExecution = sourceStage.AllowParallelExecution,
                AllowSkip = sourceStage.AllowSkip,
                RequiresQualityCheck = sourceStage.RequiresQualityCheck,
                RequiresSerialNumber = sourceStage.RequiresSerialNumber,
                IsExternalOperation = sourceStage.IsExternalOperation,
                ExternalTurnaroundDays = sourceStage.ExternalTurnaroundDays,
                StageParameters = sourceStage.StageParameters,
                RequiredMaterials = sourceStage.RequiredMaterials,
                RequiredTooling = sourceStage.RequiredTooling,
                QualityRequirements = sourceStage.QualityRequirements,
                SpecialInstructions = sourceStage.SpecialInstructions,
                CreatedBy = createdBy,
                LastModifiedBy = createdBy
            };
            _db.ProcessStages.Add(cloneStage);

            if (source.PlateReleaseStageId == sourceStage.Id)
                plateReleaseClone = cloneStage;
        }

        // Single save generates all stage IDs, then set plate release FK
        await _db.SaveChangesAsync();
        if (plateReleaseClone != null)
        {
            clone.PlateReleaseStageId = plateReleaseClone.Id;
            await _db.SaveChangesAsync();
        }
        return clone;
    }

    public async Task<ManufacturingProcess> CreateProcessFromApproachAsync(int partId, int approachId, string createdBy)
    {
        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy is required.", nameof(createdBy));

        var existing = await _db.ManufacturingProcesses.AnyAsync(p => p.PartId == partId);
        if (existing)
            throw new InvalidOperationException($"Part {partId} already has a manufacturing process.");

        var approach = await _db.ManufacturingApproaches.FindAsync(approachId)
            ?? throw new InvalidOperationException($"Manufacturing approach {approachId} not found.");

        var part = await _db.Parts.FindAsync(partId)
            ?? throw new InvalidOperationException($"Part {partId} not found.");

        // Look up all production stages by slug for template resolution
        var allStages = await _db.ProductionStages.ToListAsync();
        var stageBySlug = allStages.ToDictionary(s => s.StageSlug, s => s, StringComparer.OrdinalIgnoreCase);

        var template = approach.ParsedRoutingTemplate;

        var process = new ManufacturingProcess
        {
            PartId = partId,
            ManufacturingApproachId = approachId,
            Name = $"{part.PartNumber} — {approach.Name}",
            Description = $"Auto-created from {approach.Name} template",
            DefaultBatchCapacity = approach.DefaultBatchCapacity > 0 ? approach.DefaultBatchCapacity : 60,
            IsActive = true,
            Version = 1,
            CreatedBy = createdBy,
            LastModifiedBy = createdBy
        };

        _db.ManufacturingProcesses.Add(process);
        await _db.SaveChangesAsync();

        var order = 1;
        ProcessStage? plateReleaseStage = null;

        foreach (var entry in template)
        {
            if (!stageBySlug.TryGetValue(entry.Slug, out var catalogStage))
                continue;

            var processStage = new ProcessStage
            {
                ManufacturingProcessId = process.Id,
                ProductionStageId = catalogStage.Id,
                ExecutionOrder = order++,
                ProcessingLevel = entry.Level,
                DurationFromBuildConfig = entry.DurationFromBuildConfig,
                SetupDurationMode = catalogStage.DefaultSetupMinutes > 0
                    ? (entry.Level == ProcessingLevel.Build ? DurationMode.PerBuild : DurationMode.PerBatch)
                    : DurationMode.None,
                SetupTimeMinutes = catalogStage.DefaultSetupMinutes > 0 ? catalogStage.DefaultSetupMinutes : null,
                RunDurationMode = entry.Level switch
                {
                    ProcessingLevel.Build => DurationMode.PerBuild,
                    ProcessingLevel.Batch => DurationMode.PerBatch,
                    ProcessingLevel.Part => DurationMode.PerPart,
                    _ => DurationMode.PerPart
                },
                RunTimeMinutes = !entry.DurationFromBuildConfig ? catalogStage.DefaultDurationHours * 60 : null,
                BatchCapacityOverride = entry.BatchCapacityOverride,
                IsRequired = true,
                IsBlocking = true,
                RequiresQualityCheck = catalogStage.StageSlug == "qc",
                RequiresSerialNumber = catalogStage.RequiresSerialNumber,
                CreatedBy = createdBy,
                LastModifiedBy = createdBy
            };

            _db.ProcessStages.Add(processStage);
            await _db.SaveChangesAsync();

            if (entry.IsPlateReleaseTrigger)
            {
                plateReleaseStage = processStage;
            }
        }

        // Set plate release trigger — use template marker, or default to last build-level stage
        if (plateReleaseStage != null)
        {
            process.PlateReleaseStageId = plateReleaseStage.Id;
        }
        else
        {
            var lastBuildStage = await _db.ProcessStages
                .Where(s => s.ManufacturingProcessId == process.Id && s.ProcessingLevel == ProcessingLevel.Build)
                .OrderByDescending(s => s.ExecutionOrder)
                .FirstOrDefaultAsync();
            if (lastBuildStage != null)
            {
                process.PlateReleaseStageId = lastBuildStage.Id;
            }
        }

        await _db.SaveChangesAsync();
        return process;
    }

    private static double CalculateModeMinutes(DurationMode mode, double timeMinutes, int partCount, int batchCount)
    {
        return mode switch
        {
            DurationMode.None => 0,
            DurationMode.PerBuild => timeMinutes,
            DurationMode.PerBatch => timeMinutes * batchCount,
            DurationMode.PerPart => timeMinutes * partCount,
            _ => 0
        };
    }

    private static string BuildBreakdownString(ProcessStage stage, double setupMinutes, double runMinutes, int partCount, int batchCount)
    {
        var parts = new List<string>();

        if (setupMinutes > 0)
        {
            var setupLabel = stage.SetupDurationMode switch
            {
                DurationMode.PerBuild => "setup (per build)",
                DurationMode.PerBatch => $"setup ({stage.SetupTimeMinutes}min × {batchCount} batches)",
                DurationMode.PerPart => $"setup ({stage.SetupTimeMinutes}min × {partCount} parts)",
                _ => "setup"
            };
            parts.Add($"{setupMinutes:F0}min {setupLabel}");
        }

        if (runMinutes > 0)
        {
            var runLabel = stage.RunDurationMode switch
            {
                DurationMode.PerBuild => "run (per build)",
                DurationMode.PerBatch => $"run ({stage.RunTimeMinutes}min × {batchCount} batches)",
                DurationMode.PerPart => $"run ({stage.RunTimeMinutes}min × {partCount} parts)",
                _ => "run"
            };
            parts.Add($"{runMinutes:F0}min {runLabel}");
        }

        if (parts.Count == 0) return "0min";

        var total = setupMinutes + runMinutes;
        return $"{string.Join(" + ", parts)} = {total:F0}min ({total / 60.0:F1}h)";
    }
}
