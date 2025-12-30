// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Marker interface for model-specific atmospheric details.
/// </summary>
/// <remarks>
/// Implement this interface to provide additional atmospheric data
/// specific to a particular atmospheric model. For example, NRLMSISE-00
/// provides molecular density breakdowns that are not available in
/// simpler models.
/// </remarks>
public interface IAtmosphericDetails
{
}
