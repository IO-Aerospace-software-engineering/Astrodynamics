using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

public class TrackingDataSolver : ICategorySolver
{
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

        // Load the BSP kernel (path is relative to conformance-tests root)
        var bspPath = Path.Combine(_conformanceTestsPath, bspOrbit.Filename);
        SpiceAPI.Instance.LoadKernels(new FileInfo(bspPath));

        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);

        // Create site from location inputs
        var site = new Site(888, "ConformanceSite", earth,
            new Planetodetic(
                UnitConversion.DegToRad(inputs.Location.LongitudeDeg),
                UnitConversion.DegToRad(inputs.Location.LatitudeDeg),
                UnitConversion.KmToM(inputs.Location.HeightKm)));

        // Create kernel-backed spacecraft
        var windowStart = ParseTime(inputs.SearchWindow.Start);
        var windowEnd = ParseTime(inputs.SearchWindow.End);
        var searchWindow = new Window(windowStart, windowEnd);
        var spacecraft = new Spacecraft(bspOrbit.NaifId, "TrackingTarget", epoch: windowStart);

        var step = TimeSpan.FromSeconds(inputs.StepSizeS);

        // Compute tracking arcs
        var arcs = ComputeTrackingArcs(spacecraft, site, moon, searchWindow, step);

        // Collect observables
        var (rangeKm, rangeRateKmS) = CollectTrackingObservables(spacecraft, site, arcs, searchWindow, step);

        // Build flat output dictionary
        var result = new Dictionary<string, object>
        {
            ["arc_count"] = arcs.Count
        };

        for (int i = 0; i < arcs.Count; i++)
        {
            result[$"arc_{i}_rise"] = arcs[i].StartDate.ToUTC().ToString();
            result[$"arc_{i}_fall"] = arcs[i].EndDate.ToUTC().ToString();
        }

        if (rangeKm.Count > 0)
        {
            var sortedRange = rangeKm.OrderBy(x => x).ToList();
            result["range_mean_km"] = rangeKm.Average();
            result["range_median_km"] = sortedRange[sortedRange.Count / 2];
            result["range_min_km"] = sortedRange[0];
            result["range_max_km"] = sortedRange[^1];

            var sortedDoppler = rangeRateKmS.OrderBy(x => x).ToList();
            result["doppler_mean_km_s"] = rangeRateKmS.Average();
            result["doppler_median_km_s"] = sortedDoppler[sortedDoppler.Count / 2];
            result["doppler_min_km_s"] = sortedDoppler[0];
            result["doppler_max_km_s"] = sortedDoppler[^1];
        }

        return result;
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

    /// <summary>
    /// Parses a time string, handling "UTC" suffix that the Time(string) constructor doesn't support.
    /// </summary>
    private static Time ParseTime(string timeString)
    {
        if (timeString.EndsWith(" UTC", StringComparison.OrdinalIgnoreCase))
        {
            var dateStr = timeString[..^4];
            var dt = DateTime.Parse(dateStr, CultureInfo.InvariantCulture);
            return new Time(dt, TimeFrame.UTCFrame);
        }

        return new Time(timeString);
    }
}
