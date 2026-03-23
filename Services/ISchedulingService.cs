using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

public interface ISchedulingService
{
    /// <summary>
    /// Auto-schedule all stage executions for a job, assigning machines
    /// and finding non-overlapping time slots. Respects shift hours,
    /// machine capabilities, and predecessor chains.
    /// </summary>
    Task AutoScheduleJobAsync(int jobId, DateTime? startAfter = null);

    /// <summary>
    /// Auto-schedule a job and collect per-execution diagnostic data.
    /// </summary>
    Task<JobScheduleDiagnostic> AutoScheduleJobWithDiagnosticsAsync(int jobId, DateTime? startAfter = null);

    /// <summary>
    /// Re-schedule a single execution to its earliest available slot
    /// on its currently assigned machine (or auto-assign one).
    /// </summary>
    Task<StageExecution> AutoScheduleExecutionAsync(int executionId, DateTime? startAfter = null);

    /// <summary>
    /// Find the earliest available time slot on a machine that doesn't
    /// overlap with existing scheduled work.
    /// </summary>
    Task<ScheduleSlot> FindEarliestSlotAsync(int machineId, double durationHours, DateTime notBefore);

    /// <summary>
    /// Get machines capable of executing a production stage, ordered by
    /// preference (specific assignment → preferred → capable → all).
    /// </summary>
    Task<List<Machine>> GetCapableMachinesAsync(int productionStageId, PartStageRequirement? requirement = null);

    /// <summary>
    /// Auto-schedule all unscheduled executions across all jobs,
    /// ordered by job priority then due date.
    /// </summary>
    Task<int> AutoScheduleAllAsync();

    /// <summary>
    /// Clear all scheduling data: nulls StageExecution schedule fields,
    /// resets Job schedule windows, and unlocks BuildPackages.
    /// Returns the number of entities affected.
    /// </summary>
    Task<ScheduleClearResult> ClearAllScheduleDataAsync();

    /// <summary>
    /// Hard-delete all scheduling-related data: stage executions, jobs, build packages
    /// (including parts, revisions, file info), part instances, and batches.
    /// Use with extreme caution — this is a full data wipe for debugging.
    /// </summary>
    Task<DataDeleteResult> DeleteAllSchedulingDataAsync();

    /// <summary>
    /// Hard-delete all scheduling data AND work orders.
    /// This is the nuclear option for a completely clean slate for testing.
    /// </summary>
    Task<DataDeleteResult> DeleteAllSchedulingAndWorkOrderDataAsync();

    /// <summary>
    /// Returns counts of key entities for the debug page.
    /// </summary>
    Task<DatabaseStats> GetDatabaseStatsAsync();
}

public record ScheduleClearResult(int ExecutionsCleared, int JobsReset, int BuildsUnlocked);

public record DataDeleteResult(
    int StageExecutionsDeleted,
    int JobsDeleted,
    int BuildsDeleted,
    int PartInstancesDeleted,
    int BatchesDeleted,
    int WorkOrdersDeleted = 0);

public record ScheduleSlot(DateTime Start, DateTime End, int MachineId);

public record DatabaseStats(
    int Machines,
    int ProductionStages,
    int ManufacturingProcesses,
    int Parts,
    int Jobs,
    int StageExecutions,
    int BuildPackages,
    int ProductionBatches,
    int WorkOrders);
