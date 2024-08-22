using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body;

public class Barycenter : CelestialItem
{
    public Barycenter(int naifId) : this(naifId,  TimeSystem.Time.J2000TDB)
    {
    }

    public Barycenter(int naifId, Time epoch) : base(naifId, Frame.ECLIPTIC_J2000, epoch)
    {
    }
}