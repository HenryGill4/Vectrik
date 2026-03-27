using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public interface IMachineProgramService
{
    // ── CRUD ─────────────────────────────────────────────────
    Task<MachineProgram?> GetByIdAsync(int id);
    Task<List<MachineProgram>> GetAllAsync();
    Task<List<MachineProgram>> GetProgramsForPartAsync(int partId);
    Task<List<MachineProgram>> GetProgramsForMachineAsync(int machineId);
    Task<MachineProgram?> GetProgramForStageAsync(int processStageId);

    /// <summary>
    /// Returns active programs that match a specific part and optionally a specific machine.
    /// Used by scheduling pipeline to resolve the best program for a stage execution.
    /// Queries the MachineAssignments join table when machineId is provided.
    /// </summary>
    Task<List<MachineProgram>> GetActiveProgramsAsync(int? partId = null, int? machineId = null, int? processStageId = null);

    Task<MachineProgram> CreateAsync(MachineProgram program, string createdBy);
    Task UpdateAsync(MachineProgram program, string modifiedBy);
    Task DeleteAsync(int id);

    // ── Machine Assignments ──────────────────────────────────

    /// <summary>
    /// Returns all machine assignments for a program, including Machine navigation.
    /// </summary>
    Task<List<MachineProgramAssignment>> GetMachineAssignmentsAsync(int programId);

    /// <summary>
    /// Assigns a machine to a program. If the program has no other assignments,
    /// the first one is automatically marked as preferred.
    /// </summary>
    Task<MachineProgramAssignment> AssignMachineAsync(int programId, int machineId, string assignedBy, bool isPreferred = false, string? notes = null);

    /// <summary>
    /// Removes a machine assignment. If the removed assignment was preferred,
    /// the next remaining assignment (if any) is promoted to preferred.
    /// </summary>
    Task UnassignMachineAsync(int programId, int machineId);

    /// <summary>
    /// Sets which machine assignment is the preferred one for a program.
    /// Clears the preferred flag on all other assignments for the same program.
    /// </summary>
    Task SetPreferredMachineAsync(int programId, int machineId);

    /// <summary>
    /// Replaces all machine assignments for a program in a single operation.
    /// Used by the bulk-assign UI in the editor modal.
    /// </summary>
    Task SyncMachineAssignmentsAsync(int programId, List<int> machineIds, string assignedBy);

    // ── Versioning ───────────────────────────────────────────
    /// <summary>
    /// Creates a new version of the program. The old version is marked Superseded.
    /// </summary>
    Task<MachineProgram> CreateNewVersionAsync(int programId, string createdBy);

    // ── File Management ──────────────────────────────────────
    Task<MachineProgramFile> UploadFileAsync(int programId, string fileName, Stream fileStream, long fileSize, string uploadedBy, string? description = null);
    Task<List<MachineProgramFile>> GetFilesAsync(int programId);
    Task<MachineProgramFile?> GetFileByIdAsync(int fileId);
    Task DeleteFileAsync(int fileId);
    Task SetPrimaryFileAsync(int programId, int fileId);

    // ── Cloning ──────────────────────────────────────────────
    Task<MachineProgram> CloneProgramAsync(int sourceProgramId, int? targetPartId, int? targetMachineId, string createdBy);

    // ── Parameter Templates ──────────────────────────────────
    /// <summary>
    /// Returns default JSON parameter template for a given machine type.
    /// </summary>
    string GetDefaultParametersForMachineType(string machineType);

    // ── Tooling Management ───────────────────────────────────

    /// <summary>
    /// Returns all active tooling items for a program, including linked MachineComponent data.
    /// </summary>
    Task<List<ProgramToolingItem>> GetToolingItemsAsync(int programId);

    /// <summary>
    /// Creates or updates a tooling item for a program.
    /// </summary>
    Task<ProgramToolingItem> SaveToolingItemAsync(ProgramToolingItem item, string modifiedBy);

    /// <summary>
    /// Removes a tooling item (soft-delete).
    /// </summary>
    Task DeleteToolingItemAsync(int toolingItemId);

    /// <summary>
    /// Checks if all tooling for a program is ready for production.
    /// Returns a list of alerts for any components that need maintenance or are approaching wear limits.
    /// An empty list means all tooling is ready.
    /// </summary>
    Task<List<ToolingReadinessAlert>> CheckToolingReadinessAsync(int programId);

    // ── Program Feedback ─────────────────────────────────────

    /// <summary>
    /// Returns feedback for a program, newest first.
    /// </summary>
    Task<List<ProgramFeedback>> GetFeedbackAsync(int programId, ProgramFeedbackStatus? statusFilter = null);

    /// <summary>
    /// Submits operator feedback on a program.
    /// </summary>
    Task<ProgramFeedback> SubmitFeedbackAsync(ProgramFeedback feedback);

    /// <summary>
    /// Engineer reviews and resolves feedback.
    /// </summary>
    Task<ProgramFeedback> ReviewFeedbackAsync(int feedbackId, ProgramFeedbackStatus newStatus, string reviewedBy, string? resolution = null);

    // ── Execution History (for learning dashboard) ───────────

    /// <summary>
    /// Returns recent stage executions that used this program, for learning/trend display.
    /// </summary>
    Task<List<StageExecution>> GetExecutionHistoryAsync(int programId, int maxResults = 20);

    /// <summary>
    /// Returns the count of unresolved feedback items for display in program cards.
    /// </summary>
    Task<Dictionary<int, int>> GetUnresolvedFeedbackCountsAsync(List<int> programIds);

    // ── Program Part Operations ────────────────────────────────

    /// <summary>
    /// Returns all BuildPlate-type programs, optionally filtered by status.
    /// </summary>
    Task<List<MachineProgram>> GetBuildPlateProgramsAsync(ProgramStatus? statusFilter = null);

    /// <summary>
    /// Returns ProgramPart entries for a program, with Part navigation loaded.
    /// </summary>
    Task<List<ProgramPart>> GetProgramPartsAsync(int programId);

    /// <summary>
    /// Adds a part to a program with quantity and optional stack level.
    /// </summary>
    Task<ProgramPart> AddProgramPartAsync(int programId, int partId, int quantity, int stackLevel = 1, int? workOrderLineId = null);

    /// <summary>
    /// Updates quantity, stack level, or position notes on an existing program part entry.
    /// </summary>
    Task<ProgramPart> UpdateProgramPartAsync(int programPartId, int quantity, int stackLevel, string? positionNotes = null);

    /// <summary>
    /// Removes a part entry from a program.
    /// </summary>
    Task RemoveProgramPartAsync(int programPartId);

    /// <summary>
    /// Saves slicer metadata (layer count, build height, estimated print hours, powder, slicer info)
    /// on a BuildPlate program after the slicer import.
    /// </summary>
    Task UpdateSlicerDataAsync(int programId, double? estimatedPrintHours, int? layerCount = null,
        double? buildHeightMm = null, double? estimatedPowderKg = null,
        string? slicerFileName = null, string? slicerSoftware = null, string? slicerVersion = null,
        string? partPositionsJson = null);

    /// <summary>
    /// Links post-processing programs (depowder and/or EDM) to a BuildPlate program.
    /// Pass null for either FK to leave it unchanged.
    /// </summary>
    Task UpdatePostProcessingLinksAsync(int buildPlateProgramId, int? depowderProgramId, int? edmProgramId, string modifiedBy);

    /// <summary>
    /// Returns candidate programs that can serve as post-processing steps for a build plate.
    /// Filters to Depowder or EDM machine-type programs (or generic programs).
    /// </summary>
    Task<List<MachineProgram>> GetPostProcessingCandidatesAsync(string machineType);

    // ── Duration & Program Selection ───────────────────────────

    /// <summary>
    /// Calculates the estimated duration in minutes for a program execution.
    /// For BuildPlate programs: uses EstimatedPrintHours * 60.
    /// For Standard programs: uses SetupTimeMinutes + (RunTimeMinutes * quantity) + CycleTimeMinutes.
    /// Returns null if the program has no duration data configured.
    /// </summary>
    Task<ProgramDurationResult?> GetDurationFromProgramAsync(int programId, int quantity = 1);

    /// <summary>
    /// Finds the best matching active program for a part/machine/stage combination.
    /// Selection criteria: PartId match → MachineId match (via assignments) → Status Active
    /// Prefers programs with learned EMA data (ActualAverageDurationMinutes) and higher sample counts.
    /// Returns null if no matching program is found.
    /// </summary>
    Task<MachineProgram?> GetBestProgramForStageAsync(int partId, int? machineId = null, int? productionStageId = null);
}

/// <summary>
/// Result of duration calculation from a program.
/// </summary>
public record ProgramDurationResult(
    double TotalMinutes,
    double SetupMinutes,
    double RunMinutes,
    double CycleMinutes,
    string Source,
    bool IsLearned);

/// <summary>
/// Alert returned by tooling readiness checks, indicating a component linked to
/// a program's tooling requires maintenance or is approaching wear limits.
/// </summary>
public class ToolingReadinessAlert
{
    public int ToolingItemId { get; set; }
    public string ToolPosition { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public int MachineComponentId { get; set; }
    public double WearPercent { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsBlocking { get; set; }
    public string Message { get; set; } = string.Empty;
}
