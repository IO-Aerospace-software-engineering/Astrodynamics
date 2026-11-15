using System;

namespace IO.Astrodynamics.Math
{
    public readonly record struct Vector3
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public static Vector3 VectorX { get; } = new Vector3(1.0, 0.0, 0.0);
        public static Vector3 VectorY { get; } = new Vector3(0.0, 1.0, 0.0);
        public static Vector3 VectorZ { get; } = new Vector3(0.0, 0.0, 1.0);
        public static Vector3 Zero { get; } = new Vector3(0.0, 0.0, 0.0);

        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double Magnitude()
        {
            return System.Math.Sqrt(MagnitudeSquared());
        }
        
        public double MagnitudeSquared()
        {
            return X * X + Y * Y + Z * Z;
        }

        /// <summary>
        /// The unit vector of this vector.
        /// </summary>
        /// <exception cref="InvalidOperationException">The magnitude is 0 (#680; the vector itself was returned before
        /// 11.0.0): a zero vector, or one whose components are all too small for their squares to be represented (below about
        /// 1.5e-162), since the magnitude is not scaled. A vector of magnitude 0 has no direction.</exception>
        public Vector3 Normalize()
        {
            var m = Magnitude();
            if (m == 0)
            {
                throw new InvalidOperationException($"The vector {this} has a magnitude of 0 and no direction.");
            }

            return this / m;
        }

        /// <summary>
        /// The unit vector of this vector, or this vector when its magnitude is 0: the library's own normalizations, whose
        /// degenerate cases rely on the zero vector (a zero angular velocity rotates nothing, a coincident target is at
        /// angle 0, the transfer plane of collinear positions has no z component), bit for bit the former
        /// <see cref="Normalize"/> (#680).
        /// </summary>
        internal Vector3 NormalizeOrZero()
        {
            var m = Magnitude();
            if (m == 0)
            {
                return this;
            }

            return this / m;
        }

        public Vector3 Cross(in Vector3 vector)
        {
            return new Vector3((Y * vector.Z) - (Z * vector.Y), (Z * vector.X) - (X * vector.Z), (X * vector.Y) - (Y * vector.X));
        }

        public double Angle(in Vector3 vector)
        {
            var mag1 = Magnitude();
            var mag2 = vector.Magnitude();
            
            // Handle zero vectors
            if (mag1 == 0.0 || mag2 == 0.0)
            {
                return 0.0; // Angle is undefined, return 0 by convention
            }
            
            var dotProduct = (this * vector) / (mag1 * mag2);
            
            // Clamp to [-1, 1] to handle floating-point precision errors
            dotProduct = System.Math.Clamp(dotProduct, -1.0, 1.0);
            
            return System.Math.Acos(dotProduct);
        }

        /// <summary>
        /// The signed angle from this vector to <paramref name="vector"/>, positive counterclockwise about the normal of
        /// <paramref name="plane"/>, in (−π, π]: atan2((a × b) · n̂, a · b).
        /// </summary>
        /// <exception cref="ArgumentException">The normal of <paramref name="plane"/> has a magnitude of 0, or a component or
        /// a magnitude that is not finite (#680's known limitation).</exception>
        public double Angle(in Vector3 vector, in Plane plane)
        {
            return Angle(vector, plane.Normal);
        }

        /// <summary>
        /// The signed angle from this vector to <paramref name="vector"/>, positive counterclockwise about
        /// <paramref name="plane"/>, the normal of the plane of the angle, of any finite non-zero length, in (−π, π]:
        /// atan2((a × b) · n̂, a · b). A zero vector gives 0, as the unsigned <see cref="Angle(in Vector3)"/> does.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="plane"/> has a magnitude of 0 (a zero normal, or components all
        /// below about 1.5e-162), or a component or a magnitude that is not finite: it orients no plane (#680's known
        /// limitation; before 11.0.0 the unsigned 0 or π was returned, its sign lost).</exception>
        public double Angle(in Vector3 vector, in Vector3 plane)
        {
            if (!double.IsFinite(plane.X) || !double.IsFinite(plane.Y) || !double.IsFinite(plane.Z))
            {
                throw new ArgumentException($"The plane normal {plane} has a component that is not finite and orients no plane.", nameof(plane));
            }

            double m = plane.Magnitude();
            if (!(m > 0.0) || !double.IsFinite(m))
            {
                throw new ArgumentException($"The plane normal {plane} has a magnitude of 0 or one that is not finite, and orients no plane.", nameof(plane));
            }

            return double.Atan2(Cross(vector) * (plane / m), this * vector);
        }

