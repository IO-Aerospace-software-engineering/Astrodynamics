using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;


namespace IO.Astrodynamics.OrbitalParameters
{
    /// <summary>
    /// Represents the Keplerian elements of an orbit, which are a set of parameters that define the shape and orientation of an orbit.
    /// </summary>
    public class KeplerianElements : OrbitalParameters, IEquatable<KeplerianElements>
    {
        /// <summary>
        /// Semi major Axis
        /// </summary>
        public double A { get; }

        /// <summary>
        /// Eccentricity
        /// </summary>
        public double E { get; }

        /// <summary>
        /// Inclination
        /// </summary>
        public double I { get; }

        /// <summary>
        /// Right Ascending Node
        /// </summary>
        public double RAAN { get; }

        /// <summary>
        /// Argument of Periapsis
        /// </summary>
        public double AOP { get; }

        /// <summary>
        /// Mean Anomaly
        /// </summary>
        public double M { get; }

        /// <summary>
        /// The rounding allowed between a semi-major axis and a perigee radius of one conic within 1e-6 of e = 1:
        /// |q/a − (1 − e)| ≤ 6 ε (1 + e). The elements the library builds there, from a state (−μ/(2ξ) and h²/(μ(1 + e))),
        /// through equinoctial elements, along the two-body motion and from SPICE's oscltx, measured at most 3.0 ε (1 + e)
        /// on Linux over 6400 states of the band (|e − 1| from 0 to 9.99e-7, q from 6600 km to 1 AU, |M| up to 2e6 rad);
        /// twice, provisional until the Windows x64 and macOS arm64 CI logs (the review of #459).
        /// </summary>
        private const double OneConicTolerance = 6.0 * 2.220446049250313e-16;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="semiMajorAxis">The semi-major axis, m: within 1e-6 of e = 1 that of the conic of the perigee radius,
        /// q / (1 − e) to rounding (|q/a − (1 − e)| ≤ 6 ε (1 + e)), +Infinity at e = 1 exactly (#459).</param>
        /// <param name="eccentricity"></param>
        /// <param name="inclination"></param>
        /// <param name="rigthAscendingNode"></param>
        /// <param name="argumentOfPeriapsis"></param>
        /// <param name="meanAnomaly"></param>
        /// <param name="observer"></param>
        /// <param name="epoch"></param>
        /// <param name="frame"></param>
        /// <param name="trueAnomaly"></param>
        /// <param name="period">The period of an ellipse, when known; <see cref="OrbitalParameters.Period"/> throws for an open
        /// orbit whatever is given (#663).</param>
        /// <param name="perigeeRadius">The perigee radius, m: required within 1e-6 of e = 1 (a parabola, whose
        /// <paramref name="semiMajorAxis"/> is +Infinity), finite and above 0 when given.</param>
        /// <param name="elementsType">Type of orbital elements (osculating or mean). Defaults to osculating.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="eccentricity"/> is not finite or is negative (#476),
        /// or <paramref name="perigeeRadius"/> is given and not finite and above 0 (#726).</exception>
        /// <exception cref="ArgumentException">An element is not finite (#726): <paramref name="semiMajorAxis"/> may be
        /// +Infinity only within 1e-6 of e = 1 given the perigee radius; or an orbit within 1e-6 of e = 1 has no perigee
        /// radius (<paramref name="perigeeRadius"/>), or a semi-major axis that is not the conic's of its perigee radius,
        /// finite at e = 1 exactly or +Infinity beyond a few ulps of it (<paramref name="semiMajorAxis"/>, #459);
        /// or <paramref name="inclination"/> is outside [−π, π]. The exception names the parameter.</exception>
        public KeplerianElements(double semiMajorAxis, double eccentricity, double inclination, double rigthAscendingNode, double argumentOfPeriapsis, double meanAnomaly,
            ILocalizable observer, Time epoch, Frame frame, double? trueAnomaly = null, TimeSpan? period = null, double? perigeeRadius = null,
            OrbitalElementsType elementsType = OrbitalElementsType.Osculating) : base(observer, epoch, frame, elementsType)
        {
            RequireEccentricity(eccentricity);
            bool parabolic = System.Math.Abs(eccentricity - 1.0) < 1E-06;

            // No conic has a non-finite element but the semi-major axis of a parabola (#726)
            if (double.IsNaN(semiMajorAxis) || double.IsNegativeInfinity(semiMajorAxis) ||
                (double.IsPositiveInfinity(semiMajorAxis) && !parabolic))
            {
                throw new ArgumentException(
                    $"The semi-major axis must be finite, +Infinity only for a parabola (|e − 1| < 1e-6): a = {semiMajorAxis:R} m, e = {eccentricity:R}.",
                    nameof(semiMajorAxis));
            }

            RequireInclination(inclination, nameof(inclination));
            RequireFinite(rigthAscendingNode, nameof(rigthAscendingNode));
            RequireFinite(argumentOfPeriapsis, nameof(argumentOfPeriapsis));
            RequireFinite(meanAnomaly, nameof(meanAnomaly));
            if (trueAnomaly.HasValue)
            {
                RequireFinite(trueAnomaly.Value, nameof(trueAnomaly));
            }

            if (perigeeRadius is { } q && !(double.IsFinite(q) && q > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(perigeeRadius), q, "The perigee radius must be finite and above 0.");
            }

            // Within 1e-6 of e = 1 the anomalies are Barker's, read from the perigee radius: it is required (#368)
            if (parabolic && perigeeRadius is null)
            {
                throw new ArgumentException("Within 1e-6 of e = 1 the perigee radius is required.", nameof(perigeeRadius));
            }

            // ... with the semi-major axis of the same conic, a (1 − e) = q, either side of e = 1 (#459; before, +Infinity
            // only): +Infinity at e = 1 exactly, and otherwise q/a = 1 − e to rounding, which a = +Infinity meets only within a
            // few ulps of e = 1 (the state of an exact parabola, whose e rounds off 1 where ξ rounds to 0). The semi-major
            // axis, the mean motion, the period and the apoapsis come from a, the state and P from q (the review of #459)
            if (parabolic && (eccentricity == 1.0
                    ? !double.IsPositiveInfinity(semiMajorAxis)
                    : !(System.Math.Abs(perigeeRadius!.Value / semiMajorAxis - (1.0 - eccentricity)) <= OneConicTolerance * (1.0 + eccentricity))))
            {
                throw new ArgumentException(
                    $"Within 1e-6 of e = 1 the semi-major axis must be that of the conic of the perigee radius, q / (1 − e), " +
                    $"+Infinity at e = 1: a = {semiMajorAxis:R} m, e = {eccentricity:R}, q = {perigeeRadius!.Value:R} m.",
                    nameof(semiMajorAxis));
            }

            A = semiMajorAxis;
            E = eccentricity;
            I = inclination;
            RAAN = SpecialFunctions.NormalizeAngle(rigthAscendingNode);
            AOP = SpecialFunctions.NormalizeAngle(argumentOfPeriapsis);

            // An angle on an ellipse; on an open orbit it grows without bound, negative before the periapsis (#368)
            M = eccentricity < 1.0 - 1E-06 ? SpecialFunctions.NormalizeAngle(meanAnomaly) : meanAnomaly;
            _period = period;
            _trueAnomaly = trueAnomaly;
            _perigeeRadius = perigeeRadius;
        }

