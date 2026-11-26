// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Propagator.Integrators;

/// <summary>
/// The settings of the integrator of a propagation (<see cref="PropagationOptions.Integrator"/>): values, from which each
/// propagation builds its own integrator. A closed hierarchy (D1 rule 5 of #668): <see cref="RK78IntegratorOptions"/>
/// and <see cref="VVIntegratorOptions"/>.
/// </summary>
/// <remarks>
/// To change one setting of the integrator of an options record, go through a typed local, since <c>with</c> on the
/// abstract property does not compile (CS0117):
/// <c>var rk = RK78IntegratorOptions.Default with { AbsoluteTolerance = 1e-12 }; options with { Integrator = rk }</c>.
/// </remarks>
public abstract record IntegratorOptions
{
    private protected IntegratorOptions()
    {
    }

    /// <summary>A new integrator of these settings, without forces, for one propagation.</summary>
    internal abstract Integrator CreateIntegrator();

    /// <summary>
    /// Checks the rules between the settings, at the start of the call that takes them (D1 rule 3).
    /// </summary>
    /// <exception cref="ArgumentException">The settings contradict each other, naming <c>options</c>.</exception>
    internal virtual void Validate()
    {
    }

    /// <summary>The settings of <paramref name="integrator"/>, or null for an integrator of another type.</summary>
    internal static IntegratorOptions Of(IIntegrator integrator) => integrator switch
    {
        RK78Integrator rk78 => rk78.Settings,
        VVIntegrator vv => new VVIntegratorOptions { Step = vv.DeltaT },
        _ => null
    };

    // A positive finite value of a setting, checked in init
    private protected static double PositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite and positive.");
        }

        return value;
    }

    // A positive step of a setting, checked in init
    private protected static TimeSpan PositiveStep(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be positive.");
        }

        return value;
    }
}

/// <summary>
/// The settings of the adaptive Prince-Dormand RK7(8) integrator (<see cref="RK78Integrator"/>), or of its fixed-step
/// mode (<see cref="FixedStep"/>). Its defaults are those of <c>new RK78Integrator()</c>: tolerances of 1e-9, a first step
/// of 60 s, steps between 1 µs and one day, a safety factor of 0.9 and step factors between 0.2 and 5.
/// </summary>
/// <remarks>
/// The steps are <see cref="TimeSpan"/>s (D1 rule 4 of #668), exact for the defaults: 60 s, 1 µs (10 ticks) and
/// 86400 s give the <see cref="RK78Integrator"/> the seconds of its double constructor, bit for bit. A step given through
/// <see cref="TimeSpan.FromSeconds(double)"/> may be a tick short of its decimal value; the exact factories
/// (<see cref="TimeSpan.FromMilliseconds(double)"/>, <see cref="TimeSpan.FromTicks"/>) avoid it.
/// <para>
/// The default of an options entry point (<see cref="PropagationOptions.Integrator"/>): at 1e-9 a point-mass LEO day
/// ends 1.36 m from Kepler (R07 of #668, structuring choice 5, decided by S. Guillet, 2026-11-19).
/// </para>
/// </remarks>
public sealed record RK78IntegratorOptions : IntegratorOptions
{
    private readonly double _absoluteTolerance = 1e-9;
    private readonly double _relativeTolerance = 1e-9;
    private readonly TimeSpan _initialStep = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _minStep = TimeSpan.FromTicks(10);
    private readonly TimeSpan _maxStep = TimeSpan.FromDays(1);
    private readonly double _safetyFactor = 0.9;
    private readonly double _minFactor = 0.2;
    private readonly double _maxFactor = 5.0;
    private readonly TimeSpan? _fixedStep;

    /// <summary>The default settings, equal to <c>new()</c> and to <c>new RK78Integrator()</c>.</summary>
    public static RK78IntegratorOptions Default { get; } = new();

    /// <summary>The absolute error tolerance of a step (m and m/s), 1e-9 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite and positive.</exception>
    public double AbsoluteTolerance
    {
        get => _absoluteTolerance;
        init => _absoluteTolerance = PositiveFinite(value, nameof(AbsoluteTolerance));
    }

