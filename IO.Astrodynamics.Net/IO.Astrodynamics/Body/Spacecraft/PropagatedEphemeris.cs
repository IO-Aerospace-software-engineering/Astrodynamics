// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body.Spacecraft;

/// <summary>
/// The stored states of a propagated spacecraft, in ICRF relative to the central body, split into arcs at each impulse
/// (#540): no interpolation runs across the jump of the velocity. Each arc holds the state after the impulse that opens
/// it, the output states strictly inside it and the state before the impulse that closes it, both at the impulse's epoch.
/// An output closer to an impulse than <see cref="DropFraction"/> of its distance to the next output away from it is
/// dropped, the impulse's state kept: two nodes that close make the interpolation ill-conditioned. The first and the last
/// output, the ends of the window, are always kept. An arc bounded by an impulse and holding fewer than
/// <see cref="MinimumArcStates"/> states takes <see cref="FilledArcStates"/> states of the solution, evenly spaced over it,
/// its two ends kept, when they can be <see cref="MinimumFillSpacing"/> apart. An arc of zero length (two impulses at one
/// event), or between two impulses and not longer than <see cref="SameEpochTolerance"/>, is not stored; an impulse at the
/// end of the propagation, which the last segment of the solution carries, a segment of zero length (#575), opens a last
/// arc of one state, the state after it; an impulse that no segment of the solution carries (a
/// solution built otherwise) closes the last arc. At an impulse's
/// epoch the reads take the later arc: the state after the impulse, as <see cref="PropagationSolution.InterpolateAt"/>,
/// SPICE (the later segment of an SPK) and GMAT give it. Impulses each within <see cref="SameEpochTolerance"/> of the one
/// before are one epoch (#579, decided by S. Guillet, 2026-11-17): the arc after them opens at the first with the state
/// after them all (the position of the first, its velocity after it plus the jump of each later one), so that the reads
/// and the SPK from the first impulse on give the state after them all, as for impulses at one event; before 11.0.0 the
/// arc after them opened at the last, and a read in between took the arc before them all.
/// </summary>
/// <remarks>
/// <para>
/// The arcs are immutable arrays in epoch order, replaced as a whole under a lock of the writers, so that the reads take
/// no lock and never sort (#522). A read interpolates the 8 states of its arc nearest to its epoch, found by a binary
/// search, in the order and arithmetic of the former sort of every stored state (<see cref="Lagrange.InterpolateSorted"/>):
/// far from the impulses, a read is bit for bit what it was before 11.0.0.
/// </para>
/// <para>
/// Without an impulse the states make one arc, never filled, as before 11.0.0.
/// </para>
/// </remarks>
internal sealed class PropagatedEphemeris
{
    /// <summary>Two impulses within 2 µs are one epoch (the same-epoch rule of the attitude record): no arc between them, the
    /// arc after them opening at the first (#579).</summary>
    internal const double SameEpochTolerance = 2.0e-6;

    /// <summary>
    /// An output closer to an impulse than this fraction of its distance to the next output away from the impulse (the
    /// output step) is dropped. Kept beside the impulse's state, such a node makes the Lagrange interpolation of the arc
    /// ill-conditioned, and the SPK more so, its epochs doubles of 1.2e-7 s resolution: an output 3 µs from the impulse put
    /// the SPK 301 m off at 10 s outputs, 1 804 m at 60 s; one 0.1 s from it 5.7e-3 m (review of PR 1 of #554, ARC-1).
    /// Measured on #540's LEO, RK7(8), 1, 10 and 60 s outputs, the output 1e-4 to 0.5 step from the impulse on either side:
    /// below a tenth of the step, dropping it keeps the SPK at its floor, 4e-4 to 1.5e-3 m, where keeping it costs up to
    /// 3.3e-3 m (1 s outputs) and 2.5e-3 m (10 and 60 s outputs) at 0.04 step; above, keeping it is as good or better, the
    /// managed reads at 60 s outputs most (1.6e-4 against 3.8e-4 m at 0.2 step).
    /// </summary>
    internal const double DropFraction = 0.1;

