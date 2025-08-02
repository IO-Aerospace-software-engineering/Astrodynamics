// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

public class TLETests
{
    public TLETests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Create()
    {
        TLE tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        Assert.ThrowsAny<Exception>(() => new TLE("",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"));
        Assert.ThrowsAny<Exception>(() => new TLE("ISS",
            "",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"));
        Assert.ThrowsAny<Exception>(() => new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            ""));

        Assert.Equal(6796882.0093704751, tle.SemiMajorAxis(), 6);
        Assert.Equal(4.9299999999999999e-05, tle.Eccentricity(), 6);
        Assert.Equal(0.9013281683026676, tle.Inclination(), 6);
        Assert.Equal(6.1615568022666061, tle.AscendingNode(), 6);
        Assert.Equal(5.6003339639830649, tle.ArgumentOfPeriapsis(), 6);
        Assert.Equal(0.68479738531249512, tle.MeanAnomaly(), 6);
        Assert.Equal(664419082.84759152, tle.Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds, 6);
        Assert.Equal(0.00016716999999999999, tle.BalisticCoefficient, 6);
        Assert.Equal(0.0001027, tle.DragTerm, 6);
        Assert.Equal(0.0, tle.SecondDerivativeMeanMotion, 6);
    }

    [Fact]
    public void ToStateVector()
    {
        TLE tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        TimeSystem.Time epoch = TimeSystem.Time.CreateTDB(664440682.84760022);
        var stateVector = tle.AtEpoch(epoch).ToStateVector();

        Assert.Equal(stateVector, tle.ToStateVector(epoch));
        Assert.Equal(4339206.6119421758, stateVector.Position.X, 3);
        Assert.Equal(-3648070.2129484829, stateVector.Position.Y, 3);
        Assert.Equal(-3756119.7658792436, stateVector.Position.Z, 3);
        Assert.Equal(5826.3276055659326, stateVector.Velocity.X, 3);
        Assert.Equal(2548.7270427712078, stateVector.Velocity.Y, 3);
        Assert.Equal(4259.876355263199, stateVector.Velocity.Z, 3);
        Assert.Equal("J2000", stateVector.Frame.Name);
        Assert.Equal(399, stateVector.Observer.NaifId);
        Assert.Equal(664440682.84760022, stateVector.Epoch.TimeSpanFromJ2000().TotalSeconds, 3);
    }

    [Fact]
    public void Observation()
    {
        TLE tle = new TLE("CZ-3C DEB", "1 39348U 10057N   24238.91466777  .00000306  00000-0  19116-2 0  9995",
            "2 39348  20.0230 212.2863 7218258 312.9449   5.6833  2.25781763 89468");

        TimeSystem.Time epoch = new TimeSystem.Time("2024-08-26T22:34:20.00000Z");
        var stateVector = tle.ToStateVector(epoch);

        Assert.Equal(32718528.303724434, stateVector.Position.X, 1);
        Assert.Equal(-17501136.957387105, stateVector.Position.Y, 1);
        Assert.Equal(11592997.485773511, stateVector.Position.Z, 1);
        Assert.Equal(1808.0437338563306, stateVector.Velocity.X, 1);
        Assert.Equal(998.49491137687698, stateVector.Velocity.Y, 1);
        Assert.Equal(29.876025979417708, stateVector.Velocity.Z, 1);
        Assert.Equal("J2000", stateVector.Frame.Name);
        Assert.Equal(399, stateVector.Observer.NaifId);
        Assert.Equal(777983660, stateVector.Epoch.TimeSpanFromJ2000().TotalSeconds, 3);

        Site site = new Site(14, "SiteA", TestHelpers.EarthAtJ2000, new Planetodetic(19.89367 * Astrodynamics.Constants.Deg2Rad, 47.91748 * Astrodynamics.Constants.Deg2Rad, 984));
        var eq = stateVector.RelativeTo(site, Aberration.None).ToEquatorial();

        double ra = eq.RightAscension * Astrodynamics.Constants.Rad2Deg; //So, 331.591° is approximately 22 hours, 6 minutes, and 21.93 seconds
        double dec = eq.Declination * Astrodynamics.Constants.Rad2Deg; //So, 11.859° is approximately 11°51'32.4"

        //SkyField results
        double raSkyField = 331.59;
        double decSkyField = 11.859;

        //Observation results
        double raObs = 331.5980;
        double decObs = 11.8474;


        //Delta relative to observation
        double deltaRAObs = System.Math.Abs(ra - raObs);
        double deltaDecObs = System.Math.Abs(dec - decObs);

        //Delta relative to observation from skyfield
        double deltaRASkyFieldObs = System.Math.Abs(raSkyField - raObs);
        double deltaDecSkyFieldObs = System.Math.Abs(decSkyField - decObs);
        Assert.True(deltaRAObs < deltaRASkyFieldObs);
        Assert.True(deltaDecObs < deltaDecSkyFieldObs);
    }

    [Fact]
    public void Equality()
    {
        TLE tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        TLE tle2 = new TLE("ISS2",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        Assert.NotEqual(tle, tle2);
        Assert.True(tle != tle2);
        Assert.False(tle == tle2);
        Assert.False(tle.Equals(tle2));
        Assert.False(tle.Equals(null));
        Assert.True(tle.Equals(tle));
        Assert.False(tle.Equals((object)tle2));
        Assert.False(tle.Equals((object)null));
        Assert.True(tle.Equals((object)tle));
    }

    [Fact]
    public void InvalidTLE()
    {
        Assert.Throws<InvalidDataException>(() => new TLE("ISS",
            "1 25544U 98067A   21021.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"));
    }

    [Fact]
    public void InvalidLine1()
    {
        Assert.ThrowsAny<Exception>(() => new TLE("ISS",
            "",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"));
    }

    [Fact]
    public void InvalidLine2()
    {
        Assert.ThrowsAny<Exception>(() => new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            ""));
    }

    [Fact]
    public void InvalidName()
    {
        Assert.Throws<InvalidDataException>(() => new TLE("",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"));
    }

    [Fact]
    public void CreateFromKeplerian()
    {
        TLE tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var kep = tle.ToKeplerianElements();

        var newtle = TLE.Create(kep, "ISS", 25544, "98067A", 2570, Classification.Unclassified, 0.0010270, 0.00016717, elementSetNumber: 905);
        Assert.Equal(tle.Name, newtle.Name);
        Assert.Equal(tle.Line1, newtle.Line1);
        Assert.Equal(tle.Line2, newtle.Line2);
    }


    [Fact]
    public void Create_InvalidCosparId_ThrowsArgumentException()
    {
        var kep = new KeplerianElements(0.1, 0.2, 0.3, 0.4, 0.5, 0.6, TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);
        string name = "TestSatellite";
        ushort noradId = 12345;
        string cosparId = "123";
        ushort revolutionsAtEpoch = 904;

        Assert.Throws<ArgumentException>(() => TLE.Create(kep, name, noradId, cosparId, revolutionsAtEpoch));
    }

    [Fact]
    public void Create_InvalidElementSetNumber_ThrowsArgumentOutOfRangeException()
    {
        var kep = new KeplerianElements(0.1, 0.2, 0.3, 0.4, 0.5, 0.6, TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);
        string name = "TestSatellite";
        ushort noradId = 12345;
        string cosparId = "98067A";
        ushort revolutionsAtEpoch = 904;

        Assert.Throws<ArgumentOutOfRangeException>(() => TLE.Create(kep, name, noradId, cosparId, revolutionsAtEpoch, Classification.Unclassified, 0.0001, 0.0, 0.0, 10000));
    }

    [Fact]
    public void Create_InvalidKeplerianElements_ThrowsNullArgumentException()
    {
        string name = "TestSatellite";
        ushort noradId = 12345;
        string cosparId = "98067A";
        ushort revolutionsAtEpoch = 904;

        Assert.Throws<ArgumentNullException>(() => TLE.Create(null, name, noradId, cosparId, revolutionsAtEpoch, Classification.Unclassified, 0.0001, 0.0, 0.0, 10000));
    }

    [Fact]
    public void Create_InvalidName_ThrowsNullArgumentException()
    {
        var kep = new KeplerianElements(0.1, 0.2, 0.3, 0.4, 0.5, 0.6, TestHelpers.EarthAtJ2000, new TimeSystem.Time(DateTime.UtcNow, TimeFrame.UTCFrame), Frames.Frame.ICRF);
        string name = "";
        ushort noradId = 12345;
        string cosparId = "98067A";
        ushort revolutionsAtEpoch = 904;

        Assert.Throws<ArgumentOutOfRangeException>(() => TLE.Create(kep, name, noradId, cosparId, revolutionsAtEpoch, Classification.Unclassified, 0.0001, 0.0, 0.0, 10000));
    }

    [Fact]
    public void ToTLE_ValidOrbit_ReturnsValidTLE()
    {
        var epoch = new TimeSystem.Time(new DateTime(2024, 1, 1), TimeFrame.UTCFrame);
        var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), TestHelpers.EarthAtJ2000, epoch, Frames.Frame.ICRF);
        var config = new Astrodynamics.OrbitalParameters.TLE.Configuration(25666, "TestSatellite", "98067A");
        var tle = sv.ToTLE(config);
        Assert.NotNull(tle);
        Assert.Equal("TestSatellite", tle.Name);
        var computedSV = tle.ToStateVector();
        Assert.Equal(sv.Position.X, computedSV.Position.X, (x, y) => System.Math.Abs(x - y) < 1);
        Assert.Equal(sv.Position.Y, computedSV.Position.Y, (x, y) => System.Math.Abs(x - y) < 2);
        Assert.Equal(sv.Position.Z, computedSV.Position.Z, (x, y) => System.Math.Abs(x - y) < 6);
        Assert.Equal(sv.Velocity.X, computedSV.Velocity.X, (x, y) => System.Math.Abs(x - y) < 1e-2);
        Assert.Equal(sv.Velocity.Y, computedSV.Velocity.Y, (x, y) => System.Math.Abs(x - y) < 1e-3);
        Assert.Equal(sv.Velocity.Z, computedSV.Velocity.Z, (x, y) => System.Math.Abs(x - y) < 1e-3);
        Assert.Equal(sv.Epoch.TimeSpanFromJ2000().TotalSeconds, computedSV.Epoch.TimeSpanFromJ2000().TotalSeconds, 3);
        Assert.Equal(sv.Frame.Name, computedSV.Frame.Name);
        Assert.Equal(sv.Observer.NaifId, computedSV.Observer.NaifId);
    }
}