using Vectrik.Models;

namespace Vectrik.Services;

/// <summary>
/// Captures scheduling diagnostic snapshots for bug reporting.
/// Used by the Issue Tracker to auto-populate rich context for scheduling issues.
/// </summary>
public interface ISchedulingDiagnosticsService
{
    /// <summary>Returns all active machines available for scheduling.</summary>
    Task<List<Machine>> GetSchedulableMachinesAsync();

    /// <summary>
    /// Captures a full scheduling diagnostic snapshot for a specific machine.
    /// Includes executions, build packages, timeline entries, and detected conflicts.
    /// </summary>
    Task<SchedulingSnapshot> CaptureSnapshotAsync(int machineId, DateTime? rangeStart = null, DateTime? rangeEnd = null);
}

/// <summary>Immutable scheduling diagnostic snapshot serialized to JSON for issue reporting.</summary>
public record SchedulingSnapshot(
    SchedulingSnapshotMachine Machine,
    DateTime CapturedAt,
    DateTime RangeStart,
    DateTime RangeEnd,
    List<SchedulingSnapshotExecution> Executions,
    List<SchedulingSnapshotBuild> BuildPackages,
    List<SchedulingSnapshotTimeline> Timeline,
    List<SchedulingSnapshotConflict> Conflicts,
    int UnscheduledCount);

public record SchedulingSnapshotMachine(
    int Id,
    string Name,
    string MachineType,
    string Status,
    bool IsAdditive,
    bool AutoChangeoverEnabled,
    double ChangeoverMinutes,
    decimal HourlyRate);

public record SchedulingSnapshotExecution(
    int Id,
    int? JobId,
    string? PartNumber,
    string? PartName,
    string StageName,
    string? ProcessStageName,
    string Status,
    DateTime? ScheduledStart,
    DateTime? ScheduledEnd,
    double? EstimatedHours,
    int? MachineProgramId,
    string? BatchGroupId,
    string? WorkOrderNumber,
    bool IsUnmanned);

public record SchedulingSnapshotBuild(
    int Id,
    string Name,
    string Status,
    DateTime? ScheduledDate,
    double? EstimatedDurationHours,
    string? Material,
    int PartCount,
    bool IsLocked);

public record SchedulingSnapshotTimeline(
    int MachineProgramId,
    string BuildName,
    DateTime PrintStart,
    DateTime PrintEnd,
    DateTime? ChangeoverStart,
    DateTime? ChangeoverEnd,
    string Status);

public record SchedulingSnapshotConflict(
    int ExecutionIdA,
    int ExecutionIdB,
    string DescriptionA,
    string DescriptionB,
    DateTime OverlapStart,
    DateTime OverlapEnd);
