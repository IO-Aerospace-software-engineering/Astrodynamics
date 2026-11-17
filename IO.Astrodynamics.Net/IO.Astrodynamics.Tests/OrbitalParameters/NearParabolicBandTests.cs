// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The near-parabolic band, |e − 1| &lt; 1e-6, where the library takes the anomalies of a parabola (Barker's equation,
/// #368): the semi-latus rectum of <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToEquinoctial"/>
/// (#462), the mean anomaly of a true anomaly (#478), parabolic equinoctial elements without a perigee radius (#477) and
/// the conic's semi-major axis and mean motion with Barker's motion (#459). The references are closed forms at 60
/// digits on the doubles given to the library (<c>tools/near_parabolic/near_parabolic_reference.py</c>, mpmath 1.4.1
/// and spiceypy 8.4.0 on gm_de440.tpc), a value as a pair (hi, lo) of doubles whose sum is the 60-digit value.
/// </summary>
public class NearParabolicBandTests
{
    // An epoch tagged TT: about the Earth the two-body motion counts TT seconds (#501, #508), so +600 s here is 600 s of
    // the motion, the time of the references
    private static readonly TimeSystem.Time Epoch = new(new DateTime(2026, 1, 20, 6, 0, 0), TimeFrame.TDTFrame);
    private const double Mu = 398600435507022.6;

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;

