// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// A force model parameter of the variational equations: a column of the sensitivity Ψ
/// (<see cref="PropagationSolution.GetParameterSensitivity"/>), or a consider parameter of the extended covariance
/// (<see cref="PropagationSolution.GetExtendedCovariance"/>).
/// </summary>
public enum ForceParameter
{
    /// <summary>The drag coefficient Cd (<see cref="Body.Spacecraft.Spacecraft.DragCoefficient"/>), dimensionless.</summary>
    DragCoefficient,

    /// <summary>
    /// The reflectivity coefficient Cr (<see cref="Body.Spacecraft.Spacecraft.SolarRadiationCoeff"/>), dimensionless,
    /// shared by solar radiation, albedo and thermal radiation pressure.
    /// </summary>
    ReflectivityCoefficient
}
