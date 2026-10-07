using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Forces;

/// <summary>
/// GravitationalAcceleration force from given celestial body
/// </summary>
public class GravitationalAcceleration : ForceBase
{
    public CelestialItem CelestialItem { get; }

    public GravitationalAcceleration(CelestialItem celestialItem)
    {
        CelestialItem = celestialItem;
    }

    /// <summary>
    /// Evaluate gravitational acceleration at given stateVector
    /// </summary>
    /// <param name="stateVector"></param>
    /// <returns></returns>
    public override Vector3 Apply(StateVector stateVector)
    {
        return CelestialItem.EvaluateGravitationalAcceleration(RelativeToBodyFromCache(stateVector));
    }

    internal override bool DependsOnVelocity => false;

    /// <summary>
    /// Analytic point-mass partials when the field of the body has them; otherwise (geopotential, until phase 2,
    /// step 5a) the default central differences.
    /// </summary>
    /// <remarks>
    /// For a state relative to the body, the propagator's case, the analytic path allocates nothing. For a state
    /// relative to another observer, the state relative to the body is built as in <see cref="Apply(StateVector)"/>: one
    /// <see cref="StateVector"/> from the ephemeris cache, or a SPICE call without it.
    /// </remarks>
    private protected override void AccumulateStatePartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv)
    {
        if (!CelestialItem.TryAccumulateGravitationalPositionPartials(RelativeToBodyFromCache(stateVector), dadr))
        {
            base.AccumulateStatePartialsCore(stateVector, context, dadr, dadv);
        }
    }

    /// <summary>
    /// In SSB mode, the state vector observer is SSB, not the body: the state relative to the body, from the cache to
    /// avoid a SPICE call. Otherwise the state itself, which <see cref="CelestialItem"/> brings relative to the body if
    /// needed.
    /// </summary>
    private StateVector RelativeToBodyFromCache(StateVector stateVector)
    {
        if (EphemerisCache != null && stateVector.Observer as CelestialItem != CelestialItem
            && EphemerisCache.Contains(CelestialItem.NaifId, Aberration.None))
        {
            var (bodyPos, bodyVel) = EphemerisCache.GetState(CelestialItem.NaifId, Aberration.None, stateVector.Epoch);
            return new StateVector(
                stateVector.Position - bodyPos,
                stateVector.Velocity - bodyVel,
                CelestialItem, stateVector.Epoch, stateVector.Frame);
        }

        return stateVector;
    }
}
