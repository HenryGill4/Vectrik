using Opcentrix_V3.Models;

namespace Opcentrix_V3.Services;

/// <summary>
/// Shared static helpers for shift-aware time calculations.
/// Used by both SchedulingService and ProgramSchedulingService.
/// </summary>
public static class ShiftTimeHelper
{
    /// <summary>
    /// Finds the earliest point at or after <paramref name="from"/> that falls within a shift window.
    /// Returns <paramref name="from"/> unchanged if no shifts are defined (24/7 operation).
    /// </summary>
    public static DateTime SnapToNextShiftStart(DateTime from, List<OperatingShift> shifts)
    {
        if (!shifts.Any()) return from;

        for (int dayOffset = 0; dayOffset < 30; dayOffset++)
        {
            var checkDate = from.Date.AddDays(dayOffset);
            var dayName = checkDate.DayOfWeek.ToString()[..3];

            var dayShifts = shifts
                .Where(s => s.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.StartTime)
                .ToList();

            foreach (var shift in dayShifts)
            {
                var shiftStart = checkDate + shift.StartTime;
                var shiftEnd = checkDate + shift.EndTime;
                if (shift.EndTime <= shift.StartTime)
                    shiftEnd = shiftEnd.AddDays(1);

                if (from < shiftEnd)
                    return from > shiftStart ? from : shiftStart;
            }
        }

        return from;
    }

    /// <summary>
    /// Advances from a starting time by the given number of work hours,
    /// skipping non-shift periods. Returns calendar time if no shifts defined.
    /// </summary>
    public static DateTime AdvanceByWorkHours(DateTime from, double hours, List<OperatingShift> shifts)
    {
        if (!shifts.Any()) return from.AddHours(hours);

        var remaining = hours;
        var current = from;

        for (int dayOffset = 0; dayOffset < 90 && remaining > 0.001; dayOffset++)
        {
            var checkDate = current.Date;
            if (checkDate < from.Date) checkDate = from.Date;

            var dayName = checkDate.DayOfWeek.ToString()[..3];
            var dayShifts = shifts
                .Where(s => s.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.StartTime)
                .ToList();

            foreach (var shift in dayShifts)
            {
                if (remaining <= 0.001) break;

                var shiftStart = checkDate + shift.StartTime;
                var shiftEnd = checkDate + shift.EndTime;
                if (shift.EndTime <= shift.StartTime)
                    shiftEnd = shiftEnd.AddDays(1);

                if (current >= shiftEnd) continue;

                var effectiveStart = current > shiftStart ? current : shiftStart;
                var availableHours = (shiftEnd - effectiveStart).TotalHours;

                if (availableHours <= 0) continue;

                if (remaining <= availableHours)
                    return effectiveStart.AddHours(remaining);

                remaining -= availableHours;
                current = shiftEnd;
            }

            current = checkDate.AddDays(1);
        }

        return current.AddHours(remaining);
    }

    /// <summary>
    /// Checks whether an entire time window falls within a single shift on the start day.
    /// </summary>
    public static bool IsWithinShiftWindow(DateTime windowStart, DateTime windowEnd, List<OperatingShift> shifts)
    {
        if (!shifts.Any()) return true;

        var checkDate = windowStart.Date;
        var dayName = checkDate.DayOfWeek.ToString()[..3];

        var dayShifts = shifts
            .Where(s => s.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.StartTime)
            .ToList();

        foreach (var shift in dayShifts)
        {
            var shiftStart = checkDate + shift.StartTime;
            var shiftEnd = checkDate + shift.EndTime;
            if (shift.EndTime <= shift.StartTime)
                shiftEnd = shiftEnd.AddDays(1);

            if (windowStart >= shiftStart && windowEnd <= shiftEnd)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the next shift start strictly after <paramref name="after"/>.
    /// Returns null if no shifts or none found within 7 days.
    /// </summary>
    public static DateTime? FindNextShiftStart(DateTime after, List<OperatingShift> shifts)
    {
        if (!shifts.Any()) return null;

        for (int dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var checkDate = after.Date.AddDays(dayOffset);
            var dayName = checkDate.DayOfWeek.ToString()[..3];

            var dayShifts = shifts
                .Where(s => s.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.StartTime)
                .ToList();

            foreach (var shift in dayShifts)
            {
                var shiftStart = checkDate + shift.StartTime;
                if (shiftStart > after)
                    return shiftStart;
            }
        }

        return null;
    }
}
