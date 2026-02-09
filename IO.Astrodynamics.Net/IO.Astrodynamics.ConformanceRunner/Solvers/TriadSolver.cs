using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

public class TriadSolver : ICategorySolver
{
    public string Category => "pointing_triad";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseTriadInputs(caseInput.Inputs);
        var frame = FrameMapper.Map(caseInput.Metadata.ReferenceFrame);
        var epoch = new Time(inputs.Epoch);

        // Resolve observer (Earth)
        var earth = BodyResolver.Resolve("Earth");

        // Build spacecraft state vector from orbit
        var sv = BuildStateVector(inputs.Orbit, earth, epoch, frame);

        // Resolve target bodies using the framework
        var primaryTarget = BodyResolver.Resolve(inputs.PrimaryTarget);
        var secondaryTarget = BodyResolver.Resolve(inputs.SecondaryTarget);

        // Parse body vectors
        var primaryBodyVec = new Vector3(inputs.PrimaryBodyVector[0], inputs.PrimaryBodyVector[1], inputs.PrimaryBodyVector[2]);
        var secondaryBodyVec = new Vector3(inputs.SecondaryBodyVector[0], inputs.SecondaryBodyVector[1], inputs.SecondaryBodyVector[2]);

        // Create a minimal Spacecraft with engine/fuel tank (required by TriadAttitude)
        var spacecraft = new Spacecraft(-999, "ConformanceTestSC", 100.0, 1000.0,
            new Clock("ConfClk", 65536), sv);
        spacecraft.AddFuelTank(new FuelTank("ft", "ftA", "000000", 500.0, 400.0));
        spacecraft.AddEngine(new Engine("eng", "engA", "000000", 300, 50, spacecraft.FuelTanks.First()));

        // Use the framework's TriadAttitude with explicit body vectors constructor
        var triadAttitude = new TriadAttitude(
            new Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            primaryBodyVec,
            primaryTarget,
            secondaryBodyVec,
            secondaryTarget,
            spacecraft.Engines.First());

        // Execute the framework's TRIAD algorithm via TryExecute
        var (_, stateOrientation) = triadAttitude.TryExecute(sv);
        var attitudeQuat = stateOrientation.Rotation;

        // Convert to conformance format (scalar-first, canonicalized)
        var quatArray = UnitConversion.QuaternionToArray(attitudeQuat);

        // FOV check: rotate boresight axis by the framework-computed attitude quaternion
        var axisBody = new Vector3(inputs.FieldOfView.AxisBody[0], inputs.FieldOfView.AxisBody[1], inputs.FieldOfView.AxisBody[2]);
        var boresightInertial = axisBody.Rotate(attitudeQuat);

        // Compute primary target direction using the framework's ephemeris
        var primaryEph = primaryTarget.GetEphemeris(epoch, sv.Observer, sv.Frame, Aberration.LT);
        var primaryRefVec = (primaryEph.ToStateVector().Position - sv.Position).Normalize();

        var angle = boresightInertial.Angle(primaryRefVec);
        var halfAngleRad = UnitConversion.DegToRad(inputs.FieldOfView.HalfAngleDeg);
        var targetInFov = angle <= halfAngleRad;

        return new Dictionary<string, object>
        {
            ["attitude_quaternion"] = quatArray,
            ["target_in_fov"] = targetInFov
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
