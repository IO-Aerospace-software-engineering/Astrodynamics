using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

public class PropagatorSolver : ICategorySolver
{
    private readonly string _dataPath;

    public PropagatorSolver(string dataPath)
    {
        _dataPath = dataPath;
    }

    public string Category => "propagator";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParsePropagatorInputs(caseInput.Inputs);
        var frame = FrameMapper.Map(caseInput.Metadata.ReferenceFrame);
        var epoch = ParseTime(inputs.Epoch);

        // Create central body with geopotential model and optional atmospheric model
        GeopotentialModelParameters geoParams = null;
        if (inputs.GeopotentialDegree > 0 && !string.Equals(inputs.GeopotentialModel, "none", StringComparison.OrdinalIgnoreCase))
        {
            var geoModelPath = Path.Combine(_dataPath, inputs.GeopotentialModel);
            geoParams = new GeopotentialModelParameters(geoModelPath, (ushort)inputs.GeopotentialDegree);
        }
        IAtmosphericModel atmosphericModel = inputs.ForceModel.Drag ? new EarthStandardAtmosphere() : null;
        var centralBody = new CelestialBody(
            BodyResolver.ResolveNaif(inputs.CentralBody),
            frame, epoch, geoParams, atmosphericModel);

        // Build initial state vector
        var sv = BuildStateVector(inputs.Orbit, centralBody, epoch, frame);

        // Propagation window
        var windowStart = ParseTime(inputs.PropagationWindow.Start);
        var windowEnd = ParseTime(inputs.PropagationWindow.End);
        var propWindow = new Window(windowStart, windowEnd);

        // Build bodies list: central body + perturbation bodies
        var bodies = new List<CelestialItem> { centralBody };
        foreach (var bodyName in inputs.PerturbationBodies)
        {
            bodies.Add(BodyResolver.ResolveCelestialItem(bodyName));
        }

        // Create spacecraft
        var clock = new Clock("ConfClk", 256);
        double mass = inputs.Spacecraft?.MassKg ?? 100.0;
        double maxMass = inputs.Spacecraft?.MaxMassKg ?? mass * 10.0;
        double area = inputs.Spacecraft?.SectionalAreaM2 ?? 1.0;
        double cd = inputs.Spacecraft?.DragCoefficient ?? 2.2;
        double cr = inputs.Spacecraft?.RadiationPressureCoefficient ?? 1.0;
        var spacecraft = new Spacecraft(-999, "ConformanceTestSC", mass, maxMass, clock, sv,
            sectionalArea: area, dragCoeff: cd, solarRadiationCoeff: cr);

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-11, relativeTolerance: 1e-11,
            initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0);

        // Propagate
        var stepSize = TimeSpan.FromSeconds(inputs.StepSizeS);
        var propagator = new CentralBodyPropagator(
            propWindow, spacecraft, integrator, bodies,
            inputs.ForceModel.Drag, inputs.ForceModel.Srp, stepSize);

        var res=propagator.Propagate();

        // Interpolate at exact window end to avoid DeltaT sampling gap
        var (finalPos, finalVel) = res.InterpolateAt(windowEnd.ToTDB());

        // Return position (km) and velocity (km/s)
        return new Dictionary<string, object>
        {
            ["final_x_km"] = UnitConversion.MToKm(finalPos.X),
            ["final_y_km"] = UnitConversion.MToKm(finalPos.Y),
            ["final_z_km"] = UnitConversion.MToKm(finalPos.Z),
            ["final_vx_km_s"] = UnitConversion.MSToKmS(finalVel.X),
            ["final_vy_km_s"] = UnitConversion.MSToKmS(finalVel.Y),
            ["final_vz_km_s"] = UnitConversion.MSToKmS(finalVel.Z)
        };
    }

    private static Time ParseTime(string timeString)
    {
        if (timeString.EndsWith(" UTC"))
        {
            var dtStr = timeString[..^4];
            var dt = DateTime.Parse(dtStr, CultureInfo.InvariantCulture);
            return new Time(dt, TimeFrame.UTCFrame);
        }

        return new Time(timeString);
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

        if (orbit is TleOrbit tleOrbit)
        {
            var tle = new TLE(tleOrbit.Name, tleOrbit.Line1, tleOrbit.Line2);
            var osculatingIcrf = tle.ToStateVector().ToFrame(frame).ToStateVector();
            return new StateVector(osculatingIcrf.Position, osculatingIcrf.Velocity,
                observer, osculatingIcrf.Epoch, frame);
        }

        throw new ArgumentException("Unknown orbit type");
    }
}