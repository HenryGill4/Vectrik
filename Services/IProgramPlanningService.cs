using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

/// <summary>
/// Program planning service for BuildPlate (SLS) programs.
/// Handles CRUD, part assignment, slicer data, revisions, and scheduled copies.
/// Complements IMachineProgramService with scheduling-oriented operations.
/// </summary>
public interface IProgramPlanningService
{
    // ═══════════════════════════════════════════════════════════
    // Build Plate CRUD
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns all BuildPlate-type programs.
    /// </summary>
    Task<List<MachineProgram>> GetAllBuildPlateProgramsAsync();

    /// <summary>
    /// Returns BuildPlate programs that contain a specific part.
    /// </summary>
    Task<List<MachineProgram>> GetBuildPlatesForPartAsync(int partId);

    /// <summary>
    /// Gets a BuildPlate program by ID with ProgramParts loaded.
    /// </summary>
    Task<MachineProgram?> GetBuildPlateByIdAsync(int id);

    /// <summary>
    /// Creates a new BuildPlate program.
    /// </summary>
    Task<MachineProgram> CreateBuildPlateAsync(MachineProgram program, string createdBy);

    /// <summary>
    /// Updates a BuildPlate program.
    /// </summary>
    Task<MachineProgram> UpdateBuildPlateAsync(MachineProgram program, string modifiedBy);

    /// <summary>
    /// Deletes a BuildPlate program (soft-delete via Archived status).
    /// </summary>
    Task DeleteBuildPlateAsync(int programId);

    /// <summary>
    /// Deletes a BuildPlate program AND cancels all downstream work:
    /// build-level job, per-part jobs, and all stage executions linked to this program.
    /// </summary>
    Task DeleteBuildWithDownstreamAsync(int programId, string deletedBy);

    // ═══════════════════════════════════════════════════════════
    // Scheduled Copies (Print Runs)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a scheduled copy of an existing BuildPlate program.
    /// The original program stays as a template — each copy is a separate print run.
    /// Sets SourceProgramId on the copy for tracking.
    /// </summary>
    Task<MachineProgram> CreateScheduledCopyAsync(int sourceProgramId, string createdBy, int? workOrderLineId = null);

    /// <summary>
    /// Gets all runs (scheduled copies) created from a source program.
    /// </summary>
    Task<List<MachineProgram>> GetRunsForSourceProgramAsync(int sourceProgramId);

    // ═══════════════════════════════════════════════════════════
    // Part Assignment
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Adds a part to a BuildPlate program.
    /// </summary>
    Task<ProgramPart> AddPartToProgramAsync(int programId, int partId, int quantity, int? workOrderLineId = null);

    /// <summary>
    /// Updates a part entry on a BuildPlate program.
    /// </summary>
    Task<ProgramPart> UpdateProgramPartAsync(int programPartId, int quantity, int stackLevel, string? positionNotes = null);

    /// <summary>
    /// Removes a part from a BuildPlate program.
    /// </summary>
    Task RemoveProgramPartAsync(int programPartId);

    // ═══════════════════════════════════════════════════════════
    // Slicer Data
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Updates slicer metadata on a BuildPlate program after slicer import.
    /// </summary>
    Task UpdateSlicerDataAsync(
        int programId,
        double? estimatedPrintHours,
        int? layerCount = null,
        double? buildHeightMm = null,
        double? estimatedPowderKg = null,
        string? slicerFileName = null,
        string? slicerSoftware = null,
        string? slicerVersion = null,
        string? partPositionsJson = null);

    /// <summary>
    /// Updates the program's EstimatedPrintHours from slicer data.
    /// </summary>
    Task UpdateDurationFromSliceAsync(int programId);

    // ═══════════════════════════════════════════════════════════
    // Revisions / History
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a revision snapshot for a program change.
    /// </summary>
    Task<ProgramRevision> CreateRevisionAsync(int programId, string changedBy, string? notes = null);

    /// <summary>
    /// Gets all revisions for a program.
    /// </summary>
    Task<List<ProgramRevision>> GetRevisionsAsync(int programId);

    // ═══════════════════════════════════════════════════════════
    // Name Generation
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Generates a formatted program name using token-based template.
    /// Tokens: {PARTS}, {MACHINE}, {DATE}, {SEQ}, {MATERIAL}
    /// </summary>
    Task<string> GenerateProgramNameAsync(List<int> partIds, int machineId = 0, string? template = null);
}
