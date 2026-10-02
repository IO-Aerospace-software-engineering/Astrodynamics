using System;
using System.Collections.Generic;
using System.Globalization;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

public class GeosynchronousSolver : ICategorySolver
{
    public string Category => "geosynchronous";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseGeosynchronousInputs(caseInput.Inputs);
        var frame = FrameMapper.Map(caseInput.Metadata.ReferenceFrame);
        var epoch = ParseTime(inputs.Epoch);

        var centralBody = new CelestialBody(
            BodyResolver.ResolveNaif(inputs.CentralBody), frame, epoch);

        var lonRad = UnitConversion.DegToRad(inputs.LongitudeDeg);
        var latRad = UnitConversion.DegToRad(inputs.LatitudeDeg);

        var orbit = centralBody.GeosynchronousOrbit(lonRad, latRad, epoch);
        var sv = orbit.ToStateVector();

        return new Dictionary<string, object>
        {
            ["semi_major_axis_km"] = UnitConversion.MToKm(orbit.SemiMajorAxis()),
            ["eccentricity"] = orbit.Eccentricity(),
            ["inclination_deg"] = UnitConversion.RadToDeg(orbit.Inclination()),
            ["raan_deg"] = UnitConversion.RadToDeg(orbit.AscendingNode()),
            ["argp_deg"] = UnitConversion.RadToDeg(orbit.ArgumentOfPeriapsis()),
            ["mean_anomaly_deg"] = UnitConversion.RadToDeg(orbit.MeanAnomaly()),
            ["position_x_km"] = UnitConversion.MToKm(sv.Position.X),
            ["position_y_km"] = UnitConversion.MToKm(sv.Position.Y),
            ["position_z_km"] = UnitConversion.MToKm(sv.Position.Z),
            ["velocity_x_km_s"] = UnitConversion.MSToKmS(sv.Velocity.X),
            ["velocity_y_km_s"] = UnitConversion.MSToKmS(sv.Velocity.Y),
            ["velocity_z_km_s"] = UnitConversion.MSToKmS(sv.Velocity.Z)
        };
    }

    private static Time ParseTime(string timeString)
    {
        if (timeString.EndsWith(" TDB"))
        {
            var dtStr = timeString[..^4];
            var dt = DateTime.Parse(dtStr, CultureInfo.InvariantCulture);
            return new Time(dt, TimeFrame.TDBFrame);
        }

        if (timeString.EndsWith(" UTC"))
        {
            var dtStr = timeString[..^4];
            var dt = DateTime.Parse(dtStr, CultureInfo.InvariantCulture);
            return new Time(dt, TimeFrame.UTCFrame);
        }

        return new Time(timeString);
    }
}
