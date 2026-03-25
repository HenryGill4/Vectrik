using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

/// <summary>
/// Service for managing downstream program requirements when scheduling BuildPlate programs.
/// Validates that all required post-processing stages (depowder, EDM, finishing) have 
/// programs assigned before scheduling.
/// </summary>
public class DownstreamProgramService : IDownstreamProgramService
{
    private readonly TenantDbContext _db;
    private readonly IMachineProgramService _programService;

    public DownstreamProgramService(TenantDbContext db, IMachineProgramService programService)
    {
        _db = db;
        _programService = programService;
    }

    /// <inheritdoc />
    public async Task<List<DownstreamProgramRequirement>> GetRequiredProgramsAsync(int buildPlateProgramId)
    {
        var buildPlateProgram = await _db.MachinePrograms
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.Part)
                    .ThenInclude(p => p!.ManufacturingProcess)
                        .ThenInclude(mp => mp!.Stages.OrderBy(s => s.ExecutionOrder))
                            .ThenInclude(ps => ps.ProductionStage)
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.Part)
                    .ThenInclude(p => p!.ManufacturingProcess)
                        .ThenInclude(mp => mp!.Stages)
                            .ThenInclude(ps => ps.AssignedMachine)
            .Include(p => p.ProgramParts)
                .ThenInclude(pp => pp.Part)
                    .ThenInclude(p => p!.ManufacturingProcess)
                        .ThenInclude(mp => mp!.Stages)
                            .ThenInclude(ps => ps.MachineProgram)
            .FirstOrDefaultAsync(p => p.Id == buildPlateProgramId);

        if (buildPlateProgram == null)
            return [];

        var requirements = new List<DownstreamProgramRequirement>();
        var seenStages = new HashSet<int>();

        // Collect all unique downstream stages from all parts in this build plate
        foreach (var programPart in buildPlateProgram.ProgramParts)
        {
            var process = programPart.Part?.ManufacturingProcess;
            if (process == null) continue;

            // Find stages AFTER the SLS print stage (Build-level stages with higher execution order)
            var printStage = process.Stages
                .Where(s => s.ProcessingLevel == ProcessingLevel.Build)
                .OrderBy(s => s.ExecutionOrder)
                .FirstOrDefault();

            if (printStage == null) continue;

            // Get all downstream stages (Batch or Part level, or Build stages after print)
            var downstreamStages = process.Stages
                .Where(s => s.ExecutionOrder > printStage.ExecutionOrder)
                .OrderBy(s => s.ExecutionOrder)
                .ToList();

            foreach (var stage in downstreamStages)
            {
                if (seenStages.Contains(stage.Id)) continue;
                seenStages.Add(stage.Id);

                // Determine machine type from assigned machine or production stage
                var machineType = stage.AssignedMachine?.MachineType
                    ?? stage.ProductionStage?.Department
                    ?? "Unknown";

                var requirement = new DownstreamProgramRequirement(
                    ProcessStageId: stage.Id,
                    StageName: stage.ProductionStage?.Name ?? $"Stage {stage.ExecutionOrder}",
                    MachineType: machineType,
                    AssignedProgramId: stage.MachineProgramId,
                    AssignedProgramName: stage.MachineProgram?.Name,
                    IsRequired: stage.IsRequired && !stage.AllowSkip,
                    HasDefaultParameters: stage.RunTimeMinutes.HasValue || stage.ProductionStage?.DefaultDurationHours > 0,
                    ExecutionOrder: stage.ExecutionOrder
                );

                requirements.Add(requirement);
            }
        }

        return requirements.OrderBy(r => r.ExecutionOrder).ToList();
    }

    /// <inheritdoc />
    public async Task<DownstreamValidationResult> ValidateDownstreamReadinessAsync(int buildPlateProgramId)
    {
        var requirements = await GetRequiredProgramsAsync(buildPlateProgramId);

        // Only block scheduling for required stages that have no program AND no defaults.
        // Stages with configured durations (depowder, EDM, CNC, etc.) can be scheduled
        // using their process stage defaults without an explicit program assignment.
        var missingPrograms = requirements
            .Where(r => r.IsRequired && !r.AssignedProgramId.HasValue && !r.HasDefaultParameters)
            .ToList();

        var warnings = new List<string>();

        // Add warnings for optional stages without programs
        var optionalWithoutPrograms = requirements
            .Where(r => !r.IsRequired && !r.AssignedProgramId.HasValue)
            .ToList();

        foreach (var opt in optionalWithoutPrograms)
        {
            if (opt.HasDefaultParameters)
            {
                warnings.Add($"Optional stage '{opt.StageName}' has no program but can use defaults");
            }
            else
            {
                warnings.Add($"Optional stage '{opt.StageName}' has no program and no defaults configured");
            }
        }

        // Add warnings for required stages using defaults (no explicit program)
        var requiredUsingDefaults = requirements
            .Where(r => r.IsRequired && !r.AssignedProgramId.HasValue && r.HasDefaultParameters)
            .ToList();

        foreach (var req in requiredUsingDefaults)
        {
            warnings.Add($"Required stage '{req.StageName}' will use default parameters (no program assigned)");
        }

        return new DownstreamValidationResult(
            IsValid: !missingPrograms.Any(),
            MissingPrograms: missingPrograms,
            Warnings: warnings
        );
    }

    /// <inheritdoc />
    public async Task<List<MachineProgram>> CreatePlaceholderProgramsAsync(
        int buildPlateProgramId,
        List<int> stageIdsNeedingPrograms,
        string createdBy)
    {
        var createdPrograms = new List<MachineProgram>();

        var buildPlateProgram = await _db.MachinePrograms
            .Include(p => p.ProgramParts)
            .FirstOrDefaultAsync(p => p.Id == buildPlateProgramId);

        if (buildPlateProgram == null)
            return createdPrograms;

        var processStages = await _db.ProcessStages
            .Include(ps => ps.ProductionStage)
            .Include(ps => ps.AssignedMachine)
            .Where(ps => stageIdsNeedingPrograms.Contains(ps.Id))
            .ToListAsync();

        foreach (var stage in processStages)
        {
            // Determine program type - all downstream programs are Standard type
            var programType = ProgramType.Standard;

            // Calculate default duration from stage configuration
            var defaultDuration = stage.RunTimeMinutes.HasValue
                ? stage.RunTimeMinutes.Value / 60.0
                : stage.ProductionStage?.DefaultDurationHours ?? 1.0;

            var program = new MachineProgram
            {
                Name = $"{buildPlateProgram.Name}-{stage.ProductionStage?.Name ?? "Stage"}",
                ProgramNumber = $"AUTO-{buildPlateProgramId}-{stage.Id}",
                ProgramType = programType,
                Status = ProgramStatus.Active,
                ScheduleStatus = ProgramScheduleStatus.None,
                EstimatedPrintHours = defaultDuration,
                MachineId = stage.AssignedMachineId,
                MachineType = stage.AssignedMachine?.MachineType,
                ProcessStageId = stage.Id,
                CreatedBy = createdBy,
                LastModifiedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            _db.MachinePrograms.Add(program);
            await _db.SaveChangesAsync();

            // Link the program to the process stage
            stage.MachineProgramId = program.Id;
            stage.ProgramSetupRequired = false;
            stage.LastModifiedBy = createdBy;
            stage.LastModifiedDate = DateTime.UtcNow;

            createdPrograms.Add(program);
        }

        await _db.SaveChangesAsync();

        return createdPrograms;
    }
}
