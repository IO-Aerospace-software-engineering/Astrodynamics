using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Pre-computed cache of body-fixed frame orientations on a regular time grid.
/// Uses SLERP interpolation for sub-grid queries, eliminating SPICE calls during integration.
/// </summary>
/// <remarks>
/// A grid point SPICE cannot give (outside the coverage of the frame's orientation data, where
/// <see cref="SpiceAPI.TransformFrame(Time, Frame, Frame)"/> throws since #454) is kept as unavailable, and a read that
/// needs it throws: a propagation whose forces need no orientation of the central body (a point mass, no drag) still runs
/// there, as it did when the native layer returned the zero rotation.
/// </remarks>
public sealed class PropagationFrameOrientationCache
{
    private readonly record struct OrientationPoint(
        Quaternion Rotation, Vector3 AngularVelocity, double EpochSeconds);

    private readonly OrientationPoint[] _points;
    // The message of the first grid point SPICE could not give, and which points it could not give; null when it gave all
    private readonly string _unavailableMessage;
    private readonly bool[] _unavailable;
    private readonly double _gridStepSeconds;
    private readonly double _startEpochSeconds;
    private readonly int _gridSize;
    private readonly Frame _frame;

    private const int BufferPoints = 2;

    /// <summary>
    /// Create a frame orientation cache for the given body-fixed frame over the specified time window.
    /// </summary>
    /// <param name="window">Propagation time window (TDB).</param>
    /// <param name="bodyFixedFrame">The body-fixed frame to cache orientations for.</param>
    /// <param name="gridStep">Time between grid points (default: 60 seconds).</param>
    public PropagationFrameOrientationCache(
        Window window, Frame bodyFixedFrame, TimeSpan gridStep)
    {
        _frame = bodyFixedFrame ?? throw new ArgumentNullException(nameof(bodyFixedFrame));
        _gridStepSeconds = gridStep.TotalSeconds;
        if (_gridStepSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(gridStep), "Grid step must be positive.");

        double windowStart = window.StartDate.TimeSpanFromJ2000().TotalSeconds;
        double windowEnd = window.EndDate.TimeSpanFromJ2000().TotalSeconds;

        _startEpochSeconds = windowStart - BufferPoints * _gridStepSeconds;
        double endEpochSeconds = windowEnd + BufferPoints * _gridStepSeconds;
        _gridSize = (int)System.Math.Ceiling((endEpochSeconds - _startEpochSeconds) / _gridStepSeconds) + 1;

        _points = new OrientationPoint[_gridSize];
        for (int i = 0; i < _gridSize; i++)
        {
            double epochSec = _startEpochSeconds + i * _gridStepSeconds;
            var epoch = Time.CreateTDB(epochSec);
            StateOrientation so;
            try
            {
                so = bodyFixedFrame.GetStateOrientationToICRF(epoch);
            }
            catch (InvalidOperationException exception)
            {
                _unavailable ??= new bool[_gridSize];
                _unavailable[i] = true;
                _unavailableMessage ??= exception.Message;
                _points[i] = new OrientationPoint(Quaternion.Zero, Vector3.Zero, epochSec);
                continue;
            }

            _points[i] = new OrientationPoint(so.Rotation, so.AngularVelocity, epochSec);
        }
    }

    /// <summary>
    /// Get interpolated orientation at the given epoch using SLERP for rotation
    /// and linear interpolation for angular velocity.
    /// </summary>
    public StateOrientation GetOrientation(in Time epoch)
    {
        Locate(epoch, out var p0, out var p1, out double ratio);
        var rotation = p0.Rotation.SLERP(p1.Rotation, ratio);
        var angularVelocity = p0.AngularVelocity.LinearInterpolation(p1.AngularVelocity, ratio);

        return new StateOrientation(rotation, angularVelocity, epoch, Frame.ICRF);
    }

    /// <summary>
    /// The rotation of <see cref="GetOrientation"/>, the same doubles, without allocating.
    /// </summary>
    internal Quaternion GetRotation(in Time epoch)
    {
        Locate(epoch, out var p0, out var p1, out double ratio);
        return p0.Rotation.SLERP(p1.Rotation, ratio);
    }

    /// <summary>
    /// The rotation and the angular velocity of <see cref="GetOrientation"/>, the same doubles, without allocating.
    /// </summary>
    internal void GetRotationAndAngularVelocity(in Time epoch, out Quaternion rotation, out Vector3 angularVelocity)
    {
        Locate(epoch, out var p0, out var p1, out double ratio);
        rotation = p0.Rotation.SLERP(p1.Rotation, ratio);
        angularVelocity = p0.AngularVelocity.LinearInterpolation(p1.AngularVelocity, ratio);
    }

    /// <summary>
    /// The grid points around <paramref name="epoch"/> and its position between them, in [0, 1].
    /// </summary>
    private void Locate(in Time epoch, out OrientationPoint p0, out OrientationPoint p1, out double ratio)
    {
        double t = epoch.TimeSpanFromJ2000().TotalSeconds;

        int idx = (int)((t - _startEpochSeconds) / _gridStepSeconds);
        idx = System.Math.Clamp(idx, 0, _gridSize - 2);

        if (_unavailable != null && (_unavailable[idx] || _unavailable[idx + 1]))
        {
            throw new InvalidOperationException(
                $"The orientation of {_frame.Name} is not available at {epoch}: {_unavailableMessage}");
        }

        p0 = _points[idx];
        p1 = _points[idx + 1];

        double dt = p1.EpochSeconds - p0.EpochSeconds;
        ratio = dt > 0 ? (t - p0.EpochSeconds) / dt : 0.0;
        ratio = System.Math.Clamp(ratio, 0.0, 1.0);
    }
}
