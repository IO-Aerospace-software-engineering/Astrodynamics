// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body.Spacecraft;

/// <summary>
/// The margin of a target in the field of view of an instrument at any epoch (<see cref="FieldOfViewGeometry"/>), as
/// CSPICE's <c>gftfov</c> sees it: the target's position with the full aberration correction, the stellar aberration of
/// "+S" included (<c>spkezp</c>), its ellipsoid oriented by its body-fixed frame at the light-time corrected epoch, and the
/// instrument's axes at the epoch itself. Immutable: it keeps no state between evaluations.
/// </summary>
/// <remarks>
/// The positions are those of <see cref="ApparentPositionReader"/>, with the stellar aberration, and with a
/// <see cref="Star"/> read as a direction, without light time, as CSPICE's <c>gfrfov</c> reads a ray. The states are read
/// through <see cref="ILocalizable.GetGeometricStateRelativeTo"/> and the axes of the field through the spacecraft's frame,
/// whose reads add entries to its orientations.
/// </remarks>
internal sealed class FieldOfViewEvaluator
{
    private readonly Instrument _instrument;
    private readonly OccultationBodyModel _target;
    private readonly ApparentPositionReader _reader;
    private readonly double _crossAngle;

    /// <summary>The evaluator of <paramref name="target"/> in the field of view of <paramref name="instrument"/>.</summary>
    /// <param name="instrument">The instrument; its spacecraft is the observer.</param>
    /// <param name="target">The target.</param>
    /// <param name="targetShape">The target's shape: a point, or the ellipsoid of its radii.</param>
    /// <param name="targetFrame">The body-fixed frame of the ellipsoid, or null for the body's own frame; ignored for a point.</param>
    /// <param name="aberration">The aberration correction.</param>
    /// <exception cref="ArgumentException">The target is the instrument's spacecraft, or is modeled as an ellipsoid without
    /// radii.</exception>
    /// <exception cref="InvalidOperationException">The field of view cannot be searched (<see cref="FieldOfViewSearch.CheckField"/>).</exception>
    internal FieldOfViewEvaluator(Instrument instrument, ILocalizable target, ShapeType targetShape, Frame targetFrame, Aberration aberration)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(target);
        FieldOfViewSearch.CheckTarget(instrument, target, targetShape);
        _crossAngle = FieldOfViewSearch.CheckField(instrument);
        _instrument = instrument;
        _target = new OccultationBodyModel(target, targetShape, targetFrame);
        _reader = new ApparentPositionReader(instrument.Spacecraft, aberration, true, true);
    }

    /// <summary>The margin at an epoch: positive in view, zero at the contact, continuous.</summary>
    /// <exception cref="InvalidOperationException">The observer is inside the ellipsoid of the target.</exception>
    internal double Margin(in Time epoch)
    {
        var observer = _reader.Observer(epoch);
        StateVector referenceAtEpoch = null;
        var position = _reader.Position(_target.Item, epoch, observer, ref referenceAtEpoch, out var orientationEpoch);
        var target = _target.ToBody(position, orientationEpoch);
        var axes = _instrument.GetFieldOfViewAxesInICRFFrame(epoch);
        return FieldOfViewGeometry.Margin(_instrument.Shape, _instrument.FieldOfView, _crossAngle, axes, target);
    }
}
