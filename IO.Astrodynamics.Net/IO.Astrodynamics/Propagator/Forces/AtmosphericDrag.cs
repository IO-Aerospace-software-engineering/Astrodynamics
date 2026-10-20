using System;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator.Forces;

public class AtmosphericDrag : ForceBase
{
    /// <summary>
    /// Relative step δ_ρ of the central differences of the density gradient: h_ρ = δ_ρ max(|r|, 1 m), r the position
    /// relative to the body.
    /// </summary>
    /// <remarks>
    /// Chosen by the plateau study of phase 2, step 5b (<c>DensityGradientStepStudyTests</c>, and the provenance matrix
    /// of the documentation): with NRLMSISE-00 at LEO 400 km, LEO 800 km and the HEO perigee, the worst relative error
    /// of ∇ρ is below 4e-9 from 1e-7 to 1e-6, and 5.6e-10 at 3e-7 (h ≈ 2 m in LEO). Larger steps leave the truncation of
    /// the density scale height H, of order (h / H)², 1.7e-7 at 1e-5; smaller ones the rounding of the model.
    /// </remarks>
    internal const double DensityGradientRelativeStep = 3e-7;

    private readonly Spacecraft _spacecraft;
    private readonly CelestialBody _celestialBody;

    // The altitudes at which the density of the atmosphere jumps, when its model gives them (NRLMSISE-00, #413)
    private readonly IDensityDiscontinuities _discontinuities;

    /// <summary>The body whose atmosphere slows the spacecraft.</summary>
    internal CelestialBody CelestialBody => _celestialBody;

    public AtmosphericDrag(Spacecraft spacecraft, CelestialBody celestialBody)
    {
        _spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        _celestialBody = celestialBody ?? throw new ArgumentNullException(nameof(celestialBody));
        if (!_celestialBody.HasAtmosphericModel)
        {
            throw new ArgumentException($"The celestial body {_celestialBody.Name} does not have an atmospheric model.");
        }

        _discontinuities = _celestialBody.Atmosphere as IDensityDiscontinuities;
    }

    public override Vector3 Apply(StateVector stateVector)
    {
        return Apply(stateVector, ForceEvaluationContext.FromSpacecraft(_spacecraft));
    }

    internal override ForceParameters Parameters => ForceParameters.DragCoefficient;

    /// <summary>
    /// Drag acceleration with the mass and the drag coefficient of <paramref name="context"/>.
    /// </summary>
    internal override Vector3 Apply(StateVector stateVector, in ForceEvaluationContext context)
    {
        var relative = GetRelativeState(stateVector);
        var density = Density(relative.Position, BodyFixedRotation(stateVector), stateVector.Epoch);
        var areaMassRatio = _spacecraft.SectionalArea / context.TotalMass;
        return relative.AtmosphereRelativeVelocity * -0.5 * density * areaMassRatio * context.DragCoefficient *
               relative.AtmosphereRelativeVelocity.Magnitude();
    }

