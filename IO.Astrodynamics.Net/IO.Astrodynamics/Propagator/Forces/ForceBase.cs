// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.OrbitalParameters;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Forces;

public abstract class ForceBase
{
    /// <summary>
    /// Relative step δ_r of the default central differences on the position: h_r = δ_r max(|r|, 1 m).
    /// </summary>
    /// <remarks>
    /// Chosen by the plateau study of phase 2, step 2 (<c>FiniteDifferenceStepStudyTests</c>, and the provenance matrix of
    /// the documentation): the worst error of the built-in forces, scaled by the point-mass block of the central body, is
    /// below 1e-9 from 3e-7 to 1e-5 and smallest at 3e-6, in line with the ε^(1/3) of a central difference.
    /// </remarks>
    internal const double DefaultPositionRelativeStep = 3e-6;

    /// <summary>
    /// Relative step δ_v of the default central differences on the velocity: h_v = δ_v max(|v|, 1 m/s).
    /// </summary>
    /// <remarks>
    /// Chosen by the plateau study of phase 2, step 2 (<c>FiniteDifferenceStepStudyTests</c>, and the provenance matrix of
    /// the documentation): the relative error of the drag velocity block is below 1e-9 from 1e-7 to 1e-4 and smallest
    /// at 3e-6.
    /// </remarks>
    internal const double DefaultVelocityRelativeStep = 3e-6;

    /// <summary>
    /// Relative step δ_p of the default central differences on a coefficient: h_p = δ_p max(|C|, 1).
    /// </summary>
    /// <remarks>
    /// The built-in forces are linear in their coefficient, so the central difference has no truncation error and the
    /// step only sets the rounding error, about ε / δ_p relative.
    /// </remarks>
    internal const double DefaultParameterRelativeStep = 1e-3;

    private StateVector _partialsWorkState;

    /// <summary>
    /// Optional ephemeris cache for avoiding repeated SPICE calls during propagation.
    /// Set by the propagator before integration begins.
    /// </summary>
    internal PropagationEphemerisCache EphemerisCache { get; set; }

    public abstract Vector3 Apply(StateVector stateVector);

    /// <summary>
    /// Acceleration with the spacecraft quantities read from <paramref name="context"/> instead of the spacecraft.
    /// </summary>
    /// <remarks>
    /// The default ignores the context and calls <see cref="Apply(StateVector)"/>: a force written outside the library
    /// depends on no context quantity as far as the partials are concerned. A built-in force that reads the mass or a
    /// coefficient overrides it, and its <see cref="Apply(StateVector)"/> calls it with
    /// <see cref="ForceEvaluationContext.FromSpacecraft"/>, so that both give the same <c>double</c>.
    /// </remarks>
    /// <param name="stateVector">The state, relative to the observer of the propagation.</param>
    /// <param name="context">Mass and coefficients.</param>
    /// <returns>The acceleration, in m/s².</returns>
    internal virtual Vector3 Apply(StateVector stateVector, in ForceEvaluationContext context)
    {
        return Apply(stateVector);
    }

    /// <summary>
    /// Whether the acceleration depends on the velocity. When false, the default state partials skip the velocity
    /// columns, which are zero.
    /// </summary>
    internal virtual bool DependsOnVelocity => true;

    /// <summary>
    /// The coefficients of the context the acceleration depends on.
    /// </summary>
    internal virtual ForceParameters Parameters => ForceParameters.None;

    /// <summary>
    /// Adds ∂a/∂r and ∂a/∂v at <paramref name="stateVector"/>, at its fixed epoch, to <paramref name="dadr"/> and
    /// <paramref name="dadv"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The blocks are 3×3, row-major (element (i, j) at index 3i + j is ∂a_i/∂r_j), in the frame of the state. They are
    /// added to the buffers, so a caller sums over the forces by passing the same buffers to each.
    /// </para>
    /// <para>
    /// Precondition: <paramref name="stateVector"/> is the state the integrator propagates, relative to the observer of
    /// the propagation and in its frame, the observer and frame of the ephemeris cache. The partials differentiate
    /// <see cref="Apply(StateVector, in ForceEvaluationContext)"/>, which shares that precondition: some forces read
    /// body positions relative to that observer (the third bodies), or apply the light time from it (SRP, albedo,
    /// thermal), so the same physical state re-expressed relative to another observer can give another acceleration
    /// and other partials.
    /// </para>
    /// </remarks>
    /// <param name="stateVector">The state, relative to the observer of the propagation.</param>
    /// <param name="context">Mass and coefficients.</param>
    /// <param name="dadr">∂a/∂r, 9 values, in 1/s².</param>
    /// <param name="dadv">∂a/∂v, 9 values, in 1/s.</param>
    /// <exception cref="ArgumentException">A buffer does not hold exactly 9 values.</exception>
    internal void AccumulateStatePartials(StateVector stateVector, in ForceEvaluationContext context,
        Span<double> dadr, Span<double> dadv)
    {
        ValidateLength(dadr, 9, nameof(dadr));
        ValidateLength(dadv, 9, nameof(dadv));
        AccumulateStatePartialsCore(stateVector, context, dadr, dadv);
    }

