using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>eclipse</c> category (spec.md section 12.2): eclipses of the Sun seen from a spacecraft on the two-body orbit
/// of the case's elements, with the case's GM (<c>gm_km3_s2</c>, the kernel GM of the central body when the case gives
/// none, <see cref="TwoBodyTrajectory"/>), each occulting body the ellipsoid of its radii oriented by its IAU frame, the
/// light source its own shape. The search is the library's (managed: the spacecraft is propagated), its transitions
/// located to 1 µs.
/// </summary>
/// <remarks>
/// <para>
/// The first branch (<c>occulting_body</c>, 001) reports the first eclipse of the window, geometric, about the
/// occulting body. The second (<c>occulting_bodies</c>, 002 to 006) reports every event of the window, in time order:
/// for each occulting body its <see cref="OccultationType.Full"/> (umbra), <see cref="OccultationType.Annular"/>
/// (antumbra) and <see cref="OccultationType.Partial"/> (penumbra, each partial interval its own event) windows; the
/// trajectory about the Earth, built apart from the occulting bodies (design of lot 4, R1 to R3); the light time
/// <c>none</c> or <c>cn</c> (<see cref="LightTimeFlags"/>).
/// </para>
/// <para>
/// The case computes with the files of the suite's <c>data/</c> it lists (<see cref="CaseKernels"/>).
/// </para>
/// </remarks>
public class EclipseSolver : ICategorySolver
{
    // The search step: the managed search finds every eclipse of a satellite with a step of at most a quarter of its
    // period, and here the windows of 2083 s and 2064 s, and the partial phases of 9.3 s between them; the Moon's umbra
    // of 17 s and antumbra of 22 s seen from a LEO (005, 006)
    private static readonly TimeSpan SearchStep = TimeSpan.FromSeconds(60.0);

    // The output step of the propagation, from which the search interpolates the states (Lagrange, 8 points)
    private static readonly TimeSpan OutputStep = TimeSpan.FromSeconds(10.0);

    // The tolerance of RK7(8)
    private const double Tolerance = 1e-12;

    /// <summary>The array of the events of the second branch (spec.md section 12.2).</summary>
    public const string Events = "events";

