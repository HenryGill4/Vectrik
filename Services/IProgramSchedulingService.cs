using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

/// <summary>
/// Program-centric scheduling for all machine types.
/// MachineProgram is the schedulable entity — duration comes from EstimatedPrintHours (SLS)
/// or computed from RunTimeMinutes/CycleTimeMinutes (Standard programs).
/// Replaces IBuildSchedulingService for unified program-based manufacturing.
/// </summary>
public interface IProgramSchedulingService
{
    // ══════════════════════════════════════════════════════════
    // Build Plate (SLS) Scheduling
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Schedule a BuildPlate program on an SLS machine.
    /// Uses the program's EstimatedPrintHours for duration, finds the earliest slot,
    /// and creates build-level StageExecutions linked to the MachineProgramId.
    /// Also creates downstream build stages (depowder, EDM, etc.) and per-part jobs.
    /// </summary>
    Task<ProgramScheduleResult> ScheduleBuildPlateAsync(int machineProgramId, int machineId, DateTime? startAfter = null);

    /// <summary>
    /// Schedule a BuildPlate program, automatically choosing the SLS machine
    /// with the earliest available slot to maximize utilization across all machines.
    /// </summary>
    Task<ProgramScheduleResult> ScheduleBuildPlateAutoMachineAsync(int machineProgramId, DateTime? startAfter = null);

    /// <summary>
    /// Schedule an additional print run of an existing BuildPlate program.
    /// Creates a copy of the program with SourceProgramId set, then schedules it.
    /// Each run is tracked separately for WO fulfillment and part counting.
    /// </summary>
    Task<ProgramScheduleResult> ScheduleBuildPlateRunAsync(int machineProgramId, int machineId, DateTime? startAfter = null);

    /// <summary>
    /// Schedule an additional print run, automatically choosing the SLS machine
    /// with the earliest available slot.
    /// </summary>
    Task<ProgramScheduleResult> ScheduleBuildPlateRunAutoMachineAsync(int machineProgramId, DateTime? startAfter = null);

    // ══════════════════════════════════════════════════════════
    // Standard Program Scheduling (CNC, EDM, etc.)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Schedules a standard (non-BuildPlate) program. Creates a Job with stage executions
    /// using the program's duration data and linked to the MachineProgramId.
    /// </summary>
    Task<StandardProgramScheduleResult> ScheduleStandardProgramAsync(
        int machineProgramId,
        int quantity,
        int? machineId = null,
        int? workOrderLineId = null,
        DateTime? startAfter = null);

    // ══════════════════════════════════════════════════════════
    // Work Order Integration
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Get all BuildPlate programs that contain a specific part and are available for scheduling.
    /// Returns programs in Draft or Ready status that haven't been fully scheduled.
    /// </summary>
    Task<List<MachineProgram>> GetAvailableProgramsForPartAsync(int partId);

    /// <summary>
    /// Get all BuildPlate programs available for scheduling (None or Ready status).
    /// Includes ProgramParts with Part navigation for display purposes.
    /// </summary>
    Task<List<MachineProgram>> GetAvailableBuildPlateProgramsAsync();

    /// <summary>
    /// Creates and schedules a job for a work order line using the part's manufacturing process.
    /// For additive parts: requires an existing BuildPlate program or creates a suggestion.
    /// For standard parts: creates a Job with stage executions linked to available programs.
    /// Returns the created job and its stage executions.
    /// </summary>
    Task<WorkOrderScheduleResult> ScheduleFromWorkOrderLineAsync(
        int workOrderLineId,
        int? preferredMachineId = null,
        DateTime? startAfter = null);

    // ══════════════════════════════════════════════════════════
    // Cascade Rescheduling
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// After a build is inserted/moved on a machine, cascade-reschedule any overlapping
    /// downstream builds forward so they don't conflict. Returns count and names of shifted builds.
    /// </summary>
    Task<CascadeResult> CascadeRescheduleAsync(int machineId, int insertedProgramId);

