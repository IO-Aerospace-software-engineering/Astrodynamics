// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Legacy atmospheric model base class.
/// </summary>
/// <remarks>
/// DEPRECATED: Use IAtmosphericModel interface instead.
/// This class exists only for backward compatibility and will be removed in a future version.
/// Migrate to EarthStandardAtmosphere, MarsStandardAtmosphere, or Nrlmsise00Model.
/// </remarks>
[Obsolete("Use IAtmosphericModel interface and AtmosphericContext instead. " +
          "Migrate to EarthStandardAtmosphere, MarsStandardAtmosphere, or Nrlmsise00Model. " +
          "This class will be removed in v2.0.0.", false)]
public abstract class AtmosphericModel : IAtmosphericModel
{
    public abstract double GetTemperature(double altitude);
    public abstract double GetPressure(double altitude);
    public abstract double GetDensity(double altitude);

    // Interface implementation - adapts old API to new pattern
    double IAtmosphericModel.GetTemperature(IAtmosphericContext context) =>
        GetTemperature(context.Altitude);
    double IAtmosphericModel.GetPressure(IAtmosphericContext context) =>
        GetPressure(context.Altitude);
    double IAtmosphericModel.GetDensity(IAtmosphericContext context) =>
        GetDensity(context.Altitude);
}