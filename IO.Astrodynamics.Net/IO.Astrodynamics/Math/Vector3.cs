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
            return System.Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        public Vector3 Normalize()
        {
            return this / Magnitude();
        }

        public Vector3 Cross(in Vector3 vector)
        {
            return new Vector3((Y * vector.Z) - (Z * vector.Y), (Z * vector.X) - (X * vector.Z), (X * vector.Y) - (Y * vector.X));
        }

        public double Angle(in Vector3 vector)
        {
            return System.Math.Acos(this * vector / (Magnitude() * vector.Magnitude()));
        }

        public double Angle(in Vector3 vector, in Plane plane)
        {
            return Angle(vector, plane.Normal);
        }

        public double Angle(in Vector3 vector, in Vector3 plane)
        {
            return double.Atan2(Cross(vector) * plane.Normalize(), this * vector);
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

        public Quaternion To(in Vector3 vector)
        {
            var dot = this * vector;
            var angle = System.Math.Abs(this.Angle(vector));
            if (angle <= double.Epsilon)
            {
                return Quaternion.Zero;
            }
            
            var mag1 = Magnitude();
            var mag2 = vector.Magnitude();
            if (System.Math.Abs(angle-Constants.PI) < double.Epsilon) //Manage 180° case
            {
                double x = System.Math.Abs(vector.X);
                double y = System.Math.Abs(vector.Y);
                double z = System.Math.Abs(vector.Z);

                Vector3 axis = x < y ? (x < z ? VectorX : VectorZ) : (y < z ? VectorY : VectorZ);
                var vec = vector.Cross(axis);
                return new Quaternion(0.0, vec.X, vec.Y, vec.Z).Normalize();
            }

            var v = this.Cross(vector);
            var w = dot + System.Math.Sqrt(mag1 * mag1 * mag2 * mag2);

            return new Quaternion(w, v.X, v.Y, v.Z).Normalize();
        }

        public Vector3 Rotate(in Quaternion quaternion)
        {
            var p = new Quaternion(0.0, this);
            return (quaternion * p * quaternion.Conjugate()).VectorPart;
        }

        public override string ToString()
        {
            return $"X : {X} Y : {Y} Z: {Z}";
        }

        // public Vector3 ToVector()
        // {
        //     return new Vector3(stackalloc double[] { X, Y, Z, 0.0 });
        // }
    }
}