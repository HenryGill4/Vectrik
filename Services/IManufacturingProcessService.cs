using Vectrik.Models;

namespace Vectrik.Services;

/// <summary>
/// Manages ManufacturingProcess definitions and their ProcessStages.
/// Provides duration calculation and build expansion logic.
/// </summary>
public interface IManufacturingProcessService
{
    /// <summary>
    /// Load the manufacturing process for a part, including stages ordered by ExecutionOrder.
    /// </summary>
    Task<ManufacturingProcess?> GetByPartIdAsync(int partId);

    /// <summary>
    /// Load a manufacturing process by its own Id, including stages.
    /// </summary>
    Task<ManufacturingProcess?> GetByIdAsync(int id);

    /// <summary>
    /// Create a new manufacturing process for a part.
    /// </summary>
    Task<ManufacturingProcess> CreateAsync(ManufacturingProcess process);

    /// <summary>
    /// Update an existing manufacturing process.
    /// </summary>
    Task<ManufacturingProcess> UpdateAsync(ManufacturingProcess process);

    /// <summary>
    /// Soft-delete a manufacturing process.
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// Add a stage to a manufacturing process.
    /// </summary>
    Task<ProcessStage> AddStageAsync(ProcessStage stage);

    /// <summary>
    /// Update an existing process stage.
    /// </summary>
    Task<ProcessStage> UpdateStageAsync(ProcessStage stage);

    /// <summary>
    /// Remove a stage from a manufacturing process.
    /// </summary>
    Task RemoveStageAsync(int stageId);

    /// <summary>
    /// Reorder stages within a process. Accepts a list of stage IDs in desired order.
    /// </summary>
    Task ReorderStagesAsync(int processId, List<int> stageIdsInOrder);

    /// <summary>
    /// Validate a process definition for completeness.
    /// Returns a list of validation messages (empty = valid).
    /// </summary>
    Task<List<string>> ValidateProcessAsync(int processId);

    /// <summary>
    /// Calculate the total duration for a stage given part/batch/build counts.
    /// </summary>
    DurationResult CalculateStageDuration(ProcessStage stage, int partCount, int batchCount, double? buildConfigHours);

    /// <summary>
    /// Calculate the total duration for a stage, optionally using a specific program's duration data.
    /// When machineProgramId is provided, the program's duration fields take priority over stage defaults.
    /// Falls back to CalculateStageDuration(stage, partCount, batchCount, buildConfigHours) if program has no duration data.
    /// </summary>
    Task<DurationResult> CalculateStageDurationWithProgramAsync(
        ProcessStage stage, int partCount, int batchCount, double? buildConfigHours, int? machineProgramId);

    /// <summary>
    /// Clone a process from one part to another.
    /// </summary>
    Task<ManufacturingProcess> CloneProcessAsync(int sourceProcessId, int targetPartId, string createdBy);

    /// <summary>
    /// Scaffolds a ManufacturingProcess for a part from a ManufacturingApproach's routing template.
    /// Creates process stages with defaults from the ProductionStage catalog and template hints.
    /// </summary>
    Task<ManufacturingProcess> CreateProcessFromApproachAsync(int partId, int approachId, string createdBy);

    /// <summary>
    /// Returns all ProcessStages where ProgramSetupRequired is true (machines assigned, no program linked).
    /// Includes ProductionStage catalog data and the owning ManufacturingProcess/Part.
    /// </summary>
    Task<List<ProcessStage>> GetStagesPendingProgramSetupAsync();

    /// <summary>
    /// Links a MachineProgram to a ProcessStage and clears ProgramSetupRequired.
    /// </summary>
    Task LinkProgramToStageAsync(int processStageId, int machineProgramId);
}

/// <summary>
/// Result of a compound duration calculation for a single process stage.
/// </summary>
public record DurationResult(
    double SetupMinutes,
    double RunMinutes,
    double TotalMinutes,
    string HumanReadableBreakdown);
