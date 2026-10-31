// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// The time argument of a numerical propagation: the scale of its integration variable t, the seconds from the base
/// epoch of a segment, and every mapping between t and the epochs (#501). About the Earth (NAIF 399) it is TT, the
/// coordinate time of the geocentric reference system, which the conformance suite and GMAT count; about any other
/// central body, the Sun of a star or barycentre observer (SSB mode) included, TDB.
/// </summary>
/// <remarks>
/// <para>
/// Every <see cref="Time"/> this type returns is labelled TDB: the epochs handed to the forces, the caches, SPICE and
/// the output states keep their frame, and only the instant a label names changes. The forces at t are those of the
/// instant TT₀ + t: the bodies and the Earth's orientation at its TDB, the drag at its UTC.
/// </para>
/// <para>
/// The TT mapping is computed in double seconds, the tick-exact label plus IO's TDB − TT series
/// (<see cref="TDBTimeFrame.TdbMinusTt"/>), never through chained <see cref="Time"/> conversions, which truncate the
/// offset to the 100 ns tick at each step. A label is still truncated to the tick when it becomes a <see cref="Time"/>
/// (<see cref="Time.AddSeconds"/>), as before: it is at or before its instant, by less than 1.0012e-7 s (measured over
/// 200 000 random cases). The seconds between two epochs (<see cref="SecondsBetween"/>) and the length of a window
/// (<see cref="Length"/>) carry no truncation of the offset.
/// </para>
/// <para>
/// The TDB time argument is the code it replaces, expression for expression on TDB labels, so that a propagation about
/// any other body is bit for bit what it was; an epoch in another frame is converted to TDB before it is read (#507).
/// </para>
/// </remarks>
internal abstract class PropagationTimeArgument
{
    /// <summary>TDB seconds, about every central body other than the Earth.</summary>
    internal static readonly PropagationTimeArgument Tdb = new BarycentricDynamicalTime();

    /// <summary>TT seconds, about the Earth: the forces at the TDB label of TT₀ + t.</summary>
    internal static readonly PropagationTimeArgument Tt = new TerrestrialTime();

    private const double TicksPerSecond = TimeSpan.TicksPerSecond;
    private const double TicksPerCentury = TimeSpan.TicksPerDay * 36525.0;
    private const double SecondsPerCentury = 86400.0 * 36525.0;
    private static readonly long J2000Ticks = Time.J2000.Ticks;

    /// <summary>
    /// The relative drift between the time argument and the TDB labels below which the end of a window is taken as the
    /// end of the output grid: three times the largest rate of TDB − TT, 3.36e-10 (measured on the series from 1972 to
    /// 2100).
    /// </summary>
    internal const double DriftBound = 1e-9;

    /// <summary>
    /// The time argument of a propagation about <paramref name="centralBody"/>, the central body the propagator resolves:
    /// TT about the Earth (NAIF 399), TDB about any other body. A star or a barycentre resolves to the Sun, so TDB.
    /// </summary>
    internal static PropagationTimeArgument For(ILocalizable centralBody) =>
        centralBody is CelestialItem { NaifId: 399 } ? Tt : Tdb;

    /// <summary>
    /// The length of a propagation window, in seconds of this time argument: TDB, the length of
    /// <paramref name="tdbWindow"/>; TT, the TT seconds between the ends of <paramref name="window"/> as they are tagged
    /// (exact in ticks between UTC, TAI, GPS, local or TT tags, leap seconds counted; through the series in double for a
    /// TDB tag).
    /// </summary>
    /// <param name="window">The window as the caller gave it.</param>
    /// <param name="tdbWindow">The same window with its ends converted to TDB, the window of the propagator.</param>
    internal abstract double Length(in Window window, in Window tdbWindow);

    /// <summary>The epoch, a TDB label, <paramref name="t"/> seconds of this time argument after
    /// <paramref name="baseEpoch"/>: TDB, <c>baseEpoch.AddSeconds(t)</c>.</summary>
    internal abstract Time EpochAt(in Time baseEpoch, double t);

    /// <summary>The same, in the form the conjunction analysis uses: TDB, <c>baseEpoch + TimeSpan.FromSeconds(t)</c>,
    /// which differs from <see cref="Time.AddSeconds"/> by one tick for about 0.1 % of the values.</summary>
    internal abstract Time EpochAtBySpan(in Time baseEpoch, double t);

    /// <summary>The epochs of the start and of the end of an accepted step of a segment from
    /// <paramref name="baseEpoch"/>, as the conjunction analysis samples them.</summary>
    internal abstract (Time Start, Time End) StepEpochs(in Time baseEpoch, double cumulativeTime, double stepSize);

