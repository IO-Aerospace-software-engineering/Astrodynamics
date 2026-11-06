using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>ground_station</c> category (spec.md section 14.1): the passes of a target above the elevation mask of a
/// station on the Earth, each pass's rise, set, duration, culmination and maximum elevation, and the azimuth,
/// elevation, range and range rate of the target at the sample epochs, with the light time of the case.
/// </summary>
/// <remarks>
/// <para>
/// The target is a spacecraft on the two-body orbit of the case's state with its GM (<see cref="TwoBodyTrajectory"/>),
/// its states stored every 10 s, or a spacecraft of an SPK of the suite's <c>data/</c>. The station is a
/// <see cref="Site"/> of the case's geodetic coordinates on the Earth, whose ellipsoid must be the case's
/// (<see cref="TrackingDataSolver.CheckEllipsoid"/>), its frame the topocentric frame of the ellipsoid's normal.
/// </para>
/// <para>
/// A pass is a window of the library's search of the declination of the target in the station's frame, its elevation,
/// above the mask, minus the windows in which an occulting body (the Moon's sphere) hides the target seen as a point,
/// the same light time on both searches; its culmination the absolute maximum of the elevation over the pass, which is
/// an edge when the elevation is monotonic through it. The searches are the library's managed ones, each edge to 1 µs
/// (#590). The light-time flags map as <see cref="LightTimeFlags"/>; the range rate is r·v/|r| of the state of the
/// target relative to the station in ICRF, with the same correction.
/// </para>
/// </remarks>
public class GroundStationSolver : ICategorySolver
{
    /// <summary>The array of the passes of a golden (spec.md section 14.1).</summary>
    public const string Passes = "passes";

    /// <summary>The array of the samples of a golden (spec.md section 14.1).</summary>
    public const string Samples = "samples";

    // The output step of the two-body propagation, from which the searches interpolate the states (Lagrange, 8 points)
    private static readonly TimeSpan OutputStep = TimeSpan.FromSeconds(10.0);

    // The tolerance of RK7(8) (design of lot 4, section 7.1)
    private const double Tolerance = 1e-13;

    private readonly string _conformanceTestsPath;
    private readonly TwoBodyTrajectory _trajectory;

    /// <param name="conformanceTestsPath">The root of the suite, whose <c>data/</c> holds the kernels of the cases, the
    /// SPK of a target and the field file that carries a case GM.</param>
    public GroundStationSolver(string conformanceTestsPath)
    {
        _conformanceTestsPath = conformanceTestsPath;
        _trajectory = new TwoBodyTrajectory(conformanceTestsPath);
    }

