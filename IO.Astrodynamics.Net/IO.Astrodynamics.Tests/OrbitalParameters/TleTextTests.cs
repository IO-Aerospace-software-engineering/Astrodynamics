// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.Common.Enums;
using IO.Astrodynamics.CCSDS.OMM;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The text a TLE is written as, field by field, against the TLE format: each field the nearest value its columns hold
/// (#600, #601, #602, #480), the epoch the UTC of the instant (#479, #485), and the fit from elements in TEME (#464).
/// The expected epoch fields come from <c>tools/sgp4_reference/sgp4_ref.py</c> (exact decimal, CSPICE's UTC of a TDB, TT
/// or TAI instant); the other expected fields are the format's closed forms, written out below. The comparisons are text.
/// </summary>
public class TleTextTests
{
    // a = 6800 km, e = 0.001 (unless stated), i = 51.6°, Ω = 30°, ω = 40°, M = 50°, mean, in TEME
    private const double A = 6800000.0, I = 51.6 * IO.Astrodynamics.Constants.Deg2Rad, Raan = 30.0 * IO.Astrodynamics.Constants.Deg2Rad;
    private const double Aop = 40.0 * IO.Astrodynamics.Constants.Deg2Rad, M = 50.0 * IO.Astrodynamics.Constants.Deg2Rad;

    public TleTextTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Lazy<JsonElement[]> EpochFields = new(() =>
        JsonDocument.Parse(File.ReadAllText("Data/Sgp4Reference/sgp4_reference.json")).RootElement.GetProperty("epoch_fields")
            .EnumerateArray().ToArray());

    private static TLE Create(TimeSystem.Time epoch, double e = 0.001, double i = I, double raan = Raan, double aop = Aop, double m = M,
        double bstar = 0.0001, double nDot = 0.0, double nDDot = 0.0)
    {
        var earth = new CelestialBody(399, Frames.Frame.ECLIPTIC_J2000, epoch);
        var mean = new KeplerianElements(A, e, i, raan, aop, m, earth, epoch, Frames.Frame.TEME, elementsType: OrbitalElementsType.Mean);
        return TLE.Create(mean, "TEXT", 25666, "98067A", 100, bstar: bstar, nDot: nDot, nDDot: nDDot);
    }