        /// <summary>
        /// The signed angle about a normal normalized leniently, a zero normal giving the unsigned 0 or π: the library's
        /// own signed angle of <see cref="Maneuver.ApsidalAlignmentManeuver"/>, bit for bit the former public overload.
        /// </summary>
        internal double AngleAboutNormalOrZero(in Vector3 vector, in Vector3 plane)
        {
            return double.Atan2(Cross(vector) * plane.NormalizeOrZero(), this * vector);
        }

        public Vector3 Inverse()
        {
            return this * -1.0;
        }

        public static Vector3 operator *(in Vector3 v, double value)
        {
            return new Vector3(v.X * value, v.Y * value, v.Z * value);
        }

        public static double operator *(in Vector3 v, in Vector3 value)
        {
            return v.X * value.X + v.Y * value.Y + v.Z * value.Z;
        }

        public static Vector3 operator /(in Vector3 v, double value)
        {
            return new Vector3(v.X / value, v.Y / value, v.Z / value);
        }

        public static Vector3 operator +(in Vector3 v1, in Vector3 v2)
        {
            return new Vector3(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
        }

        public static Vector3 operator -(in Vector3 v1, in Vector3 v2)
        {
            return new Vector3(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
        }

        /// <summary>
        /// The unit quaternion of the shortest rotation that turns the direction of this vector onto the direction of
        /// <paramref name="vector"/>: the identity for the same direction, a half turn (w = 0) about an axis perpendicular
        /// to both for opposite directions, otherwise the rotation about this vector × <paramref name="vector"/>.
        /// </summary>
        /// <param name="vector">The target direction, of any finite non-zero length.</param>
        /// <remarks>
        /// Only the directions count: vectors whose lengths or the product of their lengths lie outside about 1e-150 to
        /// 1e150 are first divided by their largest component, so that their squares are represented (before 11.0.0 they
        /// gave NaN or a quaternion that was not a rotation). Two vectors collinear and opposite in doubles (their cross
        /// product exactly zero) give a half turn, also when their <see cref="Angle(in Vector3)"/> is not exactly π
        /// through rounding, which gave the zero quaternion or the identity before 11.0.0 (review of #680's known
        /// limitation). Opposite vectors whose cross product is a rounding residue still give the rotation about that
        /// residue, which can miss the target.
        /// </remarks>
        /// <exception cref="InvalidOperationException">This vector is zero or has a component that is not finite: it has
        /// no direction to rotate from (the identity, or NaN, was returned before 11.0.0).</exception>
        /// <exception cref="ArgumentException"><paramref name="vector"/> is zero or has a component that is not finite: it
        /// has no direction to rotate to (the identity, or NaN, was returned before 11.0.0).</exception>
        public Quaternion To(in Vector3 vector)
        {
            if (!IsFiniteNonZero(this))
            {
                throw new InvalidOperationException($"The vector {this} is zero or not finite and has no direction to rotate from.");
            }

            if (!IsFiniteNonZero(vector))
            {
                throw new ArgumentException($"The vector {vector} is zero or not finite and has no direction to rotate to.", nameof(vector));
            }

            return RotationTo(vector);
        }

        /// <summary>
        /// The rotation of <see cref="To"/>, the identity for a zero vector: the library's own attitudes, whose degenerate
        /// targets (a zero position, velocity, angular momentum or delta-V) kept the identity, bit for bit the former public
        /// method there (review of #680's known limitation).
        /// </summary>
        internal Quaternion ToOrIdentity(in Vector3 vector)
        {
            if (Magnitude() == 0.0 || vector.Magnitude() == 0.0)
            {
                return Quaternion.Identity;
            }

            return RotationTo(vector);
        }

        private static bool IsFiniteNonZero(in Vector3 v)
        {
            return double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z) && (v.X != 0.0 || v.Y != 0.0 || v.Z != 0.0);
        }

        private static double LargestComponent(in Vector3 v)
        {
            return System.Math.Max(System.Math.Abs(v.X), System.Math.Max(System.Math.Abs(v.Y), System.Math.Abs(v.Z)));
        }

        // The rotation between two non-zero vectors; vectors whose squares or the product of whose lengths would overflow or
        // lose their digits below the normal range are divided by their largest component first, so that the result does
        // not depend on their lengths; every other pair is computed bit for bit as before 11.0.0
        private Quaternion RotationTo(in Vector3 vector)
        {
            double largestFrom = LargestComponent(this);
            double largestTo = LargestComponent(vector);
            double product = largestFrom * largestTo;
            if (largestFrom > 1e-150 && largestFrom < 1e150 && largestTo > 1e-150 && largestTo < 1e150 && product > 1e-150 && product < 1e150)
            {
                return RotationBetween(this, vector);
            }

            return RotationBetween(this / largestFrom, vector / largestTo);
        }

        private static Quaternion RotationBetween(in Vector3 from, in Vector3 to)
        {
            var dot = from * to;
            var angle = System.Math.Abs(from.Angle(to));
            if (angle <= double.Epsilon)
            {
                return Quaternion.Identity;
            }

            var mag1 = from.Magnitude();
            var mag2 = to.Magnitude();
            var v = from.Cross(to);

            // Opposite directions: the angle π exactly, or a cross product exactly zero with a negative dot product (collinear
            // in doubles, where the angle can be π less a rounding residue and w = dot + |a| |b| is 0 or a residue)
            if (System.Math.Abs(angle - Constants.PI) < double.Epsilon || (v.X == 0.0 && v.Y == 0.0 && v.Z == 0.0 && dot < 0.0))
            {
                return HalfTurnOnto(to);
            }

            var w = dot + System.Math.Sqrt(mag1 * mag1 * mag2 * mag2);

            return new Quaternion(w, v.X, v.Y, v.Z).NormalizeOrZero();
        }

        // A half turn about the axis perpendicular to the target and to the coordinate axis of its smallest component
        private static Quaternion HalfTurnOnto(in Vector3 to)
        {
            double x = System.Math.Abs(to.X);
            double y = System.Math.Abs(to.Y);
            double z = System.Math.Abs(to.Z);

            Vector3 axis = x < y ? (x < z ? VectorX : VectorZ) : (y < z ? VectorY : VectorZ);
            var vec = to.Cross(axis);
            return new Quaternion(0.0, vec.X, vec.Y, vec.Z).NormalizeOrZero();
        }

        public Vector3 Rotate(in Quaternion quaternion)
        {
            var p = new Quaternion(0.0, this);
            return (quaternion * p * quaternion.Conjugate()).VectorPart;
        }

        /// <summary>
        /// Performs linear interpolation between this vector and the specified vector.
        /// </summary>
        /// <param name="vector">The target vector to interpolate towards.</param>
        /// <param name="t">The interpolation factor, clamped between 0.0 and 1.0.</param>
        /// <returns>A new <see cref="Vector3"/> that is the result of the linear interpolation.</returns>
        public Vector3 LinearInterpolation(in Vector3 vector, double t)
        {
            t = System.Math.Clamp(t, 0.0, 1.0);
            return this + (vector - this) * t;
        }

        public override string ToString()
        {
            return $"X : {X} Y : {Y} Z: {Z}";
        }
    }
}