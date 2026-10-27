using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.OrbitalParameters
{
    /// <summary>
    /// Represents the equinoctial elements of an orbit, which are a set of parameters that define the shape and orientation of an orbit.
    /// </summary>
    public class EquinoctialElements : OrbitalParameters, IEquatable<EquinoctialElements>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EquinoctialElements"/> class.
        /// </summary>
        /// <param name="p">The semiparameter.</param>
        /// <param name="f">The equinoctial parameter f.</param>
        /// <param name="g">The equinoctial parameter g.</param>
        /// <param name="h">The equinoctial parameter h.</param>
        /// <param name="k">The equinoctial parameter k.</param>
        /// <param name="l0">The true longitude.</param>
        /// <param name="observer">The observer.</param>
        /// <param name="epoch">The epoch.</param>
        /// <param name="frame">The reference frame.</param>
        /// <param name="perigeeRadius">The perigee radius (optional).</param>
        /// <param name="elementsType">The type of orbital elements (osculating or mean). Defaults to osculating.</param>
        public EquinoctialElements(double p, double f, double g, double h, double k, double l0, ILocalizable observer, Time epoch, Frame frame,
            double? perigeeRadius = null, OrbitalElementsType elementsType = OrbitalElementsType.Osculating) : base(observer,
            epoch, frame, elementsType)
        {
            P = p;
            F = f;
            G = g;
            H = h;
            K = k;
            L0 = l0;
            _perigeeRadius = perigeeRadius;
        }

        /// <summary>
        /// Gets the semiparameter.
        /// </summary>
        public double P { get; }

        /// <summary>
        /// Gets the equinoctial parameter f.
        /// </summary>
        public double F { get; }

        /// <summary>
        /// Gets the equinoctial parameter g.
        /// </summary>
        public double G { get; }

        /// <summary>
        /// Gets the equinoctial parameter h.
        /// </summary>
        public double H { get; }

        /// <summary>
        /// Gets the equinoctial parameter k.
        /// </summary>
        public double K { get; }

        /// <summary>
        /// Gets the true longitude.
        /// </summary>
        public double L0 { get; }

        /// <summary>
        /// Gets the equinoctial ex parameter (equivalent to F in some conventions).
        /// </summary>
        /// <returns>The equinoctial ex parameter.</returns>
        public double EquinoctialEx() => F;

        /// <summary>
        /// Gets the equinoctial ey parameter (equivalent to G in some conventions).
        /// </summary>
        /// <returns>The equinoctial ey parameter.</returns>
        public double EquinoctialEy() => G;

        /// <summary>
        /// Gets the hx parameter (equivalent to H in some conventions).
        /// </summary>
        /// <returns>The hx parameter.</returns>
        public double Hx() => H;

        /// <summary>
        /// Gets the hy parameter (equivalent to K in some conventions).
        /// </summary>
        /// <returns>The hy parameter.</returns>
        public double Hy() => K;

        /// <summary>
        /// Gets the lv parameter (equivalent to L0 in some conventions).
        /// </summary>
        /// <returns>The lv parameter.</returns>
        public double Lv() => L0;

        /// <summary>
        /// Converts the current instance to equinoctial elements.
        /// </summary>
        /// <returns>The current instance of <see cref="EquinoctialElements"/>.</returns>
        public override EquinoctialElements ToEquinoctial()
        {
            return this;
        }

        /// <summary>
        /// Calculates the semi-major axis.
        /// </summary>
        /// <returns>The semi-major axis.</returns>
        public override double SemiMajorAxis()
        {
            if (_semiMajorAxis.HasValue)
            {
                return _semiMajorAxis.Value;
            }

            if (IsParabolic())
            {
                _semiMajorAxis = double.PositiveInfinity;
            }
            else
            {
                _semiMajorAxis = P / (1 - F * F - G * G);
            }

            return _semiMajorAxis.Value;
        }

        /// <summary>
        /// Calculates the eccentricity.
        /// </summary>
        /// <returns>The eccentricity.</returns>
        public override double Eccentricity()
        {
            _eccentricity ??= System.Math.Sqrt(F * F + G * G);
            return _eccentricity.Value;
        }

        /// <summary>
        /// Calculates the inclination.
        /// </summary>
        /// <returns>The inclination.</returns>
        public override double Inclination()
        {
            _inclination ??= 2 * System.Math.Atan(System.Math.Sqrt(H * H + K * K));
            return _inclination.Value;
        }

        /// <summary>
        /// The right ascension of the ascending node, atan2(K, H); 0 for an equatorial orbit (H = K = 0, either sign),
        /// whose node is undefined, as for <see cref="StateVector"/> (#452).
        /// </summary>
        /// <returns>The ascending node.</returns>
        public override double AscendingNode()
        {
            _ascendingNode ??= IsExactlyEquatorial() ? 0.0 : System.Math.Atan2(K, H);
            return _ascendingNode.Value;
        }

        /// <summary>
        /// The argument of periapsis, atan2(GH − FK, FH + GK); ϖ − Ω in [0, 2π] when H = K = 0 or F = G = 0, with ϖ
        /// the longitude of periapsis and ϖ = Ω for a circular orbit, so ω = 0, and when e·tan(i/2) is below the
        /// smallest normal double, where the products of atan2 underflow (#452).
        /// </summary>
        /// <returns>The argument of periapsis.</returns>
        public override double ArgumentOfPeriapsis()
        {
            if (_periapsisArgument is null)
            {
                double y = G * H - F * K;
                double x = F * H + G * K;

                // Regular elements keep the expression they always had, bit for bit: normalizing it would change them.
                // Its arguments are both zero when F = G = 0 or H = K = 0, and lose their precision, or vanish, when
                // e·tan(i/2) is below the smallest normal double: (FH + GK)² + (GH − FK)² = e² tan²(i/2) (#452)
                _periapsisArgument = System.Math.Abs(x) + System.Math.Abs(y) < SmallestNormal
                    ? WithinOneTurn(LongitudeOfPeriapsis() - AscendingNode())
                    : System.Math.Atan2(y, x);
            }

            return _periapsisArgument.Value;
        }

        /// <summary>
        /// Calculates the mean anomaly.
        /// </summary>
        /// <returns>The mean anomaly.</returns>
        public override double MeanAnomaly()
        {
            _meanAnomaly ??= TrueAnomalyToMeanAnomaly(TrueAnomaly(), Eccentricity(), EccentricAnomaly(TrueAnomaly()));
            return _meanAnomaly.Value;
        }

        /// <summary>
        /// The true anomaly, L0 − atan2(G, F); L0 − ϖ in [0, 2π] when H = K = 0 or F = G = 0: the argument of latitude
        /// for a circular orbit, the true longitude for a circular equatorial one (#452).
        /// </summary>
        /// <returns>The true anomaly.</returns>
        public override double TrueAnomaly()
        {
            // Regular elements keep the expression they always had, bit for bit: normalizing it would change their mean
            // anomaly (#452)
            _trueAnomaly ??= HasUndefinedAngles()
                ? WithinOneTurn(TrueLongitudeWithinOneTurn() - LongitudeOfPeriapsis())
                : L0 - System.Math.Atan2(G, F);
            return _trueAnomaly.Value;
        }

        /// <summary>
        /// The state vector of the elements, computed from them directly, without the Keplerian angles, so that it
        /// stays defined for a circular or an equatorial orbit (#452).
        /// </summary>
        /// <remarks>
        /// <para>
        /// With s² = 1 + H² + K², the unit vectors of the equinoctial frame are f̂ = (1 − K² + H², 2HK, −2K) / s²
        /// and ĝ = (2HK, 1 + K² − H², 2H) / s²: the first two columns of Q in the GMAT Mathematical Specification
        /// R2026a, §3.1.4, Eqs 3.44 to 3.47, with the retrograde factor j = 1 (GMAT's p is <see cref="K"/> and its q
        /// is <see cref="H"/>). With the true longitude L = <see cref="L0"/> (Walker, Ireland and Owens, "A set of
        /// modified equinoctial orbit elements", Celestial Mechanics 36, 1985; confirmed by S. Guillet, 2026-10-27):
        /// r = p / (1 + F cos L + G sin L), r = r (cos L f̂ + sin L ĝ),
        /// v = √(μ/p) (−(G + sin L) f̂ + (F + cos L) ĝ).
        /// </para>
        /// <para>
        /// p is <see cref="P"/> when it is finite. A parabolic orbit usually has no finite P (∞ × 0 from Keplerian
        /// elements, ±∞ from a state vector); p is then q (1 + e), with q the perigee radius given to the constructor:
        /// the conic of the eccentricity of the elements with its periapsis at q, as SPICE CONICS, 2q when e = 1.
        /// </para>
        /// <para>
        /// The state is in the frame of the elements, at their epoch, relative to their observer. The only singular
        /// orbit is the retrograde equatorial one (i = π), where H and K grow without bound; in doubles they stay
        /// finite (tan(π/2) = 1.6e16) and the state with them, H and K being scaled beyond 1e150, where their squares
        /// would overflow.
        /// </para>
        /// </remarks>
        /// <returns>The state vector at the epoch of the elements.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the elements are mean elements, as by <see cref="OrbitalParameters.ToStateVector()"/>, and when
        /// P is not finite unless the orbit is parabolic and its perigee radius is given.
        /// </exception>
        public override StateVector ToStateVector()
        {
            ThrowIfMeanElements();

            if (_stateVector is null)
            {
                double p = SemiLatusRectumOfTheElements();

                // Beyond 1e150 (i within 2e-150 rad of π), H² + K² would overflow: H, K and 1 are then scaled by the
                // same power of two, which is exact. Below, the scale is 1 and changes no bit
                double largest = System.Math.Max(System.Math.Abs(H), System.Math.Abs(K));
                double scale = largest > 1e150 ? System.Math.ScaleB(1.0, -System.Math.ILogB(largest)) : 1.0;
                double hs = H * scale;
                double ks = K * scale;
                double one = scale * scale;
                double h2 = hs * hs;
                double k2 = ks * ks;
                double hk = hs * ks;
                double s2 = one + h2 + k2;
                var f = new Vector3((one - k2 + h2) / s2, 2.0 * hk / s2, -2.0 * ks * scale / s2);
                var g = new Vector3(2.0 * hk / s2, (one + k2 - h2) / s2, 2.0 * hs * scale / s2);
                double cosL = System.Math.Cos(L0);
                double sinL = System.Math.Sin(L0);
                double r = p / (1.0 + F * cosL + G * sinL);
                double rf = r * cosL;
                double rg = r * sinL;
                double sqrtMuOverP = System.Math.Sqrt(Observer.GM / p);
                double vf = -sqrtMuOverP * (G + sinL);
                double vg = sqrtMuOverP * (F + cosL);
                _stateVector = new StateVector(
                    new Vector3(rf * f.X + rg * g.X, rf * f.Y + rg * g.Y, rf * f.Z + rg * g.Z),
                    new Vector3(vf * f.X + vg * g.X, vf * f.Y + vg * g.Y, vf * f.Z + vg * g.Z), Observer, Epoch, Frame);
            }

            return _stateVector;
        }

        /// <summary>
        /// p of <see cref="ToStateVector()"/>: <see cref="P"/> when it is finite, q (1 + e) for a parabolic orbit
        /// whose perigee radius q is given. Not <see cref="OrbitalParameters.PerigeeRadius"/>, which computes the
        /// semi-latus rectum from the state of a parabolic orbit without a perigee radius, and so from this method.
        /// </summary>
        private double SemiLatusRectumOfTheElements()
        {
            if (double.IsFinite(P))
            {
                return P;
            }

            if (IsParabolic() && _perigeeRadius.HasValue)
            {
                // The conic of the eccentricity of the elements, |e − 1| < 1e-6, with its periapsis at the perigee
                // radius, as SPICE CONICS: 2q would put it at 2q / (1 + e), up to 5e-7 of q away (#452)
                return _perigeeRadius.Value * (1.0 + Eccentricity());
            }

            throw new InvalidOperationException("The semi-latus rectum P is not finite and no perigee radius was given: " +
                                                "the state of these equinoctial elements is undefined.");
        }

        // The smallest normal double: below it, the products of the argument of periapsis lose their precision
        private const double SmallestNormal = 2.2250738585072014e-308;

        // H = K = 0 exactly, either sign of zero (−0.0 == 0.0): the node is undefined
        private bool IsExactlyEquatorial() => H == 0.0 && K == 0.0;

        // F = G = 0 exactly, either sign of zero; not IsCircular(), which means e < 1e-3
        private bool IsExactlyCircular() => F == 0.0 && G == 0.0;

        private bool HasUndefinedAngles() => IsExactlyEquatorial() || IsExactlyCircular();

        // ϖ, the longitude of periapsis: Ω for a circular orbit, so that ω = 0 and ν is the argument of latitude
        private double LongitudeOfPeriapsis() => IsExactlyCircular() ? AscendingNode() : System.Math.Atan2(G, F);

        // L0 within one turn of zero. Beyond, it is reduced through its sine and cosine, whose argument reduction is
        // exact: a remainder by the double 2π (NormalizeAngle) loses 2.4e-16 rad per turn, 3.9e-7 rad at 1e10 rad (#452)
        private double TrueLongitudeWithinOneTurn() => System.Math.Abs(L0) <= Constants._2PI
            ? L0
            : System.Math.Atan2(System.Math.Sin(L0), System.Math.Cos(L0));

        // An angle of at most a few turns in [0, 2π] (2π only for a tiny negative angle), +0.0 for a zero of either
        // sign, which a TLE would print as -0.0000 (#452)
        private static double WithinOneTurn(double angle) => SpecialFunctions.NormalizeAngle(angle) + 0.0;

        /// <summary>
        /// Converts the current instance to Keplerian elements.
        /// </summary>
        /// <returns>A new instance of <see cref="KeplerianElements"/>.</returns>
        public override KeplerianElements ToKeplerianElements()
        {
            return new KeplerianElements(SemiMajorAxis(), Eccentricity(), Inclination(), AscendingNode(), ArgumentOfPeriapsis(), MeanAnomaly(), Observer, Epoch, Frame,
                perigeeRadius: _perigeeRadius, elementsType: ElementsType);
        }

        #region Operators

        public bool Equals(EquinoctialElements other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return P.Equals(other.P) && F.Equals(other.F) && G.Equals(other.G) && H.Equals(other.H) && K.Equals(other.K) && L0.Equals(other.L0) &&
                   Observer.NaifId == other.Observer.NaifId && Epoch == other.Epoch && Frame == other.Frame;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EquinoctialElements)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), P, F, G, H, K, L0);
        }

        public static bool operator ==(EquinoctialElements left, EquinoctialElements right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(EquinoctialElements left, EquinoctialElements right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return $"Epoch : {Epoch.ToString()} P : {P} F : {F} G : {G} H : {H} K {K} L0 : {L0} Frame : {Frame.Name}";
        }

        #endregion
    }
}