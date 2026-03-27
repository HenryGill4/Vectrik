using Vectrik.Models;
using Vectrik.Services;
using Xunit;

namespace Vectrik.Tests.Services;

public class ShiftTimeHelperTests
{
    // Standard weekday shift: Mon-Fri 06:00-14:00
    private static List<OperatingShift> WeekdayShift() =>
    [
        new OperatingShift
        {
            Id = 1, Name = "Day Shift",
            StartTime = TimeSpan.FromHours(6),
            EndTime = TimeSpan.FromHours(14),
            DaysOfWeek = "Mon,Tue,Wed,Thu,Fri",
            IsActive = true
        }
    ];

    // Two shifts: Day + Afternoon, Mon-Fri
    private static List<OperatingShift> TwoShifts() =>
    [
        new OperatingShift
        {
            Id = 1, Name = "Day Shift",
            StartTime = TimeSpan.FromHours(6),
            EndTime = TimeSpan.FromHours(14),
            DaysOfWeek = "Mon,Tue,Wed,Thu,Fri",
            IsActive = true
        },
        new OperatingShift
        {
            Id = 2, Name = "Afternoon Shift",
            StartTime = TimeSpan.FromHours(14),
            EndTime = TimeSpan.FromHours(22),
            DaysOfWeek = "Mon,Tue,Wed,Thu,Fri",
            IsActive = true
        }
    ];

    // Overnight shift: 22:00 → 06:00
    private static List<OperatingShift> OvernightShift() =>
    [
        new OperatingShift
        {
            Id = 1, Name = "Night Shift",
            StartTime = TimeSpan.FromHours(22),
            EndTime = TimeSpan.FromHours(6),
            DaysOfWeek = "Mon,Tue,Wed,Thu,Fri",
            IsActive = true
        }
    ];

    // ── IsWithinShiftWindow ─────────────────────────────────

    [Fact]
    public void IsWithinShiftWindow_NoShifts_ReturnsTrue()
    {
        var result = ShiftTimeHelper.IsWithinShiftWindow(
            DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30), []);

