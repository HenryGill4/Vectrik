using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IMaterialService
{
    Task<List<Material>> GetAllMaterialsAsync(bool activeOnly = true);
    Task<Material?> GetMaterialByIdAsync(int id);
    Task<Material> CreateMaterialAsync(Material material);
    Task<Material> UpdateMaterialAsync(Material material);
    Task DeleteMaterialAsync(int id);
    Task<List<Material>> GetCompatibleMaterialsAsync(int materialId);
}
