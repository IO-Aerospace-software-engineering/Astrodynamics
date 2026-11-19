// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.SolarSystemObjects;

public static class LagrangePoints
{
    public static readonly NaifObject L1 = new(391, "L1");
    public static readonly NaifObject L2 = new(392, "L2");
    public static readonly NaifObject L4 = new(394, "L4");
    public static readonly NaifObject L5 = new(395, "L5");
}