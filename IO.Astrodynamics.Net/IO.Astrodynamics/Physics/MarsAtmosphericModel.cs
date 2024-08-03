// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Physics;

public class MarsAtmosphericModel : AtmosphericModel
{
    public override double GetTemperature(double altitude)
    {
        if (altitude < 7000.0)
        {
            return -31.0 - 0.000998 * altitude;
        }

        return -23.4 - 0.00222 * altitude;
    }

    public override double GetPressure(double altitude)
    {
        return 0.699 * System.Math.Exp(-0.00009 * altitude);
    }

    public override double GetDensity(double altitude)
    {
        return GetPressure(altitude) / (0.1921 * (GetTemperature(altitude) + Constants.Kelvin));
    }
}