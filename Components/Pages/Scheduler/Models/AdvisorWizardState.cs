using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services;

namespace Vectrik.Components.Pages.Scheduler.Models;

/// <summary>
/// Shared state object cascaded to NextBuildAdvisor sub-components.
/// Holds display-relevant data; actions are surfaced as EventCallbacks on each sub-component.
/// </summary>
public class AdvisorWizardState
{
    // ── Machine context ──────────────────────────────────────────
    public List<Machine> SlsMachines { get; set; } = [];
    public int SelectedMachineId { get; set; }
    public Machine? SelectedMachine { get; set; }

    // ── Demand & recommendation ──────────────────────────────────
    public List<DemandSummary> Demand { get; set; } = [];
    public BuildRecommendation? Recommendation { get; set; }
    public ProgramTimelineEntry? CurrentBuild { get; set; }

    // ── Program selection ────────────────────────────────────────
    public List<MachineProgram> AvailablePrograms { get; set; } = [];
    public MachineProgram? SelectedExistingProgram { get; set; }
    public bool CreatingNewProgram { get; set; }
    public bool ExistingNeedsSlicerData { get; set; }

    // ── Plate composition ────────────────────────────────────────
    public List<EditablePlateAllocation> PlateAllocations { get; set; } = [];
    public int MaxPlateCapacity { get; set; } = 450; // 450mm × 450mm EOS M4
    public bool PlateEdited { get; set; }
    public List<string> ActiveWarnings { get; set; } = [];

    // ── Timing / schedule ────────────────────────────────────────
    public List<ScheduleOption> ScheduleOptions { get; set; } = [];
    public ScheduleOption? SelectedOption { get; set; }
    public ProgramScheduleSlot? ActiveSlot => SelectedOption?.Slot ?? Recommendation?.Slot;
    public bool ActiveChangeoverAligned => SelectedOption?.ChangeoverAligned ?? Recommendation?.Plate.OperatorAvailable ?? true;

    // ── Computed helpers ─────────────────────────────────────────

    /// <summary>
    /// How many total parts (accounting for stack levels) are currently on the plate.
    /// </summary>
    public int PlatePartCount => PlateAllocations.Sum(a => a.TotalParts);

    /// <summary>
    /// Rough plate utilization: ratio of planned parts to a heuristic max capacity
    /// (returns 0–1, clamped).
    /// </summary>
    public double PlateUtilization
    {
        get
        {
            if (!PlateAllocations.Any()) return 0;
            // Use the max positions across all allocations as a proxy
            var totalPositions = PlateAllocations.Sum(a => a.Positions);
            var maxPositions = PlateAllocations.Max(a =>
            {
                var demandConfig = Demand.FirstOrDefault(d => d.PartId == a.PartId)?.BuildConfig;
                return demandConfig?.GetPositionsPerBuild(a.StackLevel) ?? totalPositions;
            });
            return maxPositions > 0 ? Math.Min(1.0, (double)totalPositions / maxPositions) : 0;
        }
    }

    /// <summary>
    /// Estimated build completion (print end) for the active slot.
    /// </summary>
    public DateTime? EstimatedCompletion => ActiveSlot?.PrintEnd.ToLocalTime();
}
