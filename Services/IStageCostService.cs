using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IStageCostService
{
    /// <summary>Gets all cost profiles with their associated ProductionStage.</summary>
    Task<List<StageCostProfile>> GetAllAsync();

    /// <summary>Gets the cost profile for a specific production stage, or null if none exists.</summary>
    Task<StageCostProfile?> GetByStageIdAsync(int productionStageId);

    /// <summary>Creates or updates the cost profile for a production stage.</summary>
    Task<StageCostProfile> SaveAsync(StageCostProfile profile);

    /// <summary>Deletes the cost profile for a production stage.</summary>
    Task DeleteAsync(int profileId);

    /// <summary>
    /// Calculates the true operation cost for a stage given duration and part count.
    /// Falls back to ProductionStage.DefaultHourlyRate if no cost profile exists.
    /// </summary>
    Task<StageCostEstimate> EstimateCostAsync(int productionStageId, double durationHours, int partCount, int batchCount = 1);
}

/// <summary>Result of a cost estimation for a single stage run.</summary>
public record StageCostEstimate(
    decimal LaborCost,
    decimal EquipmentCost,
    decimal OverheadCost,
    decimal PerPartCost,
    decimal ToolingCost,
    decimal ExternalCost,
    decimal TotalCost,
    decimal CostPerPart,
    decimal FullyLoadedHourlyRate,
    bool UsedCostProfile);
