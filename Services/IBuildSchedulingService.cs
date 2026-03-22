using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

/// <summary>
/// Build-centric scheduling for additive (SLS) machines.
/// BuildPackage is the schedulable entity — duration comes from the slicer, not computed.
/// </summary>
public interface IBuildSchedulingService
{
    /// <summary>
    /// Schedule a build on an SLS machine, respecting auto-changeover
    /// and operator availability during the changeover window.
    /// </summary>
    Task<BuildScheduleResult> ScheduleBuildAsync(int buildPackageId, int machineId, DateTime? startAfter = null);

    /// <summary>
    /// Schedule an additional print run of the SAME build package on a machine.
    /// Reuses the existing build file — does not create a copy.
    /// Creates a new build-level Job + stage executions + per-part jobs for this run.
    /// </summary>
    Task<BuildScheduleResult> ScheduleBuildRunAsync(int buildPackageId, int machineId, DateTime? startAfter = null);

    /// <summary>
    /// Find the earliest slot for a build on a specific machine,
    /// factoring in changeover time between consecutive builds.
    /// </summary>
    Task<BuildScheduleSlot> FindEarliestBuildSlotAsync(int machineId, double durationHours, DateTime notBefore);

    /// <summary>
    /// Get the full build timeline for a machine: scheduled, printing, changeover windows.
    /// </summary>
    Task<List<MachineTimelineEntry>> GetMachineTimelineAsync(int machineId, DateTime from, DateTime to);

    /// <summary>
    /// Check operator availability during changeover window.
    /// If unavailable, suggest alternative build config to sync with shift.
    /// </summary>
    Task<ChangeoverAnalysis> AnalyzeChangeoverAsync(int machineId, DateTime buildEndTime);

    /// <summary>
    /// Create build-level StageExecutions (print, depowder, heat-treat, EDM)
    /// for a scheduled build package. Respects shared-resource constraints.
    /// </summary>
    Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int buildPackageId, string createdBy);

    /// <summary>
    /// After all build-level stages complete (post-EDM), create PartInstances
    /// from the plate and schedule per-part stages.
    /// </summary>
    Task<PlateReleaseResult> ReleasePlateAsync(int buildPackageId, string releasedBy);

    /// <summary>
    /// Lock a build (Ready → Scheduled). Creates revision snapshot, sets IsLocked.
    /// </summary>
    Task LockBuildAsync(int buildPackageId, string lockedBy);

    /// <summary>
    /// Unlock a build back to Draft. Creates revision note with reason.
    /// </summary>
    Task UnlockBuildAsync(int buildPackageId, string unlockedBy, string reason);
}

public record BuildScheduleSlot(
    DateTime PrintStart,
    DateTime PrintEnd,
    DateTime ChangeoverStart,
    DateTime ChangeoverEnd,
    int MachineId,
    bool OperatorAvailableForChangeover);

public record BuildScheduleResult(
    BuildScheduleSlot Slot,
    ChangeoverAnalysis? ChangeoverInfo,
    List<StageExecution> BuildStageExecutions);

public record ChangeoverAnalysis(
    bool OperatorAvailable,
    DateTime ChangeoverWindowStart,
    DateTime ChangeoverWindowEnd,
    string? SuggestedAction,
    double? SuggestedDurationHours);

public record MachineTimelineEntry(
    int BuildPackageId,
    string BuildName,
    DateTime PrintStart,
    DateTime PrintEnd,
    DateTime? ChangeoverStart,
    DateTime? ChangeoverEnd,
    BuildPackageStatus Status);

public record PlateReleaseResult(
    int BuildPackageId,
    List<PartInstance> CreatedInstances,
    List<Job> CreatedJobs,
    int TotalPartCount);
