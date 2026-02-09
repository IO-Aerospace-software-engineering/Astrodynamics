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

        // Resolve target bodies
        var primaryTarget = BodyResolver.Resolve(inputs.PrimaryTarget);
        var secondaryTarget = BodyResolver.Resolve(inputs.SecondaryTarget);

        // Compute inertial reference vectors from ephemeris
        var primaryEph = primaryTarget.GetEphemeris(epoch, sv.Observer, sv.Frame, Aberration.LT);
        var secondaryEph = secondaryTarget.GetEphemeris(epoch, sv.Observer, sv.Frame, Aberration.LT);

        var primaryRefVec = (primaryEph.ToStateVector().Position - sv.Position).Normalize();
        var secondaryRefVec = (secondaryEph.ToStateVector().Position - sv.Position).Normalize();

        // Parse body vectors
        var primaryBodyVec = new Vector3(inputs.PrimaryBodyVector[0], inputs.PrimaryBodyVector[1], inputs.PrimaryBodyVector[2]);
        var secondaryBodyVec = new Vector3(inputs.SecondaryBodyVector[0], inputs.SecondaryBodyVector[1], inputs.SecondaryBodyVector[2]);

        // TRIAD algorithm (same as TriadAttitude.ComputeOrientation)
        // Reference frame triad
        var t1R = primaryRefVec;
        var t2R = t1R.Cross(secondaryRefVec).Normalize();
        var t3R = t1R.Cross(t2R);

        // Body frame triad
        var t1B = primaryBodyVec.Normalize();
        var t2B = t1B.Cross(secondaryBodyVec).Normalize();
        var t3B = t1B.Cross(t2B);

        // Attitude: body→inertial
        var mRef = Matrix.FromColumnVectors(t1R, t2R, t3R);
        var mBody = Matrix.FromColumnVectors(t1B, t2B, t3B);
        var attitude = mRef.Multiply(mBody.Transpose());
        var attitudeQuat = attitude.ToQuaternion();

        // Convert to conformance format (scalar-last, canonicalized)
        var quatArray = UnitConversion.QuaternionToArray(attitudeQuat);

        // FOV check: uses primary target direction (already computed)
        var axisBody = new Vector3(inputs.FieldOfView.AxisBody[0], inputs.FieldOfView.AxisBody[1], inputs.FieldOfView.AxisBody[2]);
        var boresightInertial = axisBody.Rotate(attitudeQuat);
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
