using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class ManufacturingApproachService : IManufacturingApproachService
{
    private readonly TenantDbContext _db;

    public ManufacturingApproachService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<ManufacturingApproach>> GetAllAsync(bool activeOnly = true)
    {
        var query = _db.ManufacturingApproaches.AsQueryable();
        if (activeOnly)
            query = query.Where(a => a.IsActive);
        return await query.OrderBy(a => a.DisplayOrder).ToListAsync();
    }

    public async Task<ManufacturingApproach?> GetByIdAsync(int id)
    {
        return await _db.ManufacturingApproaches.FindAsync(id);
    }

    public async Task<ManufacturingApproach> CreateAsync(ManufacturingApproach approach)
    {
        _db.ManufacturingApproaches.Add(approach);
        await _db.SaveChangesAsync();
        return approach;
    }

    public async Task<ManufacturingApproach> UpdateAsync(ManufacturingApproach approach)
    {
        _db.ManufacturingApproaches.Update(approach);
        await _db.SaveChangesAsync();
        return approach;
    }

    public async Task DeleteAsync(int id)
    {
        var approach = await _db.ManufacturingApproaches.FindAsync(id);
        if (approach is null) throw new InvalidOperationException("Manufacturing approach not found.");
        approach.IsActive = false;
        await _db.SaveChangesAsync();
    }
}
