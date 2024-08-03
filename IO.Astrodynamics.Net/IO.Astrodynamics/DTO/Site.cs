// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Runtime.InteropServices;

namespace IO.Astrodynamics.DTO;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct Site
{
    
    public int Id = 0;
    public string Name = null;
    public int BodyId = -1;

    public Planetodetic Coordinates = default;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
    public AzimuthRange[] Ranges;

    public string DirectoryPath = null;

    public Site(int naifId, int bodyId, Planetodetic coordinates, string name, string directoryPath) : this()
    {
        Id = naifId;
        BodyId = bodyId;
        Coordinates = coordinates;
        Name = name;
        DirectoryPath = directoryPath;
        Ranges = ArrayBuilder.ArrayOf<AzimuthRange>(10);
    }
}