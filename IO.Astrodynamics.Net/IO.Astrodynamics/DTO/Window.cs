// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Runtime.InteropServices;

namespace IO.Astrodynamics.DTO;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct Window
{
    public double Start { get; }
    public double End { get; }

    public Window(double start, double end)
    {
        Start = start;
        End = end;
    }
}