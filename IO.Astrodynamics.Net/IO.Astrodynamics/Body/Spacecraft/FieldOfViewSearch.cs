// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body.Spacecraft;

/// <summary>
/// The field-of-view search behind <see cref="Instrument.FindWindowsInFieldOfViewConstraint"/>: the managed search of the
/// library (<see cref="FieldOfViewEvaluator"/> and the root finder of the occultations), and the rules of the field of
/// view; CSPICE's <c>gftfov</c> is the other path.
/// </summary>
internal static class FieldOfViewSearch
{
    /// <summary>The convergence tolerance of the transitions, CSPICE's <c>SPICE_GF_CNVTOL</c>: 1 µs.</summary>
    internal static readonly TimeSpan ConvergenceTolerance = TimeSpan.FromTicks(10);

    // ZZGFFVIN rejects a field whose angular radius exceeds pi/2 - 1e-6 (SPICE(FOVTOOWIDE), zzgffvu.c, N0067)
    private const double MaximumAngularRadius = Constants.PI2 - 1e-6;

    /// <summary>
    /// The managed search: the windows, in TDB, where the target is in the field of view of the instrument, its
    /// spacecraft the observer.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The step is not positive.</exception>
    /// <exception cref="ArgumentException">The search window ends before it starts, the target is the instrument's
    /// spacecraft, or the target is modeled as an ellipsoid without radii.</exception>
    /// <exception cref="InvalidOperationException">The field of view cannot be searched (<see cref="CheckField"/>).</exception>
    internal static IReadOnlyList<Window> Managed(Instrument instrument, in Window searchWindow, ILocalizable target, ShapeType targetShape, Frame targetFrame,
        Aberration aberration, in TimeSpan stepSize)
    {
        var evaluator = new FieldOfViewEvaluator(instrument, target, targetShape, targetFrame, aberration);
        return OccultationFinder.FindWindows(t => evaluator.Margin(t), searchWindow, stepSize, ConvergenceTolerance);
    }

    /// <summary>The target: not the instrument's spacecraft.</summary>
    /// <exception cref="ArgumentException">The target is the instrument's spacecraft.</exception>
    internal static void CheckTarget(Instrument instrument, ILocalizable target)
    {
        if (target.NaifId == instrument.Spacecraft.NaifId)
        {
            throw new ArgumentException("The target cannot be the instrument's spacecraft.", nameof(target));
        }
    }

    /// <summary>
    /// The field of view as CSPICE accepts it (<c>getfov</c> and <c>zzgffvu</c>, N0067), and its half-angle across the
    /// reference plane: the <see cref="EllipticalInstrument.CrossAngle"/> or <see cref="RectangularInstrument.CrossAngle"/>,
    /// the <see cref="Instrument.FieldOfView"/> of a circle.
    /// </summary>
    /// <exception cref="InvalidOperationException">The boresight is zero, the reference vector is parallel to it, a
    /// half-angle is not positive, the angular radius of the field (the larger half-angle of a circle or an ellipse, the
    /// angle of the corner of a rectangle) exceeds π/2 − 1e-6, or the instrument is of a shape other than a circle without
    /// being an <see cref="EllipticalInstrument"/> or a <see cref="RectangularInstrument"/>, so that its cross angle is
    /// unknown.</exception>
    internal static double CheckField(Instrument instrument)
    {
        double crossAngle = instrument switch
        {
            EllipticalInstrument elliptical when instrument.Shape == InstrumentShape.Elliptical => elliptical.CrossAngle,
            RectangularInstrument rectangular when instrument.Shape == InstrumentShape.Rectangular => rectangular.CrossAngle,
            _ when instrument.Shape == InstrumentShape.Circular => instrument.FieldOfView,
            _ => throw new InvalidOperationException(
                $"The field of view of {instrument.Name} cannot be searched: its cross angle is unknown for the shape {instrument.Shape} of a {instrument.GetType().Name}.")
        };

        // getfov: the boresight is not zero, the reference vector not parallel to it (|b × ref| = 0)
        if (instrument.Boresight == Vector3.Zero || instrument.Boresight.Cross(instrument.RefVector).Magnitude() == 0.0)
        {
            throw new InvalidOperationException(
                $"The field of view of {instrument.Name} cannot be searched: its boresight is zero or parallel to its reference vector.");
        }

        if (!(instrument.FieldOfView > 0.0) || !(crossAngle > 0.0))
        {
            throw new InvalidOperationException($"The field of view of {instrument.Name} cannot be searched: its half-angles must be positive.");
        }

        double angularRadius = instrument.Shape != InstrumentShape.Rectangular ? System.Math.Max(instrument.FieldOfView, crossAngle)
            : instrument.FieldOfView >= Constants.PI2 || crossAngle >= Constants.PI2 ? Constants.PI2
            : System.Math.Atan(System.Math.Sqrt(System.Math.Pow(System.Math.Tan(instrument.FieldOfView), 2) + System.Math.Pow(System.Math.Tan(crossAngle), 2)));
        if (angularRadius > MaximumAngularRadius)
        {
            throw new InvalidOperationException(
                $"The field of view of {instrument.Name} cannot be searched: its angular radius {angularRadius} rad exceeds pi/2 - 1e-6, the bound of CSPICE's gftfov.");
        }

        return crossAngle;
    }
}
