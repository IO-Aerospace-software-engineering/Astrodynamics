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
        /// Constructor
        /// </summary>
        /// <param name="semiMajorAxis"></param>
        /// <param name="eccentricity"></param>
        /// <param name="inclination"></param>
        /// <param name="rigthAscendingNode"></param>
        /// <param name="argumentOfPeriapsis"></param>
        /// <param name="meanAnomaly"></param>
        /// <param name="observer"></param>
        /// <param name="epoch"></param>
        /// <param name="frame"></param>
        /// <param name="trueAnomaly"></param>
        /// <param name="period"></param>
        /// <param name="perigeeRadius"></param>
        /// <param name="elementsType">Type of orbital elements (osculating or mean). Defaults to osculating.</param>
        /// <exception cref="ArgumentException"></exception>
        public KeplerianElements(double semiMajorAxis, double eccentricity, double inclination, double rigthAscendingNode, double argumentOfPeriapsis, double meanAnomaly,
            ILocalizable observer, Time epoch, Frame frame, double? trueAnomaly = null, TimeSpan? period = null, double? perigeeRadius = null,
            OrbitalElementsType elementsType = OrbitalElementsType.Osculating) : base(observer, epoch, frame, elementsType)
        {
            if (eccentricity < 0.0)
            {
                throw new ArgumentException("Eccentricity must be a positive number");
            }

            if (System.Math.Abs(eccentricity - 1.0) < 1E-06 && (perigeeRadius is null || !double.IsPositiveInfinity(semiMajorAxis)))
            {
                throw new ArgumentException("To build a parabolic orbit you must set perigee radius and define semi major axis as positive infinity");
            }

            if (inclination is < -Constants.PI or > Constants.PI)
            {
                throw new ArgumentException("Inclination must be in range [-PI,PI] ");
            }

            A = semiMajorAxis;
            E = eccentricity;
            I = inclination;
            RAAN = SpecialFunctions.NormalizeAngle(rigthAscendingNode);
            AOP = SpecialFunctions.NormalizeAngle(argumentOfPeriapsis);
            M = SpecialFunctions.NormalizeAngle(meanAnomaly);
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
        private KeplerianElements(double meanMotion, double eccentricity, double inclination, double rightAscendingNode,
            double argumentOfPeriapsis, double meanAnomaly, ILocalizable observer, Time epoch, Frame frame,
            OrbitalElementsType elementsType)
            : base(observer, epoch, frame, elementsType)
        {
            if (eccentricity < 0.0)
            {
                throw new ArgumentException("Eccentricity must be a positive number");
            }

            if (inclination is < -Constants.PI or > Constants.PI)
            {
                throw new ArgumentException("Inclination must be in range [-PI,PI] ");
            }

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
        /// <exception cref="ArgumentException">Thrown when eccentricity is negative or inclination is out of range</exception>
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
                OrbitalElementsType.Mean);
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