    /// <summary>The relative error tolerance of a step, 1e-9 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite and positive.</exception>
    public double RelativeTolerance
    {
        get => _relativeTolerance;
        init => _relativeTolerance = PositiveFinite(value, nameof(RelativeTolerance));
    }

    /// <summary>The first step of each segment, 60 s by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan InitialStep
    {
        get => _initialStep;
        init => _initialStep = PositiveStep(value, nameof(InitialStep));
    }

    /// <summary>The smallest step the controller takes, 1 µs by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan MinStep
    {
        get => _minStep;
        init => _minStep = PositiveStep(value, nameof(MinStep));
    }

    /// <summary>The largest step the controller takes, one day by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan MaxStep
    {
        get => _maxStep;
        init => _maxStep = PositiveStep(value, nameof(MaxStep));
    }

    /// <summary>The safety factor of the step controller, 0.9 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite and positive.</exception>
    public double SafetyFactor
    {
        get => _safetyFactor;
        init => _safetyFactor = PositiveFinite(value, nameof(SafetyFactor));
    }

    /// <summary>The smallest factor of a new step over the last one, 0.2 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite and positive.</exception>
    public double MinFactor
    {
        get => _minFactor;
        init => _minFactor = PositiveFinite(value, nameof(MinFactor));
    }

    /// <summary>The largest factor of a new step over the last one, 5 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not finite and positive.</exception>
    public double MaxFactor
    {
        get => _maxFactor;
        init => _maxFactor = PositiveFinite(value, nameof(MaxFactor));
    }

    /// <summary>
    /// The step of the fixed-step mode, without error control (<c>new RK78Integrator(fixedStepSizeSeconds)</c>); null (the
    /// default) for the adaptive mode. Set with another setting than its default, it throws
    /// <see cref="ArgumentException"/> at the start of the call that takes the options (R06 of #668, section A of the
    /// re-baseline, decided by S. Guillet, 2026-11-19).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan? FixedStep
    {
        get => _fixedStep;
        init => _fixedStep = value.HasValue ? PositiveStep(value.Value, nameof(FixedStep)) : null;
    }

    /// <inheritdoc />
    internal override void Validate()
    {
        if (_fixedStep.HasValue && this != Default with { FixedStep = _fixedStep })
        {
            throw new ArgumentException(
                "RK78IntegratorOptions.FixedStep selects the fixed-step mode, which controls no step: leave the " +
                "tolerances, the step bounds and the factors at their defaults, or remove FixedStep.", "options");
        }
    }

    /// <inheritdoc />
    internal override Integrator CreateIntegrator()
    {
        Validate();
        return _fixedStep.HasValue
            ? new RK78Integrator(_fixedStep.Value.TotalSeconds)
            : new RK78Integrator(_absoluteTolerance, _relativeTolerance, _initialStep.TotalSeconds,
                _minStep.TotalSeconds, _maxStep.TotalSeconds, _safetyFactor, _minFactor, _maxFactor);
    }
}

/// <summary>
/// The settings of the Velocity-Verlet integrator (<see cref="VVIntegrator"/>): its fixed step, 1 s by default.
/// </summary>
/// <remarks>
/// Velocity-Verlet is second order: a point-mass LEO day ends 271 m from Kepler at 1 s, 27.1 km at 10 s and 974 km at
/// 60 s (the error grows as the square of the step; R07 of #668). The step is the integration step only: the output step
/// of a propagation is <see cref="PropagationOptions.OutputStep"/>.
/// </remarks>
public sealed record VVIntegratorOptions : IntegratorOptions
{
    private readonly TimeSpan _step = TimeSpan.FromSeconds(1);

    /// <summary>The default settings, equal to <c>new()</c>: a step of 1 s.</summary>
    public static VVIntegratorOptions Default { get; } = new();

    /// <summary>The integration step, 1 s by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan Step
    {
        get => _step;
        init => _step = PositiveStep(value, nameof(Step));
    }

    /// <inheritdoc />
    internal override Integrator CreateIntegrator() => new VVIntegrator(_step);
}
