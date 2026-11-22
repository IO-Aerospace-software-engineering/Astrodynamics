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
    /// The gravity of <paramref name="celestialItem"/> evaluated with <paramref name="field"/>, a field of that item that
    /// this force alone evaluates: the field of a propagation (<see cref="CelestialItem.CreatePropagationField"/>), so
    /// that propagations sharing a body with a geopotential never share its buffers (R06 of #668).
    /// </summary>
    internal GravitationalAcceleration(CelestialItem celestialItem, GravitationalField field)
    {
        CelestialItem = celestialItem;
        _field = field ?? throw new ArgumentNullException(nameof(field));
    }

    // The field this force evaluates; null for the field of the body
    private readonly GravitationalField _field;

    /// <summary>The gravitational field this force evaluates: its own, or that of the body.</summary>
    internal GravitationalField Field => _field ?? CelestialItem.Field;

    /// <summary>
    /// Checks that the partials of <see cref="Field"/> can be computed at <paramref name="partialsDegree"/>, as
    /// <see cref="CelestialItem.ValidateGravitationalPartialsDegree"/> does for the field of the body.
    /// </summary>
    internal void ValidatePartialsDegree(int? partialsDegree)
    {
        if (Field is GeopotentialGravitationalField geopotential)
        {
            geopotential.PartialsDegree(partialsDegree);
        }
    }

    /// <summary>
    /// Evaluate gravitational acceleration at given stateVector
    /// </summary>
    /// <param name="stateVector"></param>
    /// <returns></returns>
    public override Vector3 Apply(StateVector stateVector)
    {
        var state = RelativeToBodyFromCache(stateVector);
        return _field == null
            ? CelestialItem.EvaluateGravitationalAcceleration(state)
            : CelestialItem.EvaluateGravitationalAcceleration(state, _field);
    }

    internal override bool DependsOnVelocity => false;

    /// <summary>
    /// For a geopotential, the highest degree of the partials of its harmonics; null (the default) for the degree of
    /// the field, which then gives the partials of the acceleration itself.
    /// </summary>
    /// <remarks>
    /// A degree above that of the field is that of the field; without a geopotential the degree is ignored. Set by the
    /// propagator from the variational options.
    /// </remarks>
    internal int? PartialsDegree { get; set; }

    /// <summary>
    /// Analytic partials when the field of the body has them: the point mass (B2), and the geopotential by the
    /// Cunningham recursion (B3); otherwise the default central differences.
    /// </summary>
    /// <remarks>
    /// For a state relative to the body, the propagator's case, the point-mass path allocates nothing, and neither does
    /// the geopotential path while the orientation cache of the propagation is active. For a state relative to
    /// another observer, the state relative to the body is built as in <see cref="Apply(StateVector)"/>: one
    /// <see cref="StateVector"/> from the ephemeris cache, or a SPICE call without it.
    /// </remarks>
    private protected override void AccumulateStatePartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv)
    {
        if (!CelestialItem.TryAccumulateGravitationalPositionPartials(RelativeToBodyFromCache(stateVector), dadr,
                PartialsDegree, Field))
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
