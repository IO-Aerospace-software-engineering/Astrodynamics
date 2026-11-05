// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Tests.Body;

namespace IO.Astrodynamics.Tests.Propagators;

// Inside IO.Astrodynamics.Tests the namespace Time shadows the library type
using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The rules of the attitude timeline of a propagation (#539, #547), written here: the reference of the epochs of its
/// activations and windows, never read back from the library. The burn duration of an impulse by the rocket equation in
/// closed form, the TT seconds by CSPICE's <c>unitim_c</c> (<see cref="CspiceDirect"/>) on Linux, whose native library
/// exports it. Elsewhere (the Windows DLL exports only the proxies) the TT seconds are counted by
/// <c>PropagationTimeArgument.Tt</c>, the elapsed count the repository's rules allow for a reference about the Earth, and
/// the comparisons of the library's epochs with the rules (<see cref="Checked"/>) are made on Linux only.
/// </summary>
/// <remarks>
/// <para>
/// The rules (design of #554, §4.1): an attitude activates at max(its minimum epoch, the end of the previous maneuver's
/// window) and its window ends its hold later; an impulse at its event tc has the thrust window [tc − 0.666 dt,
/// tc + 0.334 dt] and the maneuver window [tc − 0.666 dt, tc − 0.666 dt + max(hold, dt)], whose end gates the next
/// maneuver; its attitude is in force from the start of its thrust window (no earlier than the start of the propagation or
/// the previous activation) to the end of its maneuver window. About the Earth the seconds are TT seconds (#547), about
/// any other body, SSB mode included, TDB seconds.
/// </para>
/// <para>
/// <see cref="RuleBound"/> is twice the worst over Linux x64, Windows and macOS arm64 (CI run 42021804663), rounded up at
/// two digits; twice the 1.20e-7 s of Windows and macOS arm64 falls on the boundary 2.4e-7 s, so the step above. The reference
/// itself resolves 1.2e-7 s: one rounding of <c>unitim_c</c>'s result (a double of 7.6e8 s past J2000) per conversion,
/// and the tick (1e-7 s) of the epochs.
/// </para>
/// </remarks>
internal static class TimelineRules
{
    /// <summary>Standard gravity, m/s², of the rocket equation.</summary>
    internal const double G0 = 9.80665;

    /// <summary>The library's epochs against the rules, s (see the remarks). Worst 1.00e-7 / 1.20e-7 / 1.20e-7 s on Linux /
    /// Windows / macOS arm64: one tick on every rule comparison; on Windows and macOS arm64, the hold of a UTC minimum epoch
    /// counted in TT seconds about the Earth (AttitudeTimelineEdgeTests). Every row of AttitudeTimelineTests and
    /// AttitudeTimelineEdgeTests, the changes of AttitudeRecordTests and its NearEpochChain, the leading attitudes of
    /// RepropagationTests (CI run 42021804663).</summary>
    internal const double RuleBound = 2.5e-7;

    /// <summary>The burn duration of an impulse of <paramref name="deltaV"/> m/s from <paramref name="mass"/> kg with a
    /// specific impulse of <paramref name="isp"/> s and a fuel flow of <paramref name="fuelFlow"/> kg/s: the fuel burned by
    /// the rocket equation, m₀ (1 − e^(−Δv / (Isp g₀))), over the flow.</summary>
    internal static double BurnDuration(double mass, double deltaV, double isp, double fuelFlow) =>
        mass * (1.0 - System.Math.Exp(-deltaV / (isp * G0))) / fuelFlow;

    /// <summary>The fuel burned by an impulse, kg, by the rocket equation.</summary>
    internal static double FuelBurned(double mass, double deltaV, double isp) => mass * (1.0 - System.Math.Exp(-deltaV / (isp * G0)));

    /// <summary>Whether the TT seconds are those of <c>unitim_c</c>, independent of the library: the comparisons of its
    /// epochs with the rules are made then.</summary>
    internal static bool Checked => System.OperatingSystem.IsLinux();

    // TT - TDB at a TDB epoch, s, by unitim_c
    private static double TtMinusTdb(double et) => CspiceDirect.TdbToTt(et) - et;

    private static double Et(Time t) => t.ToTDB().TimeSpanFromJ2000().TotalSeconds;

    /// <summary>The TDB epoch <paramref name="seconds"/> after <paramref name="from"/>: TT seconds by <c>unitim_c</c>
    /// when <paramref name="tt"/> (about the Earth), TDB seconds otherwise.</summary>
    internal static Time After(Time from, double seconds, bool tt)
    {
        if (!tt)
        {
            return from.AddSeconds(seconds);
        }

        if (!Checked)
        {
            return PropagationTimeArgument.Tt.EpochAt(from, seconds);
        }

        // TT(end) = TT(from) + seconds: end = from + seconds + (TT - TDB)(from) - (TT - TDB)(end), the offsets apart so that
        // the epochs keep their ticks
        double et = Et(from);
        return from.AddSeconds(seconds + TtMinusTdb(et) - TtMinusTdb(et + seconds));
    }

    /// <summary>The seconds from <paramref name="from"/> to <paramref name="to"/>: TT seconds by <c>unitim_c</c> when
    /// <paramref name="tt"/>, TDB seconds otherwise.</summary>
    internal static double SecondsBetween(Time from, Time to, bool tt)
    {
        double tdb = (to - from).TotalSeconds;
        if (!tt)
        {
            return tdb;
        }

        if (!Checked)
        {
            return PropagationTimeArgument.Tt.SecondsBetween(from, to);
        }

        return tdb + TtMinusTdb(Et(to)) - TtMinusTdb(Et(from));
    }

    /// <summary>The start of the thrust window of an impulse at <paramref name="tc"/> of burn duration
    /// <paramref name="dt"/>: tc − 0.666 dt.</summary>
    internal static Time ThrustStart(Time tc, double dt, bool tt) => After(tc, -0.666 * dt, tt);

    /// <summary>The end of the maneuver window of an impulse at <paramref name="tc"/>: tc − 0.666 dt + max(hold, dt), the
    /// gate of the next maneuver.</summary>
    internal static Time ManeuverEnd(Time tc, double dt, double hold, bool tt) => After(tc, -0.666 * dt + System.Math.Max(hold, dt), tt);

    /// <summary>The activation of an attitude: the later of its minimum epoch and the end of the previous window.</summary>
    internal static Time Activation(Time minimumEpoch, Time previousEnd) => minimumEpoch > previousEnd ? minimumEpoch : previousEnd;
}
