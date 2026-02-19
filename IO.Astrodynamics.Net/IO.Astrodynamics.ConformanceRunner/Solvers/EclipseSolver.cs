using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
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

        var occultingBody = BodyResolver.Resolve(inputs.OccultingBody);
        var lightSource = BodyResolver.Resolve(inputs.LightSource);

        var sv = BuildStateVector(inputs.Orbit, occultingBody, epoch, frame);

        var windowStart = new Time(inputs.SearchWindow.Start);
        var windowEnd = new Time(inputs.SearchWindow.End);
        var searchWindow = new Window(windowStart, windowEnd);

        // Create spacecraft and propagate over the search window
        var spacecraft = new Spacecraft(-999, "ConformanceTestSC", 100.0, 1000.0,
            new Clock("ConfClk", 65536), sv);

        spacecraft.Propagate(searchWindow,
            [
                occultingBody, lightSource, PlanetsAndMoons.MOON_BODY, Barycenters.JUPITER_BARYCENTER, Barycenters.MARS_BARYCENTER, Barycenters.MERCURY_BARYCENTER,
                Barycenters.SATURN_BARYCENTER, Barycenters.URANUS_BARYCENTER, Barycenters.VENUS_BARYCENTER
            ],
            false, false, TimeSpan.FromSeconds(1.0));

        var stepSize = TimeSpan.FromSeconds(60.0);

        // Search for ANY occultation (penumbra boundary) using the framework's method
        var penumbraWindows = lightSource.FindWindowsOnOccultationConstraint(
            searchWindow, spacecraft,
            ShapeType.Ellipsoid, occultingBody, ShapeType.Ellipsoid,
            OccultationType.Any, Aberration.LT, stepSize).ToArray();

        if (penumbraWindows.Length == 0)
        {
            return NullResult();
        }

        var penumbra = penumbraWindows[0];

        // Search for FULL occultation (umbra boundary) within the penumbra window
        var umbraWindows = lightSource.FindWindowsOnOccultationConstraint(
            penumbra, spacecraft,
            ShapeType.Ellipsoid, occultingBody, ShapeType.Ellipsoid,
            OccultationType.Full, Aberration.LT, stepSize).ToArray();

        if (umbraWindows.Length == 0)
        {
            return new Dictionary<string, object>
            {
                ["penumbra_entry"] = penumbra.StartDate.ToUTC().ToString(),
                ["umbra_entry"] = (object)null,
                ["umbra_exit"] = (object)null,
                ["penumbra_exit"] = penumbra.EndDate.ToUTC().ToString(),
                ["penumbra_duration_s"] = penumbra.Length.TotalSeconds,
                ["umbra_duration_s"] = (object)null
            };
        }

        var umbra = umbraWindows[0];

        return new Dictionary<string, object>
        {
            ["penumbra_entry"] = penumbra.StartDate.ToUTC().ToString(),
            ["umbra_entry"] = umbra.StartDate.ToUTC().ToString(),
            ["umbra_exit"] = umbra.EndDate.ToUTC().ToString(),
            ["penumbra_exit"] = penumbra.EndDate.ToUTC().ToString(),
            ["penumbra_duration_s"] = penumbra.Length.TotalSeconds - umbra.Length.TotalSeconds,
            ["umbra_duration_s"] = umbra.Length.TotalSeconds
        };
    }

    private static Dictionary<string, object> NullResult()
    {
        return new Dictionary<string, object>
        {
            ["penumbra_entry"] = (object)null,
            ["umbra_entry"] = (object)null,
            ["umbra_exit"] = (object)null,
            ["penumbra_exit"] = (object)null,
            ["penumbra_duration_s"] = (object)null,
            ["umbra_duration_s"] = (object)null
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
}