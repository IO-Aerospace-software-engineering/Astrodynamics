// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

/// <summary>
/// The TT seconds between two epochs about the Earth, as the suite counts its elapsed times (spec.md section 3, the
/// time argument of the propagation, #501): <see cref="PropagationTimeArgument.Tt"/>, the tick-exact labels plus the
/// library's TDB − TT series in double.
/// </summary>
/// <remarks>
/// Never <c>(to.ToTDT() - from.ToTDT()).TotalSeconds</c>: a TDB label goes to TT through
/// <c>TimeSpan.FromSeconds(TdbMinusTt)</c>, which truncates the offset toward zero to the 100 ns tick, so that a TDB
/// label came out late by 0 to 1 tick when TDB − TT is positive, early when it is negative (#630). Between two epochs
/// tagged UTC, TAI or TT both expressions are exact in ticks.
/// </remarks>
internal static class TerrestrialSeconds
{
    /// <summary>The TT seconds from <paramref name="from"/> to <paramref name="to"/>, in any time frames.</summary>
    internal static double Between(in Time from, in Time to) => PropagationTimeArgument.Tt.SecondsBetween(from, to);
}
