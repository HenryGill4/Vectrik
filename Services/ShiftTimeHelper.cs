using Vectrik.Models;

namespace Vectrik.Services;

/// <summary>
/// Shared static helpers for shift-aware time calculations.
/// Used by both SchedulingService and ProgramSchedulingService.
/// </summary>
public static class ShiftTimeHelper
{
    /// <summary>
    /// Gets all active shifts for a given day, ordered by start time.
    /// </summary>
    public static List<OperatingShift> GetShiftsForDay(DateTime date, List<OperatingShift> shifts)
    {
        var dayName = date.DayOfWeek.ToString()[..3];
        return shifts
            .Where(s => s.DaysOfWeek.Contains(dayName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.StartTime)
            .ToList();
    }

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
            var dayShifts = GetShiftsForDay(checkDate, shifts);

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

            var dayShifts = GetShiftsForDay(checkDate, shifts);

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
        var dayShifts = GetShiftsForDay(checkDate, shifts);

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
    /// Given a build start time, finds the optimal build duration such that
    /// buildEnd + changeoverMinutes aligns with the next shift start.
    /// Returns null if no alignment is possible.
    /// </summary>
    public static double? FindChangeoverAlignedDuration(
        DateTime buildStart, double changeoverMinutes, List<OperatingShift> shifts)
    {
        if (!shifts.Any()) return null;

        // Look forward up to 5 days for shift starts
        for (int dayOffset = 0; dayOffset < 5; dayOffset++)
        {
            var checkDate = buildStart.Date.AddDays(dayOffset);
            var dayShifts = GetShiftsForDay(checkDate, shifts);

            foreach (var shift in dayShifts)
            {
                var shiftStart = checkDate + shift.StartTime;
                if (shiftStart <= buildStart) continue;

                // Target: buildEnd + changeover = shiftStart
                var targetEnd = shiftStart.AddMinutes(-changeoverMinutes);
                var duration = (targetEnd - buildStart).TotalHours;

                if (duration > 0.5 && duration < 200) // Sanity bounds
                    return duration;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines how many non-shift hours exist between two times.
    /// Useful for weekend gap calculation.
    /// </summary>
    public static double GetNonShiftHours(DateTime from, DateTime to, List<OperatingShift> shifts)
    {
        if (!shifts.Any()) return 0; // 24/7 = no gaps

        double gapHours = 0;
        var cursor = from;

        for (int dayOffset = 0; cursor < to && dayOffset < 30; dayOffset++)
        {
            var checkDate = cursor.Date;
            var dayShifts = GetShiftsForDay(checkDate, shifts);

            if (!dayShifts.Any())
            {
                // Entire day is a gap
                var dayEnd = checkDate.AddDays(1);
                var gapStart = cursor > from ? cursor : from;
                var gapEnd = dayEnd < to ? dayEnd : to;
                gapHours += (gapEnd - gapStart).TotalHours;
                cursor = dayEnd;
                continue;
            }

            foreach (var shift in dayShifts)
            {
                var shiftStart = checkDate + shift.StartTime;
                var shiftEnd = checkDate + shift.EndTime;
                if (shift.EndTime <= shift.StartTime)
                    shiftEnd = shiftEnd.AddDays(1);

                // Gap before shift
                if (cursor < shiftStart && shiftStart < to)
                {
                    var gapStart = cursor > from ? cursor : from;
                    var gapEnd = shiftStart < to ? shiftStart : to;
                    if (gapStart < gapEnd)
                        gapHours += (gapEnd - gapStart).TotalHours;
                }

                cursor = shiftEnd > cursor ? shiftEnd : cursor;
            }

            if (cursor <= checkDate.AddDays(1))
                cursor = checkDate.AddDays(1);
        }

        return gapHours;
    }

    /// <summary>
    /// Suggests the best stack level for a weekend build.
    /// Returns the stack level whose duration best spans the weekend gap.
    /// </summary>
    public static int SuggestWeekendStackLevel(
        DateTime buildStart, double changeoverMinutes,
        List<OperatingShift> shifts,
        IReadOnlyList<(int Level, double DurationHours, int PartsPerBuild)> stackOptions)
    {
        if (!stackOptions.Any()) return 1;
        if (!shifts.Any()) return stackOptions[0].Level;

        // Find the next shift start (Monday morning typically)
        var nextShift = FindNextShiftStart(buildStart, shifts);
        if (nextShift == null) return stackOptions[0].Level;

        var targetEnd = nextShift.Value.AddMinutes(-changeoverMinutes);
        var targetDuration = (targetEnd - buildStart).TotalHours;

        if (targetDuration <= 0) return stackOptions[0].Level;

        // Pick the stack level whose duration is closest to target without being significantly shorter
        var best = stackOptions[0];
        var bestDelta = double.MaxValue;

        foreach (var opt in stackOptions)
        {
            var delta = Math.Abs(opt.DurationHours - targetDuration);
            // Prefer durations that END before the shift (not after)
            if (opt.DurationHours <= targetDuration + 1) // Allow 1h tolerance for being slightly over
            {
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = opt;
                }
            }
        }

        return best.Level;
    }

    /// <summary>
    /// Returns the on-shift time segments within a calendar window.
    /// Used to visually split Gantt bars into active-work vs paused-overnight blocks.
    /// Returns [(from, to)] if no shifts are defined (24/7 mode).
    /// </summary>
    public static List<(DateTime Start, DateTime End)> GetOnShiftSegments(
        DateTime from, DateTime to, List<OperatingShift> shifts)
    {
        if (!shifts.Any() || from >= to)
            return new List<(DateTime, DateTime)> { (from, to) };

        var segments = new List<(DateTime Start, DateTime End)>();

        for (var checkDate = from.Date; checkDate <= to.Date; checkDate = checkDate.AddDays(1))
        {
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

                // Clip to the [from, to] window
                var segStart = shiftStart < from ? from : shiftStart;
                var segEnd = shiftEnd > to ? to : shiftEnd;

                if (segStart < segEnd)
                    segments.Add((segStart, segEnd));
            }
        }

        // If no shifts matched (e.g., weekend-only window with weekday shifts), return full span
        return segments.Count > 0 ? segments : new List<(DateTime, DateTime)> { (from, to) };
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
            var dayShifts = GetShiftsForDay(checkDate, shifts);

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