    /// <summary>The type of an event of the second branch: umbra, antumbra or penumbra (a partial interval).</summary>
    internal static string EventType(OccultationType type) => type switch
    {
        OccultationType.Full => "umbra",
        OccultationType.Annular => "antumbra",
        OccultationType.Partial => "penumbra",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "An event is total, annular or partial.")
    };

    private static readonly OccultationType[] EventTypes = [OccultationType.Full, OccultationType.Annular, OccultationType.Partial];

    private readonly string _conformanceTestsPath;
    private readonly TwoBodyTrajectory _trajectory;

    /// <param name="conformanceTestsPath">The root of the suite, whose <c>data/</c> holds the kernels of the cases and
    /// the field file that carries a case GM.</param>
    public EclipseSolver(string conformanceTestsPath)
    {
        _conformanceTestsPath = conformanceTestsPath;
        _trajectory = new TwoBodyTrajectory(conformanceTestsPath);
    }

    public string Category => "eclipse";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseEclipseInputs(caseInput.Inputs);
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));
        return inputs.OccultingBodies == null ? FirstEclipse(inputs) : EveryEvent(inputs);
    }

    // The first branch (001): the first eclipse of the window, about the occulting body, geometric
    private Dictionary<string, object> FirstEclipse(EclipseInputs inputs)
    {
        var epoch = EpochParser.Parse(inputs.Epoch);
        var searchWindow = new Window(EpochParser.Parse(inputs.SearchWindow.Start), EpochParser.Parse(inputs.SearchWindow.End));

        // After the kernels of the case, so that the bodies take their radii
        var occultingBody = BodyResolver.Resolve(inputs.OccultingBody);
        var lightSource = BodyResolver.Resolve(inputs.LightSource);
        var spacecraft = PropagatedSpacecraft(inputs, occultingBody, epoch, searchWindow);

        var options = new OccultationSearchOptions { FrontFrame = IauFrame(occultingBody) };
        var penumbra = lightSource.FindWindowsOnOccultationConstraint(searchWindow, spacecraft, ShapeType.Ellipsoid, occultingBody,
                ShapeType.Ellipsoid, OccultationType.Any, Aberration.None, SearchStep, options)
            .FirstOrDefault();
        var umbra = penumbra == default
            ? default
            : lightSource.FindWindowsOnOccultationConstraint(penumbra, spacecraft, ShapeType.Ellipsoid, occultingBody,
                    ShapeType.Ellipsoid, OccultationType.Full, Aberration.None, SearchStep, options)
                .FirstOrDefault();

        return new Dictionary<string, object>
        {
            ["penumbra_entry"] = Output(penumbra, () => penumbra.StartDate),
            ["umbra_entry"] = Output(umbra, () => umbra.StartDate),
            ["umbra_exit"] = Output(umbra, () => umbra.EndDate),
            ["penumbra_exit"] = Output(penumbra, () => penumbra.EndDate),
            ["penumbra_duration_s"] = Output(umbra, () => penumbra.Length.TotalSeconds - umbra.Length.TotalSeconds),
            ["umbra_duration_s"] = Output(umbra, () => umbra.Length.TotalSeconds)
        };
    }

    // The second branch (002 to 006): every event of every occulting body, about the Earth, in time order
    private Dictionary<string, object> EveryEvent(EclipseInputs inputs)
    {
        var aberration = LightTimeFlags.Map(inputs.LightTime ?? "none", LightTimeFlags.Eclipse, "an eclipse case");
        var epoch = EpochParser.Parse(inputs.Epoch);
        var searchWindow = new Window(EpochParser.Parse(inputs.SearchWindow.Start), EpochParser.Parse(inputs.SearchWindow.End));
        if (inputs.OccultingBodies.Count == 0)
        {
            throw new ArgumentException("An eclipse case of the second branch lists one occulting body or more (spec.md section 12.2).");
        }

        var lightSource = BodyResolver.Resolve(inputs.LightSource);
        var occultingBodies = inputs.OccultingBodies.Select(BodyResolver.Resolve).ToList();
        var spacecraft = PropagatedSpacecraft(inputs, new CelestialBody(PlanetsAndMoons.EARTH), epoch, searchWindow);

        var events = new List<(CelestialBody Body, OccultationType Type, Window Window)>();
        foreach (var body in occultingBodies)
        {
            var options = new OccultationSearchOptions { FrontFrame = IauFrame(body) };
            foreach (var type in EventTypes)
            {
                events.AddRange(lightSource.FindWindowsOnOccultationConstraint(searchWindow, spacecraft, ShapeType.Ellipsoid, body,
                        ShapeType.Ellipsoid, type, aberration, SearchStep, options)
                    .Select(window => (body, type, window)));
            }
        }

        events = events.OrderBy(e => e.Window.StartDate).ThenBy(e => e.Window.EndDate).ToList();
        var outputs = new Dictionary<string, object> { [Events] = events.Count };
        for (int k = 0; k < events.Count; k++)
        {
            var (body, type, window) = events[k];
            outputs[IndexedOutput(k, "body")] = BodyName(body);
            outputs[IndexedOutput(k, "type")] = EventType(type);
            outputs[IndexedOutput(k, "start")] = window.StartDate;
            outputs[IndexedOutput(k, "stop")] = window.EndDate;
            outputs[IndexedOutput(k, "duration_s")] = window.Length.TotalSeconds;
        }

        return outputs;
    }

    private static string IndexedOutput(int k, string field) => OutputComparer.IndexedOutput(Events, k, field);

    // The name of an occulting body in a golden: Earth, Moon
    private static string BodyName(CelestialBody body)
    {
        return body.NaifId == PlanetsAndMoons.MOON.NaifId ? "Moon" : body.NaifId == PlanetsAndMoons.EARTH.NaifId ? "Earth" : body.Name;
    }

    // The orientation of the shape of an occulting body, its IAU frame (spec.md section 12.2)
    private static Frame IauFrame(CelestialBody body) => new($"IAU_{body.Name.ToUpperInvariant()}");

    // The spacecraft on the two-body orbit of the elements about the body, propagated over the window
    private Spacecraft PropagatedSpacecraft(EclipseInputs inputs, CelestialBody body, Time epoch, Window searchWindow)
    {
        if (inputs.Orbit is not KeplerianOrbit kep)
        {
            throw new ArgumentException("The eclipse category takes Keplerian elements.");
        }

        double gm = TwoBodyTrajectory.Gm(inputs.GmKm3S2, body);
        var centralBody = _trajectory.CentralBody(body, gm, epoch);
        var spacecraft = new Spacecraft(-999, "ConformanceTestSC", 100.0, 1000.0, new Clock("ConfClk", 65536),
            TwoBodyTrajectory.KeplerianState(kep, body, centralBody, gm, epoch));
        TwoBodyTrajectory.Propagate(spacecraft, searchWindow, Tolerance, OutputStep);
        return spacecraft;
    }

    // An output of a window, or the exception that says it was not found
    private static object Output(Window window, Func<object> value)
    {
        return window == default
            ? new InvalidOperationException("No eclipse of that type in the search window.")
            : value();
    }
}