    /// <summary>
    /// Adds ∂a/∂Cd and ∂a/∂Cr at <paramref name="stateVector"/> to <paramref name="dadCd"/> and
    /// <paramref name="dadCr"/>. A coefficient missing from <see cref="Parameters"/> adds nothing.
    /// </summary>
    /// <param name="stateVector">The state, relative to the observer of the propagation.</param>
    /// <param name="context">Mass and coefficients.</param>
    /// <param name="dadCd">∂a/∂Cd, 3 values, in m/s².</param>
    /// <param name="dadCr">∂a/∂Cr, 3 values, in m/s².</param>
    /// <exception cref="ArgumentException">A buffer does not hold exactly 3 values.</exception>
    internal void AccumulateParameterPartials(StateVector stateVector, in ForceEvaluationContext context,
        Span<double> dadCd, Span<double> dadCr)
    {
        ValidateLength(dadCd, 3, nameof(dadCd));
        ValidateLength(dadCr, 3, nameof(dadCr));
        AccumulateParameterPartialsCore(stateVector, context, dadCd, dadCr);
    }

    /// <summary>
    /// The default state partials with given relative steps, whatever the force overrides. Used by the step study and
    /// by the tests.
    /// </summary>
    /// <param name="stateVector">The state, relative to the observer of the propagation.</param>
    /// <param name="context">Mass and coefficients.</param>
    /// <param name="dadr">∂a/∂r, 9 values, in 1/s².</param>
    /// <param name="dadv">∂a/∂v, 9 values, in 1/s.</param>
    /// <param name="positionRelativeStep">δ_r.</param>
    /// <param name="velocityRelativeStep">δ_v.</param>
    /// <exception cref="ArgumentException">A buffer does not hold exactly 9 values.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A step is not positive.</exception>
    internal void AccumulateStatePartialsByCentralDifferences(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv, double positionRelativeStep,
        double velocityRelativeStep)
    {
        ValidateLength(dadr, 9, nameof(dadr));
        ValidateLength(dadv, 9, nameof(dadv));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(positionRelativeStep);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(velocityRelativeStep);
        CentralDifferenceStatePartials(stateVector, context, dadr, dadv, positionRelativeStep, velocityRelativeStep);
    }

