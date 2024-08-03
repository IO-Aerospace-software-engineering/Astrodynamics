// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Runtime.InteropServices;

namespace IO.Astrodynamics.DTO;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct AzimuthRange
{
    public double Start { get; }
    public double End { get; }

    public AzimuthRange(double start, double end)
    {
        Start = start;
        End = end;
    }
}