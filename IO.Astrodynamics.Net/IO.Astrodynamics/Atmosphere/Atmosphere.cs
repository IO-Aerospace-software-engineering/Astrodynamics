// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Represents atmospheric properties at a specific location and time.
/// </summary>
/// <remarks>
/// This record provides a unified result type for all atmospheric models.
/// Basic properties (Temperature, Pressure, Density) are always available.
/// Model-specific details can be accessed through the Details property.
/// </remarks>
public record Atmosphere
{
    /// <summary>
    /// Temperature in Celsius.
    /// </summary>
    public required double Temperature { get; init; }

    /// <summary>
    /// Pressure in kPa.
    /// </summary>
    public required double Pressure { get; init; }

    /// <summary>
    /// Total mass density in kg/m³.
    /// </summary>
    public required double Density { get; init; }

    /// <summary>
    /// Optional model-specific atmospheric details.
    /// </summary>
    /// <remarks>
    /// For NRLMSISE-00 model, this will be <see cref="NRLMSISE_00.NrlmsiseDetails"/>.
    /// For standard models, this will be null.
    /// Use pattern matching to access model-specific data:
    /// <code>
    /// if (atmosphere.Details is NrlmsiseDetails nrlmsise)
    /// {
    ///     var o2Density = nrlmsise.MolecularOxygenDensity;
    /// }
    /// </code>
    /// </remarks>
    public IAtmosphericDetails Details { get; init; }
}
