using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>maneuver</c> category (spec.md section 13.1): a chain of impulsive maneuvers executed by the framework's
/// maneuvers during the propagation of the <c>propagator</c> category (<see cref="PropagationCase"/>,
/// <see cref="PropagatorSolver.CreateIntegrator"/>), each burn's epoch, position, Δv and mass after it, and the final
/// state.
/// </summary>
/// <remarks>
/// <para>
/// Each step type is the framework's maneuver of the same event and closed form, on the osculating state with the kernel
/// GM (<see cref="CreateManeuver"/>). The spacecraft has the case's dry mass, a tank with its fuel and an engine with its
/// Isp, whose fuel flow (<see cref="FuelFlow"/>) makes the thrust and maneuver windows of an impulse vanish, so that the
/// gate of the next maneuver, the end of the previous maneuver window, is the previous burn plus its hold (decision 17
/// of the design of #587). The framework's hold belongs to the maneuver that sets it: the hold of step k is the
/// <c>earliest_after_previous_s</c> of step k + 1, the minimum epoch of the first step the start plus its own.
/// <see cref="PhasingManeuver"/> sets its own hold, 0.9 N periods of its phasing orbit; the suite's cases fire the next
/// step at the same event (spec.md section 13.1: the event 600 s or more after its earliest epoch).
/// </para>
/// <para>
/// The epoch of a burn is the base of the segment the impulse opens (decision 16), in TT seconds from the start; its
/// position the start of that segment; its Δv the framework's, the velocity jump it applies in ICRF; its mass the total
/// mass at the start minus the fuel of the burns up to it. A step that is not executed gives its outputs as the reason
/// (<see cref="SolverOutput"/>): an ERROR of the case, since the golden holds them.
/// </para>
/// </remarks>
public class ManeuverSolver : ICategorySolver
{
    /// <summary>
    /// The fuel flow of the engine, kg/s: the thrust window of a burn of 1 kg lasts 1e-12 s, far below the 100 ns tick
    /// of <see cref="Time"/>, so that the maneuver window of an impulse is its hold from the burn.
    /// </summary>
    internal const double FuelFlow = 1e12;

    // The thrust window of a burn starts 0.666 dt before it (ImpulseManeuver.TryExecute); the base of the segment the
    // impulse opens is its epoch within the tick of Time
    private const double SameEpochSeconds = 1e-6;

    private readonly string _conformanceTestsPath;
    private readonly string _dataPath;

    /// <param name="conformanceTestsPath">The root of the suite, whose <c>data/</c> holds the kernels of the cases.</param>
    /// <param name="dataPath">The directory of the field files; the suite's <c>data/</c> when null.</param>
    public ManeuverSolver(string conformanceTestsPath, string dataPath = null)
    {
        _conformanceTestsPath = conformanceTestsPath;
        _dataPath = dataPath ?? Path.Combine(conformanceTestsPath, "data");
    }

