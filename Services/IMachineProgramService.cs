using Opcentrix_V3.Models;

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
    /// </summary>
    Task<List<MachineProgram>> GetActiveProgramsAsync(int? partId = null, int? machineId = null, int? processStageId = null);

    Task<MachineProgram> CreateAsync(MachineProgram program, string createdBy);
    Task UpdateAsync(MachineProgram program, string modifiedBy);
    Task DeleteAsync(int id);

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
}
