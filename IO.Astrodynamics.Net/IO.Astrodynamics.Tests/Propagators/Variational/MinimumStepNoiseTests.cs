// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// RK7(8) with Φ and Ψ in the step control (<c>IncludeInStepControl</c>) on the first steps of a segment in dense air
/// (#468): a step at the minimum step size whose state meets its tolerance is accepted on the error of the state,
/// whatever the error of Y, when its retry would repeat it and the minimum step size is below 10 ms, so the propagation
/// no longer stops at random on the rounding noise of the stage Jacobians.
/// </summary>
/// <remarks>
/// The case of <see cref="StandardAtmosphereBoundaryTests"/> at R_eq + 8 km (A = 1 m², m = 1000 kg, US 1976 drag, a
/// point-mass Earth) at 1e-13. Its drag reaches 400 g at the start, so its velocity falls from 8.0 to 1.6 km/s within
/// 10 s; the trajectory reaches the surface 172 s after the start. On the first steps of a segment the error of Φ_vr,
/// which starts at 0, is the rounding noise of the drag Jacobian across the 13 stages, 1 to 13 times the tolerance at
/// any step down to the minimum one (the issue).
/// </remarks>
public class MinimumStepNoiseTests
{
    private const double Tolerance = 1e-13;
    private const double ReferenceTolerance = 1e-14;

    // The longest step of AfterAManeuver_ASegmentStartingInDenseAir_Completes, under half the 0.114 s its trajectory spends
    // above the target plane before the node
    private const double MaximumStepAcrossTheNode = 0.05;

    private static readonly TimeSystem.Time Epoch = StandardAtmosphereBoundaryTests.Epoch;
    private static readonly TimeSystem.Window Window = StandardAtmosphereBoundaryTests.Window;
    private readonly ITestOutputHelper _output;

