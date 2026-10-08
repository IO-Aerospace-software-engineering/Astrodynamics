// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// Where the variational equations are available (A6): RK7(8) only, and heliocentric propagations, relative to the Sun
/// or to the solar system barycentre.
/// </summary>
public class VariationalSupportTests : IClassFixture<PartialsTestCases>
{
    // Heliocentric case: a fixed one-day step over 10 days, so that the perturbed propagations take the same steps.
    private const double StepSize = 86400.0;
    private const int Days = 10;

    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public VariationalSupportTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    private TimeSystem.Time Start => PartialsTestCases.Epoch;

    // About 1 AU from the Sun, a little outside the orbit of the Earth, in ICRF
    private StateVector Heliocentric(ILocalizable observer) =>
        new(new Vector3(1.52e11, 1.0e10, 2.0e9), new Vector3(-2.0e3, 2.95e4, 1.2e3), observer, Start,
            Frames.Frame.ICRF);

    [Fact]
    public void EnableVariationalEquations_WithVelocityVerlet_Throws()
    {
        // Arrange: the default integrator of the propagator is Velocity-Verlet
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        using var propagator = new CentralBodyPropagator(new TimeSystem.Window(Start, Start.AddHours(1.0)),
            PartialsTestCases.Spacecraft(state), new CelestialItem[] { _cases.Earth }, false, false,
            TimeSpan.FromSeconds(60.0));

        // Act
        var exception = Assert.Throws<NotSupportedException>(() =>
            propagator.EnableVariationalEquations(new VariationalOptions()));

        // Assert
        Assert.Contains("RK78Integrator", exception.Message);
        Assert.Contains(nameof(VVIntegrator), exception.Message);
    }

    [Fact]
    public void EnableVariationalEquations_WithoutOptions_Throws()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        using var propagator = new CentralBodyPropagator(new TimeSystem.Window(Start, Start.AddHours(1.0)),
            PartialsTestCases.Spacecraft(state), new RK78Integrator(), new CelestialItem[] { _cases.Earth }, false,
            false, TimeSpan.FromSeconds(60.0));

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => propagator.EnableVariationalEquations(null));

        // Assert
        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void Heliocentric_PhiMatchesCentralDifferencesOfWholePropagations()
    {
        // Arrange: Sun and the Earth as third body. Steps of 100 km and 0.1 m/s, so that the change of the final state
        // each produces dominates the rounding accumulated by the propagation (about 1e-11 m/s and 1e-4 m): the smallest
        // block, Φ_vr ≈ n² t ≈ 4e-8 /s, turns 100 km into 4e-3 m/s, and Φ_rv ≈ t turns 0.1 m/s into 9e4 m, both about
        // 3e-9 relative. The truncation is of order (δ/r)² ≈ 4e-13 and (δv/v)² ≈ 1e-11.
        var end = Start.AddDays(Days);
        var phi = Phi(Propagate(Heliocentric(Stars.SUN_BODY), new VariationalOptions()), end);

        // Act
        var expected = new double[36];
        for (int column = 0; column < 6; column++)
        {
            double delta = column < 3 ? 1e5 : 1e-1;
            var plus = FinalState(Propagate(Perturbed(Heliocentric(Stars.SUN_BODY), column, delta), null));
            var minus = FinalState(Propagate(Perturbed(Heliocentric(Stars.SUN_BODY), column, -delta), null));
            for (int row = 0; row < 6; row++)
            {
                expected[row * 6 + column] = (plus[row] - minus[row]) / (2.0 * delta);
            }
        }

        // Assert
        double error = StmMeasures.WorstBlockError(phi, expected);
        _output.WriteLine($"heliocentric Φ against central differences of propagations: {error:E2} (worst 3×3 block)");
        Assert.True(error < 1e-6, $"Φ: {error:E2}");
    }

    [Fact]
    public void SolarSystemBarycentreObserver_GivesTheStmOfTheSunRelativeState()
    {
        // Arrange: the same physical state relative to the solar system barycentre; the Sun becomes the central body and
        // a change of origin is a translation, so ∂x_Sun/∂x_SSB = I. The observer change rounds the state to about
        // ε |r_SSB| ≈ 1e-5 m, far below what moves Φ at 1e-10.
        var relativeToSun = Heliocentric(Stars.SUN_BODY);
        var relativeToBarycentre = relativeToSun.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None)
            .ToStateVector();
        var end = Start.AddDays(Days);

        // Act
        var fromSun = Propagate(relativeToSun, new VariationalOptions());
        var fromBarycentre = Propagate(relativeToBarycentre, new VariationalOptions());

        // Assert
        Assert.Equal(Stars.SUN_BODY.NaifId, ((CelestialItem)fromBarycentre.Dynamics.Observer).NaifId);
        double error = StmMeasures.WorstBlockError(Phi(fromBarycentre, end), Phi(fromSun, end));
        _output.WriteLine($"Φ from the barycentre against Φ from the Sun: {error:E2} (worst 3×3 block)");
        Assert.True(error < 1e-10, $"Φ: {error:E2}");
    }

    private PropagationSolution Propagate(StateVector start, VariationalOptions options)
    {
        var spacecraft = PartialsTestCases.Spacecraft(start);
        return spacecraft.Propagate(new TimeSystem.Window(Start, Start.AddDays(Days)),
            new CelestialItem[] { Stars.SUN_BODY, _cases.Earth }, new RK78Integrator(StepSize), false, false,
            TimeSpan.FromSeconds(StepSize), options);
    }

    private static double[] Phi(PropagationSolution solution, TimeSystem.Time epoch)
    {
        var y = new double[36];
        solution.EvaluateVariational(epoch, y, Array.Empty<double>());
        return y;
    }

    private static double[] FinalState(PropagationSolution solution)
    {
        var step = solution.Segments[^1].Steps[^1];
        return new[]
        {
            step.EndPosition.X, step.EndPosition.Y, step.EndPosition.Z,
            step.EndVelocity.X, step.EndVelocity.Y, step.EndVelocity.Z
        };
    }

    private static StateVector Perturbed(StateVector state, int component, double delta)
    {
        var p = state.Position;
        var v = state.Velocity;
        var position = component switch
        {
            0 => new Vector3(p.X + delta, p.Y, p.Z),
            1 => new Vector3(p.X, p.Y + delta, p.Z),
            2 => new Vector3(p.X, p.Y, p.Z + delta),
            _ => p
        };
        var velocity = component switch
        {
            3 => new Vector3(v.X + delta, v.Y, v.Z),
            4 => new Vector3(v.X, v.Y + delta, v.Z),
            5 => new Vector3(v.X, v.Y, v.Z + delta),
            _ => v
        };
        return new StateVector(position, velocity, state.Observer, state.Epoch, state.Frame);
    }
}