    // ══════════════════════════════════════════════════════════
    // Slot Finding
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Find the earliest slot for a program on a specific machine,
    /// factoring in changeover time between consecutive builds.
    /// </summary>
    Task<ProgramScheduleSlot> FindEarliestSlotAsync(
        int machineId,
        double durationHours,
        DateTime notBefore,
        int? forProgramId = null);

    /// <summary>
    /// Find the best (earliest) slot across ALL available machines for a machine type.
    /// Evaluates every active machine of the type and returns the one that can start soonest.
    /// </summary>
    Task<BestProgramSlot> FindBestSlotAsync(
        double durationHours,
        DateTime notBefore,
        string? machineType = null,
        int? forProgramId = null);

    // ══════════════════════════════════════════════════════════
    // Timeline & Analysis
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Get the full program timeline for a machine: scheduled, printing, changeover windows.
    /// </summary>
    Task<List<ProgramTimelineEntry>> GetMachineTimelineAsync(int machineId, DateTime from, DateTime to);

    /// <summary>
    /// Check operator availability during changeover window.
    /// If unavailable, suggest alternative build config to sync with shift.
    /// </summary>
    Task<ChangeoverAnalysis> AnalyzeChangeoverAsync(int machineId, DateTime buildEndTime);

    /// <summary>
    /// Detect consecutive changeover conflicts on a machine.
    /// A conflict occurs when two builds finish back-to-back and the cooldown chamber
    /// from the preceding build cannot be emptied by an operator before the next
    /// changeover begins.
    /// </summary>
    Task<List<ProgramChangeoverConflict>> DetectChangeoverConflictsAsync(int machineId, DateTime from, DateTime to);

    // ══════════════════════════════════════════════════════════
    // Stage Execution Management
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Create build-level StageExecutions (print, depowder, heat-treat, EDM)
    /// for a scheduled BuildPlate program. Respects shared-resource constraints.
    /// </summary>
    Task<List<StageExecution>> CreateBuildStageExecutionsAsync(int machineProgramId, string createdBy);

    /// <summary>
    /// After all build-level stages complete (post-EDM), create PartInstances
    /// from the plate and schedule per-part stages.
    /// </summary>
    Task<ProgramPlateReleaseResult> ReleasePlateAsync(int machineProgramId, string releasedBy);

    // ══════════════════════════════════════════════════════════
    // Program Locking
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Generates multiple scheduling options for a build, considering changeover alignment,
    /// different stack levels, and weekend optimization. Returns options ranked by recommendation.
    /// </summary>
    Task<List<ScheduleOption>> GenerateScheduleOptionsAsync(
        int machineId, double baseDurationHours, DateTime notBefore,
        PartAdditiveBuildConfig? buildConfig = null, int demandQuantity = 0);

    /// <summary>
    /// Suggests an optimal build sequence for a machine, choosing stack levels that align
    /// changeovers with operator shifts. Handles weekend double-stacking and mixed builds.
    /// </summary>
    Task<List<BuildSequenceSuggestion>> SuggestBuildSequenceAsync(
        int machineId, List<BuildCandidate> candidates, DateTime horizonStart, DateTime horizonEnd);

    /// <summary>
    /// Lock a program (Ready → Scheduled). Sets IsLocked, updates ScheduleStatus.
    /// </summary>
    Task LockProgramAsync(int machineProgramId, string lockedBy);

    /// <summary>
    /// Unlock a program back to Ready. Clears IsLocked, resets ScheduleStatus.
    /// </summary>
    Task UnlockProgramAsync(int machineProgramId, string unlockedBy, string reason);
    // ══════════════════════════════════════════════════════════
    // Draft Programs (Engineer → Scheduler Handoff)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns BuildPlate programs in Draft status awaiting scheduling by a scheduler.
    /// These are plates composed by engineers but not yet assigned a time slot.
    /// </summary>
    Task<List<MachineProgram>> GetDraftProgramsAwaitingScheduleAsync();
}

// ══════════════════════════════════════════════════════════
// Result Records
// ══════════════════════════════════════════════════════════

/// <summary>
/// Result of cascade-rescheduling builds after an insertion/move.
/// </summary>
public record CascadeResult(int ShiftedCount, List<string> ShiftedBuilds);