    /// <summary>
    /// Semi-analytic partials of a = −½ ρ B |v_rel| v_rel, B = Cd A/m, v_rel = v − ω × r:
    /// ∂a/∂v = −½ ρ B (|v_rel| I + v_rel v_relᵀ / |v_rel|) and ∂a/∂r = −(∂a/∂v) [ω×] + (−½ B |v_rel| v_rel) (∇ρ)ᵀ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: Montenbruck and Gill, Satellite Orbits, Springer (2000), chapter 7 (partials of the drag
    /// acceleration; confirmed by S. Guillet, 2026-10-13). ω is constant at the fixed epoch of
    /// the state, so ∂v_rel/∂r = −[ω×] and ∂v_rel/∂v = I.
    /// </para>
    /// <para>
    /// The gradient term multiplies ∇ρ by the acceleration per unit density rather than dividing the acceleration by ρ,
    /// so ρ = 0 is covered. ∇ρ comes from central differences of the density chain alone, the one of
    /// <see cref="Apply(StateVector, in ForceEvaluationContext)"/>: rotation to the body-fixed frame computed once, then
    /// planetodetic coordinates and the atmospheric model, without ephemeris or orientation. No published gradient of
    /// the density models exists. At |v_rel| = 0 the acceleration and both blocks vanish.
    /// </para>
    /// </remarks>
    private protected override void AccumulateStatePartialsCore(StateVector stateVector,
        in ForceEvaluationContext context, Span<double> dadr, Span<double> dadv)
    {
        var relative = GetRelativeState(stateVector);
        var vRel = relative.AtmosphereRelativeVelocity;
        double speed = vRel.Magnitude();
        if (speed == 0.0)
        {
            return;
        }

        var rotation = BodyFixedRotation(stateVector);
        double density = Density(relative.Position, rotation, stateVector.Epoch);

        // Acceleration per unit density, −½ B |v_rel| v_rel
        double k = -0.5 * _spacecraft.SectionalArea / context.TotalMass * context.DragCoefficient;
        var accelerationPerDensity = vRel * (k * speed);

        // D = ∂a/∂v = ρ k (|v_rel| I + v_rel v_relᵀ / |v_rel|)
        Span<double> d = stackalloc double[9];
        double diagonal = density * k * speed;
        double outer = density * k / speed;
        for (int i = 0; i < 3; i++)
        {
            double vi = Component(vRel, i);
            for (int j = 0; j < 3; j++)
            {
                d[3 * i + j] = outer * vi * Component(vRel, j) + (i == j ? diagonal : 0.0);
            }
        }

        Span<double> gradient = stackalloc double[3];
        DensityGradient(relative.Position, rotation, stateVector.Epoch, gradient);

        // −D [ω×], with [ω×] = [[0, −ωz, ωy], [ωz, 0, −ωx], [−ωy, ωx, 0]]
        var omega = relative.AngularVelocity;
        for (int i = 0; i < 3; i++)
        {
            double di0 = d[3 * i];
            double di1 = d[3 * i + 1];
            double di2 = d[3 * i + 2];
            double ai = Component(accelerationPerDensity, i);
            dadr[3 * i] += -(di1 * omega.Z - di2 * omega.Y) + ai * gradient[0];
            dadr[3 * i + 1] += -(-di0 * omega.Z + di2 * omega.X) + ai * gradient[1];
            dadr[3 * i + 2] += -(di0 * omega.Y - di1 * omega.X) + ai * gradient[2];
            dadv[3 * i] += di0;
            dadv[3 * i + 1] += di1;
            dadv[3 * i + 2] += di2;
        }
    }

    /// <summary>
    /// ∇ρ with respect to the position relative to the body, in the frame of the state, by central differences of the
    /// density chain at the fixed epoch and rotation, with the step <see cref="DensityGradientRelativeStep"/>; on the side
    /// of the point where the stencil straddles an altitude at which the density jumps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Column j is (ρ(r + h e_j) − ρ(r − h e_j)) / ((r_j + h) − (r_j − h)), the division by the difference of the
    /// perturbed components as stored removing the representation error of the step (Press, Teukolsky, Vetterling and
    /// Flannery, Numerical Recipes, 3rd edition (2007), section 5.7, as the default partials of <see cref="ForceBase"/>).
    /// </para>
    /// <para>
    /// NRLMSISE-00 drops the mixing of a species above an altitude (160 to 450 km), where its density jumps by up to
    /// 1.8e-4 (<see cref="IDensityDiscontinuities"/>). A central difference across such an altitude adds the jump divided
    /// by the step to the gradient, tens of percent of it, which stalls the step control of the variational equations
    /// (#413). There the column is the second-order one-sided difference on the side of r, the gradient of the model on
    /// that side (<see cref="OneSidedDensityDerivative"/>); everywhere else the central difference, bit for bit.
    /// </para>
    /// </remarks>
    internal void DensityGradient(in Vector3 position, in Quaternion? rotation, in Time epoch,
        Span<double> gradient, double relativeStep = DensityGradientRelativeStep)
    {
        double step = relativeStep * System.Math.Max(position.Magnitude(), 1.0);
        for (int j = 0; j < 3; j++)
        {
            double component = Component(position, j);
            double plus = component + step;
            double minus = component - step;
            var plusCoordinates = Geodetic(WithComponent(position, j, plus), rotation, epoch);
            var minusCoordinates = Geodetic(WithComponent(position, j, minus), rotation, epoch);
            if (_discontinuities != null && _discontinuities.DensityInterval(plusCoordinates.Altitude) !=
                _discontinuities.DensityInterval(minusCoordinates.Altitude))
            {
                gradient[j] = OneSidedDensityDerivative(position, j, step, rotation, epoch);
                continue;
            }

            gradient[j] = (Density(plusCoordinates, epoch) - Density(minusCoordinates, epoch)) / (plus - minus);
        }
    }

