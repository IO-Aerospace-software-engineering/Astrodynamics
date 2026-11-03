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
/// it, the output states strictly inside it and the state before the impulse that closes it, both at the impulse's epoch;
/// an output within <see cref="SameEpochTolerance"/> of an impulse is dropped, the impulse's state kept. An arc bounded by
/// an impulse and holding fewer than <see cref="MinimumArcStates"/> states takes <see cref="FilledArcStates"/> states of
/// the solution, evenly spaced over it, its two ends kept. An arc of zero length (an impulse at the end of the
/// propagation, two impulses at one event), or bounded by an impulse and not longer than <see cref="SameEpochTolerance"/>,
/// is not stored. At an impulse's epoch the reads take the later arc: the state
/// after the impulse, as <see cref="PropagationSolution.InterpolateAt"/>, SPICE (the later segment of an SPK) and GMAT
/// give it.
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
    /// <summary>An output within 2 µs of an impulse is the impulse's epoch (the same-epoch rule of the attitude record).</summary>
    internal const double SameEpochTolerance = 2.0e-6;

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
    /// <param name="impulses">The impulses, their states in ICRF, in epoch order.</param>
    /// <param name="fill">The state of the solution at an epoch inside an arc, in ICRF, with its covariance in covariance
    /// mode, for the short arcs; null for no fill.</param>
    internal void Set(IReadOnlyList<StateVector> outputs, IReadOnlyList<ImpulseBoundary> impulses, Func<Time, StateVector> fill)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(impulses);
        var arcs = new List<StateVector[]>(impulses.Count + 1);
        int o = 0;
        StateVector opening = null;
        for (int k = 0; k <= impulses.Count; k++)
        {
            var arc = new List<StateVector>();
            if (opening != null) arc.Add(opening);
            bool last = k == impulses.Count;
            for (; o < outputs.Count; o++)
            {
                var epoch = outputs[o].Epoch;
                // The outputs within the tolerance before the closing impulse, and after the opening one, are its epoch
                if (!last && epoch.AddSeconds(SameEpochTolerance) >= impulses[k].Epoch) break;
                if (opening != null && epoch <= opening.Epoch.AddSeconds(SameEpochTolerance)) continue;
                arc.Add(outputs[o]);
            }

            bool bounded = opening != null || !last;
            if (!last)
            {
                arc.Add(impulses[k].Before);
                opening = impulses[k].After;
            }

            if (arc.Count == 0) continue;

            // An arc of zero length between impulses holds no motion, and one shorter than the same-epoch tolerance is one
            // epoch: the reads and the SPK end where the solution does
            if (bounded && (arc[^1].Epoch - arc[0].Epoch).TotalSeconds <= SameEpochTolerance) continue;

            if (bounded && arc.Count < MinimumArcStates && fill != null)
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
