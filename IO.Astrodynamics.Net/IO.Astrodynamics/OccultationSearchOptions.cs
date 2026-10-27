// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Frames;

namespace IO.Astrodynamics;

/// <summary>
/// Options of an occultation search (<c>FindWindowsOnOccultationConstraint</c>): the body-fixed frames that orient the
/// ellipsoids, and the convergence tolerance of the transitions. Immutable.
/// </summary>
/// <remarks>
/// <para>
/// A body modeled as an ellipsoid takes the radii of its kernels (<c>BODYnnn_RADII</c>) along the axes of a body-fixed
/// frame: by default the body's own frame (<c>CelestialBody.Frame</c>, which for the Earth is <c>ITRF93</c> when
/// <c>earth_assoc_itrf93.tf</c> is loaded and <c>IAU_EARTH</c> otherwise), or the frame given here, as the
/// <c>fframe</c> and <c>bframe</c> arguments of CSPICE's <c>gfoclt</c>. A spherical body needs no frame.
/// </para>
/// <para>
/// The search of the library (managed) locates each transition to <see cref="ConvergenceTolerance"/>. A search that
/// CSPICE runs (both the observer and the target loaded from kernels, one of them a spacecraft) uses <c>gfoclt</c>,
/// whose convergence tolerance is fixed at 1e-6 s (<c>SPICE_GF_CNVTOL</c>); it honors the frames.
/// </para>
/// </remarks>
public sealed record OccultationSearchOptions
{
    /// <summary>The default convergence tolerance, 1 µs: that of CSPICE's <c>gfoclt</c> (<c>SPICE_GF_CNVTOL</c>).</summary>
    public static readonly TimeSpan DefaultConvergenceTolerance = TimeSpan.FromTicks(10);

    private readonly TimeSpan _convergenceTolerance = DefaultConvergenceTolerance;

    /// <summary>The default options: the bodies' own frames, a tolerance of 1 µs.</summary>
    public static OccultationSearchOptions Default { get; } = new();

    /// <summary>
    /// The body-fixed frame of the front (occulting) body's ellipsoid, or null for the body's own frame.
    /// </summary>
    public Frame FrontFrame { get; init; }

    /// <summary>
    /// The body-fixed frame of the target (occulted, back) body's ellipsoid, or null for the body's own frame.
    /// </summary>
    public Frame TargetFrame { get; init; }

    /// <summary>
    /// The tolerance to which the search locates each transition, positive; 1 µs by default. A <see cref="Time"/>
    /// resolves 100 ns, the floor of a useful tolerance.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The tolerance is not positive.</exception>
    public TimeSpan ConvergenceTolerance
    {
        get => _convergenceTolerance;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The convergence tolerance must be positive.");
            }

            _convergenceTolerance = value;
        }
    }
}
