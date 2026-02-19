// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// Defines standard orbital direction conventions.
/// </summary>
public enum OrbitalDirection
{
    /// <summary>Velocity direction (+v).</summary>
    Prograde,

    /// <summary>Anti-velocity direction (-v).</summary>
    Retrograde,

    /// <summary>Toward central body center (-r).</summary>
    Nadir,

    /// <summary>Away from central body center (+r).</summary>
    Zenith,

    /// <summary>Orbital angular momentum direction (r x v).</summary>
    Normal,

    /// <summary>Anti-orbital angular momentum direction (-(r x v)).</summary>
    AntiNormal
}
