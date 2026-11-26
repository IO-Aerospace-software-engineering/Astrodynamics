// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Ssa;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using IO.Astrodynamics.Propagator.Variational;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The search on two propagations after an impulse (#574): RK7(8) adds the accepted step that holds an event before it
/// detects the event, so the segment that ends at an impulse keeps that step past it, and the polynomial search read the
/// trajectory without the burn between the impulse and the end of that step. A close approach there had no minimum on that
/// polynomial and was missed. The reference is a brute force of the states of the two solutions
/// (<see cref="PropagationSolution.InterpolateAt"/>, the state after the burn at and after it) every 10 ms over the window,
/// each local minimum of the samples refined by golden section on the states.
/// </summary>
/// <remarks>
/// The protected spacecraft is #574's: a 400 × 1000 km LEO from apogee about a point-mass Earth, its apogee raised to
/// 2000 km at the first perigee (an <see cref="ApogeeHeightManeuver"/> of 233.8 m/s at S + 2963.2 s), RK7(8) at 1e-12,
/// the step that holds the burn ending 12.4 s after it (17.8 s in #574's measurement, with its full force model).
/// Each secondary is built to pass 30 m from it k seconds after (or before) the burn, at a relative velocity of ∓ΔV/2 (the
/// four rows #574 measured as missed) or with a cross-track component, from its two-body state there propagated back to
/// the start of its window and then numerically over the window, so that the reference is its own trajectory.
/// </remarks>
public class ConjunctionPostBurnTests
{
    private static readonly ConjunctionAnalysisOptions Options = new() { CovariancePropagation = CovariancePropagationOptions.None };
    private static int _ids = -574_000;

    private readonly ITestOutputHelper _output;
    private readonly CelestialBody _earth;
    private readonly TimeSystem.Time _start;

