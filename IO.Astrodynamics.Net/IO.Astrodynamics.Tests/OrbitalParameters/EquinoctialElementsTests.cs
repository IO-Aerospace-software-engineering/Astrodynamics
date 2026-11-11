using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Math;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

public class EquinoctialElementsTests
{
    private readonly ITestOutputHelper _output;

    public EquinoctialElementsTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    /// <summary>
    /// The state of docs/tutorials/orbit-creation.md (#465), at its periapsis on the x axis in the equatorial plane: p = (r v)² / μ
    /// and f = e = p / r − 1, g = h = k = L = 0, with μ the kernel GM of the Earth, 398600435507022.6 m³/s²; the closed form
    /// at 40 digits (mpmath) is p = 7424377.236908115642 m, e = 0.09182018189825230031. Linux x64, 2026-11-11: P 9.3e-10 m
    /// (1.3e-16 relative), F 5.6e-17, the others 0. The bounds (1e-15, relative for P) are provisional, until the deltas this
    /// test prints in the CI logs of Linux, Windows and macOS arm64 set them to twice their worst.
    /// </summary>
    [Fact]
    public void TheStateOfTheTutorialGivesTheClosedForm()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        Assert.Equal(398600435507022.6, earth.GM);
        var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth, new TimeSystem.Time(2024, 6, 21, 12, 0, 0),
            Frames.Frame.ICRF);

        var equinoctial = sv.ToEquinoctial();

        _output.WriteLine($"P {equinoctial.P - 7424377.236908115642:E3} m, F {equinoctial.F - 0.09182018189825230031:E3}, G {equinoctial.G:E3}, H {equinoctial.H:E3}, K {equinoctial.K:E3}, L0 {equinoctial.L0:E3}");
        Assert.Equal(7424377.236908115642, equinoctial.P, 7424377.236908115642 * 1e-15);
        Assert.Equal(0.09182018189825230031, equinoctial.F, 1e-15);
        Assert.Equal(0.0, equinoctial.G, 1e-15);
        Assert.Equal(0.0, equinoctial.H, 1e-15);
        Assert.Equal(0.0, equinoctial.K, 1e-15);
        Assert.Equal(0.0, equinoctial.L0, 1e-15);
    }

    /// <summary>
    /// State S1 of #465 (r = (−4000, 5500, 2500) km, v = (−5.5, −3.0, 4.0) km/s, ICRF) against GMAT R2026a with Earth.Mu the kernel
    /// GM (#465, 2026-10-25): the modified set is P = SemilatusRectum, F = EquinoctialK, G = EquinoctialH, H = EquinoctialQ and
    /// K = EquinoctialP, the names of the tutorial and of docs/reference/keplerian-elements.md; the accessor methods return the
    /// properties. Linux x64, 2026-11-11: the components at most 1.9e-16 off, P 9.3e-10 m (GMAT prints P to 1e-9 m); the bounds
    /// (1e-15, 5e-9 m) are provisional, as above.
    /// </summary>
    [Fact]
    public void TheModifiedSetIsTheClassicalSetOfGmatUnderItsNames()
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var sv = new StateVector(new Vector3(-4000000.0, 5500000.0, 2500000.0), new Vector3(-5500.0, -3000.0, 4000.0), earth,
            new TimeSystem.Time(2024, 6, 21, 12, 0, 0), Frames.Frame.ICRF);

        var equinoctial = sv.ToEquinoctial();

        _output.WriteLine($"P {equinoctial.P - 6674290.249121238:E3} m, F {equinoctial.F - 0.25992015915397076:E3}, G {equinoctial.G - 0.12363511685104268:E3}, " +
                          $"H {equinoctial.H - -0.023979838219125259:E3}, K {equinoctial.K - 0.31440232331742002:E3}");
        Assert.Equal(6674290.249121238, equinoctial.P, 5e-9);
        Assert.Equal(0.25992015915397076, equinoctial.F, 1e-15);
        Assert.Equal(0.12363511685104268, equinoctial.G, 1e-15);
        Assert.Equal(-0.023979838219125259, equinoctial.H, 1e-15);
        Assert.Equal(0.31440232331742002, equinoctial.K, 1e-15);
        Assert.Equal(equinoctial.F, equinoctial.EquinoctialEx());
        Assert.Equal(equinoctial.G, equinoctial.EquinoctialEy());
        Assert.Equal(equinoctial.H, equinoctial.Hx());
        Assert.Equal(equinoctial.K, equinoctial.Hy());
        Assert.Equal(equinoctial.L0, equinoctial.Lv());
    }

    /// <summary>
    /// The singular orbit of the set (#465): near i = 180°, H = tan(i/2) cos Ω and K = tan(i/2) sin Ω grow as 2/δ. The orbit
    /// of #465 (a 7500 km, e 0.05, i = π − δ, Ω 1, ω 0.5, M 2) against the closed form at 50 digits (mpmath) evaluated at
    /// the double i, and the state of the elements against the state of the Keplerian elements. Linux x64, 2026-11-11: H and
    /// K at most 1.4e-16 relative, the state 1.9e-9 m and 1.3e-12 m/s; the bounds (1e-15 relative, 1e-8 m, 1e-11 m/s) are
    /// provisional, as above.
    /// </summary>
    [Theory]
    [InlineData(1e-3, 1080.604521685880298148829, 1682.941829370605789624414)]
    [InlineData(1e-6, 1080604.611452808770864849, 1682941.969174313611576429)]
    [InlineData(1e-9, 1080604389.990800568946039, 1682941624.26767132032076)]
    public void NearTheRetrogradeEquatorialOrbitHAndKGrowAsTheTangentOfHalfTheInclination(double delta, double expectedH, double expectedK)
    {
        var earth = PlanetsAndMoons.EARTH_BODY;
        var ke = new KeplerianElements(7500000.0, 0.05, System.Math.PI - delta, 1.0, 0.5, 2.0, earth, new TimeSystem.Time(2024, 6, 21, 12, 0, 0),
            Frames.Frame.ICRF);

        var equinoctial = ke.ToEquinoctial();
        var expected = ke.ToStateVector();
        var actual = equinoctial.ToStateVector();

        var dr = (actual.Position - expected.Position).Magnitude();
        var dv = (actual.Velocity - expected.Velocity).Magnitude();
        _output.WriteLine($"delta {delta:E0}: H {(equinoctial.H - expectedH) / expectedH:E3}, K {(equinoctial.K - expectedK) / expectedK:E3} relative, " +
                          $"state {dr:E3} m, {dv:E3} m/s");
        Assert.Equal(expectedH, equinoctial.H, System.Math.Abs(expectedH) * 1e-15);
        Assert.Equal(expectedK, equinoctial.K, System.Math.Abs(expectedK) * 1e-15);
        Assert.True(dr < 1e-8, $"position {dr} m");
        Assert.True(dv < 1e-11, $"velocity {dv} m/s");
    }

    [Fact]
    public void Create()
    {
        CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame);
        EquinoctialElements equ = new EquinoctialElements(1.0, 2.0, 3.0, 4.0, 5.0, 6.0, earth, epoch, Frames.Frame.ICRF);
        Assert.Equal(1.0, equ.P);
        Assert.Equal(2.0, equ.F);
        Assert.Equal(3.0, equ.G);
        Assert.Equal(4.0, equ.H);
        Assert.Equal(5.0, equ.K);
        Assert.Equal(6.0, equ.L0);
        Assert.Equal(earth, equ.Observer);
        Assert.Equal(epoch, equ.Epoch);
        Assert.Equal(Frames.Frame.ICRF, equ.Frame);
    }

    [Fact]
    public void ToEquinoctial()
    {
        CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
        KeplerianElements ke = new KeplerianElements(6800.81178582, 0.00134, 51.71 * IO.Astrodynamics.Constants.Deg2Rad, 32.57 * IO.Astrodynamics.Constants.Deg2Rad,
            105.64 * IO.Astrodynamics.Constants.Deg2Rad, 46.029 * IO.Astrodynamics.Constants.Deg2Rad, earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
            Frames.Frame.ICRF);
        EquinoctialElements equ = ke.ToEquinoctial();
        Assert.Equal(equ.SemiMajorAxis(), ke.A, 6);
        Assert.Equal(equ.Eccentricity(), ke.E, 6);
        Assert.Equal(equ.Inclination(), ke.I, 6);
        Assert.Equal(equ.AscendingNode() * IO.Astrodynamics.Constants.Rad2Deg, ke.RAAN * IO.Astrodynamics.Constants.Rad2Deg, 12);
        Assert.Equal(equ.ArgumentOfPeriapsis(), ke.AOP, 12);
        Assert.Equal(equ.MeanAnomaly(), ke.M, 1);
    }

    [Fact]
    public void Equality()
    {
        CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
        KeplerianElements ke = new KeplerianElements(6800.81178582, 0.00134, 51.71 * IO.Astrodynamics.Constants.Deg2Rad, 32.57 * IO.Astrodynamics.Constants.Deg2Rad,
            105.64 * IO.Astrodynamics.Constants.Deg2Rad, 46.029 * IO.Astrodynamics.Constants.Deg2Rad, earth, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame),
            Frames.Frame.ICRF);
        EquinoctialElements equ = ke.ToEquinoctial();
        EquinoctialElements equ2 = equ.ToEquinoctial();
        Assert.Equal(equ, equ2);
        Assert.True(equ == equ2);
        Assert.False(equ != equ2);
        Assert.False(equ == null);
        Assert.True(equ.Equals((object)equ2));
        Assert.True(equ.Equals((object)equ));
        Assert.False(equ.Equals((object)null));
        Assert.False(equ.Equals(null));
        Assert.True(equ.Equals(equ2));
    }
    
    [Fact]
    public void HyperbolicToKeplerian()
    {
        var ke = new KeplerianElements(-6800000.0, 1.2, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var eq = ke.ToEquinoctial();
        var ke2 = eq.ToKeplerianElements();
        var eq2 = ke2.ToEquinoctial();
        Assert.Equal(eq, eq2, TestHelpers.EquinoctialComparer);
    }

    [Fact]
    public void ParabolicToKeplerian()
    {
        var ke = new KeplerianElements(double.PositiveInfinity, 1, 0.2, 0.3, 0.4, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF,
            perigeeRadius: 6800000.0);
        var eq = ke.ToEquinoctial();
        var ke2 = eq.ToKeplerianElements();
        var eq2 = ke2.ToEquinoctial();
        Assert.Equal(eq, eq2, TestHelpers.EquinoctialComparer);
    }
        
    [Fact]
    public void EllipticToKeplerian()
    {
        var ke = new KeplerianElements(6800000.0, 0.2, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var eq = ke.ToEquinoctial();
        var ke2 = eq.ToKeplerianElements();
        var eq2 = ke2.ToEquinoctial();
        Assert.Equal(eq, eq2, TestHelpers.EquinoctialComparer);
    }
    
    [Fact]
    public void HyperbolicToStateVector()
    {
        var ke = new KeplerianElements(-6800000.0, 1.2, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var eq = ke.ToEquinoctial();
        var sv = eq.ToStateVector();
        var eq2 = sv.ToEquinoctial();
        Assert.Equal(eq, eq2, TestHelpers.EquinoctialComparer);
    }

    [Fact]
    public void ParabolicToStateVector()
    {
        var ke = new KeplerianElements(double.PositiveInfinity, 1, 0.2, 0.3, 0.4, 0.0, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF,
            perigeeRadius: 6800000.0);
        var eq = ke.ToEquinoctial();
        var sv = eq.ToStateVector();
        var eq2 = sv.ToEquinoctial();
        Assert.Equal(eq, eq2, TestHelpers.EquinoctialComparer);
    }
        
    [Fact]
    public void EllipticToStateVEctor()
    {
        var ke = new KeplerianElements(6800000.0, 0.2, 0.2, 0.3, 0.4, 0.5, TestHelpers.EarthAtJ2000, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var eq = ke.ToEquinoctial();
        var sv = eq.ToStateVector();
        var eq2 = sv.ToEquinoctial();
        Assert.Equal(eq, eq2, TestHelpers.EquinoctialComparer);
    }
}