// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.OrbitalParameters;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Forces;

public abstract class ForceBase
{
    /// <summary>
    /// Optional ephemeris cache for avoiding repeated SPICE calls during propagation.
    /// Set by the propagator before integration begins.
    /// </summary>
    internal PropagationEphemerisCache EphemerisCache { get; set; }

    public abstract Vector3 Apply(StateVector stateVector);
}