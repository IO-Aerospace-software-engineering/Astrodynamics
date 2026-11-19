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
    /// A frame named TEME, CIRS or TIRS, in any case, is <see cref="Frame.TEME"/>, <see cref="Frame.CIRS"/> or
    /// <see cref="Frame.TIRS"/>, the managed frames, whatever its instance (#451, the routing of #562, #656: before, CIRS and
    /// TIRS built by name reached SPICE, which threw); an instance of <see cref="TemeFrame"/> or <see cref="TirsFrame"/>
    /// keeps its own Earth orientation parameters. Its orientation is labelled with <paramref name="source"/>. Every other
    /// frame is SPICE's.
    /// </remarks>
    /// <exception cref="ArgumentException">SPICE knows no frame of the name of <paramref name="source"/>
    /// (<see cref="ArgumentException.ParamName"/> <c>source</c>): the message names the frame, SPICE's error its inner
    /// exception (R25 of #668, decided by S. Guillet, 2026-11-19).</exception>
    /// <exception cref="InvalidOperationException">SPICE cannot orient the frame at <paramref name="date"/>.</exception>
    public StateOrientation FrameTransformationToICRF(in Time date, Frame source)
    {
        if (SpiceAPI.ManagedOrientationToIcrf(date, source) is { } managed)
        {
            return managed;
        }

        // An unknown name names the parameter of this call, source (R25 of #668)
        return SpiceAPI.Instance.TransformFrame(date, source, Frame.ICRF, nameof(source), null);
    }

    public OrbitalParameters.OrbitalParameters GetEphemeris(in Time date, ILocalizable target, ILocalizable observer, Frame frame, Aberration aberration)
    {
        // Use ReadEphemerisRaw to avoid CelestialItem.Create() which can't handle
        // spacecraft/site NAIF IDs. The actual observer object is passed through directly.
        // A frame named GCRF is read from SPICE in J2000 and labelled with the caller's frame (SpiceAPI.SpiceFrameName, #543);
        // a managed frame (TEME, CIRS, TIRS, #558; the frame of a site built from coordinates or of a managed spacecraft with an
        // attitude record, #682; the frame of a site of the kernels whose frame id no loaded kernel defines, third review of
        // #690) is read in J2000 and rotated by it (SpiceAPI.ManagedReadFrame)
        var result = SpiceAPI.Instance.ReadEphemerisRaw(date, observer, target.NaifId, frame, aberration);
        // ReadEphemeris always returns TDB epoch; preserve the caller's time frame.
        return new StateVector(result.Position, result.Velocity, observer, date, result.Frame);
    }

    CelestialBody IDataProvider.GetCelestialBodyInfo(int naifId)
    {
        return SpiceAPI.Instance.GetCelestialBodyInfo(naifId);
    }
}