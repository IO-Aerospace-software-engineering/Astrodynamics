// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The stored states of a propagation run to the end of its window (#741): when the output grid, round(L/ΔT) + 1 outputs
/// every ΔT, ends before the window, the solution at the window end is stored as its last state, in place of the last
/// grid point when that lies within ΔT/10 of the end. On f70ee32 a window of 3 610 s at 60 s outputs stored its states
/// to 3 600 s, and a read at the window end, or at 3 605 s, threw "lies outside the propagated states" (#527).
/// </summary>
/// <remarks>
/// The reference is the two-body motion of the initial state (<see cref="OrbitalParameters.OrbitalParameters.AtEpoch"/>,
/// the closed form, TT seconds about the Earth, TDB about the Moon) for a point-mass propagation in RK7(8) at 1e-12, and
/// the solution itself (<see cref="PropagationSolution.InterpolateAt"/>) for the state stored at the window end.
/// Thresholds: twice the worst over Linux x64, Windows x64 and macOS arm64 (the MEASURE lines, CI run 44395076184,
/// 2026-11-18), rounded up at the second significant digit.
/// </remarks>
public class PropagationToTheWindowEndTests
{
    /// <summary>The read at 3 605 s and at the window end of a point-mass propagation in RK7(8) at 1e-12 against the
    /// two-body closed form, m: the integration error over an hour and the Lagrange interpolation over the last, shorter,
    /// interval. Worst 9.664e-7 m on Windows x64 (about the Earth; 9.647e-7 m on Linux x64 and macOS arm64; 3.787e-7 m
    /// about the Moon on the three), CI run 44395076184, 2026-11-18; twice that, rounded up.</summary>
    internal const double AgainstTwoBody = 2.0e-6;

    /// <summary>The SPK written by <c>WriteEphemeris</c> against the managed reads at 3 605 s and at the window end, m: the
    /// resolution of the ET doubles of the nodes (1.2e-7 s) times the speed, as <c>PropagatedArcsTests.SpkAgainstManaged</c>.
    /// Worst 1.738e-5 m on Linux x64, Windows x64 and macOS arm64, CI run 44395076184, 2026-11-18; twice that, rounded
    /// up.</summary>
    internal const double SpkAgainstManaged = 3.5e-5;

    private readonly ITestOutputHelper _output;

    public PropagationToTheWindowEndTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time Start = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);

    private static StateVector Orbit(string central, Time start)
    {
        if (central == "Moon")
        {
            var moon = new CelestialBody(PlanetsAndMoons.Moon, Frame.ICRF, start);
            double r = 1.9e6;
            double v = System.Math.Sqrt(moon.GM / r) * 1.05;
            return new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, v * 0.8, v * 0.6), moon, start, Frame.ICRF);
        }

        var earth = new CelestialBody(PlanetsAndMoons.Earth, Frame.ICRF, start);
        return new StateVector(new Vector3(6878137.0, 0.0, 0.0), new Vector3(0.0, 6200.0, 4500.0), earth, start, Frame.ICRF);
    }

    private static Spacecraft NewSpacecraft(int naifId, StateVector initial) =>
        new(naifId, "WE" + -naifId, 1000.0, initial, 3000.0, new Clock("we" + -naifId, 65536));

    private static PropagationSolution Propagate(Spacecraft spacecraft, Window window, string integrator, double step)
    {
        var central = (CelestialItem)spacecraft.InitialOrbitalParameters.Observer;
        var outputStep = TimeSpan.FromSeconds(step);
        return integrator == "RK"
            ? spacecraft.Propagate(window, new PropagationOptions { Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }, OutputStep = outputStep, ThirdBodies = new[] { central }, CovariancePropagation = CovariancePropagationOptions.None })
            : spacecraft.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = outputStep }, OutputStep = outputStep, ThirdBodies = new[] { central } });
    }

    /// <summary>
    /// A window of 3 610 s at 60 s outputs: 61 outputs on the grid, to 3 600 s, then the solution at the window end, 62
    /// stored states; the reads at the window end and at 3 605 s, about the Earth (TT seconds) and the Moon (TDB seconds),
    /// in RK7(8) and Velocity-Verlet. On f70ee32 both reads threw.
    /// </summary>
    [Theory]
    [InlineData("RK", "Earth", -7411)]
    [InlineData("RK", "Moon", -7412)]
    [InlineData("VV", "Earth", -7413)]
    [InlineData("VV", "Moon", -7414)]
    public void StepThatDoesNotDivideTheWindow_StatesRunToTheWindowEnd(string integrator, string central, int naifId)
    {
        // Arrange
        var initial = Orbit(central, Start);
        var spacecraft = NewSpacecraft(naifId, initial);
        var window = new Window(Start, Start.AddSeconds(3610.0));

        // Act
        var solution = Propagate(spacecraft, window, integrator, 60.0);

        // Assert: the grid, then the window end
        var states = spacecraft.PropagatedStates;
        Assert.Equal(62, states.Length);
        LabelAssert.Equal(window.EndDate, states[^1].Epoch);
        Assert.Equal(3600.0, PropagationTimeArgument.For(initial.Observer).SecondsBetween(Start, states[^2].Epoch), 6);
        Assert.Equal(62, solution.StateVectors.Count);
        LabelAssert.Equal(window.EndDate, solution.StateVectors[^1].Epoch);

        var observer = (CelestialItem)initial.Observer;
        var atEnd = spacecraft.GetEphemeris(window.EndDate, observer, Frame.ICRF, Aberration.None);
        var (endPosition, endVelocity) = solution.InterpolateAt(window.EndDate);
        double endAgainstSolution = (atEnd.Position - endPosition).Magnitude();
        double endVelocityAgainstSolution = (atEnd.Velocity - endVelocity).Magnitude();
        var inside = window.StartDate.AddSeconds(3605.0);
        var atInside = spacecraft.GetEphemeris(inside, observer, Frame.ICRF, Aberration.None);
        double insideAgainstSolution = (atInside.Position - solution.InterpolateAt(inside).position).Magnitude();
        _output.WriteLine($"MEASURE #741 {integrator} {central} 3610 s / 60 s: at the end {endAgainstSolution:E3} m " +
                          $"{endVelocityAgainstSolution:E3} m/s from the solution, at 3605 s {insideAgainstSolution:E3} m");
        Assert.Equal(0.0, endAgainstSolution);
        Assert.Equal(0.0, endVelocityAgainstSolution);

        if (integrator == "RK")
        {
            // The closed form of the two-body motion of the initial state
            double worst = 0.0;
            foreach (var epoch in new[] { inside, window.EndDate })
            {
                var expected = initial.AtEpoch(epoch).ToStateVector();
                var read = spacecraft.GetEphemeris(epoch, observer, Frame.ICRF, Aberration.None);
                worst = System.Math.Max(worst, (read.Position - expected.Position).Magnitude());
            }

            _output.WriteLine($"MEASURE #741 {central} against the two-body closed form at 3605 s and at the end: {worst:E3} m");
            Assert.True(worst < AgainstTwoBody, $"{worst:E3} m");
        }
    }

    /// <summary>
    /// A window of 3 603 s at 60 s: the last grid point, 3 s before the end, lies within ΔT/10 of it and is replaced by the
    /// window end, 61 states, no two of them closer than ΔT/10 (the rule of #598 and #540). A window of 20 s at 60 s,
    /// whose grid held the start alone: the start and the end. A window the step divides keeps its grid; one it rounds up
    /// ends at the window end (since 11.0.0, R11 of #668, choice 6: before, its grid ran to round(L/ΔT)·ΔT, up to half a
    /// step past the window, #626; see <see cref="OutputGridTests"/>).
    /// </summary>
    [Theory]
    [InlineData(3603.0, 61, -7421)]
    [InlineData(20.0, 2, -7422)]
    [InlineData(3600.0, 61, -7423)]
    [InlineData(3590.0, 61, -7424)]
    [InlineData(3654.0, 62, -7425)]
    public void LastGridPoint_NearTheEnd_IsReplaced(double length, int count, int naifId)
    {
        // Arrange
        var initial = Orbit("Earth", Start);
        var spacecraft = NewSpacecraft(naifId, initial);
        var window = new Window(Start, Start.AddSeconds(length));

        // Act
        Propagate(spacecraft, window, "RK", 60.0);

        // Assert
        var states = spacecraft.PropagatedStates;
        var argument = PropagationTimeArgument.For(initial.Observer);
        double[] seconds = states.Select(s => argument.SecondsBetween(Start, s.Epoch)).ToArray();
        _output.WriteLine($"MEASURE #741 {length} s / 60 s: {states.Length} states, the last three at " +
                          string.Join(", ", seconds.TakeLast(3).Select(s => s.ToString("F7"))) + " s");
        Assert.Equal(count, states.Length);
        // The window end, whether the grid rounds the window down or up; in TT, a window of 3 600 TDB seconds is 0.6 µs
        // shorter, its last output labelled with the end
        LabelAssert.Equal(window.EndDate, states[^1].Epoch);

        for (int i = 1; i < seconds.Length; i++)
        {
            Assert.True(seconds[i] - seconds[i - 1] >= 6.0 || seconds.Length == 2, $"{seconds[i - 1]} and {seconds[i]} s");
        }

        _ = spacecraft.GetEphemeris(window.EndDate, (CelestialItem)initial.Observer, Frame.ICRF, Aberration.None);
    }

    /// <summary>
    /// A window given in UTC, 3 610 s long, propagated about the Earth over TT seconds: its stored states end at its
    /// end, read there.
    /// </summary>
    [Fact]
    public void UtcWindow_StatesRunToTheWindowEnd()
    {
        // Arrange
        var start = new Time(2024, 3, 1, 12, 0, 0, TimeScale.UTC);
        var initial = Orbit("Earth", start);
        var spacecraft = NewSpacecraft(-7431, initial);
        var window = new Window(start, start.AddSeconds(3610.0));

        // Act
        var solution = Propagate(spacecraft, window, "RK", 60.0);

        // Assert
        var states = spacecraft.PropagatedStates;
        Assert.Equal(62, states.Length);
        LabelAssert.Equal(window.EndDate.ToTdb(), states[^1].Epoch);
        var read = spacecraft.GetEphemeris(window.EndDate, (CelestialItem)initial.Observer, Frame.ICRF, Aberration.None);
        Assert.Equal(0.0, (read.Position - solution.InterpolateAt(window.EndDate.ToTdb()).position).Magnitude());
    }

    /// <summary>
    /// The SPK written by <c>WriteEphemeris</c> gains the window end: CSPICE reads it at the window end and at 3 605 s, as
    /// the managed reads. On f70ee32 the segment ended at 3 600 s and the read at the end failed.
    /// </summary>
    [Fact]
    public void Spk_RunsToTheWindowEnd()
    {
        // Arrange
        var initial = Orbit("Earth", Start);
        var spacecraft = NewSpacecraft(-7441, initial);
        var window = new Window(Start, Start.AddSeconds(3610.0));
        Propagate(spacecraft, window, "RK", 60.0);
        Directory.CreateDirectory("Data/User/WindowEnd741");
        var spk = new FileInfo(Path.Combine("Data/User/WindowEnd741", spacecraft.Name.ToLowerInvariant() + ".bsp"));

        // Act
        spacecraft.WriteEphemeris(spk);
        SpiceApi.Instance.LoadKernels(spk);
        try
        {
            double worst = 0.0;
            foreach (var epoch in new[] { Start.AddSeconds(3605.0), window.EndDate })
            {
                var managed = spacecraft.GetEphemeris(epoch, (CelestialItem)initial.Observer, Frame.ICRF, Aberration.None);
                // The windowed read, which throws on a SPICE error
                var fromSpk = SpiceApi.Instance.ReadEphemeris(new Window(epoch, epoch), 399, spacecraft.NaifId, Frame.ICRF,
                    Aberration.None, TimeSpan.FromSeconds(1.0)).First().ToStateVector();
                worst = System.Math.Max(worst, (fromSpk.Position - managed.Position).Magnitude());
            }

            // Assert
            _output.WriteLine($"MEASURE #741 SPK against the managed reads at 3605 s and at the end: {worst:E3} m");
            Assert.True(worst < SpkAgainstManaged, $"{worst:E3} m");
        }
        finally
        {
            SpiceApi.Instance.UnloadKernels(spk);
            spk.Delete();
        }
    }
}
