// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.Common.Enums;
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
/// Use <see cref="ToStateVector(IO.Astrodynamics.Body.ILocalizable, IO.Astrodynamics.Frames.Frame, System.Nullable{IO.Astrodynamics.Math.Matrix})"/> to convert to the framework's internal StateVector (meters, m/s).
/// Use <see cref="FromStateVector"/> to create from a framework StateVector.
/// </remarks>
public class OpmStateVector
{
    // The factor of the conversion of km and km/s to m and m/s
    private const double KilometersToMeters = 1000.0;

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
    /// <param name="x">X position in km (finite).</param>
    /// <param name="y">Y position in km (finite).</param>
    /// <param name="z">Z position in km (finite).</param>
    /// <param name="xDot">X velocity in km/s (finite).</param>
    /// <param name="yDot">Y velocity in km/s (finite).</param>
    /// <param name="zDot">Z velocity in km/s (finite).</param>
    /// <param name="comments">Optional comments.</param>
    /// <exception cref="ArgumentOutOfRangeException">A component is NaN or ±Infinity, which the OPM reader refuses (#708,
    /// #717), or its value in m or m/s, the component times 1000, is beyond the range of a double (#729: above about
    /// 1.8e305 km, which <see cref="ToStateVector(ILocalizable, Frame, Matrix?)"/> converted to ±Infinity).</exception>
    public OpmStateVector(
        DateTime epoch,
        double x, double y, double z,
        double xDot, double yDot, double zDot,
        IReadOnlyList<string> comments = null)
    {
        // Real numbers, as the reader reads them (#717: NaN and ±Infinity built, and the writers wrote NaN, Infinity or
        // -Infinity, which the reader refuses), finite in m and m/s too (#729: ToStateVector gave ±Infinity)
        Epoch = epoch;
        X = CcsdsValues.RequireFiniteInSiUnits(x, KilometersToMeters, "km", nameof(x));
        Y = CcsdsValues.RequireFiniteInSiUnits(y, KilometersToMeters, "km", nameof(y));
        Z = CcsdsValues.RequireFiniteInSiUnits(z, KilometersToMeters, "km", nameof(z));
        XDot = CcsdsValues.RequireFiniteInSiUnits(xDot, KilometersToMeters, "km/s", nameof(xDot));
        YDot = CcsdsValues.RequireFiniteInSiUnits(yDot, KilometersToMeters, "km/s", nameof(yDot));
        ZDot = CcsdsValues.RequireFiniteInSiUnits(zDot, KilometersToMeters, "km/s", nameof(zDot));
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
    /// Converts from CCSDS units (km, km/s) to framework units (m, m/s). The epoch is read as UTC: this state carries no
    /// time system. <see cref="Opm.ToStateVector"/> reads it in the <c>TIME_SYSTEM</c> of its metadata (#485).
    /// </remarks>
    public StateVector ToStateVector(ILocalizable observer, Frame frame, Matrix? covariance = null)
    {
        return ToStateVector(observer, frame, covariance, TimeScale.UTC);
    }

    /// <summary>
    /// Converts this OPM state vector to a framework StateVector, its epoch read in <paramref name="timeScale"/>, the
    /// time scale of the message's <c>TIME_SYSTEM</c> (#485).
    /// </summary>
    internal StateVector ToStateVector(ILocalizable observer, Frame frame, Matrix? covariance, TimeScale timeScale)
    {
        // Convert km to m, km/s to m/s: finite, the constructor refusing a component whose product is not (#729)
        var position = new Vector3(X * KilometersToMeters, Y * KilometersToMeters, Z * KilometersToMeters);
        var velocity = new Vector3(XDot * KilometersToMeters, YDot * KilometersToMeters, ZDot * KilometersToMeters);

        var epoch = CcsdsTimeSystemMapping.ToTime(Epoch, timeScale);

        return new StateVector(position, velocity, observer, epoch, frame, covariance);
    }

    /// <summary>
    /// Creates an OpmStateVector from a framework StateVector.
    /// </summary>
    /// <param name="stateVector">The framework state vector.</param>
    /// <param name="comments">Optional comments.</param>
    /// <returns>A new OpmStateVector in CCSDS units (km, km/s).</returns>
    /// <exception cref="ArgumentOutOfRangeException">A component of the state is NaN or ±Infinity (#717), named as the
    /// constructor's parameter (<c>x</c> ... <c>zDot</c>).</exception>
    /// <remarks>
    /// Converts from framework units (m, m/s) to CCSDS units (km, km/s). The epoch is written in UTC, whatever the time
    /// frame of the state's epoch (#479), as the library's OPM writers declare <c>TIME_SYSTEM = UTC</c>.
    /// </remarks>
    public static OpmStateVector FromStateVector(StateVector stateVector, IReadOnlyList<string> comments = null)
    {
        if (stateVector == null)
            throw new ArgumentNullException(nameof(stateVector));

        // Convert m to km, m/s to km/s
        return new OpmStateVector(
            stateVector.Epoch.ToUtc().DateTime,
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
