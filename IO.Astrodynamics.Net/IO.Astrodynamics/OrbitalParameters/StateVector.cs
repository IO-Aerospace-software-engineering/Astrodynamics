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
        /// <see cref="ToFrame"/> transforms the covariance with the Jacobian of the state
        /// transformation, including the angular velocity term of a rotating frame (ITRF93, TIRS, body-fixed frames).
        /// <see cref="Matrix.TransformCovariance"/> is a pure rotation, valid only between inertial frames.
        /// <see cref="RotateCovarianceToRtn"/> is a pure rotation into RTN, the convention of the conjunction
        /// analysis and of the CDM export.
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
            if (!_inclination.HasValue)
            {
                // atan2 of the two components of h, well conditioned near 0 and π, where acos of h_z / |h| is not
                var h = SpecificAngularMomentum();
                _inclination = System.Math.Atan2(System.Math.Sqrt(h.X * h.X + h.Y * h.Y), h.Z);
            }

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

            var omega = System.Math.Atan2(n.Y, n.X);
            if (omega < 0.0)
            {
                omega += Constants._2PI;
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

            _periapsisArgument = AngleInOrbitPlane(n, e);

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

            _trueAnomaly = AngleInOrbitPlane(EccentricityVector(), sv.Position);

            // On an open orbit the true anomaly lies within ±acos(-1/e), so it is given in (-π, π]: negative before the
            // periapsis
            if (!IsElliptical() && _trueAnomaly > System.Math.PI)
            {
                _trueAnomaly -= Constants._2PI;
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
            var v = AngleInOrbitPlane(omega, sv.Position);

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
            var l = AngleInOrbitPlane(Vector3.VectorX, sv.Position);

            l = l - ArgumentOfPeriapsis() - AscendingNode();
            if (l < 0.0)
            {
                l += Constants._2PI;
            }

            return l % Constants._2PI;
        }

        /// <summary>
        /// The angle from <paramref name="from"/> to <paramref name="to"/>, in [0, 2π), counted positive about the
        /// angular momentum: atan2(ĥ · (from × to), from · to).
        /// </summary>
        /// <remarks>
        /// Well conditioned at every angle, where the acos of the normalized dot product loses about half the digits
        /// near 0 and π: near an apsis, a node or the reference direction (#368).
        /// </remarks>
        private double AngleInOrbitPlane(in Vector3 from, in Vector3 to)
        {
            var hHat = SpecificAngularMomentum().Normalize();
            double angle = System.Math.Atan2(hHat * from.Cross(to), from * to);
            return angle < 0.0 ? angle + Constants._2PI : angle;
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


        /// <summary>
        /// Computes the 3x3 rotation matrix from the inertial frame to the RTN (Radial-Transverse-Normal) frame.
        /// R = r_hat, T = (h × r) normalized, N = (r × v) normalized.
        /// </summary>
        /// <returns>A 3x3 rotation matrix whose rows are the R, T, N unit vectors in inertial coordinates.</returns>
        public Matrix CreateRtnRotation()
        {
            var r = Position.Normalize();
            var h = Position.Cross(Velocity).Normalize();
            var t = h.Cross(r).Normalize();
            return Matrix.FromRowVectors(r, t, h);
        }

        /// <summary>
        /// Rotates a vector from the inertial frame to the RTN frame defined by this state.
        /// </summary>
        /// <param name="inertialVector">A vector expressed in the inertial frame.</param>
        /// <returns>The vector expressed in RTN coordinates.</returns>
        public Vector3 ToRtn(Vector3 inertialVector)
        {
            var rtn = CreateRtnRotation();
            var rotated = rtn * new[] { inertialVector.X, inertialVector.Y, inertialVector.Z };
            return new Vector3(rotated[0], rotated[1], rotated[2]);
        }

        /// <summary>
        /// Rotates a vector from the RTN frame to the inertial frame defined by this state.
        /// </summary>
        /// <param name="rtnVector">A vector expressed in RTN coordinates.</param>
        /// <returns>The vector expressed in inertial coordinates.</returns>
        public Vector3 FromRtn(Vector3 rtnVector)
        {
            var rtn = CreateRtnRotation();
            var rotated = rtn.Transpose() * new[] { rtnVector.X, rtnVector.Y, rtnVector.Z };
            return new Vector3(rotated[0], rotated[1], rotated[2]);
        }

        /// <summary>
        /// Rotates a 6x6 position-velocity covariance matrix from the inertial frame to RTN.
        /// Uses the block-diagonal rotation: C_RTN = R_6x6 * C_ICRF * R_6x6^T
        /// where R_6x6 = diag(R_3x3, R_3x3).
        /// </summary>
        /// <param name="covarianceIcrf">A 6x6 covariance matrix in the inertial frame (units: m², m²/s, m²/s²).</param>
        /// <returns>The 6x6 covariance matrix in RTN coordinates.</returns>
        /// <remarks>
        /// This is a pure rotation: the rotation rate of the RTN frame is deliberately not applied to the velocity
        /// block. The velocity covariance is the inertial one, expressed along the R, T and N axes. This is the
        /// convention used by the conjunction analysis and by the CDM export, whose covariance is given in RTN at
        /// TCA. It differs from <see cref="ToFrame"/>, which applies the full Jacobian of a
        /// rotating frame.
        /// </remarks>
        public Matrix RotateCovarianceToRtn(Matrix covarianceIcrf)
        {
            var rotation = CreateRtnRotation();
            var rot6X6 = Matrix.CreateBlockDiagonal(rotation, rotation);
            return rot6X6 * covarianceIcrf * rot6X6.Transpose();
        }

        /// <summary>
        /// Rotates a 6x6 position-velocity covariance matrix from the RTN frame defined by this state to the inertial frame.
        /// Inverse of <see cref="RotateCovarianceToRtn"/>: C_ICRF = R_6x6^T * C_RTN * R_6x6.
        /// </summary>
        /// <param name="covarianceRtn">A 6x6 covariance matrix in RTN coordinates (units: m², m²/s, m²/s²).</param>
        /// <returns>The 6x6 covariance matrix in the inertial frame of this state.</returns>
        public Matrix RotateCovarianceFromRtn(Matrix covarianceRtn)
        {
            var rotation = CreateRtnRotation();
            var rot6X6 = Matrix.CreateBlockDiagonal(rotation, rotation);
            return rot6X6.Transpose() * covarianceRtn * rot6X6;
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