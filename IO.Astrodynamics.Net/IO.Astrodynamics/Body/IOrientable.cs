// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Body;

public interface IOrientable
{
    Frame Frame { get; }

    /// <summary>
    /// Get orientation relative to reference frame
    /// </summary>
    /// <param name="referenceFrame"></param>
    /// <param name="epoch"></param>
    /// <returns></returns>
    StateOrientation GetOrientation(Frame referenceFrame, in DateTime epoch);
}