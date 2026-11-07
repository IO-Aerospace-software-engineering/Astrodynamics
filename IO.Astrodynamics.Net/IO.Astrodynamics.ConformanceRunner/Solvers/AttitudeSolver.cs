using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>attitude</c> category (spec.md section 14.3): an attitude law along the two-body orbit of the case's state
/// with its GM (<see cref="TwoBodyTrajectory"/>), body → ICRF at t0 + k·<c>sample_step_s</c> TT seconds, or the
/// direction in ICRF of a single body axis.
/// </summary>
/// <remarks>
/// <para>
/// A law of two axes is the library's <see cref="TriadAttitude"/> of the two body vectors toward the orbital
/// directions <c>nadir</c> → <see cref="OrbitalDirectionTarget.Nadir"/>, <c>velocity</c> →
/// <see cref="OrbitalDirectionTarget.Prograde"/>, <c>orbit_normal</c> → <see cref="OrbitalDirectionTarget.Normal"/>; a
/// single axis the spacecraft's front (<see cref="Spacecraft.BodyFront"/>, the case's body vector) toward its direction,
/// <see cref="NadirAttitude"/>, <see cref="ProgradeAttitude"/> or <see cref="NormalAttitude"/>. The law is set at the
/// start with no hold, on a spacecraft with a tank and an engine, which an attitude requires and which burns nothing.
/// </para>
/// <para>
/// The propagation stores its states and attitudes every <c>sample_step_s</c> of its time argument, TT seconds about
/// the Earth (#501), so that every sample is a recorded attitude: between two records the attitude record is the
/// SLERP of the two, not the law (design of lot 4, section 7.5).
/// </para>
/// </remarks>
public class AttitudeSolver : ICategorySolver
{
    /// <summary>The array of the attitudes of a golden of two axes (spec.md section 14.3).</summary>
    public const string Quaternions = "attitude_quaternions";

    /// <summary>The array of the directions of a golden of a single axis (spec.md section 14.3).</summary>
    public const string Directions = "pointing_directions";

    // The tolerance of RK7(8) (design of lot 4, section 7.1)
    private const double Tolerance = 1e-12;

    // A sample count within this fraction of a whole number of steps is that number (spec.md section 14.3: the window's
    // TT length is a whole multiple of the step)
    private const double WholeStepsTolerance = 1e-9;

    private readonly string _conformanceTestsPath;
    private readonly TwoBodyTrajectory _trajectory;

    /// <param name="conformanceTestsPath">The root of the suite, whose <c>data/</c> holds the kernels of the cases and
    /// the field file that carries a case GM.</param>
    public AttitudeSolver(string conformanceTestsPath)
    {
        _conformanceTestsPath = conformanceTestsPath;
        _trajectory = new TwoBodyTrajectory(conformanceTestsPath);
    }

