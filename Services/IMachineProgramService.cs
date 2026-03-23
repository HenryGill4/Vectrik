using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

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
}

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
