// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// Semi-analytic drag partials (B5): ∂a/∂v and the co-rotation term of ∂a/∂r analytic, the density gradient by central
/// differences of the density chain alone, ∂a/∂Cd exact; against Ridders' derivative of the whole acceleration.
/// </summary>
/// <remarks>
/// GEO is left out, as in the step study: NRLMSISE-00 is not meant for 36,000 km, where drag is meaningless.
/// </remarks>
public class DragPartialsTests : IClassFixture<PartialsTestCases>
{
    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public DragPartialsTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    public static TheoryData<string> States => new(PartialsTestCases.StateNames.Where(s => s != PartialsTestCases.Geo));

    [Theory]
    [MemberData(nameof(States))]
    public void Partials_MatchRiddersWithin1e6(string stateName)
    {
        // Arrange
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new AtmosphericDrag(spacecraft, _cases.Earth);
        var dadr = new double[9];
        var dadv = new double[9];

        // Act
        force.AccumulateStatePartials(state, context, dadr, dadv);

        // Assert
        var reference = RiddersDerivative.StatePartials(force, state, context, 1e-3 * state.Position.Magnitude(),
            1e-3 * state.Velocity.Magnitude());
        double dadrError = RiddersDerivative.RelativeFrobeniusError(dadr, reference.Dadr);
        double dadvError = RiddersDerivative.RelativeFrobeniusError(dadv, reference.Dadv);
        _output.WriteLine($"{stateName}: dadr {dadrError:E2} (reference {reference.DadrError:E2}), " +
                          $"dadv {dadvError:E2} (reference {reference.DadvError:E2})");
        // The reference resolves the tolerance by two orders of magnitude.
        Assert.True(reference.DadrError < 1e-8, $"reference error {reference.DadrError:E2}");
        Assert.True(reference.DadvError < 1e-8, $"reference error {reference.DadvError:E2}");
        Assert.True(dadrError < 1e-6, $"dadr relative error {dadrError:E2}");
        Assert.True(dadvError < 1e-6, $"dadv relative error {dadvError:E2}");
    }

    [Theory]
    [MemberData(nameof(States))]
    public void DensityGradient_MatchesRidders(string stateName)
    {
        // Arrange
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        var force = new AtmosphericDrag(PartialsTestCases.Spacecraft(state), _cases.Earth);
        var rotation = force.BodyFixedRotation(state);
        var gradient = new double[3];

        // Act
        force.DensityGradient(state.Position, rotation, state.Epoch, gradient);

        // Assert: Ridders on each component of the density chain of Apply
        var expected = new double[3];
        var errors = new double[3];
        for (int j = 0; j < 3; j++)
        {
            int column = j;
            (expected[j], errors[j]) = RiddersDerivative.Derivative(
                t => force.Density(With(state.Position, column, t), rotation, state.Epoch),
                RiddersDerivative.Component(state.Position, j), 1e-3 * state.Position.Magnitude());
        }

        double error = RiddersDerivative.RelativeFrobeniusError(gradient, expected);
        double referenceError = System.Math.Sqrt(errors.Sum(e => e * e) / expected.Sum(e => e * e));
        _output.WriteLine($"{stateName}: ∇ρ relative error {error:E2}, reference {referenceError:E2}");
        Assert.True(referenceError < 1e-8, $"reference error {referenceError:E2}");
        Assert.True(error < 1e-6, $"relative error {error:E2}");
    }

    [Fact]
    public void Density_IsTheDensityOfTheAccelerationChain()
    {
        // Arrange: the density that the acceleration uses, ρ = |a| / (½ B |v_rel|²)
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new AtmosphericDrag(spacecraft, _cases.Earth);
        var vRel = force.GetRelativeState(state).AtmosphereRelativeVelocity;

        // Act
        double density = force.Density(state.Position, force.BodyFixedRotation(state), state.Epoch);

        // Assert
        double expected = force.Apply(state, context).Magnitude() /
                          (0.5 * spacecraft.SectionalArea / context.TotalMass * context.DragCoefficient *
                           vRel.MagnitudeSquared());
        Assert.Equal(expected, density, expected * 1e-14);
    }

    [Fact]
    public void StateInTheBodyFixedFrame_TheDensityNeedsNoRotation()
    {
        // Arrange
        var icrfState = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var state = icrfState.ToFrame(_cases.Earth.Frame).ToStateVector();
        var force = new AtmosphericDrag(PartialsTestCases.Spacecraft(icrfState), _cases.Earth);

        // Act
        var rotation = force.BodyFixedRotation(state);
        double density = force.Density(state.Position, rotation, state.Epoch);

        // Assert
        Assert.Null(rotation);
        double expected = force.Density(icrfState.Position, force.BodyFixedRotation(icrfState), icrfState.Epoch);
        Assert.Equal(expected, density, expected * 1e-12);
    }

