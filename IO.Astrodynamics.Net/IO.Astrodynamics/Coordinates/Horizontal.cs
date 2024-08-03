using System;

namespace IO.Astrodynamics.Coordinates
{
    public readonly record struct Horizontal(double Azimuth, double Elevation, double Range, DateTime Epoch);
}