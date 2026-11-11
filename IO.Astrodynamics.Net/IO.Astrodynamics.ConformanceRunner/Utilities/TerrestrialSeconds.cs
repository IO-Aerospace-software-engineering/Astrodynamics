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
/// Never <c>(to.ToTDT() - from.ToTDT()).TotalSeconds</c>: a TDB label goes to TT with its offset TDB − TT rounded to
/// the 100 ns tick, so that its TT is off by up to half a tick (#625; up to a tick when the offset was truncated toward
/// zero, #630). Between two epochs tagged UTC, TAI or TT both expressions are exact in ticks.
/// </remarks>
internal static class TerrestrialSeconds
{
    /// <summary>The TT seconds from <paramref name="from"/> to <paramref name="to"/>, in any time frames.</summary>
    internal static double Between(in Time from, in Time to) => PropagationTimeArgument.Tt.SecondsBetween(from, to);
}
