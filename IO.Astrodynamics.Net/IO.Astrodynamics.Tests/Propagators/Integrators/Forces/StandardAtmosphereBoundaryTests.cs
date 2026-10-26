// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

/// <summary>
/// Drag across the boundaries of the branches of the standard atmospheres (#433): the density of
/// <see cref="EarthStandardAtmosphere"/> jumped by −0.023 % at 11 km and −1.82 % at 25 km, that of
/// <see cref="MarsStandardAtmosphere"/> by +0.41 % at 7 km, and RK7(8) stopped there (100 consecutive rejections) at
/// every tolerance with <c>IncludeInStepControl</c>, and at 1e-12 and 1e-13 without it when the drag is strong. Since #437
/// the Earth model is the U.S. Standard Atmosphere 1976, whose density is continuous but whose slope jumps at the base of
/// each layer (+23.5 % at 11.019 km): the Earth passes cross those bases and their transitions of ±100 m, the junction
/// at 86 km, the switch of the mean molecular weight at 100 km, the bends of the temperature at 91, 110 and 120 km and
/// the entry of H at 150 km; without the transitions RK7(8) with <c>IncludeInStepControl</c> stopped on the bases at
/// every tolerance.
/// </summary>
/// <remarks>
/// Elliptical orbits with apoapsis R_eq + 400 km, inclined by 45° in ICRF, 10 min from 300 s before the perigee, a
/// point-mass body with drag, m = 1000 kg, Cd = 2.2. Measured on main (2026-10-22, Linux): RK7(8) stopped at 25.000 km of
/// geodetic altitude (Earth, perigee R_eq + 15 km, A = 0.01 m², and R_eq + 20 km, A = 1e-4 m²), at 11.000 km (R_eq + 8 km,
/// A = 1 m²) and at 7.000 km (Mars, R_eq + 35 km, A = 1 m², and R_eq + 10 km, A = 0.01 m²).
/// </remarks>
public class StandardAtmosphereBoundaryTests
{
    internal static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);
    internal static readonly TimeSystem.Window Window = new(Epoch, Epoch.AddSeconds(600.0));
    private readonly ITestOutputHelper _output;

    public StandardAtmosphereBoundaryTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    internal static CelestialBody Body(string model) => model == "Earth"
        ? new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
            new EarthStandardAtmosphere())
        : new CelestialBody(SolarSystemObjects.PlanetsAndMoons.MARS, Frames.Frame.ICRF, Epoch, null,
            new MarsStandardAtmosphere());

    internal static Spacecraft NewSpacecraft(CelestialBody body, double perigeeAltitude, double area, int naifId)
    {
        double perigee = body.EquatorialRadius + perigeeAltitude;
        double apoapsis = body.EquatorialRadius + 400e3;
        double a = 0.5 * (perigee + apoapsis);
        double meanMotion = System.Math.Sqrt(body.GM / (a * a * a));
        var orbit = new KeplerianElements(a, (apoapsis - perigee) / (apoapsis + perigee),
            45.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, -300.0 * meanMotion, body, Epoch, Frames.Frame.ICRF);
        return new Spacecraft(naifId, "BOUNDARY" + -naifId, 1000.0, 1000.0, new Clock("boundary" + -naifId, 65536),
            orbit, area, 2.2, null, 1.0);
    }

    // The lowest geodetic altitude of the trajectory, every second
    private static double Lowest(Func<TimeSystem.Time, StateVector> state, Spacecraft spacecraft, CelestialBody body)
    {
        var drag = new AtmosphericDrag(spacecraft, body);
        return Enumerable.Range(0, 601).Min(second =>
        {
            var s = state(Epoch.AddSeconds(second));
            return drag.Geodetic(s.Position, drag.BodyFixedRotation(s), s.Epoch).Altitude;
        });
    }

    // Model, perigee altitude above R_eq (m), area (m²), the boundaries the trajectory crosses (m)
    public static TheoryData<string, double, double, double, bool> Cases()
    {
        var data = new TheoryData<string, double, double, double, bool>();
        foreach (var (model, perigee, area) in new[]
                 {
                     ("Earth", 15e3, 0.01), ("Earth", 8e3, 1.0), ("Earth", 20e3, 1e-4), ("Mars", 35e3, 1.0),
                     ("Mars", 10e3, 0.01)
                 })
        {
            foreach (double tolerance in new[] { 1e-11, 1e-12, 1e-13 })
            {
                data.Add(model, perigee, area, tolerance, false);
                data.Add(model, perigee, area, tolerance, true);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void RK78_AcrossTheBoundaries_Completes(string model, double perigee, double area, double tolerance,
        bool includeInStepControl)
    {
        // Arrange
        var body = Body(model);
        var spacecraft = NewSpacecraft(body, perigee, area, -4330);
        var options = includeInStepControl ? new CovariancePropagationOptions { IncludeInStepControl = true } : null;

        // Act
        var solution = spacecraft.Propagate(Window, new CelestialItem[] { body },
            new RK78Integrator(tolerance, tolerance), true, false, Window.Length, options);

        // Assert: the trajectory goes below the lowest boundary of the model (7 km for Mars, 11 km for the Earth), so it
        // crosses every boundary, and the propagation reaches the end of its window
        int steps = solution.Segments.Sum(s => s.Steps.Count);
        double lowest = Lowest(solution.GetStateVector, spacecraft, body);
        _output.WriteLine($"MEASURE {model}, perigee R_eq + {perigee / 1000.0} km, A = {area} m², {tolerance:E0}, " +
                          $"IncludeInStepControl {includeInStepControl}: {steps} steps, lowest {lowest / 1000.0:F3} km");
        Assert.True(lowest < (model == "Earth" ? 11000.0 : 7000.0), $"lowest {lowest} m");
        var last = solution.Segments[^1];
        Assert.Equal(Window.EndDate, last.BaseEpoch.AddSeconds(last.Steps[^1].CumulativeTime + last.Steps[^1].StepSize));
        if (includeInStepControl)
        {
            Assert.Equal((6, 6), (solution.GetStateTransitionMatrix(Window.EndDate).Rows,
                solution.GetStateTransitionMatrix(Window.EndDate).Columns));
        }
    }

    public static TheoryData<string, string> Paths()
    {
        var data = new TheoryData<string, string>();
        foreach (string model in new[] { "Earth", "Mars" })
        {
            foreach (string path in new[]
                     {
                         "Velocity-Verlet", "RK7(8)", "RK7(8), state transition matrix", "RK7(8), IncludeInStepControl",
                         "builder", "batch", "scenario"
                     })
            {
                data.Add(model, path);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task EveryPublicPath_AcrossTheBoundaries_Completes(string model, string path)
    {
        // Arrange: a grazing pass on the Earth (crosses 25 km and 11 km), a pass with re-entry on Mars (crosses 7 km), at
        // 1e-12, where RK7(8) stopped with IncludeInStepControl
        var body = Body(model);
        var bodies = new CelestialItem[] { body };
        var spacecraft = model == "Earth"
            ? NewSpacecraft(body, 20e3, 1e-4, -4340)
            : NewSpacecraft(body, 10e3, 0.01, -4341);
        var options = new CovariancePropagationOptions();
        RK78Integrator Integrator() => new(1e-12, 1e-12);

        // Act
        Func<TimeSystem.Time, StateVector> state;
        switch (path)
        {
            case "Velocity-Verlet":
            {
                var states = spacecraft.Propagate(Window, bodies, true, false, TimeSpan.FromSeconds(1.0)).StateVectors;
                state = epoch => states.First(s => s.Epoch >= epoch);
                break;
            }
            case "scenario":
            {
                var scenario = new Scenario("boundary" + model, new IO.Astrodynamics.Mission.Mission("boundary" + model),
                    Window);
                scenario.AddCelestialItem(body);
                scenario.AddSpacecraft(spacecraft);
                await scenario.SimulateAsync(true, false, TimeSpan.FromSeconds(1.0));
                state = epoch => spacecraft.GetEphemeris(epoch, body, Frames.Frame.ICRF, Aberration.None)
                    .ToStateVector();
                break;
            }
            default:
            {
                var solution = path switch
                {
                    "RK7(8)" => spacecraft.Propagate(Window, bodies, Integrator(), true, false, Window.Length),
                    "RK7(8), state transition matrix" => spacecraft.Propagate(Window, bodies, Integrator(), true, false,
                        Window.Length, options),
                    "RK7(8), IncludeInStepControl" => spacecraft.Propagate(Window, bodies, Integrator(), true, false,
                        Window.Length, options with { IncludeInStepControl = true }),
                    "builder" => new CentralBodyPropagatorBuilder(Window, spacecraft, Integrator(), Window.Length)
                        .WithPerturbingBodies(bodies).IncludeAtmosphericDrag()
                        .WithCovariancePropagation(options with { IncludeInStepControl = true }).Build().Propagate(),
                    _ => BatchPropagator.Propagate(new[]
                    {
                        new PropagationTask(spacecraft, Window, bodies, true, false, Window.Length, () => Integrator())
                            { CovariancePropagation = options with { IncludeInStepControl = true } }
                    }).Results[0].Solution
                };
                state = solution.GetStateVector;
                break;
            }
        }

        // Assert: the end of the window is reached below the lowest boundary of the model
        double lowest = Lowest(state, spacecraft, body);
        var end = state(Window.EndDate);
        _output.WriteLine($"{model}, {path}: lowest {lowest / 1000.0:F3} km, end {end.Position}");
        Assert.True(lowest < (model == "Earth" ? 11000.0 : 7000.0), $"lowest {lowest} m");
        Assert.True(double.IsFinite(end.Position.Magnitude()));
    }

    /// <summary>
    /// The start of #468, a state 300 s before the perigee of the case at R_eq + 8 km (A = 1 m²) given as a state
    /// vector, at 22.36 km of geodetic altitude: with <c>IncludeInStepControl</c> at 1e-13, RK7(8) stopped there with
    /// 100 rejections at t = 0 (error of Y 7.118 at 1e-6 s, error of the state below 1), on main and on #467
    /// (2026-10-26, Linux).
    /// </summary>
    internal static StateVector StateThatStoppedAtTheMinimumStep(CelestialBody body) => new(
        new Vector3(5951776.799506006, -1661995.749092966, -1661995.7490929659),
        new Vector3(2859.6050956627555, 5284.196857156525, 5284.196857156524), body, Epoch, Frames.Frame.ICRF);

    /// <summary>
    /// A start of #468 in ECLIPJ2000: <see cref="StateThatStoppedAtTheMinimumStep"/> in that frame, moved by 4 mm along
    /// its x axis. With <c>IncludeInStepControl</c> at 1e-13, RK7(8) stopped there with 100 rejections at t = 0 on main
    /// (error of Y 4.443 at 1e-6 s), one of the 2 of 61 such starts moved by 0 to 60 mm that stopped (2026-10-26, Linux).
    /// </summary>
    internal static StateVector StateInTheEclipticThatStoppedAtTheMinimumStep(CelestialBody body) => new(
        new Vector3(5951776.8035060065, -2185955.22927316, -863747.3447828984),
        new Vector3(2859.6050956627564, 6950.0886261075375, 2746.223031659379), body, Epoch,
        Frames.Frame.ECLIPTIC_J2000);

    [Fact]
    public void RK78_IncludeInStepControl_InitialStepsAroundTheDefault_AllComplete()
    {
        // Arrange: the case at R_eq + 8 km, A = 1 m², 1e-13, with IncludeInStepControl, its initial step of 60 s moved by
        // k ulp, k = −100 … 100: every later attempt of the first step moves by about one ulp, as a last-bit difference
        // of a step factor or of a math function between platforms moves it (#468). On the first steps of a segment the
        // error of Φ_vr, which starts at 0, is the rounding noise of the drag Jacobian across the stages, 1 to 13 times
        // the tolerance whatever the step. Before #468, RK7(8) stopped with 100 rejections at the minimum step unless an
        // attempt drew an error of 1 or less: k = 14, 15 and 92 on main (2026-10-26, Linux); k = 0 on macOS arm64 in the
        // CI of #467, whose start moved by 1e-5 m
        var body = Body("Earth");
        var options = new CovariancePropagationOptions { IncludeInStepControl = true };
        var failures = new List<string>();
        int fewest = int.MaxValue;
        int most = 0;

        // Act
        for (int k = -100; k <= 100; k++)
        {
            double initialStep = 60.0;
            for (int i = 0; i < System.Math.Abs(k); i++)
            {
                initialStep = k > 0 ? System.Math.BitIncrement(initialStep) : System.Math.BitDecrement(initialStep);
            }

            try
            {
                var solution = NewSpacecraft(body, 8e3, 1.0, -4330).Propagate(Window, new CelestialItem[] { body },
                    new RK78Integrator(1e-13, 1e-13, initialStep), true, false, Window.Length, options);
                var last = solution.Segments[^1];
                if (last.BaseEpoch.AddSeconds(last.Steps[^1].CumulativeTime + last.Steps[^1].StepSize) != Window.EndDate)
                {
                    failures.Add($"k = {k}: ends at {last.BaseEpoch.AddSeconds(last.Steps[^1].CumulativeTime)}");
                }

                int steps = solution.Segments.Sum(s => s.Steps.Count);
                fewest = System.Math.Min(fewest, steps);
                most = System.Math.Max(most, steps);
            }
            catch (InvalidOperationException exception)
            {
                failures.Add($"k = {k}: {exception.Message}");
            }
        }

        // Assert: every run reaches the end of its window (2026-10-26, Linux: 2369 to 2723 steps, those that completed
        // before #468 bit for bit as before)
        _output.WriteLine($"MEASURE #468 initial steps 60 s ± 100 ulp: {201 - failures.Count} of 201 complete, " +
                          $"{fewest} to {most} steps");
        Assert.Empty(failures);
    }

    public static TheoryData<string> PathsFromTheStateThatStoppedAtTheMinimumStep()
    {
        return new TheoryData<string>
        {
            "Velocity-Verlet", "RK7(8)", "RK7(8), state transition matrix", "RK7(8), IncludeInStepControl",
            "covariance, IncludeInStepControl", "IncludeInStepControl, ECLIPJ2000", "constructor", "builder", "batch",
            "scenario"
        };
    }

    [Theory]
    [MemberData(nameof(PathsFromTheStateThatStoppedAtTheMinimumStep))]
    public async Task FromTheStateThatStoppedAtTheMinimumStep_EveryPublicPath_Completes(string path)
    {
        // Arrange: the start of #468, at 1e-13, and in ECLIPJ2000 a start that stopped in that frame; with
        // IncludeInStepControl, the first step is accepted at the minimum step size on the error of the state (#468)
        var body = Body("Earth");
        var bodies = new CelestialItem[] { body };
        bool ecliptic = path.EndsWith("ECLIPJ2000");
        var start = ecliptic ? StateInTheEclipticThatStoppedAtTheMinimumStep(body) : StateThatStoppedAtTheMinimumStep(body);
        Spacecraft NewFromTheState(StateVector state) => new(-4350, "BOUNDARY4350", 1000.0, 1000.0,
            new Clock("boundary4350", 65536), state, 1.0, 2.2, null, 1.0);
        var spacecraft = NewFromTheState(start);
        var options = new CovariancePropagationOptions { IncludeInStepControl = true };
        RK78Integrator Integrator() => new(1e-13, 1e-13);
        static PropagationSolution SolutionOf(PropagationResult result)
        {
            if (result.Error != null)
            {
                ExceptionDispatchInfo.Throw(result.Error);
            }

            return result.Solution;
        }

        // Act
        Func<TimeSystem.Time, StateVector> state;
        PropagationSolution solution = null;
        switch (path)
        {
            case "Velocity-Verlet":
            {
                var states = spacecraft.Propagate(Window, bodies, true, false, TimeSpan.FromSeconds(1.0)).StateVectors;
                state = epoch => states.First(s => s.Epoch >= epoch);
                break;
            }
            case "scenario":
            {
                var scenario = new Scenario("boundary468", new IO.Astrodynamics.Mission.Mission("boundary468"), Window);
                scenario.AddCelestialItem(body);
                scenario.AddSpacecraft(spacecraft);
                await scenario.SimulateAsync(true, false, TimeSpan.FromSeconds(1.0), () => Integrator(), options);
                state = epoch => spacecraft.GetEphemeris(epoch, body, Frames.Frame.ICRF, Aberration.None)
                    .ToStateVector();
                break;
            }
            default:
            {
                solution = path switch
                {
                    "RK7(8)" => spacecraft.Propagate(Window, bodies, Integrator(), true, false, Window.Length),
                    "RK7(8), state transition matrix" => spacecraft.Propagate(Window, bodies, Integrator(), true, false,
                        Window.Length, new CovariancePropagationOptions()),
                    "RK7(8), IncludeInStepControl" or "IncludeInStepControl, ECLIPJ2000" => spacecraft.Propagate(Window,
                        bodies, Integrator(), true, false, Window.Length, options),
                    "covariance, IncludeInStepControl" => NewFromTheState(
                        Propagators.Variational.CovariancePropagationTests.WithCovariance(start)).Propagate(Window,
                        bodies, Integrator(), true, false, Window.Length,
                        options with { Mode = CovariancePropagationMode.Covariance }),
                    "constructor" => new CentralBodyPropagator(Window, spacecraft, Integrator(), bodies, true, false,
                        Window.Length, options).Propagate(),
                    "builder" => new CentralBodyPropagatorBuilder(Window, spacecraft, Integrator(), Window.Length)
                        .WithPerturbingBodies(bodies).IncludeAtmosphericDrag().WithCovariancePropagation(options).Build()
                        .Propagate(),
                    _ => SolutionOf(BatchPropagator.Propagate(new[]
                    {
                        new PropagationTask(spacecraft, Window, bodies, true, false, Window.Length, () => Integrator())
                            { CovariancePropagation = options }
                    }).Results[0])
                };
                state = solution.GetStateVector;
                break;
            }
        }

        // Assert: the end of the window is reached below the lowest boundary of the model, with Φ when it is propagated
        double lowest = Lowest(state, spacecraft, body);
        var end = state(Window.EndDate);
        _output.WriteLine($"{path}: {(solution == null ? "" : solution.Segments.Sum(s => s.Steps.Count) + " steps, ")}" +
                          $"lowest {lowest / 1000.0:F3} km, end {end.Position}");
        Assert.True(lowest < 11000.0, $"lowest {lowest} m");
        Assert.True(double.IsFinite(end.Position.Magnitude()));
        Assert.Equal(ecliptic ? Frames.Frame.ECLIPTIC_J2000 : Frames.Frame.ICRF, end.Frame);
        if (solution != null)
        {
            var last = solution.Segments[^1];
            Assert.Equal(Window.EndDate,
                last.BaseEpoch.AddSeconds(last.Steps[^1].CumulativeTime + last.Steps[^1].StepSize));
            if (path != "RK7(8)")
            {
                Assert.Equal((6, 6), (solution.GetStateTransitionMatrix(Window.EndDate).Rows,
                    solution.GetStateTransitionMatrix(Window.EndDate).Columns));
            }
        }
    }

    [Fact]
    public void FromTheStateThatStoppedAtTheMinimumStep_WithoutTheOption_TheStepsAreThoseOfTheStateAlone()
    {
        // Arrange: the start of #468 at 1e-13, Φ and Ψ_Cd integrated with the state but out of the step control (A2):
        // the acceptance at the minimum step of #468 needs the error of Y in the control, so without the option the steps
        // are those of the state alone, bit for bit
        var body = Body("Earth");
        var start = StateThatStoppedAtTheMinimumStep(body);
        PropagationSolution Propagate(CovariancePropagationOptions options) =>
            new Spacecraft(-4351, "BOUNDARY4351", 1000.0, 1000.0, new Clock("boundary4351", 65536), start, 1.0, 2.2,
                null, 1.0).Propagate(Window, new CelestialItem[] { body }, new RK78Integrator(1e-13, 1e-13), true, false,
                Window.Length, options);

        // Act
        var stateAlone = Propagate(null);
        var withTheVariationalEquations = Propagate(new CovariancePropagationOptions());

        // Assert
        var expected = stateAlone.Segments.SelectMany(s => s.Steps).ToArray();
        var actual = withTheVariationalEquations.Segments.SelectMany(s => s.Steps).ToArray();
        _output.WriteLine($"{expected.Length} steps without the variational equations, {actual.Length} with them");
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].CumulativeTime, actual[i].CumulativeTime);
            Assert.Equal(expected[i].StepSize, actual[i].StepSize);
            Assert.Equal(expected[i].EndPosition, actual[i].EndPosition);
            Assert.Equal(expected[i].EndVelocity, actual[i].EndVelocity);
        }
    }
}
