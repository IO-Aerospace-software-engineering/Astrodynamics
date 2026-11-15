using System;

namespace IO.Astrodynamics.Math;

/// <summary>
/// A plane of normal <see cref="Normal"/> at <see cref="Distance"/> from the origin. The constructors take the normal as it
/// is, unchecked; <c>default(Plane)</c> and <c>new Plane()</c> have the zero normal, which orients no plane, so the angles
/// throw for it.
/// </summary>
public record struct Plane
{
    public Vector3 Normal { get; }
    public double Distance { get; }

    public static Plane X { get; } = new Plane(Vector3.VectorX);
    public static Plane Y { get; } = new Plane(Vector3.VectorY);
    public static Plane Z { get; } = new Plane(Vector3.VectorZ);

    public Plane(Vector3 normal) : this(normal, 0.0)
    {
    }

    public Plane(Vector3 normal, double distance)
    {
        Normal = normal;
        Distance = distance;
    }

    /// <summary>
    /// The angle between the normals of this plane and of <paramref name="plane"/>, in [0, π]: acos((n1 · n2) / (|n1| |n2|)).
    /// </summary>
    /// <remarks>
    /// The quotient is clamped to [-1, 1], so parallel and antiparallel normals give 0 and π, near which acos is
    /// sensitive (about √(2ε), 2.1e-8 rad); normals outside about 1e-150 to 1e150 are compared through their unit vectors,
    /// scaled exactly by a power of two (third round of #668: NaN for about a quarter of parallel normals, NaN or 1.3e-4
    /// rad off for those normals, before 11.0.0). Every other result is bit for bit the former one.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The normal of this plane is zero or has a component that is not finite
    /// (<c>default(Plane)</c> included): it has no orientation (NaN before 11.0.0).</exception>
    /// <exception cref="ArgumentException">The normal of <paramref name="plane"/> is zero or has a component that is not
    /// finite (NaN before 11.0.0).</exception>
    public double GetAngle(Plane plane)
    {
        RequireOrientation();
        if (!Vector3.IsFiniteNonZero(plane.Normal))
        {
            throw new ArgumentException($"The normal {plane.Normal} of the plane is zero or not finite, and orients no plane.", nameof(plane));
        }

        return System.Math.Acos(Vector3.Cosine(Normal, plane.Normal));
    }

    /// <summary>
    /// The angle of <paramref name="vector"/> to this plane, in [-π/2, π/2], positive on the side of its normal:
    /// asin((n · v) / (|n| |v|)).
    /// </summary>
    /// <remarks>
    /// The quotient is clamped to [-1, 1], so a vector along the normal gives ±π/2; vectors outside about 1e-150 to 1e150
    /// are compared through their unit vectors, scaled exactly by a power of two (third round of #668: NaN for about a
    /// quarter of vectors parallel to the normal, NaN or an error for those vectors, before 11.0.0). Every other result is
    /// bit for bit the former one.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The normal of this plane is zero or has a component that is not finite
    /// (<c>default(Plane)</c> included): it has no orientation (NaN before 11.0.0).</exception>
    /// <exception cref="ArgumentException"><paramref name="vector"/> is zero or has a component that is not finite: its
    /// angle to a plane is undefined (NaN before 11.0.0).</exception>
    public double GetAngle(Vector3 vector)
    {
        RequireOrientation();
        if (!Vector3.IsFiniteNonZero(vector))
        {
            throw new ArgumentException($"The vector {vector} is zero or not finite, and has no angle to a plane.", nameof(vector));
        }

        return System.Math.Asin(Vector3.Cosine(Normal, vector));
    }

    private void RequireOrientation()
    {
        if (!Vector3.IsFiniteNonZero(Normal))
        {
            throw new InvalidOperationException($"The normal {Normal} of this plane is zero or not finite, and orients no plane.");
        }
    }
}
