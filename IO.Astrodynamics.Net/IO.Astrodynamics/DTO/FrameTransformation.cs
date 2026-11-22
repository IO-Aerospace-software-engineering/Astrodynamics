// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Runtime.InteropServices;

namespace IO.Astrodynamics.DTO;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal readonly struct FrameTransformation
{
    public Quaternion Rotation { get; }
    public Vector3D AngularVelocity { get; }
}