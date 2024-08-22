using System;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Coordinates
{
    public readonly record struct Horizontal(double Azimuth, double Elevation, double Range, Time Epoch);
}