    /// <summary>
    /// Adds the state partials. The default is <see cref="CentralDifferenceStatePartials"/> with the default steps;
    /// a force with analytic partials overrides it.
    /// </summary>
    private protected virtual void AccumulateStatePartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv)
    {
        CentralDifferenceStatePartials(stateVector, context, dadr, dadv, DefaultPositionRelativeStep,
            DefaultVelocityRelativeStep);
    }

    /// <summary>
    /// Adds the parameter partials. The default is a central difference on each coefficient of
    /// <see cref="Parameters"/> in the context.
    /// </summary>
    private protected virtual void AccumulateParameterPartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadCd, Span<double> dadCr)
    {
        if ((Parameters & ForceParameters.DragCoefficient) != 0)
        {
            var (plus, minus, step) = PerturbedCoefficients(context.DragCoefficient);
            AddDifference(dadCd, Apply(stateVector, context with { DragCoefficient = plus }),
                Apply(stateVector, context with { DragCoefficient = minus }), step);
        }

        if ((Parameters & ForceParameters.ReflectivityCoefficient) != 0)
        {
            var (plus, minus, step) = PerturbedCoefficients(context.ReflectivityCoefficient);
            AddDifference(dadCr, Apply(stateVector, context with { ReflectivityCoefficient = plus }),
                Apply(stateVector, context with { ReflectivityCoefficient = minus }), step);
        }
    }

    /// <summary>
    /// Central differences of <see cref="Apply(StateVector, in ForceEvaluationContext)"/> on each position component,
    /// then on each velocity component when <see cref="DependsOnVelocity"/>, at the fixed epoch of the state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Column j of ∂a/∂r is (a(r + h e_j) − a(r − h e_j)) / ((r_j + h) − (r_j − h)). Dividing by the difference of the
    /// perturbed components as they are stored, rather than by 2h, removes the representation error of the step.
    /// Reference: Press, Teukolsky, Vetterling and Flannery, Numerical Recipes, 3rd edition (2007), section 5.7,
    /// confirmed by S. Guillet (2026-10-07).
    /// </para>
    /// <para>
    /// The perturbed states go through a work state vector kept by the force and updated in place, so the differences
    /// allocate nothing beyond what <see cref="Apply(StateVector, in ForceEvaluationContext)"/> itself allocates. A
    /// force is therefore not thread-safe, as the integrator that owns it.
    /// </para>
    /// </remarks>
    private void CentralDifferenceStatePartials(StateVector stateVector, in ForceEvaluationContext context,
        Span<double> dadr, Span<double> dadv, double positionRelativeStep, double velocityRelativeStep)
    {
        var position = stateVector.Position;
        var velocity = stateVector.Velocity;
        var work = GetPartialsWorkState(stateVector);
        work.UpdateEpoch(stateVector.Epoch);

        double positionStep = positionRelativeStep * System.Math.Max(position.Magnitude(), 1.0);
        work.UpdateVelocity(velocity);
        for (int j = 0; j < 3; j++)
        {
            double component = Component(position, j);
            double plus = component + positionStep;
            double minus = component - positionStep;
            work.UpdatePosition(WithComponent(position, j, plus));
            var accelerationPlus = Apply(work, context);
            work.UpdatePosition(WithComponent(position, j, minus));
            var accelerationMinus = Apply(work, context);
            AddDifferenceColumn(dadr, j, accelerationPlus, accelerationMinus, plus - minus);
        }

        if (!DependsOnVelocity)
        {
            return;
        }

        double velocityStep = velocityRelativeStep * System.Math.Max(velocity.Magnitude(), 1.0);
        work.UpdatePosition(position);
        for (int j = 0; j < 3; j++)
        {
            double component = Component(velocity, j);
            double plus = component + velocityStep;
            double minus = component - velocityStep;
            work.UpdateVelocity(WithComponent(velocity, j, plus));
            var accelerationPlus = Apply(work, context);
            work.UpdateVelocity(WithComponent(velocity, j, minus));
            var accelerationMinus = Apply(work, context);
            AddDifferenceColumn(dadv, j, accelerationPlus, accelerationMinus, plus - minus);
        }
    }

    /// <summary>
    /// The work state of the finite differences, created at the first use and again only if the observer or the frame
    /// changes.
    /// </summary>
    private StateVector GetPartialsWorkState(StateVector stateVector)
    {
        if (_partialsWorkState == null || !ReferenceEquals(_partialsWorkState.Observer, stateVector.Observer) ||
            !ReferenceEquals(_partialsWorkState.Frame, stateVector.Frame))
        {
            _partialsWorkState = new StateVector(stateVector.Position, stateVector.Velocity, stateVector.Observer,
                stateVector.Epoch, stateVector.Frame);
        }

        return _partialsWorkState;
    }

    private static (double Plus, double Minus, double Step) PerturbedCoefficients(double coefficient)
    {
        double step = DefaultParameterRelativeStep * System.Math.Max(System.Math.Abs(coefficient), 1.0);
        double plus = coefficient + step;
        double minus = coefficient - step;
        return (plus, minus, plus - minus);
    }

    private static void AddDifferenceColumn(Span<double> block, int column, in Vector3 plus, in Vector3 minus,
        double step)
    {
        block[column] += (plus.X - minus.X) / step;
        block[3 + column] += (plus.Y - minus.Y) / step;
        block[6 + column] += (plus.Z - minus.Z) / step;
    }

    private static void AddDifference(Span<double> vector, in Vector3 plus, in Vector3 minus, double step)
    {
        vector[0] += (plus.X - minus.X) / step;
        vector[1] += (plus.Y - minus.Y) / step;
        vector[2] += (plus.Z - minus.Z) / step;
    }

    private static double Component(in Vector3 vector, int index)
    {
        return index switch
        {
            0 => vector.X,
            1 => vector.Y,
            _ => vector.Z
        };
    }

    private static Vector3 WithComponent(in Vector3 vector, int index, double value)
    {
        return index switch
        {
            0 => new Vector3(value, vector.Y, vector.Z),
            1 => new Vector3(vector.X, value, vector.Z),
            _ => new Vector3(vector.X, vector.Y, value)
        };
    }

    private static void ValidateLength(Span<double> buffer, int length, string name)
    {
        if (buffer.Length != length)
        {
            throw new ArgumentException($"The buffer must hold exactly {length} values, not {buffer.Length}.", name);
        }
    }
}
