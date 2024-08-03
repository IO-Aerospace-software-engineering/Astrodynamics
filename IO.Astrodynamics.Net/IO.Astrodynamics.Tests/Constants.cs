using System.IO;

namespace IO.Astrodynamics.Tests;

public static class Constants
{
    public const double RAD_DEG = 180.0 / System.Math.PI;
    public const double DEG_RAD = System.Math.PI / 180.0;
    public static readonly DirectoryInfo OutputPath = new("Data/User/");
    public static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");
}