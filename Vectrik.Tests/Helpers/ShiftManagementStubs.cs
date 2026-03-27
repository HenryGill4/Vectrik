using Vectrik.Models;
using Vectrik.Services;

namespace Vectrik.Tests.Helpers;

public class StubShiftManagementService : IShiftManagementService
{
    public Task<List<OperatingShift>> GetAllShiftsAsync() => Task.FromResult(new List<OperatingShift>());
    public Task<OperatingShift?> GetShiftAsync(int id) => Task.FromResult<OperatingShift?>(null);
    public Task<OperatingShift> CreateShiftAsync(OperatingShift shift) => Task.FromResult(shift);
    public Task UpdateShiftAsync(OperatingShift shift) => Task.CompletedTask;
    public Task DeleteShiftAsync(int id) => Task.CompletedTask;
    public Task<List<OperatingShift>> GetEffectiveShiftsForMachineAsync(int machineId) =>
        Task.FromResult(new List<OperatingShift>());
    public Task SetMachineShiftsAsync(int machineId, List<int> shiftIds) => Task.CompletedTask;
    public Task SetUserShiftsAsync(int userId, List<int> shiftIds, string? assignedBy = null) => Task.CompletedTask;
    public Task<List<UserShiftAssignment>> GetUserShiftsAsync(int userId) =>
        Task.FromResult(new List<UserShiftAssignment>());
    public Task<Dictionary<int, List<OperatingShift>>> GetMachineShiftMapAsync(IEnumerable<int> machineIds) =>
        Task.FromResult(machineIds.ToDictionary(id => id, _ => new List<OperatingShift>()));
}
