// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Physics;

public class GeopotentialCoefficient : IEquatable<GeopotentialCoefficient>
{
    public ushort N { get; }
    public ushort M { get; }
    public double C { get; }
    public double S { get; }
    public double SigmaC { get; }
    public double SigmaS { get; }
    
    public GeopotentialCoefficient(ushort n, ushort m, double c, double s, double sigmaC, double sigmaS)
    {
        if (m > n)
        {
            throw new ArgumentException("order m cannot be greater than degree n");
        }
        N = n;
        M = m;
        C = c;
        S = s;
        SigmaC = sigmaC;
        SigmaS = sigmaS;
    }
    
    public bool Equals(GeopotentialCoefficient other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return N == other.N && M == other.M && C.Equals(other.C) && S.Equals(other.S) && SigmaC.Equals(other.SigmaC) && SigmaS.Equals(other.SigmaS);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((GeopotentialCoefficient)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(N, M, C, S, SigmaC, SigmaS);
    }

    public static bool operator ==(GeopotentialCoefficient left, GeopotentialCoefficient right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(GeopotentialCoefficient left, GeopotentialCoefficient right)
    {
        return !Equals(left, right);
    }
}