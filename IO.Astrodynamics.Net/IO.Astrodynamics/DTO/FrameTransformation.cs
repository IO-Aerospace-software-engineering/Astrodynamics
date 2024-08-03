// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Runtime.InteropServices;

namespace IO.Astrodynamics.DTO;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public readonly struct FrameTransformation
{
    public Quaternion Rotation { get; }
    public Vector3D AngularVelocity { get; }
}