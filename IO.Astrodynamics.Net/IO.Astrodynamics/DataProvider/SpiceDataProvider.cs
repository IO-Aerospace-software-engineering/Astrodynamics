using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using CelestialBody = IO.Astrodynamics.DTO.CelestialBody;

namespace IO.Astrodynamics.DataProvider;

public class SpiceDataProvider : IDataProvider
{
    public StateOrientation FrameTransformationToICRF(in Time date, Frame source)
    {
        return API.Instance.TransformFrame(date, source, Frame.ICRF);
    }

    public OrbitalParameters.OrbitalParameters GetEphemeris(in Time date, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration)
    {
        return API.Instance.ReadEphemeris(date, observer, target, frame, aberration);
    }

    CelestialBody IDataProvider.GetCelestialBodyInfo(int naifId)
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