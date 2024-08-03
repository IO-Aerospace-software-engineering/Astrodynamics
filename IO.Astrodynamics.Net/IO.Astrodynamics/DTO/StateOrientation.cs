// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Runtime.InteropServices;

namespace IO.Astrodynamics.DTO;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct StateOrientation
{
    public Quaternion Rotation;
    public Vector3D AngularVelocity;
    public double Epoch;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Frame;

    public StateOrientation(Quaternion orientation, Vector3D angularVelocity, double epoch, string frame)
    {
        Rotation = orientation;
        AngularVelocity = angularVelocity;
        Epoch = epoch;
        Frame = frame;
    }
}