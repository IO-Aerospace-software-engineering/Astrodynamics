// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Maneuver;

/// <summary>
/// An axis of the spacecraft body frame, resolved on the spacecraft whose plan last held the maneuver: its instance
/// axes (<see cref="Body.Spacecraft.Spacecraft.BodyFront"/>, <see cref="Body.Spacecraft.Spacecraft.BodyRight"/>,
/// <see cref="Body.Spacecraft.Spacecraft.BodyUp"/> and their opposites), or the static axes
/// (<see cref="Body.Spacecraft.Spacecraft.Front"/>, ...) before any plan (decision 69 of #668, decided by S. Guillet,
/// 2026-11-23).
/// </summary>
public enum BodyAxis
{
    /// <summary>The front axis, <see cref="Body.Spacecraft.Spacecraft.BodyFront"/> (+Y by default).</summary>
    Front,

    /// <summary>The back axis, <see cref="Body.Spacecraft.Spacecraft.BodyBack"/> (−Y by default).</summary>
    Back,

    /// <summary>The right axis, <see cref="Body.Spacecraft.Spacecraft.BodyRight"/> (+X by default).</summary>
    Right,

    /// <summary>The left axis, <see cref="Body.Spacecraft.Spacecraft.BodyLeft"/> (−X by default).</summary>
    Left,

    /// <summary>The up axis, <see cref="Body.Spacecraft.Spacecraft.BodyUp"/> (+Z by default).</summary>
    Up,

    /// <summary>The down axis, <see cref="Body.Spacecraft.Spacecraft.BodyDown"/> (−Z by default).</summary>
    Down
}
