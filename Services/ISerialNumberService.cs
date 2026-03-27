using Vectrik.Models;

namespace Vectrik.Services;

public interface ISerialNumberService
{
    Task<string> GenerateSerialNumberAsync();
    Task<List<PartInstance>> AssignSerialNumbersAsync(int workOrderLineId, int partId, int quantity, string createdBy);
    Task<PartInstance?> GetBySerialNumberAsync(string serialNumber);
    Task<List<PartInstance>> GetByWorkOrderLineAsync(int workOrderLineId);
    Task<PartInstance> UpdateStatusAsync(int partInstanceId, Models.Enums.PartInstanceStatus status);
    Task<PartInstance> MoveToStageAsync(int partInstanceId, int stageId);

    /// <summary>
    /// Generate a temporary tracking ID for a PartInstance (used before official serial).
    /// Format: "TMP-{programId}-{index:D4}".
    /// </summary>
    Task<string> GenerateTemporaryTrackingIdAsync(int programId, int index);

    /// <summary>
    /// Assign the official serial number to a PartInstance (called at laser engraving stage).
    /// Uses the tenant's SystemSettings prefix for formatting.
    /// </summary>
    Task<PartInstance> AssignOfficialSerialAsync(int partInstanceId);

    /// <summary>
    /// Generate a range of serial numbers for bulk assignment.
    /// </summary>
    Task<List<string>> GenerateSerialRangeAsync(int count);
}
