namespace IO.Astrodynamics.Math
{
    public readonly record struct Quaternion
    {
        public static Quaternion Zero = new(1, 0, 0, 0);
        public double W { get; }

        public Vector3 VectorPart { get; }

        public Quaternion() : this(1.0, 0.0, 0.0, 0.0)
        {
        }

        public Quaternion(double w, Vector3 vectorPart) : this(w, vectorPart.X, vectorPart.Y, vectorPart.Z)
        {
        }


        public Quaternion(Vector3 axis, double angle)
        {
            double c = System.Math.Cos(angle * 0.5);
            double s = System.Math.Sin(angle * 0.5);
            W = c;
            VectorPart = new Vector3(s * axis.X, s * axis.Y, s * axis.Z);
        }

        public Quaternion(double w, double x, double y, double z)
        {
            W = w;
            VectorPart = new Vector3(x, y, z);
        }

        public double Magnitude()
        {
            return System.Math.Sqrt(W * W + VectorPart.X * VectorPart.X + VectorPart.Y * VectorPart.Y + VectorPart.Z * VectorPart.Z);
        }

        public Quaternion Normalize()
        {
            double m = Magnitude();
            if (m == 0)
            {
                return this;
            }

            return new Quaternion(W / m, VectorPart / m);
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
            return new Quaternion(lhs.W * rhs.W - lhs.VectorPart * rhs.VectorPart, rhs.VectorPart * lhs.W + lhs.VectorPart * rhs.W + lhs.VectorPart.Cross(rhs.VectorPart));
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
        /// Performs Spherical Linear Interpolation (SLERP) between the current quaternion and the specified quaternion.
        /// </summary>
        /// <param name="q">The target quaternion to interpolate towards.</param>
        /// <param name="t">The interpolation factor, clamped between 0 and 1.</param>
        /// <returns>The interpolated quaternion.</returns>
        public Quaternion SLERP(Quaternion q, double t)
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
                return Lerp(q, t);
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
        /// Linearly interpolates between the current quaternion and the specified quaternion.
        /// </summary>
        /// <param name="q2">The target quaternion to interpolate towards.</param>
        /// <param name="t">The interpolation factor, clamped between 0 and 1.</param>
        /// <returns>The interpolated quaternion.</returns>
        public Quaternion Lerp(Quaternion q2, double t)
        {
            t = System.Math.Clamp(t, 0.0, 1.0);

            Quaternion result = new Quaternion(W + t * (q2.W - W),
                VectorPart.X + t * (q2.VectorPart.X - VectorPart.X),
                VectorPart.Y + t * (q2.VectorPart.Y - VectorPart.Y),
                VectorPart.Z + t * (q2.VectorPart.Z - VectorPart.Z)
            );

            return result.Normalize();
        }
    }
}