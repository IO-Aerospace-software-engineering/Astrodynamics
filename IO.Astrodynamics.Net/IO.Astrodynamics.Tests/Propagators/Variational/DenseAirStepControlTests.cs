// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// RK7(8) with Φ and Ψ in the step control (<c>IncludeInStepControl</c>) in dense air (#420): the step no longer
/// collapses with the gradient of the density in geodetic coordinates, and a collapse from another cause stops with a
/// clear exception instead of exhausting the memory.
/// </summary>
/// <remarks>
/// The case of #420: a circular orbit, the position along x, the velocity (0, 0.6 v, 0.8 v), a point-mass body with drag
/// alone, A = 10 m², m = 100 kg, Cd = 2.2, two minutes of flight. Before #420 the US 1976 atmosphere at 200 km took
/// 107 290 steps at 1e-11 and did not finish at 1e-12 and 1e-13 (memory grew to an OutOfMemoryException over 1 h).
/// </remarks>
public class DenseAirStepControlTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);
    private static readonly TimeSystem.Window Window = new(Epoch, Epoch.AddSeconds(120.0));
    private readonly ITestOutputHelper _output;

    public DenseAirStepControlTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static CelestialBody Body(string model)
    {
        return model switch
        {
            "US 1976" => new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
                new EarthStandardAtmosphere()),
            "NRLMSISE-00" => new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
                new Nrlmsise00Model(SpaceWeather.Nominal)),
            "Mars" => new CelestialBody(SolarSystemObjects.PlanetsAndMoons.MARS, Frames.Frame.ICRF, Epoch, null,
                new MarsStandardAtmosphere()),
            _ => new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, null,
                new NoisyAtmosphere())
        };
    }

    private static Spacecraft NewSpacecraft(CelestialBody body, double altitude, int naifId = -1420)
    {
        double radius = body.EquatorialRadius + altitude;
        double speed = System.Math.Sqrt(body.GM / radius);
        var start = new StateVector(new Vector3(radius, 0.0, 0.0), new Vector3(0.0, 0.6 * speed, 0.8 * speed), body,
            Epoch, Frames.Frame.ICRF);
        return new Spacecraft(naifId, "DENSEAIR" + -naifId, 100.0, 1000.0, new Clock("denseair" + -naifId, 65536),
            start, 10.0, 2.2, null, 1.0);
    }

    private static int Steps(PropagationSolution solution) => solution.Segments.Sum(s => s.Steps.Count);

    private static PropagationSolution Propagate(CelestialBody body, double altitude, double tolerance,
        bool includeInStepControl, bool srp = false)
    {
        var parameters = ForceParameters.DragCoefficient | (srp ? ForceParameters.ReflectivityCoefficient : 0);
        return NewSpacecraft(body, altitude).PropagateWithVariationalEquations(Window, new CelestialItem[] { body },
            new RK78Integrator(tolerance, tolerance), true, srp, Window.Length,
            new VariationalOptions(parameters, IncludeInStepControl: includeInStepControl));
    }

    // Budgets at twice the steps measured (2026-10-21, Linux, Release). Before #420: US 1976 at 200 km, 107 290 steps at
    // 1e-11, more than 400 000 in 8.8 s and 0.71 s of flight at 1e-12 and 1e-13; NRLMSISE-00 at 200 km, 6 030 at 1e-11,
    // 28 699 in 24.7 s of flight at 1e-12; at 400 km, 6 362 at 1e-13; Mars at 100 km, more than 400 000 in 5.3 s of
    // flight at 1e-11, and 100 rejections at the start at 1e-12 and 1e-13. NRLMSISE-00 at 200 km starts on its 200 km
    // cutoff. Mars below 112.5 km, where its model is defined (#422).
    [Theory]
    [InlineData("US 1976", 200000.0, 1e-11, 16)]
    [InlineData("US 1976", 200000.0, 1e-12, 20)]
    [InlineData("US 1976", 200000.0, 1e-13, 66)]
    [InlineData("US 1976", 400000.0, 1e-13, 18)]
    [InlineData("NRLMSISE-00", 200000.0, 1e-11, 12)]
    [InlineData("NRLMSISE-00", 200000.0, 1e-12, 76)]
    [InlineData("NRLMSISE-00", 200000.0, 1e-13, 684)]
    [InlineData("NRLMSISE-00", 220000.0, 1e-13, 200)]
    [InlineData("NRLMSISE-00", 400000.0, 1e-13, 22)]
    [InlineData("Mars", 100000.0, 1e-11, 58)]
    [InlineData("Mars", 100000.0, 1e-12, 156)]
    [InlineData("Mars", 100000.0, 1e-13, 1884)]
    [InlineData("Mars", 80000.0, 1e-13, 284)]
    public void IncludeInStepControl_InDenseAir_CompletesWithinItsBudget(string model, double altitude,
        double tolerance, int budget)
    {
        // Arrange
        var body = Body(model);

        // Act
        var solution = Propagate(body, altitude, tolerance, true);
        int steps = Steps(solution);
        int withoutTheOption = Steps(Propagate(body, altitude, tolerance, false));

        // Assert: the propagation reaches the end of its window within twice the steps measured (2026-10-21, Linux)
        _output.WriteLine($"{model}, {altitude / 1000.0} km, {tolerance:E0}: {steps} steps, {withoutTheOption} " +
                          "without the option");
        Assert.True(steps <= budget, $"{steps} steps, budget {budget}");
        Assert.NotNull(solution.GetStateTransitionMatrix(Window.EndDate));
    }

    [Fact]
    public void EveryActivationPathAndBothModes_TakeTheSameSteps()
    {
        // Arrange: US 1976 at 200 km, 1e-12, Φ and Ψ_Cd in the step control, through every public path; P0 for the
        // covariance mode, which adds no term to the step control
        var body = Body("US 1976");
        var stateTransition = new CovariancePropagationOptions { IncludeInStepControl = true };
        var covariance = stateTransition with { Mode = CovariancePropagationMode.Covariance };
        var bodies = new CelestialItem[] { body };
        Spacecraft WithP0(int naifId)
        {
            var spacecraft = NewSpacecraft(body, 200e3, naifId);
            return new Spacecraft(naifId, spacecraft.Name, 100.0, 1000.0, new Clock("p0" + -naifId, 65536),
                CovariancePropagationTests.WithCovariance((StateVector)spacecraft.InitialOrbitalParameters), 10.0, 2.2,
                null, 1.0);
        }

        // Act
        var reference = Propagate(body, 200e3, 1e-12, true);
        var solutions = new (string Path, PropagationSolution Solution)[]
        {
            ("builder, state transition only", new CentralBodyPropagatorBuilder(Window, NewSpacecraft(body, 200e3, -1431),
                    new RK78Integrator(1e-12, 1e-12), Window.Length).WithPerturbingBodies(bodies).IncludeAtmosphericDrag()
                .WithCovariancePropagation(stateTransition).Build().Propagate()),
            ("builder, covariance", new CentralBodyPropagatorBuilder(Window, WithP0(-1432),
                    new RK78Integrator(1e-12, 1e-12), Window.Length).WithPerturbingBodies(bodies).IncludeAtmosphericDrag()
                .WithCovariancePropagation(covariance).Build().Propagate()),
            ("constructor", new CentralBodyPropagator(Window, NewSpacecraft(body, 200e3, -1433),
                new RK78Integrator(1e-12, 1e-12), bodies, true, false, Window.Length, stateTransition).Propagate()),
            ("spacecraft", NewSpacecraft(body, 200e3, -1434).Propagate(Window, bodies, new RK78Integrator(1e-12, 1e-12),
                true, false, Window.Length, covariance with { Mode = CovariancePropagationMode.StateTransitionOnly })),
            ("batch", BatchPropagator.Propagate(new[]
            {
                new PropagationTask(NewSpacecraft(body, 200e3, -1435), Window, bodies, true, false, Window.Length,
                    () => new RK78Integrator(1e-12, 1e-12)) { CovariancePropagation = stateTransition }
            }).Results[0].Solution)
        };

        // Assert: the steps and Φ at the end of the internal path, bit for bit
        int steps = Steps(reference);
        var phi = reference.GetStateTransitionMatrix(Window.EndDate);
        foreach (var (path, solution) in solutions)
        {
            _output.WriteLine($"{path}: {Steps(solution)} steps ({steps} by the internal path)");
            Assert.Equal(steps, Steps(solution));
            var other = solution.GetStateTransitionMatrix(Window.EndDate);
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    Assert.Equal(phi.Get(i, j), other.Get(i, j));
                }
            }
        }
    }

    [Fact]
    public void DragAndSrpWithEclipses_PsiCdAndPsiCr_CompleteWithinTheirBudget()
    {
        // Arrange: US 1976 at 400 km over one hour, with the Sun: drag and SRP, Ψ_Cd and Ψ_Cr, through the shadow of the
        // Earth (the orbit starts on the sunlit side at the equinox and crosses the shadow)
        var body = Body("US 1976");
        var spacecraft = NewSpacecraft(body, 400e3, -1441);
        var window = new TimeSystem.Window(Epoch, Epoch.AddHours(1.0));

        // Act
        var solution = spacecraft.PropagateWithVariationalEquations(window,
            new CelestialItem[] { body, SolarSystemObjects.Stars.SUN_BODY }, new RK78Integrator(1e-12, 1e-12), true,
            true, window.Length, new VariationalOptions(ForceParameters.DragCoefficient |
                                                         ForceParameters.ReflectivityCoefficient,
                IncludeInStepControl: true));
        int shadowed = Enumerable.Range(1, 59).Count(minute =>
        {
            var state = solution.GetStateVector(Epoch.AddMinutes(minute));
            return state.Position.X < 0.0 && System.Math.Sqrt(state.Position.Y * state.Position.Y +
                                                               state.Position.Z * state.Position.Z) < body.EquatorialRadius;
        });

        // Assert: within twice the steps measured (120, 2026-10-21, Linux)
        int steps = Steps(solution);
        _output.WriteLine($"Ψ_Cd and Ψ_Cr through the shadow: {steps} steps, {shadowed} minutes in the shadow cylinder");
        Assert.True(shadowed > 0, "the orbit never enters the shadow");
        Assert.True(steps <= 240, $"{steps} steps");
        Assert.Equal((6, 2), (solution.GetParameterSensitivity(window.EndDate).Rows,
            solution.GetParameterSensitivity(window.EndDate).Columns));
    }

    [Fact]
    public void SsbMode_TheStepCollapse_ThrowsAClearException()
    {
        // Arrange: the same state relative to the solar system barycenter. In SSB mode the step control on Φ and Ψ fails
        // with or without drag: the columns of Y are scaled by |r0| relative to the barycenter, about 1.5e11 m instead of
        // 6.6e6 m, and the state alone takes 75 183 steps over 2 min at 1e-11 (#389). At 1e-9 the step collapsed without
        // end before #420; the guard now stops it
        var body = Body("US 1976");
        var geocentric = NewSpacecraft(body, 400e3, -1451);
        var ssbState = ((StateVector)geocentric.InitialOrbitalParameters)
            .RelativeTo(SolarSystemObjects.Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None).ToStateVector();
        var ssb = new Spacecraft(-1452, "DENSEAIRSSB", 100.0, 1000.0, new Clock("denseairssb", 65536), ssbState, 10.0,
            2.2, null, 1.0);
        var bodies = new CelestialItem[] { body, SolarSystemObjects.Stars.SUN_BODY };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => ssb.PropagateWithVariationalEquations(Window,
            bodies, new RK78Integrator(1e-9, 1e-9), true, false, Window.Length,
            new VariationalOptions(ForceParameters.DragCoefficient, IncludeInStepControl: true)));

        // Assert
        _output.WriteLine(exception.Message);
        Assert.StartsWith("RK7(8) with the variational equations in the step control (IncludeInStepControl)",
            exception.Message);

        // At 1e-11 the rejections stop it at the start, their message naming the variational equations
        var rejections = Assert.Throws<InvalidOperationException>(() => ssb.PropagateWithVariationalEquations(Window,
            bodies, new RK78Integrator(1e-11, 1e-11), true, false, Window.Length,
            new VariationalOptions(ForceParameters.DragCoefficient, IncludeInStepControl: true)));
        Assert.StartsWith("RK78 adaptive step failed: 100 consecutive rejections", rejections.Message);
        Assert.EndsWith("The error of the variational equations (IncludeInStepControl) drove the rejections.",
            rejections.Message);
    }

    // An exponential atmosphere whose density carries a deterministic relative noise of 1e-10, far above the rounding of
    // a model, so that its derivatives, and so the error of Y, are noisy at any step
    private sealed class NoisyAtmosphere : IAtmosphericModel, IGeodeticDensityModel
    {
        private static double Density(double altitude)
        {
            double noise = System.Math.Sin(altitude * 1000.0) * 43758.5453;
            noise -= System.Math.Floor(noise) + 0.5;
            return 1e-10 * System.Math.Exp(-(altitude - 200000.0) / 40000.0) * (1.0 + 1e-10 * noise);
        }

        double IGeodeticDensityModel.GetDensity(double altitude, double geodeticLatitude, double geodeticLongitude,
            in TimeSystem.Time epoch) => Density(altitude);

        public IO.Astrodynamics.Atmosphere.Atmosphere GetAtmosphere(IAtmosphericContext context) => new()
            { Temperature = 0.0, Pressure = 0.0, Density = Density(context.Altitude) };

        public double GetTemperature(IAtmosphericContext context) => 0.0;

        public double GetPressure(IAtmosphericContext context) => 0.0;

        public double GetDensity(IAtmosphericContext context) => Density(context.Altitude);
    }

    [Fact]
    public void NoisyPartials_TheStepCollapse_ThrowsAClearException()
    {
        // Arrange
        var body = Body("noisy");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => Propagate(body, 200e3, 1e-12, true));
        var withoutTheOption = Propagate(body, 200e3, 1e-12, false);

        // Assert
        _output.WriteLine(exception.Message);
        Assert.StartsWith("RK7(8) with the variational equations in the step control (IncludeInStepControl): 10000 " +
                          "consecutive steps over ", exception.Message);
        Assert.EndsWith("Relax the tolerance, or set IncludeInStepControl to false.", exception.Message);
        Assert.Equal(Window.EndDate, withoutTheOption.Segments[^1].BaseEpoch.AddSeconds(
            withoutTheOption.Segments[^1].Steps[^1].CumulativeTime + withoutTheOption.Segments[^1].Steps[^1].StepSize));
    }
}
