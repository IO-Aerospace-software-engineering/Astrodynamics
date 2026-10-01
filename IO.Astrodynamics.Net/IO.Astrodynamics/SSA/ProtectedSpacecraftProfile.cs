// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Body.Spacecraft;

namespace IO.Astrodynamics.SSA;

/// <summary>
/// Wraps the protected spacecraft together with the maneuver policy used by SSA workflows.
/// </summary>
public sealed class ProtectedSpacecraftProfile
{
    /// <summary>
    /// Initializes a new protected spacecraft profile.
    /// </summary>
    /// <param name="spacecraft">Spacecraft to protect during screening and conjunction analysis.</param>
    /// <param name="maneuverConstraints">Operational maneuver limits. Defaults are used when <see langword="null"/>.</param>
    public ProtectedSpacecraftProfile(
        Spacecraft spacecraft,
        ManeuverConstraints maneuverConstraints = null)
    {
        Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        ManeuverConstraints = maneuverConstraints ?? new ManeuverConstraints();
    }

    /// <summary>
    /// Gets the spacecraft to protect.
    /// </summary>
    public Spacecraft Spacecraft { get; }

    /// <summary>
    /// Gets the maneuver limits used when evaluating avoidance options.
    /// </summary>
    public ManeuverConstraints ManeuverConstraints { get; }
}
