// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Complete propagation trajectory spanning multiple segments.
/// At maneuver boundaries, returns the post-maneuver state for epochs at or after the event.
/// </summary>
public sealed class PropagationSolution
{
    private readonly List<PropagationSegment> _segments = new();
    private StateVector[] _stateVectors = Array.Empty<StateVector>();

    /// <summary>
    /// Ordered propagation segments.
    /// </summary>
    public IReadOnlyList<PropagationSegment> Segments => _segments;

    /// <summary>
    /// Pre-computed state vectors sampled at DeltaT intervals.
    /// </summary>
    public IReadOnlyList<StateVector> StateVectors => _stateVectors;

    /// <summary>
    /// The dynamics of the propagation, kept in RK7(8) for the evaluations of the variational equations after it; null
    /// for another integrator.
    /// </summary>
    internal PropagationDynamics Dynamics { get; set; }

    /// <summary>Observer of the propagated states, set by the propagator; null for a solution built by hand.</summary>
    internal ILocalizable Observer { get; set; }

    /// <summary>Frame of the propagated states, set by the propagator; null for a solution built by hand.</summary>
    internal Frame Frame { get; set; }

    /// <summary>
    /// The options of the propagation of the state transition matrix and of the covariance; null when the propagation
    /// did not integrate them (<see cref="CovariancePropagationOptions"/>).
    /// </summary>
    public CovariancePropagationOptions CovariancePropagation { get; internal set; }

    /// <summary>
    /// The force parameters of the columns of the sensitivity Ψ (<see cref="GetParameterSensitivity"/>), in their order:
    /// Cd with atmospheric drag, then Cr with a radiation force. Empty when the propagation did not integrate the
    /// variational equations, or has none of these forces.
    /// </summary>
    public IReadOnlyList<ForceParameter> SensitivityParameters =>
        ParametersOf(Dynamics?.Options?.Parameters ?? ForceParameters.None);

    /// <summary>
    /// The consider parameters, in the order of the rows and columns 7 and after of the extended covariance
    /// (<see cref="GetExtendedCovariance"/>): Cd, then Cr. Empty without the covariance or without consider parameters.
    /// </summary>
    public IReadOnlyList<ForceParameter> ConsiderParameters =>
        ParametersOf(Dynamics?.Covariance != null
            ? Dynamics.Options.ConsiderParameters?.Parameters ?? ForceParameters.None
            : ForceParameters.None);

    /// <summary>
    /// Set the sampled output state vectors.
    /// </summary>
    public void SetOutputStates(StateVector[] states)
    {
        _stateVectors = states ?? throw new ArgumentNullException(nameof(states));
    }

    /// <summary>
    /// Add a completed segment to the solution.
    /// </summary>
    public void AddSegment(PropagationSegment segment)
    {
        if (segment == null) throw new ArgumentNullException(nameof(segment));
        _segments.Add(segment);
    }

    /// <summary>
    /// Position and velocity at the given epoch, in the frame of the propagation and relative to its observer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For an RK7(8) propagation, the state is computed by a shortened RK7(8) step from the start of the accepted step
    /// that contains the epoch, so it has the accuracy of the integrator; at a step boundary, the stored state is
    /// returned. Inside a step this costs 13 evaluations of the forces, which run under a lock of the solution: like the
    /// propagation itself, it must not run concurrently with another propagation or solution that shares a
    /// <see cref="IO.Astrodynamics.Body.CelestialBody"/> with a geopotential.
    /// </para>
    /// <para>
    /// For another integrator, the state is the cubic Hermite interpolation of the accepted step
    /// (<see cref="PropagationSegment.InterpolateAt"/>).
    /// </para>
    /// <para>
    /// At a maneuver, the state after the maneuver is returned for epochs at or after it. Before the solution, its
    /// first state; after it, its last state.
    /// </para>
    /// </remarks>
    public (Vector3 position, Vector3 velocity) InterpolateAt(Time epoch)
    {
        var segment = SegmentAt(epoch, out double t);
        return Dynamics != null ? Dynamics.StateAt(segment, t) : segment.InterpolateAt(t);
    }

