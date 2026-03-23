using Opcentrix_V3.Services;

namespace Opcentrix_V3.Tests.Helpers;

/// <summary>
/// No-op stub for ILearningService used by StageService tests.
/// </summary>
internal sealed class StubLearningService : ILearningService
{
    public Task UpdateEstimateAsync(int partId, int productionStageId, double actualDurationHours)
        => Task.CompletedTask;
    public Task UpdateProcessStageEstimateAsync(int processStageId, double actualDurationMinutes)
        => Task.CompletedTask;
    public Task UpdateMachineProgramEstimateAsync(int machineProgramId, double actualDurationMinutes)
        => Task.CompletedTask;
}
