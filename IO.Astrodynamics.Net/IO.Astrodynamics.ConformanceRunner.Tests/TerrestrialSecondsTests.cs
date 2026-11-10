// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The TT seconds the runner counts about the Earth (<see cref="TerrestrialSeconds"/>, #630): <c>tca_s</c>,
/// <c>burns[].elapsed_s</c> and <c>events.*[].elapsed_s</c>, from a UTC epoch to a TDB label, against mpmath at 50
/// digits on the library's TDB − TT series (<c>tools/conformance/terrestrial_seconds_reference.py</c>,
/// <c>Data/TerrestrialSecondsReference/terrestrial_seconds_reference.json</c>): 100 labels in the 6 h after
/// 2026-01-20T06:00:00 UTC, the epoch of the conjunction cases (TDB − TT = +0.47 ms), and 100 after
/// 2026-10-20T06:00:00 UTC (−1.61 ms), the last of each 6 h after its epoch. The expression the runner used before,
/// <c>(t.ToTDT() - epoch.ToTDT()).TotalSeconds</c>, is off there by 0.0004 to 0.992 tick and by −0.995 to −0.006 tick
/// (<c>former_error_ticks</c> of the file).
/// </summary>
public class TerrestrialSecondsTests
{
    private const string ReferencePath = "Data/TerrestrialSecondsReference/terrestrial_seconds_reference.json";

    // Twice the worst measured over the 200 labels, 3.638e-12 s, the same on Linux x64, Windows x64 and macOS arm64
    // (CI of #632 at 01d14e2c, 2026-11-10). The resolution of a double near 2.16e4 s is 3.6e-12 s; one tick is 1e-7 s
    private const double Bound = 7.3e-12;

    private readonly ITestOutputHelper _output;

    public TerrestrialSecondsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FromAUtcEpochToATdbLabel_IsTheExactTtSeconds()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ReferencePath));
        double worst = 0.0;
        int count = 0;
        foreach (var epoch in document.RootElement.GetProperty("epochs").EnumerateArray())
        {
            var utc = new Time(new DateTime(epoch.GetProperty("utc_ticks").GetInt64()), TimeFrame.UTCFrame);
            foreach (var label in epoch.GetProperty("labels").EnumerateArray())
            {
                var tdb = new Time(new DateTime(label.GetProperty("tdb_ticks").GetInt64()), TimeFrame.TDBFrame);
                double expected = double.Parse(label.GetProperty("tt_seconds").GetString(), CultureInfo.InvariantCulture);
                double error = System.Math.Abs(TerrestrialSeconds.Between(utc, tdb) - expected);
                worst = System.Math.Max(worst, error);
                count++;
                Assert.True(error <= Bound, $"{epoch.GetProperty("utc_epoch").GetString()} UTC to the TDB label of " +
                                            $"{tdb.DateTime.Ticks} ticks: {error:E3} s off the reference");
            }
        }

        _output.WriteLine($"{count} labels: worst |runner - mpmath| {worst:E3} s");
        Assert.Equal(200, count);
    }

    [Fact]
    public void TheLabel6HoursAfterTheEpochOfTheConjunctionCases_IsTheExactTtSeconds()
    {
        // The last label of the first epoch: 2026-01-20T12:01:09.1844785 TDB, TT 21600.0000000397406 s after
        // 2026-01-20T06:00:00 UTC; the former expression gave 0.60 tick more
        var utc = new Time(new DateTime(2026, 1, 20, 6, 0, 0), TimeFrame.UTCFrame);
        var tdb = new Time(new DateTime(639045072691844785L), TimeFrame.TDBFrame);
        Assert.Equal(new DateTime(2026, 1, 20, 12, 1, 9).AddTicks(1844785), tdb.DateTime);
        Assert.InRange(TerrestrialSeconds.Between(utc, tdb) - 21600.0000000397406058875229913, -Bound, Bound);
    }

    [Fact]
    public void BetweenUtcEpochs_CountsTheLeapSecond()
    {
        // Exact in ticks between UTC epochs, the leap second of 2016-12-31 counted (TAI - UTC 36 s, then 37 s)
        var before = new Time(new DateTime(2016, 12, 31, 23, 59, 59), TimeFrame.UTCFrame);
        var after = new Time(new DateTime(2017, 1, 1, 0, 0, 0), TimeFrame.UTCFrame);
        Assert.Equal(2.0, TerrestrialSeconds.Between(before, after));
        Assert.Equal(-2.0, TerrestrialSeconds.Between(after, before));
        Assert.Equal(0.0, TerrestrialSeconds.Between(after, after));
    }
}