    /// <summary>
    /// The seconds of this time argument from <paramref name="baseEpoch"/> to <paramref name="epoch"/>, in any frame:
    /// TDB, the difference of the two epochs converted to TDB, the frame of the labels, <c>(epoch - baseEpoch)</c> for
    /// two TDB labels (#507); TT, through TAI for UTC, so leap seconds count.
    /// </summary>
    /// <remarks>
    /// A difference of two <see cref="Time"/>s in different frames is a difference of TAI, so TT seconds: a UTC epoch
    /// read on a TDB solution as <c>(epoch - baseEpoch)</c> would name another instant, by the change of TDB − TT
    /// since the base (0.22 m after a day in LEO, #507). The TDB time argument converts the epoch to the frame of the
    /// labels first, as the propagator converts its window (<c>ToTDB</c>), so an epoch read in the frame the caller
    /// gave reads the state of its label.
    /// </remarks>
    internal abstract double SecondsBetween(in Time baseEpoch, in Time epoch);

    /// <summary>
    /// The seconds from the start of an accepted step to <paramref name="epoch"/>: TDB, from the label of the start of
    /// the step, <paramref name="stepStart"/>; TT, from its exact instant, <paramref name="cumulativeTime"/> seconds
    /// after the base of its segment, <paramref name="segmentBase"/>.
    /// </summary>
    internal abstract double SecondsIntoStep(in Time segmentBase, in Time stepStart, double cumulativeTime,
        in Time epoch);

    /// <summary>
    /// The time left to integrate at <paramref name="current"/>, a segment base, in a propagation of
    /// <paramref name="duration"/> seconds from <paramref name="start"/> that ends at <paramref name="end"/>: TDB,
    /// <c>(end - current).TotalSeconds</c>; TT, the duration less the seconds from the start, without the truncation of
    /// the end label.
    /// </summary>
    internal abstract double Remaining(in Time start, double duration, in Time end, in Time current);

    /// <summary>The label of output state <paramref name="index"/>: TDB, <c>start + index * step</c>; TT, the TDB label
    /// of TT₀ + index × step, so that the outputs are uniform in TT (those of a UTC window fall on its UTC grid).</summary>
    internal abstract Time OutputEpoch(in Time start, int index, TimeSpan step);

    /// <summary>
    /// Whether a propagation whose outputs span <paramref name="outputDuration"/> seconds ends at the end of its window,
    /// of <paramref name="length"/> seconds, its last output labelled with the end of the window: TDB, never, the
    /// propagation then runs to the larger of the two; TT, when they differ by less than the drift between TT and the
    /// labels (<see cref="DriftBound"/> × <paramref name="length"/>), so that a window of a whole number of output steps
    /// in its own tags ends where the caller ended it.
    /// </summary>
    internal abstract bool EndsAtWindowEnd(double length, double outputDuration);

    /// <summary>
    /// A time t, in seconds of this time argument from <paramref name="baseEpoch"/> and within
    /// [<paramref name="tStart"/>, <paramref name="tEnd"/>], that <see cref="EpochAt"/> carries to
    /// <paramref name="epoch"/>, as the propagator does with the event time, so that the next segment starts exactly at
    /// the event; the time of <paramref name="epoch"/> clamped to the interval when there is none.
    /// </summary>
    internal double OnEpochGrid(in Time baseEpoch, in Time epoch, double tStart, double tEnd)
    {
        const int maxAdjustments = 64;
        double t = System.Math.Clamp(SecondsBetween(baseEpoch, epoch), tStart, tEnd);
        double onGrid = t;
        for (int i = 0; i < maxAdjustments && onGrid >= tStart && onGrid <= tEnd; i++)
        {
            var found = EpochAt(baseEpoch, onGrid);
            if (found == epoch)
                return onGrid;

            onGrid = found < epoch ? System.Math.BitIncrement(onGrid) : System.Math.BitDecrement(onGrid);
        }

        return t;
    }

    // The TT of an instant as a tick count and a correction in seconds, TT = ticks / 1e7 + correction: a TDB label by
    // the series in double, any other frame exactly in ticks (TT, TAI, GPS, UTC and local time differ by whole ticks)
    private static (long Ticks, double Correction) TerrestrialTimeParts(in Time time)
    {
        if (time.Frame is TDBTimeFrame)
            return (time.DateTime.Ticks, -TdbMinusTtAtTdbLabel(time.DateTime.Ticks));

        if (time.Frame is TDTTimeFrame)
            return (time.DateTime.Ticks, 0.0);

        return (time.ToTDT().DateTime.Ticks, 0.0);
    }

    // The last TDB label read, with its TDB − TT: the 13 stages of a step share their base
    [ThreadStatic] private static long _lastLabelTicks;
    [ThreadStatic] private static double _lastLabelOffset;
    [ThreadStatic] private static bool _hasLastLabel;

