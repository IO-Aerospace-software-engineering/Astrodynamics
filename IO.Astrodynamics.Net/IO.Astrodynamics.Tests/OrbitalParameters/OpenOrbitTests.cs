// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Threading.Tasks;
using IO.Astrodynamics.OrbitalParameters;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// Parabolic and hyperbolic orbits (#368): the mean anomaly is not an angle, negative before the periapsis, and the
/// parabolic anomaly follows Barker's equation, against SPICE OSCELT for the hyperbola and against the trigonometric
/// solution of Barker's equation for the parabola.
/// </summary>
public class OpenOrbitTests
{
    private const double PeriapsisRadius = 7000000.0;

    private readonly ITestOutputHelper _output;

    public OpenOrbitTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    public static TheoryData<double, double> HyperbolicCases()
    {
        var data = new TheoryData<double, double>();
        foreach (double e in new[] { 1.5, 3.0 })
        foreach (double m in new[] { -10.0, -2.0, -0.1, 0.0, 0.5, 3.0, 10.0 })
            data.Add(e, m);
        return data;
    }

    public static TheoryData<double> ParabolicCases => new() { -10.0, -2.0, -0.1, 0.0, 0.5, 3.0, 10.0 };

    [Theory]
    [MemberData(nameof(HyperbolicCases))]
    public void Hyperbola_StateAndMeanAnomalyMatchSpice(double e, double m)
    {
        // Arrange
        var elements = new KeplerianElements(PeriapsisRadius / (1.0 - e), e, 0.3, 0.2, 0.4, m, TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        const double dt = 3600.0;

        // Act
        var state = elements.ToStateVector();
        var spice = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);
        var later = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(
            elements.ToStateVector(TimeSystem.Time.J2000TDB.AddSeconds(dt)));
        double back = state.ToKeplerianElements().M;

        // Assert: the state has the given elements for SPICE, M included, and M grows by n Δt
        double error = System.Math.Abs(spice.M - m);
        double roundTrip = System.Math.Abs(back - m);
        double propagated = System.Math.Abs(later.M - (m + elements.MeanMotion() * dt));
        _output.WriteLine($"e = {e}, M = {m}: SPICE M {error:E2}, round trip {roundTrip:E2}, after 1 h {propagated:E2}");
        Assert.True(error < HyperbolicTolerance * (1.0 + System.Math.Abs(m)), $"SPICE M: {error:E2}");
        Assert.True(roundTrip < HyperbolicTolerance * (1.0 + System.Math.Abs(m)), $"round trip M: {roundTrip:E2}");
        Assert.True(propagated < HyperbolicTolerance * (1.0 + System.Math.Abs(m) + elements.MeanMotion() * dt),
            $"after 1 h: {propagated:E2}");
    }

    [Theory]
    [MemberData(nameof(ParabolicCases))]
    public void Parabola_FollowsBarkersEquation(double m)
    {
        // Arrange
        var elements = new KeplerianElements(double.PositiveInfinity, 1.0, 0.3, 0.2, 0.4, m, TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, perigeeRadius: PeriapsisRadius);
        const double dt = 3600.0;

        // Act
        var state = elements.ToStateVector();
        var later = elements.ToStateVector(TimeSystem.Time.J2000TDB.AddSeconds(dt));
        double back = state.ToKeplerianElements().M;

        // Assert: r = q (1 + D²) and ν = 2 atan D, with D from the trigonometric solution of Barker's equation, at M and
        // at M + n Δt
        double laterM = m + elements.MeanMotion() * dt;
        double radius = RelativeRadiusError(state, m);
        double anomaly = TestHelpers.AngleDifference(state.TrueAnomaly(), 2.0 * System.Math.Atan(Barker(m)));
        double laterRadius = RelativeRadiusError(later, laterM);
        double laterAnomaly = TestHelpers.AngleDifference(later.TrueAnomaly(), 2.0 * System.Math.Atan(Barker(laterM)));
        double roundTrip = System.Math.Abs(back - m);
        _output.WriteLine($"M = {m}: r {radius:E2}, ν {anomaly:E2}; after 1 h, r {laterRadius:E2}, ν {laterAnomaly:E2}; " +
                          $"round trip M {roundTrip:E2}");
        Assert.True(radius < ParabolicTolerance, $"r: {radius:E2}");
        Assert.True(anomaly < ParabolicTolerance, $"ν: {anomaly:E2}");
        Assert.True(laterRadius < ParabolicTolerance, $"r after 1 h: {laterRadius:E2}");
        Assert.True(laterAnomaly < ParabolicTolerance, $"ν after 1 h: {laterAnomaly:E2}");
        Assert.True(roundTrip < ParabolicTolerance * (1.0 + System.Math.Abs(m)), $"round trip M: {roundTrip:E2}");
    }

