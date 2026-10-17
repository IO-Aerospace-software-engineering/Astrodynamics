// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// The replay of the variational equations along the accepted steps of an RK7(8) propagation that did not integrate
/// them: the conjunction analysis propagates the covariance of a participant this way, without a second propagation
/// (step 10, E2; decisions of 2026-10-06 and 2026-10-07).
/// </summary>
/// <remarks>
/// <para>
/// Each stored step is computed again from its stored start state and size, in the context of its segment, by the same
/// <see cref="RK78Stepper"/> and <see cref="VariationalEquations.Step"/> as the integrator. With the step controlled on
/// the state only (A2), a propagation with the variational equations takes the same steps as one without them, so the
/// replayed Y and Q are those of a propagation run with the same options from the start, bit for bit. At an event, the
/// values are evaluated at the event time the propagation handled (<see cref="PropagationSegment.EventTime"/>), and the
/// execution error of a maneuver is added to Q there (<see cref="PropagationSegment.EntryManeuver"/>, C5), as the
/// propagator does.
/// </para>
/// <para>
/// The result is a separate solution over the steps up to the one that contains the end epoch: the solution replayed is
/// never changed. It shares the forces of that solution and the lock of its dynamics, so its evaluations and those of
/// the solution replayed are serialized.
/// </para>
/// </remarks>
internal static class VariationalReplay
{
    /// <summary>
    /// Replay the variational equations of <paramref name="options"/> along <paramref name="solution"/>, from its start
    /// to the step that contains <paramref name="end"/>, P0 being the covariance of the initial orbital parameters of
    /// <paramref name="participant"/>, the object the solution propagates.
    /// </summary>
    /// <returns>A solution over the replayed steps, from which Φ, Ψ and P are read as from a propagation with
    /// <paramref name="options"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="options"/> put Φ and Ψ in the step control, which would change
    /// the steps; or name a consider parameter whose force the propagation lacks.</exception>
    /// <exception cref="InvalidOperationException">The solution is not an RK7(8) propagation, already holds variational
    /// data, or does not reach <paramref name="end"/>; or P0 is rejected (<see cref="PropagatorBase.CreateCovarianceModel(ILocalizable, in Time, VariationalOptions)"/>).</exception>
    internal static PropagationSolution Replay(PropagationSolution solution, ILocalizable participant,
        CovariancePropagationOptions options, in Time end)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(options);
        var source = solution.Dynamics ?? throw new InvalidOperationException(
            "Only an RK7(8) propagation can be replayed: its solution keeps the dynamics of the propagation.");
        if (source.Options != null)
            throw new InvalidOperationException(
                "The propagation already integrated the variational equations: read them from its solution.");
        if (options.IncludeInStepControl)
            throw new ArgumentException(
                "The replay follows the steps of the propagation, controlled on the state only: Φ and Ψ cannot take part " +
                "in the step control (IncludeInStepControl).", nameof(options));

        var segments = solution.Segments;
        if (segments.Count == 0 || segments[0].Steps.Count == 0)
            throw new InvalidOperationException("The solution holds no steps.");

        var last = segments[^1];
        if (end < segments[0].BaseEpoch || (end - last.BaseEpoch).TotalSeconds > last.Duration + EpochTick)
            throw new ArgumentOutOfRangeException(nameof(end), end, "The epoch is outside the propagation.");

        var forces = source.Forces;
        var variationalOptions = options.ToVariationalOptions(forces);
        var covariance = PropagatorBase.CreateCovarianceModel(participant, segments[0].BaseEpoch, variationalOptions);
        foreach (var gravity in forces.OfType<GravitationalAcceleration>())
        {
            gravity.CelestialItem.ValidateGravitationalPartialsDegree(variationalOptions.GeopotentialPartialsDegree);
        }

        var equations = new VariationalEquations(variationalOptions);
        var dynamics = new PropagationDynamics(forces, source.Observer, source.Frame, source.Epoch,
            source.OrientationCacheFrame, source.OrientationCache, variationalOptions, equations, covariance,
            source.SyncRoot) { Integrator = source.Integrator };
        var replayed = new PropagationSolution
        {
            Dynamics = dynamics,
            Observer = solution.Observer,
            Frame = solution.Frame,
            CovariancePropagation = options
        };