    /// <summary>
    /// The state transition matrix Φ(t, t0) at <paramref name="epoch"/>, from the start of the propagation: the
    /// derivative of the state at t with respect to the initial state, 6×6, rows and columns ordered x, y, z, vx, vy, vz.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Units: the position-position and velocity-velocity blocks are dimensionless, the position-velocity block (∂r/∂v0)
    /// is in s, the velocity-position block (∂v/∂r0) in 1/s. Inertial frame of the propagation, relative to its central
    /// body.
    /// </para>
    /// <para>
    /// Inside an integration step, the value comes from a shortened RK7(8) step from the start of that step, at the
    /// accuracy of the integrator; at a step boundary, the stored value. At a maneuver, the value after it: the maneuver is
    /// open loop, so Φ is continuous there. The evaluation runs under the lock of the solution; it evaluates the forces,
    /// so it must not run concurrently with a propagation or an evaluation sharing a
    /// <see cref="IO.Astrodynamics.Body.CelestialBody"/> with a geopotential.
    /// </para>
    /// </remarks>
    /// <param name="epoch">The epoch, within the propagation; up to one tick of <see cref="Time"/> (100 ns) after its
    /// end, which reads the values at the end.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epoch"/> is outside the propagation.</exception>
    /// <exception cref="InvalidOperationException">The propagation did not integrate the state transition matrix
    /// (<see cref="CovariancePropagationOptions"/>).</exception>
    public Matrix GetStateTransitionMatrix(Time epoch)
    {
        var y = EvaluateY(epoch, out int columns);
        var phi = new Matrix(VariationalEquations.StateDimension, VariationalEquations.StateDimension);
        for (int i = 0; i < VariationalEquations.StateDimension; i++)
        {
            for (int j = 0; j < VariationalEquations.StateDimension; j++)
            {
                phi.Set(i, j, y[i * columns + j]);
            }
        }

        return phi;
    }

    /// <summary>
    /// The sensitivity Ψ(t) of the state at <paramref name="epoch"/> to the force parameters, 6×p, one column per
    /// parameter in the order of <see cref="SensitivityParameters"/>: the derivative of the state with respect to Cd and
    /// Cr, the initial state held.
    /// </summary>
    /// <remarks>
    /// Units: the position rows in m, the velocity rows in m/s, per unit of the dimensionless coefficient. Evaluated as
    /// <see cref="GetStateTransitionMatrix"/>.
    /// </remarks>
    /// <param name="epoch">The epoch, within the propagation; up to one tick of <see cref="Time"/> (100 ns) after its
    /// end, which reads the values at the end.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epoch"/> is outside the propagation.</exception>
    /// <exception cref="InvalidOperationException">The propagation did not integrate the state transition matrix, or
    /// has no force with a parameter (no drag, no radiation force).</exception>
    public Matrix GetParameterSensitivity(Time epoch)
    {
        CheckWithin(epoch);
        if (Dynamics?.Options != null && Dynamics.ColumnCount == VariationalEquations.StateDimension)
            throw new InvalidOperationException(
                "The propagation has no force parameter: Ψ has a column for Cd with atmospheric drag and for Cr with a " +
                "radiation force.");

        var y = EvaluateY(epoch, out int columns);
        int p = columns - VariationalEquations.StateDimension;
        var psi = new Matrix(VariationalEquations.StateDimension, p);
        for (int i = 0; i < VariationalEquations.StateDimension; i++)
        {
            for (int j = 0; j < p; j++)
            {
                psi.Set(i, j, y[i * columns + VariationalEquations.StateDimension + j]);
            }
        }

        return psi;
    }

    /// <summary>
    /// The propagated covariance P(t) = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q(t) at <paramref name="epoch"/>, symmetrized, 6×6, rows and
    /// columns ordered x, y, z, vx, vy, vz.
    /// </summary>
    /// <remarks>
    /// Units: m², m²/s, m²/s². Inertial frame of the propagation, relative to its central body. At a maneuver, the value
    /// after it, its execution error included. Evaluated as <see cref="GetStateTransitionMatrix"/>.
    /// </remarks>
    /// <param name="epoch">The epoch, within the propagation; up to one tick of <see cref="Time"/> (100 ns) after its
    /// end, which reads the values at the end.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epoch"/> is outside the propagation.</exception>
    /// <exception cref="InvalidOperationException">The propagation did not propagate the covariance
    /// (<see cref="CovariancePropagationMode.Covariance"/>).</exception>
    public Matrix GetCovariance(Time epoch)
    {
        var covariance = new double[VariationalEquations.StateDimension * VariationalEquations.StateDimension];
        EvaluateCovariance(epoch, covariance);
        return new Matrix(VariationalEquations.StateDimension, VariationalEquations.StateDimension, covariance);
    }

