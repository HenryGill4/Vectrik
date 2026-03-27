using Vectrik.Services;

namespace Vectrik.Components.Pages.Scheduler.Models;

/// <summary>
/// Editable plate allocation used by the PlateComposer and NextBuildAdvisor.
/// Tracks a single part type's placement on a build plate during composition.
/// </summary>
public class EditablePlateAllocation
{
    public int PartId { get; set; }
    public string PartNumber { get; set; } = "";
    public int Positions { get; set; }
    public int StackLevel { get; set; } = 1;
    public int DemandRemaining { get; set; }
    public int? WorkOrderLineId { get; set; }
    public DateTime? WoDueDate { get; set; }
    public List<DemandSourceLine> SourceLines { get; set; } = [];

    public int TotalParts => Positions * StackLevel;
    public int Surplus => Math.Max(0, TotalParts - DemandRemaining);
    public string WoNumbers => SourceLines.Any()
        ? string.Join(", ", SourceLines.Select(s => s.WorkOrderNumber).Distinct())
        : "";
}
