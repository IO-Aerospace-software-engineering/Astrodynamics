// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Runtime.InteropServices;

namespace IO.Astrodynamics.DTO;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public readonly struct TLEElements
{
    public double BalisticCoefficient { get; }
    public double SecondDerivativeOfMeanMotion { get; }
    public double DragTerm { get; }
    public double Epoch { get; }
    public double A { get; }
    public double E { get; }
    public double I { get; }
    public double W { get; }
    public double O { get; }
    public double M { get; }

    public TLEElements(double balisticCoefficient, double secondDerivativeOfMeanMotion, double dragTerm, double epoch, double a, double e, double i, double w, double o, double m)
    {
        BalisticCoefficient = balisticCoefficient;
        SecondDerivativeOfMeanMotion = secondDerivativeOfMeanMotion;
        DragTerm = dragTerm;
        Epoch = epoch;
        A = a;
        E = e;
        I = i;
        W = w;
        O = o;
        M = m;
    }
}