    [Theory]
    [MemberData(nameof(ParabolicCases))]
    public void Parabola_ElementsMatchSpice(double m)
    {
        // Arrange
        var elements = new KeplerianElements(double.PositiveInfinity, 1.0, 0.3, 0.2, 0.4, m, TestHelpers.EarthAtJ2000,
            TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, perigeeRadius: PeriapsisRadius);
        var state = elements.ToStateVector();

        // Act: before #373, the conversion threw, SPICE giving no semi-major axis to a parabola
        var spice = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);

        // Assert
        double q = System.Math.Abs(spice.PerigeeRadius() - PeriapsisRadius) / PeriapsisRadius;
        double e = System.Math.Abs(spice.E - 1.0);
        double i = System.Math.Abs(spice.I - 0.3);
        double node = TestHelpers.AngleDifference(spice.RAAN, 0.2);
        double periapsis = TestHelpers.AngleDifference(spice.AOP, 0.4);
        double meanAnomaly = System.Math.Abs(spice.M - m);
        double trueAnomaly = TestHelpers.AngleDifference(spice.TrueAnomaly(), 2.0 * System.Math.Atan(Barker(m)));
        var back = spice.ToStateVector();
        double position = (back.Position - state.Position).Magnitude() / state.Position.Magnitude();
        double velocity = (back.Velocity - state.Velocity).Magnitude() / state.Velocity.Magnitude();
        _output.WriteLine($"M = {m}: q {q:E2} (relative), e {e:E2}, i {i:E2}, Ω {node:E2}, ω {periapsis:E2}, M {meanAnomaly:E2}, " +
                          $"ν {trueAnomaly:E2}; back to the state, position {position:E2}, velocity {velocity:E2} (relative)");
        Assert.True(double.IsPositiveInfinity(spice.A));
        Assert.True(q < ParabolicTolerance, $"q: {q:E2}");
        Assert.True(e < ParabolicTolerance, $"e: {e:E2}");
        Assert.True(i < ParabolicTolerance, $"i: {i:E2}");
        Assert.True(node < ParabolicTolerance, $"Ω: {node:E2}");
        Assert.True(periapsis < ParabolicTolerance, $"ω: {periapsis:E2}");
        Assert.True(meanAnomaly < ParabolicTolerance * (1.0 + System.Math.Abs(m)), $"M: {meanAnomaly:E2}");
        Assert.True(trueAnomaly < ParabolicTolerance, $"ν: {trueAnomaly:E2}");
        Assert.True(position < ParabolicTolerance, $"position: {position:E2}");
        Assert.True(velocity < ParabolicTolerance, $"velocity: {velocity:E2}");
    }

    public static TheoryData<double, double> OpenCases()
    {
        var data = new TheoryData<double, double>();
        foreach (double e in new[] { 1.0, 1.5, 3.0 })
        foreach (double m in new[] { -10.0, -2.0, -0.1, 0.0, 0.5, 3.0, 10.0 })
            data.Add(e, m);
        return data;
    }

    [Theory]
    [MemberData(nameof(OpenCases))]
    public void OpenOrbit_SpiceStateAtTheEpochOfTheElements_HasTheElements(double e, double m)
    {
        // Arrange
        var elements = e == 1.0
            ? new KeplerianElements(double.PositiveInfinity, 1.0, 0.3, 0.2, 0.4, m, TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, perigeeRadius: PeriapsisRadius)
            : new KeplerianElements(PeriapsisRadius / (1.0 - e), e, 0.3, 0.2, 0.4, m, TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

        // Act: before #372, the native conversion never returned on a hyperbola before its periapsis (M = -2 and
        // -10 at e = 1.5). A regression fails this case after 10 s instead of hanging it, but the conversion that does
        // not return keeps the SPICE lock, so every later SPICE call of the run waits
        var conversion = Task.Run(() => SpiceAPI.Instance.ConvertConicElementsToStateVector(elements));
        Assert.True(conversion.Wait(TimeSpan.FromSeconds(10.0)), "the conversion did not return");
        var state = conversion.Result;
        var spice = SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state);

        // Assert: SPICE OSCELT finds the elements in the state, which is at their epoch
        double q = System.Math.Abs(spice.PerigeeRadius() - PeriapsisRadius) / PeriapsisRadius;
        double eccentricity = System.Math.Abs(spice.E - e);
        double i = System.Math.Abs(spice.I - 0.3);
        double node = TestHelpers.AngleDifference(spice.RAAN, 0.2);
        double periapsis = TestHelpers.AngleDifference(spice.AOP, 0.4);
        double meanAnomaly = System.Math.Abs(spice.M - m);
        _output.WriteLine($"e = {e}, M = {m}: q {q:E2} (relative), e {eccentricity:E2}, i {i:E2}, Ω {node:E2}, " +
                          $"ω {periapsis:E2}, M {meanAnomaly:E2}");
        Assert.Equal(elements.Epoch, state.Epoch);
        Assert.True(q < OpenOrbitSpiceTolerance, $"q: {q:E2}");
        Assert.True(eccentricity < OpenOrbitSpiceTolerance, $"e: {eccentricity:E2}");
        Assert.True(i < OpenOrbitSpiceTolerance, $"i: {i:E2}");
        Assert.True(node < OpenOrbitSpiceTolerance, $"Ω: {node:E2}");
        Assert.True(periapsis < OpenOrbitSpiceTolerance, $"ω: {periapsis:E2}");
        Assert.True(meanAnomaly < OpenOrbitSpiceTolerance * (1.0 + System.Math.Abs(m)), $"M: {meanAnomaly:E2}");
    }

    // Measured (Linux, .NET 10, 2026-10-11): the parabola within 3.6e-15 of Barker's trigonometric solution and, on
    // 2026-10-12, its elements within 1.5e-15 of SPICE (M relative to 1 + |M|), its state back within 8e-16; the
    // hyperbola within 2e-13 of SPICE relative to 1 + |M|, the convergence of the hyperbolic Kepler solver (steps
    // below 1e-6, so a residual of order 1e-12)
    private const double ParabolicTolerance = 1e-14;
    private const double HyperbolicTolerance = 1e-12;

    // Measured (Linux, .NET 10, 2026-10-12): SPICE CONICS then OSCELT, 2.3e-14 at most (q, then e at 1.2e-14, at e = 1.5
    // and M = ±10, where r is about 18 q and the elements are less well conditioned in the state), 6e-15 elsewhere
    private const double OpenOrbitSpiceTolerance = 1e-13;

    // The parabolic anomaly D of Barker's equation M = D + D³/3 by its trigonometric solution: cot 2s = 3M/2,
    // tan³ w = tan s, D = 2 cot 2w (D. A. Vallado, Fundamentals of Astrodynamics and Applications, 4th edition, 2013,
    // algorithm 3; to be verified by S. Guillet). Independent of the closed form 2 sinh(asinh(3M/2)/3) of the library.
    private static double Barker(double m)
    {
        double s = 0.5 * System.Math.Atan2(1.0, 1.5 * m);
        double w = System.Math.Atan(System.Math.Cbrt(System.Math.Tan(s)));
        return 2.0 / System.Math.Tan(2.0 * w);
    }

    private static double RelativeRadiusError(StateVector state, double m)
    {
        double d = Barker(m);
        double expected = PeriapsisRadius * (1.0 + d * d);
        return System.Math.Abs(state.Position.Magnitude() - expected) / expected;
    }
}
