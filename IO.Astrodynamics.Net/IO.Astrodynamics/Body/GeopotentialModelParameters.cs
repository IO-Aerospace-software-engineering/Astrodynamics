// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.IO;

namespace IO.Astrodynamics.Body;

public class GeopotentialModelParameters
{
    public GeopotentialModelParameters(string geopotentialModelPath, ushort geopotentialDegree = 60) : this(
        new FileStream(geopotentialModelPath, FileMode.Open, FileAccess.Read, FileShare.Read),
        geopotentialDegree)
    {
    }

    public GeopotentialModelParameters(Stream geopotentialModelStream, ushort geopotentialDegree = 60)
    {
        GeopotentialModelPath = new StreamReader(geopotentialModelStream);
        GeopotentialDegree = geopotentialDegree;
    }

    public StreamReader GeopotentialModelPath { get; }
    public ushort GeopotentialDegree { get; }
}