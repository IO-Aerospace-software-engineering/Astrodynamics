// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.OrbitalParameters.TLE;

/// <summary>
/// The settings of the fit of <see cref="OrbitalParameters.ToTle"/> and the fields it writes besides the elements, in the
/// order and with the meaning of <see cref="TLE.Create"/>'s (decision 113 of #668, decided by S. Guillet, 2026-11-26: the
/// record <c>TLE.Configuration</c> before 11.0.0, whose name, NORAD id and COSPAR id are now parameters of
/// <c>ToTle</c> and whose <c>Tolerance</c>, never read, is removed, #833). Every default is the former one.
/// </summary>
public sealed record TleFitOptions
{
    /// <summary>The options of every default, as <c>new()</c>.</summary>
    public static TleFitOptions Default { get; } = new();

    /// <summary>The revolution number at epoch, 0 to 99999 (default 0), checked by <see cref="TLE.Create"/>.</summary>
    public int RevolutionNumberAtEpoch { get; init; }

    /// <summary>The security classification of the object (default <c>Unclassified</c>).</summary>
    public Classification Classification { get; init; } = Classification.Unclassified;

    /// <summary>The B* drag term, in 1/earth radii (default 0.0001), the initial value of the fit and the field written.</summary>
    public double BStar { get; init; } = 0.0001;

    /// <summary>The first-derivative field, ṅ/2, in revolutions/day² (default 0.0), as OMM's <c>MEAN_MOTION_DOT</c>
    /// (#605).</summary>
    public double MeanMotionDot { get; init; }

    /// <summary>The second-derivative field, n̈/6, in revolutions/day³ (default 0.0), as OMM's <c>MEAN_MOTION_DDOT</c>
    /// (#605).</summary>
    public double MeanMotionDDot { get; init; }

    /// <summary>The element set number, 0 to 9999 (default <see cref="TLE.DefaultElementSetNumber"/>, 999, #606), checked by
    /// <see cref="TLE.Create"/>.</summary>
    public int ElementSetNumber { get; init; } = TLE.DefaultElementSetNumber;

    private readonly int _maxIterations = 15;

    /// <summary>The maximum number of iterations of the fit (default 15). An <c>int</c> (a <c>ushort</c> before 11.0.0,
    /// decision 113 of #668).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown at init for a negative value, which the former <c>ushort</c>
    /// could not hold.</exception>
    public int MaxIterations
    {
        get => _maxIterations;
        init => _maxIterations = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(MaxIterations), value, "The maximum number of iterations cannot be negative.");
    }
}
