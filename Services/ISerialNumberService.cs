using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface ISerialNumberService
{
    Task<string> GenerateSerialNumberAsync();
    Task<List<PartInstance>> AssignSerialNumbersAsync(int workOrderLineId, int partId, int quantity, string createdBy, int? buildPackageId = null);
    Task<PartInstance?> GetBySerialNumberAsync(string serialNumber);
    Task<List<PartInstance>> GetByWorkOrderLineAsync(int workOrderLineId);
    Task<PartInstance> UpdateStatusAsync(int partInstanceId, Models.Enums.PartInstanceStatus status);
    Task<PartInstance> MoveToStageAsync(int partInstanceId, int stageId);
}
