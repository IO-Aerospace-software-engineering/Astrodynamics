using System;
using IO.Astrodynamics.Frames;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

public static class FrameMapper
{
    public static Frame Map(string frameName)
    {
        return frameName?.ToUpperInvariant() switch
        {
            "EME2000" or "J2000" or "ICRF" => Frame.ICRF,
            "TEME" => Frame.TEME,
            "ECLIPJ2000" or "ECLIPTIC_J2000" => Frame.ECLIPTIC_J2000,
            _ => throw new ArgumentException($"Unknown reference frame: {frameName}")
        };
    }
}
