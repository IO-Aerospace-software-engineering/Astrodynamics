using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.Body;

public class LagrangePoint : CelestialItem
{
    public LagrangePoint(NaifObject systemObject) : this(systemObject, DateTimeExtension.J2000)
    {
        this.InitialOrbitalParameters = GetEphemeris(DateTimeExtension.J2000, new Barycenter(Barycenters.EARTH_BARYCENTER.NaifId), Frame.ECLIPTIC_J2000, Aberration.None);
    }

    public LagrangePoint(NaifObject systemObject, DateTime epoch) : base(systemObject.NaifId, systemObject.Name, 0.0, null)
    {
        this.InitialOrbitalParameters = GetEphemeris(epoch, new Barycenter(Barycenters.EARTH_BARYCENTER.NaifId), Frame.ECLIPTIC_J2000, Aberration.None);
    }
}