    /// <summary>
    /// ∂ρ/∂r_j on the side of <paramref name="position"/> of the altitudes at which the density jumps: the three-point
    /// difference on r, r + s h e_j and r + 2 s h e_j, s = ±1 the direction whose two points stay in the interval of r.
    /// </summary>
    /// <remarks>
    /// The weights are those of the first derivative on the three points as stored, x0 = r_j, x1 and x2, with d1 = x1 − x0
    /// and d2 = x2 − x0: −(d1 + d2) / (d1 d2), d2 / (d1 (d2 − d1)) and −d1 / (d2 (d2 − d1)), that is
    /// (−3 ρ0 + 4 ρ1 − ρ2) / (2 s h) for equal spacings, of second order in h like the central difference (B. Fornberg,
    /// "Generation of finite difference formulas on arbitrarily spaced grids", Mathematics of Computation 51 (1988),
    /// 699–706, to be verified by S. Guillet). The side of r is that of the model: a point exactly at a cutoff is where
    /// the model's comparison places it. When neither direction keeps both points in the interval of r, which would need
    /// two discontinuities within 4 h, the central difference is kept.
    /// </remarks>
    private double OneSidedDensityDerivative(in Vector3 position, int j, double step, in Quaternion? rotation,
        in Time epoch)
    {
        var origin = Geodetic(position, rotation, epoch);
        int interval = _discontinuities.DensityInterval(origin.Altitude);
        double x0 = Component(position, j);
        foreach (double direction in stackalloc double[] { 1.0, -1.0 })
        {
            double x1 = x0 + direction * step;
            double x2 = x0 + 2.0 * direction * step;
            var first = Geodetic(WithComponent(position, j, x1), rotation, epoch);
            var second = Geodetic(WithComponent(position, j, x2), rotation, epoch);
            if (_discontinuities.DensityInterval(first.Altitude) != interval ||
                _discontinuities.DensityInterval(second.Altitude) != interval)
            {
                continue;
            }

            double d1 = x1 - x0;
            double d2 = x2 - x0;
            return -(d1 + d2) / (d1 * d2) * Density(origin, epoch) + d2 / (d1 * (d2 - d1)) * Density(first, epoch) -
                   d1 / (d2 * (d2 - d1)) * Density(second, epoch);
        }

        return (Density(WithComponent(position, j, x0 + step), rotation, epoch) -
                Density(WithComponent(position, j, x0 - step), rotation, epoch)) / ((x0 + step) - (x0 - step));
    }

    /// <summary>
    /// The density at a position relative to the body, in the frame of the state: rotated to the body-fixed frame by
    /// <paramref name="rotation"/> (none when the state is in that frame), then planetocentric and planetodetic
    /// coordinates and the atmospheric model, at <paramref name="epoch"/>. The chain of
    /// <see cref="OrbitalParameters.OrbitalParameters.ToPlanetocentric"/>, with the same doubles.
    /// </summary>
    internal double Density(in Vector3 position, in Quaternion? rotation, in Time epoch)
    {
        return Density(Geodetic(position, rotation, epoch), epoch);
    }

