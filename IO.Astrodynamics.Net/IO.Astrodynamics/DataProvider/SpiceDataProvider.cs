using System.Collections.Generic;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.DataProvider;

public class SpiceDataProvider : IDataProvider
{
    public StateOrientation FrameTransformation(Frame source, Frame target, in Time date)
    {
        return API.Instance.TransformFrame(source, target, date);
    }

    public OrbitalParameters.OrbitalParameters GetEphemeris(in Time epoch, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration)
    {
        return API.Instance.ReadEphemeris(epoch, observer, target, frame, aberration);
    }

    public DTO.CelestialBody GetCelestialBodyInfo(int naifId)
    {
        return API.Instance.GetCelestialBodyInfo(naifId);
    }

    public void WriteEphemeris(FileInfo outputFile, INaifObject objectId, IEnumerable<StateVector> stateVectors)
    {
        API.Instance.WriteEphemeris(outputFile, objectId, stateVectors);
    }

    public void WriteOrientation(FileInfo outputFile, INaifObject objectId, IEnumerable<StateOrientation> stateOrientations)
    {
        API.Instance.WriteOrientation(outputFile, objectId, stateOrientations);
    }
}