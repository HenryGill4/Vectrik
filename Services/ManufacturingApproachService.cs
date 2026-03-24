using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public class ManufacturingApproachService : IManufacturingApproachService
{
    private readonly TenantDbContext _db;

    public ManufacturingApproachService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<ManufacturingApproach>> GetAllAsync(bool activeOnly = true)
    {
        var query = _db.ManufacturingApproaches.AsQueryable();
        if (activeOnly)
            query = query.Where(a => a.IsActive);
        return await query.OrderBy(a => a.DisplayOrder).ToListAsync();
    }

    public async Task<ManufacturingApproach?> GetByIdAsync(int id)
    {
        return await _db.ManufacturingApproaches.FindAsync(id);
    }

    public async Task<ManufacturingApproach> CreateAsync(ManufacturingApproach approach)
    {
        _db.ManufacturingApproaches.Add(approach);
        await _db.SaveChangesAsync();
        return approach;
    }

    public async Task<ManufacturingApproach> UpdateAsync(ManufacturingApproach approach)
    {
        _db.ManufacturingApproaches.Update(approach);
        await _db.SaveChangesAsync();
        return approach;
    }

    public async Task DeleteAsync(int id)
    {
        var approach = await _db.ManufacturingApproaches.FindAsync(id);
        if (approach is null) throw new InvalidOperationException("Manufacturing approach not found.");
        approach.IsActive = false;
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<int> PropagateRoutingChangesAsync(int approachId)
    {
        var approach = await _db.ManufacturingApproaches.FindAsync(approachId);
        if (approach is null)
            throw new InvalidOperationException("Manufacturing approach not found.");

        var template = approach.ParsedRoutingTemplate;

        // Look up slug → ProductionStage catalog entry
        var allStages = await _db.ProductionStages.ToListAsync();
        var stageBySlug = allStages.ToDictionary(s => s.StageSlug, s => s, StringComparer.OrdinalIgnoreCase);

        // Resolve template entries to catalog stage IDs (skip unknown slugs)
        var resolvedTemplate = new List<(RoutingTemplateStage Entry, ProductionStage Catalog)>();
        foreach (var entry in template)
        {
            if (stageBySlug.TryGetValue(entry.Slug, out var catalogStage))
                resolvedTemplate.Add((entry, catalogStage));
        }

        // Find all active processes linked to this approach
        var processes = await _db.ManufacturingProcesses
            .Include(p => p.Stages)
            .Where(p => p.ManufacturingApproachId == approachId && p.IsActive)
            .ToListAsync();

        var updatedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var process in processes)
        {
            var existingByProductionStageId = process.Stages
                .ToDictionary(s => s.ProductionStageId, s => s);

            var templateProductionStageIds = new HashSet<int>(
                resolvedTemplate.Select(t => t.Catalog.Id));

            // ── Remove stages no longer in the template ──
            var stagesToRemove = process.Stages
                .Where(s => !templateProductionStageIds.Contains(s.ProductionStageId))
                .ToList();

            foreach (var orphan in stagesToRemove)
            {
                // Clear plate release reference if it points to this stage
                if (process.PlateReleaseStageId == orphan.Id)
                    process.PlateReleaseStageId = null;

                _db.ProcessStages.Remove(orphan);
                updatedCount++;
            }

            // ── Add or update stages from template ──
            ProcessStage? newPlateReleaseStage = null;
            var order = 1;

            foreach (var (entry, catalogStage) in resolvedTemplate)
            {
                if (existingByProductionStageId.TryGetValue(catalogStage.Id, out var existing))
                {
                    // Sync template-driven properties on the existing stage
                    var changed = false;

                    if (existing.ExecutionOrder != order)
                    { existing.ExecutionOrder = order; changed = true; }

                    if (existing.ProcessingLevel != entry.Level)
                    {
                        existing.ProcessingLevel = entry.Level;
                        existing.RunDurationMode = entry.Level switch
                        {
                            ProcessingLevel.Build => DurationMode.PerBuild,
                            ProcessingLevel.Batch => DurationMode.PerBatch,
                            _ => DurationMode.PerPart
                        };
                        if (existing.SetupTimeMinutes is not null)
                        {
                            existing.SetupDurationMode = entry.Level == ProcessingLevel.Build
                                ? DurationMode.PerBuild
                                : DurationMode.PerBatch;
                        }
                        changed = true;
                    }

                    if (existing.DurationFromBuildConfig != entry.DurationFromBuildConfig)
                    { existing.DurationFromBuildConfig = entry.DurationFromBuildConfig; changed = true; }

                    if (existing.BatchCapacityOverride != entry.BatchCapacityOverride)
                    { existing.BatchCapacityOverride = entry.BatchCapacityOverride; changed = true; }

                    var newMachineIds = entry.MachineIds.Count > 0
                        ? string.Join(",", entry.MachineIds)
                        : null;
                    if (existing.PreferredMachineIds != newMachineIds)
                    { existing.PreferredMachineIds = newMachineIds; changed = true; }

                    var needsProgram = entry.MachineIds.Count > 0 && existing.MachineProgramId is null;
                    if (existing.ProgramSetupRequired != needsProgram)
                    { existing.ProgramSetupRequired = needsProgram; changed = true; }

                    if (changed)
                    {
                        existing.LastModifiedDate = now;
                        updatedCount++;
                    }

                    if (entry.IsPlateReleaseTrigger)
                        newPlateReleaseStage = existing;
                }
                else
                {
                    // Scaffold a new process stage from the template + catalog defaults
                    var newStage = new ProcessStage
                    {
                        ManufacturingProcessId = process.Id,
                        ProductionStageId = catalogStage.Id,
                        ExecutionOrder = order,
                        ProcessingLevel = entry.Level,
                        DurationFromBuildConfig = entry.DurationFromBuildConfig,
                        SetupDurationMode = catalogStage.DefaultSetupMinutes > 0
                            ? (entry.Level == ProcessingLevel.Build ? DurationMode.PerBuild : DurationMode.PerBatch)
                            : DurationMode.None,
                        SetupTimeMinutes = catalogStage.DefaultSetupMinutes > 0
                            ? catalogStage.DefaultSetupMinutes
                            : null,
                        RunDurationMode = entry.Level switch
                        {
                            ProcessingLevel.Build => DurationMode.PerBuild,
                            ProcessingLevel.Batch => DurationMode.PerBatch,
                            _ => DurationMode.PerPart
                        },
                        RunTimeMinutes = !entry.DurationFromBuildConfig
                            ? catalogStage.DefaultDurationHours * 60
                            : null,
                        BatchCapacityOverride = entry.BatchCapacityOverride,
                        PreferredMachineIds = entry.MachineIds.Count > 0
                            ? string.Join(",", entry.MachineIds)
                            : null,
                        ProgramSetupRequired = entry.MachineIds.Count > 0,
                        IsRequired = true,
                        IsBlocking = true,
                        RequiresQualityCheck = catalogStage.StageSlug == "qc",
                        RequiresSerialNumber = catalogStage.RequiresSerialNumber,
                        CreatedBy = process.LastModifiedBy,
                        LastModifiedBy = process.LastModifiedBy,
                        CreatedDate = now,
                        LastModifiedDate = now
                    };

                    _db.ProcessStages.Add(newStage);

                    if (entry.IsPlateReleaseTrigger)
                        newPlateReleaseStage = newStage;

                    updatedCount++;
                }

                order++;
            }

            // ── Update process-level properties ──
            if (approach.DefaultBatchCapacity > 0 && process.DefaultBatchCapacity != approach.DefaultBatchCapacity)
            {
                process.DefaultBatchCapacity = approach.DefaultBatchCapacity;
                updatedCount++;
            }

            // Plate release: save once so new stages get IDs, then set reference
            if (newPlateReleaseStage is not null || stagesToRemove.Any() || resolvedTemplate.Count > 0)
            {
                await _db.SaveChangesAsync();

                if (newPlateReleaseStage is not null && process.PlateReleaseStageId != newPlateReleaseStage.Id)
                {
                    process.PlateReleaseStageId = newPlateReleaseStage.Id;
                    updatedCount++;
                }
                else if (newPlateReleaseStage is null && process.PlateReleaseStageId is not null)
                {
                    // No template stage marked as plate release — fall back to last build-level stage
                    var lastBuildStage = await _db.ProcessStages
                        .Where(s => s.ManufacturingProcessId == process.Id
                                    && s.ProcessingLevel == ProcessingLevel.Build)
                        .OrderByDescending(s => s.ExecutionOrder)
                        .FirstOrDefaultAsync();

                    var newId = lastBuildStage?.Id;
                    if (process.PlateReleaseStageId != newId)
                    {
                        process.PlateReleaseStageId = newId;
                        updatedCount++;
                    }
                }

                process.LastModifiedDate = now;
            }
        }

        if (updatedCount > 0)
            await _db.SaveChangesAsync();

        return updatedCount;
    }
}
