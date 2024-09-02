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
        /// <exception cref="ArgumentException"></exception>
        public KeplerianElements(double semiMajorAxis, double eccentricity, double inclination, double rigthAscendingNode, double argumentOfPeriapsis, double meanAnomaly,
            ILocalizable observer, Time epoch, Frame frame, double? trueAnomaly = null, TimeSpan? period = null) : base(observer, epoch, frame)
        {
            if (semiMajorAxis <= 0.0)
            {
                throw new ArgumentException("Semi major axis must be a positive number");
            }

            if (eccentricity < 0.0)
            {
                throw new ArgumentException("Eccentricity must be a positive number");
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
        }

        public override KeplerianElements ToKeplerianElements()
        {
            return this;
        }

        /// <summary>
        /// Get the state vector
        /// </summary>
        /// <returns></returns>
        public override StateVector ToStateVector()
        {
            if (_stateVector is null)
            {
                // local variables
                double a = A;
                double ecc = E;
                double inc = I;
                double lnode = RAAN;
                double argp = AOP;
                double m0 = M;
                double gm = Observer.GM;

                // Check for valid input
                if (ecc < 0)
                {
                    throw new ArgumentException("Eccentricity must be positive. Provided value: " + ecc);
                }

                if (gm <= 0)
                {
                    throw new ArgumentException("Gravitational parameter (mu) must be positive. Provided value: " + gm);
                }

                // Compute perigee distance
                double rp = a * (1.0 - ecc);

                // Compute position and velocity in perifocal frame
                double cosi = System.Math.Cos(inc);
                double sini = System.Math.Sin(inc);
                double cosn = System.Math.Cos(lnode);
                double sinn = System.Math.Sin(lnode);
                double cosw = System.Math.Cos(argp);
                double sinw = System.Math.Sin(argp);
                double snci = sinn * cosi;
                double cnci = cosn * cosi;

                double[] basisp = new double[3];
                double[] basisq = new double[3];

                basisp[0] = cosn * cosw - snci * sinw;
                basisp[1] = sinn * cosw + cnci * sinw;
                basisp[2] = sini * sinw;

                basisq[0] = -cosn * sinw - snci * cosw;
                basisq[1] = -sinn * sinw + cnci * cosw;
                basisq[2] = sini * cosw;

                // perigee state
                double v = System.Math.Sqrt(gm * (ecc + 1.0) / rp);

                double[] state = new double[6];
                // multiply base vectors
                for (int i = 0; i < 3; i++)
                {
                    state[i] = rp * basisp[i];
                    state[i + 3] = v * basisq[i];
                }

                var sv = new StateVector(new Vector3(state[0], state[1], state[2]), new Vector3(state[3], state[4], state[5]), Observer, Epoch, Frame);

                double ainvrs, n, period, d__1, dt;
                if (ecc < 1)
                {

                    ainvrs = (1 - ecc) / rp;
                    n = System.Math.Sqrt(gm * ainvrs) * ainvrs;
                    period = Constants._2PI / n;



                    d__1 = m0 / n;
                    dt = d__1 % period;

                }
                else if (ecc > 1)
                {


                    ainvrs = (ecc - 1) / rp;
                    n = System.Math.Sqrt(gm * ainvrs) * ainvrs;
                    dt = m0 / n;

                }
                else
                {
                    n = System.Math.Sqrt(gm / (rp * 2)) / rp;
                    dt = m0 / n;
                }

                _stateVector = sv.AtEpoch(Epoch.AddSeconds(dt)).ToStateVector();
                return _stateVector;
            }

            return _stateVector;
        }

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
    }
}