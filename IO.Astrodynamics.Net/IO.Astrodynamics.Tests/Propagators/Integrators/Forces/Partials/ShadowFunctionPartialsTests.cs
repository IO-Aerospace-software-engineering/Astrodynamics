// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// The SRP partials in the penumbra with the derivative of the shadow function (#423): GMAT Mathematical Specification,
/// R2026a, §4.2.4 and §4.2.5, the derivative of the overlap area in its reduced form, the regime from the geometry.
/// </summary>
public class ShadowFunctionPartialsTests : IClassFixture<PartialsTestCases>
{
    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public ShadowFunctionPartialsTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    public static TheoryData<string, double, bool> PenumbraGrid()
    {
        var data = new TheoryData<string, double, bool>();
        foreach (var orbit in new[] { PartialsTestCases.Leo400, PartialsTestCases.Geo })
        {
            foreach (double nu in new[] { 1e-4, 1e-2, 0.1, 0.5, 0.9, 0.99, 1.0 - 1e-4 })
            {
                data.Add(orbit, nu, false);
                data.Add(orbit, nu, true);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PenumbraGrid))]
    public void Penumbra_PartialsMatchRiddersOfACancellationFreeShadowFunction(string orbit, double shadowFraction,
        bool relativeToTheSun)
    {
        // Arrange
        double angle = PenumbraStates.AngleOfShadowFraction(_cases, orbit, shadowFraction);
        var state = PenumbraStates.At(_cases, orbit, angle);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new SolarRadiationPressure(spacecraft, new[] { _cases.Earth, _cases.Moon });
        var dadr = new double[9];
        var dadv = Enumerable.Repeat(5.0, 9).ToArray();

        // Act
        if (relativeToTheSun)
        {
            force.EphemerisCache = PartialsTestCases.Cache(_cases.Sun, _cases.Earth, _cases.Moon);
            force.AccumulateStatePartials(state.RelativeTo(_cases.Sun, Aberration.None).ToStateVector(), context, dadr,
                dadv);
            force.EphemerisCache = null;
        }
        else
        {
            force.AccumulateStatePartials(state, context, dadr, dadv);
        }

        // Assert: Ridders' derivative of the acceleration relative to the Earth (a change of origin is a translation), with
        // the shadow fraction evaluated without cancellation (PenumbraStates): on the shadow fraction of the library it
        // resolved only about 1e-6 at ν = 0.99 in LEO, its rounding (1.2e-9) divided by the steps of the extrapolation,
        // and its error estimate missed it (2.4e-6 against an estimate of 1.7e-7 on macOS arm64, 2026-10-22). The
        // initial step within the penumbra and a quarter of the distance to the nearest contact
        var (reference, resolution) = CancellationFreeReference(state, context, RiddersStep(orbit, angle));
        double error = RiddersDerivative.RelativeFrobeniusError(dadr, reference);
        _output.WriteLine($"MEASURE #423 {orbit}, ν {shadowFraction:E1}{(relativeToTheSun ? ", relative to the Sun" : "")}: " +
                          $"against Ridders {error:E2}, reference {resolution:E2}");
        Assert.True(error < 1e-6, $"relative error {error:E2}");
        Assert.All(dadv, x => Assert.Equal(5.0, x));
    }

