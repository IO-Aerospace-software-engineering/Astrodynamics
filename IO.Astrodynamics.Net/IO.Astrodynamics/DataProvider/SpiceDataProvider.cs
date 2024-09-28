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
    public Task<IEnumerable<StateOrientation>> FrameTransformationAsync(Window window, Frame source, Frame target, TimeSpan stepSize)
    {
        return Task.Run(() => API.Instance.TransformFrame(window, source, target, stepSize));
    }

    public StateOrientation FrameTransformation(in Time date, Frame source, Frame target)
    {
        return API.Instance.TransformFrame(date, source, target);
    }

    public OrbitalParameters.OrbitalParameters GetEphemeris(in Time date, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration)
    {
        return API.Instance.ReadEphemeris(date, observer, target, frame, aberration);
    }

    public Task<IEnumerable<OrbitalParameters.OrbitalParameters>> GetEphemerisAsync(Window window, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration,
        TimeSpan stepSize)
    {
        return Task.Run(() => API.Instance.ReadEphemeris(window, observer, target, frame, aberration, stepSize));
    }

    public Task<DTO.CelestialBody> GetCelestialBodyInfoAsync(int naifId)
    {
        return Task.Run(() => API.Instance.GetCelestialBodyInfo(naifId));
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