    public ConjunctionPostBurnTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
        _start = new TimeSystem.Time(2026, 1, 20, 6, 0, 0, TimeScale.UTC).ToTdb();
        _earth = new CelestialBody(PlanetsAndMoons.Earth, Frames.Frame.ICRF, _start);
    }

    private static int NextId() => Interlocked.Decrement(ref _ids);

    private Spacecraft NewSpacecraft(int id, Astrodynamics.OrbitalParameters.OrbitalParameters orbit, out Engine engine)
    {
        var spacecraft = new Spacecraft(id, "PB" + -id, 1000.0, orbit, 3000.0, new Clock("pb" + -id, 65536));
        var tank = new FuelTank("tank" + -id, 1000.0, 1000.0, "model", "sn1");
        engine = new Engine("engine" + -id, 300.0, 10.0, tank, "model", "sn1");
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        return spacecraft;
    }

    // #574's spacecraft and its apogee raise, propagated over 2 h; the burn epoch and ΔV
    private (Spacecraft Spacecraft, PropagationSolution Solution, TimeSystem.Time Burn, Vector3 DeltaV) PropagateRaise(bool rk78)
    {
        var orbit = new StateVector(new Vector3(-7378137.0, 0.0, 0.0), new Vector3(0.0, -5509.9, -4623.4), _earth, _start,
            Frames.Frame.ICRF);
        var spacecraft = NewSpacecraft(NextId(), orbit, out var engine);
        var maneuver = new ApogeeHeightManeuver(8378137.0, engine, _start, TimeSpan.Zero, _earth);
        spacecraft.SetManeuvers(maneuver);
        var solution = spacecraft.Propagate(new Window(_start, _start.AddHours(2.0)), new PropagationOptions { Integrator = rk78 ? new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 } : new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) }, OutputStep = TimeSpan.FromSeconds(60.0), ThirdBodies = new CelestialItem[] { _earth }, CovariancePropagation = CovariancePropagationOptions.None });
        Assert.Equal(2, solution.Segments.Count);
        return (spacecraft, solution, solution.Segments[1].BaseEpoch, maneuver.DeltaV);
    }

    // The end of the accepted step that holds the event ending the first segment
    private static TimeSystem.Time EndOfTheStepOfTheEvent(PropagationSolution solution)
    {
        var segment = solution.Segments[0];
        var step = segment.Steps[^1];
        return solution.TimeArgument.StepEpochs(segment.BaseEpoch, step.CumulativeTime, step.StepSizeSeconds).End;
    }

    // A secondary 30 m from the protected trajectory at tc, at the relative velocity given, over [tc − 150 s, tc + 150 s]
    private (Spacecraft Spacecraft, PropagationSolution Solution, Window Window) Secondary(PropagationSolution protectedSolution,
        TimeSystem.Time tc, Vector3 relativeVelocity)
    {
        var (position, velocity) = protectedSolution.InterpolateAt(tc);
        var normal = relativeVelocity.Cross(position).Normalize();
        var atTc = new StateVector(position + normal * 30.0, velocity + relativeVelocity, _earth, tc, Frames.Frame.ICRF);
        var window = new Window(tc.AddSeconds(-150.0), tc.AddSeconds(150.0));
        var spacecraft = NewSpacecraft(NextId(), atTc.AtEpoch(window.StartDate).ToStateVector(), out _);
        var solution = spacecraft.Propagate(window, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }, OutputStep = TimeSpan.FromSeconds(10.0), ThirdBodies = new CelestialItem[] { _earth }, CovariancePropagation = CovariancePropagationOptions.None });
        return (spacecraft, solution, window);
    }

    // The local minima of the distance of the states of the two solutions over the window: samples every 10 ms, each
    // interior local minimum refined by golden section on the states to 1e-7 s
    private static List<(TimeSystem.Time Epoch, double Distance)> BruteForceMinima(PropagationSolution first,
        PropagationSolution second, Window window)
    {
        double Distance(TimeSystem.Time epoch) => (second.InterpolateAt(epoch).position - first.InterpolateAt(epoch).position)
            .Magnitude();

        const double step = 0.01;
        int count = (int)System.Math.Round(window.Length.TotalSeconds / step);
        var samples = new double[count + 1];
        for (int i = 0; i <= count; i++)
        {
            samples[i] = Distance(window.StartDate.AddSeconds(i * step));
        }

        var minima = new List<(TimeSystem.Time, double)>();
        for (int i = 1; i < count; i++)
        {
            if (!(samples[i] <= samples[i - 1] && samples[i] < samples[i + 1]))
            {
                continue;
            }

            double a = (i - 1) * step, b = (i + 1) * step;
            const double ratio = 0.6180339887498949;
            double c = b - ratio * (b - a), d = a + ratio * (b - a);
            double fc = Distance(window.StartDate.AddSeconds(c)), fd = Distance(window.StartDate.AddSeconds(d));
            while (b - a > 1e-7)
            {
                if (fc < fd)
                {
                    b = d; d = c; fd = fc; c = b - ratio * (b - a); fc = Distance(window.StartDate.AddSeconds(c));
                }
                else
                {
                    a = c; c = d; fc = fd; d = a + ratio * (b - a); fd = Distance(window.StartDate.AddSeconds(d));
                }
            }

            var epoch = window.StartDate.AddSeconds(0.5 * (a + b));
            minima.Add((epoch, Distance(epoch)));
        }

        return minima;
    }

    public static IEnumerable<object[]> PostBurnRows()
    {
        // #574's four missed rows: 1, 3, 6 and 9 s after the burn at −ΔV/2 relative to the spacecraft
        foreach (double k in new[] { 1.0, 3.0, 6.0, 9.0 })
        {
            yield return new object[] { "RK7(8)", k, "alongDeltaV" };
        }

        // A crossing at 100 m/s across the burn, after it, and before the burn on the trajectory without it, at +ΔV/2
        yield return new object[] { "RK7(8)", 4.5, "crossing" };
        yield return new object[] { "RK7(8)", -2.0, "alongDeltaV" };
        yield return new object[] { "RK7(8)", -5.0, "crossing" };

        // Velocity-Verlet locates the event at a step end: no step past it, the search unchanged
        yield return new object[] { "Velocity-Verlet", 3.0, "alongDeltaV" };
    }

    [Theory]
    [MemberData(nameof(PostBurnRows))]
    public void CloseApproachInsideTheStepOfTheBurn_IsListedAndSelected(string integrator, double k, string geometry)
    {
        // Arrange
        var (spacecraft, solution, burn, deltaV) = PropagateRaise(integrator == "RK7(8)");
        var stepEnd = EndOfTheStepOfTheEvent(solution);
        _output.WriteLine($"{integrator}: burn of {deltaV.Magnitude():F3} m/s at S + {(burn - _start).TotalSeconds:F3} s, " +
                          $"the step that holds it ends {(stepEnd - burn).TotalSeconds:F3} s after it");
        if (integrator == "RK7(8)")
        {
            Assert.True((stepEnd - burn).TotalSeconds > System.Math.Max(k, 0.0), "the close approach lies past the step");
        }

        var tc = burn.AddSeconds(k);
        double sign = k > 0.0 ? -1.0 : 1.0;
        var along = deltaV * (0.5 * sign);
        var (position, velocity) = solution.InterpolateAt(tc);
        var cross = velocity.Cross(position).Normalize() * 100.0;
        var (secondary, secondarySolution, window) = Secondary(solution, tc, geometry == "crossing" ? along + cross : along);
        var reference = BruteForceMinima(solution, secondarySolution, window);
        var closest = reference.MinBy(m => m.Distance);
        var afterBurn = reference.Where(m => k > 0.0 ? m.Epoch > burn : m.Epoch < burn)
            .MinBy(m => System.Math.Abs((m.Epoch - tc).TotalSeconds));

        // Act
        var all = ConjunctionAssessment.AnalyzeAll(spacecraft, solution, secondary, secondarySolution, window, Options);
        var encounter = ConjunctionAssessment.Analyze(spacecraft, solution, secondary, secondarySolution, window, Options);

        // Assert: the close approach on the side of the burn the secondary was built on is listed, and Analyze returns the
        // closest minimum of the states
        foreach (var m in reference)
        {
            _output.WriteLine($"reference minimum S + {(m.Epoch - _start).TotalSeconds:F6} s, {m.Distance:F6} m");
        }

        foreach (var e in all)
        {
            _output.WriteLine($"listed S + {(e.EncounterState.Epoch - _start).TotalSeconds:F6} s, " +
                              $"{e.EncounterState.MissDistanceMeters:F6} m");
        }

        var listed = all.MinBy(e => System.Math.Abs((e.EncounterState.Epoch - afterBurn.Epoch).TotalSeconds));
        Assert.NotNull(listed);
        AssertNear(listed.EncounterState, afterBurn, $"{integrator} k = {k} {geometry}, the listed minimum");
        AssertNear(encounter.EncounterState, closest, $"{integrator} k = {k} {geometry}, Analyze");
    }

    [Fact]
    public void ContinueEvent_TheStepIsCut_TheCloseApproachAfterItIsListed()
    {
        // Arrange: a combined maneuver whose precondition fails at the first apogee (S + 292 s, #355's orbit): the event
        // returns Continue, the state unchanged; a secondary crossing 3 s after it, inside the step that holds it
        var orbit = new KeplerianElements(11480000.0, 0.5, 28.5 * IO.Astrodynamics.Constants.Deg2Rad, 0.0,
            System.Math.PI * 0.5, System.Math.PI - 0.15, _earth, _start, Frames.Frame.ICRF);
        var spacecraft = NewSpacecraft(NextId(), orbit, out var engine);
        spacecraft.SetManeuvers(new CombinedManeuver(9000000.0, 0.1, engine, _start, TimeSpan.Zero, _earth));
        var solution = spacecraft.Propagate(new Window(_start, _start.AddHours(1.0)), new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }, OutputStep = TimeSpan.FromSeconds(60.0), ThirdBodies = new CelestialItem[] { _earth }, CovariancePropagation = CovariancePropagationOptions.None });
        Assert.Equal(2, solution.Segments.Count);
        var eventEpoch = solution.Segments[1].BaseEpoch;
        var stepEnd = EndOfTheStepOfTheEvent(solution);
        Assert.True((stepEnd - eventEpoch).TotalSeconds > 3.0, "the close approach lies past the step");
        var tc = eventEpoch.AddSeconds(3.0);
        var (position, velocity) = solution.InterpolateAt(tc);
        var (secondary, secondarySolution, window) = Secondary(solution, tc,
            velocity.Cross(position).Normalize() * 100.0 + velocity.Normalize() * 20.0);
        var closest = BruteForceMinima(solution, secondarySolution, window).MinBy(m => m.Distance);

        // Act
        var all = ConjunctionAssessment.AnalyzeAll(spacecraft, solution, secondary, secondarySolution, window, Options);
        var encounter = ConjunctionAssessment.Analyze(spacecraft, solution, secondary, secondarySolution, window, Options);

        // Assert
        AssertNear(Assert.Single(all).EncounterState, closest, "Continue, AnalyzeAll");
        AssertNear(encounter.EncounterState, closest, "Continue, Analyze");
    }

    private void AssertNear(EncounterState state, (TimeSystem.Time Epoch, double Distance) reference, string label)
    {
        double tcaError = (state.Epoch - reference.Epoch).TotalSeconds;
        double missError = state.MissDistanceMeters - reference.Distance;
        _output.WriteLine($"MEASURE #574 {label}: TCA {tcaError:E3} s, miss {missError:E3} m from the brute force of the " +
                          $"states ({reference.Distance:F6} m)");
        Assert.True(System.Math.Abs(tcaError) <= TcaBound, $"{label}: TCA {tcaError:E3} s");
        Assert.True(System.Math.Abs(missError) <= MissBound, $"{label}: miss {missError:E3} m");
    }

    // Twice the worst over every row on Linux x64, Windows x64 and macOS arm64 (CI run 44225490669), rounded up to two
    // digits: the TCA 9.0e-7 s on macOS arm64 (RK7(8), k = −2 s along ΔV, Analyze; 7.0e-7 s on Linux x64 and Windows x64),
    // the tick of the TCA against the golden section of the reference, to 1e-7 s; the miss 1.914e-9 m on macOS arm64 (the
    // crossing, k = −5 s; 1.694e-9 m on Linux x64 and Windows x64). Before the fix (a85098d), the rows k = 1, 3, 6 and 4.5 s listed the minimum
    // before the burn only, 2k s away (658.5 m off for the crossing), and Analyze returned a minimum 4 s off for k = −2 s
    private const double TcaBound = 1.8e-6;
    private const double MissBound = 3.9e-9;
}
