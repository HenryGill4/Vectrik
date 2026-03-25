using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

public interface IBuildAdvisorService
{
    /// <summary>
    /// "What should I print next on this machine?"
    /// Analyzes all demand, picks the best plate composition, and returns a recommendation.
    /// </summary>
    Task<BuildRecommendation> RecommendNextBuildAsync(int machineId, DateTime? startAfter = null);

    /// <summary>
    /// Aggregates all outstanding demand across all active work orders, grouped by part.
    /// </summary>
    Task<List<DemandSummary>> GetAggregateDemandAsync();

    /// <summary>
    /// Optimizes plate composition for a given slot: primary part + fill parts.
    /// When <paramref name="forcePrimaryPartId"/> is set, that part is used as the primary
    /// regardless of urgency ranking (for WO-initiated scheduling).
    /// </summary>
    Task<PlateComposition> OptimizePlateAsync(
        int machineId, DateTime slotStart, List<DemandSummary> demand,
        int maxPartTypes = 4, int? forcePrimaryPartId = null);

    /// <summary>
    /// Detects downstream capacity bottlenecks across a planning horizon.
    /// </summary>
    Task<BottleneckReport> AnalyzeBottlenecksAsync(DateTime horizonStart, DateTime horizonEnd);
}

// ══════════════════════════════════════════════════════════
// Result Records
// ══════════════════════════════════════════════════════════

/// <summary>
/// Complete recommendation for the next build on a machine.
/// </summary>
public record BuildRecommendation(
    int MachineId,
    string MachineName,
    ProgramScheduleSlot Slot,
    PlateComposition Plate,
    string Rationale,
    List<string> Warnings);

/// <summary>
/// Optimized plate composition: what parts go on this plate and at what stack levels.
/// </summary>
public record PlateComposition(
    List<PlatePartAllocation> Parts,
    int RecommendedStackLevel,
    double EstimatedPrintHours,
    bool ChangeoverAligned,
    DateTime ChangeoverTime,
    bool OperatorAvailable);

/// <summary>
/// A single part allocation on a build plate.
/// </summary>
public record PlatePartAllocation(
    int PartId,
    string PartNumber,
    int Positions,
    int StackLevel,
    int TotalParts,
    int DemandRemaining,
    int Surplus,
    int? WorkOrderLineId,
    DateTime? WoDueDate);

/// <summary>
/// Aggregated demand for a single part type across all work orders.
/// </summary>
public record DemandSummary(
    int PartId,
    string PartNumber,
    int TotalOrdered,
    int TotalProduced,
    int InPrograms,
    int InProduction,
    int NetRemaining,
    DateTime EarliestDueDate,
    JobPriority HighestPriority,
    bool IsOverdue,
    bool IsAdditive,
    PartAdditiveBuildConfig? BuildConfig,
    List<DemandSourceLine> SourceLines);

/// <summary>
/// Links a demand summary back to specific WO lines.
/// </summary>
public record DemandSourceLine(
    int WorkOrderLineId,
    string WorkOrderNumber,
    string CustomerName,
    int Quantity,
    int Produced,
    DateTime DueDate);

/// <summary>
/// Bottleneck analysis across departments.
/// </summary>
public record BottleneckReport(
    List<BottleneckItem> Items,
    Dictionary<string, double> DepartmentUtilization,
    List<string> Recommendations);

/// <summary>
/// A single bottleneck or capacity concern.
/// </summary>
public record BottleneckItem(
    string Department,
    string? MachineName,
    double UtilizationPct,
    double QueueHours,
    double CapacityHours,
    string Severity);
