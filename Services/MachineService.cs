using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

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
        if (machine == null) throw new InvalidOperationException("Machine not found.");

        var hasActiveWork = await _db.StageExecutions
            .AnyAsync(e => e.MachineId == id
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed);

        if (hasActiveWork)
            throw new InvalidOperationException(
                $"Cannot delete '{machine.Name}' — it has active or scheduled work. " +
                "Reassign or complete the work first, or deactivate the machine instead.");

        _db.Machines.Remove(machine);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> MachineIdExistsAsync(string machineId, int? excludeId = null)
    {
        var query = _db.Machines.Where(m => m.MachineId == machineId);
        if (excludeId.HasValue) query = query.Where(m => m.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<Machine?> GetMachineWithComponentsAsync(int id)
    {
        return await _db.Machines
            .Include(m => m.Components.Where(c => c.IsActive))
                .ThenInclude(c => c.MaintenanceRules.Where(r => r.IsActive))
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<List<Machine>> GetFilteredMachinesAsync(
        string? machineType, MachineStatus? status, string? department, string? search)
    {
        var query = _db.Machines.AsQueryable();

        if (!string.IsNullOrWhiteSpace(machineType))
            query = query.Where(m => m.MachineType == machineType);

        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(m => m.Department == department);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(m =>
                m.Name.ToLower().Contains(term) ||
                m.MachineId.ToLower().Contains(term) ||
                (m.MachineModel != null && m.MachineModel.ToLower().Contains(term)) ||
                (m.Location != null && m.Location.ToLower().Contains(term)));
        }

        return await query.OrderBy(m => m.MachineId).ToListAsync();
    }

    public async Task<List<string>> GetDistinctDepartmentsAsync()
    {
        return await _db.Machines
            .Where(m => m.Department != null && m.Department != "")
            .Select(m => m.Department!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();
    }

    public async Task<List<string>> GetDistinctMachineTypesAsync()
    {
        return await _db.Machines
            .Select(m => m.MachineType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
    }

    public async Task<List<StageExecution>> GetMachineScheduleAsync(int machineId, int maxResults = 10)
    {
        return await _db.StageExecutions
            .Include(e => e.Job)
            .Include(e => e.ProductionStage)
            .Where(e => e.MachineId == machineId
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed)
            .OrderBy(e => e.ScheduledStartAt ?? DateTime.MaxValue)
            .Take(maxResults)
            .ToListAsync();
    }
}
