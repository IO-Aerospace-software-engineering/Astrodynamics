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

        var earth = BodyResolver.Resolve("Earth");
        var sv = BuildStateVector(inputs.Orbit, earth, epoch, frame);

        var primaryTarget = BodyResolver.Resolve(inputs.PrimaryTarget);
        var secondaryTarget = BodyResolver.Resolve(inputs.SecondaryTarget);

        // Parse body vectors (instrument boresight and clock/refVector)
        var boresight = new Vector3(inputs.PrimaryBodyVector[0], inputs.PrimaryBodyVector[1], inputs.PrimaryBodyVector[2]);
        var refVector = new Vector3(inputs.SecondaryBodyVector[0], inputs.SecondaryBodyVector[1], inputs.SecondaryBodyVector[2]);

        // Create spacecraft with instrument, engine and fuel tank
        var spacecraft = new Spacecraft(-999, "ConformanceTestSC", 100.0, 1000.0,
            new Clock("ConfClk", 65536), sv);
        spacecraft.AddFuelTank(new FuelTank("ft", "ftA", "000000", 500.0, 400.0));
        spacecraft.AddEngine(new Engine("eng", "engA", "000000", 300, 50, spacecraft.FuelTanks.First()));

        // Create a circular instrument with the boresight and clock directions
        var fovRad = UnitConversion.DegToRad(inputs.FieldOfView.HalfAngleDeg);
        spacecraft.AddCircularInstrument(-99900, "ConfInst", "ConfModel",
            fovRad, boresight, refVector, Vector3.Zero);

        // Use the single-instrument TriadAttitude constructor:
        // boresight → primary target, clock/refVector → secondary target
        var triadAttitude = new TriadAttitude(
            new Time(DateTime.MinValue, TimeFrame.TDBFrame),
            TimeSpan.FromSeconds(10.0),
            spacecraft.Instruments.First(),
            primaryTarget,
            secondaryTarget,
            spacecraft.Engines.First());

        // Execute the framework's TRIAD algorithm
        var (_, stateOrientation) = triadAttitude.TryExecute(sv);
        var attitudeQuat = stateOrientation.Rotation;
        var quatArray = UnitConversion.QuaternionToArray(attitudeQuat);

        // FOV check: rotate instrument boresight by the attitude quaternion
        var boresightInertial = boresight.Rotate(attitudeQuat);

        // Compute primary target direction from ephemeris
        var primaryEph = primaryTarget.GetEphemeris(epoch, sv.Observer, sv.Frame, Aberration.LT);
        var primaryRefVec = (primaryEph.ToStateVector().Position - sv.Position).Normalize();

        var angle = boresightInertial.Angle(primaryRefVec);
        var targetInFov = angle <= fovRad;

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
