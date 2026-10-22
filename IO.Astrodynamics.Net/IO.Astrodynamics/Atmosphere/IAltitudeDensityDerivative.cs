// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// An atmospheric model whose density depends on the altitude alone and gives its derivative in closed form, for the
/// density gradient of the drag partials: ∇ρ = (dρ/dh) n̂, n̂ the normal of the ellipsoid, without finite differences
/// (#420).
/// </summary>
internal interface IAltitudeDensityDerivative
{
    /// <summary>
    /// dρ/dh at an altitude: the derivative of the branch of the model the altitude falls in, with the model's own
    /// comparisons, the same doubles as its density.
    /// </summary>
    /// <param name="altitude">Altitude, in m.</param>
    /// <returns>dρ/dh, in kg/m⁴.</returns>
    double GetDensityAltitudeDerivative(double altitude);
}
