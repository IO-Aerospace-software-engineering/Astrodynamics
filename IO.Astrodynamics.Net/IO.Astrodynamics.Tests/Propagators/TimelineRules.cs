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
/// window) and its window ends its hold later; an impulse at its event tc has the thrust window [tc − f dt,
/// tc + (1 − f) dt], tc the centroid of the acceleration of the burn (f, <see cref="ThrustLead"/>, by quadrature here;
/// #335, derived from S. Guillet's remark of 2026-11-18, decided by S. Guillet, 2026-11-18: a fixed 0.666 before 11.0.0), and the maneuver window [tc − f dt,
/// tc − f dt + max(hold, dt)], whose end gates the next maneuver; its attitude is in force from the start of its thrust window (no earlier than the start of the propagation or
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
    /// <paramref name="dt"/> and lead fraction <paramref name="lead"/> (<see cref="ThrustLead"/>): tc − f dt.</summary>
    internal static Time ThrustStart(Time tc, double dt, double lead, bool tt) => After(tc, -lead * dt, tt);

    /// <summary>The end of the maneuver window of an impulse at <paramref name="tc"/>: tc − f dt + max(hold, dt), the
    /// gate of the next maneuver.</summary>
    internal static Time ManeuverEnd(Time tc, double dt, double lead, double hold, bool tt) =>
        After(tc, -lead * dt + System.Math.Max(hold, dt), tt);

    /// <summary>
    /// The fraction f of a burn of constant thrust and fuel flow before the centroid of its acceleration (#335), by
    /// composite Gauss–Legendre quadrature (64 panels of 8 points), independent of the closed form and the series of the
    /// library. With the mass fraction burned x = 1 − e^(−U), U = Δv / (Isp g₀), and u = τ/dt, the acceleration is
    /// proportional to 1/(1 − x u): f = ∫₀¹ u/(1 − x u) du / ∫₀¹ 1/(1 − x u) du up to x = 0.9; above, where that integrand
    /// nears its pole at u = 1/x, the same centroid weighted by the velocity change, the epoch of the burn at which the
    /// velocity change reaches s Isp g₀ being dt (1 − e^(−s)) / x: f = ∫₀^U (1 − e^(−s)) ds / (U x). 1/2 for no burn.
    /// </summary>
    internal static double ThrustLead(double deltaV, double isp)
    {
        double total = deltaV / (isp * G0);
        double x = 1.0 - System.Math.Exp(-total);
        if (x <= 0.9)
        {
            return Quadrature(u => u / (1.0 - x * u), 1.0) / Quadrature(u => 1.0 / (1.0 - x * u), 1.0);
        }

        return Quadrature(v => 1.0 - System.Math.Exp(-v), total) / (total * x);
    }

    // ∫₀^b g by composite Gauss–Legendre, 64 panels of 8 points
    private static double Quadrature(System.Func<double, double> g, double b)
    {
        const int panels = 64;
        double half = 0.5 * b / panels, sum = 0.0;
        for (int p = 0; p < panels; p++)
        {
            double middle = (2 * p + 1) * half;
            for (int k = 0; k < GaussNodes.Length; k++)
            {
                sum += half * GaussWeights[k] * g(middle + half * GaussNodes[k]);
            }
        }

        return sum;
    }

    // The nodes and weights of the 8-point Gauss–Legendre rule on [-1, 1] (Abramowitz and Stegun, table 25.4; the doubles
    // of numpy.polynomial.legendre.leggauss(8))
    private static readonly double[] GaussNodes =
    {
        -0.9602898564975362, -0.7966664774136267, -0.525532409916329, -0.18343464249564978,
        0.18343464249564978, 0.525532409916329, 0.7966664774136267, 0.9602898564975362
    };

    private static readonly double[] GaussWeights =
    {
        0.10122853629037706, 0.22238103445337443, 0.3137066458778869, 0.36268378337836166,
        0.36268378337836166, 0.3137066458778869, 0.22238103445337443, 0.10122853629037706
    };

    /// <summary>
    /// The thrust window of an impulse executed at <paramref name="tc"/> by <c>TryExecute</c> (TDB seconds), of the burn
    /// duration <paramref name="duration"/>, against the rule: it opens f dt before <paramref name="tc"/>, f the lead of
    /// the centroid of the acceleration (<see cref="ThrustLead"/>, by quadrature), to the tick of the epochs, in the frame
    /// of <paramref name="tc"/>, and lasts <paramref name="duration"/> (#335).
    /// </summary>
    internal static void AssertThrustWindow(Time tc, double deltaV, double isp, System.TimeSpan duration,
        IO.Astrodynamics.TimeSystem.Window? actual)
    {
        Xunit.Assert.True(actual.HasValue, "not executed");
        var expectedStart = tc.AddSeconds(-ThrustLead(deltaV, isp) * duration.TotalSeconds);
        double startError = (actual.Value.StartDate - expectedStart).TotalSeconds;
        Xunit.Assert.True(System.Math.Abs(startError) <= 1.0e-7, $"thrust start {startError:E3} s from the centroid rule");
        Xunit.Assert.Equal(tc.Scale, actual.Value.StartDate.Scale);
        Xunit.Assert.Equal(duration, actual.Value.Length);
    }

    /// <summary>The activation of an attitude: the later of its minimum epoch and the end of the previous window.</summary>
    internal static Time Activation(Time minimumEpoch, Time previousEnd) => minimumEpoch > previousEnd ? minimumEpoch : previousEnd;
}
