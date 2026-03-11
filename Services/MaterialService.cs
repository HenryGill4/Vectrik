using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public class MaterialService : IMaterialService
{
    private readonly TenantDbContext _db;

    public MaterialService(TenantDbContext db)
    {
        _db = db;
    }

    public async Task<List<Material>> GetAllMaterialsAsync(bool activeOnly = true)
    {
        var query = _db.Materials.AsQueryable();
        if (activeOnly)
            query = query.Where(m => m.IsActive);
        return await query.OrderBy(m => m.Name).ToListAsync();
    }

    public async Task<Material?> GetMaterialByIdAsync(int id)
    {
        return await _db.Materials.FindAsync(id);
    }

    public async Task<Material> CreateMaterialAsync(Material material)
    {
        material.CreatedDate = DateTime.UtcNow;
        material.LastModifiedDate = DateTime.UtcNow;
        _db.Materials.Add(material);
        await _db.SaveChangesAsync();
        return material;
    }

    public async Task<Material> UpdateMaterialAsync(Material material)
    {
        material.LastModifiedDate = DateTime.UtcNow;
        _db.Materials.Update(material);
        await _db.SaveChangesAsync();
        return material;
    }

    public async Task DeleteMaterialAsync(int id)
    {
        var material = await _db.Materials.FindAsync(id);
        if (material == null) throw new InvalidOperationException("Material not found.");
        material.IsActive = false;
        material.LastModifiedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<List<Material>> GetCompatibleMaterialsAsync(int materialId)
    {
        var material = await _db.Materials.FindAsync(materialId);
        if (material == null || string.IsNullOrWhiteSpace(material.CompatibleMaterials))
            return new List<Material>();

        var compatibleIds = material.CompatibleMaterials
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();

        return await _db.Materials
            .Where(m => compatibleIds.Contains(m.Id) && m.IsActive)
            .ToListAsync();
    }
}
