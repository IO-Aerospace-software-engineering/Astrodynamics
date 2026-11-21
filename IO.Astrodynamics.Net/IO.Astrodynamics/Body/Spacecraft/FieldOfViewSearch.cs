// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body.Spacecraft;

/// <summary>
/// The field-of-view search behind <see cref="Instrument.FindWindowsInFieldOfViewConstraint"/>: the managed search of the
/// library (<see cref="FieldOfViewEvaluator"/> and the root finder of the occultations), and the rules both paths apply
/// before the search, CSPICE's <c>gftfov</c> on the other one.
/// </summary>
internal static class FieldOfViewSearch
{
    /// <summary>The convergence tolerance of the transitions, CSPICE's <c>SPICE_GF_CNVTOL</c>: 1 µs.</summary>
    internal static readonly TimeSpan ConvergenceTolerance = TimeSpan.FromTicks(10);

    // ZZGFFVIN rejects a field whose angular radius exceeds pi/2 - 1e-6 (SPICE(FOVTOOWIDE), zzgffvu.c, N0067)
    private const double MaximumAngularRadius = Constants.HalfPi - 1e-6;

    /// <summary>
    /// The managed search: the windows, in TDB, where the target is in the field of view of the instrument, its
    /// spacecraft the observer.
    /// </summary>
    /// <exception cref="ArgumentException">An argument of <see cref="Check"/>, or the search window leaves the propagated
    /// states of the observer or of a spacecraft target (<see cref="CheckCoverage"/>).</exception>
    /// <exception cref="InvalidOperationException">The field of view cannot be searched (<see cref="CheckField"/>), or the
    /// spacecraft has no attitude (<see cref="CheckAttitude"/>).</exception>
    internal static IReadOnlyList<Window> Managed(Instrument instrument, in Window searchWindow, ILocalizable target, ShapeType targetShape, Frame targetFrame,
        Aberration aberration, in TimeSpan stepSize)
    {
        Check(instrument, searchWindow, target, targetShape, stepSize);
        CheckCoverage(instrument.Spacecraft, searchWindow);
        if (target is Spacecraft spacecraft)
        {
            CheckCoverage(spacecraft, searchWindow);
        }

        var evaluator = new FieldOfViewEvaluator(instrument, target, targetShape, targetFrame, aberration);
        return OccultationFinder.FindWindows(t => evaluator.Margin(t), searchWindow, stepSize, ConvergenceTolerance);
    }

