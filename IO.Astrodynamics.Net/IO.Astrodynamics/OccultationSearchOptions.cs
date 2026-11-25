// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Frames;

namespace IO.Astrodynamics;

/// <summary>
/// Options of an occultation search: the step, the aberration correction, the shapes and body-fixed frames of the
/// bodies, the type of occultation and the convergence tolerance of the transitions (R15 of #668, lot L6, D1, decided by
/// S. Guillet, 2026-11-19). Immutable.
/// </summary>
/// <remarks>
/// <para>
/// A body modeled as an ellipsoid takes the radii of its kernels (<c>BODYnnn_RADII</c>) along the axes of a body-fixed
/// frame: by default the body's own frame (<c>CelestialBody.Frame</c>, which for the Earth is <c>ITRF93</c> when
/// <c>earth_assoc_itrf93.tf</c> is loaded and <c>IAU_EARTH</c> otherwise), or the frame given here, as the
/// <c>fframe</c> and <c>bframe</c> arguments of CSPICE's <c>gfoclt</c>. A spherical body needs no frame. A null shape is an
/// ellipsoid for a body with radii and a point otherwise; a null aberration is <see cref="IO.Astrodynamics.Aberration.None"/>.
/// </para>
/// <para>
/// The search of the library (managed) locates each transition to <see cref="ConvergenceTolerance"/>, 1 µs when null. A
/// search that CSPICE runs uses <c>gfoclt</c>, whose convergence tolerance is fixed at 1e-6 s (<c>SPICE_GF_CNVTOL</c>):
/// a tolerance set on it throws <see cref="ArgumentException"/> on the searches on the observer.
/// </para>
/// </remarks>
public sealed record OccultationSearchOptions
{
    /// <summary>The default convergence tolerance, 1 µs: that of CSPICE's <c>gfoclt</c> (<c>SPICE_GF_CNVTOL</c>).</summary>
    internal static readonly TimeSpan DefaultConvergenceTolerance = WindowSearchSettings.DefaultConvergenceTolerance;

    private readonly TimeSpan? _step;
    private readonly TimeSpan? _convergenceTolerance;

    /// <summary>The default options, equal to <c>new()</c>: every setting unset, any occultation.</summary>
    public static OccultationSearchOptions Default { get; } = new();

    /// <summary>
    /// The step of the samples, positive; null for the step the search infers where it can, required otherwise
    /// (<see cref="ArgumentException"/> naming it).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan? Step
    {
        get => _step;
        init => _step = WindowSearchSettings.PositiveOrNull(value, nameof(Step));
    }

    /// <summary>
    /// The aberration correction, none or a light time (LT, CN, XLT, XCN; the stellar aberration of the "+S"
    /// corrections is ignored, as <c>gfoclt</c> does); null for <see cref="IO.Astrodynamics.Aberration.None"/>.
    /// </summary>
    public Aberration? Aberration { get; init; }

    /// <summary>The shape of the target (the back body); null for an ellipsoid when it has radii, a point otherwise.</summary>
    public ShapeType? TargetShape { get; init; }

    /// <summary>The shape of the front (occulting) body; null for an ellipsoid when it has radii, a point otherwise.</summary>
    public ShapeType? FrontShape { get; init; }

    /// <summary>The type of occultation, <see cref="IO.Astrodynamics.OccultationType.Any"/> by default.</summary>
    public OccultationType OccultationType { get; init; } = OccultationType.Any;

    /// <summary>
    /// The body-fixed frame of the front (occulting) body's ellipsoid, or null for the body's own frame.
    /// </summary>
    public Frame FrontFrame { get; init; }

    /// <summary>
    /// The body-fixed frame of the target (occulted, back) body's ellipsoid, or null for the body's own frame.
    /// </summary>
    public Frame TargetFrame { get; init; }

    /// <summary>
    /// The tolerance to which the library's search locates each transition, positive; null for 1 µs. A
    /// <see cref="TimeSystem.Time"/> resolves 100 ns, the floor of a useful tolerance.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The tolerance is not positive.</exception>
    public TimeSpan? ConvergenceTolerance
    {
        get => _convergenceTolerance;
        init => _convergenceTolerance = WindowSearchSettings.PositiveOrNull(value, nameof(ConvergenceTolerance));
    }
}
