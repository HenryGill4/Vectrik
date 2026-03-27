using Vectrik.Models;

namespace Vectrik.Services;

public interface IShiftManagementService
{
    // Shift CRUD
    Task<List<OperatingShift>> GetAllShiftsAsync();
    Task<OperatingShift?> GetShiftAsync(int id);
    Task<OperatingShift> CreateShiftAsync(OperatingShift shift);
    Task UpdateShiftAsync(OperatingShift shift);
    Task DeleteShiftAsync(int id);

    // Machine-Shift assignments
    Task<List<OperatingShift>> GetEffectiveShiftsForMachineAsync(int machineId);
    Task SetMachineShiftsAsync(int machineId, List<int> shiftIds);

    // Operator-Shift assignments
    Task SetUserShiftsAsync(int userId, List<int> shiftIds, string? assignedBy = null);
    Task<List<UserShiftAssignment>> GetUserShiftsAsync(int userId);

    // Scheduling queries
    Task<Dictionary<int, List<OperatingShift>>> GetMachineShiftMapAsync(IEnumerable<int> machineIds);
}
