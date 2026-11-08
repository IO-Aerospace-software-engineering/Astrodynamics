// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The library's SGP4 (CSPICE <c>evsgp4_c</c>, Vallado's 2006 code in the AFSPC mode with WGS-72 old) against Vallado's
/// code, python <c>sgp4</c> 2.27 with <c>WGS72OLD</c> and <c>sgp4init(..., 'a', ...)</c>, and the TLE epochs against
/// their exact decimal decode and CSPICE <c>getelm_c</c>, from <c>tools/sgp4_reference/sgp4_ref.py</c>
/// (<c>Data/Sgp4Reference/sgp4_reference.json</c>): the 29 element sets of Vallado's SGP4-VER.TLE that SGP4 evaluates,
/// at the tsince of their rows in tcppver.out, and two TLEs hourly over a day at UTC instants (#599).
/// </summary>
/// <remarks>
/// The bounds are twice the worst value measured on Linux x64 on 2026-11-08 (PROVISIONAL until the CI of Linux, Windows
/// and macOS arm64). On the verification set the library is CSPICE, within 7.6e-5 m and 1.3e-8 m/s of the reference (the
/// script's <c>cspice_minus_sgp4</c>); at UTC instants the residual is the double of the epoch the library hands to
/// <c>evsgp4_c</c>, ET₀ plus the TT seconds.
/// </remarks>
public class Sgp4ReferenceTests
{
    // PROVISIONAL bounds (Linux x64, 2026-11-08, until the CI of Linux, Windows and macOS arm64): twice the worst measured
    // per component, m and m/s. Measured: the verification set 7.59e-5 m (23333) and 1.29e-8 m/s (20413's second set),
    // CSPICE's own difference from Vallado's code on the same samples; the two TLEs at UTC instants 3.57e-4 m and
    // 4.09e-7 m/s (the ISS: the double of ET0 plus the TT seconds, ~1e-7 s at 8.3e8 s)
    private const double VerificationPosition = 1.6e-4, VerificationVelocity = 2.6e-8;
    private const double InstantPosition = 7.2e-4, InstantVelocity = 8.2e-7;

    private readonly ITestOutputHelper _output;

    public Sgp4ReferenceTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static readonly Lazy<JsonElement> Reference = new(() =>
        JsonDocument.Parse(File.ReadAllText("Data/Sgp4Reference/sgp4_reference.json")).RootElement);

    private static Vector3 Km(JsonElement e) =>
        new Vector3(e[0].GetDouble(), e[1].GetDouble(), e[2].GetDouble()) * 1e3;

    private static TimeSystem.Time Utc(string iso) => new(DateTime.ParseExact(iso, "yyyy-MM-ddTHH:mm:ss.fffffff",
        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal), TimeFrame.UTCFrame);

    private static TimeSpan Minutes(double minutes) => new((long)System.Math.Round(minutes * TimeSpan.TicksPerMinute));

    private static double MaxComponent(Vector3 v) => System.Math.Max(System.Math.Abs(v.X), System.Math.Max(System.Math.Abs(v.Y), System.Math.Abs(v.Z)));

    [Fact]
    public void VerificationSet_MatchesValladoAfspcWgs72Old()
    {
        double worstR = 0.0, worstV = 0.0;
        int count = 0;
        foreach (var set in Reference.Value.GetProperty("verification").EnumerateArray())
        {
            var tle = new TLE("VER", set.GetProperty("line1").GetString(), set.GetProperty("line2").GetString());
            double setR = 0.0, setV = 0.0;
            foreach (var sample in set.GetProperty("samples").EnumerateArray())
            {
                // The instant tsince TT minutes after the TLE epoch, exact in ticks: the tsince of tcppver.out have at
                // most 7 decimals of a minute, whole multiples of 6 us (TimeSpan.FromSeconds truncates 60 * 54.2028672 to
                // a tick below, 0.77 mm on 22312)
                double tsince = sample.GetProperty("tsince_min").GetDouble();
                var epoch = tle.Epoch.ToTDT() + Minutes(tsince);
                var sv = tle.ToOsculating(epoch);
                setR = System.Math.Max(setR, MaxComponent(sv.Position - Km(sample.GetProperty("r_km"))));
                setV = System.Math.Max(setV, MaxComponent(sv.Velocity - Km(sample.GetProperty("v_km_s"))));
                Assert.Equal(Frames.Frame.TEME, sv.Frame);
                count++;
            }

            _output.WriteLine($"{set.GetProperty("satnum").GetInt32()}: {setR:E3} m, {setV:E3} m/s");
            worstR = System.Math.Max(worstR, setR);
            worstV = System.Math.Max(worstV, setV);
        }

        _output.WriteLine($"worst over {count} samples: {worstR:E3} m, {worstV:E3} m/s");
        Assert.Equal(577, count);
        Assert.InRange(worstR, 0.0, VerificationPosition);
        Assert.InRange(worstV, 0.0, VerificationVelocity);
    }

