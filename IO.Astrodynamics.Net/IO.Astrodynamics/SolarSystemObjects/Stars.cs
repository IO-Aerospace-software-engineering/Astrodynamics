// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.SolarSystemObjects;

public static class Stars
{
    public static NaifObject Sun = new(10, "SUN", "IAU_SUN");
}