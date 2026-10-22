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
/// Drag with <see cref="MarsStandardAtmosphere"/> (#422): before the isothermal layer, the density was negative above
/// 112.5 km of geodetic altitude, so drag pushed a spacecraft along its velocity, and one day in Velocity-Verlet raised
/// a 150 km orbit by 312 km and a 200 km orbit by 17 km.
/// </summary>
/// <remarks>
/// Circular orbits of radius R_eq + h, inclined by 45° in ICRF, from the ascending node: the point lies at 26.8° of
/// geodetic latitude of Mars, 4.0 km above h. A = 10 m², m = 100 kg, Cd = 2.2, as the dense-air tests of #420.
/// </remarks>
public class MarsDragTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);
    private readonly ITestOutputHelper _output;

    public MarsDragTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static CelestialBody Mars() => new(SolarSystemObjects.PlanetsAndMoons.MARS, Frames.Frame.ICRF, Epoch, null,
        new MarsStandardAtmosphere());

    private static StateVector Circular(CelestialBody body, double altitude) =>
        new KeplerianElements(body.EquatorialRadius + altitude, 0.0, 45.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0,
            0.0, 0.0, body, Epoch, Frames.Frame.ICRF).ToStateVector();

    private static Spacecraft NewSpacecraft(IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit, int naifId) =>
        new(naifId, "MARSDRAG" + -naifId, 100.0, 1000.0, new Clock("marsdrag" + -naifId, 65536), orbit, 10.0, 2.2, null,
            1.0);

    [Fact]
    public void Drag_OpposesTheRelativeVelocity_AtEveryAltitude()
    {
        // Arrange
        var body = Mars();
        var spacecraft = NewSpacecraft(Circular(body, 100e3), -4220);
        var drag = new AtmosphericDrag(spacecraft, body);
        var omega = body.GetOrientation(Frames.Frame.ICRF, Epoch).AngularVelocity;

        for (double altitude = 0.0; altitude <= 1000e3; altitude += 2e3)
        {
            var state = Circular(body, altitude);

            // Act
            var acceleration = drag.Apply(state);

            // Assert: along −v_rel, and not zero
            var relativeVelocity = state.Velocity - omega.Cross(state.Position);
            double cosine = acceleration * relativeVelocity / (acceleration.Magnitude() * relativeVelocity.Magnitude());
            Assert.True(acceleration.Magnitude() > 0.0, $"{altitude} m: no drag");
            Assert.True(cosine < -1.0 + 1e-12, $"{altitude} m: cos {cosine}");
        }
    }

    // One day in Velocity-Verlet at 1 s, the semi-major axis against the same propagation without drag; before #422
    // +17.1 km at 200 km (and +311.6 km at 150 km, where the spacecraft now re-enters within the day)
    [Theory]
    [InlineData(200e3)]
    [InlineData(300e3)]
    public void OneDay_DragLowersTheOrbit(double altitude)
    {
        // Arrange
        var body = Mars();
        var window = new TimeSystem.Window(Epoch, Epoch.AddDays(1.0));
        var bodies = new CelestialItem[] { body };

        // Act
        var withDrag = NewSpacecraft(Circular(body, altitude), -4221).Propagate(window, bodies, true, false,
            TimeSpan.FromSeconds(1.0)).StateVectors.Last();
        var withoutDrag = NewSpacecraft(Circular(body, altitude), -4222).Propagate(window, bodies, false, false,
            TimeSpan.FromSeconds(1.0)).StateVectors.Last();

        // Assert
        double decay = withDrag.SemiMajorAxis() - withoutDrag.SemiMajorAxis();
        _output.WriteLine($"{altitude / 1000.0} km: semi-major axis {decay:F6} m against no drag over one day");
        Assert.True(decay < 0.0, $"{decay} m");
    }

    public static TheoryData<string> Paths => new()
    {
        "Velocity-Verlet", "RK7(8)", "RK7(8), state transition matrix", "RK7(8), IncludeInStepControl", "builder",
        "scenario"
    };

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task EveryPublicPath_DragLowersA150KmOrbit(string path)
    {
        // Arrange: one hour at 150 km, where the density was negative
        var body = Mars();
        var window = new TimeSystem.Window(Epoch, Epoch.AddHours(1.0));
        var bodies = new CelestialItem[] { body };
        var step = TimeSpan.FromSeconds(1.0);
        var options = new CovariancePropagationOptions();
        var spacecraft = NewSpacecraft(Circular(body, 150e3), -4230);
        var reference = NewSpacecraft(Circular(body, 150e3), -4231);

        // Act: the same path without drag as the reference
        StateVector Final(Spacecraft s, bool drag) => path switch
        {
            "Velocity-Verlet" => s.Propagate(window, bodies, drag, false, step).StateVectors.Last(),
            "RK7(8)" => s.Propagate(window, bodies, new RK78Integrator(1e-12, 1e-12), drag, false, window.Length)
                .GetStateVector(window.EndDate),
            "RK7(8), state transition matrix" => s.Propagate(window, bodies, new RK78Integrator(1e-12, 1e-12), drag,
                false, window.Length, options).GetStateVector(window.EndDate),
            "RK7(8), IncludeInStepControl" => s.Propagate(window, bodies, new RK78Integrator(1e-12, 1e-12), drag, false,
                window.Length, options with { IncludeInStepControl = true }).GetStateVector(window.EndDate),
            "builder" => (drag
                    ? new CentralBodyPropagatorBuilder(window, s, new RK78Integrator(1e-12, 1e-12), window.Length)
                        .WithPerturbingBodies(bodies).IncludeAtmosphericDrag()
                    : new CentralBodyPropagatorBuilder(window, s, new RK78Integrator(1e-12, 1e-12), window.Length)
                        .WithPerturbingBodies(bodies))
                .Build().Propagate().GetStateVector(window.EndDate),
            _ => null
        };

        StateVector withDrag;
        StateVector withoutDrag;
        if (path == "scenario")
        {
            withDrag = await Simulate(spacecraft, body, window, true);
            withoutDrag = await Simulate(reference, body, window, false);
        }
        else
        {
            withDrag = Final(spacecraft, true);
            withoutDrag = Final(reference, false);
        }

        // Assert
        double decay = withDrag.SemiMajorAxis() - withoutDrag.SemiMajorAxis();
        _output.WriteLine($"{path}: semi-major axis {decay:F3} m against no drag over one hour");
        Assert.True(decay < 0.0, $"{decay} m");
    }

    private static async Task<StateVector> Simulate(Spacecraft spacecraft, CelestialBody body,
        TimeSystem.Window window, bool drag)
    {
        var scenario = new Scenario("marsdrag" + -spacecraft.NaifId + drag, new IO.Astrodynamics.Mission.Mission("marsdrag" + drag),
            window);
        scenario.AddCelestialItem(body);
        scenario.AddSpacecraft(spacecraft);
        await scenario.SimulateAsync(drag, false, TimeSpan.FromSeconds(1.0));
        return spacecraft.GetEphemeris(window.EndDate, body, Frames.Frame.ICRF, Aberration.None).ToStateVector();
    }

    // An elliptical orbit whose perigee lies below the base of the isothermal layer (49.887 km) and whose apoapsis lies
    // above 112.5 km, 10 min around the perigee, A = 0.01 m² and m = 1000 kg; RK7(8) with Φ and Ψ_Cd in the step
    // control. Without the transition of 5 km above the base, it stopped on the base at every tolerance (100 rejections
    // at the minimum step), where the curve fit alone, smooth there, took 24, 32 and 61 steps (2026-10-22, Linux)
    [Theory]
    [InlineData(1e-11)]
    [InlineData(1e-12)]
    [InlineData(1e-13)]
    public void AcrossTheBaseOfTheLayer_IncludeInStepControl_Completes(double tolerance)
    {
        // Arrange
        var body = Mars();
        double perigee = body.EquatorialRadius + 40e3;
        double apoapsis = body.EquatorialRadius + 400e3;
        double a = 0.5 * (perigee + apoapsis);
        double meanMotion = System.Math.Sqrt(body.GM / (a * a * a));
        var orbit = new KeplerianElements(a, (apoapsis - perigee) / (apoapsis + perigee),
            45.0 * IO.Astrodynamics.Constants.Deg2Rad, 0.0, 0.0, -300.0 * meanMotion, body, Epoch, Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(-4240, "MARSLAYER", 1000.0, 1000.0, new Clock("marslayer", 65536), orbit, 0.01,
            2.2, null, 1.0);
        var window = new TimeSystem.Window(Epoch, Epoch.AddSeconds(600.0));
        var options = new CovariancePropagationOptions { IncludeInStepControl = true };

        // Act
        var solution = spacecraft.Propagate(window, new CelestialItem[] { body },
            new RK78Integrator(tolerance, tolerance), true, false, window.Length, options);

        // Assert: the trajectory crosses the base of the layer, and the propagation reaches the end of its window
        var drag = new AtmosphericDrag(spacecraft, body);
        double lowest = Enumerable.Range(0, 601).Min(second =>
        {
            var state = solution.GetStateVector(Epoch.AddSeconds(second));
            return drag.Geodetic(state.Position, drag.BodyFixedRotation(state), state.Epoch).Altitude;
        });
        int steps = solution.Segments.Sum(s => s.Steps.Count);
        _output.WriteLine($"{tolerance:E0}: {steps} steps, lowest geodetic altitude {lowest / 1000.0:F3} km");
        Assert.True(lowest < MarsStandardAtmosphere.IsothermalLayerBase, $"lowest {lowest} m");
        Assert.NotNull(solution.GetStateTransitionMatrix(window.EndDate));
    }
}
