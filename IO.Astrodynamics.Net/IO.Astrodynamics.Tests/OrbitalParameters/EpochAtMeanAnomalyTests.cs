using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.EpochAtMeanAnomaly"/> on an ellipse (#474): the next
/// passage at the mean anomaly modulo 2π, whatever the turn of the given anomaly and of the anomaly at the epoch, from a
/// <see cref="StateVector"/> (whose mean anomaly is in (−π, π)), <see cref="KeplerianElements"/> and
/// <see cref="EquinoctialElements"/>, against the closed form ((M_t − M_0) mod 2π) / n at 60 digits
/// (<c>tools/epoch_at_mean_anomaly/epoch_at_mean_anomaly_reference.py</c>, mpmath 1.4.1 and spiceypy 8.3.0 on the test
/// kernels). About the Earth the elapsed time counts TT seconds, about the Sun TDB seconds (#501, #508).
/// </summary>
public class EpochAtMeanAnomalyTests
{
    private const double Au = 149597870700.0;

    // The comet of the issue: one ulp of its mean anomaly near 2π (8.9e-16 rad) is 4.5e-3 s of its mean motion (1.99e-13
    // rad/s). Provisional, twice the worst on Linux x64 (3.43e-3 s, 2026-11-11) rounded up, until the CI of the three
    // platforms has printed its MEASURE lines; the Earth orbits within half a tick (4.5e-8 s), twice that
    private const double CometTolerance = 7.0e-3;
    private const double EarthTolerance = 1.0e-7;

    // Near e = 1 the true anomaly at the sphere of influence (a = 5e7 m about the Moon) is 1.0e-3 rad from π, where one ulp
    // of cos ν = (p / r − 1) / e (1.1e-16, IEEE arithmetic) moves the time by 2.2e-5 s and one ulp of ν by 8.9e-8 s
    // (mpmath): −1.35e-5 s on Linux x64 (2026-11-11). Provisional, twice that rounded up, until the CI of the three
    // platforms has printed its MEASURE lines
    private const double MoonSoiTolerance = 3.0e-5;

    private readonly ITestOutputHelper _output;

    public EpochAtMeanAnomalyTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static readonly TimeSystem.Time Epoch = TimeSystem.Time.J2000TDB;

    private static KeplerianElements EarthOrbit(double meanAnomaly, double a = 1e7, double e = 0.3) =>
        new(a, e, 0.5, 0.3, 0.2, meanAnomaly, TestHelpers.EarthAtJ2000, Epoch, Frames.Frame.ICRF);

    private static double TtSeconds(TimeSystem.Time from, TimeSystem.Time to) => PropagationTimeArgument.Tt.SecondsBetween(from, to);

    private void AssertSeconds(double expected, double actual, double tolerance, string label)
    {
        _output.WriteLine($"MEASURE {label}: {actual - expected:E3} s");
        Assert.InRange(actual - expected, -tolerance, tolerance);
    }

    [Fact]
    public void FromAStateVector_TheNextPassage()
    {
        // M = 5 rad, held by the StateVector as 5 − 2π: 10743.97 s and 11056.37 s before #474, one period late
        var state = EarthOrbit(5.0).ToStateVector();
        Assert.True(state.MeanAnomaly() < 0.0);
        AssertSeconds(791.95612117941006437, TtSeconds(Epoch, state.EpochAtMeanAnomaly(5.5)), EarthTolerance, "state 5.5");
        var at300 = state.ToStateVector(300.0 * IO.Astrodynamics.Constants.Deg2Rad);
        AssertSeconds(1104.3510599100211468, TtSeconds(Epoch, at300.Epoch), EarthTolerance, "state nu 300 deg");
    }

    [Theory]
    [InlineData(-7.0, 7232.7303192310607183)]
    [InlineData(8.0, 1135.3715674609300271)]
    [InlineData(0.5, 9160.0580078714008097)]
    public void FromKeplerianElements_TargetOfAnyTurn(double target, double expected)
    {
        // −7 and 8 rad gave −2719.28 s and 11087.39 s before #474
        AssertSeconds(expected, TtSeconds(Epoch, EarthOrbit(1.0).EpochAtMeanAnomaly(target)), EarthTolerance, $"keplerian {target}");
    }

    [Theory]
    [InlineData(5.5, 791.95612117941006437)]
    [InlineData(-7.0, 897.08134979578020335)]
    [InlineData(8.0, 4751.7367270764603862)]
    public void FromEquinoctialElements_TargetOfAnyTurn(double target, double expected)
    {
        var equinoctial = EarthOrbit(5.0).ToEquinoctial();
        AssertSeconds(expected, TtSeconds(Epoch, equinoctial.EpochAtMeanAnomaly(target)), EarthTolerance, $"equinoctial {target}");
    }

