// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// The quintic smootherstep w = 10x³ − 15x⁴ + 6x⁵, 0 at x = 0 and 1 at x = 1, with w′ and w″ zero at both ends, which
/// blends two branches of an atmospheric model so that the density and its first two derivatives stay continuous
/// (K. Perlin, "Improving noise", ACM Transactions on Graphics 21(3) (2002); to be verified by S. Guillet).
/// </summary>
internal static class Smootherstep
{
    /// <summary>
    /// The weight of the upper branch and its derivative in altitude, over a transition that starts at
    /// <paramref name="start"/> and spans <paramref name="width"/>; (1, 0) at and above its end.
    /// </summary>
    /// <param name="altitude">Altitude, in m.</param>
    /// <param name="start">Altitude of the start of the transition, in m.</param>
    /// <param name="width">Width of the transition, in m.</param>
    internal static (double Weight, double Slope) At(double altitude, double start, double width)
    {
        double x = (altitude - start) / width;
        if (x >= 1.0)
        {
            return (1.0, 0.0);
        }

        return (x * x * x * (10.0 + x * (-15.0 + 6.0 * x)), 30.0 * x * x * (1.0 - x) * (1.0 - x) / width);
    }
}