    public MinimumStepNoiseTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static PropagationSolution Propagate(CelestialBody body, StateVector start, double tolerance,
        CovariancePropagationOptions options)
    {
        return new Spacecraft(-4360, "MINSTEP4360", 1000.0, 1000.0, new Clock("minstep4360", 65536), start, 1.0, 2.2,
            null, 1.0).Propagate(Window, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = tolerance, RelativeTolerance = tolerance }, OutputStep = Window.Length, ThirdBodies = new CelestialItem[] { body }, AtmosphericDrag = true, CovariancePropagation = options ?? CovariancePropagationOptions.None });
    }

    /// <summary>
    /// Φ of the start that stopped at the minimum step, now propagated with the option, against central differences of
    /// whole propagations at 1e-14 from the exact perturbed states (the reference of
    /// <see cref="NonConservativeVariationalTests"/>), at four epochs before the trajectory reaches the surface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The steps of the differences come from their plateau (2026-10-26, Linux): the worst error of the position columns
    /// over the four epochs is 1.8e-6, 1.6e-7, 2.9e-8, 1.7e-7, 1.6e-6 and 1.7e-5 for 0.1, 1, 3, 10, 30 and 100 m, their
    /// δ² truncation from 10 m on and the noise of the reference propagations below; that of the velocity columns is
    /// 1.4e-4, 2.7e-5, 2.9e-7, 1.3e-7, 1.8e-7, 1.6e-6 and 1.8e-5 for 1e-4, 1e-3, 0.1, 0.3, 1, 3 and 10 m/s. Hence 3 m
    /// and 0.3 m/s.
    /// </para>
    /// <para>
    /// The central differences resolve Φ to about 1e-7: the same comparison gives the same errors, to three digits, for
    /// the propagation at 1e-14 with the step on the state only, which takes no step at the minimum step size (its
    /// smallest is 7.5e-4 s). That propagation, the reference of Ψ_Cr at the noise floor (A2), resolves what the steps
    /// accepted on the error of the state can change, so the test also bounds Φ against it: within 2.7e-10 (CI run
    /// 44225490669: 8.32e-11 on Linux x64, 1.34e-10 on Windows x64, 1.78e-11 on macOS arm64).
    /// </para>
    /// <para>
    /// That measure is noise between two step sequences: steps accepted at the minimum step on the error of the state, the
    /// error of Φ_vr at the rounding noise of the drag Jacobian. The exact SLERP of the orientation cache (#561), from which
    /// the drag's co-rotating atmosphere reads the Earth's orientation, redrew it: 4.95e-11 on Linux x64, 3.50e-11 on
    /// Windows x64 and 1.03e-11 on macOS arm64 on main a85098d (run 44055569759), threshold 9.1e-11. Starts one to 40 ulps
    /// apart on Linux x64 give 2.9e-12 to 2.30e-10 after #561 (median 3.37e-11) and 4.9e-12 to 6.35e-10 before it (median
    /// 2.85e-11): the distribution did not grow, its tail is heavy, and a later redraw of the dynamics may cross 2.7e-10
    /// without a defect. Against the central differences the error stays 1.07e-7 to 1.14e-7 on every platform.
    /// </para>
    /// </remarks>
    [Fact]
    public void FormerFailure_Phi_MatchesCentralDifferencesOfWholePropagations()
    {
        // Arrange
        var body = StandardAtmosphereBoundaryTests.Body("Earth");
        var start = StandardAtmosphereBoundaryTests.StateThatStoppedAtTheMinimumStep(body);
        var epochs = new[] { 1.0, 10.0, 60.0, 150.0 }.Select(t => Epoch.AddSeconds(t)).ToArray();
        var solution = Propagate(body, start, Tolerance, new CovariancePropagationOptions { IncludeInStepControl = true });
        var onTheStateSteps = Propagate(body, start, ReferenceTolerance, new CovariancePropagationOptions());

        // Act
        var expected = WholePropagationDifferences.Phi(state => Propagate(body, state, ReferenceTolerance, null), start,
            3.0, 0.3, epochs);

        // Assert: the thresholds are twice the worst errors over Linux x64, Windows x64 and macOS arm64 (CI run
        // 44225490669, after #561), rounded up at the second significant digit: 1.14e-7 against the central differences
        // (on Linux; 1.07e-7 on Windows, 7.67e-8 on macOS arm64; 1.05e-7 on main, 1.26e-7 on Linux in run 40317789704 of
        // 2026-10-26), 1.34e-10 against Φ at 1e-14 on the steps of the state (on Windows; 8.32e-11 on Linux, 1.78e-11 on
        // macOS arm64; see the remarks)
        double worst = 0.0;
        double worstOnTheStateSteps = 0.0;
        for (int k = 0; k < epochs.Length; k++)
        {
            var phi = StmMeasures.Elements(solution.GetStateTransitionMatrix(epochs[k]));
            var errors = StmMeasures.BlockErrors(phi, expected[k]);
            double onTheState = StmMeasures.WorstBlockError(phi,
                StmMeasures.Elements(onTheStateSteps.GetStateTransitionMatrix(epochs[k])));
            _output.WriteLine($"MEASURE #468 Φ of the former failure at {(epochs[k] - Epoch).TotalSeconds} s against " +
                              $"central differences at 1e-14: Φ_rr {errors[0]:E2}, Φ_rv {errors[1]:E2}, Φ_vr " +
                              $"{errors[2]:E2}, Φ_vv {errors[3]:E2}; against Φ at 1e-14 on the steps of the state " +
                              $"{onTheState:E2}");
            worst = System.Math.Max(worst, errors.Max());
            worstOnTheStateSteps = System.Math.Max(worstOnTheStateSteps, onTheState);
        }

        _output.WriteLine($"MEASURE #468 Φ of the former failure, worst over the epochs: {worst:E2} against the " +
                          $"central differences, {worstOnTheStateSteps:E2} against Φ at 1e-14 on the steps of the state");
        Assert.True(worst < 2.3e-7, $"Φ against the central differences: {worst:E2}");
        Assert.True(worstOnTheStateSteps < 2.7e-10, $"Φ against Φ at 1e-14: {worstOnTheStateSteps:E2}");
    }

    [Fact]
    public void AFirstStepBelowTheMinimumStepSize_RejectedOnY_IsRetriedAtTheMinimumStepSize()
    {
        // Arrange: the start of #468 at 1e-14, its first step 9.5e-7 s, below the minimum step size of 1e-6 s. There the
        // error of Y is the rounding noise of the drag Jacobian, ten times its value at 1e-13 whatever the step (71.18 at
        // 1e-6 s), while the state meets its tolerance. The retry of a step below the minimum step size is a different
        // step, at the minimum step size, as before #468; only a step that its retry would repeat is accepted on the
        // error of the state, so a propagation that completed before takes the same steps. Accepted at 9.5e-7 s, the
        // steps would change: the case at R_eq + 8 km at 1e-13 with a first step of 9.5e-7 s, its start moved by 0 to
        // 399 mm, completed 9 times in 400 before #468, and one of them then took other steps (2026-10-26, Linux)
        var body = StandardAtmosphereBoundaryTests.Body("Earth");
        var window = new TimeSystem.Window(Epoch, Epoch.AddSeconds(0.05));

        // Act
        var solution = new Spacecraft(-4363, "MINSTEP4363", 1000.0, 1000.0, new Clock("minstep4363", 65536),
            StandardAtmosphereBoundaryTests.StateThatStoppedAtTheMinimumStep(body), 1.0, 2.2, null, 1.0).Propagate(window, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = ReferenceTolerance, RelativeTolerance = ReferenceTolerance, InitialStep = TimeSpan.FromSeconds(9.5e-7) }, OutputStep = window.Length, ThirdBodies = new CelestialItem[] { body }, AtmosphericDrag = true, CovariancePropagation = new CovariancePropagationOptions { IncludeInStepControl = true } });

        // Assert: the first step is the minimum step size, and the propagation reaches the end of its window
        var steps = solution.Segments.SelectMany(s => s.Steps).ToArray();
        _output.WriteLine($"{steps.Length} steps, the first " +
                          string.Join(", ", steps.Take(3).Select(s => s.StepSize.ToString("R"))));
        Assert.Equal(1e-6, steps[0].StepSize);
        WindowEndAssert.ReachesTheEndOf(window, solution);
    }

    [Fact]
    public void AfterAManeuver_ASegmentStartingInDenseAir_Completes()
    {
        // Arrange: the case at R_eq + 8 km from its own start, with a plane alignment at the node of a target plane that
        // contains the position at 2.49 s (the state there, its velocity rotated by 1e-3 rad about the position; hard-coded
        // from the propagation without the burn, 2026-10-26): about 3.7 m/s at 2.38 s, so the segment after the burn starts
        // in the dense air at 22 km, a new draw of the noise. On main (2026-10-26, Linux) RK7(8) stopped 2 µs into that
        // segment (100 rejections, error of Y 1.694 at 1e-6 s), as for 4 of 300 such targets from 0.01 to 3 s
        var body = StandardAtmosphereBoundaryTests.Body("Earth");
        var orbit = StandardAtmosphereBoundaryTests.NewSpacecraft(body, 8e3, 1.0, -4361).InitialOrbitalParameters;
        var spacecraft = new Spacecraft(-4362, "MINSTEP4362", 990.0, 2000.0, new Clock("minstep4362", 65536), orbit,
            1.0, 2.2, null, 1.0);
        var tank = new FuelTank("minsteptank", "model", "sn1", 10.0, 10.0);
        var engine = new Engine("minstepengine", "model", "sn1", 300.0, 10.0, tank);
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        var target = new StateVector(new Vector3(5956434.445278982, -1653154.6911787654, -1653542.0820755141),
            new Vector3(1296.2335176264082, 2546.3347704417083, 2305.938709298581), body, Epoch.AddSeconds(2.49),
            Frames.Frame.ICRF);
        var maneuver = new PlaneAlignmentManeuver(Epoch, TimeSpan.Zero, target, engine);
        spacecraft.SetManeuvers(maneuver);

        // Act. The node is a grazing one: the trajectory, below the target plane at the start (the ascending node is locked),
        // rises above it from 2.381 s to 2.495 s only, 0.10 m at most (2026-11-18, Linux), where the steps of RK7(8) are
        // 0.10 to 0.115 s long. The event is seen only when a step end falls in that excursion, so a step sequence
        // that straddles it (the rounding of the platform, as on the ubuntu-26.04 runner of CI run 44395076184, 862
        // steps in the test above where ubuntu-24.04 takes 880) fires no burn and leaves one segment: 7 in 294 initial
        // step sizes from 7.2e-4 to 96 s on Linux x64, by a step such as [2.380445 s, 2.506085 s]. Steps of at most
        // 0.05 s put at least two step ends in the excursion whatever the platform; the first steps after the burn, in
        // the dense air, are below 1e-2 s on every platform (CI run 44395076184), under that bound, so the segment still
        // starts in the regime of the issue
        var solution = spacecraft.Propagate(Window, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = Tolerance, RelativeTolerance = Tolerance, MaxStep = TimeSpan.FromSeconds(MaximumStepAcrossTheNode) }, OutputStep = Window.Length, ThirdBodies = new CelestialItem[] { body }, AtmosphericDrag = true, CovariancePropagation = new CovariancePropagationOptions { IncludeInStepControl = true } });

        // Assert: the burn splits the propagation in two segments, the second reaches the end of the window, and Φ runs
        // through the burn
        var last = solution.Segments[^1];
        _output.WriteLine($"burn of {maneuver.DeltaV.Magnitude():F3} m/s at " +
                          $"{(last.BaseEpoch - Epoch).TotalSeconds:F6} s; steps " +
                          string.Join(" + ", solution.Segments.Select(s => s.Steps.Count)) + "; first steps after it " +
                          string.Join(", ", last.Steps.Take(3).Select(s => s.StepSize.ToString("E2"))));
        Assert.Equal(2, solution.Segments.Count);
        Assert.True(maneuver.DeltaV.Magnitude() > 1.0, $"ΔV {maneuver.DeltaV.Magnitude()} m/s");
        WindowEndAssert.ReachesTheEndOf(Window, solution);
        var phi = solution.GetStateTransitionMatrix(Window.EndDate);
        Assert.True(Enumerable.Range(0, 36).All(i => double.IsFinite(phi.Get(i / 6, i % 6))));
    }
}