        var stepper = new RK78Stepper(forces);
        stepper.Reset(source.Observer, source.Frame, source.Epoch);
        int covarianceLength = equations.HasProcessNoise ? VariationalEquations.CovarianceLength : 0;
        var y0 = new double[equations.YLength];
        var y1 = new double[equations.YLength];
        var q0 = new double[covarianceLength];
        var q1 = new double[covarianceLength];
        var entryY = new double[equations.YLength];
        equations.SetIdentity(entryY);
        var entryQ = new double[equations.CarriedCovarianceLength];

        lock (source.SyncRoot)
        {
            // The forces carry the degree of their partials, as the propagator sets it before a propagation with them
            foreach (var gravity in forces.OfType<GravitationalAcceleration>())
            {
                gravity.PartialsDegree = variationalOptions.GeopotentialPartialsDegree;
            }

            using (Frame.UseOrientationCache(source.OrientationCacheFrame, source.OrientationCache))
            {
                for (int s = 0; s < segments.Count; s++)
                {
                    var segment = segments[s];
                    if (s > 0 && segment.BaseEpoch > end)
                        break;

                    var context = segment.Context ??
                                  throw new InvalidOperationException("The segment holds no context.");
                    if (segment.EntryManeuver?.ExecutionCovariance is { } execution && entryQ.Length > 0)
                    {
                        AddExecutionError(execution, entryQ);
                    }

                    double endOffset = (end - segment.BaseEpoch).TotalSeconds;
                    var copy = new PropagationSegment(segment.BaseEpoch, segment.Steps.Count)
                    {
                        Context = context,
                        EntryManeuver = segment.EntryManeuver,
                        EventTime = segment.EventTime
                    };
                    var data = new VariationalSegmentData(equations.YLength, equations.HasProcessNoise,
                        segment.Steps.Count, entryY, entryQ, segment.EntryManeuver);
                    copy.Variational = data;

                    equations.SetIdentity(y0);
                    Array.Clear(q0);
                    stepper.Context = context;
                    foreach (var step in segment.Steps)
                    {
                        copy.AddStep(step);
                        stepper.Step(step.StartPosition, step.StartVelocity, segment.BaseEpoch, step.CumulativeTime,
                            step.StepSize, out _, out _, out _, out _);
                        equations.Step(stepper.StageStates, forces, context, step.StepSize, y0, q0, y1, q1);
                        data.Append(y1, q1);
                        (y0, y1) = (y1, y0);
                        (q0, q1) = (q1, q0);
                        if (step.CumulativeTime + step.StepSize >= endOffset)
                            break;
                    }

                    replayed.AddSegment(copy);

                    // The values at the event that ends the segment become the entry values of the next one, evaluated
                    // as the propagator did, by a shortened step at the event time
                    if (s + 1 < segments.Count && segments[s + 1].BaseEpoch <= end && segment.EventTime is { } eventTime)
                    {
                        entryY = new double[equations.YLength];
                        entryQ = new double[equations.CarriedCovarianceLength];
                        dynamics.Evaluate(copy, eventTime, entryY, entryQ);
                    }
                }
            }
        }

        replayed.SetOutputStates(Array.Empty<OrbitalParameters.StateVector>());
        return replayed;
    }

    // One tick of Time (100 ns), as PropagationSolution allows past the end of the last step
    private const double EpochTick = 1e-7;

    // Q⁺ = Q⁻ + B L_e Bᵀ, B = [0 ; I]: the execution error of a burn on the velocity block (C5, model of Gates), as the
    // propagator adds it
    private static void AddExecutionError(double[] execution, double[] q)
    {
        for (int i = 0; i < 3; i++)
        {
            for (int j = i; j < 3; j++)
            {
                q[VariationalEquations.CovarianceIndex(i + 3, j + 3)] += execution[3 * i + j];
            }
        }
    }
}
