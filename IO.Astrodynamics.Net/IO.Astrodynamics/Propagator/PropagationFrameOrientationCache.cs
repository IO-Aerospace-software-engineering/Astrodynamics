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
public sealed class PropagationFrameOrientationCache
{
    private readonly record struct OrientationPoint(
        Quaternion Rotation, Vector3 AngularVelocity, double EpochSeconds);

    private readonly OrientationPoint[] _points;
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
            var so = bodyFixedFrame.GetStateOrientationToICRF(epoch);
            _points[i] = new OrientationPoint(so.Rotation, so.AngularVelocity, epochSec);
        }
    }

    /// <summary>
    /// Get interpolated orientation at the given epoch using SLERP for rotation
    /// and linear interpolation for angular velocity.
    /// </summary>
    public StateOrientation GetOrientation(in Time epoch)
    {
        double t = epoch.TimeSpanFromJ2000().TotalSeconds;

        int idx = (int)((t - _startEpochSeconds) / _gridStepSeconds);
        idx = System.Math.Clamp(idx, 0, _gridSize - 2);

        var p0 = _points[idx];
        var p1 = _points[idx + 1];

        double dt = p1.EpochSeconds - p0.EpochSeconds;
        double ratio = dt > 0 ? (t - p0.EpochSeconds) / dt : 0.0;
        ratio = System.Math.Clamp(ratio, 0.0, 1.0);

        var rotation = p0.Rotation.SLERP(p1.Rotation, ratio);
        var angularVelocity = p0.AngularVelocity.LinearInterpolation(p1.AngularVelocity, ratio);

        return new StateOrientation(rotation, angularVelocity, epoch, Frame.ICRF);
    }
}
