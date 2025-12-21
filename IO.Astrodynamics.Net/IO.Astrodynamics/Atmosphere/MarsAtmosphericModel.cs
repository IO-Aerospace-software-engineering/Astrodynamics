// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// Legacy Mars atmospheric model.
/// </summary>
/// <remarks>
/// DEPRECATED: Use MarsStandardAtmosphere instead.
/// This class exists only for backward compatibility and will be removed in a future version.
/// </remarks>
[Obsolete("Use MarsStandardAtmosphere instead. This class will be removed in v2.0.0.", false)]
public class MarsAtmosphericModel : AtmosphericModel
{
    private readonly MarsStandardAtmosphere _impl = new MarsStandardAtmosphere();

    public override double GetTemperature(double altitude) =>
        _impl.GetTemperature(AtmosphericContext.FromAltitude(altitude));

    public override double GetPressure(double altitude) =>
        _impl.GetPressure(AtmosphericContext.FromAltitude(altitude));

    public override double GetDensity(double altitude) =>
        _impl.GetDensity(AtmosphericContext.FromAltitude(altitude));
}