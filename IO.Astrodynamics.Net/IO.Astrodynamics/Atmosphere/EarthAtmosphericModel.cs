// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Legacy Earth atmospheric model.
/// </summary>
/// <remarks>
/// DEPRECATED: Use EarthStandardAtmosphere instead.
/// This class exists only for backward compatibility and will be removed in a future version.
/// </remarks>
[Obsolete("Use EarthStandardAtmosphere instead. This class will be removed in v2.0.0.", false)]
public class EarthAtmosphericModel : AtmosphericModel
{
    private readonly EarthStandardAtmosphere _impl = new EarthStandardAtmosphere();

    public override double GetTemperature(double altitude) =>
        _impl.GetTemperature(AtmosphericContext.FromAltitude(altitude));

    public override double GetPressure(double altitude) =>
        _impl.GetPressure(AtmosphericContext.FromAltitude(altitude));

    public override double GetDensity(double altitude) =>
        _impl.GetDensity(AtmosphericContext.FromAltitude(altitude));
}