    public string Category => "attitude";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseAttitudeInputs(caseInput.Inputs);
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));

        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var epoch = EpochParser.Parse(inputs.Epoch);
        var window = new Window(EpochParser.Parse(inputs.PropagationWindow.Start), EpochParser.Parse(inputs.PropagationWindow.End));
        int samples = SampleCount(window, inputs.SampleStepS);

        var central = _trajectory.CentralBody(earth, TwoBodyTrajectory.Gm(inputs.GmKm3S2, earth), epoch);
        var state = PropagationCase.BuildStateVector(inputs.Orbit, central, epoch, Frame.ICRF);
        var front = inputs.Pointing == null ? (Vector3?)null : Axis(inputs.Pointing.BodyVector);
        var spacecraft = front.HasValue
            ? new Spacecraft(-999, "AttitudeTarget", 100.0, 1000.0, new Clock("ConfClk", 65536), state,
                bodyFront: front.Value, bodyRight: RightOf(front.Value), bodyUp: RightOf(front.Value).Cross(front.Value))
            : new Spacecraft(-999, "AttitudeTarget", 100.0, 1000.0, new Clock("ConfClk", 65536), state);
        var tank = new FuelTank("ConformanceTank", "conformance", "0001", 100.0, 100.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("ConformanceEngine", "conformance", "0001", 300.0, 1.0, tank);
        spacecraft.AddEngine(engine);
        spacecraft.SetStandbyManeuver(Law(inputs, central, window.StartDate, engine));

        TwoBodyTrajectory.Propagate(spacecraft, window, Tolerance, TimeSpan.FromSeconds(inputs.SampleStepS));

        var record = (SpacecraftFrame)spacecraft.Frame;
        string array = front.HasValue ? Directions : Quaternions;
        var outputs = new Dictionary<string, object> { [array] = samples };
        for (int k = 0; k < samples; k++)
        {
            var rotation = record.GetStateOrientationToICRF(window.StartDate.ToTDT().AddSeconds(k * inputs.SampleStepS)).Rotation;
            outputs[OutputComparer.IndexedOutput(array, k)] = front.HasValue
                ? Components(front.Value.Rotate(rotation).Normalize())
                : new[] { rotation.W, rotation.VectorPart.X, rotation.VectorPart.Y, rotation.VectorPart.Z };
        }

        return outputs;
    }

    /// <summary>
    /// The number of samples, t0 + k·step for k = 0 … N, the last at the end of the window (spec.md section 14.3).
    /// </summary>
    /// <exception cref="ArgumentException">The step is not positive, or the TT length of the window is not a whole
    /// multiple of it.</exception>
    internal static int SampleCount(Window window, double step)
    {
        double length = (window.EndDate.ToTDT() - window.StartDate.ToTDT()).TotalSeconds;
        double steps = step > 0.0 ? length / step : double.NaN;
        if (!(System.Math.Abs(steps - System.Math.Round(steps)) <= WholeStepsTolerance * System.Math.Max(1.0, steps)))
        {
            throw new ArgumentException(
                $"The TT length of the window, {length:R} s, is not a whole multiple of sample_step_s {step:R} s (spec.md section 14.3).");
        }

        return (int)System.Math.Round(steps) + 1;
    }

    /// <summary>The library's law of the case (see the remarks of <see cref="AttitudeSolver"/>).</summary>
    internal static Attitude Law(AttitudeInputs inputs, CelestialBody center, Time start, Engine engine)
    {
        if (inputs.Pointing != null)
        {
            return inputs.Pointing.Direction switch
            {
                AttitudeAxis.Nadir => new NadirAttitude(center, start, TimeSpan.Zero, engine),
                AttitudeAxis.Velocity => new ProgradeAttitude(center, start, TimeSpan.Zero, engine),
                AttitudeAxis.OrbitNormal => new NormalAttitude(center, start, TimeSpan.Zero, engine),
                _ => throw UnknownDirection(inputs.Pointing.Direction)
            };
        }

        return new TriadAttitude(center, start, TimeSpan.Zero, Axis(inputs.Primary.BodyVector), Target(inputs.Primary.Direction),
            Axis(inputs.Secondary.BodyVector), Target(inputs.Secondary.Direction), engine);
    }

    /// <summary>The library's orbital direction of a direction of the suite.</summary>
    internal static OrbitalDirectionTarget Target(string direction)
    {
        return direction switch
        {
            AttitudeAxis.Nadir => OrbitalDirectionTarget.Nadir,
            AttitudeAxis.Velocity => OrbitalDirectionTarget.Prograde,
            AttitudeAxis.OrbitNormal => OrbitalDirectionTarget.Normal,
            _ => throw UnknownDirection(direction)
        };
    }

    private static ArgumentException UnknownDirection(string direction) =>
        new($"The direction '{direction}' is not nadir, velocity or orbit_normal (spec.md section 14.3).");

    private static Vector3 Axis(double[] v) => new(v[0], v[1], v[2]);

    // A body axis orthogonal to a signed unit axis: the next axis in X, Y, Z order
    private static Vector3 RightOf(Vector3 front)
    {
        return System.Math.Abs(front.X) > 0.5 ? Vector3.VectorY : System.Math.Abs(front.Y) > 0.5 ? Vector3.VectorZ : Vector3.VectorX;
    }

    private static double[] Components(Vector3 v) => new[] { v.X, v.Y, v.Z };
}