        /// <summary>
        /// Private constructor for creating mean Keplerian elements from mean motion.
        /// Semi-major axis is computed from mean motion using Kepler's third law.
        /// </summary>
        /// <param name="meanMotion">Mean motion in rad/s</param>
        /// <param name="eccentricity">Eccentricity</param>
        /// <param name="inclination">Inclination in radians</param>
        /// <param name="rightAscendingNode">Right ascension of ascending node in radians</param>
        /// <param name="argumentOfPeriapsis">Argument of periapsis in radians</param>
        /// <param name="meanAnomaly">Mean anomaly in radians</param>
        /// <param name="observer">Central body</param>
        /// <param name="epoch">Epoch time</param>
        /// <param name="frame">Reference frame</param>
        /// <param name="elementsType">Type of orbital elements</param>
        /// <param name="inclinationName">The name of the inclination as the caller took it, for its exception.</param>
        /// <param name="rightAscendingNodeName">The name of the node as the caller took it.</param>
        /// <param name="argumentOfPeriapsisName">The name of the argument of periapsis as the caller took it.</param>
        /// <param name="meanAnomalyName">The name of the mean anomaly as the caller took it.</param>
        private KeplerianElements(double meanMotion, double eccentricity, double inclination, double rightAscendingNode,
            double argumentOfPeriapsis, double meanAnomaly, ILocalizable observer, Time epoch, Frame frame,
            OrbitalElementsType elementsType, string inclinationName, string rightAscendingNodeName,
            string argumentOfPeriapsisName, string meanAnomalyName)
            : base(observer, epoch, frame, elementsType)
        {
            // A mean motion is finite and above 0: 0 gave a = +Infinity, a negative one the a of its opposite (#726)
            if (!(double.IsFinite(meanMotion) && meanMotion > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(meanMotion), meanMotion, "The mean motion must be finite and above 0.");
            }

            RequireEccentricity(eccentricity);
            RequireInclination(inclination, inclinationName);
            RequireFinite(rightAscendingNode, rightAscendingNodeName);
            RequireFinite(argumentOfPeriapsis, argumentOfPeriapsisName);
            RequireFinite(meanAnomaly, meanAnomalyName);

            // Compute semi-major axis from mean motion using Kepler's third law: n = sqrt(GM/a^3)
            A = System.Math.Cbrt(Constants.G * observer.Mass / (meanMotion * meanMotion));
            E = eccentricity;
            I = inclination;
            RAAN = SpecialFunctions.NormalizeAngle(rightAscendingNode);
            AOP = SpecialFunctions.NormalizeAngle(argumentOfPeriapsis);
            M = SpecialFunctions.NormalizeAngle(meanAnomaly);

            // Cache the original mean motion to preserve precision
            _meanMotion = meanMotion;
        }

