using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface IPartTrackerService
{
    Task<PartTrackerResult> TrackByWorkOrderAsync(string orderNumber);
    Task<PartTrackerResult> TrackByPartNumberAsync(string partNumber);
    Task<PartInstanceTrack?> TrackBySerialNumberAsync(string serialNumber);
    Task<List<StagePipelineItem>> GetStagePipelineAsync();
}

public class PartTrackerResult
{
    public string SearchTerm { get; set; } = string.Empty;
    public string SearchType { get; set; } = string.Empty;
    public List<WorkOrderLineTrack> Lines { get; set; } = new();
}

public class WorkOrderLineTrack
{
    public int WorkOrderLineId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public int QuantityOrdered { get; set; }
    public int QuantityProduced { get; set; }
    public int QuantityShipped { get; set; }
    public string CurrentBatchStage { get; set; } = string.Empty;
    public int BatchQuantityAtStage { get; set; }
    public List<PartInstanceTrack> SerializedParts { get; set; } = new();
}

public class PartInstanceTrack
{
    public int PartInstanceId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string CurrentStageName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<StageLogEntry> History { get; set; } = new();
}

public class StageLogEntry
{
    public string StageName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public double? DurationHours { get; set; }
}

public class StagePipelineItem
{
    public int StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string StageColor { get; set; } = string.Empty;
    public int PartsInQueue { get; set; }
    public int PartsInProgress { get; set; }
    public int PartsCompleted { get; set; }
}
