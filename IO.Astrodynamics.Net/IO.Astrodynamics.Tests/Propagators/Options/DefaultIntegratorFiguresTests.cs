// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Diagnostics;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Options;

/// <summary>
/// The figures of R07 of #668 (structuring choice 5, D4.1) measured again in lot L4 and printed as MEASURE lines, so that
/// Linux, Windows and macOS arm64 are read from the CI logs (decision 12 of lot L4, decided by S. Guillet, 2026-11-22):
/// the distance to the Keplerian closed form after a point-mass LEO day (Velocity-Verlet at 60 s, 10 s and 1 s, RK7(8) at
/// 1e-9 and 1e-12), the Hohmann transfer to GEO against its closed form, the cost of the SSB mode at 1e-9 and 1e-12, the
/// cost of the covariance under Auto, and the memory a spacecraft keeps with its last propagation. The docs publish them;
/// each test asserts only what the mathematics fixes (the order of the errors, the closed-form Δv to the accuracy of the
/// integrator, an unchanged trajectory), no threshold on a figure until the three platforms are read, none ever on the
/// memory.
/// </summary>
public class DefaultIntegratorFiguresTests
{
    private static readonly TimeSystem.Time Start = new(2024, 6, 21, 12, 0, 0, TimeScale.TDB);
    private readonly ITestOutputHelper _output;

    public DefaultIntegratorFiguresTests(ITestOutputHelper output)
    {
        TestKernels.EnsureSolarSystem();
        _output = output;
    }

    private static int _nextId = -1794000;

    private static Spacecraft NewSpacecraft(IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit, double dryMass = 500.0,
        double maximumMass = 3000.0)
    {
        int id = System.Threading.Interlocked.Decrement(ref _nextId);
        return new Spacecraft(id, $"FIG{-id}", dryMass, maximumMass, new Clock($"fig{-id}", 65536), orbit);
    }