        /// <summary>
        /// Creates mean Keplerian elements from OMM (Orbit Mean-elements Message) data.
        /// This factory method accepts values in OMM native units and handles all conversions internally.
        /// The resulting elements will have <see cref="OrbitalParameters.ElementsType"/> set to <see cref="OrbitalElementsType.Mean"/>.
        /// </summary>
        /// <param name="meanMotion">Mean motion in revolutions per day</param>
        /// <param name="eccentricity">Eccentricity (dimensionless)</param>
        /// <param name="inclination">Inclination in degrees</param>
        /// <param name="raan">Right ascension of ascending node in degrees</param>
        /// <param name="argumentOfPeriapsis">Argument of periapsis in degrees</param>
        /// <param name="meanAnomaly">Mean anomaly in degrees</param>
        /// <param name="observer">Central body (typically Earth)</param>
        /// <param name="epoch">Epoch time</param>
        /// <param name="frame">Reference frame (typically TEME for TLE-compatible OMM)</param>
        /// <returns>Mean Keplerian elements with preserved mean motion precision</returns>
        /// <exception cref="ArgumentOutOfRangeException">The mean motion is not finite and above 0 (#726), or the
        /// eccentricity is not finite or is negative (#476).</exception>
        /// <exception cref="ArgumentException">An angle is not finite (#726), or the inclination is out of range; the
        /// exception names the parameter.</exception>
        public static KeplerianElements FromOMM(
            double meanMotion,
            double eccentricity,
            double inclination,
            double raan,
            double argumentOfPeriapsis,
            double meanAnomaly,
            ILocalizable observer,
            Time epoch,
            Frame frame)
        {
            // Convert OMM units to internal units
            // Mean motion: rev/day -> rad/s
            double meanMotionRadPerSec = meanMotion * Constants._2PI / 86400.0;

            // Angles: degrees -> radians
            return new KeplerianElements(
                meanMotionRadPerSec,
                eccentricity,
                inclination * Constants.Deg2Rad,
                raan * Constants.Deg2Rad,
                argumentOfPeriapsis * Constants.Deg2Rad,
                meanAnomaly * Constants.Deg2Rad,
                observer,
                epoch,
                frame,
                OrbitalElementsType.Mean,
                nameof(inclination), nameof(raan), nameof(argumentOfPeriapsis), nameof(meanAnomaly));
        }

