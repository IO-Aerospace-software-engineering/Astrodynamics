// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Covariance of one participant at the encounter epoch, as used by the conjunction analysis and the CDM export.
/// </summary>
/// <param name="CovarianceInertial">6x6 covariance in the inertial frame of the encounter state, or null when none is available.</param>
/// <param name="Age">Time between the epoch of the covariance and the encounter epoch; null when no covariance is available.</param>
/// <param name="Source">Where the covariance comes from.</param>
/// <param name="Solution">For a propagated covariance, the solution it is read from (the propagation itself, or the
/// replay of its variational equations); null otherwise.</param>
internal sealed record EncounterCovariance(Matrix? CovarianceInertial, TimeSpan? Age, CovarianceSource Source,
    PropagationSolution Solution = null)
{
    /// <summary>True when the covariance was taken from the participant's initial state, held in RTN.</summary>
    internal bool FromInitialState => Source == CovarianceSource.InitialStateHeldInRtn;

    /// <summary>Whether a process noise was added to a propagated covariance.</summary>
    internal bool ProcessNoiseApplied => Solution?.CovariancePropagation?.ProcessNoise != null;

    /// <summary>The non-linearity index of a propagated covariance, null when not computed.</summary>
    internal double? NonlinearityIndex { get; init; }

    /// <summary>For a propagated covariance, the propagation of the participant, whose variational equations
    /// <see cref="Solution"/> may replay up to the last encounter only; null otherwise.</summary>
    internal PropagationSolution Propagation { get; init; }

    /// <summary>The public view of this covariance on <see cref="EncounterState"/>.</summary>
    internal ParticipantCovariance ToParticipantCovariance() =>
        new(Source, Age, Source == CovarianceSource.Propagated && ProcessNoiseApplied,
            Source == CovarianceSource.Propagated ? NonlinearityIndex : null);

    /// <summary>
    /// Resolves the covariance of a participant at the encounter epoch, without propagating it.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// <list type="number">
    /// <item>the covariance carried by the state at the encounter epoch, if any (age zero);</item>
    /// <item>otherwise the covariance of the participant's initial state, held fixed in the RTN frame: rotated into
    /// RTN with the initial state, then back into the inertial frame with the encounter state. Holding it in RTN keeps
    /// its orientation relative to the orbit (large along-track, small radial);</item>
    /// <item>otherwise no covariance.</item>
    /// </list>
    /// A propagated covariance is resolved first by <see cref="EncounterCovarianceResolver"/>.
    /// </remarks>
    internal static EncounterCovariance Resolve(ILocalizable source, StateVector encounterState)
    {
        if (encounterState.Covariance.HasValue)
        {
            return new EncounterCovariance(encounterState.Covariance.Value, TimeSpan.Zero,
                CovarianceSource.ProvidedAtEncounter);
        }

        if (source.InitialOrbitalParameters is StateVector initialState && initialState.Covariance.HasValue)
        {
            var covarianceRtn = initialState.RotateCovarianceToRtn(initialState.Covariance.Value);
            var covarianceInertial = encounterState.RotateCovarianceFromRtn(covarianceRtn);
            var age = (encounterState.Epoch - initialState.Epoch).Duration();
            return new EncounterCovariance(covarianceInertial, age, CovarianceSource.InitialStateHeldInRtn);
        }

        return new EncounterCovariance(null, null, CovarianceSource.None);
    }

    /// <summary>
    /// True when the covariance comes from the initial state and is older than the given threshold.
    /// </summary>
    internal bool IsStale(TimeSpan threshold)
    {
        return FromInitialState && Age > threshold;
    }
}

/// <summary>
/// The covariance of one participant at the encounter epochs of one analysis: propagated when the three conditions of
/// E2 hold, resolved by <see cref="EncounterCovariance.Resolve"/> otherwise (step 10, E1 and E2).
/// </summary>
/// <remarks>
/// <para>
/// The covariance is propagated when the options are given, the participant comes with an RK7(8) solution that covers
/// the encounter epoch, and either that solution propagated the covariance, read as it is, or the initial state of the
/// participant carries a covariance at the start of the solution: the variational equations are then replayed along its
/// steps, once, up to the last encounter epoch of the analysis (<see cref="VariationalReplay"/>). The covariance is
/// evaluated at the final encounter epoch only, never during the search.
/// </para>
/// <para>
/// Not thread-safe: one instance per participant and per analysis.
/// </para>
/// </remarks>
internal sealed class EncounterCovarianceResolver
{
    // One tick of Time (100 ns), as PropagationSolution allows past the end of the last step
    private const double EpochTick = 1e-7;

