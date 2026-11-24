using System;
using System.Threading;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// A propagator of one spacecraft over one window. It holds no resource to release: <see cref="IDisposable"/> was removed
/// in 11.0.0 (R28 of #668), its <c>Dispose</c> being empty.
/// </summary>
/// <remarks>
/// Implemented by the library (<see cref="CentralBodyPropagator"/>, <see cref="TLEPropagator"/>) and by the subclasses of
/// <see cref="PropagatorBase"/>, which build their own solution. Since the constructor of <see cref="PropagationSolution"/>
/// is internal (decision 55 of #668, decided by S. Guillet, 2026-11-23), an implementation outside the library either
/// subclasses <see cref="PropagatorBase"/> or returns the solution of a library propagator it delegates to.
/// </remarks>
public interface IPropagator
{
    Window Window { get; }
    Spacecraft Spacecraft { get; }
    TimeSpan DeltaT { get; }

    /// <summary>
    /// Propagate spacecraft
    /// </summary>
    /// <param name="cancellationToken">Stops the propagation with an <see cref="OperationCanceledException"/> carrying
    /// it (R28 of #668); see each implementation for where it is read and what a cancelled run leaves.</param>
    /// <returns>The propagation solution containing segments and sampled state vectors</returns>
    PropagationSolution Propagate(CancellationToken cancellationToken = default);
}