    /// <summary>
    /// The extended covariance of the state and of the consider parameters at <paramref name="epoch"/>,
    /// [[P, Ψc Pp], [Pp Ψcᵀ, Pp]], (6 + c)×(6 + c), the consider parameters in the order of
    /// <see cref="ConsiderParameters"/>, constant.
    /// </summary>
    /// <remarks>
    /// Units: P in m², m²/s, m²/s²; the state-parameter blocks in m and m/s; the parameter block dimensionless.
    /// Evaluated as <see cref="GetStateTransitionMatrix"/>.
    /// </remarks>
    /// <param name="epoch">The epoch, within the propagation; up to one tick of <see cref="Time"/> (100 ns) after its
    /// end, which reads the values at the end.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epoch"/> is outside the propagation.</exception>
    /// <exception cref="InvalidOperationException">The propagation did not propagate the covariance, or has no consider
    /// parameter.</exception>
    public Matrix GetExtendedCovariance(Time epoch)
    {
        CheckWithin(epoch);
        var model = Dynamics?.Covariance;
        if (model != null && model.ConsiderCount == 0)
            throw new InvalidOperationException(
                "The propagation has no consider parameter: the extended covariance is P itself (GetCovariance).");

        int m = model?.ExtendedDimension ?? 0;
        var covariance = new double[m * m];
        EvaluateExtendedCovariance(epoch, covariance);
        return new Matrix(m, m, covariance);
    }

    /// <summary>
    /// The state at <paramref name="epoch"/>, as <see cref="InterpolateAt"/> computes it, in the frame of the propagation
    /// and relative to its observer; with its covariance (<see cref="GetCovariance"/>) when the propagation propagated
    /// it, without otherwise.
    /// </summary>
    /// <param name="epoch">The epoch, within the propagation; up to one tick of <see cref="Time"/> (100 ns) after its
    /// end, which reads the values at the end.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epoch"/> is outside the propagation.</exception>
    /// <exception cref="InvalidOperationException">The solution was not built by a propagator: it has no observer and
    /// frame.</exception>
    public StateVector GetStateVector(Time epoch)
    {
        CheckWithin(epoch);
        if (Observer == null || Frame == null)
            throw new InvalidOperationException(
                "The solution was not built by a propagator: the observer and frame of its states are unknown.");

        if (Dynamics?.Covariance != null)
            return StateAndCovarianceAt(epoch);

        var (position, velocity) = InterpolateAt(epoch);
        return new StateVector(position, velocity, Observer, epoch, Frame);
    }

    /// <summary>
    /// The state at <paramref name="epoch"/> with its covariance, from one shortened step: the state is that of
    /// <see cref="InterpolateAt"/>, bit for bit, including its clamping at the ends of the solution, so that the last
    /// output epoch never falls outside by the rounding of the step sizes. The output states of a propagation of the
    /// covariance.
    /// </summary>
    /// <exception cref="InvalidOperationException">The propagation did not propagate the covariance.</exception>
    internal StateVector StateAndCovarianceAt(Time epoch)
    {
        var segment = SegmentAt(epoch, out double t);
        if (Dynamics == null)
            throw new InvalidOperationException(
                "The solution holds no covariance: the propagation did not propagate the covariance.");

        var covariance = new double[VariationalEquations.StateDimension * VariationalEquations.StateDimension];
        var (position, velocity) = Dynamics.StateAndCovarianceAt(segment, t, covariance);
        return new StateVector(position, velocity, Observer, epoch, Frame,
            new Matrix(VariationalEquations.StateDimension, VariationalEquations.StateDimension, covariance));
    }

    // The cumulative Y at an epoch, in a buffer of its size
    private double[] EvaluateY(in Time epoch, out int columns)
    {
        var segment = VariationalSegmentAt(epoch, out double t);
        if (Dynamics.Options == null)
            throw new InvalidOperationException(
                "The solution holds no variational data: the propagation did not integrate the variational equations.");

        columns = Dynamics.ColumnCount;
        var y = new double[VariationalEquations.StateDimension * columns];
        var q = new double[Dynamics.CarriedCovarianceLength];
        Dynamics.Evaluate(segment, t, y, q);
        return y;
    }

    // The force parameters of a set of flags, Cd first
    private static IReadOnlyList<ForceParameter> ParametersOf(ForceParameters parameters)
    {
        var list = new List<ForceParameter>(2);
        if ((parameters & ForceParameters.DragCoefficient) != 0)
            list.Add(ForceParameter.DragCoefficient);
        if ((parameters & ForceParameters.ReflectivityCoefficient) != 0)
            list.Add(ForceParameter.ReflectivityCoefficient);
        return list.AsReadOnly();
    }

