// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
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

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

/// <summary>
/// Drag across the boundaries of the branches of the standard atmospheres (#433): the density of
/// <see cref="EarthStandardAtmosphere"/> jumped by −0.023 % at 11 km and −1.82 % at 25 km, that of
/// <see cref="MarsStandardAtmosphere"/> by +0.41 % at 7 km, and RK7(8) stopped there (100 consecutive rejections) at
/// every tolerance with <c>IncludeInStepControl</c>, and at 1e-12 and 1e-13 without it when the drag is strong.
/// </summary>
/// <remarks>
/// Elliptical orbits with apoapsis R_eq + 400 km, inclined by 45° in ICRF, 10 min from 300 s before the perigee, a
/// point-mass body with drag, m = 1000 kg, Cd = 2.2. Measured on main (2026-10-22, Linux): RK7(8) stopped at 25.000 km of
/// geodetic altitude (Earth, perigee R_eq + 15 km, A = 0.01 m², and R_eq + 20 km, A = 1e-4 m²), at 11.000 km (R_eq + 8 km,
/// A = 1 m²) and at 7.000 km (Mars, R_eq + 35 km, A = 1 m², and R_eq + 10 km, A = 0.01 m²).
/// </remarks>
public class StandardAtmosphereBoundaryTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);
    private static readonly TimeSystem.Window Window = new(Epoch, Epoch.AddSeconds(600.0));
    private readonly ITestOutputHelper _output;

    public StandardAtmosphereBoundaryTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static CelestialBody Body(string model) => model == "Earth"
        ? new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
            new EarthStandardAtmosphere())
        : new CelestialBody(SolarSystemObjects.PlanetsAndMoons.MARS, Frames.Frame.ICRF, Epoch, null,
            new MarsStandardAtmosphere());

    private static Spacecraft NewSpacecraft(CelestialBody body, double perigeeAltitude, double area, int naifId)
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
}
