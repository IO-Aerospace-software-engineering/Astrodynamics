using System;
using System.Collections.Generic;
using System.Linq;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

/// <summary>
/// The light-time flags of the suite (spec.md sections 12.2 and 14.1) and the library's corrections, SPICE's:
/// <c>none</c> → <see cref="Aberration.None"/>, <c>lt</c> → <see cref="Aberration.LT"/>, <c>lt+s</c> →
/// <see cref="Aberration.LTS"/>, <c>xlt</c> → <see cref="Aberration.XLT"/>, <c>cn</c> → <see cref="Aberration.CN"/>.
/// </summary>
public static class LightTimeFlags
{
    private static readonly Dictionary<string, Aberration> Corrections = new(StringComparer.Ordinal)
    {
        ["none"] = Aberration.None,
        ["lt"] = Aberration.LT,
        ["lt+s"] = Aberration.LTS,
        ["xlt"] = Aberration.XLT,
        ["cn"] = Aberration.CN
    };

    /// <summary>The flags of a <c>ground_station</c> case (spec.md section 14.1).</summary>
    public static readonly IReadOnlyList<string> Station = ["none", "lt", "lt+s", "xlt"];

    /// <summary>The flags of an <c>eclipse</c> case of the second branch (spec.md section 12.2).</summary>
    public static readonly IReadOnlyList<string> Eclipse = ["none", "cn"];

    /// <summary>The correction of a flag among <paramref name="allowed"/>.</summary>
    /// <exception cref="ArgumentException">The flag is not one of them.</exception>
    public static Aberration Map(string flag, IReadOnlyList<string> allowed, string where)
    {
        if (flag == null || !allowed.Contains(flag, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"The light time '{flag ?? "null"}' of {where} is not one of {string.Join(", ", allowed.Select(f => $"'{f}'"))}.");
        }

        return Corrections[flag];
    }
}