    private readonly ILocalizable _participant;
    private readonly PropagationSolution _solution;
    private readonly CovariancePropagationOptions _options;
    private readonly Time _replayEnd;
    private readonly bool _computeNonlinearityIndex;
    private PropagationSolution _replayed;
    private NonlinearityIndex _nonlinearityIndex;
    private bool _nonlinearityIndexCreated;

    /// <param name="participant">The participant.</param>
    /// <param name="solution">Its propagation, null when it has none.</param>
    /// <param name="options">The propagation of the covariance, null for none.</param>
    /// <param name="lastEncounter">The last encounter epoch of the analysis, where a replay ends.</param>
    /// <param name="computeNonlinearityIndex">Whether the non-linearity index of a propagated covariance is computed.</param>
    internal EncounterCovarianceResolver(ILocalizable participant, PropagationSolution solution,
        CovariancePropagationOptions options, in Time lastEncounter, bool computeNonlinearityIndex = false)
    {
        _participant = participant ?? throw new ArgumentNullException(nameof(participant));
        _solution = solution;
        _options = options;
        _replayEnd = lastEncounter;
        _computeNonlinearityIndex = computeNonlinearityIndex;
    }

    /// <summary>The replay of the variational equations of the participant, once made; null before, and when its
    /// covariance is read from its propagation or not propagated.</summary>
    internal PropagationSolution Replayed => _replayed;

    /// <summary>The solution the covariance is propagated with at <paramref name="epoch"/>, null when it is not
    /// propagated.</summary>
    internal PropagationSolution PropagatedSolution(in Time epoch)
    {
        if (_options == null || _solution?.Dynamics == null || !Covers(_solution, epoch))
            return null;

        if (_solution.CovariancePropagation?.Mode == CovariancePropagationMode.Covariance)
            return _solution;

        if (_participant.InitialOrbitalParameters is not StateVector { Covariance: not null } initial ||
            (initial.Epoch - _solution.Segments[0].BaseEpoch) != TimeSpan.Zero)
            return null;

        if (_replayed == null)
        {
            var end = _replayEnd;
            if (!Covers(_solution, end))
            {
                var last = _solution.Segments[^1];
                end = last.BaseEpoch.AddSeconds(last.Duration);
            }

            try
            {
                _replayed = VariationalReplay.Replay(_solution, _participant, _options, end > epoch ? end : epoch);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException(
                    $"The covariance of '{_participant.Name}' cannot be propagated to the encounter: {e.Message}",
                    nameof(ConjunctionAnalysisOptions.CovariancePropagation), e);
            }
        }

        return _replayed;
    }

    /// <summary>
    /// The covariance of the participant at the epoch of <paramref name="encounterState"/>, its state at the
    /// encounter in the analysis.
    /// </summary>
    internal EncounterCovariance Resolve(StateVector encounterState)
    {
        var solution = PropagatedSolution(encounterState.Epoch);
        if (solution == null)
            return EncounterCovariance.Resolve(_participant, encounterState);

        // P in the frame of the propagation, relative to its central body: a change of origin by a known ephemeris has
        // the Jacobian I, and a change of inertial frame is a rotation of P (ToFrame)
        var state = solution.GetStateVector(encounterState.Epoch);
        if (state.Frame != encounterState.Frame)
            state = state.ToFrame(encounterState.Frame).ToStateVector();

        return new EncounterCovariance(state.Covariance!.Value, TimeSpan.Zero, CovarianceSource.Propagated, solution)
        {
            NonlinearityIndex = NonlinearityIndexAt(solution, encounterState.Epoch),
            Propagation = _solution
        };
    }

    // The non-linearity index at the encounter epoch, from the twelve perturbed propagations made once up to the last
    // encounter; null when it is not asked for or cannot be computed
    private double? NonlinearityIndexAt(PropagationSolution solution, in Time epoch)
    {
        if (!_computeNonlinearityIndex)
            return null;

        if (!_nonlinearityIndexCreated)
        {
            var last = solution.Segments[^1];
            var end = last.BaseEpoch.AddSeconds(last.Duration);
            if (_replayEnd < end && _replayEnd >= epoch)
                end = _replayEnd;
            if (end < epoch)
                end = epoch;
            _nonlinearityIndex = NonlinearityIndex.Create(solution, _participant, end);
            _nonlinearityIndexCreated = true;
        }

        return _nonlinearityIndex?.At(solution, epoch);
    }

    private static bool Covers(PropagationSolution solution, in Time epoch)
    {
        if (solution.Segments.Count == 0)
            return false;

        var last = solution.Segments[^1];
        return epoch >= solution.Segments[0].BaseEpoch &&
               (epoch - last.BaseEpoch).TotalSeconds <= last.Duration + EpochTick;
    }
}
