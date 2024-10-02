using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body;

/// <summary>
/// Represents a Lagrange point in a celestial system.
/// </summary>
public class LagrangePoint : CelestialItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LagrangePoint"/> class with the specified system object and a default epoch of J2000TDB.
    /// </summary>
    /// <param name="systemObject">The NAIF object representing the celestial system.</param>
    public LagrangePoint(NaifObject systemObject) : this(systemObject, TimeSystem.Time.J2000TDB)
    {
       
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LagrangePoint"/> class with the specified system object and epoch.
    /// </summary>
    /// <param name="systemObject">The NAIF object representing the celestial system.</param>
    /// <param name="epoch">The epoch time for the Lagrange point.</param>
    public LagrangePoint(NaifObject systemObject, Time epoch) : base(systemObject.NaifId, systemObject.Name, 0.0, null)
    {
        this.InitialOrbitalParameters = GetEphemeris(epoch, Barycenters.EARTH_BARYCENTER, Frame.ECLIPTIC_J2000, Aberration.None);
        BarycenterOfMotionId = 3;
        CenterOfMotionId = 10;
    }
}