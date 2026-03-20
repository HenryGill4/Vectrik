using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IManufacturingApproachService
{
    Task<List<ManufacturingApproach>> GetAllAsync(bool activeOnly = true);
    Task<ManufacturingApproach?> GetByIdAsync(int id);
    Task<ManufacturingApproach> CreateAsync(ManufacturingApproach approach);
    Task<ManufacturingApproach> UpdateAsync(ManufacturingApproach approach);
    Task DeleteAsync(int id);
}
