using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public interface IMachineService
{
    Task<List<Machine>> GetAllMachinesAsync(bool activeOnly = false);
    Task<Machine?> GetByIdAsync(int id);
    Task<Machine?> GetByMachineIdAsync(string machineId);
    Task<Machine> CreateMachineAsync(Machine machine);
    Task UpdateMachineAsync(Machine machine);
    Task DeleteMachineAsync(int id);
    Task<bool> MachineIdExistsAsync(string machineId, int? excludeId = null);

    /// <summary>Loads a machine with its components and their maintenance rules.</summary>
    Task<Machine?> GetMachineWithComponentsAsync(int id);

    /// <summary>Filters machines by type, status, department, and search text.</summary>
    Task<List<Machine>> GetFilteredMachinesAsync(string? machineType, MachineStatus? status, string? department, string? search);

    /// <summary>Returns distinct departments across all machines.</summary>
    Task<List<string>> GetDistinctDepartmentsAsync();

    /// <summary>Returns distinct machine types across all machines.</summary>
    Task<List<string>> GetDistinctMachineTypesAsync();

    /// <summary>Returns upcoming/active stage executions assigned to a machine.</summary>
    Task<List<StageExecution>> GetMachineScheduleAsync(int machineId, int maxResults = 10);
}
