using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class MachineService : IMachineService
{
    private readonly TenantDbContext _db;

    public MachineService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<Machine>> GetAllMachinesAsync(bool activeOnly = false)
    {
        var query = _db.Machines.AsQueryable();
        if (activeOnly) query = query.Where(m => m.IsActive);
        return await query.OrderBy(m => m.MachineId).ToListAsync();
    }

    public async Task<Machine?> GetByIdAsync(int id) =>
        await _db.Machines.FindAsync(id);

    public async Task<Machine?> GetByMachineIdAsync(string machineId) =>
        await _db.Machines.FirstOrDefaultAsync(m => m.MachineId == machineId);

    public async Task<Machine> CreateMachineAsync(Machine machine)
    {
        machine.CreatedDate = DateTime.UtcNow;
        machine.LastModifiedDate = DateTime.UtcNow;
        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();
        return machine;
    }

    public async Task UpdateMachineAsync(Machine machine)
    {
        machine.LastModifiedDate = DateTime.UtcNow;
        _db.Machines.Update(machine);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteMachineAsync(int id)
    {
        var machine = await _db.Machines.FindAsync(id);
        if (machine != null)
        {
            _db.Machines.Remove(machine);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> MachineIdExistsAsync(string machineId, int? excludeId = null)
    {
        var query = _db.Machines.Where(m => m.MachineId == machineId);
        if (excludeId.HasValue) query = query.Where(m => m.Id != excludeId.Value);
        return await query.AnyAsync();
    }
}
