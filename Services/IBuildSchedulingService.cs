using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

/// <summary>
/// Build-centric scheduling for additive (SLS) machines.
/// BuildPackage is the schedulable entity — duration comes from the slicer, not computed.
/// </summary>
/// <remarks>
/// Use IProgramSchedulingService for new scheduling work. This interface is being superseded
/// by program-based scheduling where MachineProgram.BuildPlate replaces BuildPackage.
/// </remarks>
[Obsolete("Use IProgramSchedulingService for program-based scheduling. BuildPackage is being replaced by MachineProgram.BuildPlate.")]
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
    Task<BuildScheduleSlot> FindEarliestBuildSlotAsync(int machineId, double durationHours, DateTime notBefore, int? forBuildPackageId = null);

    /// <summary>
    /// Find the best (earliest) slot across ALL available SLS machines.
    /// Evaluates every active additive machine and returns the one that can start soonest,
    /// including changeover time between consecutive builds.
    /// </summary>
    Task<BestBuildSlot> FindBestBuildSlotAsync(double durationHours, DateTime notBefore, int? forBuildPackageId = null);

    /// <summary>
    /// Schedule an additional print run, automatically choosing the SLS machine
    /// with the earliest available slot to maximize utilization across all machines.
    /// </summary>
    Task<BuildScheduleResult> ScheduleBuildRunAutoMachineAsync(int buildPackageId, DateTime? startAfter = null);

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
    /// Detect consecutive changeover conflicts on a machine.
    /// A conflict occurs when two builds finish back-to-back and the cooldown chamber
    /// from the preceding build cannot be emptied by an operator before the next
    /// changeover begins — the auto plate change system requires a fresh cooldown station.
    /// </summary>
    Task<List<ChangeoverConflict>> DetectChangeoverConflictsAsync(int machineId, DateTime from, DateTime to);

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

    /// <summary>
    /// Schedule a build plate program on an SLS machine.
    /// Uses the program's EstimatedPrintHours for duration, finds the earliest slot,
    /// and creates build-level StageExecutions linked to the MachineProgramId.
    /// Also creates downstream build stages (depowder, EDM, etc.) and per-part jobs.
    /// </summary>
    Task<BuildScheduleResult> ScheduleProgramBuildAsync(int machineProgramId, int machineId, DateTime? startAfter = null);

    /// <summary>
    /// Schedule a build plate program, automatically choosing the SLS machine
    /// with the earliest available slot to maximize utilization across all machines.
    /// </summary>
    Task<BuildScheduleResult> ScheduleProgramBuildAutoMachineAsync(int machineProgramId, DateTime? startAfter = null);

    /// <summary>
    /// Creates and schedules a job for a work order line using the part's manufacturing process.
    /// For additive parts: requires an existing BuildPlate program or creates a suggestion.
    /// For standard parts: creates a Job with stage executions linked to available programs.
    /// Returns the created job and its stage executions.
    /// </summary>
    Task<WorkOrderScheduleResult> ScheduleFromWorkOrderLineAsync(int workOrderLineId, int? preferredMachineId = null, DateTime? startAfter = null);

    /// <summary>
    /// Schedules a standard (non-BuildPlate) program. Creates a Job with stage executions
    /// using the program's duration data and linked to the MachineProgramId.
    /// </summary>
    Task<StandardProgramScheduleResult> ScheduleStandardProgramAsync(int machineProgramId, int quantity, int? machineId = null, int? workOrderLineId = null, DateTime? startAfter = null);
}

/// <summary>
/// Result of scheduling from a work order line.
/// </summary>
public record WorkOrderScheduleResult(
    int JobId,
    string JobNumber,
    List<StageExecution> StageExecutions,
    DateTime ScheduledStart,
    DateTime ScheduledEnd,
    List<string> Warnings);

/// <summary>
/// Result of scheduling a standard (non-BuildPlate) program.
/// </summary>
public record StandardProgramScheduleResult(
    int JobId,
    string JobNumber,
    int MachineProgramId,
    List<StageExecution> StageExecutions,
    DateTime ScheduledStart,
    DateTime ScheduledEnd,
    double TotalDurationHours,
    List<string> Warnings);

public record BuildScheduleSlot(
    DateTime PrintStart,
    DateTime PrintEnd,
    DateTime ChangeoverStart,
    DateTime ChangeoverEnd,
    int MachineId,
    bool OperatorAvailableForChangeover);

/// <summary>
/// Result of FindBestBuildSlotAsync — wraps a BuildScheduleSlot with the chosen machine info.
/// </summary>
public record BestBuildSlot(
    BuildScheduleSlot Slot,
    int MachineId,
    string MachineName);

public record BuildScheduleResult(
    BuildScheduleSlot Slot,
    ChangeoverAnalysis? ChangeoverInfo,
    List<StageExecution> BuildStageExecutions,
    ScheduleDiagnosticReport? Diagnostics = null);

public record ChangeoverAnalysis(
    bool OperatorAvailable,
    DateTime ChangeoverWindowStart,
    DateTime ChangeoverWindowEnd,
    string? SuggestedAction,
    double? SuggestedDurationHours);

/// <summary>
/// Warning raised when two consecutive builds finish without an operator
/// being available to empty the cooldown chamber between them.
/// The auto plate change system requires a fresh plate in the cooldown station;
/// if the previous plate hasn't been removed, the machine will be down.
/// </summary>
public record ChangeoverConflict(
    int MachineId,
    string MachineName,
    string PrecedingBuildName,
    DateTime PrecedingChangeoverStart,
    DateTime PrecedingChangeoverEnd,
    string FollowingBuildName,
    DateTime FollowingPrintStart,
    bool PrecedingOperatorAvailable,
    bool FollowingOperatorAvailable,
    string Warning);

public record MachineTimelineEntry(
    int BuildPackageId,
    string BuildName,
    DateTime PrintStart,
    DateTime PrintEnd,
    DateTime? ChangeoverStart,
    DateTime? ChangeoverEnd,
    BuildPackageStatus Status,
    int? StageExecutionId = null,
    bool HasChangeoverConflict = false);

public record PlateReleaseResult(
    int BuildPackageId,
    List<PartInstance> CreatedInstances,
    List<Job> CreatedJobs,
    int TotalPartCount);
