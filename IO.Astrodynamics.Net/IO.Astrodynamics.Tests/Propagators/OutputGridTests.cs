// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

// Inside IO.Astrodynamics.Tests the namespaces Frame and Time shadow the library types
using Frame = IO.Astrodynamics.Frames.Frame;
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The output grid of a propagation whose step rounds its window up (R11 of #668, structuring choice 6, decided by
/// S. Guillet, 2026-11-19): start + i·ΔT inside the window, then the window end, on every path. Before 11.0.0 the grid,
/// round(L/ΔT) + 1 outputs, ran to round(L/ΔT)·ΔT, up to ΔT/2 past the window, and the propagation with it (#626).
/// </summary>
/// <remarks>
/// The references are closed forms and runs of the same window: the epochs i·ΔT (in seconds of the time argument, TT
/// about the Earth, TDB about the Moon) and the window end; the state at the end and the accepted steps, those of the
/// propagation of the same window with an output step equal to its length, which the step divides (its outputs the
/// start and the end), bit for bit: the integrator's steps do not depend on the output grid, only on where the
/// propagation ends. What moves against the former grid (decision 8 of lot L4 of #668, decided by S. Guillet,
/// 2026-11-22): in RK7(8) the last output only, every other output bit for bit; in Velocity-Verlet the last output and the
/// outputs inside the last integrator step, clipped at the window end, when the step does not divide the output step
/// (<see cref="WhatMoves_AgainstTheFormerGrid"/>).
/// </remarks>
public class OutputGridTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public OutputGridTests(Xunit.Abstractions.ITestOutputHelper output) : this()
    {
        _output = output;
    }

    private static readonly Time Start = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);

    private OutputGridTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static int _nextId = -1791000;

    private static int NextId() => System.Threading.Interlocked.Decrement(ref _nextId);

    private static StateVector Orbit(string central)
    {
        if (central == "Moon")
        {
            var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, Start);
            double r = 1.9e6;
            double v = System.Math.Sqrt(moon.GM / r) * 1.05;
            return new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, v * 0.8, v * 0.6), moon, Start, Frame.ICRF);
        }

        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Start);
        return new StateVector(new Vector3(6878137.0, 0.0, 0.0), new Vector3(0.0, 6200.0, 4500.0), earth, Start,
            Frame.ICRF);
    }

    private static Spacecraft NewSpacecraft(IO.Astrodynamics.OrbitalParameters.OrbitalParameters initial)
    {
        int id = NextId();
        return new Spacecraft(id, "GRID" + -id, 1000.0, initial, 3000.0, new Clock("grid" + -id, 65536));
    }

    private static IntegratorOptions Integrator(string name) => name == "RK"
        ? new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }
        : new VVIntegratorOptions { Step = TimeSpan.FromSeconds(60.0) };

    private static PropagationOptions Options(string integrator, double outputStep) => new()
    {
        Integrator = Integrator(integrator), OutputStep = TimeSpan.FromSeconds(outputStep)
    };

    /// <summary>
    /// A window of 3 590 s at 60 s outputs: round(59.83) = 60 intervals, the outputs at 0, 60, …, 3 540 s, then the window
    /// end, 61 outputs (before 11.0.0 the last at 3 600 s, 10 s past the window); the state at the end, the stored states
    /// and the accepted steps those of the propagation of the window with one output step, about the Earth (TT seconds)
    /// and the Moon (TDB seconds), in RK7(8) and Velocity-Verlet.
    /// </summary>
    [Theory]
    [InlineData("RK", "Earth")]
    [InlineData("RK", "Moon")]
    [InlineData("VV", "Earth")]
    [InlineData("VV", "Moon")]
    public void StepThatRoundsTheWindowUp_EndsAtTheWindowEnd(string integrator, string central)
    {
        // Arrange
        var initial = Orbit(central);
        var window = new Window(Start, Start.AddSeconds(3590.0));
        var spacecraft = NewSpacecraft(initial);
        var reference = NewSpacecraft(initial);

        // Act
        var solution = spacecraft.Propagate(window, Options(integrator, 60.0));
        var oneStep = reference.Propagate(window, Options(integrator, 3590.0));

        // Assert: the grid inside the window, then its end
        var argument = PropagationTimeArgument.For(initial.Observer);
        var states = spacecraft.PropagatedStates;
        Assert.Equal(61, states.Length);
        Assert.Equal(61, solution.StateVectors.Count);
        for (int i = 0; i < 60; i++)
        {
            Assert.Equal(60.0 * i, argument.SecondsBetween(Start, states[i].Epoch), 6);
        }

        LabelAssert.Equal(window.EndDate, states[^1].Epoch);
        LabelAssert.Equal(window.EndDate, solution.StateVectors[^1].Epoch);

        // The state at the end and the accepted steps of the window's propagation, bit for bit
        var end = reference.PropagatedStates[^1];
        Assert.True(end.Position == states[^1].Position && end.Velocity == states[^1].Velocity,
            $"{(end.Position - states[^1].Position).Magnitude():E3} m");
        var steps = solution.Segments.SelectMany(s => s.Steps).ToArray();
        var referenceSteps = oneStep.Segments.SelectMany(s => s.Steps).ToArray();
        Assert.Equal(referenceSteps.Length, steps.Length);
        for (int k = 0; k < steps.Length; k++)
        {
            Assert.Equal(referenceSteps[k], steps[k]);
        }

        // Nothing past the window
        Assert.Equal(argument.Length(window, window), steps.Sum(s => s.StepSizeSeconds), 9);
        _ = spacecraft.GetEphemeris(window.EndDate, (CelestialItem)initial.Observer, Frame.ICRF, Aberration.None);
    }

    /// <summary>
    /// Windows the step rounds up, down or divides, and one shorter than half a step: the outputs every ΔT inside the
    /// window, then its end, a grid point within ΔT/10 of the end replaced by it (#741), never two outputs closer than
    /// ΔT/10.
    /// </summary>
    [Theory]
    [InlineData(3590.0, 61)]
    [InlineData(3654.0, 62)]
    [InlineData(3630.0, 62)]
    [InlineData(3603.0, 61)]
    [InlineData(3600.0, 61)]
    [InlineData(20.0, 2)]
    [InlineData(40.0, 2)]
    public void EveryWindow_OutputsEveryStepInside_ThenItsEnd(double length, int count)
    {
        // Arrange
        var initial = Orbit("Earth");
        var spacecraft = NewSpacecraft(initial);
        var window = new Window(Start, Start.AddSeconds(length));

        // Act
        spacecraft.Propagate(window, Options("RK", 60.0));

        // Assert
        var states = spacecraft.PropagatedStates;
        var argument = PropagationTimeArgument.For(initial.Observer);
        double[] seconds = states.Select(s => argument.SecondsBetween(Start, s.Epoch)).ToArray();
        Assert.Equal(count, states.Length);
        LabelAssert.Equal(window.EndDate, states[^1].Epoch);
        for (int i = 0; i < seconds.Length - 1; i++)
        {
            Assert.Equal(60.0 * i, seconds[i], 6);
        }

        for (int i = 1; i < seconds.Length; i++)
        {
            Assert.True(seconds[i] - seconds[i - 1] >= 6.0 || seconds.Length == 2, $"{seconds[i - 1]} and {seconds[i]} s");
        }
    }

    /// <summary>
    /// The same grid on the other paths: a <see cref="CentralBodyPropagator"/> of options, one of an integrator that holds
    /// its forces, <see cref="Scenario.Simulate"/>, a batch task; and <see cref="TLEPropagator"/>, whose grid of #598 is
    /// this rule (one rule on every path).
    /// </summary>
    [Theory]
    [InlineData("CentralBodyPropagator")]
    [InlineData("IIntegrator")]
    [InlineData("Scenario")]
    [InlineData("Batch")]
    [InlineData("TLEPropagator")]
    public void EveryPath_EndsAtTheWindowEnd(string path)
    {
        // Arrange
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var start = path == "TLEPropagator" ? tle.Epoch.ToTDB() : Start;
        var window = new Window(start, start.AddSeconds(3590.0));
        var step = TimeSpan.FromSeconds(60.0);
        var initial = Orbit("Earth");
        Spacecraft spacecraft;

        // Act
        switch (path)
        {
            case "CentralBodyPropagator":
                spacecraft = NewSpacecraft(initial);
                new CentralBodyPropagator(window, spacecraft, Options("RK", 60.0)).Propagate();
                break;
            case "IIntegrator":
            {
                spacecraft = NewSpacecraft(initial);
                var integrator = new RK78Integrator(1e-12, 1e-12);
                integrator.AddForce(new GravitationalAcceleration((CelestialItem)initial.Observer));
                new CentralBodyPropagator(window, spacecraft, integrator, step).Propagate();
                break;
            }
            case "Scenario":
            {
                spacecraft = NewSpacecraft(initial);
                var scenario = new Scenario("grid", new IO.Astrodynamics.Mission.Mission("grid"), window);
                scenario.AddSpacecraft(spacecraft);
                scenario.Simulate(Options("RK", 60.0));
                break;
            }
            case "Batch":
                spacecraft = NewSpacecraft(initial);
                BatchPropagator.Propagate(new[] { new PropagationTask(spacecraft, window, Options("RK", 60.0)) });
                break;
            default:
            {
                spacecraft = NewSpacecraft(tle);
                new TLEPropagator(window, spacecraft, step).Propagate();
                break;
            }
        }

        // Assert
        var states = spacecraft.PropagatedStates;
        Assert.Equal(61, states.Length);
        LabelAssert.Equal(window.EndDate, states[^1].Epoch);
        // TT seconds about the Earth for a numerical propagation (#501), TDB ticks for TLEPropagator (#598)
        var argument = path == "TLEPropagator" ? PropagationTimeArgument.Tdb : PropagationTimeArgument.Tt;
        for (int i = 0; i < 60; i++)
        {
            Assert.Equal(60.0 * i, argument.SecondsBetween(window.StartDate, states[i].Epoch), 6);
        }
    }

    /// <summary>
    /// Against the former grid, which ran to round(L/ΔT)·ΔT = 3 600 s, the run of the window of 3 600 s (decision 8 of lot
    /// L4 of #668): in RK7(8), and in Velocity-Verlet at a step that divides the output step (60 s), the outputs before the
    /// window end are bit for bit; in Velocity-Verlet at 100 s, which does not divide 60 s, the output at 3 540 s, inside
    /// the last integrator step (3 500 s to the window end, clipped there, 3 500 s to 3 600 s before), moves too, every
    /// output before it bit for bit.
    /// </summary>
    [Theory]
    [InlineData("RK", 0.0)]
    [InlineData("VV", 60.0)]
    [InlineData("VV", 100.0)]
    public void WhatMoves_AgainstTheFormerGrid(string integrator, double vvStep)
    {
        // Arrange
        var initial = Orbit("Earth");
        IntegratorOptions settings = integrator == "RK"
            ? new RK78IntegratorOptions { AbsoluteTolerance = 1e-12, RelativeTolerance = 1e-12 }
            : new VVIntegratorOptions { Step = TimeSpan.FromSeconds(vvStep) };
        var options = new PropagationOptions { Integrator = settings, OutputStep = TimeSpan.FromSeconds(60.0) };
        var window = new Window(Start, Start.AddSeconds(3590.0));
        var formerGrid = new Window(Start, Start.AddSeconds(3600.0));

        // Act
        var solution = NewSpacecraft(initial).Propagate(window, options);
        var former = NewSpacecraft(initial).Propagate(formerGrid, options);

        // Assert: the last integrator step starts at the last step boundary before the window end
        var steps = solution.Segments.SelectMany(segment => segment.Steps).ToArray();
        double lastStepStart = steps[^1].CumulativeTime;
        var argument = PropagationTimeArgument.For(initial.Observer);
        int moved = 0;
        for (int i = 0; i < 60; i++)
        {
            var state = solution.StateVectors[i];
            var reference = former.StateVectors[i];
            LabelAssert.Equal(reference.Epoch, state.Epoch);
            double t = argument.SecondsBetween(Start, state.Epoch);
            bool same = state.Position == reference.Position && state.Velocity == reference.Velocity;
            if (t <= lastStepStart || integrator == "RK" || 60.0 % vvStep == 0.0)
            {
                Assert.True(same, $"output {i} at {t} s moved by {(state.Position - reference.Position).Magnitude():E3} m");
            }
            else
            {
                Assert.False(same, $"output {i} at {t} s");
                moved++;
                _output.WriteLine(
                    $"MEASURE #668 L4 decision 8, VV {vvStep} s: output {i} at {t} s moved by " +
                    $"{(state.Position - reference.Position).Magnitude():E6} m");
            }
        }

        Assert.Equal(integrator == "VV" && vvStep == 100.0 ? 1 : 0, moved);
        Assert.Equal(61, solution.StateVectors.Count);
        LabelAssert.Equal(window.EndDate, solution.StateVectors[^1].Epoch);
    }
}
