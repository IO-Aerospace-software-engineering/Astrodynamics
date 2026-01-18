// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
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
    public double ManDuration { get; }

    /// <summary>
    /// Gets the change in spacecraft mass due to the maneuver in kilograms.
    /// </summary>
    /// <remarks>
    /// Must be <= 0 (mass is expelled during the maneuver).
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
    /// <param name="manDuration">Duration in seconds (>= 0).</param>
    /// <param name="manDeltaMass">Mass change in kg (<= 0).</param>
    /// <param name="manRefFrame">Reference frame for delta-V components.</param>
    /// <param name="manDv1">First delta-V component in km/s.</param>
    /// <param name="manDv2">Second delta-V component in km/s.</param>
    /// <param name="manDv3">Third delta-V component in km/s.</param>
    /// <param name="comments">Optional comments.</param>
    public OpmManeuverParameters(
        DateTime manEpochIgnition,
        double manDuration,
        double manDeltaMass,
        string manRefFrame,
        double manDv1,
        double manDv2,
        double manDv3,
        IReadOnlyList<string> comments = null)
    {
        if (manDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(manDuration), "Maneuver duration cannot be negative.");
        if (manDeltaMass > 0)
            throw new ArgumentOutOfRangeException(nameof(manDeltaMass), "Maneuver delta mass must be <= 0 (mass is expelled).");
        if (string.IsNullOrWhiteSpace(manRefFrame))
            throw new ArgumentException("Maneuver reference frame is required.", nameof(manRefFrame));

        ManEpochIgnition = manEpochIgnition;
        ManDuration = manDuration;
        ManDeltaMass = manDeltaMass;
        ManRefFrame = manRefFrame;
        ManDv1 = manDv1;
        ManDv2 = manDv2;
        ManDv3 = manDv3;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates an impulsive maneuver (duration = 0).
    /// </summary>
    /// <param name="epoch">Epoch of the impulsive maneuver.</param>
    /// <param name="deltaV">Delta-V vector in km/s.</param>
    /// <param name="refFrame">Reference frame for the delta-V.</param>
    /// <param name="deltaMass">Mass change in kg (<= 0).</param>
    /// <param name="comments">Optional comments.</param>
    /// <returns>A new OpmManeuverParameters instance.</returns>
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
    public bool IsImpulsive => ManDuration == 0;

    /// <inheritdoc />
    public override string ToString()
    {
        string type = IsImpulsive ? "Impulsive" : $"Duration={ManDuration:F1}s";
        return $"OpmManeuver[{ManEpochIgnition:O}, {type}, ΔV={DeltaVMagnitude:F6}km/s, Frame={ManRefFrame}]";
    }
}