    public static TheoryData<string, bool, double> ContactGrid()
    {
        var data = new TheoryData<string, bool, double>();
        foreach (var orbit in new[] { PartialsTestCases.Leo400, PartialsTestCases.Geo })
        {
            foreach (bool inner in new[] { false, true })
            {
                foreach (double distance in new[] { 0.01, 0.1, 1.0, 10.0 })
                {
                    data.Add(orbit, inner, distance);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ContactGrid))]
    public void NearTheContacts_PartialsMatchRiddersOfACancellationFreeShadowFunction(string orbit, bool inner,
        double distance)
    {
        // Arrange: inside the penumbra, at distance from the outer or the inner contact. The shadow fraction of the
        // library carries a few 1e-8 of rounding there, and Ridders' derivative of it resolves only about 2e-3, so the
        // reference differentiates the same model evaluated without cancellation (PenumbraStates)
        var (innerAngle, outerAngle) = PenumbraStates.ContactAngles(_cases, orbit);
        double radius = PartialsTestCases.State(orbit, _cases.Earth).Position.Magnitude();
        double angle = inner ? innerAngle + distance / radius : outerAngle - distance / radius;
        var state = PenumbraStates.At(_cases, orbit, angle);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new SolarRadiationPressure(spacecraft, new[] { _cases.Earth });
        var dadr = new double[9];

        // Act
        force.AccumulateStatePartials(state, context, dadr, new double[9]);

        // Assert
        var (reference, resolution) = CancellationFreeReference(state, context, 0.25 * distance);
        double relative = RiddersDerivative.RelativeFrobeniusError(dadr, reference);
        var (sa, sb, sc) = PenumbraStates.Discs(_cases, state, _cases.Earth);
        _output.WriteLine($"MEASURE #423 {orbit}, {distance} m inside the {(inner ? "inner" : "outer")} contact, ν " +
                          $"{PenumbraStates.CancellationFreeShadowFraction(sa, sb, sc):E3}: against Ridders {relative:E2}, " +
                          $"reference {resolution:E2}");
        Assert.True(relative < 1e-6 || relative < 10.0 * resolution, $"relative error {relative:E2}");
    }

    [Fact]
    public void OverlapAreaDerivatives_AreTheReducedFormOfGmatEquation4_100()
    {
        // Arrange: eq. 4.100 of the GMAT Mathematical Specification, R2026a, transcribed with its intermediate equations
        // 4.101 to 4.104, for x each of D′ (c), R⊙′ (a) and R_B′ (b), the other two fixed; well conditioned away from the
        // contacts, where √(1 − c₁² / a²) does not vanish
        var random = new Random(423);
        double worst = 0.0;
        for (int k = 0; k < 200; k++)
        {
            double a = 1e-3 + 9e-3 * random.NextDouble();
            double b = 0.05 + 1.45 * random.NextDouble();
            double c = b - a + (0.05 + 0.9 * random.NextDouble()) * 2.0 * a;

            // Act
            var (dAdc, dAda, dAdb) = SolarRadiationPressure.OverlapAreaDerivatives(a, b, c);

            // Assert
            var expected = new[] { Gmat4_100(a, b, c, 0.0, 0.0, 1.0), Gmat4_100(a, b, c, 1.0, 0.0, 0.0),
                Gmat4_100(a, b, c, 0.0, 1.0, 0.0) };
            var actual = new[] { dAdc, dAda, dAdb };
            for (int i = 0; i < 3; i++)
            {
                worst = System.Math.Max(worst, System.Math.Abs(actual[i] - expected[i]) / System.Math.Abs(expected[i]));
            }
        }

        _output.WriteLine($"MEASURE #423 reduced form against GMAT eq. 4.100, 200 geometries: {worst:E2}");
        Assert.True(worst < 1e-9, $"relative difference {worst:E2}");
    }

    // ∂A/∂x of eq. 4.100 with ∂R⊙′/∂x = da, ∂R_B′/∂x = db, ∂D′/∂x = dd
    private static double Gmat4_100(double a, double b, double d, double da, double db, double dd)
    {
        double c1 = (d * d + a * a - b * b) / (2.0 * d);
        double c2 = System.Math.Sqrt(a * a - c1 * c1);
        double dc1 = 1.0 / 2.0 * (-1.0 / (d * d) * (d * d + a * a - b * b) * dd +
                                  2.0 / d * (d * dd + a * da - b * db)); // eq. 4.101
        double dc2 = 1.0 / c2 * (a * da - c1 * dc1); // eq. 4.102
        double dc1OverA = 1.0 / a * dc1 - c1 / (a * a) * da; // eq. 4.103
        double datan = 1.0 / (c2 * c2 + (d - c1) * (d - c1)) * ((d - c1) * dc2 - c2 * (dd - dc1)); // eq. 4.104
        return 2.0 * a * System.Math.Acos(c1 / a) * da -
               a * a / System.Math.Sqrt(1.0 - c1 * c1 / (a * a)) * dc1OverA +
               2.0 * b * System.Math.Atan2(c2, d - c1) * db + b * b * datan - d * dc2 - c2 * dd;
    }

    [Theory]
    [InlineData(PartialsTestCases.Leo400)]
    [InlineData(PartialsTestCases.Geo)]
    public void InsideThePenumbra_WhereTheComputedShadowFractionIsNotPositive_ThePartialsHoldTheShadowTerm(string orbit)
    {
        // Arrange: near the outer contact the shadow fraction of the library is rounding alone, at times zero or below
        // zero where the geometry is in the penumbra (#423). The first such state inside the contact.
        var (_, outerAngle) = PenumbraStates.ContactAngles(_cases, orbit);
        double radius = PartialsTestCases.State(orbit, _cases.Earth).Position.Magnitude();
        StateVector state = null;
        for (int k = 1; k <= 100000 && state == null; k++)
        {
            var candidate = PenumbraStates.At(_cases, orbit, outerAngle - k * 1e-4 / radius);
            var (a, b, c) = PenumbraStates.Discs(_cases, candidate, _cases.Earth);
            if (c < a + b && _cases.Sun.ShadowFraction(_cases.Earth, candidate, Aberration.None) <= 0.0)
            {
                state = candidate;
            }
        }

        Assert.NotNull(state);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new SolarRadiationPressure(spacecraft, new[] { _cases.Earth });
        var dadr = new double[9];
        var sunlit = new double[9];

        // Act
        force.AccumulateStatePartials(state, context, dadr, new double[9]);
        force.AccumulateAnalyticStatePartials(state, context, sunlit);

        // Assert: the term of ∇ν is there, the partials are not the sunlit block
        double shadowTerm = RiddersDerivative.RelativeFrobeniusError(sunlit, dadr);
        var (sa, sb, sc) = PenumbraStates.Discs(_cases, state, _cases.Earth);
        _output.WriteLine($"MEASURE #423 {orbit}: {(PenumbraStates.Discs(_cases, state, _cases.Earth).SunRadius + sb - sc):E2} rad " +
                          $"inside the outer contact, ν of the library {_cases.Sun.ShadowFraction(_cases.Earth, state, Aberration.None):E2}, " +
                          $"without cancellation {PenumbraStates.CancellationFreeShadowFraction(sa, sb, sc):E2}; the sunlit " +
                          $"block misses {shadowTerm:E2} of the partials");
        Assert.True(shadowTerm > 0.5, $"the sunlit block misses {shadowTerm:E2} of the partials");
    }

    public static TheoryData<bool> AnnularOrPartial => new() { true, false };

    [Theory]
    [MemberData(nameof(AnnularOrPartial))]
    public void ShadowOfTheMoon_PartialsMatchRidders(bool annular)
    {
        // Arrange: a spacecraft 400 000 km beyond the Moon on the axis from the Sun, where the angular radius of the Moon,
        // 4.3e-3 rad, is below that of the Sun: an annular eclipse (eq. 4.88 of the GMAT Mathematical Specification) near
        // the axis, a partial one (eq. 4.87) off it
        var moonPosition = _cases.Moon.GetEphemeris(PartialsTestCases.Epoch, _cases.Earth, Frames.Frame.ICRF,
            Aberration.None).ToStateVector().Position;
        var sunPosition = _cases.Sun.GetEphemeris(PartialsTestCases.Epoch, _cases.Earth, Frames.Frame.ICRF,
            Aberration.None).ToStateVector().Position;
        var axis = (moonPosition - sunPosition).Normalize();
        var across = axis.Cross(Vector3.VectorZ).Normalize();
        double offset = annular ? 50e3 : 1800e3;
        var state = new StateVector(moonPosition + axis * 4e8 + across * offset, Vector3.VectorZ * 1000.0, _cases.Earth,
            PartialsTestCases.Epoch, Frames.Frame.ICRF);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new SolarRadiationPressure(spacecraft, new[] { _cases.Earth, _cases.Moon });
        var (a, b, c) = PenumbraStates.Discs(_cases, state, _cases.Moon);
        var dadr = new double[9];

        // Act
        force.AccumulateStatePartials(state, context, dadr, new double[9]);

        // Assert
        Assert.True(annular ? c <= a - b : c > a - b && c < a + b, $"a {a:E3}, b {b:E3}, c {c:E3}");
        var reference = RiddersDerivative.StatePartials(force, state, context, 1e4, 1.0);
        double error = RiddersDerivative.RelativeFrobeniusError(dadr, reference.Dadr);
        _output.WriteLine($"MEASURE #423 Moon, {(annular ? "annular" : "partial")}: ν " +
                          $"{_cases.Sun.ShadowFraction(_cases.Moon, state, Aberration.None):F6}, against Ridders {error:E2}, " +
                          $"reference {reference.DadrError:E2}");
        Assert.True(error < 1e-6 || error < 10.0 * reference.DadrError, $"relative error {error:E2}");
    }

    [Theory]
    [InlineData(PartialsTestCases.Leo400)]
    [InlineData(PartialsTestCases.Geo)]
    public void Penumbra_WithTheEphemerisCache_AllocateNothing(string orbit)
    {
        // Arrange
        var state = PenumbraStates.At(_cases, orbit, PenumbraStates.AngleOfShadowFraction(_cases, orbit, 0.5));
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new SolarRadiationPressure(spacecraft, new[] { _cases.Earth, _cases.Moon })
        {
            EphemerisCache = PartialsTestCases.Cache(_cases.Earth, _cases.Sun, _cases.Moon)
        };
        var dadr = new double[9];
        var dadv = new double[9];
        force.AccumulateStatePartials(state, context, dadr, dadv);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        force.AccumulateStatePartials(state, context, dadr, dadv);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(0, allocated);
    }

    // ∂a/∂r by Ridders' derivative of a_lit (1 − ν), ν the shadow fraction of the Earth evaluated without cancellation,
    // and its resolution, the Frobenius norm of the error estimates relative to that of the block
    private (double[] Partials, double Resolution) CancellationFreeReference(StateVector state,
        ForceEvaluationContext context, double initialStep)
    {
        var unshadowed = new SolarRadiationPressure(PartialsTestCases.Spacecraft(state), Array.Empty<CelestialBody>());
        var reference = new double[9];
        double errorSum = 0.0;
        for (int j = 0; j < 3; j++)
        {
            for (int i = 0; i < 3; i++)
            {
                int row = i;
                int column = j;
                var (derivative, error) = RiddersDerivative.Derivative(x =>
                {
                    var perturbed = WithPosition(state, column, x);
                    var (a, b, c) = PenumbraStates.Discs(_cases, perturbed, _cases.Earth);
                    return RiddersDerivative.Component(unshadowed.Apply(perturbed, context), row) *
                           (1.0 - PenumbraStates.CancellationFreeShadowFraction(a, b, c));
                }, RiddersDerivative.Component(state.Position, j), initialStep);
                reference[3 * i + j] = derivative;
                errorSum += error * error;
            }
        }

        return (reference, System.Math.Sqrt(errorSum) / System.Math.Sqrt(reference.Sum(x => x * x)));
    }

    // Ridders' initial step: 1e-4 of the radius (680 m in LEO, 4.2 km at GEO), within the penumbra, and at most a quarter
    // of the distance to the nearest contact, where the partials are only continuous
    private double RiddersStep(string orbit, double angle)
    {
        var (inner, outer) = PenumbraStates.ContactAngles(_cases, orbit);
        double radius = PartialsTestCases.State(orbit, _cases.Earth).Position.Magnitude();
        double toContact = radius * System.Math.Min(angle - inner, outer - angle);
        return System.Math.Min(1e-4 * radius, 0.25 * toContact);
    }

    private static StateVector WithPosition(StateVector state, int index, double value)
    {
        var position = index switch
        {
            0 => new Vector3(value, state.Position.Y, state.Position.Z),
            1 => new Vector3(state.Position.X, value, state.Position.Z),
            _ => new Vector3(state.Position.X, state.Position.Y, value)
        };
        return new StateVector(position, state.Velocity, state.Observer, state.Epoch, state.Frame);
    }
}
