using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>frame_transformation</c> category (spec.md section 11.3): a state given in a frame of spec.md section 2.1
/// about the Earth or the Moon, translated to another centre (geometric, without light time) and rotated to another
/// frame, r′ = R r and v′ = R v + Ṙ r; with an ellipsoid, the geodetic coordinates of the position.
/// </summary>
/// <remarks>
/// The case computes with the kernels it lists, loaded from the suite's <c>data/</c> for the case alone
/// (<see cref="CaseKernels"/>).
/// </remarks>
public class FrameTransformationSolver : ICategorySolver
{
    private readonly string _conformanceTestsPath;

    public FrameTransformationSolver(string conformanceTestsPath)
    {
        _conformanceTestsPath = conformanceTestsPath;
    }

    public string Category => "frame_transformation";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseFrameTransformationInputs(caseInput.Inputs);
        var epoch = EpochParser.Parse(inputs.Epoch);
        var fromFrame = FrameCatalog.Resolve(inputs.FromFrame);
        var toFrame = FrameCatalog.Resolve(inputs.ToFrame);

        using var kernels = new CaseKernels(_conformanceTestsPath, inputs.Kernels);
        var state = new StateVector(
            new Vector3(UnitConversion.KmToM(inputs.PositionKm[0]), UnitConversion.KmToM(inputs.PositionKm[1]),
                UnitConversion.KmToM(inputs.PositionKm[2])),
            new Vector3(UnitConversion.KmSToMS(inputs.VelocityKmS[0]), UnitConversion.KmSToMS(inputs.VelocityKmS[1]),
                UnitConversion.KmSToMS(inputs.VelocityKmS[2])),
            Center(inputs.FromCenter, epoch), epoch, fromFrame);
        if (inputs.ToCenter != inputs.FromCenter)
        {
            state = state.RelativeTo(Center(inputs.ToCenter, epoch), Aberration.None).ToStateVector();
        }

        var result = state.ToFrame(toFrame).ToStateVector();
        var outputs = new Dictionary<string, object>
        {
            ["position_km"] = new[]
            {
                UnitConversion.MToKm(result.Position.X), UnitConversion.MToKm(result.Position.Y), UnitConversion.MToKm(result.Position.Z)
            },
            ["velocity_km_s"] = new[]
            {
                UnitConversion.MSToKmS(result.Velocity.X), UnitConversion.MSToKmS(result.Velocity.Y), UnitConversion.MSToKmS(result.Velocity.Z)
            }
        };

        if (inputs.Ellipsoid != null)
        {
            var position = result.Position;
            var geodetic = new Planetocentric(System.Math.Atan2(position.Y, position.X),
                    System.Math.Asin(position.Z / position.Magnitude()), position.Magnitude())
                .ToPlanetodetic(inputs.Ellipsoid.Flattening, UnitConversion.KmToM(inputs.Ellipsoid.EquatorialRadiusKm));
            outputs["latitude_deg"] = UnitConversion.RadToDeg(geodetic.Latitude);
            outputs["longitude_deg"] = UnitConversion.RadToDeg(geodetic.Longitude);
            outputs["altitude_km"] = UnitConversion.MToKm(geodetic.Altitude);
        }

        return outputs;
    }

    private static CelestialBody Center(string name, Time epoch)
    {
        return name switch
        {
            "Earth" => new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch),
            "Moon" => new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, epoch),
            _ => throw new ArgumentException($"The centre '{name}' is not one of the frame_transformation category (Earth, Moon).")
        };
    }
}
