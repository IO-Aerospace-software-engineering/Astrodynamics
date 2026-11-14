// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// Body constants read from the text of the kernels of <c>Data/SolarSystem</c>, without SPICE: <c>BODYnnn_GM</c> of
/// <c>gm_de440.tpc</c> and <c>BODYnnn_RADII</c> of <c>pck00011.tpc</c>, parsed by <see cref="double.Parse(string, IFormatProvider)"/>
/// (correctly rounded; a Fortran exponent <c>D</c> read as <c>E</c>), in SI units (#672).
/// </summary>
internal static class KernelTextValues
{
    private static readonly Lazy<KernelContent> Gm =
        new(() => KernelContent.ReadKernel(Path.Combine(Constants.SolarSystemKernelPath.FullName, "gm_de440.tpc")));

    private static readonly Lazy<KernelContent> Pck =
        new(() => KernelContent.ReadKernel(Path.Combine(Constants.SolarSystemKernelPath.FullName, "pck00011.tpc")));

    /// <summary>The GM of <c>BODYnnn_GM</c>, m³/s² (the text in km³/s² times 1e9).</summary>
    internal static double GravitationalParameter(int naifId) => Parse(Gm.Value.Values($"BODY{naifId}_GM").Single()) * 1E+09;

    /// <summary>Whether <c>gm_de440.tpc</c> gives a GM for the body.</summary>
    internal static bool HasGravitationalParameter(int naifId) => Gm.Value.Values($"BODY{naifId}_GM").Any();

    /// <summary>The radii of <c>BODYnnn_RADII</c>, m (the text in km times 1e3).</summary>
    internal static double[] Radii(int naifId) => Pck.Value.Values($"BODY{naifId}_RADII").Select(v => Parse(v) * 1000.0).ToArray();

    private static double Parse(string value) =>
        double.Parse(value.Replace('D', 'E').Replace('d', 'e'), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>
    /// The bound, in ulp, of a GM SPICE reads from the text against <see cref="GravitationalParameter"/>: SPICE's parse of a
    /// kernel's decimal text is not correctly rounded. CSPICE's <c>bodvcd_c</c>, called directly on <c>gm_de440.tpc</c>, is 1
    /// ulp from double.Parse for Nix, Kerberos, Pallas, the Didymos barycentre, the Earth and the Moon, 2 for the Earth
    /// barycentre and the Sun, 5 for the Jupiter barycentre (#672, Linux x64). The library's reads (MEASURE GM of ...) are
    /// at worst 5 ulp on Linux, 5 on Windows and 5 on macOS arm64, the Jupiter barycentre on each (CI run 43600640037,
    /// 2026-11-14); twice that worst.
    /// </summary>
    internal const long SpiceParseUlps = 10;

    /// <summary>The number of doubles between two finite doubles of the same sign (0 when equal).</summary>
    internal static long UlpDistance(double a, double b) =>
        a == b ? 0 : System.Math.Abs(BitConverter.DoubleToInt64Bits(a) - BitConverter.DoubleToInt64Bits(b));
}
