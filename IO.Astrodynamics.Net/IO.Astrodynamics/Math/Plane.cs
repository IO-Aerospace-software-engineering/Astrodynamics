namespace IO.Astrodynamics.Math;

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

    public double GetAngle(Plane plane)
    {
        return System.Math.Acos((Normal * plane.Normal) / (Normal.Magnitude() * plane.Normal.Magnitude()));
    }

    public double GetAngle(Vector3 vector)
    {
        return System.Math.Asin((Normal * vector) / (Normal.Magnitude() * vector.Magnitude()));
    }
}