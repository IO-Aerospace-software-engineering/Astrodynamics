using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

public static class UnitConversion
{
    public static double KmToM(double km) => km * 1000.0;
    public static double MToKm(double m) => m / 1000.0;
    public static double KmSToMS(double kmS) => kmS * 1000.0;
    public static double MSToKmS(double mS) => mS / 1000.0;
    public static double DegToRad(double deg) => deg * Constants.Deg2Rad;
    public static double RadToDeg(double rad) => rad * Constants.Rad2Deg;

    /// <summary>
    /// Convert IO.Astrodynamics quaternion (W,X,Y,Z) to conformance scalar-first [w,x,y,z].
    /// Both use scalar-first convention, so this is a direct extraction.
    /// Canonicalizes so W >= 0.
    /// </summary>
    public static double[] QuaternionToArray(Quaternion q)
    {
        double w = q.W;
        double x = q.VectorPart.X;
        double y = q.VectorPart.Y;
        double z = q.VectorPart.Z;

        // Canonicalize: ensure w >= 0
        if (w < 0)
        {
            w = -w;
            x = -x;
            y = -y;
            z = -z;
        }

        return new[] { w, x, y, z };
    }

    /// <summary>
    /// Convert conformance scalar-first [w,x,y,z] to IO.Astrodynamics quaternion.
    /// </summary>
    public static Quaternion QuaternionFromArray(double[] arr)
    {
        return new Quaternion(arr[0], arr[1], arr[2], arr[3]);
    }

    /// <summary>
    /// Canonicalize a scalar-first quaternion array so that w >= 0.
    /// </summary>
    public static double[] CanonicalizeQuaternion(double[] arr)
    {
        if (arr[0] < 0)
        {
            return new[] { -arr[0], -arr[1], -arr[2], -arr[3] };
        }

        return arr;
    }
}
