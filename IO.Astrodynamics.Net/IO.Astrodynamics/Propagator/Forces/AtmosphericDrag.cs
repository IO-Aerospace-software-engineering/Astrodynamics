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
    /// Relative step δ_ρ of the Cartesian central differences of the density (<see cref="CartesianDensityGradient"/>):
    /// h_ρ = δ_ρ max(|r|, 1 m), r the position relative to the body. Also the step of the one-sided altitude derivative
    /// across the altitudes at which the density jumps (#413).
    /// </summary>
    /// <remarks>
    /// Chosen by the plateau study of phase 2, step 5b, for the Cartesian differences: with NRLMSISE-00 at LEO 400 km,
    /// LEO 800 km and the HEO perigee, the worst relative error of ∇ρ is below 4e-9 from 1e-7 to 1e-6, and 5.6e-10 at
    /// 3e-7 (h ≈ 2 m in LEO). Larger steps leave the truncation of the density scale height H, of order (h / H)², 1.7e-7
    /// at 1e-5; smaller ones the rounding of the model.
    /// </remarks>
    internal const double DensityGradientRelativeStep = 3e-7;

    /// <summary>
    /// Relative step δ_h of the fourth-order derivative of the density in altitude (<see cref="DensityGradient"/>):
    /// h = δ_h max(|r|, 1 m), about 20 m in LEO.
    /// </summary>
    /// <remarks>
    /// Chosen by the plateau study of #420 (<c>DensityGradientStepStudyTests</c>): with NRLMSISE-00 at LEO 400 km, LEO
    /// 800 km and the HEO perigee, ∇ρ within 1.4e-12 to 9.0e-12 of Ridders' derivative of the density chain from 1e-6 to
    /// 3e-5, the resolution of that reference (1.4e-12 to 1.6e-12).
    /// </remarks>
    internal const double AltitudeRelativeStep = 3e-6;

    /// <summary>
    /// Relative step δ_H of the derivatives of the density along the meridian and the parallel
    /// (<see cref="DensityGradient"/>): a length of δ_H max(|r|, 1 m), about 200 m in LEO.
    /// </summary>
    /// <remarks>
    /// The density varies horizontally over thousands of kilometres, so the step can be ten times that of the
    /// altitude, which divides the rounding of the model by as much (#420): plateau from 3e-6 to 3e-5 in the study.
    /// </remarks>
    internal const double HorizontalRelativeStep = 3e-5;

    /// <summary>
    /// The largest longitude step of the east difference of <see cref="DensityGradient"/>, in rad: the chord between its
    /// two points on the parallel cuts the curvature of the parallel, which leaves a truncation of s² / (2 p) times the
    /// second derivative of the density, s the length of the step and p the distance from the polar axis. Without it,
    /// 8.1e-9 of |∇ρ| against Ridders at 1 km from the axis, 8.2e-10 at 10 km; with it, 7.6e-11 and 5.5e-12, for a
    /// noise of 1.6e-11 and 2.0e-12 (NRLMSISE-00, 400 km, measured 2026-10-21, #421).
    /// </summary>
    internal const double MaximumLongitudeStep = 1e-3;

    private readonly Spacecraft _spacecraft;
    private readonly CelestialBody _celestialBody;

    // The altitudes at which the density of the atmosphere jumps, when its model gives them (NRLMSISE-00, #413)
    private readonly IDensityDiscontinuities _discontinuities;

    // dρ/dh in closed form, when the density depends on the altitude alone (US 1976, Mars; #420)
    private readonly IAltitudeDensityDerivative _altitudeDerivative;

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
        _altitudeDerivative = _celestialBody.Atmosphere as IAltitudeDensityDerivative;
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
    /// so ρ = 0 is covered. ∇ρ comes from the density chain alone, the one of
    /// <see cref="Apply(StateVector, in ForceEvaluationContext)"/>, in geodetic coordinates (<see cref="DensityGradient"/>),
    /// without ephemeris or orientation. No published gradient of NRLMSISE-00 exists. At |v_rel| = 0 the acceleration and
    /// both blocks vanish.
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
    /// ∇ρ with respect to the position relative to the body, in the frame of the state, from the derivatives of the
    /// density in the geodetic coordinates of the position (#420).
    /// </summary>
    /// <remarks>
    /// <para>
    /// With h, φ, λ the geodetic altitude, latitude and longitude, dr = dh n̂ + (M + h) dφ ê_N + (N + h) cos φ dλ ê_E, so
    /// ∇ρ = (∂ρ/∂h) n̂ + (∂ρ/∂φ) / (M + h) ê_N + (∂ρ/∂λ) / ((N + h) cos φ) ê_E, with n̂ the normal of the ellipsoid, ê_N
    /// and ê_E the north and east unit vectors, N and M the radii of curvature of the prime vertical and of the meridian,
    /// in the body-fixed frame, rotated back to the frame of the state (W. Torge, J. Müller, Geodesy, 4th edition, de
    /// Gruyter (2012), and B. Hofmann-Wellenhof, H. Moritz, Physical Geodesy, 2nd edition, Springer (2006), ellipsoidal
    /// coordinates; to be verified by S. Guillet). The geodetic Jacobian is analytic, so the conversion of perturbed
    /// positions to geodetic coordinates, whose rounding the Cartesian differences divided by their step (about 4e-10 of
    /// |∇ρ|), no longer enters.
    /// </para>
    /// <para>
    /// A model whose density depends on the altitude alone gives dρ/dh in closed form (<see cref="IAltitudeDensityDerivative"/>);
    /// otherwise ∂ρ/∂h is the fourth-order central difference with the step <see cref="AltitudeRelativeStep"/>, and
    /// ∂ρ/∂φ, ∂ρ/∂λ the second-order central differences over the length <see cref="HorizontalRelativeStep"/> along the
    /// meridian and the parallel, the east one divided by the chord between its two points on the parallel and its
    /// longitude step at most <see cref="MaximumLongitudeStep"/>. Within that length of the polar axis, where the east
    /// step is not defined, the Cartesian central differences of the density chain are kept
    /// (<see cref="CartesianDensityGradient"/>).
    /// </para>
    /// </remarks>
    internal void DensityGradient(in Vector3 position, in Quaternion? rotation, in Time epoch, Span<double> gradient)
    {
        DensityGradient(position, rotation, epoch, gradient, AltitudeRelativeStep, HorizontalRelativeStep);
    }

    /// <summary>
    /// <see cref="DensityGradient(in Vector3, in Quaternion?, in Time, Span{double})"/> with given relative steps, for the
    /// plateau study.
    /// </summary>
    internal void DensityGradient(in Vector3 position, in Quaternion? rotation, in Time epoch, Span<double> gradient,
        double altitudeRelativeStep, double horizontalRelativeStep)
    {
        double scale = System.Math.Max(position.Magnitude(), 1.0);
        var origin = Geodetic(position, rotation, epoch);
        double sinLatitude = System.Math.Sin(origin.Latitude);
        double cosLatitude = System.Math.Cos(origin.Latitude);
        double sinLongitude = System.Math.Sin(origin.Longitude);
        double cosLongitude = System.Math.Cos(origin.Longitude);
        double e2 = _celestialBody.Flattening * (2.0 - _celestialBody.Flattening);
        double w = System.Math.Sqrt(1.0 - e2 * sinLatitude * sinLatitude);
        double parallelRadius = (_celestialBody.EquatorialRadius / w + origin.Altitude) * cosLatitude;
        double horizontalLength = horizontalRelativeStep * scale;

        double up;
        double north = 0.0;
        double east = 0.0;
        if (_altitudeDerivative != null)
        {
            up = _altitudeDerivative.GetDensityAltitudeDerivative(origin.Altitude);
        }
        else
        {
            if (parallelRadius < horizontalLength)
            {
                CartesianDensityGradient(position, rotation, epoch, gradient);
                return;
            }

            up = AltitudeDerivative(origin, epoch, altitudeRelativeStep * scale, DensityGradientRelativeStep * scale);

            double meridianRadius = _celestialBody.EquatorialRadius * (1.0 - e2) / (w * w * w) + origin.Altitude;
            double latitudeStep = horizontalLength / meridianRadius;
            double northPlus = origin.Latitude + latitudeStep;
            double northMinus = origin.Latitude - latitudeStep;
            north = (Density(origin with { Latitude = northPlus }, epoch) -
                     Density(origin with { Latitude = northMinus }, epoch)) / ((northPlus - northMinus) * meridianRadius);

            // Along the parallel, divided by the chord between the two points (#421, H3 B1): the arc would leave a
            // truncation of Δλ² / 6, Δλ reaching 1 rad near the polar axis
            double longitudeStep = System.Math.Min(horizontalLength / parallelRadius, MaximumLongitudeStep);
            double eastPlus = origin.Longitude + longitudeStep;
            double eastMinus = origin.Longitude - longitudeStep;
            east = (Density(origin with { Longitude = eastPlus }, epoch) -
                    Density(origin with { Longitude = eastMinus }, epoch)) /
                   (2.0 * System.Math.Sin(0.5 * (eastPlus - eastMinus)) * parallelRadius);
        }

        // n̂ up + ê_N north + ê_E east, in the body-fixed frame
        var bodyFixed = new Vector3(
            cosLatitude * cosLongitude * up - sinLatitude * cosLongitude * north - sinLongitude * east,
            cosLatitude * sinLongitude * up - sinLatitude * sinLongitude * north + cosLongitude * east,
            sinLatitude * up + cosLatitude * north);
        var inFrame = rotation.HasValue ? bodyFixed.Rotate(rotation.Value.Conjugate()) : bodyFixed;
        gradient[0] = inFrame.X;
        gradient[1] = inFrame.Y;
        gradient[2] = inFrame.Z;
    }

    /// <summary>
    /// ∂ρ/∂h at a position relative to the body, in the frame of the state, as <see cref="DensityGradient"/> takes it:
    /// in closed form when the model gives it, otherwise by differences in altitude.
    /// </summary>
    internal double DensityAltitudeDerivative(in Vector3 position, in Quaternion? rotation, in Time epoch)
    {
        var origin = Geodetic(position, rotation, epoch);
        if (_altitudeDerivative != null)
        {
            return _altitudeDerivative.GetDensityAltitudeDerivative(origin.Altitude);
        }

        double scale = System.Math.Max(position.Magnitude(), 1.0);
        return AltitudeDerivative(origin, epoch, AltitudeRelativeStep * scale, DensityGradientRelativeStep * scale);
    }

    /// <summary>
    /// ∂ρ/∂h at the geodetic coordinates <paramref name="origin"/>: the fourth-order central difference
    /// (−ρ(h + 2δ) + 8 ρ(h + δ) − 8 ρ(h − δ) + ρ(h − 2δ)) / (12 δ), with the weights of the points as stored. Where that
    /// stencil straddles an altitude at which the density jumps (#413), the third-order one-sided difference on h, h + s δ,
    /// h + 2 s δ and h + 3 s δ, s the side whose three points stay in the interval of h; when neither side has them (two
    /// discontinuities within 6 δ), the rule of the Cartesian differences in altitude with the step
    /// <paramref name="cutoffStep"/>: the central difference when its stencil does not straddle a discontinuity, the
    /// second-order one-sided difference on the side of h when it does (<see cref="OneSidedAltitudeDerivative"/>).
    /// </summary>
    /// <remarks>
    /// Weights of the points as stored, from the derivative at h of the Lagrange polynomial through them: B. Fornberg,
    /// "Generation of finite difference formulas on arbitrarily spaced grids", Mathematics of Computation 51 (1988),
    /// 699–706, to be verified by S. Guillet. The third order keeps the truncation over δ_h ≈ 20 m below the
    /// resolution of Ridders' reference across the cutoffs, and the larger step keeps the rounding of the model small,
    /// which the step control of the variational equations follows (#420).
    /// </remarks>
    private double AltitudeDerivative(in Planetodetic origin, in Time epoch, double step, double cutoffStep)
    {
        double h = origin.Altitude;
        double plus2 = h + 2.0 * step;
        double minus2 = h - 2.0 * step;
        if (_discontinuities != null &&
            _discontinuities.DensityInterval(plus2) != _discontinuities.DensityInterval(minus2))
        {
            int interval = _discontinuities.DensityInterval(h);
            foreach (double direction in stackalloc double[] { 1.0, -1.0 })
            {
                double x1 = h + direction * step;
                double x2 = h + 2.0 * direction * step;
                double x3 = h + 3.0 * direction * step;
                if (_discontinuities.DensityInterval(x1) == interval &&
                    _discontinuities.DensityInterval(x2) == interval &&
                    _discontinuities.DensityInterval(x3) == interval)
                {
                    return ThirdOrderOneSided(origin, epoch, x1, x2, x3);
                }
            }

            var plusCoordinates = origin with { Altitude = h + cutoffStep };
            var minusCoordinates = origin with { Altitude = h - cutoffStep };
            if (_discontinuities.DensityInterval(plusCoordinates.Altitude) == interval &&
                _discontinuities.DensityInterval(minusCoordinates.Altitude) == interval)
            {
                return (Density(plusCoordinates, epoch) - Density(minusCoordinates, epoch)) /
                       (plusCoordinates.Altitude - minusCoordinates.Altitude);
            }

            return OneSidedAltitudeDerivative(origin, epoch, cutoffStep, plusCoordinates, minusCoordinates);
        }

        double plus = h + step;
        double minus = h - step;
        return (-Density(origin with { Altitude = plus2 }, epoch) + 8.0 * Density(origin with { Altitude = plus }, epoch) -
                8.0 * Density(origin with { Altitude = minus }, epoch) + Density(origin with { Altitude = minus2 }, epoch)) /
               (6.0 * (plus - minus));
    }

    // The third-order one-sided difference on h and the three points x1, x2, x3 on one side, with the weights of the
    // points as stored (Lagrange's derivative at h of the cubic through the four points; Fornberg, op. cit.)
    private double ThirdOrderOneSided(in Planetodetic origin, in Time epoch, double x1, double x2, double x3)
    {
        double h = origin.Altitude;
        double d1 = x1 - h;
        double d2 = x2 - h;
        double d3 = x3 - h;
        double w1 = d2 * d3 / (d1 * (d2 - d1) * (d3 - d1));
        double w2 = -d1 * d3 / (d2 * (d2 - d1) * (d3 - d2));
        double w3 = d1 * d2 / (d3 * (d3 - d1) * (d3 - d2));
        double w0 = -(w1 + w2 + w3);
        return w0 * Density(origin, epoch) + w1 * Density(origin with { Altitude = x1 }, epoch) +
               w2 * Density(origin with { Altitude = x2 }, epoch) + w3 * Density(origin with { Altitude = x3 }, epoch);
    }

    // The one-sided difference of #413 in altitude: on h, h + s δ and h + 2 s δ, s the side whose two points stay in the
    // interval of h; the central difference when neither does
    private double OneSidedAltitudeDerivative(in Planetodetic origin, in Time epoch, double step,
        in Planetodetic plusCoordinates, in Planetodetic minusCoordinates)
    {
        double h = origin.Altitude;
        int interval = _discontinuities.DensityInterval(h);
        foreach (double direction in stackalloc double[] { 1.0, -1.0 })
        {
            var first = direction > 0.0 ? plusCoordinates : minusCoordinates;
            var second = origin with { Altitude = h + 2.0 * direction * step };
            if (_discontinuities.DensityInterval(first.Altitude) != interval ||
                _discontinuities.DensityInterval(second.Altitude) != interval)
            {
                continue;
            }

            double d1 = first.Altitude - h;
            double d2 = second.Altitude - h;
            return -(d1 + d2) / (d1 * d2) * Density(origin, epoch) + d2 / (d1 * (d2 - d1)) * Density(first, epoch) -
                   d1 / (d2 * (d2 - d1)) * Density(second, epoch);
        }

        return (Density(plusCoordinates, epoch) - Density(minusCoordinates, epoch)) /
               (plusCoordinates.Altitude - minusCoordinates.Altitude);
    }

    /// <summary>
    /// ∇ρ with respect to the position relative to the body, in the frame of the state, by central differences of the
    /// density chain at the fixed epoch and rotation, with the step <see cref="DensityGradientRelativeStep"/>; on the side
    /// of the point where the stencil straddles an altitude at which the density jumps. The gradient of the drag partials
    /// before #420, kept within <see cref="HorizontalRelativeStep"/> of the polar axis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Column j is (ρ(r + h e_j) − ρ(r − h e_j)) / ((r_j + h) − (r_j − h)), the division by the difference of the
    /// perturbed components as stored removing the representation error of the step (Press, Teukolsky, Vetterling and
    /// Flannery, Numerical Recipes, 3rd edition (2007), section 5.7, as the default partials of <see cref="ForceBase"/>).
    /// Each perturbed position goes through the geodetic conversion, whose rounding leaves a noise of about 4e-10 of
    /// |∇ρ| at the default step, which the step control of the variational equations follows in dense air (#420).
    /// </para>
    /// <para>
    /// NRLMSISE-00 drops the mixing of a species above an altitude (160 to 450 km), where its density jumps by up to
    /// 1.8e-4 (<see cref="IDensityDiscontinuities"/>). A central difference across such an altitude adds the jump divided
    /// by the step to the gradient, tens of percent of it, which stalls the step control of the variational equations
    /// (#413). There the column is the second-order one-sided difference on the side of r, the gradient of the model on
    /// that side (<see cref="OneSidedDensityDerivative"/>); everywhere else the central difference, bit for bit.
    /// </para>
    /// </remarks>
    internal void CartesianDensityGradient(in Vector3 position, in Quaternion? rotation, in Time epoch,
        Span<double> gradient, double relativeStep = DensityGradientRelativeStep)
    {
        double step = relativeStep * System.Math.Max(position.Magnitude(), 1.0);
        Planetodetic? origin = null;
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
                // The coordinates of r once, for every column whose stencil straddles a cutoff
                origin ??= Geodetic(position, rotation, epoch);
                gradient[j] = OneSidedDensityDerivative(position, j, step, rotation, epoch, origin.Value,
                    plusCoordinates, minusCoordinates);
                continue;
            }

            gradient[j] = CentralDensityDerivative(component, step, plusCoordinates, minusCoordinates, epoch);
        }
    }

    /// <summary>
    /// ∂ρ/∂r_j on the side of <paramref name="position"/> of the altitudes at which the density jumps: the three-point
    /// difference on r, r + s h e_j and r + 2 s h e_j, s = ±1 the direction whose first point, r ± h e_j of the central
    /// stencil, is in the interval of r.
    /// </summary>
    /// <remarks>
    /// The weights are those of the first derivative on the three points as stored, x0 = r_j, x1 and x2, with d1 = x1 − x0
    /// and d2 = x2 − x0: −(d1 + d2) / (d1 d2), d2 / (d1 (d2 − d1)) and −d1 / (d2 (d2 − d1)), that is
    /// (−3 ρ0 + 4 ρ1 − ρ2) / (2 s h) for equal spacings, of second order in h like the central difference (B. Fornberg,
    /// "Generation of finite difference formulas on arbitrarily spaced grids", Mathematics of Computation 51 (1988),
    /// 699–706, to be verified by S. Guillet). The side of r is that of the model: a point exactly at a cutoff is where
    /// the model's comparison places it. The first point is that of the central stencil, already converted; only the
    /// second is new. When neither point of the stencil is in the interval of r, or the second point is not, which would
    /// need two discontinuities within 3 h, the central difference is kept.
    /// </remarks>
    private double OneSidedDensityDerivative(in Vector3 position, int j, double step, in Quaternion? rotation,
        in Time epoch, in Planetodetic origin, in Planetodetic plusCoordinates, in Planetodetic minusCoordinates)
    {
        int interval = _discontinuities.DensityInterval(origin.Altitude);
        double x0 = Component(position, j);
        double direction;
        Planetodetic first;
        if (_discontinuities.DensityInterval(plusCoordinates.Altitude) == interval)
        {
            direction = 1.0;
            first = plusCoordinates;
        }
        else if (_discontinuities.DensityInterval(minusCoordinates.Altitude) == interval)
        {
            direction = -1.0;
            first = minusCoordinates;
        }
        else
        {
            return CentralDensityDerivative(x0, step, plusCoordinates, minusCoordinates, epoch);
        }

        double x1 = x0 + direction * step;
        double x2 = x0 + 2.0 * direction * step;
        var second = Geodetic(WithComponent(position, j, x2), rotation, epoch);
        if (_discontinuities.DensityInterval(second.Altitude) != interval)
        {
            return CentralDensityDerivative(x0, step, plusCoordinates, minusCoordinates, epoch);
        }

        double d1 = x1 - x0;
        double d2 = x2 - x0;
        return -(d1 + d2) / (d1 * d2) * Density(origin, epoch) + d2 / (d1 * (d2 - d1)) * Density(first, epoch) -
               d1 / (d2 * (d2 - d1)) * Density(second, epoch);
    }

    // The central difference on the points of the stencil already converted
    private double CentralDensityDerivative(double x0, double step, in Planetodetic plusCoordinates,
        in Planetodetic minusCoordinates, in Time epoch)
    {
        return (Density(plusCoordinates, epoch) - Density(minusCoordinates, epoch)) / ((x0 + step) - (x0 - step));
    }

    /// <summary>
    /// The density at a position relative to the body, in the frame of the state: rotated to the body-fixed frame by
    /// <paramref name="rotation"/> (none when the state is in that frame), then the exact geodetic coordinates on the
    /// ellipsoid of the body (<see cref="GeodeticCoordinates"/>, a closed form, smooth for the central differences of
    /// <see cref="DensityGradient"/>) and the atmospheric model, at <paramref name="epoch"/>.
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
        return GeodeticCoordinates.FromBodyFixed(bodyFixed, _celestialBody.EquatorialRadius, _celestialBody.Flattening);
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
