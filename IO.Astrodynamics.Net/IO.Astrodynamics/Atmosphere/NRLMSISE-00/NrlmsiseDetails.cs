// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere.NRLMSISE_00;

/// <summary>
/// NRLMSISE-00 specific atmospheric details including molecular densities.
/// </summary>
/// <remarks>
/// All number densities are in particles per cubic meter (m?³).
/// These values are specific to Earth's atmosphere as modeled by NRLMSISE-00.
/// </remarks>
public record NrlmsiseDetails : IAtmosphericDetails
{
    /// <summary>
    /// Helium (He) number density in m?³.
    /// </summary>
    public double HeliumDensity { get; init; }

    /// <summary>
    /// Atomic oxygen (O) number density in m?³.
    /// </summary>
    public double AtomicOxygenDensity { get; init; }

    /// <summary>
    /// Molecular nitrogen (N?) number density in m?³.
    /// </summary>
    public double NitrogenDensity { get; init; }

    /// <summary>
    /// Molecular oxygen (O?) number density in m?³.
    /// </summary>
    public double MolecularOxygenDensity { get; init; }

    /// <summary>
    /// Argon (Ar) number density in m?³.
    /// </summary>
    public double ArgonDensity { get; init; }

    /// <summary>
    /// Hydrogen (H) number density in m?³.
    /// </summary>
    /// <remarks>
    /// Set to zero below 72,500 m altitude.
    /// </remarks>
    public double HydrogenDensity { get; init; }

    /// <summary>
    /// Atomic nitrogen (N) number density in m?³.
    /// </summary>
    /// <remarks>
    /// Set to zero below 72,500 m altitude.
    /// </remarks>
    public double AtomicNitrogenDensity { get; init; }

    /// <summary>
    /// Anomalous oxygen number density in m?³.
    /// </summary>
    public double AnomalousOxygenDensity { get; init; }

    /// <summary>
    /// Exospheric temperature in Kelvin.
    /// </summary>
    /// <remarks>
    /// Set to global average for altitudes below 120,000 m.
    /// </remarks>
    public double ExosphericTemperature { get; init; }
}
