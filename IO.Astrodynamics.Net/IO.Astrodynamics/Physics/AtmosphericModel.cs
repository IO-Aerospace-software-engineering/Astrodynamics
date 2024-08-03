// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Physics;

public abstract class AtmosphericModel
{
    public abstract double GetTemperature(double altitude);
    public abstract double GetPressure(double altitude);
    public abstract double GetDensity(double altitude);
}