    [Theory]
    [MemberData(nameof(States))]
    public void StateRelativeToTheSun_WithTheEphemerisCache_GivesTheSamePartials(string stateName)
    {
        // Arrange: the same physical state relative to the Sun, the Earth read from the cache as in SSB mode
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        var heliocentric = state.RelativeTo(_cases.Sun, Aberration.None).ToStateVector();
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new AtmosphericDrag(spacecraft, _cases.Earth);
        var heliocentricForce = new AtmosphericDrag(spacecraft, _cases.Earth)
        {
            EphemerisCache = PartialsTestCases.Cache(_cases.Sun, _cases.Earth)
        };
        var dadr = new double[9];
        var dadv = new double[9];
        var heliocentricDadr = new double[9];
        var heliocentricDadv = new double[9];

        // Act
        force.AccumulateStatePartials(state, context, dadr, dadv);
        heliocentricForce.AccumulateStatePartials(heliocentric, context, heliocentricDadr, heliocentricDadv);

        // Assert: the heliocentric coordinates round the position by about 1e-5 m, which moves ∇ρ by its gradient
        // over the scale height, and the 8-point interpolation of the cache
        double dadrError = RiddersDerivative.RelativeFrobeniusError(heliocentricDadr, dadr);
        double dadvError = RiddersDerivative.RelativeFrobeniusError(heliocentricDadv, dadv);
        _output.WriteLine($"{stateName}: dadr {dadrError:E2}, dadv {dadvError:E2}");
        Assert.True(dadrError < 1e-6, $"dadr relative error {dadrError:E2}");
        Assert.True(dadvError < 1e-6, $"dadv relative error {dadvError:E2}");
    }

    [Fact]
    public void AccelerationAndPartials_WithTheOrientationCacheOfAPropagation_AllocateNothing()
    {
        // Arrange: the propagation's case, the orientation cache of the Earth-fixed frame active on this thread; the
        // density through NRLMSISE-00 without a context, ω and the rotation without an orientation object
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new AtmosphericDrag(spacecraft, _cases.Earth);
        var cache = new PropagationFrameOrientationCache(
            new TimeSystem.Window(state.Epoch.AddHours(-1.0), state.Epoch.AddHours(1.0)), _cases.Earth.Frame,
            TimeSpan.FromSeconds(60.0));
        var dadr = new double[9];
        var dadv = new double[9];
        var dadCd = new double[3];
        var dadCr = new double[3];

        using (Frames.Frame.UseOrientationCache(_cases.Earth.Frame, cache))
        {
            _ = force.Apply(state, context);
            force.AccumulateStatePartials(state, context, dadr, dadv);
            force.AccumulateParameterPartials(state, context, dadCd, dadCr);

            // Act
            long before = GC.GetAllocatedBytesForCurrentThread();
            _ = force.Apply(state, context);
            long acceleration = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            force.AccumulateStatePartials(state, context, dadr, dadv);
            force.AccumulateParameterPartials(state, context, dadCd, dadCr);
            long partials = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert: 3.4 kB and about 27 kB before, in NRLMSISE-00 and Frame.ToFrame
            _output.WriteLine($"acceleration {acceleration} bytes, partials {partials} bytes");
            Assert.Equal(0, acceleration);
            Assert.Equal(0, partials);
        }
    }

    [Fact]
    public void NoVelocityRelativeToTheAtmosphere_NoAccelerationAndNoPartials()
    {
        // Arrange: the spacecraft moves with the atmosphere, v = ω × r
        var leo = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(leo);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new AtmosphericDrag(spacecraft, _cases.Earth);
        var omega = force.GetRelativeState(leo).AngularVelocity;
        var state = new StateVector(leo.Position, omega.Cross(leo.Position), leo.Observer, leo.Epoch, leo.Frame);
        var dadr = Enumerable.Repeat(5.0, 9).ToArray();
        var dadv = Enumerable.Repeat(5.0, 9).ToArray();

        // Act
        force.AccumulateStatePartials(state, context, dadr, dadv);

        // Assert
        Assert.Equal(Vector3.Zero, force.Apply(state, context));
        Assert.All(dadr, x => Assert.Equal(5.0, x));
        Assert.All(dadv, x => Assert.Equal(5.0, x));
    }

    [Theory]
    [MemberData(nameof(States))]
    public void DragCoefficientPartial_MatchesRidders(string stateName)
    {
        // Arrange
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new AtmosphericDrag(spacecraft, _cases.Earth);
        var dadCd = new double[3];

        // Act
        force.AccumulateParameterPartials(state, context, dadCd, new double[3]);

        // Assert
        var expected = new double[3];
        for (int i = 0; i < 3; i++)
        {
            int row = i;
            expected[i] = RiddersDerivative.Derivative(
                cd => RiddersDerivative.Component(force.Apply(state, context with { DragCoefficient = cd }), row),
                context.DragCoefficient, 1.0).Derivative;
        }

        double error = RiddersDerivative.RelativeFrobeniusError(dadCd, expected);
        _output.WriteLine($"{stateName}: ∂a/∂Cd relative error {error:E2}");
        Assert.True(error < 1e-12, $"relative error {error:E2}");
    }

    private static Vector3 With(in Vector3 vector, int index, double value)
    {
        return index switch
        {
            0 => new Vector3(value, vector.Y, vector.Z),
            1 => new Vector3(vector.X, value, vector.Z),
            _ => new Vector3(vector.X, vector.Y, value)
        };
    }
}
