// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The precision of <see cref="CelestialItem.ShadowFraction(double, double, double)"/> in the penumbra (#434): against
/// the exact shadow fraction of the same inputs at 60 digits, and its range along the shadow crossings of R3 and of a
/// GEO.
/// </summary>
/// <remarks>
/// <para>
/// The reference, <c>Data/ShadowFunctionReference/shadow_fraction_reference.json</c>, comes from
/// <c>tools/shadow_function/shadow_fraction_reference.py</c>: the area of the two circular segments cut by the common
/// chord (E. W. Weisstein, "Circle-Circle Intersection", MathWorld, eqs. 10 to 13, read on 2026-10-22) evaluated with
/// mpmath at 60 digits, for the Sun seen from 1 au and the Earth from 400 km, 700 km and GEO, and for a Moon slightly
/// smaller and slightly larger than the Sun, at depths of 1e-12 to 1e-2 of the width of the penumbra inside each contact
/// and at fractions of that width across it. The inputs are doubles, so the reference is exact for them and measures the
/// computation alone, not the geometry.
/// </para>
/// <para>
/// The range is measured on the geometry of <c>SolarRadiationPressure</c> (geometric positions, the angular radii
/// asin(R / d) and the angular separation of the two directions), on states inside the penumbra by the angles of the
/// discs, never by the computed shadow fraction.
/// </para>
/// </remarks>
public class ShadowFractionPrecisionTests : IClassFixture<ReferenceCases>, IClassFixture<PartialsTestCases>
{
    private const string ReferenceFile = "Data/ShadowFunctionReference/shadow_fraction_reference.json";

    private readonly ReferenceCases _cases;
    private readonly PartialsTestCases _partials;
    private readonly ITestOutputHelper _output;

    public ShadowFractionPrecisionTests(ReferenceCases cases, PartialsTestCases partials, ITestOutputHelper output)
    {
        _cases = cases;
        _partials = partials;
        _output = output;
    }

    private sealed record ReferencePoint(string Geometry, string Where, double A, double B, double C,
        double ShadowFraction);

