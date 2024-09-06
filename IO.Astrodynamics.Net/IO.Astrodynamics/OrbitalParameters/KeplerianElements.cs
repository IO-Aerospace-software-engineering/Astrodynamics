using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;


namespace IO.Astrodynamics.OrbitalParameters
{
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
        /// <exception cref="ArgumentException"></exception>
        public KeplerianElements(double semiMajorAxis, double eccentricity, double inclination, double rigthAscendingNode, double argumentOfPeriapsis, double meanAnomaly,
            ILocalizable observer, Time epoch, Frame frame, double? trueAnomaly = null, TimeSpan? period = null, double? perigeeRadius = null) : base(observer, epoch, frame)
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

            if (rigthAscendingNode is < 0.0 or > Constants._2PI)
            {
                throw new ArgumentException("Rigth ascending node must be in range [0.0,2*PI] ");
            }

            if (argumentOfPeriapsis is < 0.0 or > Constants._2PI)
            {
                throw new ArgumentException("Argument of periapsis must be in range [0.0,2*PI] ");
            }

            if (meanAnomaly is < 0.0 or > Constants._2PI)
            {
                throw new ArgumentException("Mean anomaly must be in range [0.0,2*PI] ");
            }

            A = semiMajorAxis;
            E = eccentricity;
            I = inclination;
            RAAN = rigthAscendingNode;
            AOP = argumentOfPeriapsis;
            M = meanAnomaly;
            _period = period;
            _trueAnomaly = trueAnomaly;
            _perigeeRadius = perigeeRadius;
        }

        public override KeplerianElements ToKeplerianElements()
        {
            return this;
        }

        public override double SemiMajorAxis()
        {
            return A;
        }

        public override double Eccentricity()
        {
            return E;
        }

        public override double Inclination()
        {
            return I;
        }

        public override double AscendingNode()
        {
            return RAAN;
        }

        public override double ArgumentOfPeriapsis()
        {
            return AOP;
        }

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
            return $"Epoch : {Epoch.ToString()} A : {A} Ecc. : {E} Inc. : {I} AN : {RAAN} AOP : {AOP} M : {M} Frame : {Frame.Name}";
        }
        #endregion
    }
}