    /// <summary>
    /// The planetodetic coordinates of a position relative to the body, in the frame of the state, as the density chain
    /// takes them.
    /// </summary>
    internal Planetodetic Geodetic(in Vector3 position, in Quaternion? rotation, in Time epoch)
    {
        var bodyFixed = rotation.HasValue ? position.Rotate(rotation.Value) : position;
        return _celestialBody.SubObserverPoint(bodyFixed, epoch, Aberration.None)
            .ToPlanetodetic(_celestialBody.Flattening, _celestialBody.EquatorialRadius);
    }

    // The density of the atmospheric context of the coordinates and the epoch, without building it when the model allows
    // (NRLMSISE-00)
    private double Density(in Planetodetic coordinates, in Time epoch)
    {
        return _celestialBody.GetAirDensity(coordinates.Altitude, coordinates.Latitude, coordinates.Longitude, epoch);
    }

    /// <summary>
    /// The rotation from the frame of the state to the body-fixed frame, the rotation of <see cref="Frame.ToFrame"/>
    /// (<see cref="Frame.RotationTo"/>), or null when the state is in the body-fixed frame.
    /// </summary>
    internal Quaternion? BodyFixedRotation(StateVector stateVector)
    {
        return stateVector.Frame == _celestialBody.Frame
            ? null
            : stateVector.Frame.RotationTo(_celestialBody.Frame, stateVector.Epoch);
    }

    /// <summary>
    /// The position relative to the body, the angular velocity of the body and the velocity relative to its
    /// atmosphere, v_rel = v − ω × r, in the frame of the state. Shared by the acceleration and its partials.
    /// </summary>
    internal RelativeState GetRelativeState(StateVector stateVector)
    {
        // Get body-centered state vector (position and velocity relative to the celestial body)
        Vector3 position;
        Vector3 velocity;
        if (EphemerisCache != null && stateVector.Observer as CelestialItem != _celestialBody
                                   && EphemerisCache.Contains(_celestialBody.NaifId, Aberration.None))
        {
            var (bodyPos, bodyVel) = EphemerisCache.GetState(_celestialBody.NaifId, Aberration.None, stateVector.Epoch);
            position = stateVector.Position - bodyPos;
            velocity = stateVector.Velocity - bodyVel;
        }
        else
        {
            var bodyCentered = stateVector.RelativeTo(_celestialBody, Aberration.None).ToStateVector();
            position = bodyCentered.Position;
            velocity = bodyCentered.Velocity;
        }

        // Angular velocity of the body frame relative to the inertial frame of the state, expressed in that frame
        // (Frame.ToFrame convention: target relative to source, in the source axes). It is the same vector for any
        // inertial frame, so drag does not depend on the frame of the propagation.
        var omega = stateVector.Frame.AngularVelocityTo(_celestialBody.Frame, stateVector.Epoch);

        // Atmosphere-relative velocity: v_rel = v_body_centered - omega x r_body_centered
        return new RelativeState(position, omega, velocity - omega.Cross(position));
    }

    private static double Component(in Vector3 vector, int index)
    {
        return index switch
        {
            0 => vector.X,
            1 => vector.Y,
            _ => vector.Z
        };
    }

    private static Vector3 WithComponent(in Vector3 vector, int index, double value)
    {
        return index switch
        {
            0 => new Vector3(value, vector.Y, vector.Z),
            1 => new Vector3(vector.X, value, vector.Z),
            _ => new Vector3(vector.X, vector.Y, value)
        };
    }

    /// <summary>
    /// The state relative to the body and its atmosphere.
    /// </summary>
    /// <param name="Position">Position relative to the body, in m.</param>
    /// <param name="AngularVelocity">ω, angular velocity of the body, in rad/s.</param>
    /// <param name="AtmosphereRelativeVelocity">v_rel = v − ω × r, in m/s.</param>
    internal readonly record struct RelativeState(
        Vector3 Position,
        Vector3 AngularVelocity,
        Vector3 AtmosphereRelativeVelocity);
}