        Assert.True(result);
    }

    [Fact]
    public void IsWithinShiftWindow_WithinShift_ReturnsTrue()
    {
        // Wednesday 08:00 - 08:30 is within 06:00-14:00
        var start = new DateTime(2026, 3, 25, 8, 0, 0, DateTimeKind.Utc); // Wed
        var end = start.AddMinutes(30);

        Assert.True(ShiftTimeHelper.IsWithinShiftWindow(start, end, WeekdayShift()));
    }

    [Fact]
    public void IsWithinShiftWindow_OutsideShift_ReturnsFalse()
    {
        // Wednesday 03:00 - 03:30 is outside 06:00-14:00
        var start = new DateTime(2026, 3, 25, 3, 0, 0, DateTimeKind.Utc); // Wed
        var end = start.AddMinutes(30);

        Assert.False(ShiftTimeHelper.IsWithinShiftWindow(start, end, WeekdayShift()));
    }

    [Fact]
    public void IsWithinShiftWindow_Weekend_ReturnsFalse()
    {
        // Saturday 08:00 - 08:30 — no weekend shifts configured
        var start = new DateTime(2026, 3, 28, 8, 0, 0, DateTimeKind.Utc); // Sat
        var end = start.AddMinutes(30);

        Assert.False(ShiftTimeHelper.IsWithinShiftWindow(start, end, WeekdayShift()));
    }

    [Fact]
    public void IsWithinShiftWindow_OvernightShift_ReturnsTrue()
    {
        // Tuesday 23:00 - 23:30 is within overnight 22:00-06:00
        var start = new DateTime(2026, 3, 24, 23, 0, 0, DateTimeKind.Utc); // Tue
        var end = start.AddMinutes(30);

        Assert.True(ShiftTimeHelper.IsWithinShiftWindow(start, end, OvernightShift()));
    }

    [Fact]
    public void IsWithinShiftWindow_SpansAcrossShiftEnd_ReturnsFalse()
    {
        // Wednesday 13:30 - 14:30 crosses shift end at 14:00
        var start = new DateTime(2026, 3, 25, 13, 30, 0, DateTimeKind.Utc);
        var end = start.AddHours(1);

        Assert.False(ShiftTimeHelper.IsWithinShiftWindow(start, end, WeekdayShift()));
    }

    // ── SnapToNextShiftStart ────────────────────────────────

    [Fact]
    public void SnapToNextShiftStart_NoShifts_ReturnsOriginal()
    {
        var from = new DateTime(2026, 3, 25, 3, 0, 0, DateTimeKind.Utc);

        Assert.Equal(from, ShiftTimeHelper.SnapToNextShiftStart(from, []));
    }

    [Fact]
    public void SnapToNextShiftStart_BeforeShift_SnapsToShiftStart()
    {
        // Wednesday 03:00 → snaps to 06:00
        var from = new DateTime(2026, 3, 25, 3, 0, 0, DateTimeKind.Utc);

        var result = ShiftTimeHelper.SnapToNextShiftStart(from, WeekdayShift());

        Assert.Equal(new DateTime(2026, 3, 25, 6, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void SnapToNextShiftStart_DuringShift_ReturnsOriginal()
    {
        // Wednesday 10:00 is during 06:00-14:00, returns as-is
        var from = new DateTime(2026, 3, 25, 10, 0, 0, DateTimeKind.Utc);

        var result = ShiftTimeHelper.SnapToNextShiftStart(from, WeekdayShift());

        Assert.Equal(from, result);
    }

    [Fact]
    public void SnapToNextShiftStart_FridayAfterShift_SnapsToMonday()
    {
        // Friday 15:00 (after 14:00 shift end) → snaps to Monday 06:00
        var from = new DateTime(2026, 3, 27, 15, 0, 0, DateTimeKind.Utc); // Fri
        var expected = new DateTime(2026, 3, 30, 6, 0, 0, DateTimeKind.Utc); // Mon

        var result = ShiftTimeHelper.SnapToNextShiftStart(from, WeekdayShift());

        Assert.Equal(expected, result);
    }

    // ── FindNextShiftStart ──────────────────────────────────

    [Fact]
    public void FindNextShiftStart_NoShifts_ReturnsNull()
    {
        Assert.Null(ShiftTimeHelper.FindNextShiftStart(DateTime.UtcNow, []));
    }

    [Fact]
    public void FindNextShiftStart_BeforeShift_ReturnsShiftStart()
    {
        // Wednesday 03:00 → next shift is Wed 06:00
        var after = new DateTime(2026, 3, 25, 3, 0, 0, DateTimeKind.Utc);

        var result = ShiftTimeHelper.FindNextShiftStart(after, WeekdayShift());

        Assert.Equal(new DateTime(2026, 3, 25, 6, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void FindNextShiftStart_SaturdayEvening_FindsMondayMorning()
    {
        // Saturday 18:00 → no weekend shifts → finds Monday 06:00
        var after = new DateTime(2026, 3, 28, 18, 0, 0, DateTimeKind.Utc); // Sat

        var result = ShiftTimeHelper.FindNextShiftStart(after, WeekdayShift());

        Assert.Equal(new DateTime(2026, 3, 30, 6, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void FindNextShiftStart_TwoShifts_ReturnsEarliestAfterTime()
    {
        // Wednesday 07:00 (during day shift) → next shift start is afternoon 14:00
        var after = new DateTime(2026, 3, 25, 7, 0, 0, DateTimeKind.Utc);

        var result = ShiftTimeHelper.FindNextShiftStart(after, TwoShifts());

        Assert.Equal(new DateTime(2026, 3, 25, 14, 0, 0, DateTimeKind.Utc), result);
    }

    // ── SuggestWeekendStackLevel ────────────────────────────

    [Fact]
    public void SuggestWeekendStackLevel_PicksClosestToWeekendSpan()
    {
        // Friday 14:00 → Monday 06:00 = 64 hours gap. Changeover = 30 min. Target = 63.5h
        var buildStart = new DateTime(2026, 3, 27, 14, 0, 0, DateTimeKind.Utc); // Fri 14:00
        var options = new List<(int Level, double DurationHours, int PartsPerBuild)>
        {
            (1, 24, 6),   // ends Sat 14:00 — too short
            (2, 48, 12),  // ends Sun 14:00 — closer
            (3, 64, 18),  // ends Mon 06:00 — perfect!
        };

        var result = ShiftTimeHelper.SuggestWeekendStackLevel(
            buildStart, 30, WeekdayShift(), options);

        // Level 3 at 64h is closest to the 63.5h target (within 1h tolerance)
        Assert.Equal(3, result);
    }

    [Fact]
    public void SuggestWeekendStackLevel_NoShifts_ReturnsFirstOption()
    {
        var options = new List<(int Level, double DurationHours, int PartsPerBuild)>
        {
            (1, 24, 6),
            (2, 48, 12),
        };

        var result = ShiftTimeHelper.SuggestWeekendStackLevel(
            DateTime.UtcNow, 30, [], options);

        Assert.Equal(1, result);
    }
}
