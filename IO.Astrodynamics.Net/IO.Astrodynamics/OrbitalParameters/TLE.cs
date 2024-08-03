// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.SolarSystemObjects;

namespace IO.Astrodynamics.OrbitalParameters;

public class TLE : KeplerianElements, IEquatable<TLE>
{
    public string Line1 { get; }
    public string Line2 { get; }
    public string Line3 { get; }
    public double BalisticCoefficient { get; }
    public double DragTerm { get; }
    public double SecondDerivativeMeanMotion { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="line1"></param>
    /// <param name="line2"></param>
    /// <param name="line3"></param>
    /// <returns></returns>
    public static TLE Create(string line1, string line2, string line3)
    {
        if (string.IsNullOrEmpty(line1)) throw new ArgumentException("Value cannot be null or empty.", nameof(line1));
        if (string.IsNullOrEmpty(line2)) throw new ArgumentException("Value cannot be null or empty.", nameof(line2));
        if (string.IsNullOrEmpty(line3)) throw new ArgumentException("Value cannot be null or empty.", nameof(line3));
        return API.Instance.CreateTLE(line1, line2, line3);
    }

    internal TLE(string line1, string line2, string line3, double balisticCoefficient, double dragTerm, double secondDerivativeMeanMotion, double a, double e, double i, double o,
        double w, double m, DateTime epoch, Frame frame) : base(a, e, i, o, w, m, new CelestialBody(PlanetsAndMoons.EARTH), epoch, frame)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));
        if (string.IsNullOrEmpty(line1)) throw new ArgumentException("Value cannot be null or empty.", nameof(line1));
        if (string.IsNullOrEmpty(line2)) throw new ArgumentException("Value cannot be null or empty.", nameof(line2));
        if (string.IsNullOrEmpty(line3)) throw new ArgumentException("Value cannot be null or empty.", nameof(line3));
        Line1 = line1;
        Line2 = line2;
        Line3 = line3;
        BalisticCoefficient = balisticCoefficient;
        DragTerm = dragTerm;
        SecondDerivativeMeanMotion = secondDerivativeMeanMotion;
    }

    public override OrbitalParameters AtEpoch(DateTime epoch)
    {
        return ToStateVector(epoch);
    }
    public override StateVector ToStateVector(DateTime epoch)
    {
        return API.Instance.ConvertTleToStateVector(Line1, Line2, Line3, epoch);
    }
    
    public bool Equals(TLE other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return base.Equals(other) && Line1 == other.Line1 && Line2 == other.Line2 && Line3 == other.Line3;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((TLE)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Line1, Line2, Line3);
    }

    public static bool operator ==(TLE left, TLE right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(TLE left, TLE right)
    {
        return !Equals(left, right);
    }
}