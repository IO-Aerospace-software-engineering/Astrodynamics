using System;
using System.Collections.Generic;
using IO.Astrodynamics.Frames;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

/// <summary>
/// The frames of the conversion categories (spec.md section 2.1), by their exact names. Every other name is rejected,
/// EME2000 and TIRS among them: GMAT's EarthMJ2000Eq is not EME2000, and TIRS is not a frame of lot 1.
/// </summary>
/// <remarks>
/// The legacy categories keep <see cref="FrameMapper"/>, which maps EME2000 to ICRF.
/// </remarks>
public static class FrameCatalog
{
    /// <summary>The seven names of spec.md section 2.1.</summary>
    public static IReadOnlyList<string> Names { get; } =
        new[] { "ICRF", "ECLIPJ2000", "ITRF93", "IAU_EARTH", "IAU_MOON", "MOON_PA", "MOON_ME" };

    /// <summary>
    /// The framework frame of a name of spec.md section 2.1: ICRF is the SPICE J2000 frame, ECLIPJ2000 the framework's
    /// ecliptic, and the kernel-defined frames are the SPICE frames of the same name.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not one of <see cref="Names"/>.</exception>
    public static Frame Resolve(string name)
    {
        return name switch
        {
            "ICRF" => Frame.ICRF,
            "ECLIPJ2000" => Frame.ECLIPTIC_J2000,
            "ITRF93" or "IAU_EARTH" or "IAU_MOON" or "MOON_PA" or "MOON_ME" => new Frame(name),
            _ => throw new ArgumentException(
                $"The frame '{name}' is not a frame of spec.md section 2.1 ({string.Join(", ", Names)}).", nameof(name))
        };
    }
}
