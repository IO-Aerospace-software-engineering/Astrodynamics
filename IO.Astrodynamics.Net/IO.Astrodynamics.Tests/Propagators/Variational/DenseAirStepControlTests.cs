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

    // Budgets at twice the worst steps measured on Linux, Windows and macOS arm64 (2026-10-21, Release, CI); the steps
    // differ between the platforms by up to a third (NRLMSISE-00 at 200 km and 1e-12: 26, 21 and 35). Before #420: US 1976 at 200 km, 107 290 steps at
    // 1e-11, more than 400 000 in 8.8 s and 0.71 s of flight at 1e-12 and 1e-13; NRLMSISE-00 at 200 km, 6 030 at 1e-11,
    // 28 699 in 24.7 s of flight at 1e-12; at 400 km, 6 362 at 1e-13; Mars at 100 km, more than 400 000 in 5.3 s of
    // flight at 1e-11, and 100 rejections at the start at 1e-12 and 1e-13. NRLMSISE-00 at 200 km starts on its 200 km
    // cutoff. The Mars budgets set again after #422 (2026-10-22, Release, CI): the states lie at 84.0 and 104.0 km of
    // geodetic altitude, in the isothermal layer of Seiff, whose density falls with a scale height of 7.4 km (d ln ρ/dh
    // −1.33e-4 /m) where the curve fit gave −5.5e-5 /m at 84 km and a density rising with the altitude above 101.4 km;
    // the steps follow that slope, not the density (Cd from 0.22 to 22: 257 to 294 steps at 80 km, 147 to 183 before).
    // 100 km at 1e-11, 1e-12, 1e-13: 11, 16, 121 (Linux), 11, 17, 150 (Windows), 11, 15, 155 (macOS arm64), 29, 78, 942
    // on Linux before #422; 80 km at 1e-13: 272, 294, 304, 142 on Linux before #422. The US 1976 budgets at 200 km set
    // again when the model became the U.S. Standard Atmosphere 1976 (#437; decision of S. Guillet, 2026-10-23): 6, 7, 10
    // steps at 1e-11, 1e-12, 1e-13 on Linux, Windows and macOS arm64 (CI of 945e92d), where the NASA Glenn curve fit,
    // 38.7 times denser at 200 km, took 8, 10 and 33 (budgets 16, 20, 66); at 400 km 9, as before.
    [Theory]
    [InlineData("US 1976", 200000.0, 1e-11, 12)]
    [InlineData("US 1976", 200000.0, 1e-12, 14)]
    [InlineData("US 1976", 200000.0, 1e-13, 20)]
    [InlineData("US 1976", 400000.0, 1e-13, 18)]
    [InlineData("NRLMSISE-00", 200000.0, 1e-11, 12)]
    [InlineData("NRLMSISE-00", 200000.0, 1e-12, 70)]
    [InlineData("NRLMSISE-00", 200000.0, 1e-13, 674)]
    [InlineData("NRLMSISE-00", 220000.0, 1e-13, 198)]
    [InlineData("NRLMSISE-00", 400000.0, 1e-13, 28)]
    [InlineData("Mars", 100000.0, 1e-11, 22)]
    [InlineData("Mars", 100000.0, 1e-12, 34)]
    [InlineData("Mars", 100000.0, 1e-13, 310)]
    [InlineData("Mars", 80000.0, 1e-13, 608)]
    public void IncludeInStepControl_InDenseAir_CompletesWithinItsBudget(string model, double altitude,
        double tolerance, int budget)
    {
        // Arrange
        var body = Body(model);

        // Act
        var solution = Propagate(body, altitude, tolerance, true);
        int steps = Steps(solution);
        int withoutTheOption = Steps(Propagate(body, altitude, tolerance, false));

        // Assert: the propagation reaches the end of its window within its budget
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

        // Assert: within the budget of 198 steps: 99, 101 and 101 on Linux, Windows and macOS arm64 with the shadow fraction
        // of #434 (CI of ab411fb); 102, 101 and 100 before it with the U.S. Standard Atmosphere 1976 (#437, CI of 945e92d;
        // the budget kept, decision of S. Guillet, 2026-10-23); 98, 99 and 99 with
        // the NASA Glenn curve fit (CI of aed79f2, 2026-10-22, with the derivative of the shadow function of #423); 120, 121
        // and 119 before it, 2026-10-21
        int steps = Steps(solution);
        _output.WriteLine($"Ψ_Cd and Ψ_Cr through the shadow: {steps} steps, {shadowed} minutes in the shadow cylinder");
        Assert.True(shadowed > 0, "the orbit never enters the shadow");
        Assert.True(steps <= 198, $"{steps} steps");
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

        // At 1e-11 the error of Y exceeds the tolerance at the minimum step, that of the state does not: the steps are
        // accepted there on the error of the state (#468), and the collapse guard stops it, its message naming Y (error
        // of Y 35.8, of the state 3.9e-4 at the last step; 2026-10-26, Linux). Before #468 the 100 rejections stopped it
        // at the start (error of Y 15.45 at 1e-6 s)
        var collapse = Assert.Throws<InvalidOperationException>(() => ssb.PropagateWithVariationalEquations(Window,
            bodies, new RK78Integrator(1e-11, 1e-11), true, false, Window.Length,
            new VariationalOptions(ForceParameters.DragCoefficient, IncludeInStepControl: true)));
        _output.WriteLine(collapse.Message);
        Assert.StartsWith("RK7(8) with the variational equations in the step control (IncludeInStepControl): 10000 " +
                          "consecutive steps over ", collapse.Message);
        Assert.EndsWith("Relax the tolerance, or set IncludeInStepControl to false.", collapse.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StateDrivenRejections_AtTheMinimumStep_StillStopAfter100Rejections(bool includeInStepControl)
    {
        // Arrange: a force noisy in time of up to 0.5 m/s², whose noise the error of the state sees at every step (above
        // 1 at the minimum step of 1e-6 s at 1e-12), and not Φ, its partials being zero. The acceptance at the minimum
        // step of #468 needs the state to meet its tolerance, so the 100-rejection guard still stops the propagation,
        // with or without the option, its message not naming Y
        var body = Body("US 1976");
        var integrator = new RK78Integrator(1e-12, 1e-12);
        integrator.AddForce(new JitterForce(1.0));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => new CentralBodyPropagator(Window,
            NewSpacecraft(body, 400e3), integrator, new CelestialItem[] { body }, false, false, Window.Length,
            new CovariancePropagationOptions { IncludeInStepControl = includeInStepControl }).Propagate());

        // Assert
        _output.WriteLine(exception.Message);
        Assert.StartsWith("RK78 adaptive step failed: 100 consecutive rejections at t=0.000000s. ", exception.Message);
        Assert.Contains("h=1.000000E-006s", exception.Message);
        Assert.EndsWith("Consider relaxing tolerances or checking force model.", exception.Message);
    }

    [Fact]
    public void YAndTheStateAboveTheTolerance_AtTheMinimumStep_TheRejectionsStillStopIt_WithoutNamingY()
    {
        // Arrange: two-body motion at 1e-20, below the rounding of the state: at the minimum step of 1e-6 s both the
        // error of the state (64.75) and that of Y (282) exceed 1 (2026-10-26, Linux). The state missing its tolerance, the
        // step is not accepted (#468), and the 100-rejection guard stops the propagation. Its message does not name Y: the
        // same propagation with the step on the state only stops on the same step, t = 0 and h = 1e-6 s, so the option is
        // not what stops it
        var body = Body("US 1976");
        InvalidOperationException Propagate(bool includeInStepControl) => Assert.Throws<InvalidOperationException>(
            () => NewSpacecraft(body, 400e3).Propagate(Window, new CelestialItem[] { body },
                new RK78Integrator(1e-20, 1e-20), false, false, Window.Length,
                new CovariancePropagationOptions { IncludeInStepControl = includeInStepControl }));
        static string Error(Exception exception) =>
            exception.Message.Split(' ').Single(word => word.StartsWith("Error=")).TrimEnd(',');

        // Act
        var exception = Propagate(true);
        var onTheState = Propagate(false);

        // Assert: the state misses its tolerance at the minimum step, and the rejections stop the propagation there
        _output.WriteLine(exception.Message);
        _output.WriteLine(onTheState.Message);
        _output.WriteLine($"MEASURE #468 two-body at 1e-20, the step of 1e-6 s at t = 0: {Error(onTheState)} for the " +
                          $"state, {Error(exception)} for the state and Y");
        foreach (var message in new[] { onTheState.Message, exception.Message })
        {
            Assert.StartsWith("RK78 adaptive step failed: 100 consecutive rejections at t=0.000000s. ", message);
            Assert.Contains("h=1.000000E-006s", message);
            Assert.EndsWith("Consider relaxing tolerances or checking force model.", message);
        }
    }

    [Theory]
    [InlineData(1e-2)]
    [InlineData(2e-2)]
    public void NoisyPartials_AMinimumStepOf10MsOrMore_TheRejectionsStillStopIt_NamingY(double minStepSize)
    {
        // Arrange: the noisy atmosphere at 1e-12, with a minimum step size of 10 ms or more. 10 000 steps at the minimum
        // step size then cover 100 s or more of flight, so the collapse guard cannot stop a run of them: the step that
        // only the error of Y rejects at the minimum step is not accepted on the error of the state (#468), and the
        // 100-rejection guard stops the propagation at the start, as before #468, its message naming Y, the state meeting
        // its tolerance (error of Y 121.8 at 10 ms, 331.7 at 20 ms; 2026-10-26, Linux). Accepted, the steps ran to the end
        // of the window, in 9 704 and 5 138 steps, with errors of Y up to 327 and 637
        var body = Body("noisy");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => NewSpacecraft(body, 200e3)
            .PropagateWithVariationalEquations(Window, new CelestialItem[] { body },
                new RK78Integrator(1e-12, 1e-12, minStepSize: minStepSize), true, false, Window.Length,
                new VariationalOptions(ForceParameters.DragCoefficient, IncludeInStepControl: true)));

        // Assert
        _output.WriteLine($"MEASURE #468 noisy atmosphere, minimum step {minStepSize} s: {exception.Message}");
        Assert.StartsWith("RK78 adaptive step failed: 100 consecutive rejections at t=0.000000s. ", exception.Message);
        Assert.Contains($"h={minStepSize.ToString("E6", System.Globalization.CultureInfo.InvariantCulture)}s",
            exception.Message);
        Assert.EndsWith("The error of the variational equations (IncludeInStepControl) drove the rejections.",
            exception.Message);
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

    // An acceleration of noise in time alone, up to half its amplitude: the force is not smooth, its partials, taken at
    // a fixed epoch, are zero
    private sealed class JitterForce(double amplitude) : ForceBase
    {
        public override Vector3 Apply(StateVector stateVector)
        {
            double noise = System.Math.Sin(stateVector.Epoch.TimeSpanFromJ2000().TotalSeconds * 1000.0) * 43758.5453;
            noise -= System.Math.Floor(noise) + 0.5;
            return new Vector3(amplitude * noise, 0.0, 0.0);
        }
    }

    [Fact]
    public void NoisyForce_TheStepCollapse_ThrowsAClearExceptionNamingTheState()
    {
        // Arrange: the noise of the force reaches the state, not Φ (error of the state 0.56, of Y 8.2e-8, measured
        // 2026-10-21)
        var body = Body("US 1976");
        var integrator = new RK78Integrator(1e-12, 1e-12);
        integrator.AddForce(new JitterForce(1e-7));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => new CentralBodyPropagator(Window,
            NewSpacecraft(body, 400e3), integrator, new CelestialItem[] { body }, false, false, Window.Length,
            new CovariancePropagationOptions { IncludeInStepControl = true }).Propagate());

        // Assert
        _output.WriteLine(exception.Message);
        Assert.StartsWith("RK7(8) with the variational equations in the step control (IncludeInStepControl): 10000 " +
                          "consecutive steps over ", exception.Message);
        Assert.EndsWith("The error of the state keeps the step small, not that of Φ and Ψ: a force is not smooth " +
                        "enough at this tolerance (a discontinuity of the force model or noise in it). Relax the " +
                        "tolerance.", exception.Message);
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
