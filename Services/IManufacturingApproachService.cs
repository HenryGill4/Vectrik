using Vectrik.Models;

namespace Vectrik.Services;

public interface IManufacturingApproachService
{
    Task<List<ManufacturingApproach>> GetAllAsync(bool activeOnly = true);
    Task<ManufacturingApproach?> GetByIdAsync(int id);
    Task<ManufacturingApproach> CreateAsync(ManufacturingApproach approach);
    Task<ManufacturingApproach> UpdateAsync(ManufacturingApproach approach);
    Task DeleteAsync(int id);

    /// <summary>
    /// Propagates routing template changes to all active processes using this approach.
    /// Adds new stages, removes orphaned stages, and syncs execution order, processing level,
    /// machine assignments, batch overrides, and duration flags from the template.
    /// Returns the total number of process-level changes made.
    /// </summary>
    Task<int> PropagateRoutingChangesAsync(int approachId);
}
