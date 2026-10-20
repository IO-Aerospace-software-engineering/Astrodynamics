// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Atmosphere;

/// <summary>
/// An atmospheric model whose density is discontinuous at some altitudes, for the density gradient of the drag partials
/// (#413): a central difference whose stencil straddles one of them would add the jump of the density to the gradient.
/// </summary>
internal interface IDensityDiscontinuities
{
    /// <summary>
    /// The altitudes at which the density is discontinuous, in m, increasing; empty when the density is continuous.
    /// </summary>
    ReadOnlySpan<double> DensityDiscontinuities { get; }

    /// <summary>
    /// The interval between those altitudes in which the model places <paramref name="altitude"/>, with the model's own
    /// comparisons: 0 below the first, n above the n-th. Two altitudes of the same interval are on the same side of
    /// every discontinuity.
    /// </summary>
    /// <param name="altitude">Altitude, in m.</param>
    int DensityInterval(double altitude);
}
