using Vectrik.Models;

namespace Vectrik.Services;

public interface ISchedulingWeightsService
{
    /// <summary>Returns the current weights (creates a default row if none exists).</summary>
    Task<SchedulingWeights> GetWeightsAsync();

    /// <summary>Saves updated weights.</summary>
    Task UpdateWeightsAsync(SchedulingWeights weights);

    /// <summary>Resets all weights to factory defaults.</summary>
    Task<SchedulingWeights> ResetToDefaultsAsync();
}