    public string Category => "maneuver";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));
        var propagationCase = PropagationCase.Build(caseInput, _dataPath);
        var chain = InputParser.ParseManeuverInputs(caseInput.Inputs);
        CheckPlanes(chain.Steps);

        var spacecraft = propagationCase.Spacecraft;
        var center = (CelestialBody)propagationCase.Bodies[0];
        var tank = new FuelTank("ConformanceTank", "conformance", "0001", chain.FuelMassKg, chain.FuelMassKg);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("ConformanceEngine", "conformance", "0001", chain.IspS, FuelFlow, tank);
        spacecraft.AddEngine(engine);
        double initialMass = spacecraft.GetTotalMass();

        var start = propagationCase.Window.StartDate;
        var initialState = spacecraft.InitialOrbitalParameters.ToStateVector();
        var maneuvers = new List<ImpulseManeuver>();
        for (int k = 0; k < chain.Steps.Count; k++)
        {
            // The hold of step k is the earliest epoch of step k + 1 after its burn; the minimum epoch of the first step
            // the start plus its earliest epoch, in TT seconds
            var minimumEpoch = start.ToTDT().AddSeconds(k == 0 ? chain.Steps[0].EarliestAfterPreviousS : 0.0);
            var hold = TimeSpan.FromSeconds(k + 1 < chain.Steps.Count ? chain.Steps[k + 1].EarliestAfterPreviousS : 0.0);
            var maneuver = CreateManeuver(chain.Steps[k], center, initialState, minimumEpoch, hold, engine);
            if (k == 0)
            {
                spacecraft.SetStandbyManeuver(maneuver);
            }
            else
            {
                maneuvers[k - 1].SetNextManeuver(maneuver);
            }

            maneuvers.Add(maneuver);
        }

        var solution = spacecraft.Propagate(propagationCase.Window, propagationCase.Bodies, PropagatorSolver.CreateIntegrator(),
            propagationCase.Drag, propagationCase.SolarRadiationPressure, propagationCase.OutputStep);

        var outputs = new Dictionary<string, object>();
        double mass = initialMass;
        string notExecuted = null;
        for (int k = 0; k < maneuvers.Count; k++)
        {
            var maneuver = maneuvers[k];
            string label = chain.Steps[k].Label;
            if (notExecuted != null || !maneuver.ThrustWindow.HasValue)
            {
                // The chain stops at the first step not executed
                var failure = new InvalidOperationException(notExecuted == null
                    ? $"the step '{label}' ({chain.Steps[k].Type}) was not executed within the window: its event did not come after its earliest epoch, or its precondition failed at every occurrence"
                    : $"the step '{label}' follows the step '{notExecuted}', which was not executed");
                notExecuted ??= label;
                foreach (var field in BurnFields)
                {
                    outputs[OutputComparer.LabelledOutput(MetricTable.Burns, label, field)] = failure;
                }

                continue;
            }

            mass -= maneuver.FuelBurned;
            var segment = BurnSegment(solution, maneuver.ThrustWindow.Value.StartDate);
            var position = segment.InterpolateAt(0.0).position;
            Add(outputs, label, "elapsed_s", TerrestrialSeconds.Between(start, segment.BaseEpoch));
            Add(outputs, label, "position_km", Kilometres(position));
            Add(outputs, label, "delta_v_km_s", Kilometres(maneuver.DeltaV));
            Add(outputs, label, "mass_after_kg", mass);
        }

        var (finalPos, finalVel) = solution.InterpolateAt(propagationCase.Window.EndDate);
        outputs["final_x_km"] = UnitConversion.MToKm(finalPos.X);
        outputs["final_y_km"] = UnitConversion.MToKm(finalPos.Y);
        outputs["final_z_km"] = UnitConversion.MToKm(finalPos.Z);
        outputs["final_vx_km_s"] = UnitConversion.MSToKmS(finalVel.X);
        outputs["final_vy_km_s"] = UnitConversion.MSToKmS(finalVel.Y);
        outputs["final_vz_km_s"] = UnitConversion.MSToKmS(finalVel.Z);
        return outputs;
    }

    /// <summary>The fields of a burn of a golden (spec.md section 13.1).</summary>
    internal static readonly string[] BurnFields = { "elapsed_s", "position_km", "delta_v_km_s", "mass_after_kg" };

    /// <summary>
    /// The framework's maneuver of a step (spec.md section 13.1), its center the central body, in the frame of the
    /// initial state (ICRF).
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="center">The central body, whose kernel GM the closed forms take.</param>
    /// <param name="initialState">The state at the start: the plane of an <c>apsidal_alignment</c> target and the
    /// frame and epoch of the target orbits.</param>
    /// <param name="minimumEpoch">The earliest epoch given to the maneuver.</param>
    /// <param name="hold">Its hold, the earliest epoch of the next step after its burn.</param>
    /// <param name="engine">The engine of the spacecraft.</param>
    /// <exception cref="ArgumentException">An unknown type.</exception>
    internal static ImpulseManeuver CreateManeuver(ManeuverStep step, CelestialBody center, StateVector initialState, Time minimumEpoch,
        TimeSpan hold, Engine engine)
    {
        var epoch = initialState.Epoch;
        var frame = initialState.Frame;
        switch (step.Type)
        {
            case ManeuverStepType.ApoapsisRadius:
                return new ApogeeHeightManeuver(center, minimumEpoch, hold, UnitConversion.KmToM(step.TargetApoapsisRadiusKm!.Value), engine);
            case ManeuverStepType.PeriapsisRadius:
                return new PerigeeHeightManeuver(center, minimumEpoch, hold, UnitConversion.KmToM(step.TargetPeriapsisRadiusKm!.Value), engine);
            case ManeuverStepType.PlaneChange:
                // A circular orbit of the target plane: the maneuver reads its normal only, fixed in ICRF
                var plane = new KeplerianElements(initialState.Position.Magnitude(), 0.0,
                    UnitConversion.DegToRad(step.TargetPlane.InclinationDeg), UnitConversion.DegToRad(step.TargetPlane.RaanDeg), 0.0, 0.0,
                    center, epoch, frame);
                return new PlaneAlignmentManeuver(minimumEpoch, hold, plane, engine);
            case ManeuverStepType.ApsidalAlignment:
                // In the current plane, the plane of the initial state (CheckPlanes): its argument of periapsis from the
                // ascending node of that plane
                var orbit = new KeplerianElements(UnitConversion.KmToM(step.TargetOrbit.AKm), step.TargetOrbit.E,
                    initialState.Inclination(), initialState.AscendingNode(), UnitConversion.DegToRad(step.TargetOrbit.ArgpDeg), 0.0,
                    center, epoch, frame);
                return new ApsidalAlignmentManeuver(minimumEpoch, hold, orbit, engine);
            case ManeuverStepType.Phasing:
                var target = PropagationCase.BuildStateVector(step.Target, center, epoch, frame);
                return new PhasingManeuver(minimumEpoch, hold, target, (uint)step.Revolutions!.Value, engine);
            case ManeuverStepType.Combined:
                return new CombinedManeuver(center, minimumEpoch, hold, UnitConversion.KmToM(step.TargetPeriapsisRadiusKm!.Value),
                    UnitConversion.DegToRad(step.TargetInclinationDeg!.Value), engine);
            default:
                throw new ArgumentException($"The type '{step.Type}' of the step '{step.Label}' is not a maneuver type of spec.md section 13.1.");
        }
    }

    /// <summary>
    /// An <c>apsidal_alignment</c> target lies in the current plane, which the runner takes from the initial state: no
    /// step before it may change the plane.
    /// </summary>
    /// <exception cref="InvalidOperationException">An <c>apsidal_alignment</c> step follows a <c>plane_change</c> or a
    /// <c>combined</c> step.</exception>
    internal static void CheckPlanes(IReadOnlyList<ManeuverStep> steps)
    {
        for (int k = 0; k < steps.Count; k++)
        {
            var planeChange = steps.Take(k).FirstOrDefault(s => s.Type is ManeuverStepType.PlaneChange or ManeuverStepType.Combined);
            if (steps[k].Type == ManeuverStepType.ApsidalAlignment && planeChange != null)
            {
                throw new InvalidOperationException(
                    $"The apsidal_alignment step '{steps[k].Label}' follows the step '{planeChange.Label}', which changes the plane: the runner takes the plane of its target from the initial state.");
            }
        }
    }

    // The segment the impulse opens, which starts at its epoch; a segment of an event skipped by its precondition starts
    // at another epoch
    private static PropagationSegment BurnSegment(PropagationSolution solution, Time thrustStart)
    {
        return solution.Segments.Skip(1).FirstOrDefault(s => System.Math.Abs((s.BaseEpoch - thrustStart).TotalSeconds) < SameEpochSeconds)
               ?? throw new InvalidOperationException($"No segment of the solution starts at the impulse of {thrustStart}.");
    }

    private static void Add(Dictionary<string, object> outputs, string label, string field, object value)
    {
        outputs[OutputComparer.LabelledOutput(MetricTable.Burns, label, field)] = value;
    }

    private static double[] Kilometres(Vector3 metres)
    {
        return new[] { UnitConversion.MToKm(metres.X), UnitConversion.MToKm(metres.Y), UnitConversion.MToKm(metres.Z) };
    }
}
