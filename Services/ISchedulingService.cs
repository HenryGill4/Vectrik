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
}

public record ScheduleSlot(DateTime Start, DateTime End, int MachineId);
