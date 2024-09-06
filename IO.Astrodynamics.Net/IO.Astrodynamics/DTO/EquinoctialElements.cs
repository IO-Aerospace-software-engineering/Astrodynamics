using System.Runtime.InteropServices;

namespace IO.Astrodynamics.DTO;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct EquinoctialElements
{
    public double Epoch;
    public int CenterOfMotionId; 
    
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Frame;
    public double SemiMajorAxis;
    public double H;
    public double K;
    public double P;
    public double Q;
    public double L;
    public double PeriapsisLongitudeRate;
    public double RightAscensionOfThePole;
    public double DeclinationOfThePole;
    public double AscendingNodeLongitudeRate;
    public double Period;

    public EquinoctialElements(double epoch, int centerOfMotionId, string frame, double semiMajorAxis, double h, double k, double p, double q, double l, double periapsisLongitudeRate, double rightAscensionOfThePole, double declinationOfThePole, double ascendingNodeLongitudeRate)
    {
        Epoch = epoch;
        CenterOfMotionId = centerOfMotionId;
        Frame = frame;
        SemiMajorAxis = semiMajorAxis;
        H = h;
        K = k;
        P = p;
        Q = q;
        L = l;
        PeriapsisLongitudeRate = periapsisLongitudeRate;
        RightAscensionOfThePole = rightAscensionOfThePole;
        DeclinationOfThePole = declinationOfThePole;
        AscendingNodeLongitudeRate = ascendingNodeLongitudeRate;
    }
}