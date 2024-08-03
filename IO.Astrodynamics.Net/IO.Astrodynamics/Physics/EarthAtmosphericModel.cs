// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Physics;

public class EarthAtmosphericModel : AtmosphericModel
{
    public override double GetTemperature(double altitude)
    {
        if (altitude < 11000.0)
        {
            return 15.04 - 0.00649 * altitude;
        }

        if (altitude < 25000.0)
        {
            return -56.46;
        }

        return double.Min(-131.21 + 0.00299 * altitude, 2200.0);
    }

    public override double GetPressure(double altitude)
    {
        if (altitude < 11000.0)
        {
            return 101.29 * System.Math.Pow(((GetTemperature(altitude) + Constants.Kelvin) / 288.08), 5.256);
        }

        if (altitude < 25000.0)
        {
            return 22.65 * System.Math.Exp(1.73 - .000157 * altitude);
        }

        return 2.488 * System.Math.Pow(((GetTemperature(altitude) + Constants.Kelvin) / 216.6), -11.388);
    }

    public override double GetDensity(double altitude)
    {
        return GetPressure(altitude) / (0.2869 * (GetTemperature(altitude) + Constants.Kelvin));
    }
}