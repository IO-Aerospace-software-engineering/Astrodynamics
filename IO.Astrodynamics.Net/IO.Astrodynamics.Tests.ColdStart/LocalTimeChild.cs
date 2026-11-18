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
        Environment.SetEnvironmentVariable("TZ", "America/New_York");
        TimeZoneInfo.ClearCachedData();
        var zone = TimeZoneInfo.Local;
        if (zone.BaseUtcOffset != TimeSpan.FromHours(-5.0) || !zone.IsInvalidTime(new DateTime(2025, 3, 9, 2, 30, 0)) ||
            !zone.IsAmbiguousTime(new DateTime(2025, 11, 2, 1, 30, 0)))
        {
            Console.Error.WriteLine($"The zone America/New_York could not be set: the local zone is {zone.Id}.");
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
