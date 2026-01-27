// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Represents the state vector section of an OPM message in CCSDS native units.
/// </summary>
/// <remarks>
/// This is a data transfer object that stores position and velocity in CCSDS units (km, km/s).
/// Use <see cref="ToStateVector"/> to convert to the framework's internal StateVector (meters, m/s).
/// Use <see cref="FromStateVector"/> to create from a framework StateVector.
/// </remarks>
public class OpmStateVector
{
    /// <summary>
    /// Gets the epoch of the state vector.
    /// </summary>
    public DateTime Epoch { get; }

    /// <summary>
    /// Gets the X position component in kilometers.
    /// </summary>
    public double X { get; }

    /// <summary>
    /// Gets the Y position component in kilometers.
    /// </summary>
    public double Y { get; }

    /// <summary>
    /// Gets the Z position component in kilometers.
    /// </summary>
    public double Z { get; }

    /// <summary>
    /// Gets the X velocity component in kilometers per second.
    /// </summary>
    public double XDot { get; }

    /// <summary>
    /// Gets the Y velocity component in kilometers per second.
    /// </summary>
    public double YDot { get; }

    /// <summary>
    /// Gets the Z velocity component in kilometers per second.
    /// </summary>
    public double ZDot { get; }

    /// <summary>
    /// Gets optional comments associated with the state vector.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpmStateVector"/> class.
    /// </summary>
    /// <param name="epoch">The epoch of the state vector.</param>
    /// <param name="x">X position in km.</param>
    /// <param name="y">Y position in km.</param>
    /// <param name="z">Z position in km.</param>
    /// <param name="xDot">X velocity in km/s.</param>
    /// <param name="yDot">Y velocity in km/s.</param>
    /// <param name="zDot">Z velocity in km/s.</param>
    /// <param name="comments">Optional comments.</param>
    public OpmStateVector(
        DateTime epoch,
        double x, double y, double z,
        double xDot, double yDot, double zDot,
        IReadOnlyList<string> comments = null)
    {
        Epoch = epoch;
        X = x;
        Y = y;
        Z = z;
        XDot = xDot;
        YDot = yDot;
        ZDot = zDot;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Converts this OPM state vector to a framework StateVector.
    /// </summary>
    /// <param name="observer">The central body (observer).</param>
    /// <param name="frame">The reference frame.</param>
    /// <param name="covariance">Optional 6x6 covariance matrix in framework units (m², m²/s, m²/s²).</param>
    /// <returns>A new StateVector in framework units (meters, m/s).</returns>
    /// <remarks>
    /// Converts from CCSDS units (km, km/s) to framework units (m, m/s).
    /// </remarks>
    public StateVector ToStateVector(ILocalizable observer, Frame frame, Matrix? covariance = null)
    {
        // Convert km to m, km/s to m/s
        var position = new Vector3(X * 1000.0, Y * 1000.0, Z * 1000.0);
        var velocity = new Vector3(XDot * 1000.0, YDot * 1000.0, ZDot * 1000.0);

        var epoch = new Time(Epoch, TimeFrame.UTCFrame);

        return new StateVector(position, velocity, observer, epoch, frame, covariance);
    }

    /// <summary>
    /// Creates an OpmStateVector from a framework StateVector.
    /// </summary>
    /// <param name="stateVector">The framework state vector.</param>
    /// <param name="comments">Optional comments.</param>
    /// <returns>A new OpmStateVector in CCSDS units (km, km/s).</returns>
    /// <remarks>
    /// Converts from framework units (m, m/s) to CCSDS units (km, km/s).
    /// </remarks>
    public static OpmStateVector FromStateVector(StateVector stateVector, IReadOnlyList<string> comments = null)
    {
        if (stateVector == null)
            throw new ArgumentNullException(nameof(stateVector));

        // Convert m to km, m/s to km/s
        return new OpmStateVector(
            stateVector.Epoch.DateTime,
            stateVector.Position.X / 1000.0,
            stateVector.Position.Y / 1000.0,
            stateVector.Position.Z / 1000.0,
            stateVector.Velocity.X / 1000.0,
            stateVector.Velocity.Y / 1000.0,
            stateVector.Velocity.Z / 1000.0,
            comments);
    }

    /// <summary>
    /// Gets the position as a Vector3 in kilometers.
    /// </summary>
    public Vector3 PositionKm => new(X, Y, Z);

    /// <summary>
    /// Gets the velocity as a Vector3 in kilometers per second.
    /// </summary>
    public Vector3 VelocityKmPerSec => new(XDot, YDot, ZDot);

    /// <inheritdoc />
    public override string ToString()
    {
        return $"OpmStateVector[Epoch={Epoch:O}, Pos=({X:F3},{Y:F3},{Z:F3}) km, Vel=({XDot:F6},{YDot:F6},{ZDot:F6}) km/s]";
    }
}