    public NearParabolicBandTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
        Assert.Equal(Mu, _earth.GM);
    }

    private void Measure(string what, double value) => _output.WriteLine($"MEASURE {what}: {value:E3}");

    private StateVector State(double[] r, double[] v) =>
        new(new Vector3(r[0], r[1], r[2]), new Vector3(v[0], v[1], v[2]), _earth, Epoch, Frames.Frame.ICRF);

    // h²/μ of a state, from its components: the closed form, not the library's conversions
    private static double SemiLatusRectumOf(StateVector sv)
    {
        var r = sv.Position;
        var v = sv.Velocity;
        double hx = r.Y * v.Z - r.Z * v.Y;
        double hy = r.Z * v.X - r.X * v.Z;
        double hz = r.X * v.Y - r.Y * v.X;
        return (hx * hx + hy * hy + hz * hz) / Mu;
    }

    #region #462: the semi-latus rectum of ToEquinoctial

    [Theory]
    [InlineData(1.0, 13600000.0, 0.0)]
    [InlineData(0.9999995, 13599996.6, 6.522355988636264e-10)]
    [InlineData(1.0000005, 13600003.4, 1.0271605788148008e-10)]
    public void Issue462_KeplerianElementsOfTheBand_HaveTheConicsSemiLatusRectum(double e, double hi, double lo)
    {
        // Arrange: the inputs of KeplerianElementsTests.ParabolicToEquinoctial, q = 6800 km
        var ke = new KeplerianElements(double.PositiveInfinity, e, 0.2, 0.3, 0.4, 0.0, _earth, Epoch, Frames.Frame.ICRF,
            perigeeRadius: 6800000.0);

        // Act
        var eq = ke.ToEquinoctial();

        // Assert: p = q (1 + e), the conic of the elements with its periapsis at q, 2q at e = 1 (before: NaN at e = 1, ±∞
        // in the band, of a sign set by rounding)
        double error = (eq.P - hi - lo) / hi;
        Measure($"#462 Keplerian e = {e:R}, P relative error", error);
        Assert.True(double.IsFinite(eq.P));
        if (e == 1.0)
        {
            Assert.Equal(13600000.0, eq.P);
        }

        // Measured at most 4.8e-17 relative on Linux (the rounding of q (1 + e)); provisional, twice, until the Windows x64
        // and macOS arm64 CI logs
        Assert.InRange(error, -9.6e-17, 9.6e-17);
        Assert.DoesNotContain("NaN", eq.ToString());
        Assert.DoesNotContain("Infinity", eq.ToString());
    }

    public static TheoryData<double, double, double> PeriapsisStates => new()
    {
        // vy (m/s), h²/μ at 60 digits (hi, lo): e − 1 = −5e-7, −7.9e-9 and +5e-7 at r = (7000 km, 0, 0)
        { 10671.729487052813, 13999996.5, -3.733329786349084e-10 },
        { 10671.7308, 13999999.944850652, 6.386977430653186e-10 },
        { 10671.732154985518, 14000003.5, 6.967542062940009e-10 },
    };

    [Theory]
    [MemberData(nameof(PeriapsisStates))]
    public void Issue462_StatesOfTheBand_HaveTheirSemiLatusRectum(double vy, double hi, double lo)
    {
        // Arrange
        var sv = State([7000000.0, 0.0, 0.0], [0.0, vy, 0.0]);
        Assert.True(sv.IsParabolic());

        // Act: the paths of the issue, each a P of the same orbit
        var direct = sv.ToEquinoctial().P;
        var throughKeplerian = sv.ToKeplerianElements().ToEquinoctial().P;
        var later = sv.AtEpoch(Epoch.AddSeconds(3600.0)).ToEquinoctial().P;
        var ecliptic = sv.ToFrame(Frames.Frame.ECLIPTIC_J2000).ToEquinoctial().P;

        // Assert: h²/μ (before: +∞ below e = 1, −∞ above)
        foreach (var (path, p) in new[] { ("direct", direct), ("Keplerian", throughKeplerian), ("+1 h", later), ("ecliptic", ecliptic) })
        {
            double error = (p - hi - lo) / hi;
            Measure($"#462 vy = {vy:R} {path}, P relative error", error);
            Assert.True(double.IsFinite(p));
            // Measured at most 4.9e-16 relative on Linux (the rounding of e, q and the rotation to the ecliptic);
            // provisional, twice, until the Windows x64 and macOS arm64 CI logs
            Assert.InRange(error, -9.7e-16, 9.7e-16);
        }
    }

    [Fact]
    public void Issue462_TheStateOfAnExactParabola_HasItsSemiLatusRectumInAnyFrame()
    {
        // Arrange: the input of StateVectorTests.ParabolicToEquinoctial, whose e is off 1 by its rounding
        var ke = new KeplerianElements(double.PositiveInfinity, 1.0, 0.2, 0.3, 0.4, 0.0, _earth, Epoch, Frames.Frame.ICRF,
            perigeeRadius: 6800000.0);
        var sv = ke.ToStateVector();
        var ecliptic = sv.ToFrame(Frames.Frame.ECLIPTIC_J2000).ToStateVector();

        foreach (var (name, state) in new[] { ("ICRF", sv), ("ECLIPTIC_J2000", ecliptic) })
        {
            // Act
            double p = state.ToEquinoctial().P;

            // Assert: h²/μ of the state, and 2q of the elements within the rounding of the state
            Measure($"#462 exact parabola in {name}, P − h²/μ relative", (p - SemiLatusRectumOf(state)) / p);
            Measure($"#462 exact parabola in {name}, P − 2q relative", (p - 13600000.0) / 13600000.0);
            Assert.True(double.IsFinite(p));
            // Measured at most 2.8e-16 relative to h²/μ and 5.5e-16 to 2q on Linux; provisional, twice, until the Windows
            // x64 and macOS arm64 CI logs
            Assert.InRange((p - SemiLatusRectumOf(state)) / p, -5.5e-16, 5.5e-16);
            Assert.InRange((p - 13600000.0) / 13600000.0, -1.1e-15, 1.1e-15);
        }
    }

    [Fact]
    public void Issue462_ExactParabolas_HaveAFinitePositiveSemiLatusRectum()
    {
        // Arrange: exact parabolas built in doubles, p = 2q, i = 0.7 rad, ν from −170° to 170° every 0.5°
        double worst = 0.0;
        int count = 0;
        foreach (double q in new[] { 6600e3, 8000e3, 42164e3, 1e9 })
        {
            for (int step = -340; step <= 340; step++)
            {
                double nu = step * 0.5 * IO.Astrodynamics.Constants.Deg2Rad;
                double p = 2.0 * q;
                double r = p / (1.0 + System.Math.Cos(nu));
                double w = System.Math.Sqrt(Mu / p);
                double ci = System.Math.Cos(0.7), si = System.Math.Sin(0.7);
                var sv = State([r * System.Math.Cos(nu), r * System.Math.Sin(nu) * ci, r * System.Math.Sin(nu) * si],
                    [-w * System.Math.Sin(nu), w * (1.0 + System.Math.Cos(nu)) * ci, w * (1.0 + System.Math.Cos(nu)) * si]);

                // Act
                double actual = sv.ToEquinoctial().P;

                // Assert: finite, positive, h²/μ of the state (before: NaN, +∞ or −∞ on every one)
                Assert.True(double.IsFinite(actual) && actual > 0.0, $"q = {q}, ν = {nu}: P = {actual}");
                worst = System.Math.Max(worst, System.Math.Abs(actual - SemiLatusRectumOf(sv)) / actual);
                count++;
            }
        }

        Measure($"#462 {count} exact parabolas, worst |P − h²/μ| relative", worst);
        // Measured 2.9e-16 relative on Linux; provisional, twice, until the Windows x64 and macOS arm64 CI logs
        Assert.InRange(worst, 0.0, 5.7e-16);
    }

    [Fact]
    public void Issue462_OutsideTheBand_PIsUnchanged()
    {
        // a (1 − e²) bit for bit outside the band, an ellipse and a hyperbola
        foreach (var (a, e) in new[] { (6800000.0, 0.2), (-7954545.45, 1.88), (7e12, 0.999998) })
        {
            var ke = new KeplerianElements(a, e, 0.2, 0.3, 0.4, 0.5, _earth, Epoch, Frames.Frame.ICRF);
            Assert.Equal(a * (1 - e * e), ke.ToEquinoctial().P);
        }
    }

    #endregion
}
