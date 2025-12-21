// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere.NRLMSISE_00;

/// <summary>
/// Encapsulates space weather data for atmospheric modeling.
/// </summary>
/// <remarks>
/// Contains solar activity indices (F10.7) and geomagnetic activity indices (Ap)
/// used by atmospheric models like NRLMSISE-00 to account for solar and geomagnetic effects.
/// </remarks>
public record SpaceWeather
{
    /// <summary>
    /// Daily F10.7 flux for previous day (solar radio flux at 10.7 cm wavelength).
    /// </summary>
    /// <remarks>
    /// Typical values: 70-80 (solar minimum), 150 (moderate), 250+ (solar maximum).
    /// Units: 10⁻²² W·m⁻²·Hz⁻¹
    /// </remarks>
    public required double F107 { get; init; }

    /// <summary>
    /// 81-day average of F10.7 flux centered on current day.
    /// </summary>
    /// <remarks>
    /// Represents the longer-term solar activity trend.
    /// Units: 10⁻²² W·m⁻²·Hz⁻¹
    /// </remarks>
    public required double F107A { get; init; }

    /// <summary>
    /// Daily magnetic index (Ap).
    /// </summary>
    /// <remarks>
    /// Typical values: 0-7 (quiet), 8-15 (unsettled), 16-29 (active), 30-49 (minor storm), 50+ (major storm).
    /// </remarks>
    public required double Ap { get; init; }

    /// <summary>
    /// Detailed Ap array for time-varying geomagnetic activity.
    /// </summary>
    /// <remarks>
    /// Contains Ap indices at various time intervals before current time.
    /// If null, uses default quiet conditions (all values = 4.0).
    /// </remarks>
    public ApArray? ApArray { get; init; }

    /// <summary>
    /// Creates space weather data representing nominal quiet conditions (solar minimum).
    /// </summary>
    /// <returns>Space weather with F10.7=150, F10.7A=150, Ap=4 (typical quiet conditions).</returns>
    public static SpaceWeather Nominal => new()
    {
        F107 = 150.0,
        F107A = 150.0,
        Ap = 4.0,
        ApArray = ApArray.Default
    };

    /// <summary>
    /// Creates space weather data representing solar minimum conditions.
    /// </summary>
    /// <returns>Space weather with F10.7=70, F10.7A=70, Ap=4 (very quiet sun).</returns>
    public static SpaceWeather SolarMinimum => new()
    {
        F107 = 70.0,
        F107A = 70.0,
        Ap = 4.0,
        ApArray = ApArray.Default
    };

    /// <summary>
    /// Creates space weather data representing solar maximum conditions.
    /// </summary>
    /// <returns>Space weather with F10.7=250, F10.7A=240, Ap=15 (active sun).</returns>
    public static SpaceWeather SolarMaximum => new()
    {
        F107 = 250.0,
        F107A = 240.0,
        Ap = 15.0,
        ApArray = ApArray.Default
    };

    /// <summary>
    /// Creates space weather data representing moderate activity.
    /// </summary>
    /// <returns>Space weather with F10.7=150, F10.7A=150, Ap=7.</returns>
    public static SpaceWeather Moderate => new()
    {
        F107 = 150.0,
        F107A = 150.0,
        Ap = 7.0,
        ApArray = ApArray.Default
    };
}
