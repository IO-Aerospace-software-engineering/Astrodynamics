namespace IO.Astrodynamics.TimeSystem;

/// <summary>
/// The time scale of a <see cref="Time"/>: the clock its calendar counts (D3 of the ergonomics review, #668, approved by
/// S. Guillet, 2026-11-13, amended 2026-11-19; it replaces <c>ITimeFrame</c> and the <c>TimeFrame</c> classes of 10.x). The conversions between the scales are
/// managed and read no kernel: TAI − UTC from the leap second table (10 s on 1972-01-01 to 37 s from 2017-01-01, a fixed
/// 9 s before 1972), TT = TAI + 32.184 s, GPS = TAI − 19 s, and TDB − TT by the one-term series
/// 0.001658 sin(g + 0.0167 sin g) s.
/// </summary>
/// <remarks>
/// <see cref="TDB"/> is 0, so the default <see cref="Time"/> is 0001-01-01T00:00:00 TDB. A local time is not a scale: the
/// calendar of an instant in the zone of the machine is <c>t.ToUTC().DateTime.ToLocalTime()</c>.
/// </remarks>
public enum TimeScale
{
    /// <summary>
    /// Barycentric Dynamical Time, the ephemeris time CSPICE reads (ET).
    /// </summary>
    TDB = 0,

    /// <summary>
    /// Terrestrial Time, TAI + 32.184 s (written TDT before 11.0.0).
    /// </summary>
    TT,

    /// <summary>
    /// International Atomic Time.
    /// </summary>
    TAI,

    /// <summary>
    /// Coordinated Universal Time, TAI less the leap seconds.
    /// </summary>
    UTC,

    /// <summary>
    /// GPS time, TAI − 19 s.
    /// </summary>
    GPS
}