    [Fact]
    public void UtcInstants_CountTsinceInTerrestrialTime()
    {
        // Vallado's driver at the UTC Julian date of the instant: tsince the UTC days from the epoch, the TT seconds in a
        // window without a leap second. Before #599 the library counted TDB seconds: 0.110 m (06251) and 0.214 m (the ISS)
        double worstR = 0.0, worstV = 0.0;
        foreach (var item in Reference.Value.GetProperty("instants").EnumerateArray())
        {
            var tle = new TLE(item.GetProperty("name").GetString(), item.GetProperty("line1").GetString(), item.GetProperty("line2").GetString());
            double r = 0.0, v = 0.0;
            foreach (var sample in item.GetProperty("samples").EnumerateArray())
            {
                var sv = tle.ToOsculating(Utc(sample.GetProperty("utc").GetString()));
                r = System.Math.Max(r, MaxComponent(sv.Position - Km(sample.GetProperty("r_km"))));
                v = System.Math.Max(v, MaxComponent(sv.Velocity - Km(sample.GetProperty("v_km_s"))));

                // The same instant in TDB gives the same state: tsince is the TT seconds, whatever the frame of the epoch
                var tdb = tle.ToOsculating(Utc(sample.GetProperty("utc").GetString()).ToTDB());
                Assert.InRange((tdb.Position - sv.Position).Magnitude(), 0.0, 1e-3);
            }

            _output.WriteLine($"{item.GetProperty("name").GetString()}: {r:E3} m, {v:E3} m/s");
            worstR = System.Math.Max(worstR, r);
            worstV = System.Math.Max(worstV, v);
        }

        Assert.InRange(worstR, 0.0, InstantPosition);
        Assert.InRange(worstV, 0.0, InstantVelocity);
    }

    [Fact]
    public void Epoch_IsTheExactDecimalOfTheField()
    {
        // #603: the epoch field read in decimal, to the tick; before, through AddDays(double), 100 ns early on 43 % of
        // TLEs. The SGP4 epoch is getelm_c's (ET), read from the native layer
        foreach (var set in Reference.Value.GetProperty("verification").EnumerateArray())
        {
            string line1 = set.GetProperty("line1").GetString(), line2 = set.GetProperty("line2").GetString();
            var tle = new TLE("VER", line1, line2);
            var expected = Utc(set.GetProperty("epoch_utc").GetString());
            Assert.Equal(TimeFrame.UTCFrame, tle.Epoch.Frame);
            Assert.Equal(expected.DateTime.Ticks, tle.Epoch.DateTime.Ticks);

            Assert.Equal(set.GetProperty("epoch_et").GetDouble(), SpiceAPI.Instance.ReadTleEpoch("VER", line1, line2));
        }
    }

    [Fact]
    public void FailedSgp4_Throws()
    {
        // 33334 (e out of range at the epoch, checksums recomputed) and 28350 after its decay: Vallado's errors 3 and 1,
        // CSPICE's BADPECCENTRICITY and BADMECCENTRICITY; the native layer returns a zero state, the library throws (#454)
        foreach (var item in Reference.Value.GetProperty("errors").EnumerateArray())
        {
            Assert.NotEqual(0, item.GetProperty("sgp4_error").GetInt32());
            Assert.StartsWith("SPICE(", item.GetProperty("cspice_error").GetString());
            var tle = new TLE("ERR", item.GetProperty("line1").GetString(), item.GetProperty("line2").GetString());
            var epoch = tle.Epoch.ToTDT() + Minutes(item.GetProperty("tsince_min").GetDouble());
            Assert.Throws<InvalidOperationException>(() => tle.ToOsculating(epoch));
        }
    }

    [Fact]
    public void SpiceApi_CountsTsinceInTerrestrialTime()
    {
        // The public SpiceAPI call takes the epoch of the line itself: the same state as the TLE's
        var item = Reference.Value.GetProperty("instants")[1];
        string line1 = item.GetProperty("line1").GetString(), line2 = item.GetProperty("line2").GetString();
        var sample = item.GetProperty("samples")[24];
        var epoch = Utc(sample.GetProperty("utc").GetString());
        var sv = SpiceAPI.Instance.ConvertTleToStateVector("ISS", line1, line2, epoch).ToStateVector();
        var tle = new TLE("ISS", line1, line2).ToOsculating(epoch);
        Assert.Equal(tle.Position, sv.Position);
        Assert.Equal(tle.Velocity, sv.Velocity);
        Assert.InRange(MaxComponent(sv.Position - Km(sample.GetProperty("r_km"))), 0.0, InstantPosition);
    }
}
