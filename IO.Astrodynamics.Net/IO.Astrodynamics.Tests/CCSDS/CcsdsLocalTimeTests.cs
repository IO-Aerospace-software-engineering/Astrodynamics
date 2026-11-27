// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Ccsds;
using IO.Astrodynamics.Tests.CCSDS.CDM;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.CCSDS;

/// <summary>
/// The CCSDS writers outside UTC (#617), in child processes whose time zone the test sets through <c>TZ</c>: the test
/// process cannot change its own zone without changing it for every test that runs beside, and the CI machines run in
/// UTC, where an <see cref="DateTimeKind.Unspecified"/> date read as local time is not shifted.
/// </summary>
/// <remarks>
/// <para>
/// The references: every date of a CDM is UTC (CCSDS 508.0-B-1 Cor. 2, 6.3.2.6; the record properties are named
/// <c>…Utc</c>), so a clock reading given without a kind is written as given; a <see cref="DateTimeKind.Local"/> date is
/// written as the UTC of that local time, from the IANA rules of the zone (Europe/Paris on 2010-03-13: CET, UTC+1, the
/// summer time from 2010-03-28; America/New_York: EST, UTC−5, the summer time from 2010-03-14 02:00 local), which the
/// expected texts state literally. The OPM and OMM <c>EPOCH</c>, <c>REF_FRAME_EPOCH</c> and <c>MAN_EPOCH_IGNITION</c> are
/// in <c>TIME_SYSTEM</c> (CCSDS 502.0-B-3), so a clock reading of kind <see cref="DateTimeKind.Utc"/> or
/// <see cref="DateTimeKind.Unspecified"/> is written as given, a local time converted to UTC under
/// <c>TIME_SYSTEM = UTC</c> and refused under any other (decided by S. Guillet, 2026-11-17); their <c>CREATION_DATE</c> is
/// UTC (CCSDS 502.0-B-3, Table 3-1).
/// </para>
/// <para>
/// Before #617 (measured on a85098d, Linux x64): under Europe/Paris the CDM writer wrote the eight dates given without a
/// kind one hour early (<c>2010-03-13T21:37:52.6180000Z</c>), under America/New_York five hours late, on the next day
/// (<c>2010-03-14T03:37:52.6180000Z</c>); the OPM and OMM writers wrote a local time as its clock reading, under
/// <c>TIME_SYSTEM = UTC</c> one hour or five hours off, and under <c>TDB</c> without an error.
/// </para>
/// <para>
/// Windows ignores <c>TZ</c>: there the child runs in the machine's zone, the offset it reports gives the local-time
/// expectations, and the zone is not checked.
/// </para>
/// </remarks>
[Collection(nameof(ColdStartCollection))]
public class CcsdsLocalTimeTests
{
    private const string ClockCdm = "2010-03-13T22:37:52.6180000Z";
    private const string ClockOdm = "2010-03-13T22:37:52.618000";
    private static readonly DateTime Clock = new(2010, 3, 13, 22, 37, 52, 618, DateTimeKind.Unspecified);
    private readonly ITestOutputHelper _output;

    public CcsdsLocalTimeTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    [Theory]
    [InlineData("Europe/Paris", 3600, "2010-03-13T21:37:52.6180000Z", "2010-03-13T21:37:52.618000")]
    [InlineData("America/New_York", -18000, "2010-03-14T03:37:52.6180000Z", "2010-03-14T03:37:52.618000")]
    public void Writers_OutsideUtc_FollowTheKindOfEachDate(string zone, int offsetSeconds, string localCdm, string localOdm)
    {
        var cdm = CdmTests.CreateManualEncounterCase().ToCdm(new CdmExportOptions
        {
            CreationDateUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc),
            Originator = "IO.Astrodynamics.Tests",
            MessageId = "CDM-UNIT-617"
        });
        var path = Path.Combine(Path.GetTempPath(), $"cdm_617_{Guid.NewGuid():N}.xml");
        Dictionary<string, string> lines;
        try
        {
            cdm.Save(path);
            var output = ColdStartTests.RunChildWithEnvironment($"(ccsds-local-time, {zone})",
                new Dictionary<string, string> { ["TZ"] = zone }, "ccsds-local-time", path);
            _output.WriteLine(output);
            lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split(' ', 2))
                .ToDictionary(parts => parts[0], parts => parts[1]);
        }
        finally
        {
            File.Delete(path);
        }

        var reported = lines["OFFSET"].Split(' ');
        int offset = int.Parse(reported[^1], CultureInfo.InvariantCulture);
        if (OperatingSystem.IsWindows())
        {
            var utc = Clock.AddSeconds(-offset);
            localCdm = utc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
            localOdm = utc.ToString("yyyy-MM-ddTHH:mm:ss.ffffff", CultureInfo.InvariantCulture);
        }
        else
        {
            // The child runs in the zone given: the test measures what it should
            Assert.Equal(offsetSeconds, offset);
        }

        // CDM: a date without a kind is the UTC clock reading given; a local time its UTC
        foreach (var keyword in new[]
                 {
                     "CREATION_DATE", "TCA", "START_SCREEN_PERIOD", "STOP_SCREEN_PERIOD", "SCREEN_ENTRY_TIME",
                     "SCREEN_EXIT_TIME", "TIME_LASTOB_START", "TIME_LASTOB_END"
                 })
        {
            Assert.Equal(ClockCdm, lines[$"CDM-UNSPECIFIED-{keyword}"]);
        }

        Assert.Equal(localCdm, lines["CDM-LOCAL-TCA"]);

        // OPM and OMM: a local time converted under TIME_SYSTEM = UTC, its CREATION_DATE always UTC
        foreach (var keyword in new[] { "CREATION_DATE", "EPOCH", "MAN_EPOCH_IGNITION" })
        {
            Assert.Equal(localOdm, lines[$"OPM-UTC-LOCAL-{keyword}"]);
        }

        foreach (var keyword in new[] { "CREATION_DATE", "EPOCH" })
        {
            Assert.Equal(localOdm, lines[$"OMM-UTC-LOCAL-{keyword}"]);
        }

        // Under TDB the clock reading of a date without a kind, and a local time refused
        Assert.Equal(ClockOdm, lines["OPM-TDB-UNSPECIFIED-EPOCH"]);
        Assert.Equal(ClockOdm, lines["OMM-TDB-UNSPECIFIED-EPOCH"]);
        Assert.Equal("ArgumentException opm", lines["OPM-TDB-LOCAL"]);
        Assert.Equal("ArgumentException omm", lines["OMM-TDB-LOCAL"]);
    }
}
