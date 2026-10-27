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
        var expected = WholePropagationDifferences.Phi(state => PropagateFixedStep(earth, state, end, null), start, 1.0,
            1e-3);

        // Assert
        double error = StmMeasures.WorstBlockError(phi, expected);
        _output.WriteLine($"EGM2008 {degree}x{degree}: Φ against central differences of propagations: {error:E2} " +
                          "(worst 3×3 block)");
        Assert.True(error < 1e-7, $"Φ: {error:E2}");
    }

    /// <summary>
    /// With the GM of the model in the central term (decision 18 of #457): Φ of RK7(8) at 1e-13 against central
    /// differences of whole propagations at 1e-14 from the exact perturbed state (#398), each with its own steps, per
    /// 3×3 block every hour and at the end of 6 h; EGM2008 10×10, the Moon and the Sun, LEO 400 km. Steps of 10 m and
    /// 1 cm/s, those of R2 (<c>WholePropagationStepStudyTests</c>).
    /// </summary>
    [Fact]
    public void Leo_WithTheModelCentralTerm_PhiMatchesCentralDifferencesOfWholePropagations()
    {
        // Arrange
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
            new GeopotentialModelParameters(GeopotentialPartialsBodies.Model, 10)
                { CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter });
        var bodies = new CelestialItem[]
            { earth, new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start), new CelestialBody(Stars.Sun) };
        var start = Leo(earth);
        var window = new TimeSystem.Window(Start, Start.AddHours(6.0));
        var solution = PartialsTestCases.Spacecraft(start).PropagateWithVariationalEquations(window, bodies,
            new RK78Integrator(1e-13, 1e-13), false, false, TimeSpan.FromSeconds(600.0), new VariationalOptions());
        var epochs = Enumerable.Range(1, 6).Select(h => Start.AddHours(h)).ToArray();

        // Act
        var expected = WholePropagationDifferences.Phi(state => new CentralBodyPropagator(window,
                PartialsTestCases.Spacecraft(state), new RK78Integrator(1e-14, 1e-14), bodies, false, false,
                TimeSpan.FromSeconds(600.0)).Propagate(), start, 10.0, 1e-2, epochs);
        var errors = epochs.Select((t, k) =>
            StmMeasures.BlockErrors(StmMeasures.Phi(StmMeasures.Y(solution, t)), expected[k])).ToArray();

        // Assert
        var worst = Enumerable.Range(0, 4).Select(b => errors.Max(e => e[b])).ToArray();
        _output.WriteLine($"MEASURE central term with the model GM (decision 18 of #457), Φ against central " +
                          $"differences of whole propagations at 1e-14, worst over 6 h per block (rr, rv, vr, vv): " +
                          $"{worst[0]:E2}, {worst[1]:E2}, {worst[2]:E2}, {worst[3]:E2}; at the end " +
                          $"{string.Join(", ", errors[^1].Select(e => e.ToString("E2")))}");
        // Provisional bound, twice the worst block on Linux rounded up at the second significant digit (2026-10-27:
        // 2.45e-9, Φ_rv), to be set from the three platforms. With the GM of the body in the point-mass partials and the GM of the model in the acceleration,
        // the same comparison gives 4.4e-6 (measured on a scratch build)
        Assert.All(worst, e => Assert.True(e < 5.0e-9, $"Φ: {e:E2}"));
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
}
