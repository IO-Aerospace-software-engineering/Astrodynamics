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

    public OrbitalParameters.OrbitalParameters GetEphemerisFromICRF(in Time date, ILocalizable target, Frame frame, Aberration aberration)
    {
        return API.Instance.ReadEphemeris(date, Barycenters.SOLAR_SYSTEM_BARYCENTER, target, frame, aberration);
    }

    CelestialBody IDataProvider.GetCelestialBodyInfo(int naifId)
    {
        return API.Instance.GetCelestialBodyInfo(naifId);
    }
}