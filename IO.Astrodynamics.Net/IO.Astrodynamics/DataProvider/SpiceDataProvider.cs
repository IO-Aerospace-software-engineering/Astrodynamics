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
    /// <remarks>
    /// A frame named TEME is <see cref="Frame.TEME"/>, the managed <see cref="TemeFrame"/>, whatever its instance (#451,
    /// the routing of #562); its orientation is labelled with <paramref name="source"/>. Every other frame is SPICE's.
    /// </remarks>
    public StateOrientation FrameTransformationToICRF(in Time date, Frame source)
    {
        if (SpiceAPI.IsTeme(source))
        {
            var teme = Frame.TEME.GetStateOrientationToICRF(date);
            return new StateOrientation(teme.Rotation, teme.AngularVelocity, date, source);
        }

        return SpiceAPI.Instance.TransformFrame(date, source, Frame.ICRF);
    }

    public OrbitalParameters.OrbitalParameters GetEphemeris(in Time date, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration)
    {
        // Use ReadEphemerisRaw to avoid CelestialItem.Create() which can't handle
        // spacecraft/site NAIF IDs. The actual observer object is passed through directly.
        // A frame named GCRF is read from SPICE in J2000 and labelled with the caller's frame (SpiceAPI.SpiceFrameName, #543);
        // a managed frame (TEME, CIRS, TIRS, #558; the frame of a site built from coordinates or of a managed spacecraft with an
        // attitude record, #682) is read in J2000 and rotated by it (SpiceAPI.ManagedReadFrame)
        var result = SpiceAPI.Instance.ReadEphemerisRaw(date, observer, target.NaifId, frame, aberration);
        // ReadEphemeris always returns TDB epoch; preserve the caller's time frame.
        return new StateVector(result.Position, result.Velocity, observer, date, result.Frame);
    }

    CelestialBody IDataProvider.GetCelestialBodyInfo(int naifId)
    {
        return SpiceAPI.Instance.GetCelestialBodyInfo(naifId);
    }
}