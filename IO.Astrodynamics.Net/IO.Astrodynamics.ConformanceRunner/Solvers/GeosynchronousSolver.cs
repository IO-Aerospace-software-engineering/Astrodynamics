using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>geosynchronous</c> category (spec.md section 12.5): the geosynchronous orbit above a longitude and a latitude
/// of the body frame at the epoch, by <see cref="CelestialBody.GeosynchronousOrbit(double, double, Time, TimeSpan)"/>
/// with the case's sidereal period, and its state.
/// </summary>
/// <remarks>
/// The case computes with the files of the suite's <c>data/</c> it lists (<see cref="CaseKernels"/>), the Earth's
/// orientation file among them. The library takes the GM of the kernels: the case's GM must be that double, or the case
/// is an ERROR. A case without a period, as before conformance-tests#25, takes the body's sidereal rotation period at the
/// epoch (<see cref="CelestialBody.GeosynchronousOrbit(double, double, Time)"/>).
/// </remarks>
public class GeosynchronousSolver : ICategorySolver
{
    // m³ per km³
    private const double CubicMetresPerCubicKilometre = 1e9;

    private readonly string _conformanceTestsPath;

    public GeosynchronousSolver(string conformanceTestsPath)
    {
        _conformanceTestsPath = conformanceTestsPath;
    }

    public string Category => "geosynchronous";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseGeosynchronousInputs(caseInput.Inputs);
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));
        var frame = FrameMapper.Map(caseInput.Metadata.ReferenceFrame);
        var epoch = EpochParser.Parse(inputs.Epoch);

        var centralBody = new CelestialBody(
            BodyResolver.ResolveNaif(inputs.CentralBody), frame, epoch);
        if (inputs.GmKm3S2.HasValue && inputs.GmKm3S2.Value * CubicMetresPerCubicKilometre != centralBody.GM)
        {
            throw new InvalidOperationException(
                $"The gravitational parameter of the case, {inputs.GmKm3S2.Value * CubicMetresPerCubicKilometre:R} m3/s2, is not that of the kernels, {centralBody.GM:R} m3/s2.");
        }

        var lonRad = UnitConversion.DegToRad(inputs.LongitudeDeg);
        var latRad = UnitConversion.DegToRad(inputs.LatitudeDeg);

        var orbit = inputs.SiderealPeriodS.HasValue
            ? centralBody.GeosynchronousOrbit(lonRad, latRad, epoch, TimeSpan.FromSeconds(inputs.SiderealPeriodS.Value))
            : centralBody.GeosynchronousOrbit(lonRad, latRad, epoch);
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
}
