using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.OMM;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// What <see cref="Tle.Create"/>, <see cref="Astrodynamics.OrbitalParameters.OrbitalParameters.ToTle"/> and
/// <see cref="Omm.ToTle"/> accept and write by default: SGP4 mean elements of an Earth orbit in TEME only (#607), and one
/// default element set number, 999 (#606).
/// </summary>
public class TleCreateRulesTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 1, 20, 12, 50, 14, TimeSystem.TimeScale.UTC);

    public TleCreateRulesTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static CelestialBody EarthAtEpoch => new(399, Frames.Frame.ECLIPTIC_J2000, Epoch);

    private static KeplerianElements MeanElements(ILocalizable observer, Frames.Frame frame) =>
        new(6_796_882.0, 0.0000493, 51.6423 * IO.Astrodynamics.Constants.Deg2Rad, 353.0312 * IO.Astrodynamics.Constants.Deg2Rad,
            320.8755 * IO.Astrodynamics.Constants.Deg2Rad, 39.2360 * IO.Astrodynamics.Constants.Deg2Rad, observer, Epoch, frame,
            elementsType: OrbitalElementsType.Mean);

    /// <summary>
    /// The checksum of a TLE line by the rule of the format (NORAD's Spacetrack Report No. 3; CelesTrak's "FAQs: Two-Line
    /// Element Set Format"): the digits of columns 1 to 68 summed, each minus sign counting 1, modulo 10.
    /// </summary>
    private static int Checksum(string line)
    {
        int sum = 0;
        foreach (var c in line.Substring(0, 68))
        {
            if (char.IsDigit(c)) sum += c - '0';
            else if (c == '-') sum += 1;
        }

        return sum % 10;
    }

    /// <summary>The element set number of line 1 (columns 65 to 68) and its checksum (column 69).</summary>
    private static void AssertElementSetNumber999(string line1)
    {
        Assert.Equal(69, line1.Length);
        Assert.Equal(" 999", line1.Substring(64, 4));
        Assert.Equal(Checksum(line1), line1[68] - '0');
    }

    #region #606, one default element set number

    [Fact]
    public void Create_WithoutElementSetNumber_Writes999()
    {
        // #606: Tle.Create defaulted to 9999, Omm.ToTle to 999; 999 is the value CelesTrak and Space-Track publish
        // (decided by S. Guillet, 2026-11-17)
        var tle = Tle.Create(MeanElements(EarthAtEpoch, Frames.Frame.TEME), "ISS", 25544, "98067A", 2570);
        AssertElementSetNumber999(tle.Line1);
        Assert.Equal(999, Tle.DefaultElementSetNumber);
    }

    [Fact]
    public void TleFitOptions_WithoutElementSetNumber_Is999()
    {
        Assert.Equal(999, new IO.Astrodynamics.OrbitalParameters.TLE.TleFitOptions().ElementSetNumber);
    }

    [Fact]
    public void OmmToTle_WithoutElementSetNumber_Writes999()
    {
        var omm = Omm.CreateForTle("ISS", "1998-067A", new DateTime(2021, 1, 20, 12, 50, 14), 15.49309423, 0.0000493, 51.6423,
            353.0312, 320.8755, 39.2360, 0.0001027, 0.00016717, 0.0, noradCatalogId: 25544, revolutionNumber: 2570);
        Assert.Null(omm.Data.TleParameters.ElementSetNumber);
        AssertElementSetNumber999(omm.ToTle().Line1);
    }

    [Fact]
    public void ToTle_WithTheDefaultConfiguration_Writes999()
    {
        // An Earth orbit in ICRF: the fit rotates it to TEME, and the TLE is written as by Tle.Create
        var iss = new Tle("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var state = iss.ToStateVector().ToFrame(Frames.Frame.ICRF).ToStateVector();
        var tle = state.ToTle("ISS", 25544, "98067A");
        AssertElementSetNumber999(tle.Line1);
    }

    #endregion

    #region #607, SGP4 mean elements of an Earth orbit in TEME

    [Fact]
    public void Create_MeanElementsInIcrf_Throws()
    {
        // #607: mean elements in ICRF were written as a TLE, their angles taken as TEME's
        var ex = Assert.Throws<ArgumentException>(() =>
            Tle.Create(MeanElements(EarthAtEpoch, Frames.Frame.ICRF), "ISS", 25544, "98067A", 2570));
        Assert.Equal("meanElements", ex.ParamName);
        Assert.Contains("TEME", ex.Message);
        Assert.Contains(Frames.Frame.ICRF.Name, ex.Message);
    }

    [Fact]
    public void Create_MeanElementsAboutTheMoon_Throws()
    {
        // #607: mean elements about the Moon were written as a TLE, their mean motion from the Moon's GM
        var moon = new CelestialBody(301, Frames.Frame.ECLIPTIC_J2000, Epoch);
        var ex = Assert.Throws<ArgumentException>(() =>
            Tle.Create(MeanElements(moon, Frames.Frame.TEME), "LUNAR", 25544, "98067A", 2570));
        Assert.Equal("meanElements", ex.ParamName);
        Assert.Contains("MOON", ex.Message);
    }

    [Fact]
    public void Create_MeanElementsAboutTheEarthMoonBarycenter_Throws()
    {
        var barycenter = new Barycenter(3, Epoch);
        var ex = Assert.Throws<ArgumentException>(() =>
            Tle.Create(MeanElements(barycenter, Frames.Frame.TEME), "EMB", 25544, "98067A", 2570));
        Assert.Equal("meanElements", ex.ParamName);
    }

    [Fact]
    public void Create_MeanEquinoctialElementsInIcrf_Throws()
    {
        var keplerian = MeanElements(EarthAtEpoch, Frames.Frame.ICRF);
        var equinoctial = new EquinoctialElements(keplerian.SemiMajorAxis() * (1.0 - keplerian.Eccentricity() * keplerian.Eccentricity()), keplerian.Eccentricity() * System.Math.Cos(keplerian.ArgumentOfPeriapsis() + keplerian.AscendingNode()),
            keplerian.Eccentricity() * System.Math.Sin(keplerian.ArgumentOfPeriapsis() + keplerian.AscendingNode()), 0.1, 0.2, 1.0, keplerian.Observer, Epoch,
            Frames.Frame.ICRF, elementsType: OrbitalElementsType.Mean);
        var ex = Assert.Throws<ArgumentException>(() => Tle.Create(equinoctial, "ISS", 25544, "98067A", 2570));
        Assert.Equal("meanElements", ex.ParamName);
    }

    [Fact]
    public void Create_MeanElementsOfTheEarthInTeme_AreWritten()
    {
        // The frame compared by name (Frame equality), the centre by NAIF identifier: an Earth built in any frame
        var tle = Tle.Create(MeanElements(new CelestialBody(399), Frames.Frame.TEME), "ISS", 25544, "98067A", 2570);
        Assert.Equal(51.6423, double.Parse(tle.Line2.Substring(8, 8), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ToTle_OrbitAboutTheMoon_Throws()
    {
        // #607: the fit took the state about the Moon for one about the Earth (SGP4's centre) and wrote the TLE of an
        // Earth orbit through that position; a TLE describes an Earth orbit, so the conversion of an orbit about another
        // centre is refused before the fit. The state is a circular Earth orbit of 8000 km by its velocity
        // (√(GM⊕/r) = 7059 m/s), which the fit could reach, and a hyperbola about the Moon
        var moon = new CelestialBody(301, Frames.Frame.ECLIPTIC_J2000, Epoch);
        var state = new StateVector(new Vector3(8.0e6, 0.0, 0.0), new Vector3(0.0, 6113.0, 3529.0), moon, Epoch, Frames.Frame.ICRF);
        var ex = Assert.Throws<InvalidOperationException>(() => state.ToTle("LUNAR", 99001, "21001A"));
        Assert.Contains("MOON", ex.Message);
    }

    #endregion
}
