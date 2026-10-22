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

        // Assert: every input is inside the penumbra, and the error is published per geometry, across the penumbra and
        // near each contact
        Assert.Equal(85, points.Count);
        Assert.All(points, p => Assert.True(p.C > System.Math.Abs(p.B - p.A) && p.C < p.A + p.B, p.Where));
        foreach (var geometry in errors.GroupBy(e => e.Point.Geometry))
        {
            double Worst(Func<string, bool> where) => geometry.Where(e => where(e.Point.Where))
                .Max(e => double.IsNaN(e.Value) ? double.PositiveInfinity : System.Math.Abs(e.Value - e.Point.ShadowFraction));
            int outside = geometry.Count(e => !(e.Value >= 0.0 && e.Value <= 1.0));
            _output.WriteLine($"MEASURE #434 {geometry.Key}: error of ν across the penumbra " +
                              $"{Worst(w => w.EndsWith("of the width")):E2}; at depths of 1e-2, 1e-4, 1e-6, 1e-8 and " +
                              $"1e-10 to 1e-12 of the width inside the contacts {Worst(w => w.Contains("1e-02")):E2}, " +
                              $"{Worst(w => w.Contains("1e-04")):E2}, {Worst(w => w.Contains("1e-06")):E2}, " +
                              $"{Worst(w => w.Contains("1e-08")):E2}, " +
                              $"{Worst(w => w.Contains("1e-10") || w.Contains("1e-12")):E2}; {outside} of " +
                              $"{geometry.Count()} outside [0, 1] or NaN");
        }
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

        // Assert: published
        Assert.Equal(8, contacts.Count);
        Assert.True(count > 15000, $"{count} states in the penumbra");
        _output.WriteLine($"MEASURE #434 R3, {contacts.Count} contacts, {count} states in the penumbra within 2 ms of " +
                          $"them: ν below 0 {below}, above 1 {above}, NaN {nan}");
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

        // Assert: published
        Assert.True(count > 39000, $"{count} states in the penumbra");
        _output.WriteLine($"MEASURE #434 GEO, {count} states in the penumbra within 20 m of the contacts: ν below 0 " +
                          $"{below}, above 1 {above}, NaN {nan}");
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
