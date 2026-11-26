// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Represents a maneuver parameters section of an OPM message.
/// </summary>
/// <remarks>
/// <para>
/// OPM supports zero or more maneuver parameter sections, each describing a planned or executed maneuver.
/// </para>
/// <para>
/// Delta-V components are in km/s and are referenced to the specified MAN_REF_FRAME
/// (typically "RSW", "RTN", or a standard reference frame like "EME2000").
/// </para>
/// </remarks>
public class OpmManeuverParameters
{
    /// <summary>
    /// Gets the epoch of maneuver ignition.
    /// </summary>
    public DateTime ManEpochIgnition { get; }

    /// <summary>
    /// Gets the maneuver duration in seconds.
    /// </summary>
    /// <remarks>
    /// Must be >= 0. A value of 0 represents an impulsive maneuver.
    /// </remarks>
    public double ManDurationSeconds { get; }

    /// <summary>
    /// Gets the change in spacecraft mass due to the maneuver in kilograms.
    /// </summary>
    /// <remarks>
    /// Must be &lt;= 0 (mass is expelled during the maneuver).
    /// </remarks>
    public double ManDeltaMass { get; }

    /// <summary>
    /// Gets the reference frame for the delta-V components.
    /// </summary>
    /// <remarks>
    /// Common values: "RSW" (Radial-Along track-Cross track), "RTN" (Radial-Transverse-Normal),
    /// "TNW" (Transverse-Normal-W), or standard frames like "EME2000".
    /// </remarks>
    public string ManRefFrame { get; }

    /// <summary>
    /// Gets the first delta-V component in km/s.
    /// </summary>
    /// <remarks>
    /// The meaning depends on MAN_REF_FRAME:
    /// - RSW/RTN: Radial component (outward positive)
    /// - TNW: Transverse component (along velocity positive)
    /// </remarks>
    public double ManDv1 { get; }

    /// <summary>
    /// Gets the second delta-V component in km/s.
    /// </summary>
    /// <remarks>
    /// The meaning depends on MAN_REF_FRAME:
    /// - RSW: Along-track component (in velocity direction positive)
    /// - RTN: Transverse component (along velocity positive)
    /// - TNW: Normal component (orbit normal positive)
    /// </remarks>
    public double ManDv2 { get; }

    /// <summary>
    /// Gets the third delta-V component in km/s.
    /// </summary>
    /// <remarks>
    /// The meaning depends on MAN_REF_FRAME:
    /// - RSW/RTN: Cross-track/Normal component (orbit normal positive)
    /// - TNW: W component
    /// </remarks>
    public double ManDv3 { get; }

    /// <summary>
    /// Gets optional comments associated with the maneuver.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpmManeuverParameters"/> class.
    /// </summary>
    /// <param name="manEpochIgnition">Epoch of maneuver ignition.</param>
    /// <param name="manDurationSeconds">Duration in seconds (finite, >= 0).</param>
    /// <param name="manDeltaMass">Mass change in kg (finite, &lt;= 0).</param>
    /// <param name="manRefFrame">Reference frame for delta-V components.</param>
    /// <param name="manDv1">First delta-V component in km/s (finite).</param>
    /// <param name="manDv2">Second delta-V component in km/s (finite).</param>
    /// <param name="manDv3">Third delta-V component in km/s (finite).</param>
    /// <param name="comments">Optional comments.</param>
    /// <exception cref="ArgumentOutOfRangeException">The duration is not finite and 0 or more, the delta mass not finite
    /// and 0 or less (#708), or a ΔV component is NaN or ±Infinity (#717).</exception>
    public OpmManeuverParameters(
        DateTime manEpochIgnition,
        double manDurationSeconds,
        double manDeltaMass,
        string manRefFrame,
        double manDv1,
        double manDv2,
        double manDv3,
        IReadOnlyList<string> comments = null)
    {
        // Finite values (#708: NaN, and -Infinity for the delta mass, passed the checks)
        if (!(double.IsFinite(manDurationSeconds) && manDurationSeconds >= 0))
            throw new ArgumentOutOfRangeException(nameof(manDurationSeconds), "Maneuver duration must be finite and cannot be negative.");
        if (!(double.IsFinite(manDeltaMass) && manDeltaMass <= 0))
            throw new ArgumentOutOfRangeException(nameof(manDeltaMass),
                "Maneuver delta mass must be finite and non-positive. Use negative values for expelled mass (e.g., -25.0 kg means 25 kg of propellant burned).");
        if (string.IsNullOrWhiteSpace(manRefFrame))
            throw new ArgumentException("Maneuver reference frame is required.", nameof(manRefFrame));

        ManEpochIgnition = manEpochIgnition;
        ManDurationSeconds = manDurationSeconds;
        ManDeltaMass = manDeltaMass;
        ManRefFrame = manRefFrame;
        // The ΔV components carry no range but are real numbers, as the reader reads them (#717)
        ManDv1 = CcsdsValues.RequireFinite(manDv1, nameof(manDv1));
        ManDv2 = CcsdsValues.RequireFinite(manDv2, nameof(manDv2));
        ManDv3 = CcsdsValues.RequireFinite(manDv3, nameof(manDv3));
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates an impulsive maneuver (duration = 0).
    /// </summary>
    /// <param name="epoch">Epoch of the impulsive maneuver.</param>
    /// <param name="deltaV">Delta-V vector in km/s.</param>
    /// <param name="refFrame">Reference frame for the delta-V.</param>
    /// <param name="deltaMass">Mass change in kg (&lt;= 0).</param>
    /// <param name="comments">Optional comments.</param>
    /// <returns>A new OpmManeuverParameters instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A component of <paramref name="deltaV"/> is NaN or ±Infinity (#717),
    /// named as the constructor's parameter (<c>manDv1</c> ... <c>manDv3</c>), or the delta mass is not finite and 0 or
    /// less.</exception>
    public static OpmManeuverParameters CreateImpulsive(
        DateTime epoch,
        Vector3 deltaV,
        string refFrame = "RSW",
        double deltaMass = 0,
        IReadOnlyList<string> comments = null)
    {
        return new OpmManeuverParameters(
            epoch,
            0, // impulsive
            deltaMass,
            refFrame,
            deltaV.X,
            deltaV.Y,
            deltaV.Z,
            comments);
    }

    /// <summary>
    /// Gets the delta-V as a Vector3 in km/s.
    /// </summary>
    public Vector3 DeltaVKmPerSec => new(ManDv1, ManDv2, ManDv3);

    /// <summary>
    /// Gets the total delta-V magnitude in km/s.
    /// </summary>
    public double DeltaVMagnitude => DeltaVKmPerSec.Magnitude();

    /// <summary>
    /// Gets whether this is an impulsive maneuver (duration = 0).
    /// </summary>
    public bool IsImpulsive => ManDurationSeconds == 0;

    /// <inheritdoc />
    public override string ToString()
    {
        string type = IsImpulsive ? "Impulsive" : $"Duration={ManDurationSeconds:F1}s";
        return $"OpmManeuver[{ManEpochIgnition:O}, {type}, ΔV={DeltaVMagnitude:F6}km/s, Frame={ManRefFrame}]";
    }
}
