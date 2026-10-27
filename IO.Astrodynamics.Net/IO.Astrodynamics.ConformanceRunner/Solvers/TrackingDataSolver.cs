using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>tracking_data</c> category (spec.md section 12.3): the range and range-rate statistics of a spacecraft of an
/// SPK seen from a station on the Earth, over the visible samples of a UTC grid, and the arcs of visibility, geometric.
/// </summary>
/// <remarks>
/// The case computes with the files of the suite's <c>data/</c> it lists (<see cref="CaseKernels"/>). The library's
/// <see cref="Site"/> lies on the ellipsoid of its body's kernel radii: the case's ellipsoid must be that one, to the
/// rounding of its flattening, or the case is an ERROR.
/// </remarks>
public class TrackingDataSolver : ICategorySolver
{
    // The station on the declared ellipsoid: its radii those of the body within 1 µm
    private const double EllipsoidToleranceMetres = 1e-6;

    private readonly string _conformanceTestsPath;

    public string Category => "tracking_data";

    public TrackingDataSolver(string conformanceTestsPath)
    {
        _conformanceTestsPath = conformanceTestsPath;
    }

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseTrackingDataInputs(caseInput.Inputs);
        var bspOrbit = (BspSourceOrbit)inputs.Orbit;
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));

        // Load the BSP kernel (path is relative to conformance-tests root)
        var bspPath = Path.Combine(_conformanceTestsPath, bspOrbit.Filename);
        SuiteDataFile.EnsureContent(bspPath);
        SpiceAPI.Instance.LoadKernels(new FileInfo(bspPath));

        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        CheckEllipsoid(inputs.Location.Ellipsoid, earth);

        // Create site from location inputs
        var site = new Site(888, "ConformanceSite", earth,
            new Planetodetic(
                UnitConversion.DegToRad(inputs.Location.LongitudeDeg),
                UnitConversion.DegToRad(inputs.Location.LatitudeDeg),
                UnitConversion.KmToM(inputs.Location.HeightKm)));

        // Create kernel-backed spacecraft
        var windowStart = EpochParser.Parse(inputs.SearchWindow.Start);
        var windowEnd = EpochParser.Parse(inputs.SearchWindow.End);
        var searchWindow = new Window(windowStart, windowEnd);
        var spacecraft = new Spacecraft(bspOrbit.NaifId, "TrackingTarget", epoch: windowStart);

        var step = TimeSpan.FromSeconds(inputs.StepSizeS);

        // Compute tracking arcs
        var arcs = ComputeTrackingArcs(spacecraft, site, moon, searchWindow, step);

        // Collect observables
        var (rangeKm, rangeRateKmS) = CollectTrackingObservables(spacecraft, site, arcs, searchWindow, step);

        // The outputs of spec.md section 12.3: the arcs, and the statistics named <block>.<statistic>
        var result = new Dictionary<string, object>
        {
            ["arcs"] = arcs
        };
        AddStatistics(result, MetricTable.TrackingRangeBlock, rangeKm);
        AddStatistics(result, MetricTable.TrackingRangeRateBlock, rangeRateKmS);
        return result;
    }

    // The mean, the median sorted[n/2] counted from 0, the minimum and the maximum; none without a visible sample
    private static void AddStatistics(Dictionary<string, object> outputs, string block, List<double> samples)
    {
        if (samples.Count == 0)
        {
            return;
        }

        var sorted = samples.OrderBy(x => x).ToList();
        outputs[$"{block}.mean"] = samples.Average();
        outputs[$"{block}.median"] = sorted[sorted.Count / 2];
        outputs[$"{block}.min"] = sorted[0];
        outputs[$"{block}.max"] = sorted[^1];
    }

    /// <summary>
    /// Checks that the declared ellipsoid is that of the body's kernel radii, on which the library places the station.
    /// </summary>
    /// <exception cref="InvalidOperationException">An equatorial or polar radius differs by more than 1 µm.</exception>
    internal static void CheckEllipsoid(EllipsoidInputs ellipsoid, CelestialBody body)
    {
        if (ellipsoid == null)
        {
            return;
        }

        double equatorial = UnitConversion.KmToM(ellipsoid.EquatorialRadiusKm);
        double polar = equatorial * (1.0 - ellipsoid.Flattening);
        if (System.Math.Abs(equatorial - body.EquatorialRadius) > EllipsoidToleranceMetres ||
            System.Math.Abs(polar - body.PolarRadius) > EllipsoidToleranceMetres)
        {
            throw new InvalidOperationException(
                $"The station's ellipsoid ({equatorial:R} m, flattening {ellipsoid.Flattening:R}) is not that of the kernel radii of {body.Name} " +
                $"({body.EquatorialRadius:R} m, {body.PolarRadius:R} m), the only one on which the library places a site.");
        }
    }

    private static List<Window> ComputeTrackingArcs(
        Spacecraft spacecraft, Site site, CelestialBody occultingBody,
        Window searchWindow, TimeSpan step)
    {
        // Find windows when target is above the horizon (Z > 0 in site topocentric frame)
        var aboveHorizonWindows = spacecraft.FindWindowsOnCoordinateConstraint(
            searchWindow, site, site.Frame,
            CoordinateSystem.Rectangular, Coordinate.Z,
            RelationnalOperator.Greater, 0.0, 0.0,
            Aberration.None, step).ToList();

        // Find windows when target is occulted by the occulting body from site
        var occultations = spacecraft.FindWindowsOnOccultationConstraint(
            searchWindow, site,
            ShapeType.Point, occultingBody, ShapeType.Ellipsoid,
            OccultationType.Full, Aberration.None, step).ToList();

        // Subtract occultation windows from above-horizon windows
        var trackingArcs = new List<Window>();
        foreach (var visWindow in aboveHorizonWindows)
        {
            var remaining = new List<Window> { visWindow };
            foreach (var occWindow in occultations)
            {
                var next = new List<Window>();
                foreach (var r in remaining)
                {
                    if (occWindow.EndDate <= r.StartDate || occWindow.StartDate >= r.EndDate)
                    {
                        next.Add(r);
                    }
                    else
                    {
                        if (occWindow.StartDate > r.StartDate)
                            next.Add(new Window(r.StartDate, occWindow.StartDate));
                        if (occWindow.EndDate < r.EndDate)
                            next.Add(new Window(occWindow.EndDate, r.EndDate));
                    }
                }
                remaining = next;
            }
            trackingArcs.AddRange(remaining);
        }

        // Resample as half-open intervals [rise, fall) on the step grid
        var sampledArcs = new List<Window>();
        bool inArc = false;
        Time arcStart = default!;

        for (var epoch = searchWindow.StartDate; epoch < searchWindow.EndDate; epoch = epoch.Add(step))
        {
            bool isVisible = trackingArcs.Any(a => epoch >= a.StartDate && epoch < a.EndDate);

            if (isVisible && !inArc)
            {
                arcStart = epoch;
                inArc = true;
            }
            else if (!isVisible && inArc)
            {
                sampledArcs.Add(new Window(arcStart, epoch));
                inArc = false;
            }
        }

        if (inArc)
        {
            sampledArcs.Add(new Window(arcStart, searchWindow.EndDate));
        }

        return sampledArcs;
    }

    private static (List<double> RangeKm, List<double> RangeRateKmS) CollectTrackingObservables(
        Spacecraft spacecraft, Site site, List<Window> arcs,
        Window searchWindow, TimeSpan step)
    {
        var rangeKm = new List<double>();
        var rangeRateKmS = new List<double>();

        for (var epoch = searchWindow.StartDate; epoch < searchWindow.EndDate; epoch = epoch.Add(step))
        {
            if (!arcs.Any(x => epoch >= x.StartDate && epoch < x.EndDate))
            {
                continue;
            }

            var horizontal = site.GetHorizontalCoordinates(epoch, spacecraft, Aberration.None);
            rangeKm.Add(horizontal.Range / 1000.0);

            var sv = spacecraft.GetEphemeris(epoch, site, Frame.ICRF, Aberration.None).ToStateVector();
            double rangeRate = (sv.Position * sv.Velocity) / sv.Position.Magnitude();
            rangeRateKmS.Add(rangeRate / 1000.0);
        }

        return (rangeKm, rangeRateKmS);
    }
}
