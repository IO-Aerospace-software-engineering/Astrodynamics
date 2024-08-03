// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Runtime.InteropServices;

namespace IO.Astrodynamics.DTO;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct CelestialBody
{
    public int Id;
    public int CenterOfMotionId;
    public int BarycenterOfMotionId;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Name;

    public Vector3D Radii;
    public double GM;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string FrameName;

    public int FrameId;
    public double J2 = 0.0;
    public double J3 = 0.0;
    public double J4 = 0.0;

    public CelestialBody(int id, int centerOfMotionId, int barycenterOfMotionId, string name, Vector3D radii, double gm, string frameName, int frameId, double j2, double j3,
        double j4)
    {
        Id = id;
        CenterOfMotionId = centerOfMotionId;
        Name = name;
        Radii = radii;
        GM = gm;
        FrameName = frameName;
        FrameId = frameId;
        J2 = j2;
        J3 = j3;
        J4 = j4;
        BarycenterOfMotionId = barycenterOfMotionId;
    }
}