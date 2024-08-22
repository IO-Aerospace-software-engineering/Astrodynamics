using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body;

public class LagrangePoint : CelestialItem
{
    public LagrangePoint(NaifObject systemObject) : this(systemObject, TimeSystem.Time.J2000TDB)
    {
        this.InitialOrbitalParameters = GetEphemeris(TimeSystem.Time.J2000TDB, new Barycenter(Barycenters.EARTH_BARYCENTER.NaifId), Frame.ECLIPTIC_J2000, Aberration.None);
    }

    public LagrangePoint(NaifObject systemObject, Time epoch) : base(systemObject.NaifId, systemObject.Name, 0.0, null)
    {
        this.InitialOrbitalParameters = GetEphemeris(epoch, new Barycenter(Barycenters.EARTH_BARYCENTER.NaifId), Frame.ECLIPTIC_J2000, Aberration.None);
    }
}