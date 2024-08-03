using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.OrbitalParameters
{
    public class StateVector : OrbitalParameters, IEquatable<StateVector>
    {
        private double? _eccentricity;
        private double? _inclination;
        private double? _semiMajorAxis;
        private double? _ascendingNode;
        private double? _argumentOfPeriapsis;
        private double? _trueAnomaly;
        private double? _eccentricAnomaly;
        private double? _meanAnomaly;
        private StateVector _inverse;

        protected override void ResetCache()
        {
            base.ResetCache();
            _eccentricity = _inclination = _semiMajorAxis = _ascendingNode = _argumentOfPeriapsis = _trueAnomaly = _eccentricAnomaly = _meanAnomaly = null;
            _inverse = null;
        }

        public Vector3 Position { get; private set; }

        public Vector3 Velocity { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="position"></param>
        /// <param name="velocity"></param>
        /// <param name="observer"></param>
        /// <param name="epoch"></param>
        /// <param name="frame"></param>
        public StateVector(in Vector3 position, in Vector3 velocity, ILocalizable observer, in DateTime epoch, Frame frame) : base(observer, epoch, frame)
        {
            Position = position;
            Velocity = velocity;
        }

        public override Vector3 SpecificAngularMomentum()
        {
            _specificAngularMomentum ??= Position.Cross(Velocity);
            return _specificAngularMomentum.Value;
        }

        public override double Eccentricity()
        {
            _eccentricity ??= EccentricityVector().Magnitude();
            return _eccentricity.Value;
        }

        public override Vector3 EccentricityVector()
        {
            _eccentricVector ??= (Velocity.Cross(SpecificAngularMomentum()) / Observer.GM) - Position.Normalize();
            return _eccentricVector.Value;
        }

        public override double Inclination()
        {
            _inclination ??= SpecificAngularMomentum().Angle(Vector3.VectorZ);
            return _inclination.Value;
        }


        public override double SpecificOrbitalEnergy()
        {
            _specificOrbitalEnergy ??= System.Math.Pow(Velocity.Magnitude(), 2.0) / 2.0 - (Observer.GM / Position.Magnitude());
            return _specificOrbitalEnergy.Value;
        }

        public override double SemiMajorAxis()
        {
            _semiMajorAxis ??= -(Observer.GM / (2.0 * SpecificOrbitalEnergy()));
            return _semiMajorAxis.Value;
        }

        public override Vector3 AscendingNodeVector()
        {
            if (_ascendingNodeVector.HasValue)
            {
                return _ascendingNodeVector.Value;
            }

            if (Inclination() == 0.0)
            {
                return Vector3.VectorX;
            }

            var h = SpecificAngularMomentum();
            _ascendingNodeVector = new Vector3(-h.Y, h.X, 0.0);

            return _ascendingNodeVector.Value;
        }

        public override double AscendingNode()
        {
            if (_ascendingNode.HasValue)
            {
                return _ascendingNode.Value;
            }

            Vector3 n = AscendingNodeVector();

            if (n.Magnitude() == 0.0)
            {
                _ascendingNode = 0.0;
                return _ascendingNode.Value;
            }

            var omega = System.Math.Acos(n.X / n.Magnitude());


            if (n.Y < 0.0)
            {
                omega = 2 * System.Math.PI - omega;
            }

            _ascendingNode = omega;


            return _ascendingNode.Value;
        }

        public override double ArgumentOfPeriapsis()
        {
            if (_argumentOfPeriapsis.HasValue)
            {
                return _argumentOfPeriapsis.Value;
            }

            var n = AscendingNodeVector();
            var e = EccentricityVector();

            if (e == Vector3.Zero)
            {
                return 0.0;
            }

            if (n == Vector3.Zero)
            {
                _argumentOfPeriapsis = System.Math.Atan2(e.Y, e.X);
                if (Inclination() > Constants.PI2)
                {
                    _argumentOfPeriapsis = Constants._2PI - _argumentOfPeriapsis;
                    return _argumentOfPeriapsis!.Value;
                }
            }

            _argumentOfPeriapsis = System.Math.Acos(System.Math.Clamp((n * e) / (n.Magnitude() * e.Magnitude()), -1.0, 1.0));
            if (e.Z < 0.0)
            {
                _argumentOfPeriapsis = System.Math.PI * 2.0 - _argumentOfPeriapsis;
            }

            return _argumentOfPeriapsis.Value;
        }

        public override double TrueAnomaly()
        {
            if (_trueAnomaly.HasValue)
            {
                return _trueAnomaly.Value;
            }


            if (IsCircular())
            {
                if (Inclination() < 1E-03)
                {
                    _trueAnomaly = CircularNoInclinationTrueAnomaly();
                    return _trueAnomaly.Value;
                }

                _trueAnomaly = CircularTrueAnomaly();
                return _trueAnomaly.Value;
            }

            var e = EccentricityVector();
            _trueAnomaly = System.Math.Acos((e * Position) / (e.Magnitude() * Position.Magnitude()));
            if (Position * Velocity < 0.0)
            {
                _trueAnomaly = System.Math.PI * 2.0 - _trueAnomaly;
            }

            return _trueAnomaly.Value;
        }

        private double CircularTrueAnomaly()
        {
            var omega = AscendingNodeVector();
            var v = System.Math.Acos((omega * Position) / (omega.Magnitude() * Position.Magnitude()));
            if (Position.Z < 0.0)
            {
                v = Constants._2PI - v;
            }

            v -= ArgumentOfPeriapsis();
            if (v < 0.0)
            {
                v += Constants._2PI;
            }

            return v % Constants._2PI;
        }

        private double CircularNoInclinationTrueAnomaly()
        {
            var l = System.Math.Acos(Position.X / Position.Magnitude());
            if (Velocity.X > 0)
            {
                l = Constants._2PI - l;
            }

            l = l - ArgumentOfPeriapsis() - AscendingNode();
            if (l < 0.0)
            {
                l += Constants._2PI;
            }

            return l % Constants._2PI;
        }

        public override double EccentricAnomaly()
        {
            if (_eccentricAnomaly.HasValue)
            {
                return _eccentricAnomaly.Value;
            }

            double v = TrueAnomaly();
            double e = Eccentricity();
            _eccentricAnomaly ??= 2 * System.Math.Atan((System.Math.Tan(v / 2.0)) / System.Math.Sqrt((1 + e) / (1 - e)));
            return _eccentricAnomaly.Value;
        }

        public override StateVector ToStateVector()
        {
            return this;
        }

        public override double MeanAnomaly()
        {
            if (_meanAnomaly.HasValue)
            {
                return _meanAnomaly.Value;
            }

            _meanAnomaly ??= EccentricAnomaly() - Eccentricity() * System.Math.Sin(EccentricAnomaly());
            return _meanAnomaly.Value;
        }

        public static StateVector operator +(StateVector sv1, StateVector sv2)
        {
            if (sv1.Epoch != sv2.Epoch || sv1.Frame != sv2.Frame)
            {
                throw new ArgumentException("State vector must have the same frame and the same epoch");
            }

            return new StateVector(sv1.Position + sv2.Position, sv1.Velocity + sv2.Velocity, sv2.Observer, sv1.Epoch, sv2.Frame);
        }

        public static StateVector operator -(StateVector sv1, StateVector sv2)
        {
            if (sv1.Epoch != sv2.Epoch || sv1.Frame != sv2.Frame)
            {
                throw new ArgumentException("State vector must have the same frame and the same epoch");
            }

            return new StateVector(sv1.Position - sv2.Position, sv1.Velocity - sv2.Velocity, sv2.Observer, sv1.Epoch, sv2.Frame);
        }

        internal void UpdatePosition(in Vector3 position)
        {
            ResetCache();
            Position = position;
        }

        internal void UpdateVelocity(in Vector3 velocity)
        {
            ResetCache();
            Velocity = velocity;
        }


        public double[] ToArray()
        {
            return [Position.X, Position.Y, Position.Z, Velocity.X, Velocity.Y, Velocity.Z];
        }

        public StateVector Inverse()
        {
            if (_inverse is not null)
            {
                return _inverse;
            }

            _inverse ??= new StateVector(Position.Inverse(), Velocity.Inverse(), Observer, Epoch, Frame);
            return _inverse;
        }


        public bool Equals(StateVector other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Position.Equals(other.Position) && Velocity.Equals(other.Velocity) && Observer.NaifId == other.Observer.NaifId && Epoch == other.Epoch && Frame == other.Frame;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((StateVector)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), Position, Velocity);
        }

        public static bool operator ==(StateVector left, StateVector right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(StateVector left, StateVector right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return $"Epoch : {Epoch.ToFormattedString()} Position : {Position} Velocity : {Velocity} Frame : {Frame.Name}";
        }
    }
}