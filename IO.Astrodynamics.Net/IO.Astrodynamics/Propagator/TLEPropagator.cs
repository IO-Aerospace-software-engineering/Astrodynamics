using System;
using System.Collections.Generic;
using System.Threading;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Samples the SGP4/SDP4 state of a spacecraft whose initial orbital parameters are a <see cref="TLE"/> over a window,
/// adds the samples to the spacecraft's stored states and returns them as a synthetic <see cref="PropagationSolution"/>.
/// </summary>
/// <remarks>
/// The samples are those of the closed form of the grid, counted in ticks (#598): sample i at the window start plus
/// i·<see cref="DeltaT"/>, i = 0 … ceil(L/ΔT) − 1, then the window end, so ceil(L/ΔT) + 1 samples, the last step shorter
/// when ΔT does not divide the window length L; a grid point closer to the end than ΔT/10 is dropped, the last step then
/// longer, up to 1.1 ΔT (a window given in UTC is a few ticks longer or shorter in TDB). Before 11.0.0 they were counted
/// in whole seconds: a step below 1 s divided by zero, a fractional step ran past the window end, and a window that ΔT
/// does not divide lost its end.
/// </remarks>
public class TLEPropagator : IPropagator
{
    private readonly Time[] _sampleEpochs;
    private readonly StateVector[] _svCache;
    // Keyed by label (#512), as the orientations of a frame
    private readonly Dictionary<Time, StateOrientation> _stateOrientation = new Dictionary<Time, StateOrientation>(Time.ExactComparer);
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
    /// or the initial orbital parameters of the spacecraft are not a TLE.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaT"/> is zero or less (#598; before 11.0.0, a
    /// step below 1 s threw <see cref="DivideByZeroException"/>), or gives more samples than an array holds.</exception>
    public TLEPropagator(Window window, Spacecraft spacecraft, TimeSpan deltaT)
    {
        PropagatorBase.RequireWindowLength(window);
        Window = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        if (Spacecraft.InitialOrbitalParameters is not TLE)
        {
            throw new ArgumentException("Spacecraft orbital parameters must be a two lines elements");
        }

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

        var initialState = Spacecraft.InitialOrbitalParameters.AtEpoch(Window.StartDate).ToStateVector();
        _svCache = new StateVector[_sampleEpochs.Length];
        _svCache[0] = initialState;
    }

    /// <summary>
    /// Samples the SGP4 state at the epochs of the grid, adds the samples to the spacecraft's stored states and returns
    /// them as a solution.
    /// </summary>
    /// <param name="cancellationToken">Read before the first sample and at each sample (R28 of #668): a run stopped adds
    /// nothing to the spacecraft, which is left as it was, its stored states those it had before the run (decision 19 of
    /// lot L4: the exception to C2, since this propagator adds its samples instead of replacing the stored states).</param>
    /// <returns>The solution.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; the exception
    /// carries it.</exception>
    public PropagationSolution Propagate(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _stateOrientation[Window.StartDate] = new StateOrientation(Quaternion.Identity, Vector3.Zero, Window.StartDate, Frame.ICRF);
        int sampleCount = _sampleEpochs.Length;
        for (int i = 1; i < sampleCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _svCache[i] = Spacecraft.InitialOrbitalParameters.ToStateVector(_sampleEpochs[i]);
        }

        _stateOrientation[Window.EndDate] = new StateOrientation(Quaternion.Identity, Vector3.Zero, Window.EndDate, Frame.ICRF);

        // Build synthetic PropagationSolution from TLE states, with the accelerations of SGP4 at the samples, which the cubic
        // Hermite of a step interpolates the velocity with (#675: zero before, up to 49.9 m/s off SGP4 between samples).
        // Read before the spacecraft receives the samples: an SGP4 that fails within 1 s of a window end leaves it as it was
        var accelerations = new Vector3[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            accelerations[i] = Sgp4Acceleration(_sampleEpochs[i]);
        }

        Spacecraft.AddPropagatedStates(_svCache);

        var solution = new PropagationSolution { Owner = Spacecraft };
        var segment = new PropagationSegment(Window.StartDate, sampleCount);

        // The steps join the samples, in TDB seconds from the start (the solution's time argument): ΔT each, the last
        // shorter, or up to 1.1 ΔT, when ΔT does not divide the window (#598); i·ΔT exactly for a whole-second step
        for (int i = 0; i < sampleCount - 1; i++)
        {
            double cumulativeTime = (_sampleEpochs[i] - Window.StartDate).TotalSeconds;
            double stepSize = (_sampleEpochs[i + 1] - Window.StartDate).TotalSeconds - cumulativeTime;
            segment.AddStep(new AcceptedStep(
                cumulativeTime, stepSize,
                _svCache[i].Position, _svCache[i].Velocity,
                _svCache[i + 1].Position, _svCache[i + 1].Velocity,
                accelerations[i], accelerations[i + 1]));
        }

        solution.AddSegment(segment);
        solution.SetOutputStates(_svCache);

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
        var after = Spacecraft.InitialOrbitalParameters.ToStateVector(epoch.AddSeconds(Sgp4DifferenceStep)).Velocity;
        var before = Spacecraft.InitialOrbitalParameters.ToStateVector(epoch.AddSeconds(-Sgp4DifferenceStep)).Velocity;
        return (after - before) / (2.0 * Sgp4DifferenceStep);
    }

}