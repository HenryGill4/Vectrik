using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class ShiftManagementService : IShiftManagementService
{
    private readonly TenantDbContext _db;

    public ShiftManagementService(TenantDbContext db) => _db = db;

    // ── Shift CRUD ──

    public async Task<List<OperatingShift>> GetAllShiftsAsync()
    {
        var shifts = await _db.OperatingShifts
            .Include(s => s.MachineAssignments).ThenInclude(a => a.Machine)
            .Include(s => s.UserAssignments).ThenInclude(a => a.User)
            .ToListAsync();
        return shifts.OrderBy(s => s.StartTime).ToList();
    }

    public async Task<OperatingShift?> GetShiftAsync(int id) =>
        await _db.OperatingShifts
            .Include(s => s.MachineAssignments).ThenInclude(a => a.Machine)
            .Include(s => s.UserAssignments).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<OperatingShift> CreateShiftAsync(OperatingShift shift)
    {
        shift.CreatedDate = DateTime.UtcNow;
        _db.OperatingShifts.Add(shift);
        await _db.SaveChangesAsync();
        return shift;
    }

    public async Task UpdateShiftAsync(OperatingShift shift)
    {
        _db.OperatingShifts.Update(shift);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteShiftAsync(int id)
    {
        var shift = await _db.OperatingShifts.FindAsync(id);
        if (shift == null) return;
        _db.OperatingShifts.Remove(shift);
        await _db.SaveChangesAsync();
    }

    // ── Machine-Shift assignments ──

    public async Task<List<OperatingShift>> GetEffectiveShiftsForMachineAsync(int machineId)
    {
        var assigned = await _db.MachineShiftAssignments
            .Where(a => a.MachineId == machineId)
            .Include(a => a.OperatingShift)
            .Select(a => a.OperatingShift)
            .Where(s => s.IsActive)
            .ToListAsync();

        if (assigned.Any()) return assigned;

        // Fallback: all active shifts (backward compat)
        return await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
    }

    public async Task SetMachineShiftsAsync(int machineId, List<int> shiftIds)
    {
        var existing = await _db.MachineShiftAssignments
            .Where(a => a.MachineId == machineId)
            .ToListAsync();
        _db.MachineShiftAssignments.RemoveRange(existing);

        foreach (var shiftId in shiftIds)
        {
            _db.MachineShiftAssignments.Add(new MachineShiftAssignment
            {
                MachineId = machineId,
                OperatingShiftId = shiftId
            });
        }

        await _db.SaveChangesAsync();
    }

    // ── Operator-Shift assignments ──

    public async Task<List<UserShiftAssignment>> GetUserShiftsAsync(int userId) =>
        await _db.UserShiftAssignments
            .Include(a => a.OperatingShift)
            .Where(a => a.UserId == userId)
            .ToListAsync();

    public async Task SetUserShiftsAsync(int userId, List<int> shiftIds, string? assignedBy = null)
    {
        var existing = await _db.UserShiftAssignments
            .Where(a => a.UserId == userId)
            .ToListAsync();
        _db.UserShiftAssignments.RemoveRange(existing);

        var isPrimary = true;
        foreach (var shiftId in shiftIds)
        {
            _db.UserShiftAssignments.Add(new UserShiftAssignment
            {
                UserId = userId,
                OperatingShiftId = shiftId,
                IsPrimary = isPrimary,
                EffectiveFrom = DateTime.UtcNow,
                AssignedBy = assignedBy
            });
            isPrimary = false; // first is primary
        }

        await _db.SaveChangesAsync();
    }

    // ── Scheduling queries ──

    public async Task<Dictionary<int, List<OperatingShift>>> GetMachineShiftMapAsync(IEnumerable<int> machineIds)
    {
        var ids = machineIds.ToList();
        var assignments = await _db.MachineShiftAssignments
            .Where(a => ids.Contains(a.MachineId))
            .Include(a => a.OperatingShift)
            .Where(a => a.OperatingShift.IsActive)
            .ToListAsync();

        var allShifts = await _db.OperatingShifts.Where(s => s.IsActive).ToListAsync();
        var map = new Dictionary<int, List<OperatingShift>>();

        foreach (var machineId in ids)
        {
            var machineShifts = assignments
                .Where(a => a.MachineId == machineId)
                .Select(a => a.OperatingShift)
                .ToList();

            map[machineId] = machineShifts.Any() ? machineShifts : allShifts;
        }

        return map;
    }
}
