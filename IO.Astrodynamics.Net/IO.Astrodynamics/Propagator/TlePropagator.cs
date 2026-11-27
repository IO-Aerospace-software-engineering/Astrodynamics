using System;
using System.Threading;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Samples the SGP4/SDP4 state of a spacecraft whose initial orbital parameters are a <see cref="Tle"/> over a window,
/// stores the samples as the spacecraft's stored states and returns them as a synthetic <see cref="PropagationSolution"/>.
/// </summary>
/// <remarks>
/// <para>
/// The samples are those of the closed form of the grid, counted in ticks (#598): sample i at the window start plus
/// i·<see cref="DeltaT"/>, i = 0 … ceil(L/ΔT) − 1, then the window end, so ceil(L/ΔT) + 1 samples, the last step shorter
/// when ΔT does not divide the window length L; a grid point closer to the end than ΔT/10 is dropped, the last step then
/// longer, up to 1.1 ΔT (a window given in UTC is a few ticks longer or shorter in TDB). Before 11.0.0 they were counted
/// in whole seconds: a step below 1 s divided by zero, a fractional step ran past the window end, and a window that ΔT
/// does not divide lost its end.
/// </para>
/// <para>
/// A run computes every sample and every acceleration first, then, in one block, re-arms the spacecraft as a numerical
/// propagation does (its stored states, attitude record, last solution and executed maneuvers dropped, its tanks
/// refuelled) and stores its samples, which replace the stored states; a stop or a failure of SGP4 changes nothing
/// (decision 38 of #668, decided by S. Guillet, 2026-11-23: before 11.0.0 a run added its samples to the stored states,
/// in one arc with those of earlier runs, and kept the attitude record and the solution of an earlier numerical run). The
/// spacecraft is then not <see cref="Spacecraft.IsPropagated"/>, and its geometric state reads SGP4 at every epoch, within
/// the samples too; only <c>GetEphemeris</c> from its Earth without aberration interpolates the samples, within them
/// (#675).
/// Every run samples the TLE the propagator was built from, into arrays of its own, so the solutions of two runs share no
/// state (#785, decision 39 of #668; before 11.0.0 they shared one array, its first sample built once at construction).
/// </para>
/// </remarks>
public class TlePropagator : IPropagator
{
    private readonly Time[] _sampleEpochs;

    // The TLE the propagator was built from, sampled by every run (#783, decision 37 of #668)
    private readonly Tle _tle;
    public Window Window { get; }
    public Spacecraft Spacecraft { get; }
    public TimeSpan DeltaT { get; }