    public string Category => "ground_station";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseGroundStationInputs(caseInput.Inputs);
        var aberration = LightTimeFlags.Map(inputs.Aberration, LightTimeFlags.Station, "a ground_station case");
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));

        // The SPK of the target, unloaded with the case, so that the suite holds no file SPICE still reads
        using var spk = inputs.Orbit is BspSourceOrbit bsp ? new CaseKernels(_conformanceTestsPath, [SpkName(bsp)]) : null;

        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        TrackingDataSolver.CheckEllipsoid(inputs.Location.Ellipsoid, earth);
        var site = new Site(888, "ConformanceSite", earth,
            new Planetodetic(
                UnitConversion.DegToRad(inputs.Location.LongitudeDeg),
                UnitConversion.DegToRad(inputs.Location.LatitudeDeg),
                UnitConversion.KmToM(inputs.Location.HeightKm)));
        var occulters = inputs.OccultingBodies.Select(Occulter).ToList();

        var window = new Window(EpochParser.Parse(inputs.SearchWindow.Start), EpochParser.Parse(inputs.SearchWindow.End));
        var target = Target(inputs, earth, window);
        var step = TimeSpan.FromSeconds(inputs.StepSizeS);
        double mask = UnitConversion.DegToRad(inputs.ElevationMaskDeg);

        var outputs = new Dictionary<string, object>();
        var passes = FindPasses(target, site, occulters, window, mask, aberration, step);
        outputs[Passes] = passes.Count;
        for (int k = 0; k < passes.Count; k++)
        {
            var pass = passes[k];
            var culmination = target.FindWindowsOnCoordinateConstraint(pass, site, site.Frame, CoordinateSystem.RaDec, Coordinate.Declination,
                RelationnalOperator.AbsoluteMax, 0.0, 0.0, aberration, step).First().StartDate;
            outputs[OutputComparer.IndexedOutput(Passes, k, "rise")] = pass.StartDate;
            outputs[OutputComparer.IndexedOutput(Passes, k, "set")] = pass.EndDate;
            outputs[OutputComparer.IndexedOutput(Passes, k, "duration_s")] = pass.Length.TotalSeconds;
            outputs[OutputComparer.IndexedOutput(Passes, k, "culmination")] = culmination;
            outputs[OutputComparer.IndexedOutput(Passes, k, "max_elevation_deg")] =
                UnitConversion.RadToDeg(site.GetHorizontalCoordinates(culmination, target, aberration).Elevation);
        }

        outputs[Samples] = inputs.SampleEpochs.Count;
        for (int k = 0; k < inputs.SampleEpochs.Count; k++)
        {
            var epoch = EpochParser.ParseInstant(inputs.SampleEpochs[k]);
            var horizontal = site.GetHorizontalCoordinates(epoch, target, aberration);
            var state = target.GetEphemeris(epoch, site, Frame.ICRF, aberration).ToStateVector();
            outputs[OutputComparer.IndexedOutput(Samples, k, "epoch")] = inputs.SampleEpochs[k];
            outputs[OutputComparer.IndexedOutput(Samples, k, "azimuth_deg")] = UnitConversion.RadToDeg(horizontal.Azimuth);
            outputs[OutputComparer.IndexedOutput(Samples, k, "elevation_deg")] = UnitConversion.RadToDeg(horizontal.Elevation);
            outputs[OutputComparer.IndexedOutput(Samples, k, "range_km")] = UnitConversion.MToKm(horizontal.Range);
            outputs[OutputComparer.IndexedOutput(Samples, k, "range_rate_km_s")] =
                UnitConversion.MToKm(state.Position * state.Velocity / state.Position.Magnitude());
        }

        return outputs;
    }

    /// <summary>
    /// The passes: the windows of the elevation above the mask, minus those in which an occulting body hides the target,
    /// in time order; a piece of zero length is no pass.
    /// </summary>
    internal static List<Window> FindPasses(Spacecraft target, Site site, IReadOnlyList<CelestialBody> occulters, Window window,
        double mask, Aberration aberration, TimeSpan step)
    {
        var passes = target.FindWindowsOnCoordinateConstraint(window, site, site.Frame, CoordinateSystem.RaDec, Coordinate.Declination,
            RelationnalOperator.Greater, mask, 0.0, aberration, step).ToList();
        foreach (var occulter in occulters)
        {
            var hidden = target.FindWindowsOnOccultationConstraint(window, site, ShapeType.Point, occulter, ShapeType.Ellipsoid,
                OccultationType.Full, aberration, step).ToList();
            passes = Subtract(passes, hidden);
        }

        return passes.Where(p => p.EndDate > p.StartDate).OrderBy(p => p.StartDate).ToList();
    }

    // The parts of the windows outside every removed window
    internal static List<Window> Subtract(IEnumerable<Window> windows, IReadOnlyList<Window> removed)
    {
        var result = new List<Window>();
        foreach (var window in windows)
        {
            var remaining = new List<Window> { window };
            foreach (var cut in removed)
            {
                var next = new List<Window>();
                foreach (var part in remaining)
                {
                    if (cut.EndDate <= part.StartDate || cut.StartDate >= part.EndDate)
                    {
                        next.Add(part);
                        continue;
                    }

                    if (cut.StartDate > part.StartDate)
                    {
                        next.Add(new Window(part.StartDate, cut.StartDate));
                    }

                    if (cut.EndDate < part.EndDate)
                    {
                        next.Add(new Window(cut.EndDate, part.EndDate));
                    }
                }

                remaining = next;
            }

            result.AddRange(remaining);
        }

        return result;
    }

    // The prefix of the SPK of a bsp_source orbit, a file of data/ (spec.md section 14.1)
    private const string DataPrefix = "data/";

    // The name in data/ of the SPK of a target
    private static string SpkName(BspSourceOrbit bsp)
    {
        return bsp.Filename != null && bsp.Filename.StartsWith(DataPrefix, StringComparison.Ordinal)
            ? bsp.Filename[DataPrefix.Length..]
            : throw new ArgumentException($"The SPK '{bsp.Filename}' of the target is not a file of data/ (spec.md section 14.1).");
    }

    // An occulting body of a station case: the Moon only (spec.md section 14.1)
    private static CelestialBody Occulter(string name)
    {
        return string.Equals(name, "Moon", StringComparison.OrdinalIgnoreCase)
            ? new CelestialBody(PlanetsAndMoons.MOON)
            : throw new ArgumentException($"The occulting body '{name}' of a ground_station case is not the Moon (spec.md section 14.1).");
    }

    // The target: a spacecraft of the SPK of the case, loaded by the caller, or on the two-body orbit of the state, propagated over the window
    private Spacecraft Target(GroundStationInputs inputs, CelestialBody earth, Window window)
    {
        if (inputs.Orbit is BspSourceOrbit bsp)
        {
            return new Spacecraft(bsp.NaifId, "StationTarget", epoch: window.StartDate);
        }

        if (inputs.Orbit is not StateVectorOrbit svo)
        {
            throw new ArgumentException("A ground_station case gives a state_vector or a bsp_source orbit (spec.md section 14.1).");
        }

        var epoch = EpochParser.Parse(inputs.Epoch);
        var central = _trajectory.CentralBody(earth, TwoBodyTrajectory.Gm(inputs.GmKm3S2, earth), epoch);
        var state = PropagationCase.BuildStateVector(svo, central, epoch, Frame.ICRF);
        var spacecraft = new Spacecraft(-999, "StationTarget", 100.0, 1000.0, new Clock("ConfClk", 65536), state);
        TwoBodyTrajectory.Propagate(spacecraft, window, Tolerance, OutputStep);
        return spacecraft;
    }
}
