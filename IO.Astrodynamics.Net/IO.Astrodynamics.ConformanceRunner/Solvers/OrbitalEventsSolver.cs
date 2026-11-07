using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>orbital_events</c> category (spec.md section 14.2): the apsides with their radii, the nodes on the ICRF equator
/// and the crossings of a geodetic altitude of the propagation of the <c>propagator</c> category
/// (<see cref="PropagationCase"/>, <see cref="PropagatorSolver.CreateIntegrator"/>), each epoch in TT seconds from the
/// start.
/// </summary>
/// <remarks>
/// The propagated spacecraft stores its states every 10 s; the events are the library's window searches on them at the
/// case's <c>step_size_s</c>, each edge to 1 µs (#590, #486; design of lot 4, open choice 15): the apsides the local
/// minima and maxima of the distance to the central body, the ascending and descending nodes the starts and ends of the
/// windows of Z &gt; 0 in ICRF, the altitude crossings up and down the starts and ends of the windows of the geodetic
/// altitude in the central body's frame above the value. An edge at a bound of the window is no crossing: a window that
/// holds at the start starts there (#461). The radius of an apsis is |r| of the stored state there.
/// </remarks>
public class OrbitalEventsSolver : ICategorySolver
{
    // The output step of the propagation, from which the searches interpolate the states (Lagrange, 8 points)
    private static readonly TimeSpan OutputStep = TimeSpan.FromSeconds(10.0);

    private readonly string _conformanceTestsPath;
    private readonly string _dataPath;

    /// <param name="conformanceTestsPath">The root of the suite, whose <c>data/</c> holds the kernels of the cases.</param>
    /// <param name="dataPath">The directory of the field files; the suite's <c>data/</c> when null.</param>
    public OrbitalEventsSolver(string conformanceTestsPath, string dataPath = null)
    {
        _conformanceTestsPath = conformanceTestsPath;
        _dataPath = dataPath ?? Path.Combine(conformanceTestsPath, "data");
    }

    public string Category => "orbital_events";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var events = InputParser.ParseOrbitalEvents(caseInput.Inputs);
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));
        var propagationCase = PropagationCase.Build(caseInput, _dataPath);
        var spacecraft = propagationCase.Spacecraft;
        var center = (CelestialBody)propagationCase.Bodies[0];
        var window = propagationCase.Window;
        spacecraft.Propagate(window, propagationCase.Bodies, PropagatorSolver.CreateIntegrator(), propagationCase.Drag,
            propagationCase.SolarRadiationPressure, OutputStep);

        var step = propagationCase.StepSize;
        var outputs = new Dictionary<string, object>();
        foreach (var request in events)
        {
            switch (request.Type)
            {
                case OrbitalEventRequest.Periapsis:
                case OrbitalEventRequest.Apoapsis:
                    var extremes = spacecraft.FindWindowsOnDistanceConstraint(window, center,
                        request.Type == OrbitalEventRequest.Periapsis ? RelationnalOperator.LocalMin : RelationnalOperator.LocalMax,
                        0.0, Aberration.None, step).Select(w => w.StartDate).ToList();
                    Add(outputs, request.Type, extremes, window.StartDate,
                        t => UnitConversion.MToKm(spacecraft.GetEphemeris(t, center, Frame.ICRF, Aberration.None).ToStateVector().Position.Magnitude()));
                    break;
                case OrbitalEventRequest.AscendingNode:
                case OrbitalEventRequest.DescendingNode:
                    var north = spacecraft.FindWindowsOnCoordinateConstraint(window, center, Frame.ICRF, CoordinateSystem.Rectangular, Coordinate.Z,
                        RelationnalOperator.Greater, 0.0, 0.0, Aberration.None, step);
                    Add(outputs, request.Type, Crossings(north, window, request.Type == OrbitalEventRequest.AscendingNode), window.StartDate);
                    break;
                case OrbitalEventRequest.Altitude:
                    var above = spacecraft.FindWindowsOnCoordinateConstraint(window, center, center.Frame, CoordinateSystem.Geodetic, Coordinate.Altitude,
                        RelationnalOperator.Greater, UnitConversion.KmToM(request.ValueKm!.Value), 0.0, Aberration.None, step).ToList();
                    if (request.Direction != OrbitalEventRequest.Down)
                    {
                        Add(outputs, "altitude_up", Crossings(above, window, true), window.StartDate);
                    }

                    if (request.Direction != OrbitalEventRequest.Up)
                    {
                        Add(outputs, "altitude_down", Crossings(above, window, false), window.StartDate);
                    }

                    break;
                default:
                    throw new ArgumentException($"The event type '{request.Type}' is not an event of spec.md section 14.2.");
            }
        }

        return outputs;
    }

    /// <summary>
    /// The crossings of the windows in which a function is positive: their starts (<paramref name="upward"/>, − to +) or
    /// their ends (+ to −), but those at the bounds of the search window, which are no crossings.
    /// </summary>
    internal static List<Time> Crossings(IEnumerable<Window> windows, Window search, bool upward)
    {
        return windows.Select(w => upward ? w.StartDate : w.EndDate)
            .Where(t => t > search.StartDate && t < search.EndDate)
            .ToList();
    }

    // The list events.<list>: its count and, for each event, its epoch in TT seconds from the start and, for an apsis,
    // its radius
    private static void Add(Dictionary<string, object> outputs, string list, IReadOnlyList<Time> epochs, Time start,
        Func<Time, double> radiusKm = null)
    {
        string name = MetricTable.EventList(list);
        outputs[name] = epochs.Count;
        for (int k = 0; k < epochs.Count; k++)
        {
            outputs[OutputComparer.IndexedOutput(name, k, "elapsed_s")] = (epochs[k].ToTDT() - start.ToTDT()).TotalSeconds;
            if (radiusKm != null)
            {
                outputs[OutputComparer.IndexedOutput(name, k, "radius_km")] = radiusKm(epochs[k]);
            }
        }
    }
}
