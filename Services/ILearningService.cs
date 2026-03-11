namespace Opcentrix_V3.Services;

public interface ILearningService
{
    Task UpdateEstimateAsync(int partId, int productionStageId, double actualDurationHours);
}