    /// <summary>
    /// The smallest spacing of the states of a filled arc, s: a shorter arc keeps its states (its interpolation over less
    /// than 15 µs is linear within 3e-10 m), so that the states of an arc are distinct epochs of the SPK, doubles of
    /// 1.2e-7 s resolution in 2024, 9.5e-7 s until 2136 (review of PR 1 of #554, spk-F2: 16 states over 2.1 µs collided).
    /// </summary>
    internal const double MinimumFillSpacing = 1.0e-6;

    /// <summary>An arc bounded by an impulse with fewer stored states than this is filled from the solution.</summary>
    internal const int MinimumArcStates = 4;

    /// <summary>The number of states of a filled arc, its ends included.</summary>
    internal const int FilledArcStates = 16;

    private readonly object _writeLock = new();

    // The arcs in time order, each in epoch order, and the first epoch of each
    private sealed class Snapshot
    {
        internal static readonly Snapshot Empty = new(Array.Empty<StateVector[]>());

        internal Snapshot(StateVector[][] arcs)
        {
            Arcs = arcs;
            Starts = new Time[arcs.Length];
            for (int k = 0; k < arcs.Length; k++)
            {
                Starts[k] = arcs[k][0].Epoch;
                Count += arcs[k].Length;
            }
        }

        internal StateVector[][] Arcs { get; }
        internal Time[] Starts { get; }
        internal int Count { get; }

        // The arc of an epoch: the last one that starts at or before it (the later one at an impulse), the first one before
        // every arc
        internal int ArcIndex(in Time epoch)
        {
            int lo = 1, hi = Starts.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Starts[mid] <= epoch) lo = mid + 1;
                else hi = mid;
            }

