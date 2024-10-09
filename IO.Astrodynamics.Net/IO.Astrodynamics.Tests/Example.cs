using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests;

public class Example
{
    [Fact]
    public void ReadEphemeris()
    {
        //In this example we want the ephemeris of the moon at epoch (2000-01-01T12:00Z) in ICRF frame with earth at center
        API.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));//replace Data/SolarSystem by your kernel path
        var earth = PlanetsAndMoons.EARTH_BODY;
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var ephemeris = moon.GetEphemeris(new TimeSystem.Time(new DateTime(2000, 1, 1, 12, 0, 0), TimeFrame.UTCFrame), earth, Frames.Frame.ICRF, Aberration.None);
    }
}