    [Fact]
    public void FromStateVectorsAfterTheApoapsis_WithinOnePeriod()
    {
        // Mean anomalies after the apoapsis, the targets on both sides of the periapsis; the closed form in double, with
        // the mean anomaly the state was built from
        double worst = 0.0;
        foreach (double e in new[] { 0.001, 0.1, 0.3, 0.7, 0.9, 0.999 })
        {
            double a = 7e6 / (1.0 - e);
            foreach (double m0 in new[] { 3.5, 4.5, 5.5, 6.2 })
            {
                var state = EarthOrbit(m0, a, e).ToStateVector();
                double n = state.MeanMotion();
                double period = IO.Astrodynamics.Constants._2PI / n;
                for (double target = 0.05; target < IO.Astrodynamics.Constants._2PI; target += 0.3)
                {
                    double expected = IO.Astrodynamics.Math.SpecialFunctions.NormalizeAngle(target - m0) / n;
                    double actual = TtSeconds(Epoch, state.EpochAtMeanAnomaly(target));
                    Assert.InRange(actual, 0.0, period);
                    worst = System.Math.Max(worst, System.Math.Abs(actual - expected) / period);
                }
            }
        }

        _output.WriteLine($"MEASURE grid: worst |Δt − closed form| / T {worst:E3}");
        // 8.4e-12 on Linux x64 (2026-11-11), the mean anomaly of the state against the one it was built from
        Assert.True(worst < 2e-11, $"{worst:E3}");
    }

    [Fact]
    public void Comet_EccentricityKeptUnreduced()
    {
        // e = 0.999999, the one double at which KeplerianElements keeps M as given: before #474 its passage, beyond the epochs a Time holds,
        // threw (OverflowException in 10.x, ArgumentOutOfRangeException since #625)
        var comet = new KeplerianElements(1e4 * Au, 1.0 - 1e-6, 0.5, 0.3, 0.2, -0.02, TestHelpers.Sun, Epoch, Frames.Frame.ECLIPTIC_J2000);
        Assert.Equal(-0.02, comet.MeanAnomaly());
        var passage = comet.EpochAtMeanAnomaly(IO.Astrodynamics.Constants._2PI - 0.01);
        AssertSeconds(50226428909.255040238, (passage - Epoch).TotalSeconds, CometTolerance, "comet M -0.02");
    }

    [Fact]
    public void TimeToRadiusAndSphereOfInfluence_EccentricityKeptUnreduced()
    {
        // e = 0.999999 at M = 20 rad, kept unreduced: #474's example to r = a, −12194.735 s before #474; about the Moon to
        // its sphere of influence, TDB seconds
        var earthOrbit = new KeplerianElements(7e6, 1.0 - 1e-6, 0.5, 0.1, 0.2, 20.0, TestHelpers.EarthAtJ2000, Epoch, Frames.Frame.ICRF);
        Assert.Equal(20.0, earthOrbit.MeanAnomaly());
        AssertSeconds(5290.8147701649110473, TtSeconds(Epoch, earthOrbit.TimeToRadius(7e6)), EarthTolerance, "time to radius e 0.999999");

        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Epoch);
        Assert.InRange(moon.SphereOfInfluence - 66482232.21562789, -3e-8, 3e-8);
        var moonOrbit = new KeplerianElements(5e7, 1.0 - 1e-6, 0.5, 0.1, 0.2, 20.0, moon, Epoch, Frames.Frame.ICRF);
        AssertSeconds(973267.78183257802201, (moonOrbit.TimeToSphereOfInfluence() - Epoch).TotalSeconds, MoonSoiTolerance, "time to SOI e 0.999999");
    }

    [Fact]
    public void Comet_AfterThreeHundredYears()
    {
        // M = 2π − 0.001 rad moved by 300 Julian years past the perihelion, held unreduced (6.284 rad): −61.3 years before
        // #474 for a passage beyond TimeSpan, a past epoch
        var comet = new KeplerianElements(1e4 * Au, 1.0 - 1e-6, 0.5, 0.3, 0.2, IO.Astrodynamics.Constants._2PI - 0.001, TestHelpers.Sun, Epoch,
            Frames.Frame.ECLIPTIC_J2000);
        var later = comet.AtEpoch(Epoch.AddSeconds(9467280000.0));
        Assert.True(later.MeanAnomaly() > IO.Astrodynamics.Constants._2PI);
        var passage = later.EpochAtMeanAnomaly(0.002);
        AssertSeconds(5600648672.7794670488, (passage - later.Epoch).TotalSeconds, CometTolerance, "comet 300 yr");

        // 3.16e13 s, beyond the epochs a Time holds
        Assert.Throws<ArgumentOutOfRangeException>(() => later.EpochAtMeanAnomaly(0.0005));
    }
}
