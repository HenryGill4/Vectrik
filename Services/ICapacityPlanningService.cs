using Vectrik.Models;
using Vectrik.Models.Enums;

namespace Vectrik.Services;

public interface ICapacityPlanningService
{
    Task<List<MachineCapacityCard>> GetMachineCapacityCardsAsync(DateTime from, DateTime to);
    Task<List<DemandGapItem>> GetDemandGapsAsync();
    Task<List<MachineAssignmentSuggestion>> SuggestAssignmentsAsync(List<int> partIds);
    Task<List<StageExecution>> ExecuteAssignmentsAsync(List<MachineAssignmentSuggestion> assignments, int userId);
}

public class MachineCapacityCard
{
    public int MachineId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string MachineType { get; set; } = string.Empty;
    public MachineStatus Status { get; set; }
    public string? CurrentProgramName { get; set; }
    public double UtilizationPct { get; set; }
    public double LoadedHours { get; set; }
    public double AvailableHours { get; set; }
    public int QueueCount { get; set; }
    public string? NextDuePart { get; set; }
    public DateTime? NextDueDate { get; set; }
    public bool IsAdditive { get; set; }
}

public class DemandGapItem
{
    public int PartId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public int DemandQty { get; set; }
    public int ScheduledQty { get; set; }
    public int GapQty { get; set; }
    public int CapableMachineCount { get; set; }
    public DateTime EarliestDueDate { get; set; }
    public bool IsOverdue { get; set; }
    public JobPriority HighestPriority { get; set; }
}

public class MachineAssignmentSuggestion
{
    public int PartId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public int MachineId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public int MachineProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public double EstimatedSetupMinutes { get; set; }
    public double EstimatedRunMinutes { get; set; }
    public int Quantity { get; set; }
    public int Score { get; set; }
    public string ScoreReason { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
}
