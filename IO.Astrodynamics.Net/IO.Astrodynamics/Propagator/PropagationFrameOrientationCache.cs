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
/// <para>
/// A grid point SPICE cannot give (outside the coverage of the frame's orientation data, where
/// <see cref="SpiceAPI.TransformFrame(Time, Frame, Frame)"/> throws since #454) is kept as unavailable: a propagation
/// whose forces need no orientation of the central body (a point mass, no drag) still runs there, as it did when the
/// native layer returned the zero rotation.
/// </para>
/// <para>
/// When the coverage ends between the window end and the next grid point, that point is replaced by the orientation read
/// once at the window end (and likewise at the window start when the coverage starts between the previous grid point and
/// it), so a read inside the window interpolates over the shortened interval. A read whose interval still has an
/// unavailable point, or which lies past such a boundary point, reads the frame's orientation at its epoch from the data
/// provider, never through this cache: it throws, with SPICE's message, only when SPICE has no orientation at that epoch.
/// A grid SPICE gives entirely reads nothing and allocates nothing.
/// </para>
/// </remarks>
public sealed class PropagationFrameOrientationCache
{
    private readonly record struct OrientationPoint(
        Quaternion Rotation, Vector3 AngularVelocity, double EpochSeconds);

    private readonly OrientationPoint[] _points;
    // Which points SPICE could not give; null when it gave all
    private readonly bool[] _unavailable;
    // The span the grid interpolates when a boundary point was read at the window start or end (KR-1 of lot 6, #457)
    private readonly double _interpolatedFromSeconds = double.NegativeInfinity;
    private readonly double _interpolatedToSeconds = double.PositiveInfinity;
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
                _points[i] = new OrientationPoint(Quaternion.Zero, Vector3.Zero, epochSec);
                continue;
            }

            _points[i] = new OrientationPoint(so.Rotation, so.AngularVelocity, epochSec);
        }

        if (_unavailable == null)
        {
            return;
        }

        // The coverage starts between the last grid point before the window start and it: that point becomes the
        // orientation at the window start
        int before = LastPointAtOrBefore(windowStart);
        if (before >= 0 && _unavailable[before] && _points[before].EpochSeconds < windowStart &&
            TryReadBoundary(windowStart, before))
        {
            _interpolatedFromSeconds = windowStart;
        }

        // The coverage ends between the window end and the first grid point after it: that point becomes the orientation
        // at the window end
        int after = LastPointAtOrBefore(windowEnd) + 1;
        if (after < _gridSize && _unavailable[after] && TryReadBoundary(windowEnd, after))
        {
            _interpolatedToSeconds = windowEnd;
        }
    }

    // The index of the last grid point at or before the epoch, -1 when there is none
    private int LastPointAtOrBefore(double epochSeconds)
    {
        int index = -1;
        while (index + 1 < _gridSize && _points[index + 1].EpochSeconds <= epochSeconds)
        {
            index++;
        }

        return index;
    }

    // Replaces the unavailable point at index by the orientation at the epoch, when SPICE gives it
    private bool TryReadBoundary(double epochSeconds, int index)
    {
        StateOrientation so;
        try
        {
            so = _frame.ReadStateOrientationToICRF(Time.CreateTDB(epochSeconds));
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        _points[index] = new OrientationPoint(so.Rotation, so.AngularVelocity, epochSeconds);
        _unavailable[index] = false;
        return true;
    }

    /// <summary>
    /// Get interpolated orientation at the given epoch using SLERP for rotation
    /// and linear interpolation for angular velocity.
    /// </summary>
    public StateOrientation GetOrientation(in Time epoch)
    {
        if (!Locate(epoch, out var p0, out var p1, out double ratio))
        {
            var read = ReadAt(epoch);
            return new StateOrientation(read.Rotation, read.AngularVelocity, epoch, Frame.ICRF);
        }

        var rotation = p0.Rotation.SLERP(p1.Rotation, ratio);
        var angularVelocity = p0.AngularVelocity.LinearInterpolation(p1.AngularVelocity, ratio);

        return new StateOrientation(rotation, angularVelocity, epoch, Frame.ICRF);
    }

    /// <summary>
    /// The rotation of <see cref="GetOrientation"/>, the same doubles, without allocating.
    /// </summary>
    internal Quaternion GetRotation(in Time epoch)
    {
        if (!Locate(epoch, out var p0, out var p1, out double ratio))
        {
            return ReadAt(epoch).Rotation;
        }

        return p0.Rotation.SLERP(p1.Rotation, ratio);
    }

    /// <summary>
    /// The rotation and the angular velocity of <see cref="GetOrientation"/>, the same doubles, without allocating.
    /// </summary>
    internal void GetRotationAndAngularVelocity(in Time epoch, out Quaternion rotation, out Vector3 angularVelocity)
    {
        if (!Locate(epoch, out var p0, out var p1, out double ratio))
        {
            var read = ReadAt(epoch);
            rotation = read.Rotation;
            angularVelocity = read.AngularVelocity;
            return;
        }

        rotation = p0.Rotation.SLERP(p1.Rotation, ratio);
        angularVelocity = p0.AngularVelocity.LinearInterpolation(p1.AngularVelocity, ratio);
    }

    /// <summary>
    /// The grid points around <paramref name="epoch"/> and its position between them, in [0, 1]; false when the grid
    /// cannot give it (an unavailable point, or past a boundary point read at the window start or end).
    /// </summary>
    private bool Locate(in Time epoch, out OrientationPoint p0, out OrientationPoint p1, out double ratio)
    {
        double t = epoch.TimeSpanFromJ2000().TotalSeconds;

        int idx = (int)((t - _startEpochSeconds) / _gridStepSeconds);
        idx = System.Math.Clamp(idx, 0, _gridSize - 2);

        if (_unavailable != null && (_unavailable[idx] || _unavailable[idx + 1] || t < _interpolatedFromSeconds ||
                                     t > _interpolatedToSeconds))
        {
            p0 = default;
            p1 = default;
            ratio = 0.0;
            return false;
        }

        p0 = _points[idx];
        p1 = _points[idx + 1];

        double dt = p1.EpochSeconds - p0.EpochSeconds;
        ratio = dt > 0 ? (t - p0.EpochSeconds) / dt : 0.0;
        ratio = System.Math.Clamp(ratio, 0.0, 1.0);
        return true;
    }

    // The orientation at the epoch from the data provider, never through this cache (the frame's own read would come back
    // here while the cache is active): it throws only when SPICE has no orientation at the epoch
    private StateOrientation ReadAt(in Time epoch)
    {
        try
        {
            return _frame.ReadStateOrientationToICRF(epoch);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The orientation of {_frame.Name} is not available at {epoch}: {exception.Message}", exception);
        }
    }
}