    /// <summary>
    /// The rules of a search, checked before either path: the step, the search window, the target and its shape, the field
    /// of view, so that both paths throw the same exception for them, whatever <c>gftfov</c> would signal (an error it
    /// signals throws <see cref="InvalidOperationException"/> with SPICE's message since the fourth round of #668; before,
    /// the native proxy dropped it and returned no window).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The step is not positive.</exception>
    /// <exception cref="ArgumentException">The search window ends before it starts, the target is the instrument's
    /// spacecraft, or the target is modeled as an ellipsoid without radii.</exception>
    /// <exception cref="InvalidOperationException">The field of view cannot be searched (<see cref="CheckField"/>).</exception>
    internal static void Check(Instrument instrument, in Window searchWindow, ILocalizable target, ShapeType targetShape, in TimeSpan stepSize)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(target);
        OccultationFinder.CheckStep(stepSize, ConvergenceTolerance);
        OccultationFinder.CheckWindow(searchWindow);
        CheckTarget(instrument, target, targetShape);
        CheckField(instrument);
    }

    /// <summary>The target: not the instrument's spacecraft, and with radii when it is an ellipsoid.</summary>
    /// <exception cref="ArgumentException">The target is the instrument's spacecraft, or is modeled as an ellipsoid without
    /// radii (a site, a spacecraft, a barycenter, a Lagrange point, a star).</exception>
    internal static void CheckTarget(Instrument instrument, ILocalizable target, ShapeType targetShape)
    {
        if (target.NaifId == instrument.Spacecraft.NaifId)
        {
            throw new ArgumentException("The target cannot be the instrument's spacecraft.", nameof(target));
        }

        OccultationBodyModel.CheckShape(target, targetShape);
    }

    /// <summary>
    /// The attitude of the instrument's spacecraft: a managed spacecraft whose attitude record is empty, never propagated
    /// and with no orientation added, built from a TLE or propagated by the TLE propagator, has none to search with; its
    /// CK, which <c>gftfov</c> would read, cannot be written either (<see cref="SpacecraftFrame.WriteOrientation"/>). Before
    /// 11.0.0 the identity of ICRF was searched (#682). A kernel-backed spacecraft reads its CK through SPICE.
    /// </summary>
    /// <exception cref="InvalidOperationException">The spacecraft is managed and its attitude record is empty.</exception>
    internal static void CheckAttitude(Instrument instrument)
    {
        if (instrument.Spacecraft.Frame is SpacecraftFrame frame && !frame.HasAttitude)
        {
            throw new InvalidOperationException(
                $"Spacecraft {instrument.Spacecraft.Name} has no attitude to search the field of view of {instrument.Name} with: it has not been propagated, and no orientation was added to its frame (SpacecraftFrame.AddStateOrientationToICRF).");
        }
    }

    /// <summary>
    /// The field of view as CSPICE accepts it (<c>getfov</c> and <c>zzgffvu</c>, N0067), and its half-angle across the
    /// reference plane: the <see cref="EllipticalInstrument.CrossAngle"/> or <see cref="RectangularInstrument.CrossAngle"/>,
    /// the <see cref="Instrument.FieldOfView"/> of a circle.
    /// </summary>
    /// <remarks>
    /// The constructor of <see cref="Instrument"/> rejects the vectors <c>getfov</c> rejects, a zero or non-finite boresight
    /// or reference vector and a reference vector parallel to the boresight, and a field of view that is not a finite
    /// positive number (#707), and those of <see cref="EllipticalInstrument"/> and <see cref="RectangularInstrument"/> a
    /// cross angle that is not one (#715): before 11.0.0 this check threw for the zero and parallel vectors and for a cross
    /// angle of 0 at search time.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The angular radius of the field (the
    /// larger half-angle of a circle or an ellipse, the angle of the corner of a rectangle) exceeds π/2 − 1e-6, or the
    /// instrument is of a shape other than a circle without being an <see cref="EllipticalInstrument"/> or a
    /// <see cref="RectangularInstrument"/>, so that its cross angle is unknown.</exception>
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

        // The boresight, the reference vector, the field of view and the cross angle are finite, and the half-angles positive,
        // checked by the constructors (#707, #715): the cross angle of a circle is its field of view
        double angularRadius = instrument.Shape != InstrumentShape.Rectangular ? System.Math.Max(instrument.FieldOfView, crossAngle)
            : instrument.FieldOfView >= Constants.HalfPi || crossAngle >= Constants.HalfPi ? Constants.HalfPi
            : System.Math.Atan(System.Math.Sqrt(System.Math.Pow(System.Math.Tan(instrument.FieldOfView), 2) + System.Math.Pow(System.Math.Tan(crossAngle), 2)));
        if (angularRadius > MaximumAngularRadius)
        {
            throw new InvalidOperationException(
                $"The field of view of {instrument.Name} cannot be searched: its angular radius {angularRadius} rad exceeds pi/2 - 1e-6, the bound of CSPICE's gftfov.");
        }

        return crossAngle;
    }

    /// <summary>
    /// The search window within the propagated states of a spacecraft that <see cref="Spacecraft.GetGeometricStateRelativeTo(in IO.Astrodynamics.TimeSystem.Time, IO.Astrodynamics.Body.CelestialItem)"/>
    /// would otherwise extrapolate; nothing to check for a kernel-backed spacecraft, a TLE, or a spacecraft whose initial
    /// orbital parameters are propagated in two-body motion (<see cref="Spacecraft.InterpolatedStatesWindow"/>). The light
    /// time of a spacecraft target reads it up to the light time before the start of the search window (reception) or
    /// after its end (transmission, XLT and XCN), which its propagated states allow (#527, decided by S. Guillet,
    /// 2026-11-17).
    /// </summary>
    /// <exception cref="ArgumentException">The search window leaves the propagated states.</exception>
    internal static void CheckCoverage(Spacecraft spacecraft, in Window searchWindow)
    {
        spacecraft.CheckInterpolatedStatesCover(searchWindow, "search window", nameof(searchWindow));
    }
}
