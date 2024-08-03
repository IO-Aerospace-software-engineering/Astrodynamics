
namespace IO.Astrodynamics.Math
{
    public readonly record struct Quaternion
    {
        public static Quaternion Zero = new (1, 0, 0, 0);
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
    }
}