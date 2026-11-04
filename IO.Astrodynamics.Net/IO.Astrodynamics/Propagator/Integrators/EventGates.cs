// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// The gates of the detectors of a segment (#569): a gated detector (<see cref="IGatedEventDetector"/>) reports no
/// crossing before its gate, and its first crossing is searched from the integrated state at its gate.
/// </summary>
/// <remarks>
/// <para>
/// In the accepted step that holds a gate, the g-value at the gate replaces the previous g-value of its detector, the
/// value <see cref="IEventDetector.Evaluate"/> returned at the previous step end, before the gate; the integrator then
/// compares it with the g-value at the step end, as for any step. The step [t0, t1] holds the gate t_m when
/// t0 &lt; t_m ≤ t1.
/// </para>
/// <para>
/// A g-value exactly zero at the gate is a crossing at the gate when the sign that follows it is the target sign: inside a
/// step, the sign of the step end; when the gate is a step end or the segment base, where the g-value there is zero too,
/// the sign of the next step end.
/// </para>
/// <para>
/// No gate is created for a detector whose gate is open at the segment base, nor for the detector of the event just
/// handled (an <see cref="EventAction.Continue"/>, the integrator's skip of its root): that event decided its gate. A
/// segment with neither a closed gate nor a zero at a gate equal to its base has no <see cref="EventGates"/> and is
/// integrated as before #569; so is every step that does not hold a gate.
/// </para>
/// </remarks>
internal sealed class EventGates
{
    private readonly IReadOnlyList<IEventDetector> _detectors;

    // The gate of each detector still closed, default otherwise; the count of closed gates
    private readonly Time[] _gates;
    private readonly bool[] _closed;
    private int _closedCount;

    // A zero at a gate that the next step end judges: the gate at the base, or at the end of the last step
    private readonly bool[] _pendingZero;
    private bool _anyPendingZero;

    // The gates that opened in the last accepted step: their time, and whether the g-value there is zero
    private readonly double[] _gateTime;
    private readonly bool[] _zeroAtGate;
    private readonly bool[] _openedInLastStep;
    private bool _anyOpenedInLastStep;

    private EventGates(IReadOnlyList<IEventDetector> detectors)
    {
        int count = detectors.Count;
        _detectors = detectors;
        _gates = new Time[count];
        _closed = new bool[count];
        _pendingZero = new bool[count];
        _gateTime = new double[count];
        _zeroAtGate = new bool[count];
        _openedInLastStep = new bool[count];
    }

    /// <summary>
    /// The gates of <paramref name="detectors"/> still closed at <paramref name="baseEpoch"/>, and the zeros at a gate equal
    /// to it; null when there is none, and the segment is then integrated as before #569.
    /// </summary>
    /// <param name="detectors">The detectors of the segment.</param>
    /// <param name="baseEpoch">The base epoch of the segment.</param>
    /// <param name="prevG">The g-values of the detectors at the base.</param>
    /// <param name="justHandled">The detector of the event just handled, whose root the segment starts at, or null.</param>
    internal static EventGates Create(IReadOnlyList<IEventDetector> detectors, in Time baseEpoch, double[] prevG,
        IEventDetector justHandled)
    {
        if (detectors == null || prevG == null) return null;

        EventGates gates = null;
        for (int i = 0; i < detectors.Count; i++)
        {
            if (detectors[i] is not IGatedEventDetector gated || !gated.IsActive ||
                ReferenceEquals(detectors[i], justHandled)) continue;

            var gate = gated.Gate;
            if (baseEpoch < gate)
            {
                gates ??= new EventGates(detectors);
                gates._gates[i] = gate;
                gates._closed[i] = true;
                gates._closedCount++;
            }
            else if (!(gate < baseEpoch) && prevG[i] == 0.0)
            {
                // The gate at the base, and g zero there: a crossing at the base when the first step end has the target
                // sign
                gates ??= new EventGates(detectors);
                gates._pendingZero[i] = true;
                gates._anyPendingZero = true;
            }
        }

        return gates;
    }

