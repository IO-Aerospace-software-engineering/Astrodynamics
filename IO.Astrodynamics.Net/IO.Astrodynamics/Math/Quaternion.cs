using System;
using System.Runtime.CompilerServices;

namespace IO.Astrodynamics.Math
{
    public readonly record struct Quaternion
    {
        /// <summary>
        /// The identity rotation, (1, 0, 0, 0), equal to <c>new Quaternion()</c>: a vector rotated by it is unchanged. It is
        /// <c>readonly</c>, so no caller can change the identity of the library (#571; <c>Quaternion.Zero</c> before 11.0.0).
        /// </summary>
        public static readonly Quaternion Identity = new(1, 0, 0, 0);

        public double W { get; }

        public Vector3 VectorPart { get; }

        public Quaternion() : this(1.0, 0.0, 0.0, 0.0)
        {
        }

        public Quaternion(double w, Vector3 vectorPart) : this(w, vectorPart.X, vectorPart.Y, vectorPart.Z)
        {
        }


        /// <summary>
        /// The unit quaternion of the rotation by <paramref name="angle"/> about the direction of <paramref name="axis"/>:
        /// (cos(a/2), k sin(a/2)), k = axis / |axis| (the Euler-Rodrigues parameters, a unit quaternion only for a unit k).
        /// </summary>
        /// <param name="axis">The direction of the rotation axis, of any finite non-zero length; a vector rotated by the
        /// quaternion turns by <paramref name="angle"/> about it, positive counterclockwise seen from its tip.</param>
        /// <param name="angle">The rotation angle, in radians.</param>
        /// <remarks>
        /// The axis is divided by its magnitude (#688; before 11.0.0 it was taken as it was, so a non-unit axis gave a
        /// non-unit quaternion, whose rotation of a vector was scaled by |q|² and turned by another angle). A unit axis
        /// (|axis| = 1 exactly) gives the same quaternion bit for bit; an axis whose squared components would overflow or
        /// lose their digits (a component above about 1.34e154, or all below about 1e-150) is scaled by the power of two of
        /// its largest component first (<see cref="Vector3.Normalize"/>), so that its magnitude is represented.
        /// </remarks>
        /// <exception cref="ArgumentException">The axis is zero or has a component that is not finite (NaN or infinite): it
        /// has no direction (#688).</exception>
        public Quaternion(Vector3 axis, double angle)
        {
            var unitAxis = UnitAxis(axis);
            double c = System.Math.Cos(angle * 0.5);
            double s = System.Math.Sin(angle * 0.5);
            W = c;
            VectorPart = new Vector3(s * unitAxis.X, s * unitAxis.Y, s * unitAxis.Z);
        }

        /// <summary>
        /// The quaternion (cos(a/2), axis sin(a/2)) of an axis taken as it is, unchecked: the library's own axis-angle
        /// quaternions, bit for bit the former public constructor (#688). Its axes are unit vectors, or the zero vector of a
        /// zero angular velocity at a zero angle, which gives the identity.
        /// </summary>
        internal static Quaternion FromAxisAngle(in Vector3 axis, double angle)
        {
            double c = System.Math.Cos(angle * 0.5);
            double s = System.Math.Sin(angle * 0.5);
            return new Quaternion(c, s * axis.X, s * axis.Y, s * axis.Z);
        }

        // The unit vector of a finite non-zero axis, scaled first by the power of two of its largest component when the
        // squares of its components would overflow or lose their digits below the normal range (Vector3.Normalize)
        private static Vector3 UnitAxis(in Vector3 axis)
        {
            if (!double.IsFinite(axis.X) || !double.IsFinite(axis.Y) || !double.IsFinite(axis.Z))
            {
                throw new ArgumentException($"The rotation axis {axis} has a component that is not finite and no direction.", nameof(axis));
            }

            if (axis.X == 0.0 && axis.Y == 0.0 && axis.Z == 0.0)
            {
                throw new ArgumentException("The rotation axis is the zero vector and has no direction.", nameof(axis));
            }

            return axis.Normalize();
        }

        public Quaternion(double w, double x, double y, double z)
        {
            W = w;
            VectorPart = new Vector3(x, y, z);
        }

        /// <summary>
        /// The magnitude of this quaternion, √(w² + x² + y² + z²).
        /// </summary>
        /// <remarks>
        /// A quaternion whose squared components would overflow or lose their digits below the normal range (a component
        /// above about 1.34e154, or all below about 1e-150) is first scaled by the power of two of its largest component,
        /// exactly, so its magnitude is the representable one (third round of #668: +Infinity or 0 before 11.0.0). Every
        /// other quaternion gives the unscaled magnitude, bit for bit. A NaN component gives NaN, an infinite one +Infinity.
        /// </remarks>
        public double Magnitude()
        {
            double s = MagnitudeSquared();
            if (s >= Vector3.OrdinaryLowest && s <= double.MaxValue)
            {
                return System.Math.Sqrt(s);
            }

            return ScaledMagnitude(s);
        }

        private double MagnitudeSquared()
        {
            return W * W + VectorPart.X * VectorPart.X + VectorPart.Y * VectorPart.Y + VectorPart.Z * VectorPart.Z;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private double ScaledMagnitude(double s)
        {
            if (!IsFinite() || IsZero())
            {
                return System.Math.Sqrt(s);
            }

            int k = System.Math.ILogB(LargestComponent());
            return System.Math.ScaleB(System.Math.Sqrt(ScaleB(-k).MagnitudeSquared()), k);
        }

        /// <summary>
        /// The unit quaternion of this quaternion.
        /// </summary>
        /// <remarks>
        /// Every finite non-zero quaternion has one: a quaternion whose squared components would overflow or lose their
        /// digits (a component above about 1.34e154, or all below about 1e-150) is first scaled by the power of two of its
        /// largest component, exactly (third round of #668: such quaternions gave the zero quaternion or threw before
        /// 11.0.0). Every other quaternion gives the quaternion over its unscaled magnitude, bit for bit.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The quaternion is zero (#680; the quaternion itself was returned
        /// before 11.0.0, and a vector rotated by it was zero), or has a component that is not finite (third round of #668;
        /// NaN components were returned before 11.0.0): it is no rotation.</exception>
        public Quaternion Normalize()
        {
            double s = MagnitudeSquared();
            if (s >= Vector3.OrdinaryLowest && s <= double.MaxValue)
            {
                double m = System.Math.Sqrt(s);
                return new Quaternion(W / m, VectorPart / m);
            }

            if (!IsFinite())
            {
                throw new InvalidOperationException($"The quaternion {this} has a component that is not finite and is no rotation.");
            }

            if (IsZero())
            {
                throw new InvalidOperationException($"The quaternion {this} has a magnitude of 0 and is no rotation.");
            }

            return ScaledUnit();
        }

        /// <summary>
        /// The unit quaternion of this quaternion, or this quaternion when it is zero: the library's own normalizations, bit
        /// for bit the former <see cref="Normalize"/> (#680) on every quaternion whose sum of squares lies in [1e-300,
        /// double.MaxValue]; a quaternion with a component that is not finite keeps its former result (NaN components), and
        /// any other finite non-zero quaternion is scaled as <see cref="Normalize"/> does (third round of #668).
        /// </summary>
        internal Quaternion NormalizeOrZero()
        {
            double s = MagnitudeSquared();
            if (s >= Vector3.OrdinaryLowest && s <= double.MaxValue || !IsFinite())
            {
                double m = System.Math.Sqrt(s);
                return new Quaternion(W / m, VectorPart / m);
            }

            if (IsZero())
            {
                return this;
            }

            return ScaledUnit();
        }

        /// <summary>
        /// Whether this quaternion can be normalized: every component finite and not all zero.
        /// </summary>
        internal bool IsNormalizable()
        {
            return IsFinite() && !IsZero();
        }

        private bool IsFinite()
        {
            return double.IsFinite(W) && double.IsFinite(VectorPart.X) && double.IsFinite(VectorPart.Y) && double.IsFinite(VectorPart.Z);
        }

        private bool IsZero()
        {
            return W == 0.0 && VectorPart.X == 0.0 && VectorPart.Y == 0.0 && VectorPart.Z == 0.0;
        }

        private double LargestComponent()
        {
            return System.Math.Max(System.Math.Max(System.Math.Abs(W), System.Math.Abs(VectorPart.X)),
                System.Math.Max(System.Math.Abs(VectorPart.Y), System.Math.Abs(VectorPart.Z)));
        }

        private Quaternion ScaleB(int n)
        {
            return new Quaternion(System.Math.ScaleB(W, n), System.Math.ScaleB(VectorPart.X, n), System.Math.ScaleB(VectorPart.Y, n),
                System.Math.ScaleB(VectorPart.Z, n));
        }

        // The unit quaternion of a finite non-zero quaternion, scaled first by the power of two of its largest component
        // (exact), so that its sum of squares lies in [1, 4]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Quaternion ScaledUnit()
        {
            var scaled = ScaleB(-System.Math.ILogB(LargestComponent()));
            double m = System.Math.Sqrt(scaled.MagnitudeSquared());
            return new Quaternion(scaled.W / m, scaled.VectorPart / m);
        }

        // The unit quaternion of an argument (parameterName) or of this instance (parameterName null), which must be
        // normalizable: ArgumentException for an argument, InvalidOperationException for the instance
        internal static Quaternion RequireUnit(in Quaternion q, string parameterName)
        {
            if (q.IsNormalizable())
            {
                return q.Normalize();
            }

            string message = $"The quaternion {q} is zero or has a component that is not finite, and is no rotation.";
            if (parameterName == null)
            {
                throw new InvalidOperationException(message);
            }

            throw new ArgumentException(message, parameterName);
        }

        public Quaternion Conjugate()
        {
            return new Quaternion(W, VectorPart.Inverse());
        }

        public static Quaternion operator *(Quaternion lhs, double rhs)
        {
            return new Quaternion(lhs.W * rhs, lhs.VectorPart * rhs);
        }

        public static Quaternion operator *(double lhs, Quaternion rhs)
        {
            return rhs * lhs;
        }

        public static Quaternion operator *(Quaternion lhs, Quaternion rhs)
        {
            return new Quaternion(lhs.W * rhs.W - lhs.VectorPart.Dot(rhs.VectorPart), rhs.VectorPart * lhs.W + lhs.VectorPart * rhs.W + lhs.VectorPart.Cross(rhs.VectorPart));
        }

        public static Quaternion operator /(Quaternion lhs, double rhs)
        {
            return new Quaternion(lhs.W / rhs, lhs.VectorPart / rhs);
        }


        public Vector3 ToEuler()
        {
            double sinrCosp = 2 * (W * VectorPart.X + VectorPart.Y * VectorPart.Z);
            double cosrCosp = 1 - 2 * (VectorPart.X * VectorPart.X + VectorPart.Y * VectorPart.Y);
            var x = System.Math.Atan2(sinrCosp, cosrCosp);

            // pitch (y-axis rotation)
            double sinp = System.Math.Sqrt(1 + 2 * (W * VectorPart.Y - VectorPart.X * VectorPart.Z));
            double cosp = System.Math.Sqrt(1 - 2 * (W * VectorPart.Y - VectorPart.X * VectorPart.Z));
            var y = 2 * System.Math.Atan2(sinp, cosp) - System.Math.PI / 2;

            // yaw (z-axis rotation)
            double sinyCosp = 2 * (W * VectorPart.Z + VectorPart.X * VectorPart.Y);
            double cosyCosp = 1 - 2 * (VectorPart.Y * VectorPart.Y + VectorPart.Z * VectorPart.Z);
            var z = System.Math.Atan2(sinyCosp, cosyCosp);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Performs Spherical Linear Interpolation (SLERP) between the rotation of the current quaternion and the rotation
        /// of the specified quaternion, on the shorter path.
        /// </summary>
        /// <param name="q">The target quaternion to interpolate towards.</param>
        /// <param name="t">The interpolation factor, clamped between 0 and 1.</param>
        /// <returns>The interpolated unit quaternion.</returns>
        /// <remarks>
        /// Both quaternions are normalized first, so the result is the SLERP of their rotations (#680's known limitation;
        /// before 11.0.0 a non-unit input gave the SLERP of the quaternions themselves, not a rotation). A unit input
        /// whose magnitude rounds to 1 gives the same result bit for bit; one that is unit only to rounding can move it by
        /// about 1e-16.
        /// </remarks>
        /// <exception cref="InvalidOperationException">This quaternion is zero or has a component that is not finite: it is
        /// no rotation. Every finite non-zero quaternion is one, its magnitude scaled as <see cref="Normalize"/> does (third
        /// round of #668: outside about 1e-154 to 1e154 it threw before 11.0.0).</exception>
        /// <exception cref="ArgumentException"><paramref name="q"/> is zero or has a component that is not
        /// finite.</exception>
        public Quaternion Slerp(Quaternion q, double t)
        {
            var from = RequireUnit(this, null);
            var to = RequireUnit(q, nameof(q));
            return from.SlerpUnchecked(to, t);
        }

        /// <summary>
        /// The SLERP of the quaternions as they are, unchecked: the body of the public <see cref="Slerp"/>, bit for bit
        /// its former self. The orientation cache of a propagation interpolates by <see cref="SlerpByAxisAngle"/> since
        /// 11.0.0 (#561).
        /// </summary>
        internal Quaternion SlerpUnchecked(Quaternion q, double t)
        {
            // Clamps the parameter t between 0 and 1
            t = System.Math.Clamp(t, 0.0, 1.0);

            // Calculates the cosine of the angle between the quaternions
            double dot = W * q.W + VectorPart.X * q.VectorPart.X + VectorPart.Y * q.VectorPart.Y + VectorPart.Z * q.VectorPart.Z;

            // If the dot product is negative, invert one of the quaternions to take the shortest path
            if (dot < 0.0f)
            {
                q = new Quaternion(-q.W, -q.VectorPart.X, -q.VectorPart.Y, -q.VectorPart.Z);
                dot = -dot;
            }

            // If the quaternions are very close, use linear interpolation to avoid numerical errors
            const float epsilon = 0.0001f;
            if (dot > 1.0f - epsilon)
            {
                // Linear interpolation
                return LerpUnchecked(q, t);
            }

            // Calculates the angle between the quaternions
            double theta_0 = System.Math.Acos(dot); // initial angle
            double theta = theta_0 * t; // interpolated angle

            // Calculates the intermediate quaternions
            double sin_theta_0 = System.Math.Sin(theta_0);
            double sin_theta = System.Math.Sin(theta);

            double s1 = System.Math.Cos(theta) - dot * sin_theta / sin_theta_0;
            double s2 = sin_theta / sin_theta_0;

            // Interpolates the quaternions
            return new Quaternion((s1 * W) + (s2 * q.W),
                (s1 * VectorPart.X) + (s2 * q.VectorPart.X),
                (s1 * VectorPart.Y) + (s2 * q.VectorPart.Y),
                (s1 * VectorPart.Z) + (s2 * q.VectorPart.Z));
        }

        /// <summary>
        /// The exact spherical linear interpolation from <paramref name="q0"/> to <paramref name="q1"/> at
        /// <paramref name="fraction"/>, by the axis and angle of q1 q0⁻¹ on the shorter path, with no linear fallback: the
        /// rotation by <c>fraction</c> times that angle about that axis, composed with q0 (the interpolation of a type-3 CK).
        /// </summary>
        /// <returns>The interpolated rotation, and the unit axis and the angle (rad, in [0, π]) of q1 q0⁻¹; for two equal
        /// rotations, q0, a zero axis and a zero angle.</returns>
        internal static (Quaternion Rotation, Vector3 Axis, double Angle) SlerpByAxisAngle(in Quaternion q0, in Quaternion q1, double fraction)
        {
            var delta = (q1 * q0.Conjugate()).NormalizeOrZero();
            if (delta.W < 0.0)
            {
                delta = delta * -1.0;
            }

            double sinHalf = delta.VectorPart.Magnitude();
            if (sinHalf == 0.0)
            {
                return (q0, Vector3.Zero, 0.0);
            }

            double angle = 2.0 * System.Math.Atan2(sinHalf, delta.W);
            var axis = delta.VectorPart / sinHalf;
            return ((FromAxisAngle(axis, angle * fraction) * q0).NormalizeOrZero(), axis, angle);
        }

        /// <summary>
        /// Linearly interpolates between the current quaternion and the specified quaternion, q + t (q2 − q), normalized.
        /// </summary>
        /// <param name="q2">The target quaternion to interpolate towards.</param>
        /// <param name="t">The interpolation factor, clamped between 0 and 1.</param>
        /// <returns>The interpolated unit quaternion.</returns>
        /// <remarks>
        /// The path is the chord from this quaternion to <paramref name="q2"/>, as given: no hemisphere is chosen, so from
        /// q to a quaternion of the other hemisphere it takes the longer rotation. A chord through zero has no rotation
        /// there (#680's known limitation; the zero quaternion was returned before 11.0.0). Every finite non-zero quaternion
        /// is accepted: a chord that overflows (components of opposite signs near <see cref="double.MaxValue"/>), or of
        /// quaternions whose largest component is below 1e-150, whose products would round into the subnormal range, is
        /// computed on both quaternions scaled by one power of two, and the result normalized as <see cref="Normalize"/>
        /// does (third round of #668: outside about 1e-154 to 1e154 it threw before 11.0.0). Every other chord is the
        /// unscaled one, bit for bit.
        /// </remarks>
        /// <exception cref="InvalidOperationException">This quaternion is zero or has a component that is not
        /// finite.</exception>
        /// <exception cref="ArgumentException"><paramref name="q2"/> is zero or has a component that is not finite; or the
        /// interpolated quaternion is zero (<paramref name="q2"/> a negative multiple of this quaternion, as −q at
        /// t = 0.5).</exception>
        public Quaternion Lerp(Quaternion q2, double t)
        {
            RequireUnit(this, null);
            RequireUnit(q2, nameof(q2));

            double largest = System.Math.Max(LargestComponent(), q2.LargestComponent());
            var result = LerpRaw(q2, t);
            if (!result.IsFinite() || largest < LerpLowest)
            {
                // The chord of two finite quaternions overflowed (components of opposite signs near double.MaxValue), or
                // its products would round into the subnormal range (quaternions below about 1e-150): the same chord on both
                // scaled by one power of two, which leaves the direction of the result unchanged
                int k = -System.Math.ILogB(largest);
                result = ScaleB(k).LerpRaw(q2.ScaleB(k), t);
            }

            if (!result.IsNormalizable())
            {
                throw new ArgumentException(
                    $"The linear interpolation from {this} to {q2} at t = {t} is {result}, which has a magnitude of 0 or one that is not finite, and is no rotation.",
                    nameof(q2));
            }

            return result.Normalize();
        }

        // The largest component below which Lerp scales its quaternions: the products t (q2 − q) of a larger one keep the
        // digits that count (any one lost lies below 2^-1074 / 1e-150 of the largest, relative), bit for bit as before
        private const double LerpLowest = 1e-150;

        // The former public Lerp, unchecked: the fallback of the unchecked SLERP, bit for bit
        private Quaternion LerpUnchecked(Quaternion q2, double t)
        {
            return LerpRaw(q2, t).NormalizeOrZero();
        }

        private Quaternion LerpRaw(in Quaternion q2, double t)
        {
            t = System.Math.Clamp(t, 0.0, 1.0);

            return new Quaternion(W + t * (q2.W - W),
                VectorPart.X + t * (q2.VectorPart.X - VectorPart.X),
                VectorPart.Y + t * (q2.VectorPart.Y - VectorPart.Y),
                VectorPart.Z + t * (q2.VectorPart.Z - VectorPart.Z)
            );
        }
    }
}