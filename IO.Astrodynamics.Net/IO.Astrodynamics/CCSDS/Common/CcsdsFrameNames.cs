// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Frames;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// The CCSDS name of a frame of the library, the <c>REF_FRAME</c> and <c>MAN_REF_FRAME</c> the OPM writers emit (#339,
/// decided by S. Guillet, 2026-11-18).
/// </summary>
/// <remarks>
/// <see cref="Frame.ICRF"/> is the SPICE frame <c>J2000</c>, which the library treats as the ICRF axes: its CCSDS name is
/// <c>ICRF</c>. Every other frame keeps its name, which is the CCSDS one for <see cref="Frame.GCRF"/> (<c>GCRF</c>),
/// <see cref="Frame.TEME"/> (<c>TEME</c>), <c>ITRF93</c>, <c>ECLIPJ2000</c>, <c>B1950</c> and <c>FK4</c>, the values of
/// <see cref="Enums.CcsdsReferenceFrame"/>. Before 11.0.0 the writers wrote <c>J2000</c>, which no CCSDS list holds and
/// <c>OpmValidator</c> reported as not a standard CCSDS frame. The reader maps <c>ICRF</c> and <c>GCRF</c> back onto
/// <see cref="Frame.ICRF"/>.
/// </remarks>
internal static class CcsdsFrameNames
{
    /// <summary>The CCSDS name of the ICRF, the name written for <see cref="Frame.ICRF"/>.</summary>
    internal const string Icrf = "ICRF";

    /// <summary>
    /// The CCSDS name of a frame: <c>ICRF</c> for the SPICE frame <c>J2000</c> (<see cref="Frame.ICRF"/>, in any letter case),
    /// the frame's own name otherwise.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The name to write under <c>REF_FRAME</c> or <c>MAN_REF_FRAME</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="frame"/> is null.</exception>
    internal static string ToCcsdsName(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return string.Equals(frame.Name, Frame.ICRF.Name, StringComparison.OrdinalIgnoreCase) ? Icrf : frame.Name;
    }
}
