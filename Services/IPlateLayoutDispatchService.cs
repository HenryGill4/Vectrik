using Vectrik.Models;

namespace Vectrik.Services;

public interface IPlateLayoutDispatchService
{
    /// <summary>
    /// Detects unmet additive demand and creates PlateLayout dispatches targeted at SLS engineers.
    /// </summary>
    Task<List<SetupDispatch>> DetectAndCreatePlateLayoutDispatchesAsync();

    /// <summary>
    /// Auto-completes PlateLayout dispatches when a program reaches Ready status and covers demand.
    /// Returns true if any dispatch was completed.
    /// </summary>
    Task<bool> TryAutoCompleteForProgramAsync(int machineProgramId);

    /// <summary>
    /// Gets active PlateLayout dispatches, optionally filtered by target role.
    /// </summary>
    Task<List<SetupDispatch>> GetEngineerDispatchesAsync(int? roleId = null);
}
