using Microsoft.EntityFrameworkCore;
using Opcentrix_V3.Data;
using Opcentrix_V3.Models;
using Opcentrix_V3.Models.Enums;

namespace Opcentrix_V3.Services;

/// <inheritdoc />
public class SchedulingDiagnosticsService : ISchedulingDiagnosticsService
{
    private readonly TenantDbContext _db;

    public SchedulingDiagnosticsService(TenantDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<List<Machine>> GetSchedulableMachinesAsync()
    {
        return await _db.Machines
            .Where(m => m.IsActive && m.IsAvailableForScheduling)
            .OrderBy(m => m.MachineType)
            .ThenBy(m => m.Name)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<SchedulingSnapshot> CaptureSnapshotAsync(
        int machineId, DateTime? rangeStart = null, DateTime? rangeEnd = null)
    {
        var machine = await _db.Machines.FindAsync(machineId)
            ?? throw new InvalidOperationException($"Machine '{machineId}' not found.");

        var from = rangeStart ?? DateTime.UtcNow.AddDays(-1);
        var to = rangeEnd ?? DateTime.UtcNow.AddDays(14);

        // Pull all non-terminal executions on this machine in the range
        var executions = await _db.StageExecutions
            .Include(e => e.Job)
                .ThenInclude(j => j!.Part)
            .Include(e => e.Job)
                .ThenInclude(j => j!.WorkOrderLine)
                    .ThenInclude(wl => wl!.WorkOrder)
            .Include(e => e.ProductionStage)
            .Include(e => e.ProcessStage)
            .Where(e => e.MachineId == machineId
                && e.Status != StageExecutionStatus.Completed
                && e.Status != StageExecutionStatus.Skipped
                && e.Status != StageExecutionStatus.Failed
                && e.ScheduledStartAt != null
                && e.ScheduledStartAt <= to
                && (e.ScheduledEndAt ?? e.ScheduledStartAt) >= from)
            .OrderBy(e => e.ScheduledStartAt)
            .ToListAsync();

        // Count unscheduled executions that could potentially go on this machine
        var unscheduledCount = await _db.StageExecutions
            .CountAsync(e => e.Status == StageExecutionStatus.NotStarted
                && (e.ScheduledStartAt == null || e.MachineId == null));

        // Detect conflicts (overlapping executions on same machine)
        var conflicts = DetectConflicts(executions);

        return new SchedulingSnapshot(
            Machine: new SchedulingSnapshotMachine(
                machine.Id, machine.Name, machine.MachineType,
                machine.Status.ToString(), machine.IsAdditiveMachine,
                machine.AutoChangeoverEnabled, machine.ChangeoverMinutes,
                machine.HourlyRate),
            CapturedAt: DateTime.UtcNow,
            RangeStart: from,
            RangeEnd: to,
            Executions: executions.Select(MapExecution).ToList(),
            BuildPackages: [],
            Timeline: [],
            Conflicts: conflicts,
            UnscheduledCount: unscheduledCount);
    }

    private static List<SchedulingSnapshotConflict> DetectConflicts(List<StageExecution> executions)
    {
        var conflicts = new List<SchedulingSnapshotConflict>();
        for (var i = 0; i < executions.Count; i++)
        {
            var a = executions[i];
            if (a.ScheduledStartAt is null || a.ScheduledEndAt is null) continue;

            for (var j = i + 1; j < executions.Count; j++)
            {
                var b = executions[j];
                if (b.ScheduledStartAt is null || b.ScheduledEndAt is null) continue;

                if (a.ScheduledStartAt < b.ScheduledEndAt && a.ScheduledEndAt > b.ScheduledStartAt)
                {
                    var overlapStart = a.ScheduledStartAt > b.ScheduledStartAt
                        ? a.ScheduledStartAt.Value : b.ScheduledStartAt.Value;
                    var overlapEnd = a.ScheduledEndAt < b.ScheduledEndAt
                        ? a.ScheduledEndAt.Value : b.ScheduledEndAt.Value;

                    conflicts.Add(new SchedulingSnapshotConflict(
                        a.Id, b.Id,
                        FormatExecDescription(a),
                        FormatExecDescription(b),
                        overlapStart, overlapEnd));
                }
            }
        }
        return conflicts;
    }

    private static string FormatExecDescription(StageExecution e)
    {
        var part = e.Job?.Part?.PartNumber ?? "no-part";
        var stage = e.ProductionStage?.Name ?? $"stage-{e.ProductionStageId}";
        var times = $"{e.ScheduledStartAt:MM/dd HH:mm}–{e.ScheduledEndAt:MM/dd HH:mm}";
        return $"Exec#{e.Id} [{stage}] {part} ({times})";
    }

    private static SchedulingSnapshotExecution MapExecution(StageExecution e) => new(
        e.Id,
        e.JobId,
        e.Job?.Part?.PartNumber,
        e.Job?.Part?.Name,
        e.ProductionStage?.Name ?? $"stage-{e.ProductionStageId}",
        e.ProcessStage?.ProductionStage?.Name,
        e.Status.ToString(),
        e.ScheduledStartAt,
        e.ScheduledEndAt,
        e.EstimatedHours,
        null,
        e.BatchGroupId,
        e.Job?.WorkOrderLine?.WorkOrder?.OrderNumber,
        e.IsUnmanned);

}
