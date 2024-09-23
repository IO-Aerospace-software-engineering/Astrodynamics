// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body;

/// <summary>
/// Represents an interface for orientable celestial objects.
/// </summary>
public interface IOrientable<out T> where T : Frame
{
    /// <summary>
    /// Gets the reference frame of the orientable object.
    /// </summary>
    T Frame { get; }

    /// <summary>
    /// Gets the orientation of the object relative to a reference frame at a specific epoch.
    /// </summary>
    /// <param name="referenceFrame">The reference frame to compare against.</param>
    /// <param name="epoch">The epoch time for the orientation.</param>
    /// <returns>The state orientation of the object.</returns>
    StateOrientation GetOrientation(Frame referenceFrame, in Time epoch);
}