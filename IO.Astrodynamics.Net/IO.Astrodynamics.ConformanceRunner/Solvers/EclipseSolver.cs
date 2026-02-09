using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

public class EclipseSolver : ICategorySolver
{
    public string Category => "eclipse";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseEclipseInputs(caseInput.Inputs);
        var frame = FrameMapper.Map(caseInput.Metadata.ReferenceFrame);
        var epoch = new Time(inputs.Epoch);

        // Resolve bodies using the framework's CelestialBody
        var earth = BodyResolver.Resolve(inputs.OccultingBody);
        var sun = BodyResolver.Resolve(inputs.LightSource);

        // Build orbit from inputs and convert to Keplerian elements (for two-body propagation)
        var sv = BuildStateVector(inputs.Orbit, earth, epoch, frame);
        var kep = sv.ToKeplerianElements();

        // Parse search window
        var windowStart = new Time(inputs.SearchWindow.Start);
        var windowEnd = new Time(inputs.SearchWindow.End);

        // Use the framework's FindWindowsOnOccultationConstraint on the Sun,
        // searching for when it is occulted by Earth as seen from the spacecraft orbit.
        // First, find Any occultation windows, then determine the specific type.
        var searchWindow = new Window(windowStart, windowEnd);

        // Use GeometryFinder-based window search via CelestialItem.FindWindowsOnOccultationConstraint
        // The observer needs to be a localizable object — use the spacecraft orbit position.
        // Since FindWindowsOnOccultationConstraint uses a zero-position StateVector relative to
        // the observer, we need to use the framework's IsOcculted instance method instead,
        // which properly checks occultation from an arbitrary orbital position.
        var result = FindFirstEclipseUsingFramework(kep, earth, sun, searchWindow);

        if (result == null)
        {
            return new Dictionary<string, object>
            {
                ["eclipse_entry"] = (object)null,
                ["eclipse_exit"] = (object)null,
                ["eclipse_duration_s"] = (object)null,
                ["eclipse_type"] = (object)null
            };
        }

        return new Dictionary<string, object>
        {
            ["eclipse_entry"] = result.Value.Entry.ToString(),
            ["eclipse_exit"] = result.Value.Exit.ToString(),
            ["eclipse_duration_s"] = (result.Value.Exit - result.Value.Entry).TotalSeconds,
            ["eclipse_type"] = MapEclipseType(result.Value.Type)
        };
    }

    /// <summary>
    /// Uses the framework's CelestialItem.IsOcculted instance method and GeometryFinder
    /// to find the first eclipse window from the spacecraft's orbital position.
    /// </summary>
    private static EclipseResult? FindFirstEclipseUsingFramework(
        KeplerianElements kep,
        CelestialBody occultingBody,
        CelestialBody lightSource,
        Window searchWindow)
    {
        // Use GeometryFinder to search for occultation windows.
        // The framework's GeometryFinder.FindWindowsWithCondition does coarse+bisect search.
        var geometryFinder = new GeometryFinder();

        // Define the occultation check using the framework's IsOcculted instance method
        Func<Time, bool> isOcculted = date =>
        {
            var sv = kep.ToStateVector(date);
            var occType = lightSource.IsOcculted(occultingBody, sv, Aberration.LT);
            return occType is OccultationType.Full or OccultationType.Partial or OccultationType.Annular;
        };

        // Use framework's GeometryFinder with 60s step size for coarse scan
        var windows = geometryFinder.FindWindowsWithCondition(
            searchWindow,
            isOcculted,
            RelationnalOperator.Equal,
            true,
            TimeSpan.FromSeconds(60.0));

        var windowList = windows.ToArray();
        if (windowList.Length == 0)
            return null;

        var firstWindow = windowList[0];

        // Determine eclipse type at midpoint using the framework's IsOcculted
        var midTime = firstWindow.StartDate + TimeSpan.FromSeconds(firstWindow.Length.TotalSeconds / 2.0);
        var midSv = kep.ToStateVector(midTime);
        var eclipseType = lightSource.IsOcculted(occultingBody, midSv, Aberration.LT) ?? OccultationType.None;

        return new EclipseResult
        {
            Entry = firstWindow.StartDate,
            Exit = firstWindow.EndDate,
            Type = eclipseType
        };
    }

    private static string MapEclipseType(OccultationType type)
    {
        return type switch
        {
            OccultationType.Full => "umbra",
            OccultationType.Partial => "penumbra",
            OccultationType.Annular => "annular",
            _ => "none"
        };
    }

    private static StateVector BuildStateVector(object orbit, CelestialBody observer, Time epoch, Frame frame)
    {
        if (orbit is KeplerianOrbit kep)
        {
            var ke = new KeplerianElements(
                UnitConversion.KmToM(kep.AKm),
                kep.E,
                UnitConversion.DegToRad(kep.IDeg),
                UnitConversion.DegToRad(kep.RaanDeg),
                UnitConversion.DegToRad(kep.ArgpDeg),
                UnitConversion.DegToRad(kep.MaDeg),
                observer,
                epoch,
                frame);
            return ke.ToStateVector();
        }

        if (orbit is StateVectorOrbit svo)
        {
            return new StateVector(
                new Vector3(UnitConversion.KmToM(svo.PositionKm[0]), UnitConversion.KmToM(svo.PositionKm[1]), UnitConversion.KmToM(svo.PositionKm[2])),
                new Vector3(UnitConversion.KmSToMS(svo.VelocityKmS[0]), UnitConversion.KmSToMS(svo.VelocityKmS[1]), UnitConversion.KmSToMS(svo.VelocityKmS[2])),
                observer,
                epoch,
                frame);
        }

        throw new ArgumentException("Unknown orbit type");
    }

    private struct EclipseResult
    {
        public Time Entry;
        public Time Exit;
        public OccultationType Type;
    }
}
