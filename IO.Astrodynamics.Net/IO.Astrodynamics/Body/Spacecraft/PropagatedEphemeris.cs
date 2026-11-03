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
/// event), or between two impulses and not longer than <see cref="SameEpochTolerance"/>, is not stored; an impulse that no
/// segment of the solution carries (located at the end of the propagation) closes the last arc. At an impulse's
/// epoch the reads take the later arc: the state after the impulse, as <see cref="PropagationSolution.InterpolateAt"/>,
/// SPICE (the later segment of an SPK) and GMAT give it.
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
    /// <summary>Two impulses within 2 µs are one epoch (the same-epoch rule of the attitude record): no arc between them.</summary>
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
    /// segment of the solution carries it (at the end of the propagation): the states end with the state before it.</param>
    /// <param name="fill">The state of the solution at an epoch inside an arc, in ICRF, with its covariance in covariance
    /// mode, for the short arcs; null for no fill.</param>
    internal void Set(IReadOnlyList<StateVector> outputs, IReadOnlyList<ImpulseBoundary> impulses, Func<Time, StateVector> fill)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(impulses);
        var arcs = new List<StateVector[]>(impulses.Count + 1);
        int o = 0;
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
            if (bounded && (span <= 0.0 || opened && closed && span <= SameEpochTolerance)) continue;

            if (bounded && arc.Count < MinimumArcStates && fill != null && span >= (FilledArcStates - 1) * MinimumFillSpacing)
            {
                arc = Fill(arc[0], arc[^1], fill);
            }

            arcs.Add(arc.ToArray());
        }

        lock (_writeLock)
        {
            _snapshot = new Snapshot(arcs.ToArray());
        }
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
    /// Adds states, each to the arc that holds its epoch (the later one at an impulse, the first one before every arc, the
    /// last one after), replacing a state of that arc at the same epoch; with no arc, they make one.
    /// </summary>
    internal void Add(IEnumerable<StateVector> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        lock (_writeLock)
        {
            var snapshot = _snapshot;
            var arcs = snapshot.Arcs.Select(a => a.ToList()).ToList();
            foreach (var sv in states)
            {
                if (arcs.Count == 0)
                {
                    arcs.Add(new List<StateVector> { sv });
                    continue;
                }

                var arc = arcs[ArcIndex(arcs, sv.Epoch)];
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

            _snapshot = new Snapshot(arcs.Select(a => a.ToArray()).ToArray());
        }
    }

    // The arc of an epoch among arcs being built: the last one that starts at or before it, the first one before every arc
    private static int ArcIndex(List<List<StateVector>> arcs, in Time epoch)
    {
        for (int k = arcs.Count - 1; k > 0; k--)
        {
            if (arcs[k][0].Epoch <= epoch) return k;
        }

        return 0;
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
