using System.Runtime.InteropServices;

namespace IO.Astrodynamics.DTO;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct KeplerianElements
{
    public int CenterOfMotionId;
    public double Epoch;
    public double PerifocalDistance;
    public double Eccentricity;
    public double Inclination;
    public double AscendingNodeLongitude;
    public double PeriapsisArgument;
    public double MeanAnomaly;
    public double TrueAnomaly;
    public double OrbitalPeriod;
    public double SemiMajorAxis;
    
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Frame;

    public KeplerianElements(int centerOfMotionId, double epoch, double perifocalDistance, double eccentricity, double inclination, double ascendingNodeLongitude, double periapsisArgument, double meanAnomaly,  string frame)
    {
        CenterOfMotionId = centerOfMotionId;
        Epoch = epoch;
        PerifocalDistance = perifocalDistance;
        Eccentricity = eccentricity;
        Inclination = inclination;
        AscendingNodeLongitude = ascendingNodeLongitude;
        PeriapsisArgument = periapsisArgument;
        MeanAnomaly = meanAnomaly;
        Frame = frame;
    }
}