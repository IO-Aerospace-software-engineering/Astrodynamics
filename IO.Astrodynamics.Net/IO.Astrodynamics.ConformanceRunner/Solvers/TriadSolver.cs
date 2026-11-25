using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>pointing_triad</c> category (spec.md section 12.4): the TRIAD attitude that points the instrument's boresight
/// (the primary body vector) at the primary target and keeps its reference vector (the secondary body vector) towards the
/// secondary target, with the directions of the targets LT-corrected from the Earth's centre minus the spacecraft's
/// geometric position (<see cref="EarthCentreLightTimeTarget"/>: the model of the case, which
/// <see cref="CelestialAttitudeTarget"/> no longer follows since #740, its light time the spacecraft's, 1.0e-6 rad from
/// the case's model for 001, the variant <c>lt_from_spacecraft</c> of its reference check); and whether the primary
/// direction lies within the instrument's field of view.
/// </summary>
public class TriadSolver : ICategorySolver
{
    public string Category => "pointing_triad";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseTriadInputs(caseInput.Inputs);
        var frame = FrameMapper.Map(caseInput.Metadata.ReferenceFrame);
        var epoch = EpochParser.Parse(inputs.Epoch);

        var earth = BodyResolver.Resolve("Earth");
        var sv = BuildStateVector(inputs.Orbit, earth, epoch, frame);

        var primaryTarget = new EarthCentreLightTimeTarget(BodyResolver.Resolve(inputs.PrimaryTarget));
        var secondaryTarget = new EarthCentreLightTimeTarget(BodyResolver.Resolve(inputs.SecondaryTarget));

        // Parse body vectors (instrument boresight and clock/refVector)
        var boresight = new Vector3(inputs.PrimaryBodyVector[0], inputs.PrimaryBodyVector[1], inputs.PrimaryBodyVector[2]);
        var refVector = new Vector3(inputs.SecondaryBodyVector[0], inputs.SecondaryBodyVector[1], inputs.SecondaryBodyVector[2]);

        // Create spacecraft with instrument (an attitude takes no engine since lot L5b, decisions 48 and 93 of #668)
        var spacecraft = new Spacecraft(-999, "ConformanceTestSC", 100.0, sv,
            1000.0, new Clock("ConfClk", 65536));

        // Create a circular instrument with the boresight and clock directions
        var fovRad = UnitConversion.DegToRad(inputs.FieldOfView.HalfAngleDeg);
        spacecraft.AddCircularInstrument("ConfInst",
            fovRad, boresight, refVector, Vector3.Zero, naifId: -99900, model: "ConfModel");
        var instrument = spacecraft.Instruments.First();

        // The TRIAD of the IAttitudeTarget constructor: the instrument's boresight towards the primary target, its
        // reference vector towards the secondary one
        var triadAttitude = new TriadAttitude(instrument.GetBoresightInSpacecraftFrame(),
            primaryTarget,
            instrument.GetRefVectorInSpacecraftFrame(),
            secondaryTarget,
            new Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.FromSeconds(10.0),
            earth);

        // The framework's TRIAD algorithm, by the public preview of the attitude (decision 50 of #668: the rotation of the
        // execution at this state, the attitude's windows left unset)
        var stateOrientation = triadAttitude.ComputeStateOrientation(sv);
        var attitudeQuat = stateOrientation.Rotation;
        var quatArray = UnitConversion.QuaternionToArray(attitudeQuat);

        // The field of view: the primary direction within the half angle of the axis of the field, rotated to the frame
        var axis = new Vector3(inputs.FieldOfView.AxisBody[0], inputs.FieldOfView.AxisBody[1], inputs.FieldOfView.AxisBody[2]);
        var targetInFov = axis.Rotate(attitudeQuat).Angle(primaryTarget.GetDirection(sv)) <= fovRad;

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

    /// <summary>
    /// The direction of a target in the model of spec.md section 12.4: its position LT-corrected from the centre of the
    /// state (the Earth; SPICE <c>spkpos</c>, abcorr <c>LT</c>) minus the spacecraft's geometric position, normalized,
    /// as <see cref="CelestialAttitudeTarget"/> gave it before #740.
    /// </summary>
    internal sealed class EarthCentreLightTimeTarget(ILocalizable target) : IAttitudeTarget
    {
        public string Name => (target as INaifObject)?.Name ?? "CelestialTarget";

        public Vector3 GetDirection(StateVector observerState)
        {
            ArgumentNullException.ThrowIfNull(observerState);
            var ephemeris = target.GetEphemeris(observerState.Epoch, observerState.Observer, observerState.Frame, Aberration.LT);
            return (ephemeris.ToStateVector().Position - observerState.Position).Normalize();
        }
    }
}
