using System;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Frames;

public class Frame : IEquatable<Frame>
{
    public string Name { get; }
    public int? Id { get; }

    public static readonly Frame ICRF = new Frame("J2000");
    public static readonly Frame ECLIPTIC_B1950 = new Frame("ECLIPB1950");
    public static readonly Frame ECLIPTIC_J2000 = new Frame("ECLIPJ2000");
    public static readonly Frame GALACTIC_SYSTEM2 = new Frame("GALACTIC");
    public static readonly Frame B1950 = new Frame("B1950");
    public static readonly Frame FK4 = new Frame("FK4");

    public Frame(string name, int? id = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Frame must have a name");
        }

        Name = name;
        Id = id;
    }

    public StateOrientation ToFrame(Frame frame, DateTime epoch)
    {
        return API.Instance.TransformFrame(this, frame, epoch);
    }

    public override string ToString()
    {
        return Name;
    }

    public bool Equals(Frame other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Frame)obj);
    }

    public override int GetHashCode()
    {
        return (Name != null ? Name.GetHashCode() : 0);
    }

    public static bool operator ==(Frame left, Frame right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Frame left, Frame right)
    {
        return !Equals(left, right);
    }
}