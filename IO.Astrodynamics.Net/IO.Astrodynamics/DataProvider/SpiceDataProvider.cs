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
        return SpiceAPI.Instance.TransformFrame(date, source, Frame.ICRF);
    }

    public OrbitalParameters.OrbitalParameters GetEphemeris(in Time date, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration)
    {
        // Use ReadEphemerisRaw to avoid CelestialItem.Create() which can't handle
        // spacecraft/site NAIF IDs. The actual observer object is passed through directly.
        var result = SpiceAPI.Instance.ReadEphemerisRaw(date, observer, target.NaifId, frame, aberration);
        // ReadEphemeris always returns TDB epoch; preserve the caller's time frame.
        return new StateVector(result.Position, result.Velocity, observer, date, result.Frame);
    }

    CelestialBody IDataProvider.GetCelestialBodyInfo(int naifId)
    {
        return SpiceAPI.Instance.GetCelestialBodyInfo(naifId);
    }
}