    /// <summary>
    /// Builds the propagator of <paramref name="spacecraft"/> over <paramref name="window"/>, sampled every
    /// <paramref name="deltaT"/> and at the window end.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="spacecraft"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="window"/> has zero length, one rule on every propagation path
    /// (decisions 7 and 15 of lot L4 of #668, decided by S. Guillet, 2026-11-22; before 11.0.0, one sample at the start),
    /// or the initial orbital parameters of the spacecraft are not a TLE, or the spacecraft has a maneuver plan, which
    /// SGP4 cannot execute (decision 40 of #668, decided by S. Guillet, 2026-11-23; before 11.0.0 it was ignored).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaT"/> is zero or less (#598; before 11.0.0, a
    /// step below 1 s threw <see cref="DivideByZeroException"/>), or gives more samples than an array holds.</exception>
    public TlePropagator(Window window, Spacecraft spacecraft, TimeSpan deltaT)
    {
        PropagatorBase.RequireWindowLength(window);
        Window = new Window(window.StartDate.ToTdb(), window.EndDate.ToTdb());
        Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        if (Spacecraft.InitialOrbitalParameters is not Tle tle)
        {
            throw new ArgumentException("Spacecraft orbital parameters must be a two lines elements");
        }

        if (PlanError(Spacecraft) is { } planError)
        {
            throw new ArgumentException(planError, nameof(spacecraft));
        }

        _tle = tle;

        if (deltaT <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaT), deltaT, "The step must be above zero.");
        }

        DeltaT = deltaT;

        // Counted in ticks: i·ΔT from the start, then the window end (#598), ceil(L/ΔT) + 1 samples; a grid point closer
        // to the end than a tenth of the step is dropped, the last step then up to 1.1 ΔT long, as the stored states drop
        // an output that close to an impulse: a window given in UTC is a few ticks longer or shorter in TDB than in UTC,
        // and its last step would otherwise be a few ticks long, two stored states a few ticks apart, whose read is
        // ill-conditioned (4.3 km off SGP4 for the ISS over one UTC hour at 60 s; a defect of the stored-state read, not
        // fixed here, listed under the known limitations of 11.0.0)
        long lengthTicks = Window.Length.Ticks;
        long stepTicks = DeltaT.Ticks;
        long intervals = lengthTicks / stepTicks + (lengthTicks % stepTicks == 0 ? 0 : 1);
        if (intervals > 1 && (lengthTicks - (intervals - 1) * stepTicks) * 10 < stepTicks)
        {
            intervals--;
        }
        if (intervals >= Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaT), deltaT, $"The step gives {intervals + 1} samples over the window, more than an array holds.");
        }

        _sampleEpochs = new Time[intervals + 1];
        for (long i = 0; i < intervals; i++)
        {
            _sampleEpochs[i] = Window.StartDate.Add(TimeSpan.FromTicks(stepTicks * i));
        }

        _sampleEpochs[intervals] = Window.EndDate;
    }

    // Why the spacecraft cannot be sampled: a maneuver plan, which SGP4 cannot execute (decision 40 of #668); null otherwise
    private static string PlanError(Spacecraft spacecraft) => spacecraft.Maneuvers.Count > 0
        ? $"{spacecraft.Name} has a maneuver plan, which SGP4 cannot execute: a TlePropagator samples a spacecraft without " +
          "maneuvers. Clear the plan (SetManeuvers()), or propagate the spacecraft numerically (Spacecraft.Propagate)."
        : null;

    /// <summary>
    /// Samples the SGP4 state of the TLE the propagator was built from at the epochs of the grid, stores the samples as
    /// the spacecraft's stored states and returns them as a solution.
    /// </summary>
    /// <param name="cancellationToken">Read before the first sample and at each sample (R28 of #668): a run stopped
    /// changes nothing on the spacecraft, which re-arms and stores only once every sample is computed (decision 38 of
    /// #668, decided by S. Guillet, 2026-11-23; decision 19 of lot L4, then the exception to C2, became the rule).</param>
    /// <returns>The solution, in <see cref="Frame.TEME"/> about the TLE's Earth (<see cref="PropagationSolution.Observer"/>,
    /// <see cref="PropagationSolution.Frame"/>; decision 53 of #668).</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; the exception
    /// carries it.</exception>
    /// <exception cref="InvalidOperationException">The spacecraft holds other initial orbital parameters than the TLE the
    /// propagator was built from, compared by reference (#783, decision 37 of #668; before 11.0.0 the run took its first
    /// sample from the TLE of the construction and the others from the spacecraft's orbit), or it has a maneuver plan,
    /// set after the propagator was built (decision 40 of #668), or SGP4 fails at a sample. Nothing changes.</exception>
    public PropagationSolution Propagate(CancellationToken cancellationToken = default)
    {
        // The checks of the run, before the token, as on the numerical path
        PropagatorBase.RequireInitialOrbit(Spacecraft, _tle);
        if (PlanError(Spacecraft) is { } planError)
        {
            throw new InvalidOperationException(planError);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 1. Every sample and acceleration, in arrays of this run (#785), before anything changes: a stop or an SGP4 that
        // fails (within 1 s of a window end for the accelerations) leaves the spacecraft as it was
        int sampleCount = _sampleEpochs.Length;
        var samples = new StateVector[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            samples[i] = _tle.ToStateVector(_sampleEpochs[i]);
        }

        // The accelerations of SGP4 at the samples, which the cubic Hermite of a step interpolates the velocity with
        // (#675: zero before, up to 49.9 m/s off SGP4 between samples)
        var accelerations = new Vector3[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            accelerations[i] = Sgp4Acceleration(_sampleEpochs[i]);
        }

        var solution = new PropagationSolution { Owner = Spacecraft, Observer = samples[0].Observer, Frame = samples[0].Frame };
        var segment = new PropagationSegment(Window.StartDate, sampleCount);

        // The steps join the samples, in TDB seconds from the start (the solution's time argument): ΔT each, the last
        // shorter, or up to 1.1 ΔT, when ΔT does not divide the window (#598); i·ΔT exactly for a whole-second step
        for (int i = 0; i < sampleCount - 1; i++)
        {
            double cumulativeTime = (_sampleEpochs[i] - Window.StartDate).TotalSeconds;
            double stepSize = (_sampleEpochs[i + 1] - Window.StartDate).TotalSeconds - cumulativeTime;
            segment.AddStep(new AcceptedStep(
                cumulativeTime, stepSize,
                samples[i].Position, samples[i].Velocity,
                samples[i + 1].Position, samples[i + 1].Velocity,
                accelerations[i], accelerations[i + 1]));
        }

        solution.AddSegment(segment);
        solution.SetOutputStates(samples);

        // 2. In one block: the re-arm of every run (decision 38 of #668), then the samples, which replace the stored states
        Spacecraft.ResetPropagation();
        Spacecraft.AddPropagatedStates(samples);

        return solution;
    }

    // The half-width of the central difference of the SGP4 velocity, s
    private const double Sgp4DifferenceStep = 1.0;

    /// <summary>
    /// The acceleration of SGP4 at <paramref name="epoch"/>: the central difference of its velocity at ±1 s, whose
    /// truncation v‴·δ²/6 is about 1.9e-6 m/s² in LEO (1.7e-5 m/s over a step of 60 s); a difference over the neighbouring
    /// samples grows with the step squared (about 0.06 m/s at 60 s). The rotation of TEME of date over 2 s adds below
    /// 1e-7 m/s².
    /// </summary>
    private Vector3 Sgp4Acceleration(in Time epoch)
    {
        var after = _tle.ToStateVector(epoch.AddSeconds(Sgp4DifferenceStep)).Velocity;
        var before = _tle.ToStateVector(epoch.AddSeconds(-Sgp4DifferenceStep)).Velocity;
        return (after - before) / (2.0 * Sgp4DifferenceStep);
    }

}