// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Occultation;

/// <summary>A body of an occultation: its shape, its radii and the body-fixed frame of its ellipsoid. Immutable.</summary>
internal sealed class OccultationBodyModel
{
    private readonly Vector3 _radii;
    private readonly Frame _frame;
    private readonly bool _isSphere;

    /// <summary>A body of an occultation.</summary>
    /// <param name="item">The body.</param>
    /// <param name="shape">Its shape: a point, or the ellipsoid of its radii (a <see cref="CelestialBody"/>).</param>
    /// <param name="frame">The body-fixed frame of the ellipsoid, or null for the body's own frame.</param>
    /// <exception cref="ArgumentException">An ellipsoid is asked for a body without radii.</exception>
    internal OccultationBodyModel(ILocalizable item, ShapeType shape, Frame frame)
    {
        Item = item;
        IsPoint = shape == ShapeType.Point;
        if (IsPoint)
        {
            return;
        }

        if (item is not CelestialBody body || !(body.Radii.X > 0.0 && body.Radii.Y > 0.0 && body.Radii.Z > 0.0))
        {
            throw new ArgumentException($"{item.Name} has no radii: it cannot be modeled as an ellipsoid.");
        }

        _radii = body.Radii;
        _isSphere = _radii.X == _radii.Y && _radii.Y == _radii.Z;
        _frame = frame ?? body.Frame;
    }

    /// <summary>The body.</summary>
    internal ILocalizable Item { get; }

    /// <summary>Whether the body is modeled as a point.</summary>
    internal bool IsPoint { get; }

    /// <summary>
    /// The body at an instant: its center at <paramref name="position"/> from the observer, its ellipsoid oriented by
    /// its frame at <paramref name="orientationEpoch"/> (not read for a sphere).
    /// </summary>
    internal OccultationBody ToBody(in Vector3 position, in Time orientationEpoch)
    {
        if (IsPoint)
        {
            return OccultationBody.Point(position);
        }

        if (_isSphere)
        {
            // A sphere needs no orientation
            return OccultationBody.Ellipsoid(position, new Vector3(_radii.X, 0.0, 0.0), new Vector3(0.0, _radii.X, 0.0),
                new Vector3(0.0, 0.0, _radii.X));
        }

        var rotation = _frame.GetRotationToICRF(orientationEpoch);
        return OccultationBody.Ellipsoid(position, Vector3.VectorX.Rotate(rotation) * _radii.X,
            Vector3.VectorY.Rotate(rotation) * _radii.Y, Vector3.VectorZ.Rotate(rotation) * _radii.Z);
    }
}
