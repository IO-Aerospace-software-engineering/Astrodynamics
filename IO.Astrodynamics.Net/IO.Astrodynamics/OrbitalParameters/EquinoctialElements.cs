using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
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
        public EquinoctialElements(double p, double f, double g, double h, double k, double l0, ILocalizable observer, Time epoch, Frame frame,
            double? perigeeRadius = null) : base(observer,
            epoch, frame)
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
        /// Calculates the ascending node.
        /// </summary>
        /// <returns>The ascending node.</returns>
        public override double AscendingNode()
        {
            _ascendingNode ??= System.Math.Atan2(K, H);
            return _ascendingNode.Value;
        }

        /// <summary>
        /// Calculates the argument of periapsis.
        /// </summary>
        /// <returns>The argument of periapsis.</returns>
        public override double ArgumentOfPeriapsis()
        {
            _periapsisArgument ??= System.Math.Atan2(G * H - F * K, F * H + G * K);
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
        /// Calculates the true anomaly.
        /// </summary>
        /// <returns>The true anomaly.</returns>
        public override double TrueAnomaly()
        {
            _trueAnomaly ??= L0 - System.Math.Atan2(G, F);
            return _trueAnomaly.Value;
        }

        /// <summary>
        /// Converts the current instance to Keplerian elements.
        /// </summary>
        /// <returns>A new instance of <see cref="KeplerianElements"/>.</returns>
        public override KeplerianElements ToKeplerianElements()
        {
            return new KeplerianElements(SemiMajorAxis(), Eccentricity(), Inclination(), AscendingNode(), ArgumentOfPeriapsis(), MeanAnomaly(), Observer, Epoch, Frame,
                perigeeRadius: _perigeeRadius);
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