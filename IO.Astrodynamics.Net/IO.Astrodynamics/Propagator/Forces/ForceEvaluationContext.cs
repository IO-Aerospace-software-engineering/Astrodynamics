// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body.Spacecraft;

namespace IO.Astrodynamics.Propagator.Forces;

/// <summary>
/// The spacecraft quantities a force reads besides the state: total mass and the drag and reflectivity coefficients.
/// </summary>
/// <remarks>
/// <para>
/// During a propagation these are the live values of the spacecraft (<see cref="FromSpacecraft"/>): the mass is
/// constant within a segment, since fuel burns only at impulsive maneuvers. The context lets an evaluation made after
/// the propagation use the mass of the segment instead of the final one, and lets the parameter partials perturb a
/// coefficient without touching the spacecraft.
/// </para>
/// <para>
/// A value type passed by <c>in</c>, so building and passing it allocates nothing.
/// </para>
/// </remarks>
/// <param name="TotalMass">Total mass of the spacecraft, in kg (dry, fuel and payload).</param>
/// <param name="DragCoefficient">Drag coefficient Cd, dimensionless.</param>
/// <param name="ReflectivityCoefficient">
/// Reflectivity coefficient Cr, dimensionless. Shared by solar, albedo and thermal radiation pressure.
/// </param>
internal readonly record struct ForceEvaluationContext(
    double TotalMass,
    double DragCoefficient,
    double ReflectivityCoefficient)
{
    /// <summary>
    /// The context of a spacecraft as it is now: <see cref="Spacecraft.GetTotalMass"/>,
    /// <see cref="Spacecraft.DragCoefficient"/> and <see cref="Spacecraft.SolarRadiationCoeff"/>.
    /// </summary>
    /// <param name="spacecraft">The spacecraft.</param>
    /// <returns>The context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spacecraft"/> is null.</exception>
    internal static ForceEvaluationContext FromSpacecraft(Spacecraft spacecraft)
    {
        ArgumentNullException.ThrowIfNull(spacecraft);
        return new ForceEvaluationContext(spacecraft.GetTotalMass(), spacecraft.DragCoefficient,
            spacecraft.SolarRadiationCoeff);
    }
}