    private static IReadOnlyList<ReferencePoint> ReferencePoints()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ReferenceFile));
        return document.RootElement.GetProperty("points").EnumerateArray().Select(p => new ReferencePoint(
            p.GetProperty("geometry").GetString(), p.GetProperty("where").GetString(), p.GetProperty("a").GetDouble(),
            p.GetProperty("b").GetDouble(), p.GetProperty("c").GetDouble(),
            double.Parse(p.GetProperty("shadow_fraction").GetString()!, CultureInfo.InvariantCulture))).ToList();
    }

    [Fact]
    public void InThePenumbra_AgainstTheExactShadowFractionAt60Digits()
    {
        // Arrange
        var points = ReferencePoints();

        // Act
        var errors = points.Select(p => (Point: p,
            Value: CelestialItem.ShadowFraction(p.C, 2.0 * p.A, 2.0 * p.B))).ToList();

        // Assert: every input is inside the penumbra; the error published per geometry, across the penumbra, near each
        // contact and where the computation changes form
        Assert.Equal(144, points.Count);
        Assert.All(points, p => Assert.True(p.C > System.Math.Abs(p.B - p.A) && p.C < p.A + p.B, p.Where));
        foreach (var geometry in errors.GroupBy(e => e.Point.Geometry))
        {
            double Worst(Func<string, bool> where) => geometry.Where(e => where(e.Point.Where)).DefaultIfEmpty()
                .Max(e => double.IsNaN(e.Value) ? double.PositiveInfinity : System.Math.Abs(e.Value - e.Point.ShadowFraction));
            int outside = geometry.Count(e => !(e.Value >= 0.0 && e.Value <= 1.0));
            _output.WriteLine($"MEASURE #434 {geometry.Key}: error of ν across the penumbra " +
                              $"{Worst(w => w.EndsWith("of the width")):E2}; at depths of 1e-2, 1e-4, 1e-6, 1e-8 and " +
                              $"1e-10 to 1e-12 of the width inside the contacts {Worst(w => w.Contains("1e-02")):E2}, " +
                              $"{Worst(w => w.Contains("1e-04")):E2}, {Worst(w => w.Contains("1e-06")):E2}, " +
                              $"{Worst(w => w.Contains("1e-08")):E2}, " +
                              $"{Worst(w => w.Contains("1e-10") || w.Contains("1e-12")):E2}; where the form changes " +
                              $"{Worst(w => w.StartsWith("singular")):E2}; {outside} of {geometry.Count()} outside " +
                              "[0, 1] or NaN");
        }

        // Near the outer contact ν is small: its error relative to ν
        var small = errors.Where(e => e.Point.ShadowFraction > 0.0 && e.Point.ShadowFraction < 1e-6).ToList();
        double relative = small.Max(e => System.Math.Abs(e.Value - e.Point.ShadowFraction) / e.Point.ShadowFraction);
        double absolute = errors.Max(e => System.Math.Abs(e.Value - e.Point.ShadowFraction));
        _output.WriteLine($"MEASURE #434 worst error of ν {absolute:E2}; relative to ν below 1e-6 ({small.Count} " +
                          $"points) {relative:E2}");

        // .NET 10 (CI of 3340116, 2026-10-22): 2.2e-16 absolute (1 ulp of 1), 8.7e-16 relative to ν below 1e-6, the same
        // on Linux, Windows and macOS arm64 (on main 1.7e-4, and relative errors far above 1, the closed form
        // cancelling). Bounds at twice the worst of the three platforms (lesson 13 of #364)
        Assert.All(errors, e => Assert.InRange(e.Value, 0.0, 1.0));
        Assert.True(absolute < 4.5e-16, $"worst error of ν {absolute:E2}");
        Assert.True(relative < 1.8e-15, $"worst error relative to ν below 1e-6 {relative:E2}");
    }

    [Fact]
    public void AlongTheShadowCrossingsOfR3_TheRangeOfTheShadowFraction()
    {
        // Arrange: the first three hours of R3 (Sun-synchronous 700 km, ascending node at noon), two shadow crossings
        // per revolution
        var r3 = _cases.R3;
        var start = r3.InitialState;
        var solution = new CentralBodyPropagator(new TimeSystem.Window(start.Epoch, start.Epoch.AddHours(3.0)),
            ReferenceCase.NewSpacecraft(-1434, start), new RK78Integrator(ReferenceCases.Tolerance,
                ReferenceCases.Tolerance), r3.Bodies, false, true, TimeSpan.FromSeconds(600.0)).Propagate();
        var earth = (CelestialBody)start.Observer;
        var sun = r3.Bodies.OfType<CelestialBody>().First(body => body.NaifId == 10);

        StateVector At(double t)
        {
            var epoch = start.Epoch.AddSeconds(t);
            var (position, velocity) = solution.InterpolateAt(epoch);
            return new StateVector(position, velocity, start.Observer, epoch, start.Frame);
        }

        double Outer(double t) => Discs(sun, earth, At(t)) is var (a, b, c) ? c - (a + b) : 0.0;
        double Inner(double t) => Discs(sun, earth, At(t)) is var (a, b, c) ? c - (b - a) : 0.0;
        var contacts = new List<double>();
        double previousOuter = Outer(0.0), previousInner = Inner(0.0);
        for (double t = 2.0; t <= 3.0 * 3600.0; t += 2.0)
        {
            double outer = Outer(t), inner = Inner(t);
            if (System.Math.Sign(outer) != System.Math.Sign(previousOuter))
            {
                contacts.Add(Root(Outer, t - 2.0, t));
            }

            if (System.Math.Sign(inner) != System.Math.Sign(previousInner))
            {
                contacts.Add(Root(Inner, t - 2.0, t));
            }

            previousOuter = outer;
            previousInner = inner;
        }

        // Act: 1 µs apart (7.5 mm) over the 2 ms inside each contact, the side of the penumbra by the geometry
        var (count, below, above, nan) = (0, 0, 0, 0);
        foreach (double contact in contacts)
        {
            double inside = Outer(contact - 1e-3) < 0.0 && Inner(contact - 1e-3) > 0.0 ? -1.0 : 1.0;
            for (int k = 1; k <= 2000; k++)
            {
                var (a, b, c) = Discs(sun, earth, At(contact + inside * k * 1e-6));
                if (c >= a + b || c <= b - a)
                {
                    continue;
                }

                double nu = CelestialItem.ShadowFraction(c, 2.0 * a, 2.0 * b);
                count++;
                below += nu < 0.0 ? 1 : 0;
                above += nu > 1.0 ? 1 : 0;
                nan += double.IsNaN(nu) ? 1 : 0;
            }
        }

        // Assert: every state of the penumbra in [0, 1] (on main before #434, 340 to 355 of them outside on the three
        // platforms)
        Assert.Equal(8, contacts.Count);
        Assert.True(count > 15000, $"{count} states in the penumbra");
        _output.WriteLine($"MEASURE #434 R3, {contacts.Count} contacts, {count} states in the penumbra within 2 ms of " +
                          $"them: ν below 0 {below}, above 1 {above}, NaN {nan}");
        Assert.Equal((0, 0, 0), (below, above, nan));
    }

    [Fact]
    public void AcrossThePenumbraOfAGeo_TheRangeOfTheShadowFraction()
    {
        // Arrange: the states of the force partials in GEO, on the circle of the plane of the Sun and of the pole
        const string orbit = PartialsTestCases.Geo;
        var (innerAngle, outerAngle) = PenumbraStates.ContactAngles(_partials, orbit);
        double radius = PartialsTestCases.State(orbit, _partials.Earth).Position.Magnitude();

        // Act: 1 mm apart over the 20 m inside each contact
        var (count, below, above, nan) = (0, 0, 0, 0);
        foreach (var (contact, inside) in new[] { (innerAngle, 1.0), (outerAngle, -1.0) })
        {
            for (int k = 1; k <= 20000; k++)
            {
                var (a, b, c) = Discs(_partials.Sun, _partials.Earth,
                    PenumbraStates.At(_partials, orbit, contact + inside * k * 1e-3 / radius));
                if (c >= a + b || c <= b - a)
                {
                    continue;
                }

                double nu = CelestialItem.ShadowFraction(c, 2.0 * a, 2.0 * b);
                count++;
                below += nu < 0.0 ? 1 : 0;
                above += nu > 1.0 ? 1 : 0;
                nan += double.IsNaN(nu) ? 1 : 0;
            }
        }

        // Assert: every state of the penumbra in [0, 1] (on main before #434, 172 to 174 of them outside)
        Assert.True(count > 39000, $"{count} states in the penumbra");
        _output.WriteLine($"MEASURE #434 GEO, {count} states in the penumbra within 20 m of the contacts: ν below 0 " +
                          $"{below}, above 1 {above}, NaN {nan}");
        Assert.Equal((0, 0, 0), (below, above, nan));
    }

    public static TheoryData<double, double> Radii => new()
    {
        { 4.65e-3, 1.12 }, // the Sun and the Earth from a LEO, a < b
        { 4.65e-3, 4.3e-3 }, // the Sun and a smaller Moon, a > b
        { 4.65e-3, 4.65e-3 } // equal discs
    };

    [Theory]
    [MemberData(nameof(Radii))]
    public void OutsideThePenumbra_TheRegimesGiveTheirExactValues(double a, double b)
    {
        // Arrange: the separations at and beyond each contact, the regimes of before #434, bit for bit
        double outer = a + b;
        double inner = System.Math.Abs(b - a);

        // Act and assert: no overlap at and beyond the outer contact, total or annular at and within the inner one
        Assert.Equal(0.0, CelestialItem.ShadowFraction(outer, 2.0 * a, 2.0 * b));
        Assert.Equal(0.0, CelestialItem.ShadowFraction(2.0 * outer, 2.0 * a, 2.0 * b));
        double inside = b >= a ? 1.0 : b * b / (a * a);
        Assert.Equal(inside, CelestialItem.ShadowFraction(inner, 2.0 * a, 2.0 * b));
        Assert.Equal(inside, CelestialItem.ShadowFraction(0.5 * inner, 2.0 * a, 2.0 * b));
        Assert.Equal(inside, CelestialItem.ShadowFraction(0.0, 2.0 * a, 2.0 * b));

        // And just inside the penumbra, in (0, 1) and next to the value of the regime it leaves
        double nearOuter = CelestialItem.ShadowFraction(outer * (1.0 - 1e-12), 2.0 * a, 2.0 * b);
        double nearInner = CelestialItem.ShadowFraction(inner + 1e-12 * outer, 2.0 * a, 2.0 * b);
        Assert.InRange(nearOuter, 0.0, 1e-12);
        Assert.InRange(nearInner, inside - 1e-5, System.Math.Min(inside, 1.0));
    }

    [Theory]
    [InlineData(Aberration.None)]
    [InlineData(Aberration.LT)]
    public void TheInstanceOverload_IsTheStaticOneOnTheAnglesOfItsAberration(Aberration aberration)
    {
        // Arrange: a GEO state at half shadow of the Earth, and one in the penumbra of the Moon 400 000 km beyond it
        var geo = PenumbraStates.At(_partials, PartialsTestCases.Geo,
            PenumbraStates.AngleOfShadowFraction(_partials, PartialsTestCases.Geo, 0.5));
        var behindTheMoon = BehindTheMoon(1800e3);

        foreach (var (state, body) in new[] { (geo, _partials.Earth), (behindTheMoon, _partials.Moon) })
        {
            // Act
            double nu = _partials.Sun.ShadowFraction(body, state, aberration);

            // Assert: the static overload on the angular sizes and separation of the same aberration, in the penumbra
            var toSun = _partials.Sun.GetEphemeris(state.Epoch, state.Observer, state.Frame, aberration)
                .ToStateVector().Position - state.Position;
            var toBody = body.GetEphemeris(state.Epoch, state.Observer, state.Frame, aberration).ToStateVector()
                .Position - state.Position;
            double expected = CelestialItem.ShadowFraction(_partials.Sun.AngularSeparation(state.Epoch, body, state,
                aberration), _partials.Sun.AngularSize(toSun.Magnitude()), body.AngularSize(toBody.Magnitude()));
            Assert.Equal(expected, nu);
            Assert.InRange(nu, 1e-3, 1.0 - 1e-3);
        }
    }

    [Fact]
    public void Srp_WithTheEarthAndTheMoon_TakesTheLargestShadowFraction()
    {
        // Arrange: a state in the penumbra of the Moon, where the Earth casts no shadow, and one at half shadow of the
        // Earth in GEO, where the Moon casts none
        var behindTheMoon = BehindTheMoon(1800e3);
        var geo = PenumbraStates.At(_partials, PartialsTestCases.Geo,
            PenumbraStates.AngleOfShadowFraction(_partials, PartialsTestCases.Geo, 0.5));

        foreach (var (state, shadowing) in new[] { (behindTheMoon, _partials.Moon), (geo, _partials.Earth) })
        {
            var spacecraft = PartialsTestCases.Spacecraft(state);
            var both = new SolarRadiationPressure(spacecraft, new[] { _partials.Earth, _partials.Moon });
            var alone = new SolarRadiationPressure(spacecraft, new[] { shadowing });
            var unshadowed = new SolarRadiationPressure(spacecraft, Array.Empty<CelestialBody>());

            // Act
            var acceleration = both.Apply(state);

            // Assert: the shadow of the body that casts one, bit for bit, the lit acceleration times 1 − ν
            Assert.Equal(alone.Apply(state), acceleration);
            double nu = _partials.Sun.ShadowFraction(shadowing, state, Aberration.None);
            Assert.InRange(nu, 1e-3, 1.0 - 1e-3);
            double relative = (acceleration - unshadowed.Apply(state) * (1.0 - nu)).Magnitude() /
                              acceleration.Magnitude();
            Assert.True(relative < 1e-14, $"relative difference {relative:E2}");
        }
    }

    [Fact]
    public void InThePenumbra_ACallAllocatesNothing()
    {
        // Arrange: warmed up, in the penumbra of a LEO
        var point = ReferencePoints().First(p => p.Where == "0.5 of the width");
        double sum = CelestialItem.ShadowFraction(point.C, 2.0 * point.A, 2.0 * point.B);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 1000; k++)
        {
            sum += CelestialItem.ShadowFraction(point.C * (1.0 + 1e-6 * k), 2.0 * point.A, 2.0 * point.B);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.True(sum > 0.0);
        Assert.Equal(0, allocated);
    }

    // A spacecraft 400 000 km beyond the Moon on the axis from the Sun, offset across it: the angular radius of the Moon,
    // 4.3e-3 rad, is below that of the Sun, so the shadow is annular near the axis and partial off it
    private StateVector BehindTheMoon(double offset)
    {
        var moonPosition = _partials.Moon.GetEphemeris(PartialsTestCases.Epoch, _partials.Earth, Frames.Frame.ICRF,
            Aberration.None).ToStateVector().Position;
        var sunPosition = _partials.Sun.GetEphemeris(PartialsTestCases.Epoch, _partials.Earth, Frames.Frame.ICRF,
            Aberration.None).ToStateVector().Position;
        var axis = (moonPosition - sunPosition).Normalize();
        var across = axis.Cross(Vector3.VectorZ).Normalize();
        return new StateVector(moonPosition + axis * 4e8 + across * offset, Vector3.VectorZ * 1000.0, _partials.Earth,
            PartialsTestCases.Epoch, Frames.Frame.ICRF);
    }

    // The angular radii of the Sun and of the occulting body and their angular separation, as SolarRadiationPressure
    private static (double SunRadius, double BodyRadius, double Separation) Discs(CelestialBody sun, CelestialBody body,
        StateVector state)
    {
        var position = state.ToStateVector().Position;
        Vector3 Geometric(CelestialItem item) => item.Equals(state.Observer)
            ? Vector3.Zero
            : item.GetEphemeris(state.Epoch, state.Observer, state.Frame, Aberration.None).ToStateVector().Position;
        var toSun = Geometric(sun) - position;
        var toBody = Geometric(body) - position;
        return (0.5 * sun.AngularSize(toSun.Magnitude()), 0.5 * body.AngularSize(toBody.Magnitude()),
            toSun.Normalize().Angle(toBody.Normalize()));
    }

    // The root of f in [low, high], by bisection
    private static double Root(Func<double, double> f, double low, double high)
    {
        double atLow = f(low);
        for (int k = 0; k < 200 && high - low > 1e-10; k++)
        {
            double middle = 0.5 * (low + high);
            if (System.Math.Sign(f(middle)) == System.Math.Sign(atLow))
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return 0.5 * (low + high);
    }
}
