// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// The force model of one numerical propagation, kept to propagate another spacecraft with it afterwards: the forces of
/// its integrator with their ephemeris cache, the settings of the integrator, the observer and frame of the integrated
/// state, the orientation cache, the lock under which the forces are evaluated, and the mass and coefficients of each
/// segment. The avoidance options of the conjunction assessment are propagated with it (step 10, E4; #402).
/// </summary>
/// <remarks>
/// Kept by the <see cref="PropagationSolution"/> of an RK7(8) or Velocity-Verlet propagation and by its spacecraft
/// until its next propagation (<see cref="Spacecraft.LastForceModel"/>), without the steps: about 0.2 MB for one day in
/// LEO with EGM2008 10×10, NRLMSISE-00, the Moon and the Sun. The forces are not reentrant (their buffers and those of
/// the geopotential): every propagation with them runs under <see cref="SyncRoot"/>, the lock of the dynamics of an
/// RK7(8) solution.
/// </remarks>
internal sealed class PropagationForceModel
{
    private readonly Integrator _integrator;
    private readonly Time[] _segmentEpochs;
    private readonly ForceEvaluationContext[] _segmentContexts;

    private PropagationForceModel(Integrator integrator, ILocalizable observer, Frame frame,
        PropagationFrameOrientationCache orientationCache, object syncRoot, Time[] segmentEpochs,
        ForceEvaluationContext[] segmentContexts, in Time end)
    {
        _integrator = integrator;
        Observer = observer;
        Frame = frame;
        OrientationCache = orientationCache;
        SyncRoot = syncRoot;
        _segmentEpochs = segmentEpochs;
        _segmentContexts = segmentContexts;
        Start = segmentEpochs[0];
        End = end;
    }

    /// <summary>Observer of the integrated state, the central body of the propagation.</summary>
    internal ILocalizable Observer { get; }

    /// <summary>Frame of the integrated state.</summary>
    internal Frame Frame { get; }

    /// <summary>The orientation cache of the propagation, or null.</summary>
    internal PropagationFrameOrientationCache OrientationCache { get; }

    /// <summary>The lock under which the forces are evaluated.</summary>
    internal object SyncRoot { get; }

    /// <summary>Start of the propagation.</summary>
    internal Time Start { get; }

    /// <summary>End of the propagation, which its ephemeris and orientation caches cover.</summary>
    internal Time End { get; }

    /// <summary>The forces, in their order.</summary>
    internal IReadOnlyCollection<ForceBase> Forces => _integrator.Forces;

    /// <summary>
    /// The force model of a propagation by RK7(8) or Velocity-Verlet, null for another integrator (its settings cannot be
    /// copied) or when no segment was integrated.
    /// </summary>
    /// <param name="integrator">The integrator of the propagation, with its forces.</param>
    /// <param name="solution">Its solution, whose segments carry their mass and coefficients.</param>
    /// <param name="initialState">The integrated initial state: its observer and frame.</param>
    /// <param name="orientationCache">The orientation cache of the propagation, or null.</param>
    /// <param name="syncRoot">The lock of the dynamics of the solution, or null for a new one.</param>
    internal static PropagationForceModel TryCreate(IIntegrator integrator, PropagationSolution solution,
        StateVector initialState, PropagationFrameOrientationCache orientationCache, object syncRoot)
    {
        if (integrator is not (RK78Integrator or VVIntegrator) || solution.Segments.Count == 0)
            return null;

        var epochs = new Time[solution.Segments.Count];
        var contexts = new ForceEvaluationContext[solution.Segments.Count];
        for (int i = 0; i < epochs.Length; i++)
        {
            var segment = solution.Segments[i];
            if (segment.Context == null)
                return null;

            epochs[i] = segment.BaseEpoch;
            contexts[i] = segment.Context.Value;
        }

        var last = solution.Segments[^1];
        return new PropagationForceModel((Integrator)integrator, initialState.Observer, initialState.Frame, orientationCache,
            syncRoot ?? new object(), epochs, contexts, last.BaseEpoch.AddSeconds(last.Duration));
    }

    /// <summary>Whether the propagation covers <paramref name="epoch"/>, its end excluded.</summary>
    internal bool Covers(in Time epoch) => epoch >= Start && epoch < End;

    /// <summary>
    /// The mass and coefficients at <paramref name="epoch"/>: those of the segment that holds it, the segment after a
    /// maneuver at that epoch.
    /// </summary>
    internal ForceEvaluationContext ContextAt(in Time epoch)
    {
        int index = 0;
        for (int i = 1; i < _segmentEpochs.Length && _segmentEpochs[i] <= epoch; i++)
        {
            index = i;
        }

        return _segmentContexts[index];
    }

    /// <summary>
    /// A new integrator with the settings of the propagation and its forces, which evaluates them with
    /// <paramref name="context"/> (#415). To be used under <see cref="SyncRoot"/>.
    /// </summary>
    internal Integrator NewIntegrator(in ForceEvaluationContext context)
    {
        var integrator = _integrator.WithSameSettings();
        foreach (var force in _integrator.Forces)
        {
            integrator.AddForce(force);
        }

        integrator.EvaluateForcesWith(context);
        return integrator;
    }
}