/// <summary>
/// Result of scheduling a BuildPlate program.
/// </summary>
public record ProgramScheduleResult(
    ProgramScheduleSlot Slot,
    ChangeoverAnalysis? ChangeoverInfo,
    List<StageExecution> StageExecutions,
    int MachineProgramId,
    string ProgramName,
    ScheduleDiagnosticReport? Diagnostics = null);

/// <summary>
/// Schedule slot for a program on a machine.
/// </summary>
public record ProgramScheduleSlot(
    DateTime PrintStart,
    DateTime PrintEnd,
    DateTime ChangeoverStart,
    DateTime ChangeoverEnd,
    int MachineId,
    bool OperatorAvailableForChangeover);

/// <summary>
/// Result of FindBestSlotAsync — wraps a ProgramScheduleSlot with the chosen machine info.
/// </summary>
public record BestProgramSlot(
    ProgramScheduleSlot Slot,
    int MachineId,
    string MachineName);

/// <summary>
/// Timeline entry for a scheduled program on a machine.
/// </summary>
public record ProgramTimelineEntry(
    int MachineProgramId,
    string ProgramName,
    ProgramType ProgramType,
    DateTime PrintStart,
    DateTime PrintEnd,
    DateTime? ChangeoverStart,
    DateTime? ChangeoverEnd,
    ProgramScheduleStatus ScheduleStatus,
    int? StageExecutionId = null,
    bool HasChangeoverConflict = false);

/// <summary>
/// Result of analyzing changeover timing and operator availability.
/// </summary>
public record ChangeoverAnalysis(
    bool OperatorAvailable,
    DateTime ChangeoverStart,
    DateTime ChangeoverEnd,
    string? SuggestedAction,
    double? SuggestedDurationHours);

/// <summary>
/// Warning raised when two consecutive programs finish without an operator
/// being available to empty the cooldown chamber between them.
/// </summary>
public record ProgramChangeoverConflict(
    int MachineId,
    string MachineName,
    string PrecedingProgramName,
    DateTime PrecedingChangeoverStart,
    DateTime PrecedingChangeoverEnd,
    string FollowingProgramName,
    DateTime FollowingPrintStart,
    bool PrecedingOperatorAvailable,
    bool FollowingOperatorAvailable,
    string Warning);

/// <summary>
/// Input candidate for the build sequence optimizer.
/// </summary>
public record BuildCandidate(
    int PartId,
    string PartNumber,
    int DemandQuantity,
    List<(int Level, double DurationHours, int PartsPerBuild)> StackOptions);

/// <summary>
/// A suggested build in the optimized sequence.
/// </summary>
public record BuildSequenceSuggestion(
    int PartId,
    string PartNumber,
    int StackLevel,
    int PartsPerBuild,
    double DurationHours,
    DateTime SuggestedStart,
    DateTime SuggestedEnd,
    bool ChangeoverAligned,
    string Rationale);

/// <summary>
/// A single scheduling option generated by the suggestion engine.
/// </summary>
public record ScheduleOption(
    string Label,
    string Description,
    ProgramScheduleSlot Slot,
    int StackLevel,
    int PartsPerBuild,
    double DurationHours,
    bool ChangeoverAligned,
    bool IsWeekendOptimal,
    int RecommendationScore)
{
    public bool IsRecommended => RecommendationScore >= 80;
}

/// <summary>
/// Result of releasing parts from a completed BuildPlate program.
/// </summary>
public record ProgramPlateReleaseResult(
    int MachineProgramId,
    List<PartInstance> CreatedInstances,
    List<Job> CreatedJobs,
    int TotalPartCount);

/// <summary>
/// Result of scheduling a standard (CNC/EDM) program.
/// </summary>
public record StandardProgramScheduleResult(
    int JobId,
    string JobNumber,
    int MachineProgramId,
    List<StageExecution> StageExecutions,
    DateTime ScheduledStart,
    DateTime ScheduledEnd,
    double? EstimatedHours,
    List<string> Warnings);

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
