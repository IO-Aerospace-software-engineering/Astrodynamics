// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Tests.ColdStart;

/// <summary>
/// The mode <c>local-dst</c> (#512): the local times of a zone with daylight saving time, America/New_York, compared as the
/// instants <see cref="DateTime.ToUniversalTime"/> gives their calendars. The zone is set by <c>TZ</c> before the first
/// read of the local zone of the process, which Linux and macOS honour and Windows does not (exit 4 there: the zone of
/// the machine is not the one the checks need).
/// </summary>
/// <remarks>
/// The expected instants come from the rules of the zone (the IANA time zone database, America/New_York: EST = UTC − 5 h,
/// EDT = UTC − 4 h from the second Sunday of March at 02:00 local to the first Sunday of November at 02:00 local) and from
/// the documented reading of <see cref="DateTime.ToUniversalTime"/>: a calendar the zone skips (2025-03-09 02:00 to 03:00)
/// or repeats (2025-11-02 01:00 to 02:00) is read with the standard offset. So local 02:30 and local 03:30 of 2025-03-09
/// are one instant, 07:30 UTC, later than local 03:00 (07:00 UTC) although their calendar is earlier; before 11.0.0 the
/// local times of one frame were compared by their calendars.
/// Exit codes: 0 every check holds; 1 a check failed (on the standard error); 4 the zone could not be set.
/// </remarks>
internal static class LocalTimeChild
{
    internal static int Run()
    {
        if (!SetZone())
        {
            return 4;
        }

        var skipped = Local(2025, 3, 9, 2, 30);
        var afterSkip = Local(2025, 3, 9, 3, 30);
        var change = Local(2025, 3, 9, 3, 0);
        var repeated = Local(2025, 11, 2, 1, 30);

        int failures = 0;
        failures += Check("02:30 skipped is 07:30 UTC", skipped == Utc(2025, 3, 9, 7, 30));
        failures += Check("03:30 EDT is 07:30 UTC", afterSkip == Utc(2025, 3, 9, 7, 30));
        failures += Check("03:00 EDT is 07:00 UTC", change == Utc(2025, 3, 9, 7, 0));
        failures += Check("02:30 and 03:30 one instant", skipped == afterSkip && skipped.CompareTo(afterSkip) == 0 &&
                                                         skipped.GetHashCode() == afterSkip.GetHashCode());
        failures += Check("02:30 after 03:00", skipped > change && change < skipped);
        failures += Check("01:30 repeated read as EST, 06:30 UTC", repeated == Utc(2025, 11, 2, 6, 30));
        failures += Check("01:30 repeated after 05:30 UTC", repeated > Utc(2025, 11, 2, 5, 30));
        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// The mode <c>local-tolocal</c> (#751): <see cref="CheckToLocal"/> in America/New_York. Exit codes as <see cref="Run"/>.
    /// </summary>
    internal static int RunToLocal()
    {
        if (!SetZone())
        {
            return 4;
        }

        return CheckToLocal() == 0 ? 0 : 1;
    }

    // America/New_York as the local zone of the process, set before its first read; false when the platform ignores TZ
    private static bool SetZone()
    {
        Environment.SetEnvironmentVariable("TZ", "America/New_York");
        TimeZoneInfo.ClearCachedData();
        var zone = TimeZoneInfo.Local;
        if (zone.BaseUtcOffset != TimeSpan.FromHours(-5.0) || !zone.IsInvalidTime(new DateTime(2025, 3, 9, 2, 30, 0)) ||
            !zone.IsAmbiguousTime(new DateTime(2025, 11, 2, 1, 30, 0)))
        {
            Console.Error.WriteLine($"The zone America/New_York could not be set: the local zone is {zone.Id}.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// <see cref="Time.ToLocal"/> changes the label, not the instant (#751, decided by S. Guillet, 2026-11-18): over both
    /// transitions of 2025, every minute from 4 h before to 4 h after each, the local time of a UTC instant is the calendar
    /// of the zone's rule (UTC − 4 h before the transition, UTC − 5 h after it in November, the reverse in March), equals
    /// the instant, and its <see cref="Time.ToUTC"/> is that instant to the tick. Before, the label of an instant in the
    /// first of the two repeated hours lost the daylight time mark of <see cref="DateTime.ToLocalTime"/> (the
    /// <see cref="DateTimeKind.Local"/> <c>SpecifyKind</c> of the constructor) and was read back with the standard offset:
    /// 2025-11-02T05:30Z gave local 01:30, which is 06:30Z.
    /// </summary>
    private static int CheckToLocal()
    {
        int failures = 0;
        // The transitions as UTC instants: 2025-03-09 02:00 EST is 07:00Z, 2025-11-02 02:00 EDT is 06:00Z
        foreach (var (transition, offsetBefore, offsetAfter) in new[]
                 {
                     (new DateTime(2025, 3, 9, 7, 0, 0, DateTimeKind.Utc), -5.0, -4.0),
                     (new DateTime(2025, 11, 2, 6, 0, 0, DateTimeKind.Utc), -4.0, -5.0)
                 })
        {
            int wrong = 0;
            for (int minute = -240; minute <= 240; minute++)
            {
                var instant = transition.AddMinutes(minute);
                var utc = new Time(instant, TimeFrame.UTCFrame);
                var local = utc.ToLocal();
                var calendar = DateTime.SpecifyKind(instant.AddHours(minute < 0 ? offsetBefore : offsetAfter), DateTimeKind.Unspecified);
                if (DateTime.SpecifyKind(local.DateTime, DateTimeKind.Unspecified) != calendar || local != utc || local.ToUTC().DateTime != instant ||
                    local.ToUTC().DateTime.Ticks != instant.Ticks)
                {
                    if (wrong++ == 0)
                    {
                        Console.Error.WriteLine(
                            $"ToLocal of {instant:O}: local {local.DateTime:O}, expected {calendar:O}, back to UTC {local.ToUTC().DateTime:O}.");
                    }
                }
            }

            failures += Check($"ToLocal around {transition:O} is the instant ({wrong} of 481 minutes wrong)", wrong == 0);
        }

        // The issue's probe: 05:30Z, in the first repeated hour, and 06:30Z, in the second, both local 01:30
        var first = new Time(new DateTime(2025, 11, 2, 5, 30, 0), TimeFrame.UTCFrame);
        var second = new Time(new DateTime(2025, 11, 2, 6, 30, 0), TimeFrame.UTCFrame);
        failures += Check("05:30Z ToLocal is 05:30Z", first == first.ToLocal() && first.ToLocal().ToUTC().DateTime == first.DateTime);
        failures += Check("06:30Z ToLocal is 06:30Z", second == second.ToLocal() && second.ToLocal().ToUTC().DateTime == second.DateTime);
        failures += Check("both local 01:30, two instants", first.ToLocal().DateTime.Hour == 1 && second.ToLocal().DateTime.Hour == 1 &&
                                                           first.ToLocal() != second.ToLocal() && first.ToLocal() < second.ToLocal());
        // A label built from the calendar of the repeated hour keeps the standard offset (documented)
        failures += Check("calendar 01:30 is 06:30Z", Local(2025, 11, 2, 1, 30) == second);
        return failures;
    }

    private static Time Local(int year, int month, int day, int hour, int minute) =>
        new(new DateTime(year, month, day, hour, minute, 0), TimeFrame.LocalFrame);

    private static Time Utc(int year, int month, int day, int hour, int minute) =>
        new(new DateTime(year, month, day, hour, minute, 0), TimeFrame.UTCFrame);

    private static int Check(string name, bool holds)
    {
        if (holds)
        {
            return 0;
        }

        Console.Error.WriteLine($"Local time check failed: {name}.");
        return 1;
    }
}
