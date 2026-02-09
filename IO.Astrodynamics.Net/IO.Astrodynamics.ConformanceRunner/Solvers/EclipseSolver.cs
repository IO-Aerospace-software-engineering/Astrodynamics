using System;
using System.Collections.Generic;
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

        // Resolve bodies
        var earth = BodyResolver.Resolve(inputs.OccultingBody);
        var sun = BodyResolver.Resolve(inputs.LightSource);

        // Build orbit
        var sv = BuildStateVector(inputs.Orbit, earth, epoch, frame);

        // Parse search window
        var windowStart = new Time(inputs.SearchWindow.Start);
        var windowEnd = new Time(inputs.SearchWindow.End);

        // Search for first eclipse
        var result = FindFirstEclipse(sv, earth, sun, windowStart, windowEnd, frame);

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

    private static EclipseResult? FindFirstEclipse(
        StateVector initialSv,
        CelestialBody occultingBody,
        CelestialBody lightSource,
        Time windowStart,
        Time windowEnd,
        Frame frame)
    {
        // Get the Keplerian elements for two-body propagation
        var kep = initialSv.ToKeplerianElements();

        // Coarse scan: step through at 10-second intervals
        var stepSeconds = 10.0;
        var totalSeconds = (windowEnd - windowStart).TotalSeconds;
        var steps = (int)(totalSeconds / stepSeconds) + 1;

        OccultationType prevType = OccultationType.None;
        Time? entryCandidate = null;
        OccultationType entryType = OccultationType.None;

        for (int i = 0; i <= steps; i++)
        {
            var t = windowStart + TimeSpan.FromSeconds(System.Math.Min(i * stepSeconds, totalSeconds));
            var currentType = ComputeOccultation(kep, t, occultingBody, lightSource, frame);

            if (prevType == OccultationType.None && currentType != OccultationType.None)
            {
                // Transition into eclipse — bisect to find entry
                var prevT = i > 0 ? windowStart + TimeSpan.FromSeconds((i - 1) * stepSeconds) : windowStart;
                var entryTime = BisectTransition(kep, prevT, t, occultingBody, lightSource, frame, searchForEntry: true);
                entryCandidate = entryTime;
                entryType = currentType;
            }
            else if (prevType != OccultationType.None && currentType == OccultationType.None && entryCandidate.HasValue)
            {
                // Transition out of eclipse — bisect to find exit
                var prevT = windowStart + TimeSpan.FromSeconds((i - 1) * stepSeconds);
                var exitTime = BisectTransition(kep, prevT, t, occultingBody, lightSource, frame, searchForEntry: false);

                // Determine the eclipse type at the midpoint
                var midTime = entryCandidate.Value + TimeSpan.FromSeconds((exitTime - entryCandidate.Value).TotalSeconds / 2.0);
                var midType = ComputeOccultation(kep, midTime, occultingBody, lightSource, frame);
                if (midType == OccultationType.None) midType = entryType;

                return new EclipseResult
                {
                    Entry = entryCandidate.Value,
                    Exit = exitTime,
                    Type = midType
                };
            }

            prevType = currentType;
        }

        // If we're still in eclipse at window end
        if (entryCandidate.HasValue && prevType != OccultationType.None)
        {
            var midTime = entryCandidate.Value + TimeSpan.FromSeconds((windowEnd - entryCandidate.Value).TotalSeconds / 2.0);
            var midType = ComputeOccultation(kep, midTime, occultingBody, lightSource, frame);
            if (midType == OccultationType.None) midType = entryType;

            return new EclipseResult
            {
                Entry = entryCandidate.Value,
                Exit = windowEnd,
                Type = midType
            };
        }

        return null;
    }

    private static Time BisectTransition(
        KeplerianElements kep,
        Time tBefore,
        Time tAfter,
        CelestialBody occultingBody,
        CelestialBody lightSource,
        Frame frame,
        bool searchForEntry)
    {
        // Binary search to ~1ms precision
        var lo = tBefore;
        var hi = tAfter;

        for (int iter = 0; iter < 50; iter++)
        {
            var midSeconds = (lo - tBefore).TotalSeconds + ((hi - lo).TotalSeconds / 2.0);
            var mid = tBefore + TimeSpan.FromSeconds((lo - tBefore).TotalSeconds + (hi - lo).TotalSeconds / 2.0);

            var midType = ComputeOccultation(kep, mid, occultingBody, lightSource, frame);
            bool isInEclipse = midType != OccultationType.None;

            if (searchForEntry)
            {
                // Looking for entry: eclipse starts somewhere in [lo, hi]
                // lo is NOT in eclipse, hi IS in eclipse
                if (isInEclipse)
                    hi = mid;
                else
                    lo = mid;
            }
            else
            {
                // Looking for exit: eclipse ends somewhere in [lo, hi]
                // lo IS in eclipse, hi is NOT in eclipse
                if (isInEclipse)
                    lo = mid;
                else
                    hi = mid;
            }

            if ((hi - lo).TotalSeconds < 0.001)
                break;
        }

        return searchForEntry ? hi : lo;
    }

    private static OccultationType ComputeOccultation(
        KeplerianElements kep,
        Time t,
        CelestialBody occultingBody,
        CelestialBody lightSource,
        Frame frame)
    {
        // Two-body propagation to time t
        var sv = kep.ToStateVector(t);

        // Use the instance method on CelestialItem
        var occultation = lightSource.IsOcculted(occultingBody, sv);

        return occultation ?? OccultationType.None;
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
