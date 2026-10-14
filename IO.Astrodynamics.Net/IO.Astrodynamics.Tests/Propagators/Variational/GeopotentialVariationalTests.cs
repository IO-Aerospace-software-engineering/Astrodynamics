// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The analytic geopotential partials (B3) in a propagation: the degree option, the trajectory, and Φ against central
/// differences of whole propagations.
/// </summary>
public class GeopotentialVariationalTests : IClassFixture<GeopotentialPartialsBodies>
{
    private readonly GeopotentialPartialsBodies _bodies;
    private readonly ITestOutputHelper _output;

    public GeopotentialVariationalTests(GeopotentialPartialsBodies bodies, ITestOutputHelper output)
    {
        _bodies = bodies;
        _output = output;
    }

    private static TimeSystem.Time Start => PartialsTestCases.Epoch;

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-3)]
    public void EnableVariationalEquations_DegreeBelowTwo_Throws(int degree)
    {
        // Arrange
        using var propagator = Propagator(_bodies.Earths[10], new RK78Integrator());

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            propagator.EnableVariationalEquations(new VariationalOptions(GeopotentialPartialsDegree: degree)));

        // Assert
        Assert.Equal("options", exception.ParamName);
        Assert.Contains("at least 2", exception.Message);
    }

    [Fact]
    public void EnableVariationalEquations_AboveTheGuardDegree_Throws_AndAReducedDegreeIsAccepted()
    {
        // Arrange
        var earth = _bodies.Earths[GeopotentialGravitationalField.MaxPartialsDegree + 10];
        var integrator = new RK78Integrator();
        using var propagator = Propagator(earth, integrator);

        // Act and assert: the failure comes before the propagation, and leaves the forces as they were
        var exception = Assert.Throws<NotSupportedException>(() =>
            propagator.EnableVariationalEquations(new VariationalOptions()));
        Assert.Contains("GeopotentialPartialsDegree", exception.Message);
        Assert.Null(Gravity(integrator).PartialsDegree);
        Assert.Null(integrator.VariationalEquations);
        propagator.EnableVariationalEquations(new VariationalOptions(GeopotentialPartialsDegree: 20));
        Assert.Equal(20, Gravity(integrator).PartialsDegree);
    }

    [Fact]
    public void EnableVariationalEquations_GivesTheDegreeToTheGravityForce_AndIgnoresItWithoutGeopotential()
    {
        // Arrange
        var withGeopotential = new RK78Integrator();
        var pointMass = new RK78Integrator();
        var pointMassEarth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
        using var first = Propagator(_bodies.Earths[10], withGeopotential);
        using var second = Propagator(pointMassEarth, pointMass);

        // Act
        first.EnableVariationalEquations(new VariationalOptions(GeopotentialPartialsDegree: 4));
        second.EnableVariationalEquations(new VariationalOptions(GeopotentialPartialsDegree: 4));

        // Assert
        Assert.Equal(4, Gravity(withGeopotential).PartialsDegree);
        Assert.NotNull(pointMass.VariationalEquations);
    }

    [Fact]
    public void WithTheGeopotentialPartials_TheTrajectoryIsBitIdentical()
    {
        // Arrange: EGM2008 10x10, Moon and Sun, 3 h
        var earth = _bodies.Earths[10];
        var end = Start.AddHours(3.0);

        // Act
        var without = Propagate(earth, Leo(earth), end, null, 1e-9);
        var full = Propagate(earth, Leo(earth), end, new VariationalOptions(), 1e-9);
        var reduced = Propagate(earth, Leo(earth), end, new VariationalOptions(GeopotentialPartialsDegree: 2), 1e-9);

        // Assert
        var reference = without.Segments.Single().Steps;
        foreach (var solution in new[] { full, reduced })
        {
            var steps = solution.Segments.Single().Steps;
            Assert.Equal(reference.Count, steps.Count);
            for (int k = 0; k < steps.Count; k++)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(reference[k].StepSize),
                    BitConverter.DoubleToInt64Bits(steps[k].StepSize));
                Assert.Equal(reference[k].EndPosition, steps[k].EndPosition);
                Assert.Equal(reference[k].EndVelocity, steps[k].EndVelocity);
            }
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(70)]
    public void Leo_PhiMatchesCentralDifferencesOfWholePropagations(int degree)
    {
        // Arrange: EGM2008 alone, a fixed 30 s step over about one orbit, so that every perturbed propagation takes
        // the same steps and the differences see the discrete map itself. Steps of 1 m and 1 mm/s, as in the two-body
        // case: truncation of order (δ/r)², rounding of the final state about ε |r| / δ ≈ 1e-9 relative
        var earth = _bodies.Earths[degree];
        var start = Leo(earth);
        const double duration = 5520.0;
        var end = Start.AddSeconds(duration);
        var solution = PropagateFixedStep(earth, start, end, new VariationalOptions());
        var phi = new double[36];
        solution.EvaluateVariational(end, phi, Array.Empty<double>());

        // Act
        var expected = new double[36];
        for (int column = 0; column < 6; column++)
        {
            double delta = column < 3 ? 1.0 : 1e-3;
            var plus = FinalState(PropagateFixedStep(earth, Perturbed(start, column, delta), end, null));
            var minus = FinalState(PropagateFixedStep(earth, Perturbed(start, column, -delta), end, null));
            for (int row = 0; row < 6; row++)
            {
                expected[row * 6 + column] = (plus[row] - minus[row]) / (2.0 * delta);
            }
        }

        // Assert
        double error = StmMeasures.WorstBlockError(phi, expected);
        _output.WriteLine($"EGM2008 {degree}x{degree}: Φ against central differences of propagations: {error:E2} " +
                          "(worst 3×3 block)");
        Assert.True(error < 1e-7, $"Φ: {error:E2}");
    }

    private static StateVector Leo(CelestialBody earth) => PartialsTestCases.State(PartialsTestCases.Leo400, earth);

    private CentralBodyPropagator Propagator(CelestialBody earth, RK78Integrator integrator)
    {
        var state = Leo(earth);
        return new CentralBodyPropagator(new TimeSystem.Window(Start, Start.AddHours(1.0)),
            PartialsTestCases.Spacecraft(state), integrator, new CelestialItem[] { earth }, false, false,
            TimeSpan.FromSeconds(60.0));
    }

    private static GravitationalAcceleration Gravity(RK78Integrator integrator) =>
        integrator.ForceList.OfType<GravitationalAcceleration>().Single();

    private static PropagationSolution Propagate(CelestialBody earth, StateVector start, TimeSystem.Time end,
        VariationalOptions options, double tolerance)
    {
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start);
        var sun = new CelestialBody(Stars.Sun);
        return PartialsTestCases.Spacecraft(start).PropagateWithVariationalEquations(new TimeSystem.Window(Start, end),
            new CelestialItem[] { earth, moon, sun }, new RK78Integrator(tolerance, tolerance), false, false,
            TimeSpan.FromSeconds(600.0), options);
    }

    private static PropagationSolution PropagateFixedStep(CelestialBody earth, StateVector start, TimeSystem.Time end,
        VariationalOptions options)
    {
        return PartialsTestCases.Spacecraft(start).PropagateWithVariationalEquations(new TimeSystem.Window(Start, end),
            new CelestialItem[] { earth }, new RK78Integrator(30.0), false, false, TimeSpan.FromSeconds(60.0),
            options);
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