    /// <summary>
    /// After the accepted <paramref name="step"/>, before the event check: for each gate that opens in it, its time in the
    /// step and the g-value there, which becomes the previous g-value of its detector; and the zeros at a gate at the start
    /// of the step, judged by its end.
    /// </summary>
    /// <param name="step">The accepted step.</param>
    /// <param name="baseEpoch">The base epoch of the segment.</param>
    /// <param name="endEpoch">The epoch of the end of the step.</param>
    /// <param name="prevG">The previous g-values of the detectors, updated.</param>
    /// <param name="integrator">The integrator, for the time argument and the state inside the step.</param>
    internal void AfterStep(in AcceptedStep step, in Time baseEpoch, in Time endEpoch, double[] prevG,
        Integrator integrator)
    {
        if (_anyOpenedInLastStep)
        {
            Array.Clear(_openedInLastStep);
            _anyOpenedInLastStep = false;
        }

        if (_anyPendingZero) JudgePendingZeros(step, endEpoch, prevG, integrator);

        if (_closedCount == 0) return;

        double t0 = step.CumulativeTime;
        double t1 = t0 + step.StepSize;
        for (int i = 0; i < _closed.Length; i++)
        {
            if (!_closed[i] || endEpoch < _gates[i]) continue;

            // The gate opens in this step: its time on the epoch grid, so that its epoch is the gate itself
            _closed[i] = false;
            _closedCount--;
            var detector = (IGatedEventDetector)_detectors[i];
            double tm = integrator.TimeArgument.OnEpochGrid(baseEpoch, _gates[i].ToTDB(), t0, t1);
            double g = detector.EvaluateOpen(integrator.StateInsideAcceptedStep(step, baseEpoch, tm));
            bool zero = g == 0.0;
            if (zero && tm >= t1)
            {
                // The gate on the step end: when the step end reads zero too, the next step end decides
                _pendingZero[i] = true;
                _anyPendingZero = true;
            }

            Open(i, tm, zero);
            prevG[i] = zero ? SignBeforeCrossing(i, step, endEpoch, integrator) : g;
        }
    }

    /// <summary>
    /// Whether the gate of detector <paramref name="index"/> opened in the last accepted step, with its time in seconds of
    /// the time argument from the segment base and whether the g-value there is zero.
    /// </summary>
    internal bool OpenedInLastStep(int index, out double gateTime, out bool zeroAtGate)
    {
        bool opened = _anyOpenedInLastStep && _openedInLastStep[index];
        gateTime = opened ? _gateTime[index] : double.NaN;
        zeroAtGate = opened && _zeroAtGate[index];
        return opened;
    }

    // A zero at a gate at the start of this step (the base, or the end of the last step): a crossing there when the end of
    // this step has the target sign. A non-zero g at the start of the step is the integrator's own, from the last check
    private void JudgePendingZeros(in AcceptedStep step, in Time endEpoch, double[] prevG, Integrator integrator)
    {
        _anyPendingZero = false;
        for (int i = 0; i < _pendingZero.Length; i++)
        {
            if (!_pendingZero[i]) continue;
            _pendingZero[i] = false;
            if (prevG[i] != 0.0) continue;
            Open(i, step.CumulativeTime, true);
            prevG[i] = SignBeforeCrossing(i, step, endEpoch, integrator);
        }
    }

    private void Open(int index, double gateTime, bool zeroAtGate)
    {
        _gateTime[index] = gateTime;
        _zeroAtGate[index] = zeroAtGate;
        _openedInLastStep[index] = true;
        _anyOpenedInLastStep = true;
    }

    // The previous g-value that makes a zero at the gate a crossing when the step end has the target sign: the sign before
    // the crossing of the detector's direction; for a crossing in either direction, the sign opposite to that of the step
    // end, 0 when the step end is zero too
    private double SignBeforeCrossing(int index, in AcceptedStep step, in Time endEpoch, Integrator integrator)
    {
        var detector = (IGatedEventDetector)_detectors[index];
        switch (detector.Direction)
        {
            case CrossingDirection.PositiveToNegative:
                return 1.0;
            case CrossingDirection.NegativeToPositive:
                return -1.0;
            default:
                var end = integrator.StateAt(step.EndPosition, step.EndVelocity, endEpoch);
                return -System.Math.Sign(detector.EvaluateOpen(end));
        }
    }
}
