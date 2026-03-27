using Vectrik.Models;

namespace Vectrik.Services;

/// <summary>
/// Service for managing downstream program requirements when scheduling BuildPlate programs.
/// Validates that all required post-processing stages (depowder, EDM, finishing) have 
/// programs assigned before scheduling.
/// </summary>
public interface IDownstreamProgramService
{
    /// <summary>
    /// Get required downstream programs for a BuildPlate after SLS print.
    /// Returns stages that need programs with their assignment status.
    /// </summary>
    Task<List<DownstreamProgramRequirement>> GetRequiredProgramsAsync(int buildPlateProgramId);

    /// <summary>
    /// Validate all downstream programs are ready before scheduling.
    /// Returns validation result with missing programs.
    /// </summary>
    Task<DownstreamValidationResult> ValidateDownstreamReadinessAsync(int buildPlateProgramId);

    /// <summary>
    /// Auto-create placeholder programs for stages that don't have one.
    /// Uses default parameters from ProductionStage configuration.
    /// </summary>
    Task<List<MachineProgram>> CreatePlaceholderProgramsAsync(
        int buildPlateProgramId,
        List<int> stageIdsNeedingPrograms,
        string createdBy);
}

/// <summary>
/// Represents a downstream stage's program requirement status.
/// </summary>
/// <param name="ProcessStageId">The ManufacturingProcessStage ID</param>
/// <param name="StageName">Display name of the production stage</param>
/// <param name="MachineType">Type of machine required (e.g., "Depowder", "EDM")</param>
/// <param name="AssignedProgramId">ID of the assigned program, if any</param>
/// <param name="AssignedProgramName">Name of the assigned program, if any</param>
/// <param name="IsRequired">Whether this stage is required before completion</param>
/// <param name="HasDefaultParameters">Whether default parameters exist for auto-creation</param>
/// <param name="ExecutionOrder">Order in the manufacturing process</param>
public record DownstreamProgramRequirement(
    int ProcessStageId,
    string StageName,
    string MachineType,
    int? AssignedProgramId,
    string? AssignedProgramName,
    bool IsRequired,
    bool HasDefaultParameters,
    int ExecutionOrder);

/// <summary>
/// Result of validating downstream program readiness.
/// </summary>
/// <param name="IsValid">True if all required programs are assigned</param>
/// <param name="MissingPrograms">List of stages missing required programs</param>
/// <param name="Warnings">Non-blocking warnings (e.g., optional stages without programs)</param>
public record DownstreamValidationResult(
    bool IsValid,
    List<DownstreamProgramRequirement> MissingPrograms,
    List<string> Warnings);
