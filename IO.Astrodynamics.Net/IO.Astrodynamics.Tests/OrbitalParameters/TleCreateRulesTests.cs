using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using Xunit;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using TleConfiguration = IO.Astrodynamics.OrbitalParameters.TLE.Configuration;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// What <see cref="TLE.Create"/> and <see cref="Astrodynamics.OrbitalParameters.OrbitalParameters.ToTLE"/> accept: SGP4
/// mean elements of an Earth orbit in TEME only (#607).
/// </summary>
public class TleCreateRulesTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 1, 20, 12, 50, 14, frame: TimeSystem.TimeFrame.UTCFrame);

    public TleCreateRulesTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static CelestialBody EarthAtEpoch => new(399, Frames.Frame.ECLIPTIC_J2000, Epoch);

    private static KeplerianElements MeanElements(ILocalizable observer, Frames.Frame frame) =>
        new(6_796_882.0, 0.0000493, 51.6423 * IO.Astrodynamics.Constants.Deg2Rad, 353.0312 * IO.Astrodynamics.Constants.Deg2Rad,
            320.8755 * IO.Astrodynamics.Constants.Deg2Rad, 39.2360 * IO.Astrodynamics.Constants.Deg2Rad, observer, Epoch, frame,
            elementsType: OrbitalElementsType.Mean);

    #region #607, SGP4 mean elements of an Earth orbit in TEME

    [Fact]
    public void Create_MeanElementsInIcrf_Throws()
    {
        // #607: mean elements in ICRF were written as a TLE, their angles taken as TEME's
        var ex = Assert.Throws<ArgumentException>(() =>
            TLE.Create(MeanElements(EarthAtEpoch, Frames.Frame.ICRF), "ISS", 25544, "98067A", 2570));
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
            TLE.Create(MeanElements(moon, Frames.Frame.TEME), "LUNAR", 25544, "98067A", 2570));
        Assert.Equal("meanElements", ex.ParamName);
        Assert.Contains("MOON", ex.Message);
    }

    [Fact]
    public void Create_MeanElementsAboutTheEarthMoonBarycenter_Throws()
    {
        var barycenter = new Barycenter(3, Epoch);
        var ex = Assert.Throws<ArgumentException>(() =>
            TLE.Create(MeanElements(barycenter, Frames.Frame.TEME), "EMB", 25544, "98067A", 2570));
        Assert.Equal("meanElements", ex.ParamName);
    }

    [Fact]
    public void Create_MeanEquinoctialElementsInIcrf_Throws()
    {
        var keplerian = MeanElements(EarthAtEpoch, Frames.Frame.ICRF);
        var equinoctial = new EquinoctialElements(keplerian.A * (1.0 - keplerian.E * keplerian.E), keplerian.E * System.Math.Cos(keplerian.AOP + keplerian.RAAN),
            keplerian.E * System.Math.Sin(keplerian.AOP + keplerian.RAAN), 0.1, 0.2, 1.0, keplerian.Observer, Epoch,
            Frames.Frame.ICRF, elementsType: OrbitalElementsType.Mean);
        var ex = Assert.Throws<ArgumentException>(() => TLE.Create(equinoctial, "ISS", 25544, "98067A", 2570));
        Assert.Equal("meanElements", ex.ParamName);
    }

    [Fact]
    public void Create_MeanElementsOfTheEarthInTeme_AreWritten()
    {
        // The frame compared by name (Frame equality), the centre by NAIF identifier: an Earth built in any frame
        var tle = TLE.Create(MeanElements(new CelestialBody(399), Frames.Frame.TEME), "ISS", 25544, "98067A", 2570);
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
        var ex = Assert.Throws<InvalidOperationException>(() => state.ToTLE(new TleConfiguration(99001, "LUNAR", "21001A")));
        Assert.Contains("MOON", ex.Message);
    }

    #endregion
}
