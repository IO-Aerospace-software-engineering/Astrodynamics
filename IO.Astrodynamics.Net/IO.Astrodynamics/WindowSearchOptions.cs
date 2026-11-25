// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics;

/// <summary>
/// Options of a distance, coordinate, horizontal or illumination window search, and of the static
/// <c>GeometryFinder.FindWindows</c> (R15 of #668, lot L6, D1, decided by S. Guillet, 2026-11-19). Immutable.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Step"/> is required: nothing infers the step of a threshold search, and a search given none throws
/// <see cref="ArgumentException"/> naming it. <see cref="Aberration"/> null is <see cref="IO.Astrodynamics.Aberration.None"/>.
/// <see cref="ConvergenceTolerance"/> null is 1 µs, the tolerance of the library's search and of CSPICE's
/// (<c>SPICE_GF_CNVTOL</c>); set on a search that CSPICE runs, which cannot take it, it throws <see cref="ArgumentException"/>.
/// <see cref="AdjustValue"/> applies to <see cref="RelationnalOperator.AbsoluteMin"/> and
/// <see cref="RelationnalOperator.AbsoluteMax"/> only, and throws with any other relation and on CSPICE's distance search.
/// </para>
/// </remarks>
public sealed record WindowSearchOptions
{
    private readonly TimeSpan? _step;
    private readonly TimeSpan? _convergenceTolerance;
    private readonly double? _adjustValue;

    /// <summary>The default options, equal to <c>new()</c>: every setting unset.</summary>
    public static WindowSearchOptions Default { get; } = new();

    /// <summary>
    /// The step of the samples, positive; required: a search given none throws <see cref="ArgumentException"/> naming
    /// it. The library's search finds every window and gap when it is at most half the time between two extremes of the
    /// quantity (a <see cref="bool"/> quantity: shorter than the shortest window or gap to find); CSPICE's when it is
    /// shorter than the shortest interval where the quantity is monotone.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan? Step
    {
        get => _step;
        init => _step = WindowSearchSettings.PositiveOrNull(value, nameof(Step));
    }

    /// <summary>The aberration correction; null for <see cref="IO.Astrodynamics.Aberration.None"/>.</summary>
    public Aberration? Aberration { get; init; }

    /// <summary>
    /// The tolerance to which the library's search locates each transition, positive; null for 1 µs. Set on a search
    /// that CSPICE runs, it throws <see cref="ArgumentException"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan? ConvergenceTolerance
    {
        get => _convergenceTolerance;
        init => _convergenceTolerance = WindowSearchSettings.PositiveOrNull(value, nameof(ConvergenceTolerance));
    }

    /// <summary>
    /// For <see cref="RelationnalOperator.AbsoluteMin"/> and <see cref="RelationnalOperator.AbsoluteMax"/>, the windows
    /// where the quantity is within this value of the extreme (CSPICE's <c>adjust</c>); 0 or null for the extreme
    /// itself. Set with another relation, or on CSPICE's distance search, it throws <see cref="ArgumentException"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    public double? AdjustValue
    {
        get => _adjustValue;
        init
        {
            if (value.HasValue && !(double.IsFinite(value.Value) && value.Value >= 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(AdjustValue), value, "AdjustValue must be finite and positive or zero.");
            }

            _adjustValue = value;
        }
    }
}

/// <summary>
/// Options of a field-of-view window search (<c>Instrument.FindFieldOfViewWindows</c>; R15 of #668, lot L6, D1, decided
/// by S. Guillet, 2026-11-19). Immutable.
/// </summary>
/// <remarks>
/// <see cref="Step"/> is required (<see cref="ArgumentException"/> naming it when null). <see cref="Aberration"/> null is
/// <see cref="IO.Astrodynamics.Aberration.None"/>, <see cref="ConvergenceTolerance"/> null 1 µs (set on CSPICE's
/// <c>gftfov</c>, which cannot take it, it throws), <see cref="TargetShape"/> null an ellipsoid for a target with radii and
/// a point otherwise, <see cref="TargetFrame"/> null the target's own body-fixed frame.
/// </remarks>
public sealed record FieldOfViewSearchOptions
{
    private readonly TimeSpan? _step;
    private readonly TimeSpan? _convergenceTolerance;

    /// <summary>The default options, equal to <c>new()</c>: every setting unset.</summary>
    public static FieldOfViewSearchOptions Default { get; } = new();

    /// <summary>
    /// The step of the samples, positive; required: a search given none throws <see cref="ArgumentException"/> naming
    /// it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan? Step
    {
        get => _step;
        init => _step = WindowSearchSettings.PositiveOrNull(value, nameof(Step));
    }

