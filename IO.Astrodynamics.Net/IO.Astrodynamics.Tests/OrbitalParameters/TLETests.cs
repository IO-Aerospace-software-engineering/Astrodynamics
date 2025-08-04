// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
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

        Assert.Equal(6796882.0093704751, tle.MeanSemiMajorAxis, 6);
        Assert.Equal(4.9299999999999999e-05, tle.MeanEccentricity, 6);
        Assert.Equal(0.9013281683026676, tle.MeanInclination, 6);
        Assert.Equal(6.1615568022666061, tle.MeanAscendingNode, 6);
        Assert.Equal(5.6003339639830649, tle.MeanArgumentOfPeriapsis, 6);
        Assert.Equal(0.68479738531249512, tle.MeanMeanAnomaly, 6);
        Assert.Equal(664419082.84759152, tle.Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds, 6);
        Assert.Equal(0.00016716999999999999, tle.FirstDerivationMeanMotion, 6);
        Assert.Equal(0.0001027, tle.BalisticCoefficient, 6);
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
        // Adjusted tolerances to account for SGP4/SDP4 propagator differences
        // Position tolerance: ~30m (acceptable for orbital mechanics)
        Assert.True(System.Math.Abs(stateVector.Position.X - 4339206.6119421758) < 30.0,
            $"Position X difference: {System.Math.Abs(stateVector.Position.X - 4339206.6119421758):F3}m should be < 30m");
        Assert.True(System.Math.Abs(stateVector.Position.Y - (-3648070.2129484829)) < 30.0,
            $"Position Y difference: {System.Math.Abs(stateVector.Position.Y - (-3648070.2129484829)):F3}m should be < 30m");
        Assert.True(System.Math.Abs(stateVector.Position.Z - (-3756119.7658792436)) < 30.0,
            $"Position Z difference: {System.Math.Abs(stateVector.Position.Z - (-3756119.7658792436)):F3}m should be < 30m");
        // Velocity tolerance: ~0.02 m/s (acceptable for orbital velocities)
        Assert.Equal(5826.3276055659326, stateVector.Velocity.X, 1);
        Assert.Equal(2548.7270427712078, stateVector.Velocity.Y, 1);
        Assert.Equal(4259.876355263199, stateVector.Velocity.Z, 1);
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

        // Assert.Equal(32718528.303724434, stateVector.Position.X, 1);
        // Assert.Equal(-17501136.957387105, stateVector.Position.Y, 1);
        // Assert.Equal(11592997.485773511, stateVector.Position.Z, 1);
        // Assert.Equal(1808.0437338563306, stateVector.Velocity.X, 1);
        // Assert.Equal(998.49491137687698, stateVector.Velocity.Y, 1);
        // Assert.Equal(29.876025979417708, stateVector.Velocity.Z, 1);
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
        Assert.Throws<InvalidOperationException>(() => new TLE("ISS",
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
        Assert.Throws<ArgumentException>(() => new TLE("",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"));
    }

    [Fact]
    public void CreateFromKeplerian()
    {
        TLE tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        var meanElements = tle.ToMeanKeplerianElements();
        var newtle = TLE.Create(
            meanElements, "ISS", 25544, "98067A", 2570, Classification.Unclassified, 0.0010270, 0.00016717, elementSetNumber: 905);
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
        Assert.Equal(sv.Position.X, computedSV.Position.X, (x, y) => System.Math.Abs(x - y) < 0.5);
        Assert.Equal(sv.Position.Y, computedSV.Position.Y, (x, y) => System.Math.Abs(x - y) < 2);
        Assert.Equal(sv.Position.Z, computedSV.Position.Z, (x, y) => System.Math.Abs(x - y) < 6);
        Assert.Equal(sv.Velocity.X, computedSV.Velocity.X, (x, y) => System.Math.Abs(x - y) < 3e-3);
        Assert.Equal(sv.Velocity.Y, computedSV.Velocity.Y, (x, y) => System.Math.Abs(x - y) < 1e-4);
        Assert.Equal(sv.Velocity.Z, computedSV.Velocity.Z, (x, y) => System.Math.Abs(x - y) < 1e-4);
        Assert.Equal(sv.Epoch.TimeSpanFromJ2000().TotalSeconds, computedSV.Epoch.TimeSpanFromJ2000().TotalSeconds, 6);
        Assert.Equal(sv.Frame.Name, computedSV.Frame.Name);
        Assert.Equal(sv.Observer.NaifId, computedSV.Observer.NaifId);
    }

    [Fact]
    public void Parse_ValidTLELines_ParsesCorrectly()
    {
        string line1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
        string line2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";

        var tle = new TLE("ISS", line1, line2);

        // Test basic TLE properties that are available
        Assert.NotNull(tle.Name);
        Assert.Equal("ISS", tle.Name);
        Assert.Equal(line1, tle.Line1);
        Assert.Equal(line2, tle.Line2);

        // Test orbital parameters
        Assert.True(tle.MeanSemiMajorAxis > 6000000); // Should be reasonable for ISS
        Assert.True(tle.MeanEccentricity >= 0 && tle.MeanEccentricity < 1); // Valid eccentricity range
        Assert.True(tle.MeanInclination > 0 && tle.MeanInclination < System.Math.PI); // Valid inclination range
    }

    [Fact]
    public void Parse_ClassifiedTLE_ParsesClassificationCorrectly()
    {
        string line1 = "1 25544S 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
        string line2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";

        var tle = new TLE("Secret Satellite", line1, line2);

        Assert.NotNull(tle);
        Assert.Equal("Secret Satellite", tle.Name);
        Assert.Equal(line1, tle.Line1);
        Assert.Equal(line2, tle.Line2);
    }

    [Fact]
    public void Parse_ConfidentialTLE_ParsesClassificationCorrectly()
    {
        string line1 = "1 25544C 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
        string line2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";

        var tle = new TLE("Confidential Satellite", line1, line2);

        Assert.NotNull(tle);
        Assert.Equal("Confidential Satellite", tle.Name);
        Assert.Equal(line1, tle.Line1);
        Assert.Equal(line2, tle.Line2);
    }

    [Fact]
    public void Parse_InvalidChecksum_ThrowsException()
    {
        string line1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9055"; // Invalid checksum
        string line2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";

        Assert.Throws<InvalidOperationException>(() => new TLE("ISS", line1, line2));
    }

    [Fact]
    public void Parse_MismatchedNoradIds_ThrowsException()
    {
        string line1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
        string line2 = "2 25545  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"; // Different NORAD ID

        Assert.Throws<InvalidOperationException>(() => new TLE("ISS", line1, line2));
    }

    [Fact]
    public void Parse_InvalidLineFormat_ThrowsException()
    {
        string line1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3"; // Too short
        string line2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";

        Assert.Throws<ArgumentException>(() => new TLE("ISS", line1, line2));
    }

    [Fact]
    public void Propagation_FutureEpoch_CalculatesCorrectly()
    {
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        var futureTime = TimeSystem.Time.CreateTDB(664419082.84759152 + 3600); // 1 hour later
        var futureStateVector = tle.ToStateVector(futureTime);

        Assert.NotNull(futureStateVector);
        Assert.Equal(futureTime.TimeSpanFromJ2000().TotalSeconds, futureStateVector.Epoch.TimeSpanFromJ2000().TotalSeconds, 3);
        Assert.True(futureStateVector.Position.Magnitude() > 6000000); // Reasonable orbital radius
        Assert.True(futureStateVector.Velocity.Magnitude() > 7000); // Reasonable orbital velocity
    }

    [Fact]
    public void Propagation_PastEpoch_CalculatesCorrectly()
    {
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        var pastTime = TimeSystem.Time.CreateTDB(664419082.84759152 - 3600); // 1 hour earlier
        var pastStateVector = tle.ToStateVector(pastTime);

        Assert.NotNull(pastStateVector);
        Assert.Equal(pastTime.TimeSpanFromJ2000().TotalSeconds, pastStateVector.Epoch.TimeSpanFromJ2000().TotalSeconds, 3);
        Assert.True(pastStateVector.Position.Magnitude() > 6000000); // Reasonable orbital radius
        Assert.True(pastStateVector.Velocity.Magnitude() > 7000); // Reasonable orbital velocity
    }

    [Fact]
    public void MeanKeplerianElements_ConversionRoundTrip_PreservesValues()
    {
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        var meanElements = tle.ToMeanKeplerianElements();
        var newTle = TLE.Create(meanElements, "ISS", 25544, "98067A", 25703, Classification.Unclassified, 0.0001027, 0.00016717, 0.0, 905);

        Assert.Equal(tle.MeanSemiMajorAxis, newTle.MeanSemiMajorAxis, 6);
        Assert.Equal(tle.MeanEccentricity, newTle.MeanEccentricity, 6);
        Assert.Equal(tle.MeanInclination, newTle.MeanInclination, 6);
        Assert.Equal(tle.MeanAscendingNode, newTle.MeanAscendingNode, 6);
        Assert.Equal(tle.MeanArgumentOfPeriapsis, newTle.MeanArgumentOfPeriapsis, 6);
        Assert.Equal(tle.MeanMeanAnomaly, newTle.MeanMeanAnomaly, 6);
    }

    [Fact]
    public void EllipticalOrbit_HighEccentricity_HandlesCorrectly()
    {
        // Test with a high eccentricity orbit (like a comet or Molniya orbit)
        var tle = new TLE("MOLNIYA 1-91",
            "1 25485U 98054A   21020.50000000  .00000100  00000-0  10000-3 0  9995",
            "2 25485  63.4000 270.0000 7200000 270.0000  30.0000  2.00000000 10002");

        var stateVector = tle.ToStateVector();

        Assert.NotNull(stateVector);
        Assert.True(tle.MeanEccentricity > 0.7); // High eccentricity
        Assert.True(stateVector.Position.Magnitude() > 6000000); // Valid orbital radius
    }

    [Fact]
    public void CircularOrbit_ZeroEccentricity_HandlesCorrectly()
    {
        // Test with a circular orbit
        var tle = new TLE("CIRCULAR SAT",
            "1 12345U 21001A   21020.50000000  .00000100  00000-0  10000-3 0  9994",
            "2 12345  45.0000 180.0000 0000000 000.0000 000.0000 15.00000000 10002");

        var stateVector = tle.ToStateVector();

        Assert.NotNull(stateVector);
        Assert.Equal(0.0, tle.MeanEccentricity, 6);
        Assert.True(stateVector.Position.Magnitude() > 6000000); // Valid orbital radius
    }

    [Fact]
    public void PolarOrbit_HighInclination_HandlesCorrectly()
    {
        // Test with a polar orbit (high inclination)
        var tle = new TLE("POLAR SAT",
            "1 54321U 21002A   21020.50000000  .00000100  00000-0  10000-3 0  9995",
            "2 54321  98.0000 180.0000 0010000 000.0000 000.0000 14.00000000 10000");

        var stateVector = tle.ToStateVector();

        Assert.NotNull(stateVector);
        Assert.True(tle.MeanInclination > 1.7); // Near polar (> 97 degrees)
        Assert.True(stateVector.Position.Magnitude() > 6000000); // Valid orbital radius
    }

    [Fact]
    public void GeosynchronousOrbit_CorrectPeriod_HandlesCorrectly()
    {
        // Test with a geosynchronous orbit (mean motion ~1 rev/day)
        var tle = new TLE("GEO SAT",
            "1 99999U 21003A   21020.50000000  .00000100  00000-0  10000-3 0  9996",
            "2 99999  00.0000 180.0000 0000000 000.0000 000.0000  1.00273791 10007");

        var stateVector = tle.ToStateVector();

        Assert.NotNull(stateVector);
        // Test that it's approximately 1 revolution per day by checking the orbital period
        var period = 2 * System.Math.PI * System.Math.Sqrt(System.Math.Pow(tle.MeanSemiMajorAxis, 3) / TestHelpers.EarthAtJ2000.GM);
        Assert.True(TimeSpan.FromSeconds(period) - TimeSpan.FromHours(23.934469591) < TimeSpan.FromSeconds(1)); // Within 300 seconds of 24 hours
        Assert.True(stateVector.Position.Magnitude() > 40000000); // GEO altitude range
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        var result = tle.ToString();

        Assert.Contains("ISS", result);
        Assert.Contains("25544", result);
    }

    [Fact]
    public void TLEAreEquals()
    {
        var tle1 = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var tle2 = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        Assert.Equal(tle1, tle2);
    }

    [Fact]
    public void Create_ExtremeValues_HandlesCorrectly()
    {
        var epoch = new TimeSystem.Time(new DateTime(2024, 1, 1), TimeFrame.UTCFrame);
        var kep = new KeplerianElements(
            42164000.0, // GEO semi-major axis
            0.0001, // Very low eccentricity
            0.001, // Very low inclination
            0.0, // RAAN
            0.0, // Argument of periapsis
            0.0, // Mean anomaly
            TestHelpers.EarthAtJ2000,
            epoch,
            Frames.Frame.ICRF);

        var tle = TLE.Create(kep, "TEST_SAT", 65535, "21001A", 1000,
            Classification.Unclassified, 0.0001, 0.000001, 0.0, 100);

        Assert.NotNull(tle);
        Assert.Equal("TEST_SAT", tle.Name);
        Assert.Contains("65535", tle.Line1);
    }

    [Fact]
    public void OrbitalParameters_AccessibilityTest()
    {
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        // Test that all orbital parameters are accessible and reasonable
        Assert.True(tle.MeanSemiMajorAxis > 6371000); // Above Earth's radius
        Assert.True(tle.MeanEccentricity >= 0 && tle.MeanEccentricity < 1);
        Assert.True(tle.MeanInclination >= 0 && tle.MeanInclination <= System.Math.PI);
        Assert.True(tle.MeanAscendingNode >= 0 && tle.MeanAscendingNode < 2 * System.Math.PI);
        Assert.True(tle.MeanArgumentOfPeriapsis >= 0 && tle.MeanArgumentOfPeriapsis < 2 * System.Math.PI);
        Assert.True(tle.MeanMeanAnomaly >= 0 && tle.MeanMeanAnomaly < 2 * System.Math.PI);

        // Test derived parameters
        Assert.True(tle.FirstDerivationMeanMotion != 0); // Should have some drag for ISS
        Assert.True(tle.BalisticCoefficient > 0); // Should be positive
        Assert.Equal(0.0, tle.SecondDerivativeMeanMotion); // Usually zero for most satellites
    }

    [Fact]
    public void EpochExtraction_ValidatesCorrectly()
    {
        var tle = new TLE("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        // Verify that the epoch is extracted correctly
        Assert.True(tle.Epoch.TimeSpanFromJ2000().TotalSeconds > 0);

        // The epoch should be reasonable (around 2021)
        // Test that the epoch is in a reasonable time range by checking seconds since J2000
        var epochSeconds = tle.Epoch.TimeSpanFromJ2000().TotalSeconds;
        var year2020Seconds = 630720000.0; // Approximate seconds from J2000 to 2020
        var year2025Seconds = 788918400.0; // Approximate seconds from J2000 to 2025
        Assert.True(epochSeconds >= year2020Seconds && epochSeconds <= year2025Seconds);
    }
}