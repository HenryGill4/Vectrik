using Opcentrix_V3.Models;

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
}
