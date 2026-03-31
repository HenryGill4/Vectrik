using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vectrik.Data;
using Vectrik.Hubs;
using Vectrik.Models;
using Vectrik.Models.Enums;
using Vectrik.Services.Platform;

namespace Vectrik.Services;

public class ChangeoverDispatchService : IChangeoverDispatchService
{
    private readonly TenantDbContext _db;
    private readonly ISetupDispatchService _dispatchService;
    private readonly IShiftManagementService _shiftService;
    private readonly IDispatchNotifier _notifier;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ChangeoverDispatchService> _logger;

    private const int BasePriority = 50;
    private const int UrgencyBonus = 50;

    public ChangeoverDispatchService(
        TenantDbContext db,
        ISetupDispatchService dispatchService,
        IShiftManagementService shiftService,
        IDispatchNotifier notifier,
        ITenantContext tenantContext,
        ILogger<ChangeoverDispatchService> logger)
    {
        _db = db;
        _dispatchService = dispatchService;
        _shiftService = shiftService;
        _notifier = notifier;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<SetupDispatch?> CreateOrUpdateChangeoverDispatchAsync(int machineId, DateTime buildEndTime)
    {
        var machine = await _db.Machines.FindAsync(machineId);
        if (machine == null || !machine.AutoChangeoverEnabled)
            return null;

        // Check for existing active changeover dispatch for this machine
        var existing = await _db.SetupDispatches
            .Where(d => d.MachineId == machineId
                && d.DispatchType == DispatchType.Changeover
                && d.Status != DispatchStatus.Completed
                && d.Status != DispatchStatus.Cancelled)
            .FirstOrDefaultAsync();

        var shifts = await _shiftService.GetEffectiveShiftsForMachineAsync(machineId);
        var priority = CalculateChangeoverPriority(buildEndTime, shifts, out var reason, out var breakdownJson);

        if (existing != null)
        {
            // Update priority on existing dispatch
            await _dispatchService.UpdateDispatchPriorityAsync(existing.Id, priority, reason, breakdownJson);
            existing.ScheduledStartAt = buildEndTime;
            existing.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            if (priority >= 95)
                await NotifyUrgentAsync(machineId, $"URGENT: Changeover required on {machine.Name} — chamber at risk");

            return existing;
        }

        // Create new changeover dispatch
        var dispatch = await _dispatchService.CreateManualDispatchAsync(
            machineId: machineId,
            type: DispatchType.Changeover,
            estimatedSetupMinutes: machine.OperatorUnloadMinutes,
            notes: $"Auto-generated changeover dispatch. Build ends ~{buildEndTime:g}.");

        await _dispatchService.UpdateDispatchPriorityAsync(dispatch.Id, priority, reason, breakdownJson);

        // Set scheduled start
        var reloaded = await _dispatchService.GetByIdAsync(dispatch.Id);
        if (reloaded == null) return dispatch;
        reloaded.ScheduledStartAt = buildEndTime;
        await _db.SaveChangesAsync();
        dispatch = reloaded;

        if (priority >= 95)
            await NotifyUrgentAsync(machineId, $"URGENT: Changeover required on {machine.Name} — chamber at risk");

        return dispatch;
    }

    public async Task EscalateChangeoverPrioritiesAsync()
    {
        var activeChangeovers = await _dispatchService.GetActiveDispatchesByTypeAsync(DispatchType.Changeover);

        foreach (var dispatch in activeChangeovers)
        {
            var shifts = await _shiftService.GetEffectiveShiftsForMachineAsync(dispatch.MachineId);
            var buildEndTime = dispatch.ScheduledStartAt ?? DateTime.UtcNow;
            var priority = CalculateChangeoverPriority(buildEndTime, shifts, out var reason, out var breakdownJson);

            if (priority != dispatch.Priority)
            {
                await _dispatchService.UpdateDispatchPriorityAsync(dispatch.Id, priority, reason, breakdownJson);

                if (priority >= 95)
                {
                    var machine = dispatch.Machine ?? await _db.Machines.FindAsync(dispatch.MachineId);
                    var machineName = machine?.Name ?? $"Machine {dispatch.MachineId}";
                    await NotifyUrgentAsync(dispatch.MachineId,
                        $"CRITICAL: Changeover overdue on {machineName} — machine at risk of going DOWN");
                }
            }
        }
    }

    public async Task<List<SetupDispatch>> GetActiveChangeoverDispatchesAsync()
    {
        return await _dispatchService.GetActiveDispatchesByTypeAsync(DispatchType.Changeover);
    }

    public async Task HandleChangeoverCompletionAsync(int dispatchId)
    {
        var dispatch = await _dispatchService.GetByIdAsync(dispatchId);
        if (dispatch == null) return;

        var machine = await _db.Machines.FindAsync(dispatch.MachineId);
        if (machine != null)
        {
            machine.SetupState = MachineSetupState.Running;
            machine.LastSetupChangeAt = DateTime.UtcNow;
            machine.LastModifiedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    // ── Priority Calculation ──────────────────────────────────

    public static int CalculateChangeoverPriority(
        DateTime buildEndTime,
        List<OperatingShift> shifts,
        out string reason,
        out string breakdownJson)
    {
        var now = DateTime.UtcNow;
        var currentShiftEnd = FindCurrentShiftEnd(now, shifts);

        int priority;
        if (currentShiftEnd == null)
        {
            // No active shift right now — this is critical (shift ended, chamber not cleared)
            priority = 100;
            reason = "CRITICAL: No active shift — changeover overdue, machine at risk of going DOWN";
        }
        else
        {
            var shiftEnd = currentShiftEnd.Value;
            var totalShiftMinutes = GetCurrentShiftDuration(now, shifts);
            var remainingMinutes = Math.Max(0, (shiftEnd - now).TotalMinutes);
            var timeRemainingRatio = totalShiftMinutes > 0
                ? remainingMinutes / totalShiftMinutes
                : 0;

            // Priority formula: base + urgency * (1 - ratio)^2
            var escalation = UrgencyBonus * Math.Pow(1 - timeRemainingRatio, 2);
            priority = (int)Math.Clamp(BasePriority + escalation, 1, 100);

            reason = remainingMinutes switch
            {
                > 120 => $"Normal: {remainingMinutes:F0}min remaining in shift",
                > 60 => $"Elevated: {remainingMinutes:F0}min remaining in shift",
                > 30 => $"High: {remainingMinutes:F0}min remaining — changeover needed soon",
                > 0 => $"URGENT: Only {remainingMinutes:F0}min remaining in shift",
                _ => "CRITICAL: Shift ended — changeover overdue"
            };
        }

        priority = Math.Clamp(priority, 1, 100);

        var breakdown = new
        {
            basePriority = BasePriority,
            urgencyBonus = UrgencyBonus,
            buildEndTime = buildEndTime.ToString("o"),
            calculatedAt = now.ToString("o"),
            shiftEndTime = currentShiftEnd?.ToString("o"),
            finalPriority = priority
        };
        breakdownJson = JsonSerializer.Serialize(breakdown);

        return priority;
    }

    private static DateTime? FindCurrentShiftEnd(DateTime now, List<OperatingShift> shifts)
    {
        var currentTime = now.TimeOfDay;
        var dayName = now.DayOfWeek switch
        {
            DayOfWeek.Monday => "Mon",
            DayOfWeek.Tuesday => "Tue",
            DayOfWeek.Wednesday => "Wed",
            DayOfWeek.Thursday => "Thu",
            DayOfWeek.Friday => "Fri",
            DayOfWeek.Saturday => "Sat",
            DayOfWeek.Sunday => "Sun",
            _ => ""
        };

        foreach (var shift in shifts)
        {
            if (!shift.IsActive) continue;
            if (!shift.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase)) continue;

            // Handle shifts that cross midnight
            if (shift.EndTime > shift.StartTime)
            {
                if (currentTime >= shift.StartTime && currentTime < shift.EndTime)
                    return now.Date + shift.EndTime;
            }
            else
            {
                // Crosses midnight
                if (currentTime >= shift.StartTime)
                    return now.Date.AddDays(1) + shift.EndTime;
                if (currentTime < shift.EndTime)
                    return now.Date + shift.EndTime;
            }
        }

        return null;
    }

    private static double GetCurrentShiftDuration(DateTime now, List<OperatingShift> shifts)
    {
        var currentTime = now.TimeOfDay;
        var dayName = now.DayOfWeek switch
        {
            DayOfWeek.Monday => "Mon",
            DayOfWeek.Tuesday => "Tue",
            DayOfWeek.Wednesday => "Wed",
            DayOfWeek.Thursday => "Thu",
            DayOfWeek.Friday => "Fri",
            DayOfWeek.Saturday => "Sat",
            DayOfWeek.Sunday => "Sun",
            _ => ""
        };

        foreach (var shift in shifts)
        {
            if (!shift.IsActive) continue;
            if (!shift.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase)) continue;

            if (shift.EndTime > shift.StartTime)
            {
                if (currentTime >= shift.StartTime && currentTime < shift.EndTime)
                    return (shift.EndTime - shift.StartTime).TotalMinutes;
            }
            else
            {
                if (currentTime >= shift.StartTime || currentTime < shift.EndTime)
                    return (TimeSpan.FromHours(24) - shift.StartTime + shift.EndTime).TotalMinutes;
            }
        }

        return 480; // Default 8-hour shift fallback
    }

    private async Task NotifyUrgentAsync(int machineId, string message)
    {
        var tenantCode = _tenantContext.TenantCode;
        if (!string.IsNullOrEmpty(tenantCode))
        {
            try { await _notifier.SendUrgentDispatchAsync(tenantCode, machineId, message); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Urgent changeover notification failed for machine {MachineId}", machineId);
            }
        }
    }
}
