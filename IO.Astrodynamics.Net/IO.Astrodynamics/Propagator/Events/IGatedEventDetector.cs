// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator.Events;

/// <summary>
/// An event detector closed before an epoch, its gate (#569): it reports no crossing before the gate, and its first
/// crossing is searched from the state at the gate. <see cref="Integrators.VVIntegrator"/> and
/// <see cref="Integrators.RK78Integrator"/> evaluate its g-function at the gate, in the step that holds it, instead of
/// comparing the first g-value after the gate with the value <see cref="IEventDetector.Evaluate"/> returns before it.
/// </summary>
internal interface IGatedEventDetector : IEventDetector
{
    /// <summary>
    /// The epoch before which the detector reports no crossing. It is open at an epoch that does not compare before it.
    /// </summary>
    Time Gate { get; }

    /// <summary>
    /// The g-function at <paramref name="state"/>, whatever its epoch: at the gate, the start of the search for the
    /// crossing, then at the states of that search. The first evaluation may decide which crossing the search looks for
    /// (the node of <see cref="Maneuver.PlaneAlignmentManeuver"/>).
    /// </summary>
    double EvaluateOpen(StateVector state);
}