    /// <summary>
    /// The cumulative variational values at <paramref name="epoch"/>, from the start of the propagation: Y = [Φ | Ψ] and
    /// Q (<see cref="VariationalEquations"/>). Picks the segment as <see cref="InterpolateAt"/> does, the later one at a
    /// maneuver.
    /// </summary>
    /// <param name="epoch">The epoch, within the solution; up to one tick of <see cref="Time"/> (100 ns) after its end,
    /// which reads the values at the end.</param>
    /// <param name="y">Y, 6n values.</param>
    /// <param name="q">Q, 21 values when it is carried (process noise or covariance), empty otherwise.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epoch"/> is outside the solution.</exception>
    /// <exception cref="InvalidOperationException">The propagation did not integrate the variational equations.</exception>
    /// <exception cref="ArgumentException"><paramref name="y"/> or <paramref name="q"/> does not have the length of the
    /// propagation's options.</exception>
    internal void EvaluateVariational(Time epoch, Span<double> y, Span<double> q)
    {
        var segment = VariationalSegmentAt(epoch, out double t);
        Dynamics.Evaluate(segment, t, y, q);
    }

    /// <summary>
    /// The propagated covariance at <paramref name="epoch"/>, P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q, symmetrized, in the frame of
    /// the propagation (m², m²/s, m²/s²; <see cref="CovarianceModel"/>). Picks the segment as
    /// <see cref="InterpolateAt"/> does: at a maneuver, the covariance after it, its execution error included.
    /// </summary>
    /// <param name="epoch">The epoch, within the solution; up to one tick of <see cref="Time"/> (100 ns) after its end,
    /// which reads the values at the end.</param>
    /// <param name="covariance">P, 6×6 row-major.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epoch"/> is outside the solution.</exception>
    /// <exception cref="InvalidOperationException">The propagation did not propagate the covariance.</exception>
    /// <exception cref="ArgumentException"><paramref name="covariance"/> does not hold 36 values.</exception>
    internal void EvaluateCovariance(Time epoch, Span<double> covariance)
    {
        var segment = VariationalSegmentAt(epoch, out double t);
        Dynamics.EvaluateCovariance(segment, t, covariance, false);
    }

    /// <summary>
    /// The extended covariance of the state and the consider parameters at <paramref name="epoch"/>,
    /// [[P, Ψc Pp], [Pp Ψcᵀ, Pp]], m×m with m = 6 + the number of consider parameters (Cd, then Cr).
    /// </summary>
    /// <param name="epoch">The epoch, within the solution; up to one tick of <see cref="Time"/> (100 ns) after its end,
    /// which reads the values at the end.</param>
    /// <param name="covariance">The extended covariance, m×m row-major.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epoch"/> is outside the solution.</exception>
    /// <exception cref="InvalidOperationException">The propagation did not propagate the covariance.</exception>
    /// <exception cref="ArgumentException"><paramref name="covariance"/> does not hold m² values.</exception>
    internal void EvaluateExtendedCovariance(Time epoch, Span<double> covariance)
    {
        var segment = VariationalSegmentAt(epoch, out double t);
        Dynamics.EvaluateCovariance(segment, t, covariance, true);
    }

    // One tick of Time (100 ns): the propagation ends at Window.StartDate.AddSeconds(duration), which AddSeconds
    // truncates to the tick, so its last step can end one tick before the end of the window (H3 review of lots A to C)
    private const double EpochTick = 1e-7;

    // The segment of an epoch inside the solution, for the variational values; an epoch within one tick after the last
    // step reads the values at its end
    private PropagationSegment VariationalSegmentAt(in Time epoch, out double t)
    {
        CheckWithin(epoch);
        var segment = SegmentAt(epoch, out t);
        t = System.Math.Min(t, segment.Duration);
        if (Dynamics == null)
            throw new InvalidOperationException(
                "The solution holds no variational data: the propagation did not integrate the variational equations.");

        return segment;
    }

    // An epoch inside the propagated span, or within one tick after its end
    private void CheckWithin(in Time epoch)
    {
        if (_segments.Count == 0)
            throw new InvalidOperationException("Solution contains no segments.");

        var last = _segments[^1];
        if (epoch < _segments[0].BaseEpoch || (epoch - last.BaseEpoch).TotalSeconds > last.Duration + EpochTick)
            throw new ArgumentOutOfRangeException(nameof(epoch), epoch,
                "The epoch is outside the propagation.");
    }

    // The segment containing the epoch, the later one at boundaries (post-maneuver state), and the time from its start
    private PropagationSegment SegmentAt(in Time epoch, out double t)
    {
        if (_segments.Count == 0)
            throw new InvalidOperationException("Solution contains no segments.");

        for (int i = _segments.Count - 1; i >= 0; i--)
        {
            var segment = _segments[i];
            t = (epoch - segment.BaseEpoch).TotalSeconds;

            if (t >= 0.0)
            {
                return segment;
            }
        }

        // Before the first segment: the start of the first segment
        t = 0.0;
        return _segments[0];
    }
}