        /// <summary>
        /// An eccentricity is finite and non-negative (#476): NaN took no conic branch, +Infinity gave a state of NaN.
        /// −0.0 is accepted, as 0.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The eccentricity is not finite or is negative.</exception>
        private static void RequireEccentricity(double eccentricity)
        {
            if (!double.IsFinite(eccentricity) || eccentricity < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(eccentricity), eccentricity,
                    "The eccentricity must be finite and non-negative.");
            }
        }

        /// <exception cref="ArgumentException">The inclination is not finite or is outside [−π, π].</exception>
        private static void RequireInclination(double inclination, string name)
        {
            RequireFinite(inclination, name);
            if (inclination is < -Constants.PI or > Constants.PI)
            {
                throw new ArgumentException("Inclination must be in range [-PI,PI] ", name);
            }
        }

        /// <exception cref="ArgumentException">The value is not finite (#726).</exception>
        private static void RequireFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentException($"The element {name} must be finite: {value:R}.", name);
            }
        }

        /// <summary>
        /// Converts the current instance to Keplerian elements.
        /// </summary>
        /// <returns>The current instance as Keplerian elements.</returns>
        public override KeplerianElements ToKeplerianElements()
        {
            return this;
        }

        /// <summary>
        /// Gets the semi-major axis of the orbit.
        /// </summary>
        /// <returns>The semi-major axis.</returns>
        public override double SemiMajorAxis()
        {
            return A;
        }

        /// <summary>
        /// Gets the eccentricity of the orbit.
        /// </summary>
        /// <returns>The eccentricity.</returns>
        public override double Eccentricity()
        {
            return E;
        }

        /// <summary>
        /// Gets the inclination of the orbit.
        /// </summary>
        /// <returns>The inclination.</returns>
        public override double Inclination()
        {
            return I;
        }

        /// <summary>
        /// Gets the right ascension of the ascending node.
        /// </summary>
        /// <returns>The right ascension of the ascending node.</returns>
        public override double AscendingNode()
        {
            return RAAN;
        }

        /// <summary>
        /// Gets the argument of periapsis.
        /// </summary>
        /// <returns>The argument of periapsis.</returns>
        public override double ArgumentOfPeriapsis()
        {
            return AOP;
        }

        /// <summary>
        /// Gets the mean anomaly.
        /// </summary>
        /// <returns>The mean anomaly.</returns>
        public override double MeanAnomaly()
        {
            return M;
        }
        
        
        
        #region Operator

        public bool Equals(KeplerianElements other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return A.Equals(other.A) && E.Equals(other.E) && I.Equals(other.I) && RAAN.Equals(other.RAAN) && AOP.Equals(other.AOP) && M.Equals(other.M) &&
                   Observer.NaifId == other.Observer.NaifId && Epoch == other.Epoch && Frame == other.Frame;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((KeplerianElements)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), A, E, I, RAAN, AOP, M);
        }

        public static bool operator ==(KeplerianElements left, KeplerianElements right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(KeplerianElements left, KeplerianElements right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return $"Epoch : {Epoch.ToString()} A : {A}, Ecc. : {E}, Inc. : {I}, AN : {RAAN}, AOP : {AOP}, M : {M}, Frame : {Frame.Name}";
        }
        #endregion
    }
}