    private static TimeSystem.Time Utc(string iso) => new(DateTime.ParseExact(iso, "yyyy-MM-ddTHH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal), TimeFrame.UTCFrame);

    private static string EpochField(TLE tle) => tle.Line1.Substring(18, 14);

    [Fact]
    public void Epoch_RoundsToTheNearestLatticeValueWithTheCarry()
    {
        // #600: 2026-11-07T23:59:59.9996 was written 26311.00000000, a day early; the tie of 2024-02-29T23:59:59.999568
        // (0.999999995 day) rounds up into day 61
        foreach (var item in EpochFields.Value.Where(e => e.TryGetProperty("utc", out _)))
        {
            var utc = Utc(item.GetProperty("utc").GetString());
            var tle = Create(utc);
            Assert.Equal(item.GetProperty("field").GetString(), EpochField(tle));
            Assert.Equal(69, tle.Line1.Length);
        }
    }

    [Fact]
    public void Epoch_OfANonUtcInstantIsItsUtc()
    {
        // #479: a TDB, TT or TAI epoch was written as its own calendar, 69.19 s, 69.184 s or 37 s late. The library's UTC
        // of a TDB instant is within 1.1 us of CSPICE's, far from a rounding edge of these fields
        foreach (var item in EpochFields.Value.Where(e => e.TryGetProperty("scale", out _)))
        {
            var dateTime = DateTime.ParseExact(item.GetProperty("instant").GetString(), "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            ITimeFrame frame = item.GetProperty("scale").GetString() switch
            {
                "TDB" => TimeFrame.TDBFrame,
                "TT" => TimeFrame.TDTFrame,
                _ => TimeFrame.TAIFrame
            };
            Assert.Equal(item.GetProperty("field").GetString(), EpochField(Create(new TimeSystem.Time(dateTime, frame))));
        }
    }

    [Fact]
    public void ExponentFields_AreTheNearestRepresentableValue()
    {
        // #601: below 1e-10 the exponent took two digits, a 70-character line that threw; now the nearest value of a
        // five-digit mantissa and a one-digit exponent: 0 below 5e-11, 0.10000e-9 from 5e-11 to 1e-10
        var epoch = Utc("2026-11-07T12:00:00");
        Assert.Equal(" 00000-0", Create(epoch, bstar: 1.5e-11).Line1.Substring(53, 8));
        Assert.Equal(" 00000-0", Create(epoch, nDDot: 1.2e-13).Line1.Substring(44, 8));
        Assert.Equal(" 10000-9", Create(epoch, bstar: 7e-11).Line1.Substring(53, 8));
        Assert.Equal("-10000-9", Create(epoch, bstar: -9.99996e-11).Line1.Substring(53, 8));
        Assert.Equal(" 10000-9", Create(epoch, nDDot: 1e-10).Line1.Substring(44, 8));
        Assert.Equal(" 12345-9", Create(epoch, bstar: 1.2345e-10).Line1.Substring(53, 8));
        Assert.Equal(" 10270-3", Create(epoch, bstar: 1.027e-4).Line1.Substring(53, 8));
        Assert.Equal("-44346-2", Create(epoch, bstar: -4.4346e-3).Line1.Substring(53, 8));
        Assert.Equal(" 99999+9", Create(epoch, bstar: 9.9999e8).Line1.Substring(53, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(epoch, bstar: 1e10));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(epoch, bstar: double.NaN));
    }

    [Fact]
    public void Eccentricity_IsNotClampedAbove()
    {
        // #602: e = 0 was written 0000001
        var epoch = Utc("2026-11-07T12:00:00");
        Assert.Equal("0000000", Create(epoch, e: 0.0).Line2.Substring(26, 7));
        Assert.Equal("0000000", Create(epoch, e: 4e-8).Line2.Substring(26, 7));
        Assert.Equal("0000001", Create(epoch, e: 6e-8).Line2.Substring(26, 7));
        Assert.Equal(0.0, Create(epoch, e: 0.0).MeanEccentricity);
    }

    [Fact]
    public void ZeroFields_AreWrittenWithoutASign()
    {
        // #480: −0.0 angles were written -0.0000 and ṅ/2 in (−5e-9, 0] - .00000000, a 70-character line that threw
        var epoch = Utc("2026-11-07T12:00:00");
        var line2 = Create(epoch, i: -0.0, raan: -0.0, aop: -0.0, m: -0.0).Line2;
        Assert.Equal(["  0.0000", "  0.0000", "  0.0000", "  0.0000"],
            new[] { line2.Substring(8, 8), line2.Substring(17, 8), line2.Substring(34, 8), line2.Substring(43, 8) });
        foreach (var nDot in new[] { -0.0, -1e-12, -1e-9, -4.99e-9, 4e-9 })
            Assert.Equal(" .00000000", Create(epoch, nDot: nDot).Line1.Substring(33, 10));
        Assert.Equal("-.00000001", Create(epoch, nDot: -6e-9).Line1.Substring(33, 10));
        Assert.Equal("-.00000001", Create(epoch, nDot: -5.000001e-9).Line1.Substring(33, 10));
    }

    [Fact]
    public void Epoch_IsReadToTheTick()
    {
        // #603: 26311.28305435 is 06:47:35.89584 exactly; it was read 06:47:35.8958399, and its OMM text 35.895839
        // (CALSPHERE 1, CelesTrak GP data of 2026-11-07)
        var tle = new TLE("CALSPHERE 1", "1 00900U 64063C   26311.28305435  .00000638  00000+0  63900-3 0  9998",
            "2 00900  90.2232  75.0898 0025865 215.7471 230.6709 13.76755757 91111");
        var expected = new DateTime(2026, 11, 7, 6, 47, 35).AddTicks(8958400);
        Assert.Equal(expected, tle.Epoch.DateTime);
        Assert.Equal(TimeFrame.UTCFrame, tle.Epoch.Frame);
        Assert.Equal(expected, tle.ToOmm().Data.MeanElements.Epoch);
        Assert.Equal("26311.28305435", EpochField(TLE.Create(tle.ToMeanKeplerianElements(), "CALSPHERE 1", 900, "64063C", 9111)));
    }

    [Fact]
    public void ToTle_FitsElementsInTeme()
    {
        // #464: elements in TEME (a TLE, osculating Keplerian elements) threw InvalidCastException; they fit as their
        // state in TEME
        var tle = new TLE("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var config = new IO.Astrodynamics.OrbitalParameters.TLE.Configuration(25544, "ISS", "98067A");
        var fromState = tle.ToOsculating().ToTLE(config);
        var fromTle = tle.ToTLE(config);
        var fromElements = tle.ToOsculating().ToKeplerianElements().ToTLE(config);
        Assert.Equal(fromState.Line1, fromTle.Line1);
        Assert.Equal(fromState.Line2, fromTle.Line2);
        Assert.Equal(Frames.Frame.TEME, tle.ToOsculating().ToKeplerianElements().Frame);
        Assert.Equal(fromState.Line2[..20], fromElements.Line2[..20]);
        Assert.Throws<InvalidOperationException>(() => tle.ToMeanKeplerianElements().ToTLE(config));
    }

    private static Omm IssOmm(string timeSystem, DateTime epoch)
    {
        var metadata = new OmmMetadata("ISS", "1998-067A", "EARTH", "TEME", timeSystem, "SGP4");
        var meanElements = MeanElements.CreateWithMeanMotion(epoch, 15.49309423, 0.0000493, 51.6423, 353.0312, 320.8755, 39.2360);
        var tleParameters = TleParameters.CreateWithBStarAndDDot(0.0001027, 0.00016717, 0.0, ephemerisType: 0, classificationType: "U",
            noradCatalogId: 25544, elementSetNumber: 999, revolutionNumberAtEpoch: 25703);
        return new Omm(CcsdsHeader.CreateDefault(), metadata, OmmData.CreateForTle(meanElements, tleParameters));
    }

    [Fact]
    public void OmmToTle_ReadsTheEpochInItsTimeSystem()
    {
        // #485: the EPOCH of an OMM was read as UTC whatever TIME_SYSTEM said
        var instant = new DateTime(2021, 3, 20, 12, 0, 0);
        foreach (var item in EpochFields.Value.Where(e => e.TryGetProperty("scale", out _) && e.GetProperty("instant").GetString() == "2021-03-20T12:00:00"))
        {
            var tle = IssOmm(item.GetProperty("scale").GetString(), instant).ToTle();
            Assert.Equal(item.GetProperty("field").GetString(), EpochField(tle));
        }

        Assert.Equal("21079.50000000", EpochField(IssOmm("UTC", instant).ToTle()));
        Assert.Equal("21079.49979167", EpochField(IssOmm("GPS", instant).ToTle()));
        Assert.Throws<InvalidOperationException>(() => IssOmm("UT1", instant).ToTle());
    }

    [Fact]
    public void Opm_ReadsAndWritesTheEpochInUtcAndItsTimeSystem()
    {
        // #485 (reader) and #479 (writer). CSPICE: 2021-03-20 12:00 TDB is 11:58:50.8143932 UTC; the library's UTC of a
        // TDB instant is within 1.1 us of it
        var earth = new CelestialBody(399, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB);
        var cspiceUtc = new DateTime(2021, 3, 20, 11, 58, 50).AddTicks(8143932);
        var tdb = new TimeSystem.Time(new DateTime(2021, 3, 20, 12, 0, 0), TimeFrame.TDBFrame);
        var state = new StateVector(new Vector3(7000000.0, 0.0, 0.0), new Vector3(0.0, 7500.0, 0.0), earth, tdb, Frames.Frame.ICRF);

        var written = Opm.CreateFromStateVector("SAT", "2021-001A", state);
        Assert.Equal("UTC", written.Metadata.TimeSystem);
        Assert.InRange(System.Math.Abs((written.Data.StateVector.Epoch - cspiceUtc).TotalSeconds), 0.0, 1.1e-6);
        Assert.InRange(System.Math.Abs((written.ToStateVector().Epoch - tdb).TotalSeconds), 0.0, 2e-7);

        var opmStateVector = new OpmStateVector(new DateTime(2021, 3, 20, 12, 0, 0), 7000.0, 0.0, 0.0, 0.0, 7.5, 0.0);
        foreach (var (system, frame) in new (string, ITimeFrame)[]
                     { ("TDB", TimeFrame.TDBFrame), ("tdb", TimeFrame.TDBFrame), ("TT", TimeFrame.TDTFrame), ("TAI", TimeFrame.TAIFrame), ("GPS", TimeFrame.GPSFrame), ("UTC", TimeFrame.UTCFrame) })
        {
            var opm = Opm.CreateMinimal("SAT", "2021-001A", "EARTH", "ICRF", system, opmStateVector);
            var read = opm.ToStateVector();
            Assert.Equal(new TimeSystem.Time(new DateTime(2021, 3, 20, 12, 0, 0), frame), read.Epoch);
            Assert.Equal(new Vector3(7000000.0, 0.0, 0.0), read.Position);
            Assert.Equal(read.Epoch, opm.ToStateVector(earth).Epoch);
        }

        Assert.Throws<InvalidOperationException>(() => Opm.CreateMinimal("SAT", "2021-001A", "EARTH", "ICRF", "UT1", opmStateVector).ToStateVector());
        Assert.Equal(TimeFrame.UTCFrame, opmStateVector.ToStateVector(earth, Frames.Frame.ICRF).Epoch.Frame);
    }

    [Fact]
    public void ManeuverIgnition_IsWrittenInUtc()
    {
        // #479: MAN_EPOCH_IGNITION took the calendar of the thrust window, TDB in a propagation
        var engine = new Engine("engine", "model", "sn", 350.0, 50.0, new FuelTank("tank", "model", "sn", 4000.0, 3000.0));
        var maneuver = new ApogeeHeightManeuver(TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, TimeSpan.Zero, 7000000.0, engine);
        maneuver.ThrustWindow = new Window(new TimeSystem.Time(new DateTime(2021, 3, 20, 12, 0, 0), TimeFrame.TDBFrame), TimeSpan.FromSeconds(10.0));
        var parameters = maneuver.ToOpmManeuverParameters();
        Assert.InRange(System.Math.Abs((parameters.ManEpochIgnition - new DateTime(2021, 3, 20, 11, 58, 50).AddTicks(8143932)).TotalSeconds), 0.0, 1.1e-6);
    }
}