    // The state of the tutorial (docs/tutorials/numerical-propagation.md): 6 800 km, 7 700 m/s
    private static StateVector TutorialLeo(CelestialBody earth, Matrix? covariance = null) =>
        new(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7700.0, 0.0), earth, Start, Frames.Frame.ICRF, covariance);

    private static PropagationOptions Options(IntegratorOptions integrator) => new()
    {
        Integrator = integrator, OutputStep = TimeSpan.FromSeconds(60.0)
    };

    /// <summary>
    /// A point-mass LEO day: the distance of the last state to the Keplerian closed form (<see cref="StateVector.AtEpoch"/>,
    /// TT seconds about the Earth, #508) for Velocity-Verlet at 60 s (the step of the members removed in 11.0.0), 10 s and
    /// 1 s (<see cref="VVIntegratorOptions.Default"/>), RK7(8) at <see cref="RK78IntegratorOptions.Default"/> (1e-9) and at
    /// 1e-12. Velocity-Verlet is second order: its error grows as h², 3 600 times from 1 s to 60 s.
    /// </summary>
    [Fact]
    public void PointMassLeoDay_AgainstKepler()
    {
        // Arrange
        var earth = PlanetsAndMoons.EARTH_BODY;
        var window = new Window(Start, Start.AddDays(1.0));
        var kepler = TutorialLeo(earth).AtEpoch(window.EndDate).ToStateVector();
        var cases = new (string Name, IntegratorOptions Integrator)[]
        {
            ("VV 60 s", new VVIntegratorOptions { Step = TimeSpan.FromSeconds(60.0) }),
            ("VV 10 s", new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) }),
            ("VV 1 s (VVIntegratorOptions.Default)", VVIntegratorOptions.Default),
            ("RK7(8) 1e-9 (RK78IntegratorOptions.Default)", RK78IntegratorOptions.Default),
            ("RK7(8) 1e-12", new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 })
        };

        // Act
        var errors = new double[cases.Length];
        for (int i = 0; i < cases.Length; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var solution = NewSpacecraft(TutorialLeo(earth)).Propagate(window, Options(cases[i].Integrator));
            stopwatch.Stop();
            errors[i] = (solution.StateVectors[^1].Position - kepler.Position).Magnitude();
            _output.WriteLine($"MEASURE #668 L4 point-mass LEO day, {cases[i].Name}: {errors[i]:E6} m from Kepler, " +
                              $"{stopwatch.Elapsed.TotalMilliseconds:F1} ms");
        }

        // Assert: Velocity-Verlet's error decreases with its step, RK7(8) at its defaults is below Velocity-Verlet at 1 s,
        // and tighter tolerances are closer
        Assert.True(errors[0] > errors[1] && errors[1] > errors[2], string.Join(", ", errors));
        Assert.True(errors[3] < errors[2] && errors[4] < errors[3], string.Join(", ", errors));
    }

    /// <summary>
    /// A Hohmann transfer from a 6 678.137 km circular orbit to 42 164 km (Isp 450 s, 50 kg/s, 1 000 kg dry, 5 000 kg of
    /// fuel): the Δv of each burn, its thrust start and the final eccentricity and semi-major axis, for Velocity-Verlet at
    /// 60 s and RK7(8) at its defaults, against the closed form √(μ/r) of the circular and transfer orbits (vis-viva, the
    /// kernel GM).
    /// </summary>
    [Fact]
    public void HohmannToGeo_AgainstTheClosedForm()
    {
        // Arrange
        var earth = PlanetsAndMoons.EARTH_BODY;
        double r1 = 6678137.0, r2 = 42164000.0, mu = earth.GM;
        double transfer = r1 + r2;
        double dv1 = System.Math.Sqrt(mu * (2.0 / r1 - 2.0 / transfer)) - System.Math.Sqrt(mu / r1);
        double dv2 = System.Math.Sqrt(mu / r2) - System.Math.Sqrt(mu * (2.0 / r2 - 2.0 / transfer));
        _output.WriteLine($"MEASURE #668 L4 Hohmann closed form: dv1 {dv1:F6} m/s, dv2 {dv2:F6} m/s");
        var window = new Window(Start, Start.AddDays(1.0));

        foreach (var (name, integrator) in new (string, IntegratorOptions)[]
                 {
                     ("VV 60 s", new VVIntegratorOptions { Step = TimeSpan.FromSeconds(60.0) }),
                     ("RK7(8) 1e-9", RK78IntegratorOptions.Default)
                 })
        {
            var orbit = new StateVector(new Vector3(r1, 0.0, 0.0), new Vector3(0.0, System.Math.Sqrt(mu / r1), 0.0), earth,
                Start, Frames.Frame.ICRF);
            var spacecraft = NewSpacecraft(orbit, 1000.0, 10000.0);
            var tank = new FuelTank("hohmann", "model", "sn", 9000.0, 5000.0);
            spacecraft.AddFuelTank(tank);
            var engine = new Engine("hohmann", "model", "sn", 450.0, 50.0, tank);
            spacecraft.AddEngine(engine);
            var raise = new ApogeeHeightManeuver(earth, Start, TimeSpan.Zero, r2, engine);
            raise.SetNextManeuver(new PerigeeHeightManeuver(earth, Start, TimeSpan.Zero, r2, engine));
            spacecraft.SetStandbyManeuver(raise);

            // Act
            var solution = spacecraft.Propagate(window, Options(integrator));

            // Assert
            var burns = spacecraft.ExecutedManeuvers.OfType<ImpulseManeuver>().ToArray();
            Assert.Equal(2, burns.Length);
            var final = solution.StateVectors[^1].ToKeplerianElements();
            _output.WriteLine($"MEASURE #668 L4 Hohmann {name}: burn 1 thrust start {burns[0].ThrustWindow!.Value.StartDate}, " +
                              $"dv {burns[0].DeltaV.Magnitude():F6} m/s; burn 2 thrust start " +
                              $"{burns[1].ThrustWindow!.Value.StartDate}, dv {burns[1].DeltaV.Magnitude():F6} m/s; final e " +
                              $"{final.E:E3}, a {final.A:F3} m");
            if (integrator is RK78IntegratorOptions)
            {
                // RK7(8) at its defaults gives the closed form to the millimetre per second
                Assert.Equal(dv1, burns[0].DeltaV.Magnitude(), 3);
                Assert.Equal(dv2, burns[1].DeltaV.Magnitude(), 3);
            }
        }
    }

    /// <summary>
    /// The SSB mode about the Earth: a geocentric LEO propagated about the Solar System barycentre with the Earth, the Moon
    /// and the Sun over 6 hours, RK7(8) at 1e-9 and 1e-12 (which completes since #576) and Velocity-Verlet at 10 s and 60 s:
    /// the cost per propagated hour and the accepted steps.
    /// </summary>
    [Fact]
    public void SsbMode_CostPerPropagatedHour()
    {
        // Arrange
        var earth = PlanetsAndMoons.EARTH_BODY;
        var geocentric = TutorialLeo(earth);
        var ssb = geocentric.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None).ToStateVector();
        var window = new Window(Start, Start.AddHours(6.0));
        var bodies = new CelestialItem[] { earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY };

        foreach (var (name, integrator) in new (string, IntegratorOptions)[]
                 {
                     ("RK7(8) 1e-9", RK78IntegratorOptions.Default),
                     ("RK7(8) 1e-12", new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }),
                     ("VV 10 s", new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) }),
                     ("VV 60 s", new VVIntegratorOptions { Step = TimeSpan.FromSeconds(60.0) })
                 })
        {
            // Act: the best of two runs
            double best = double.MaxValue;
            int steps = 0;
            for (int run = 0; run < 2; run++)
            {
                var stopwatch = Stopwatch.StartNew();
                var solution = NewSpacecraft(ssb).Propagate(window, Options(integrator) with { ThirdBodies = bodies });
                stopwatch.Stop();
                best = System.Math.Min(best, stopwatch.Elapsed.TotalMilliseconds);
                steps = solution.Segments.Sum(segment => segment.Steps.Count);
            }

            // Assert
            Assert.True(steps > 0);
            _output.WriteLine($"MEASURE #668 L4 SSB mode {name}: {best / 6.0:F2} ms per propagated hour, {steps} steps");
        }
    }

    /// <summary>
    /// The cost of the covariance under Auto (<see cref="PropagationOptions.CovariancePropagation"/> null, a covariance at
    /// the window start) against <see cref="CovariancePropagationOptions.None"/>, over a LEO day at the RK7(8) defaults:
    /// about the point-mass Earth, with EGM2008 10×10 and drag, and in SSB mode (section F of the design of #668). The
    /// trajectory is the same, bit for bit: the variational equations are not in the step control.
    /// </summary>
    [Fact]
    public void CovarianceAuto_Cost()
    {
        var covariance = Matrix.Diagonal(100.0, 100.0, 100.0, 1e-4, 1e-4, 1e-4);
        var window = new Window(Start, Start.AddDays(1.0));
        var earth = PlanetsAndMoons.EARTH_BODY;
        var geopotentialEarth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
            GeopotentialModelParameters.Egm2008(10));
        var barycentric = TutorialLeo(earth).RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None)
            .ToStateVector();
        var ssb = new StateVector(barycentric.Position, barycentric.Velocity, barycentric.Observer, barycentric.Epoch,
            barycentric.Frame, covariance);
        foreach (var (name, orbit, options) in new (string, StateVector, PropagationOptions)[]
                 {
                     ("point mass", TutorialLeo(earth, covariance), Options(RK78IntegratorOptions.Default)),
                     ("EGM2008 10x10 and drag", TutorialLeo(geopotentialEarth, covariance),
                         Options(RK78IntegratorOptions.Default) with { AtmosphericDrag = true }),
                     ("SSB mode, Earth, Moon and Sun", ssb, Options(RK78IntegratorOptions.Default) with
                     {
                         ThirdBodies = new CelestialItem[] { earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }
                     })
                 })
        {
            // Act: the best of five runs of each (two left the point-mass ratio between 1.6 and 4.4 over Linux runs of 15 to
            // 70 ms, the noise of the timer and of the tests running beside it)
            double auto = double.MaxValue, none = double.MaxValue;
            PropagationSolution withCovariance = null, without = null;
            for (int run = 0; run < 5; run++)
            {
                var stopwatch = Stopwatch.StartNew();
                withCovariance = NewSpacecraft(orbit).Propagate(window, options);
                auto = System.Math.Min(auto, stopwatch.Elapsed.TotalMilliseconds);
                stopwatch.Restart();
                without = NewSpacecraft(orbit).Propagate(window,
                    options with { CovariancePropagation = CovariancePropagationOptions.None });
                none = System.Math.Min(none, stopwatch.Elapsed.TotalMilliseconds);
            }

            // Assert
            Assert.NotNull(withCovariance.StateVectors[^1].Covariance);
            Assert.Null(without.StateVectors[^1].Covariance);
            Assert.True(withCovariance.StateVectors.Select(s => s.Position).SequenceEqual(without.StateVectors.Select(s => s.Position)));
            _output.WriteLine($"MEASURE #668 L4 covariance Auto, {name}: {auto:F1} ms against {none:F1} ms with None, " +
                              $"{auto / none:F2} times");
        }
    }

    /// <summary>
    /// The memory a spacecraft keeps with the solution and the force model of its last RK7(8) propagation (the internal
    /// <c>LastPropagation</c> and <c>LastForceModel</c>, read by the conjunction analysis): a LEO day with EGM2008 10×10,
    /// the Moon, the Sun and drag, at 1e-11 and at the defaults, and at the defaults with an initial covariance, which Auto
    /// propagates in the run (decision 17 of lot L4), so that the solution kept carries a covariance per output: the memory
    /// an explicit request always kept, the case of the replay for the conjunction analysis. Published without a threshold
    /// (decision 12 of lot L4).
    /// </summary>
    // Outside the measuring method, so that no reference to the solution or its segments is left in a local or a register of
    // that method, which kept part of the solution alive across the collections (measured: 0.03 MB instead of 0.72 MB
    // released with an initial covariance when the steps were read there)
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int StepsOfLastPropagation(Spacecraft spacecraft) =>
        spacecraft.LastPropagation.Segments.Sum(segment => segment.Steps.Count);

    [Fact]
    public void LastPropagation_Memory()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
            GeopotentialModelParameters.Egm2008(10));
        var window = new Window(Start, Start.AddDays(1.0));
        // A first propagation of the process leaves part of its solution held elsewhere (lazy shared data), so a warm-up run
        // comes first and is not printed
        var covariance = Matrix.Diagonal(100.0, 100.0, 100.0, 1e-4, 1e-4, 1e-4);
        foreach (var (name, tolerance, initialCovariance) in new (string, double, Matrix?)[]
                 {
                     (null, 1e-9, null), ("RK7(8) 1e-11", 1e-11, null), ("RK7(8) 1e-9 (default)", 1e-9, null),
                     ("RK7(8) 1e-9 (default), initial covariance, Auto", 1e-9, covariance)
                 })
        {
            // Arrange
            var spacecraft = NewSpacecraft(TutorialLeo(earth, initialCovariance));
            var options = Options(new RK78IntegratorOptions { AbsoluteTolerance = tolerance, RelativeTolerance = tolerance })
                with
                {
                    ThirdBodies = new CelestialItem[] { PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, AtmosphericDrag = true
                };
            spacecraft.Propagate(window, options);
            int steps = StepsOfLastPropagation(spacecraft);

            // Act: the memory held with the last propagation, then without it
            long held = GC.GetTotalMemory(true);
            spacecraft.LastPropagation = null;
            long withoutSolution = GC.GetTotalMemory(true);
            spacecraft.LastForceModel = null;
            long released = GC.GetTotalMemory(true);
            GC.KeepAlive(spacecraft);

            // Assert
            Assert.True(steps > 0);
            Assert.Equal(initialCovariance.HasValue, spacecraft.PropagatedStates[^1].Covariance.HasValue);
            if (name == null)
            {
                continue;
            }

            _output.WriteLine($"MEASURE #668 L4 memory of the last propagation, LEO day, EGM2008 10x10, Moon, Sun, drag, " +
                              $"{name}: {(held - released) / 1048576.0:F3} MB, of which LastPropagation " +
                              $"{(held - withoutSolution) / 1048576.0:F3} MB and LastForceModel " +
                              $"{(withoutSolution - released) / 1048576.0:F3} MB ({steps} steps)");
        }
    }
}
