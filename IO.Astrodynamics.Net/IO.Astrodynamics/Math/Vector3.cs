using System;
using System.Runtime.CompilerServices;

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

        // The sums of squares of the ordinary path: a vector whose sum of squares lies in [OrdinaryLowest, double.MaxValue]
        // is computed bit for bit as before 11.0.0; outside it (a component above about 1.34e154 or all below about
        // 1e-150, or a component that is not finite) the vector is scaled by the power of two of its largest component
        internal const double OrdinaryLowest = 1e-300;

        /// <summary>
        /// The Euclidean length of this vector, √(x² + y² + z²).
        /// </summary>
        /// <remarks>
        /// A vector whose squared components would overflow or lose their digits below the normal range (a component above
        /// about 1.34e154, or all below about 1e-150) is first scaled by the power of two of its largest component, exactly,
        /// so its magnitude is the representable one: +Infinity only above <see cref="double.MaxValue"/>, 0 only for the
        /// zero vector (third round of #668: +Infinity for (1e200, 0, 0) and 0 for (1e-170, 0, 0) before 11.0.0). Every
        /// other vector gives the unscaled √(x² + y² + z²), bit for bit. A NaN component gives NaN, an infinite one
        /// +Infinity.
        /// </remarks>
        public double Magnitude()
        {
            double s = MagnitudeSquared();
            if (s >= OrdinaryLowest && s <= double.MaxValue)
            {
                return System.Math.Sqrt(s);
            }

            return ScaledMagnitude(s);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private double ScaledMagnitude(double s)
        {
            if (!IsFinite(this) || IsZero(this))
            {
                return System.Math.Sqrt(s);
            }

            int k = System.Math.ILogB(LargestComponent(this));
            return System.Math.ScaleB(System.Math.Sqrt(ScaleB(this, -k).MagnitudeSquared()), k);
        }

        public double MagnitudeSquared()
        {
            return X * X + Y * Y + Z * Z;
        }

        /// <summary>
        /// The unit vector of this vector.
        /// </summary>
        /// <remarks>
        /// Every finite non-zero vector has one: a vector whose squared components would overflow or lose their digits (a
        /// component above about 1.34e154, or all below about 1e-150) is first scaled by the power of two of its largest
        /// component, exactly (third round of #668: such vectors gave the zero vector, a vector off by up to 5.5e-6, or
        /// threw, before 11.0.0). Every other vector gives the vector over its unscaled magnitude, bit for bit.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The vector is zero (#680; the vector itself was returned before
        /// 11.0.0), or has a component that is not finite (third round of #668; NaN components were returned before 11.0.0):
        /// it has no direction.</exception>
        public Vector3 Normalize()
        {
            double s = MagnitudeSquared();
            if (s >= OrdinaryLowest && s <= double.MaxValue)
            {
                return this / System.Math.Sqrt(s);
            }

            if (!IsFinite(this))
            {
                throw new InvalidOperationException($"The vector {this} has a component that is not finite and no direction.");
            }

            if (IsZero(this))
            {
                throw new InvalidOperationException($"The vector {this} has a magnitude of 0 and no direction.");
            }

            return ScaledUnit();
        }

        /// <summary>
        /// The unit vector of this vector, or this vector when it is zero: the library's own normalizations, whose
        /// degenerate cases rely on the zero vector (a zero angular velocity rotates nothing, a coincident target is at
        /// angle 0, the transfer plane of collinear positions has no z component), bit for bit the former
        /// <see cref="Normalize"/> (#680) on every vector whose sum of squares lies in [1e-300, double.MaxValue]; a vector
        /// with a component that is not finite keeps its former result (NaN components), and any other finite non-zero
        /// vector is scaled as <see cref="Normalize"/> does (third round of #668).
        /// </summary>
        internal Vector3 NormalizeOrZero()
        {
            double s = MagnitudeSquared();
            if (s >= OrdinaryLowest && s <= double.MaxValue || !IsFinite(this))
            {
                return this / System.Math.Sqrt(s);
            }

            if (IsZero(this))
            {
                return this;
            }

            return ScaledUnit();
        }

        // The unit vector of a finite non-zero vector, scaled first by the power of two of its largest component (exact),
        // so that its sum of squares lies in [1, 3]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Vector3 ScaledUnit()
        {
            var scaled = ScaleB(this, -System.Math.ILogB(LargestComponent(this)));
            return scaled / System.Math.Sqrt(scaled.MagnitudeSquared());
        }

        private static Vector3 ScaleB(in Vector3 v, int n)
        {
            return new Vector3(System.Math.ScaleB(v.X, n), System.Math.ScaleB(v.Y, n), System.Math.ScaleB(v.Z, n));
        }

        private static bool IsFinite(in Vector3 v)
        {
            return double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);
        }

        private static bool IsZero(in Vector3 v)
        {
            return v.X == 0.0 && v.Y == 0.0 && v.Z == 0.0;
        }

        // Whether both sums of squares lie in [1e-300, 1e300], where the magnitudes and their product are represented
        // without loss: the ordinary path of the cosine, bit for bit as before 11.0.0
        private static bool IsOrdinaryPair(double s1, double s2)
        {
            return s1 >= OrdinaryLowest && s1 <= 1e300 && s2 >= OrdinaryLowest && s2 <= 1e300;
        }

        /// <summary>
        /// The cosine of the angle between two finite non-zero vectors, clamped to [-1, 1]: (a · b) / (|a| |b|) when both
        /// sums of squares lie in [1e-300, 1e300] (bit for bit the former quotient, the clamp changing only a quotient
        /// rounded past ±1), otherwise the dot product of their unit vectors scaled as <see cref="Normalize"/> does.
        /// </summary>
        internal static double Cosine(in Vector3 a, in Vector3 b)
        {
            double s1 = a.MagnitudeSquared();
            double s2 = b.MagnitudeSquared();
            double cosine = IsOrdinaryPair(s1, s2)
                ? (a * b) / (System.Math.Sqrt(s1) * System.Math.Sqrt(s2))
                : a.Normalize() * b.Normalize();
            return System.Math.Clamp(cosine, -1.0, 1.0);
        }

        public Vector3 Cross(in Vector3 vector)
        {
            return new Vector3((Y * vector.Z) - (Z * vector.Y), (Z * vector.X) - (X * vector.Z), (X * vector.Y) - (Y * vector.X));
        }

        /// <summary>
        /// The angle between this vector and <paramref name="vector"/>, in [0, π]; 0 by convention when either is zero.
        /// </summary>
        /// <remarks>
        /// Vectors whose sums of squares lie outside [1e-300, 1e300] are compared through their unit vectors, scaled as
        /// <see cref="Normalize"/> does (third round of #668: two vectors below about 1e-154 gave 0 and two above about
        /// 1e154 NaN before 11.0.0); every other pair gives the former acos((a · b) / (|a| |b|)), clamped, bit for bit. A
        /// vector with a component that is not finite gives NaN.
        /// </remarks>
        public double Angle(in Vector3 vector)
        {
            // Handle zero vectors
            if (IsZero(this) || IsZero(vector))
            {
                return 0.0; // Angle is undefined, return 0 by convention
            }

            if (!IsFinite(this) || !IsFinite(vector))
            {
                return double.NaN;
            }

            return System.Math.Acos(Cosine(this, vector));
        }

        /// <summary>
        /// The signed angle from this vector to <paramref name="vector"/>, positive counterclockwise about the normal of
        /// <paramref name="plane"/>, in (−π, π]: atan2((a × b) · n̂, a · b).
        /// </summary>
        /// <exception cref="ArgumentException">The normal of <paramref name="plane"/> is zero or has a component that is not
        /// finite (#680's known limitation).</exception>
        public double Angle(in Vector3 vector, in Plane plane)
        {
            return Angle(vector, plane.Normal);
        }

        /// <summary>
        /// The signed angle from this vector to <paramref name="vector"/>, positive counterclockwise about
        /// <paramref name="plane"/>, the normal of the plane of the angle, of any finite non-zero length, in (−π, π]:
        /// atan2((a × b) · n̂, a · b). A zero vector gives 0 and a vector with a component that is not finite NaN, as the
        /// unsigned <see cref="Angle(in Vector3)"/> does.
        /// </summary>
        /// <remarks>
        /// Two finite non-zero vectors whose sums of squares are not both in [1e-300, 1e300] are first scaled by the
        /// powers of two of their largest components, exactly, so their products neither overflow nor lose their digits
        /// (third round of #668: from (1e200, 0, 0) to (1e200, 2e200, 0) gave π/4 instead of atan 2, most such pairs NaN,
        /// and vectors below about 1e-154 gave 0); every other pair gives the former atan2, bit for bit.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="plane"/> is the zero vector or has a component that is not
        /// finite: it orients no plane (#680's known limitation; before 11.0.0 the unsigned 0 or π was returned, its sign
        /// lost). A tiny or huge finite normal orients its plane (third round of #668: below about 1.5e-162 or above about
        /// 1.34e154 it threw).</exception>
        public double Angle(in Vector3 vector, in Vector3 plane)
        {
            if (!IsFinite(plane))
            {
                throw new ArgumentException($"The plane normal {plane} has a component that is not finite and orients no plane.", nameof(plane));
            }

            if (IsZero(plane))
            {
                throw new ArgumentException($"The plane normal {plane} has a magnitude of 0 and orients no plane.", nameof(plane));
            }

            var n = plane.Normalize();
            double s1 = MagnitudeSquared();
            double s2 = vector.MagnitudeSquared();
            if (IsOrdinaryPair(s1, s2) || !IsFiniteNonZero(this) || !IsFiniteNonZero(vector))
            {
                return double.Atan2(Cross(vector) * n, this * vector);
            }

            // Vectors whose products would overflow or lose their digits: both scaled by the powers of two of their largest
            // components (exact), which multiplies (a × b) · n̂ and a · b by the same power of two and leaves the angle
            var a = ScaleB(this, -System.Math.ILogB(LargestComponent(this)));
            var b = ScaleB(vector, -System.Math.ILogB(LargestComponent(vector)));
            return double.Atan2(a.Cross(b) * n, a * b);
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

        internal static bool IsFiniteNonZero(in Vector3 v)
        {
            return IsFinite(v) && !IsZero(v);
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