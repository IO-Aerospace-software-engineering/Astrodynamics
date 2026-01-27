using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.OrbitalParameters
{
    /// <summary>
    /// Represents a state vector in orbital mechanics, including position and velocity.
    /// </summary>
    /// <remarks>
    /// This class extends `OrbitalParameters` and implements `IEquatable&#60;StateVector&#62;`.
    /// </remarks>
    public class StateVector : OrbitalParameters, IEquatable<StateVector>
    {
        private StateVector _inverse;

        protected override void ResetCache()
        {
            base.ResetCache();
            _inverse = null;
        }

        /// <summary>
        /// Gets the position vector.
        /// </summary>
        public Vector3 Position { get; private set; }

        /// <summary>
        /// Gets the velocity vector.
        /// </summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>
        /// Gets the 6x6 covariance matrix representing uncertainty in position and velocity.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Optional. When set, represents the 6x6 position-velocity covariance matrix.
        /// Units should be consistent with StateVector: m² for position, m²/s for cross-terms, m²/s² for velocity.
        /// </para>
        /// <para>
        /// The matrix layout is:
        /// <code>
        ///     X       Y       Z       X_DOT   Y_DOT   Z_DOT
        /// X   σ²x
        /// Y   σxy     σ²y
        /// Z   σxz     σyz     σ²z
        /// X'  σxẋ     σyẋ     σzẋ     σ²ẋ
        /// Y'  σxẏ     σyẏ     σzẏ     σẋẏ     σ²ẏ
        /// Z'  σxż     σyż     σzż     σẋż     σẏż     σ²ż
        /// </code>
        /// </para>
        /// <para>
        /// To transform covariance between frames, use <see cref="Matrix.TransformCovariance"/>.
        /// </para>
        /// </remarks>
        public Matrix? Covariance { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="position">The position vector in meters.</param>
        /// <param name="velocity">The velocity vector in meters per second.</param>
        /// <param name="observer">The observer (central body).</param>
        /// <param name="epoch">The epoch time.</param>
        /// <param name="frame">The reference frame.</param>
        /// <param name="covariance">Optional 6x6 covariance matrix representing state uncertainty (m², m²/s, m²/s²).</param>
        public StateVector(in Vector3 position, in Vector3 velocity, ILocalizable observer, in Time epoch, Frame frame,
            Matrix? covariance = null) : base(observer, epoch, frame)
        {
            Position = position;
            Velocity = velocity;
            Covariance = covariance;
        }

        public override StateVector ToStateVector()
        {
            return this;
        }

        /// <summary>
        /// Calculates the specific orbital energy.
        /// </summary>
        /// <returns></returns>
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

            _semiMajorAxis ??= -(Observer.GM / (2.0 * SpecificOrbitalEnergy()));

            return _semiMajorAxis.Value;
        }

        /// <summary>
        /// Calculates the eccentricity vector.
        /// </summary>
        /// <returns></returns>
        public override double Eccentricity()
        {
            _eccentricity ??= EccentricityVector().Magnitude();
            return _eccentricity.Value;
        }

        /// <summary>
        /// Calculates the eccentricity vector.
        /// </summary>
        /// <returns></returns>
        public override double Inclination()
        {
            _inclination ??= SpecificAngularMomentum().Angle(Vector3.VectorZ);
            return _inclination.Value;
        }

        /// <summary>
        /// Calculates the right ascension of the ascending node.
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Calculates the argument of periapsis.
        /// </summary>
        /// <returns></returns>
        public override double ArgumentOfPeriapsis()
        {
            if (_periapsisArgument.HasValue)
            {
                return _periapsisArgument.Value;
            }

            var n = AscendingNodeVector();
            var e = EccentricityVector();

            if (e == Vector3.Zero)
            {
                return 0.0;
            }

            if (n == Vector3.Zero)
            {
                _periapsisArgument = System.Math.Atan2(e.Y, e.X);
                if (Inclination() > Constants.PI2)
                {
                    _periapsisArgument = Constants._2PI - _periapsisArgument;
                    return _periapsisArgument!.Value;
                }
            }

            _periapsisArgument = System.Math.Acos(System.Math.Clamp((n * e) / (n.Magnitude() * e.Magnitude()), -1.0, 1.0));
            if (e.Z < 0.0)
            {
                _periapsisArgument = System.Math.PI * 2.0 - _periapsisArgument;
            }

            return _periapsisArgument.Value;
        }

        /// <summary>
        /// Calculates the mean anomaly.
        /// </summary>
        /// <returns></returns>
        public override double MeanAnomaly()
        {
            double ea = EccentricAnomaly(TrueAnomaly());

            if (IsElliptical())
            {
                _meanAnomaly ??= ea - Eccentricity() * System.Math.Sin(ea);
            }
            else if (IsParabolic())
            {
                _meanAnomaly ??= ea + System.Math.Pow(ea, 3.0) / 3.0;
            }
            else if (IsHyperbolic())
            {
                _meanAnomaly ??= Eccentricity() * System.Math.Sinh(ea) - ea;
            }

            return _meanAnomaly.Value;
        }

        /// <summary>
        /// Calculates the true anomaly.
        /// </summary>
        /// <returns></returns>
        public override double TrueAnomaly()
        {
            if (_trueAnomaly.HasValue)
            {
                return _trueAnomaly.Value;
            }

            var sv = ToStateVector();

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
            var x = (e * sv.Position) / (e.Magnitude() * sv.Position.Magnitude());
            if (x > 1.0) x = 1.0;
            if (x < -1.0) x = -1.0;
            _trueAnomaly = System.Math.Acos(x);
            if (sv.Position * sv.Velocity < 0.0)
            {
                _trueAnomaly = System.Math.PI * 2.0 - _trueAnomaly;
            }

            return _trueAnomaly.Value;
        }

        /// <summary>
        /// Calculates the true anomaly for a circular orbit.
        /// </summary>
        /// <returns>The true anomaly in radians.</returns>
        private double CircularTrueAnomaly()
        {
            var sv = ToStateVector();
            var omega = AscendingNodeVector();
            var v = System.Math.Acos((omega * sv.Position) / (omega.Magnitude() * sv.Position.Magnitude()));
            if (sv.Position.Z < 0.0)
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

        /// <summary>
        /// Calculates the true anomaly for a circular orbit with no inclination.
        /// </summary>
        /// <returns>The true anomaly in radians.</returns>
        private double CircularNoInclinationTrueAnomaly()
        {
            var sv = ToStateVector();
            var l = System.Math.Acos(sv.Position.X / sv.Position.Magnitude());
            if (sv.Velocity.X > 0)
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

        #region Operators

        /// <summary>
        /// Adds two state vectors.
        /// </summary>
        /// <param name="sv1">The first state vector.</param>
        /// <param name="sv2">The second state vector.</param>
        /// <returns>The resulting state vector after addition.</returns>
        /// <exception cref="ArgumentException">Thrown when the state vectors have different frames or epochs.</exception>
        public static StateVector operator +(StateVector sv1, StateVector sv2)
        {
            if (sv1.Epoch != sv2.Epoch || sv1.Frame != sv2.Frame)
            {
                throw new ArgumentException("State vector must have the same frame and the same epoch");
            }

            return new StateVector(sv1.Position + sv2.Position, sv1.Velocity + sv2.Velocity, sv2.Observer, sv1.Epoch, sv2.Frame);
        }

        /// <summary>
        /// Updates the position vector.
        /// </summary>
        /// <param name="position">The new position vector.</param>
        internal void UpdatePosition(in Vector3 position)
        {
            ResetCache();
            Position = position;
        }

        /// <summary>
        /// Updates the velocity vector.
        /// </summary>
        /// <param name="velocity">The new velocity vector.</param>
        internal void UpdateVelocity(in Vector3 velocity)
        {
            ResetCache();
            Velocity = velocity;
        }

        /// <summary>
        /// Converts the state vector to an array.
        /// </summary>
        /// <returns>An array containing the position and velocity components.</returns>
        public double[] ToArray()
        {
            return [Position.X, Position.Y, Position.Z, Velocity.X, Velocity.Y, Velocity.Z];
        }

        /// <summary>
        /// Gets the inverse of the state vector.
        /// </summary>
        /// <returns>The inverse state vector with preserved covariance.</returns>
        public StateVector Inverse()
        {
            if (_inverse is not null)
            {
                return _inverse;
            }

            _inverse ??= new StateVector(Position.Inverse(), Velocity.Inverse(), Observer, Epoch, Frame, Covariance);
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
            return $"Epoch : {Epoch.ToString()} Position : {Position} Velocity : {Velocity} Frame : {Frame.Name}";
        }

        #endregion
    }
}