    /// <summary>
    /// TDB − TT at the instant of a TDB label, in double: the series at the TDB centuries of the label, then one
    /// fixed-point step to its TT centuries. The series moves by 3.36e-10 s per second at most, so the step leaves
    /// rate² × |Δ| ≈ 2e-22 s, below the resolution of Δ in double (2.2e-19 s).
    /// </summary>
    internal static double TdbMinusTtAtTdbLabel(long tdbTicks)
    {
        if (_hasLastLabel && _lastLabelTicks == tdbTicks)
            return _lastLabelOffset;

        double centuries = (tdbTicks - J2000Ticks) / TicksPerCentury;
        double offset = TDBTimeFrame.TdbMinusTt(centuries);
        offset = TDBTimeFrame.TdbMinusTt(centuries - offset / SecondsPerCentury);
        _lastLabelTicks = tdbTicks;
        _lastLabelOffset = offset;
        _hasLastLabel = true;
        return offset;
    }

    private sealed class BarycentricDynamicalTime : PropagationTimeArgument
    {
        internal override double Length(in Window window, in Window tdbWindow) => tdbWindow.Length.TotalSeconds;

        internal override Time EpochAt(in Time baseEpoch, double t) => baseEpoch.AddSeconds(t);

        internal override Time EpochAtBySpan(in Time baseEpoch, double t) => baseEpoch + TimeSpan.FromSeconds(t);

        internal override (Time Start, Time End) StepEpochs(in Time baseEpoch, double cumulativeTime, double stepSize)
        {
            var start = baseEpoch + TimeSpan.FromSeconds(cumulativeTime);
            return (start, start + TimeSpan.FromSeconds(stepSize));
        }

        // ToTDB is the identity on a TDB label (#510): two labels give (epoch - baseEpoch), the former expression
        internal override double SecondsBetween(in Time baseEpoch, in Time epoch) =>
            (epoch.ToTDB() - baseEpoch.ToTDB()).TotalSeconds;

        internal override double SecondsIntoStep(in Time segmentBase, in Time stepStart, double cumulativeTime,
            in Time epoch) => SecondsBetween(stepStart, epoch);

        internal override double Remaining(in Time start, double duration, in Time end, in Time current) =>
            (end - current).TotalSeconds;

        internal override Time OutputEpoch(in Time start, int index, TimeSpan step) => start + (index * step);

        internal override bool EndsAtWindowEnd(double length, double outputDuration) => false;
    }

    private sealed class TerrestrialTime : PropagationTimeArgument
    {
        internal override double Length(in Window window, in Window tdbWindow) =>
            SecondsBetween(window.StartDate, window.EndDate);

        // TDB(TT_b + t) = TDB_b + t + (Δ(TT_b + t) − Δ_b), Δ = TDB − TT: the label of the base plus a double, which
        // AddSeconds truncates to the tick; no conversion of Time, so no second truncation
        internal override Time EpochAt(in Time baseEpoch, double t)
        {
            var (ticks, correction) = TerrestrialTimeParts(baseEpoch);
            double ttSeconds = (ticks - J2000Ticks) / TicksPerSecond + correction + t;
            double offset = TDBTimeFrame.TdbMinusTt(ttSeconds / SecondsPerCentury);
            var label = baseEpoch.Frame is TDBTimeFrame ? baseEpoch : new Time(new DateTime(ticks), TimeFrame.TDBFrame);
            return label.AddSeconds(t + (offset + correction));
        }

        internal override Time EpochAtBySpan(in Time baseEpoch, double t) => EpochAt(baseEpoch, t);

        internal override (Time Start, Time End) StepEpochs(in Time baseEpoch, double cumulativeTime, double stepSize) =>
            (EpochAt(baseEpoch, cumulativeTime), EpochAt(baseEpoch, cumulativeTime + stepSize));

        // The tick difference is exact as a long, the correction a small double
        internal override double SecondsBetween(in Time baseEpoch, in Time epoch)
        {
            var (fromTicks, fromCorrection) = TerrestrialTimeParts(baseEpoch);
            var (toTicks, toCorrection) = TerrestrialTimeParts(epoch);
            return (toTicks - fromTicks) / TicksPerSecond + (toCorrection - fromCorrection);
        }

        internal override double SecondsIntoStep(in Time segmentBase, in Time stepStart, double cumulativeTime,
            in Time epoch) => SecondsBetween(segmentBase, epoch) - cumulativeTime;

        internal override double Remaining(in Time start, double duration, in Time end, in Time current) =>
            duration - SecondsBetween(start, current);

        internal override Time OutputEpoch(in Time start, int index, TimeSpan step) =>
            EpochAt(start, (index * step).TotalSeconds);

        internal override bool EndsAtWindowEnd(double length, double outputDuration) =>
            System.Math.Abs(outputDuration - length) <= DriftBound * length;
    }
}