            return lo - 1;
        }
    }

    private volatile Snapshot _snapshot = Snapshot.Empty;

    /// <summary>The number of stored states, both states of an impulse and the filled ones included.</summary>
    internal int Count => _snapshot.Count;

    /// <summary>The arcs, in time order.</summary>
    internal IReadOnlyList<StateVector[]> Arcs => _snapshot.Arcs;

    /// <summary>Every stored state in epoch order, both states of an impulse included (the state before first).</summary>
    internal StateVector[] All => _snapshot.Arcs.SelectMany(a => a).ToArray();

    /// <summary>The first stored state; null when there is none.</summary>
    internal StateVector First
    {
        get
        {
            var arcs = _snapshot.Arcs;
            return arcs.Length > 0 ? arcs[0][0] : null;
        }
    }

    /// <summary>From the first stored epoch to the last; null when there is no state.</summary>
    internal Window? Span
    {
        get
        {
            var arcs = _snapshot.Arcs;
            return arcs.Length > 0 ? new Window(arcs[0][0].Epoch, arcs[^1][^1].Epoch) : null;
        }
    }

    internal void Clear()
    {
        lock (_writeLock)
        {
            _snapshot = Snapshot.Empty;
        }
    }

    /// <summary>
    /// Replaces every stored state by those of a propagation, split at its impulses.
    /// </summary>
    /// <param name="outputs">The output states, in ICRF, in epoch order.</param>
    /// <param name="impulses">The impulses, their states in ICRF, in epoch order; the state after the last one null when no
    /// segment of the solution carries it: the states end with the state before it. An impulse at the end of the
    /// propagation is carried by a last segment of zero length (#575).</param>
    /// <param name="fill">The state of the solution at an epoch inside an arc, in ICRF, with its covariance in covariance
    /// mode, for the short arcs; null for no fill.</param>
    internal void Set(IReadOnlyList<StateVector> outputs, IReadOnlyList<ImpulseBoundary> impulses, Func<Time, StateVector> fill)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(impulses);
        var arcs = new List<StateVector[]>(impulses.Count + 1);
        int o = 0;
        // The first impulse of a group within the same-epoch tolerance whose arcs between were not stored (#579)
        int groupStart = -1;
        for (int k = 0; k <= impulses.Count; k++)
        {
            // An impulse that the solution does not carry ends it: no arc after it
            if (k > 0 && impulses[k - 1].After == null) break;
            bool opened = k > 0, closed = k < impulses.Count;
            var arc = new List<StateVector>();
            if (opened) arc.Add(impulses[k - 1].After);
            for (; o < outputs.Count; o++)
            {
                // An output at or after the closing impulse belongs to the next arc
                if (closed && outputs[o].Epoch >= impulses[k].Epoch) break;
                if (opened && IsTooNear(outputs, o, impulses[k - 1].Epoch, true)) continue;
                if (closed && IsTooNear(outputs, o, impulses[k].Epoch, false)) continue;
                arc.Add(outputs[o]);
            }

            if (closed) arc.Add(impulses[k].Before);
            if (arc.Count == 0) continue;

            // An arc of zero length holds no motion (two impulses at one event), and one between impulses shorter than the
            // same-epoch tolerance is one epoch; at the ends of the window the first and the last output keep their arc
            bool bounded = opened || closed;
            double span = (arc[^1].Epoch - arc[0].Epoch).TotalSeconds;
            if (opened && !closed && span <= 0.0)
            {
                // An impulse at the end of the propagation (#575): the last arc is its state after alone (the last output,
                // at the same epoch, is the same state), so that the reads at the end give the state the solution ends with;
                // after a group of impulses within the same-epoch tolerance, the state after them all at the first (#579)
                arcs.Add([groupStart >= 0 ? AfterGroup(impulses, groupStart, k - 1) : arc[0]]);
                groupStart = -1;
                continue;
            }

            if (bounded && (span <= 0.0 || opened && closed && span <= SameEpochTolerance))
            {
                // Impulses at one event keep the state after the last, at their epoch, as before
                if (opened && closed && span > 0.0 && groupStart < 0) groupStart = k - 1;
                continue;
            }

            if (bounded && arc.Count < MinimumArcStates && fill != null && span >= (FilledArcStates - 1) * MinimumFillSpacing)
            {
                arc = Fill(arc[0], arc[^1], fill);
            }

            // The arc after a group of impulses opens at the first of them with the state after them all (#579)
            if (opened && groupStart >= 0)
            {
                arc[0] = AfterGroup(impulses, groupStart, k - 1);
            }

            groupStart = -1;
            arcs.Add(arc.ToArray());
        }

        lock (_writeLock)
        {
            _snapshot = new Snapshot(arcs.ToArray());
        }
    }

    // The state after the impulses first..last, one epoch, at the epoch of the first: its position, and its velocity after
    // it plus the jump of each later one (the state after the last, at a velocity that differs by that jump over at most the
    // tolerance of each, a few 1e-5 m/s in LEO); the covariance of the state after the last
    private static StateVector AfterGroup(IReadOnlyList<ImpulseBoundary> impulses, int first, int last)
    {
        var opening = impulses[first].After;
        var velocity = opening.Velocity;
        for (int j = first + 1; j <= last; j++)
        {
            velocity += impulses[j].After.Velocity - impulses[j].Before.Velocity;
        }

        var after = impulses[last].After;
        return new StateVector(opening.Position, velocity, opening.Observer, opening.Epoch, opening.Frame, after.Covariance);
    }

    // An output closer to the impulse than DropFraction of its distance to the next output away from it; the first and the
    // last output, which have none, are kept
    private static bool IsTooNear(IReadOnlyList<StateVector> outputs, int o, in Time impulse, bool opening)
    {
        int neighbour = opening ? o + 1 : o - 1;
        if (neighbour < 0 || neighbour >= outputs.Count) return false;
        var epoch = outputs[o].Epoch;
        double distance = System.Math.Abs((epoch - impulse).TotalSeconds);
        double step = System.Math.Abs((outputs[neighbour].Epoch - epoch).TotalSeconds);
        return distance < DropFraction * step;
    }

    // FilledArcStates states evenly spaced over an arc, its two end states kept
    private static List<StateVector> Fill(StateVector first, StateVector last, Func<Time, StateVector> fill)
    {
        var filled = new List<StateVector>(FilledArcStates) { first };
        double span = (last.Epoch - first.Epoch).TotalSeconds;
        for (int i = 1; i < FilledArcStates - 1; i++)
        {
            filled.Add(fill(first.Epoch.AddSeconds(span * i / (FilledArcStates - 1))));
        }

        filled.Add(last);
        return filled;
    }

    /// <summary>
    /// Adds the states of one window (decisions 83 and 84 of #668, #792, decided by S. Guillet, 2026-11-23): when the hull
    /// of their epochs, closed, meets no stored arc (touching counts as meeting), they make a new arc, inserted in time
    /// order; otherwise the arcs the hull meets are merged where a gap separates them (arcs that touch, at an impulse, stay
    /// apart), and each state joins the arc of its epoch among them (the later one at an impulse, the first one before
    /// them, the last one after), replacing a state of that arc at the same epoch. A gap inside the states of one call is
    /// not detected: they are one window. Before 11.0.0 every state joined the arc of its epoch, so the states of disjoint
    /// windows made one arc, read across the gap between them.
    /// </summary>
    internal void Add(IEnumerable<StateVector> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        var added = states.ToArray();
        if (added.Length == 0) return;
        lock (_writeLock)
        {
            var arcs = _snapshot.Arcs.Select(a => a.ToList()).ToList();
            var min = added[0].Epoch;
            var max = min;
            foreach (var sv in added)
            {
                if (sv.Epoch < min) min = sv.Epoch;
                if (sv.Epoch > max) max = sv.Epoch;
            }

            // The arcs the hull [min, max] meets, consecutive since the arcs are disjoint and in time order
            int first = -1, last = -1;
            for (int k = 0; k < arcs.Count; k++)
            {
                if (arcs[k][0].Epoch <= max && arcs[k][^1].Epoch >= min)
                {
                    if (first < 0) first = k;
                    last = k;
                }
            }

            if (first < 0)
            {
                // A window apart from every arc: an arc of its own, in time order
                var arc = new List<StateVector>(added.Length);
                foreach (var sv in added) Insert(arc, sv);
                int at = 0;
                while (at < arcs.Count && arcs[at][0].Epoch < arc[0].Epoch) at++;
                arcs.Insert(at, arc);
            }
            else
            {
                // The gaps the window covers close; the arcs that touch at an impulse stay apart
                for (int k = last; k > first; k--)
                {
                    if (arcs[k - 1][^1].Epoch < arcs[k][0].Epoch)
                    {
                        arcs[k - 1].AddRange(arcs[k]);
                        arcs.RemoveAt(k);
                        last--;
                    }
                }

                foreach (var sv in added)
                {
                    int target = first;
                    for (int k = last; k > first; k--)
                    {
                        if (arcs[k][0].Epoch <= sv.Epoch)
                        {
                            target = k;
                            break;
                        }
                    }

                    Insert(arcs[target], sv);
                }
            }

            _snapshot = new Snapshot(arcs.Select(a => a.ToArray()).ToArray());
        }
    }

    // Inserts a state in epoch order, replacing a state at the same epoch
    private static void Insert(List<StateVector> arc, StateVector sv)
    {
        int lo = 0, hi = arc.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (arc[mid].Epoch < sv.Epoch) lo = mid + 1;
            else hi = mid;
        }

        if (lo < arc.Count && arc[lo].Epoch == sv.Epoch)
        {
            arc[lo] = sv;
        }
        else
        {
            arc.Insert(lo, sv);
        }
    }

    /// <summary>
    /// The windows the stored states cover, in time order: each a run of arcs that touch (at an impulse), from its first
    /// epoch to its last, two windows separated by a gap between added windows (#792); an arc of one state apart from the
    /// others a window of zero length.
    /// </summary>
    internal IReadOnlyList<Window> Windows
    {
        get
        {
            var arcs = _snapshot.Arcs;
            var windows = new List<Window>();
            if (arcs.Length == 0) return windows;
            var start = arcs[0][0].Epoch;
            var end = arcs[0][^1].Epoch;
            for (int k = 1; k < arcs.Length; k++)
            {
                if (arcs[k][0].Epoch == end)
                {
                    end = arcs[k][^1].Epoch;
                    continue;
                }

                windows.Add(new Window(start, end));
                start = arcs[k][0].Epoch;
                end = arcs[k][^1].Epoch;
            }

            windows.Add(new Window(start, end));
            return windows;
        }
    }

    /// <summary>
    /// Whether one window of the stored states (<see cref="Windows"/>) holds <paramref name="window"/>, to the tick of TDB
    /// (decision 86 of #668): a window that meets a gap is not covered.
    /// </summary>
    internal bool Covers(in Window window)
    {
        var start = window.StartDate.ToTdb();
        var end = window.EndDate.ToTdb();
        foreach (var w in Windows)
        {
            if ((start - w.StartDate.ToTdb()).Ticks >= 0 && (w.EndDate.ToTdb() - end).Ticks >= 0) return true;
        }

        return false;
    }

    /// <summary>
    /// The ticks a read may lie outside an arc for a margin in seconds, the light time of a corrected read (#527): the
    /// margin rounded up, and one tick more, the resolution of a <see cref="Time"/> a corrected epoch is rounded to.
    /// </summary>
    internal static long Slack(double margin) =>
        (long)System.Math.Ceiling(margin * TimeSpan.TicksPerSecond) + (margin > 0.0 ? 1 : 0);

    /// <summary>
    /// The Lagrange interpolation of the 8 states nearest to <paramref name="epoch"/> in the arc that reads it, null when
    /// none does (decisions 85, 87, 90 and 91 of #668, decided by S. Guillet, 2026-11-23). An epoch inside an arc (from its
    /// first epoch to its last) is read by that arc, the later one at an impulse, as before, bit for bit (91). Outside every
    /// arc, an arc of two states or more reads an epoch within it widened by <paramref name="margin"/> seconds on each side
    /// (<see cref="Slack"/>, 85), and the nearest arc that reads it does, the later one on a tie (91), so that a corrected
    /// read in a gap within the margin of two arcs reads the nearer. An arc of one state apart from the others reads its own
    /// epoch only (87); the arc of one state that an impulse at the end of a propagation opens (#575), which touches the arc
    /// before it at the impulse, also reads within the margin past it: the state after the impulse, as before (90),
    /// the tie with the arc before it, at the same distance, going to it. A read in a gap outside every widened arc, or
    /// beside an arc of one state apart from the others, gives null.
    /// </summary>
    internal StateVector InterpolateWithin(in Time epoch, double margin)
    {
        var snapshot = _snapshot;
        var arcs = snapshot.Arcs;
        if (arcs.Length == 0) return null;
        var t = epoch.ToTdb();
        int k = snapshot.ArcIndex(epoch);
        if (!Holds(arcs[k], t))
        {
            long slack = Slack(margin);
            k = -1;
            long nearest = long.MaxValue;
            for (int j = 0; j < arcs.Length; j++)
            {
                if (Reads(arcs, j, t, slack, out long distance) && distance <= nearest)
                {
                    k = j;
                    nearest = distance;
                }
            }

            if (k < 0) return null;
        }

        return Lagrange.InterpolateSorted(arcs[k], epoch);
    }

    // Whether a TDB epoch lies within an arc, its ends included
    private static bool Holds(StateVector[] arc, in Time tdb) =>
        (arc[0].Epoch.ToTdb() - tdb).Ticks <= 0 && (tdb - arc[^1].Epoch.ToTdb()).Ticks <= 0;

    // Whether arc j reads a TDB epoch within slack ticks, and the ticks from its hull: an arc of one state its own epoch, and
    // past it within slack when it touches the arc before it (the arc an impulse at the end of a propagation opens, #575)
    private static bool Reads(StateVector[][] arcs, int j, in Time tdb, long slack, out long distance)
    {
        var arc = arcs[j];
        long before = (arc[0].Epoch.ToTdb() - tdb).Ticks;
        long after = (tdb - arc[^1].Epoch.ToTdb()).Ticks;
        distance = System.Math.Max(0L, System.Math.Max(before, after));
        if (arc.Length == 1)
        {
            bool touches = j > 0 && arcs[j - 1][^1].Epoch == arc[0].Epoch;
            return before == 0 && after == 0 || touches && before <= 0 && after <= slack;
        }

        return distance <= slack;
    }

    /// <summary>
    /// The Lagrange interpolation of the 8 states nearest to <paramref name="epoch"/> in the arc that holds it, the later
    /// arc at an impulse.
    /// </summary>
    /// <exception cref="InvalidOperationException">No state is stored.</exception>
    internal StateVector Interpolate(in Time epoch)
    {
        var snapshot = _snapshot;
        if (snapshot.Arcs.Length == 0)
            throw new InvalidOperationException("No propagated state is stored.");
        return Lagrange.InterpolateSorted(snapshot.Arcs[snapshot.ArcIndex(epoch)], epoch);
    }
}
