using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;


namespace IO.Astrodynamics.OrbitalParameters
{
    public class KeplerianElements : OrbitalParameters, IEquatable<KeplerianElements>
    {
        private double? _eccentricAnomaly;
        private double? _trueAnomaly;


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
        /// <exception cref="ArgumentException"></exception>
        public KeplerianElements(double semiMajorAxis, double eccentricity, double inclination, double rigthAscendingNode, double argumentOfPeriapsis, double meanAnomaly,
            ILocalizable observer, Time epoch, Frame frame) : base(observer, epoch, frame)
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
        }

        public double A { get; }
        public double E { get; }
        public double I { get; }
        public double RAAN { get; }
        public double AOP { get; }
        public double M { get; }

        public override double ArgumentOfPeriapsis()
        {
            return AOP;
        }

        public override double AscendingNode()
        {
            return RAAN;
        }

        public override double EccentricAnomaly()
        {
            if (_eccentricAnomaly.HasValue)
            {
                return _eccentricAnomaly.Value;
            }

            double tmpEA = M;
            _eccentricAnomaly = 0.0;

            while (System.Math.Abs(tmpEA - _eccentricAnomaly.Value) > 1E-09)
            {
                _eccentricAnomaly = tmpEA;
                tmpEA = M + E * System.Math.Sin(_eccentricAnomaly.Value);
            }

            return _eccentricAnomaly.Value;
        }

        public override double Eccentricity()
        {
            return E;
        }

        public override double Inclination()
        {
            return I;
        }

        public override double SemiMajorAxis()
        {
            return A;
        }

        public override double TrueAnomaly()
        {
            if (_trueAnomaly.HasValue)
            {
                return _trueAnomaly.Value;
            }
            double EA = EccentricAnomaly();
            _trueAnomaly = System.Math.Atan2(System.Math.Sqrt(1 - E * E) * System.Math.Sin(EA), System.Math.Cos(EA) - E);
            if (_trueAnomaly < 0.0)
            {
                _trueAnomaly += Constants._2PI;
            }

            _trueAnomaly %= Constants._2PI;
            return _trueAnomaly.Value;
        }

        public override double MeanAnomaly()
        {
            return M;
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
            // Déclaration des variables
            double a = A;// Demi-grand axe
            double ecc = E;
            double inc = I;
            double lnode = RAAN;
            double argp = AOP;
            double m0 = M;
            double mu = Observer.GM;

            // Gestion des exceptions
            if (ecc < 0)
            {
                throw new ArgumentException("Eccentricity must be positive. Provided value: " + ecc);
            }

            if (mu <= 0)
            {
                throw new ArgumentException("Gravitational parameter (mu) must be positive. Provided value: " + mu);
            }

            // Calcul du rayon au périgée
            double rp = a * (1.0 - ecc);

            // Calcul des vecteurs de base orthonormés
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

            // Calcul de l'état au périgée
            double v = System.Math.Sqrt(mu * (ecc + 1.0) / rp);

            double[] state = new double[6];
            // Multiplier les vecteurs de base par les valeurs correspondantes
            for (int i = 0; i < 3; i++)
            {
                state[i] = rp * basisp[i]; // Position
                state[i + 3] = v * basisq[i]; // Vitesse
            }

            var sv = new StateVector(new Vector3(state[0], state[1], state[2]), new Vector3(state[3], state[4], state[5]), Observer, Epoch, Frame);

            double ainvrs, n, period, d__1, dt;
            if (ecc < 1)
            {
/*        Recall that: */

/*        N ( mean motion ) is given by DSQRT( MU / A**3 ). */
/*        But since, A = RP / ( 1 - ECC ) ... */

                ainvrs = (1 - ecc) / rp;
                n = System.Math.Sqrt(mu * ainvrs) * ainvrs;
                period = Constants._2PI / n;

/*        In general the mean anomaly is given by */

/*           M  = (T - TP) * N */

/*        Where TP is the time of periapse passage.  M0 is the mean */
/*        anomaly at time T0 so that */
/*        Thus */

/*           M0 = ( T0 - TP ) * N */

/*        So TP = T0-M0/N hence the time since periapse at time ET */
/*        is given by ET - T0 + M0/N.  Finally, since elliptic orbits are */
/*        periodic, we can mod this value by the period of the orbit. */

                d__1 = m0 / n;
                dt = d__1 % period;

/*     Hyperbolas next. */
            }
            else if (ecc > 1)
            {
/*        Again, recall that: */

/*        N ( mean motion ) is given by DSQRT( MU / |A**3| ). */
/*        But since, |A| = RP / ( ECC - 1 ) ... */

                ainvrs = (ecc - 1) / rp;
                n = System.Math.Sqrt(mu * ainvrs) * ainvrs;
                dt = m0 / n;

/*     Finally, parabolas. */
            }
            else
            {
                n = System.Math.Sqrt(mu / (rp * 2)) / rp;
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
            return $"Epoch : {Epoch.ToString()} A : {A} Ecc. : {E} Inc. : {I} AN : {RAAN} AOP : {AOP} M : {M} Frame : {Frame.Name}" ;
        }
    }
}