    /// <summary>The aberration correction; null for <see cref="IO.Astrodynamics.Aberration.None"/>.</summary>
    public Aberration? Aberration { get; init; }

    /// <summary>
    /// The tolerance to which the library's search locates each transition, positive; null for 1 µs. Set on a search
    /// that CSPICE runs, it throws <see cref="ArgumentException"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan? ConvergenceTolerance
    {
        get => _convergenceTolerance;
        init => _convergenceTolerance = WindowSearchSettings.PositiveOrNull(value, nameof(ConvergenceTolerance));
    }

    /// <summary>The shape of the target; null for an ellipsoid when the target has radii, a point otherwise.</summary>
    public ShapeType? TargetShape { get; init; }

    /// <summary>The body-fixed frame of the target's ellipsoid, or null for the target's own frame; ignored for a point.</summary>
    public Frames.Frame TargetFrame { get; init; }
}

/// <summary>The settings the window search records share: their checks and their defaults (R15 of #668, lot L6).</summary>
internal static class WindowSearchSettings
{
    /// <summary>The default convergence tolerance, 1 µs: that of CSPICE's searches (<c>SPICE_GF_CNVTOL</c>).</summary>
    internal static readonly TimeSpan DefaultConvergenceTolerance = TimeSpan.FromTicks(10);

    /// <summary>A positive duration of a setting, or null, checked in init.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive (<paramref name="name"/>).</exception>
    internal static TimeSpan? PositiveOrNull(TimeSpan? value, string name)
    {
        if (value.HasValue && value.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be positive.");
        }

        return value;
    }

    /// <summary>
    /// The step of a search that infers none: required (choice 2 of R15 of #668, decided by S. Guillet, 2026-11-19).
    /// </summary>
    /// <exception cref="ArgumentException">The step is null (<c>options</c>).</exception>
    internal static TimeSpan RequireStep(TimeSpan? step, string record)
    {
        return step ?? throw new ArgumentException(
            $"{record}.Step is required: give the step of the samples, shorter than the shortest window or gap to find.", "options");
    }

    /// <summary>
    /// <see cref="WindowSearchOptions.AdjustValue"/> applies to the absolute extremes only (R15 of #668, decided by
    /// S. Guillet, 2026-11-19).
    /// </summary>
    /// <exception cref="ArgumentException">An adjustment value is given with another relation (<c>options</c>).</exception>
    internal static void CheckAdjustValue(WindowSearchOptions options, RelationnalOperator relationalOperator)
    {
        if (options.AdjustValue.HasValue && relationalOperator is not (RelationnalOperator.AbsoluteMin or RelationnalOperator.AbsoluteMax))
        {
            throw new ArgumentException(
                $"WindowSearchOptions.AdjustValue applies to AbsoluteMin and AbsoluteMax only, not to {relationalOperator}: leave it null.", "options");
        }
    }

    /// <summary>
    /// A convergence tolerance given to a search that CSPICE runs, whose tolerance is fixed at 1 µs (choice 6 of R15 of
    /// #668, decided by S. Guillet, 2026-11-19; no C++ change).
    /// </summary>
    /// <exception cref="ArgumentException">The tolerance is given (<c>options</c>).</exception>
    internal static void RejectToleranceOnCspice(TimeSpan? tolerance, string record, string routine)
    {
        if (tolerance.HasValue)
        {
            throw new ArgumentException(
                $"{record}.ConvergenceTolerance cannot apply: CSPICE's {routine} runs this search, at its fixed tolerance of 1 µs. Leave it null.",
                "options");
        }
    }

    /// <summary>
    /// The windows of a search on the observer labelled in the scale of the search window's start (R15 of #668, lot L6,
    /// decided by S. Guillet, 2026-11-19; IC-7 and IC-8): an end at a bound of the search window is that bound, as given;
    /// every other end the same instant in the scale of the start, but where no label of that scale holds the instant (an
    /// end inside a UTC leap second), which keeps its label.
    /// </summary>
    internal static IReadOnlyList<Window> InScaleOf(IReadOnlyList<Window> windows, in Window searchWindow)
    {
        var start = searchWindow.StartDate;
        var end = searchWindow.EndDate;
        return windows.Select(w => new Window(Label(w.StartDate), Label(w.EndDate))).ToArray();

        Time Label(Time t)
        {
            // A bound, as the CSPICE and occultation searches label it (its TDB conversion) or as given
            if (t == start || t.EqualsExact(start.ToTDB())) return start;
            if (t == end || t.EqualsExact(end.ToTDB())) return end;
            var label = t.ConvertTo(start.Scale);
            return label == t ? label : t;
        }
    }
}
