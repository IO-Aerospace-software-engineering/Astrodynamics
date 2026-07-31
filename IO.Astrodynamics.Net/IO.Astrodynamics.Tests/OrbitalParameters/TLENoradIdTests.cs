// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// End-to-end tests for NORAD catalog numbers above the former <see cref="ushort"/> ceiling (65535),
/// covering TLE generation, parsing, SGP4 propagation and OMM interoperability.
/// </summary>
public class TLENoradIdTests
{
    private static readonly TimeSystem.Time Epoch = new(2024, 6, 21, 12, 0, 0);

    public TLENoradIdTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    /// <summary>
    /// Builds realistic LEO mean elements suitable for TLE generation.
    /// </summary>
    private static KeplerianElements MeanElements() =>
        new(6_796_882.0, 0.0000493, 51.6423 * Astrodynamics.Constants.Deg2Rad,
            353.0312 * Astrodynamics.Constants.Deg2Rad, 320.8755 * Astrodynamics.Constants.Deg2Rad,
            39.2360 * Astrodynamics.Constants.Deg2Rad,
            TestHelpers.EarthAtJ2000, Epoch, Frames.Frame.TEME,
            elementsType: OrbitalElementsType.Mean);

    #region Generation above the ushort ceiling

    [Theory]
    [InlineData(25544, "25544")]
    [InlineData(65535, "65535")]
    [InlineData(65536, "65536")] // regression: silently wrapped to 0 when the field was a ushort
    [InlineData(70000, "70000")]
    [InlineData(99999, "99999")]
    public void Create_NumericIdAboveUshortCeiling_IsWrittenVerbatim(int noradId, string expectedField)
    {
        var tle = TLE.Create(MeanElements(), "TEST SAT", noradId, "98067A", 100);

        Assert.Equal(expectedField, tle.Line1.Substring(2, 5));
        Assert.Equal(expectedField, tle.Line2.Substring(2, 5));
        Assert.Equal(noradId, tle.NoradCatalogId);
    }

    [Theory]
    [InlineData(100000, "A0000")]
    [InlineData(148493, "E8493")]
    [InlineData(275544, "T5544")]
    [InlineData(339999, "Z9999")]
    public void Create_Alpha5Id_IsEncodedInBothLines(int noradId, string expectedField)
    {
        var tle = TLE.Create(MeanElements(), "TEST SAT", noradId, "98067A", 100);

        Assert.Equal(expectedField, tle.Line1.Substring(2, 5));
        Assert.Equal(expectedField, tle.Line2.Substring(2, 5));
        Assert.Equal(noradId, tle.NoradCatalogId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25544)]
    [InlineData(65536)]
    [InlineData(99999)]
    [InlineData(100000)]
    [InlineData(275544)]
    [InlineData(339999)]
    public void Create_AnyValidId_PreservesLineLayoutAndChecksums(int noradId)
    {
        var tle = TLE.Create(MeanElements(), "TEST SAT", noradId, "98067A", 100);

        // Fixed-width layout is what makes the rest of the line parseable
        Assert.Equal(69, tle.Line1.Length);
        Assert.Equal(69, tle.Line2.Length);
        Assert.Equal('1', tle.Line1[0]);
        Assert.Equal('2', tle.Line2[0]);
        Assert.Equal('U', tle.Line1[7]); // classification must stay in column 8

        // Re-parsing validates both checksums; letters count as 0 in the checksum
        var reparsed = new TLE(tle.Name, tle.Line1, tle.Line2);
        Assert.Equal(noradId, reparsed.NoradCatalogId);
        Assert.Equal(tle.Line1, reparsed.Line1);
        Assert.Equal(tle.Line2, reparsed.Line2);
    }

    [Theory]
    [InlineData(340000)] // one past "Z9999"
    [InlineData(1000000)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Create_IdBeyondTleCapacity_ThrowsInsteadOfTruncating(int noradId)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => TLE.Create(MeanElements(), "TEST SAT", noradId, "98067A", 100));
        Assert.Equal("noradId", ex.ParamName);
    }

    #endregion

    #region Revolution number and element set number

    [Theory]
    [InlineData(0)]
    [InlineData(65535)]
    [InlineData(65536)] // regression: silently wrapped when the field was a ushort
    [InlineData(99999)]
    public void Create_RevolutionNumberAboveUshortCeiling_IsWrittenVerbatim(int revolutions)
    {
        var tle = TLE.Create(MeanElements(), "TEST SAT", 25544, "98067A", revolutions);

        Assert.Equal(69, tle.Line2.Length);
        Assert.Equal(revolutions, int.Parse(tle.Line2.Substring(63, 5).Trim()));
    }

    [Theory]
    [InlineData(100000)] // no longer fits the 5-character field
    [InlineData(-1)]
    public void Create_RevolutionNumberBeyondFieldCapacity_Throws(int revolutions)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => TLE.Create(MeanElements(), "TEST SAT", 25544, "98067A", revolutions));
        Assert.Equal("revolutionsAtEpoch", ex.ParamName);
    }

    [Theory]
    [InlineData(10000)]
    [InlineData(65536)] // regression: wrapped to 0 and passed validation when the field was a ushort
    [InlineData(70000)] // regression: wrapped to 4464 and passed validation
    [InlineData(-1)]
    public void Create_ElementSetNumberBeyondFieldCapacity_Throws(int elementSetNumber)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => TLE.Create(MeanElements(), "TEST SAT", 25544, "98067A", 100,
                Classification.Unclassified, 0.0001, 0.0, 0.0, elementSetNumber));
        Assert.Equal("elementSetNumber", ex.ParamName);
    }

    #endregion

    #region SGP4 propagation

    [Fact]
    public void ToStateVector_Alpha5Tle_PropagatesLikeItsNumericTwin()
    {
        // The catalog number carries no dynamics: an Alpha-5 identifier must not disturb SGP4.
        var numericTle = TLE.Create(MeanElements(), "TEST SAT", 25544, "98067A", 100);
        var alpha5Tle = TLE.Create(MeanElements(), "TEST SAT", 275544, "98067A", 100);

        Assert.NotEqual(numericTle.Line1.Substring(2, 5), alpha5Tle.Line1.Substring(2, 5));

        var target = Epoch.AddHours(6.0);
        var numericState = numericTle.ToStateVector(target);
        var alpha5State = alpha5Tle.ToStateVector(target);

        Assert.Equal(numericState.Position.X, alpha5State.Position.X, 9);
        Assert.Equal(numericState.Position.Y, alpha5State.Position.Y, 9);
        Assert.Equal(numericState.Position.Z, alpha5State.Position.Z, 9);
        Assert.Equal(numericState.Velocity.X, alpha5State.Velocity.X, 9);
        Assert.Equal(numericState.Velocity.Y, alpha5State.Velocity.Y, 9);
        Assert.Equal(numericState.Velocity.Z, alpha5State.Velocity.Z, 9);
    }

    [Fact]
    public void ToStateVector_Alpha5TleFromSpaceTrack_IsAcceptedByPropagator()
    {
        // Hand-written Alpha-5 lines, as distributed by Space-Track for high catalog numbers.
        var tle = new TLE("STARLINK-DUMMY",
            "1 T5544U 98067A   24173.50000000  .00016717  00000-0  10270-3 0  9052",
            "2 T5544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25701");

        Assert.Equal(275544, tle.NoradCatalogId);

        var state = tle.ToStateVector(tle.Epoch);
        Assert.True(state.Position.Magnitude() > 6_500_000.0);
        Assert.True(state.Position.Magnitude() < 7_500_000.0);
    }

    #endregion

    #region Parsing

    [Theory]
    [InlineData("1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
        "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703", 25544)]
    [InlineData("1 T5544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9052",
        "2 T5544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25701", 275544)]
    [InlineData("1 Z9999U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9050",
        "2 Z9999  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25709", 339999)]
    public void Constructor_DecodesCatalogNumber(string line1, string line2, int expected)
    {
        var tle = new TLE("SAT", line1, line2);
        Assert.Equal(expected, tle.NoradCatalogId);
    }

    [Fact]
    public void Constructor_MismatchedCatalogNumbersBetweenLines_Throws()
    {
        // Lines belonging to two different objects must not silently form one TLE.
        // Both lines are individually well-formed (valid checksums), only the identifiers differ.
        var ex = Assert.Throws<InvalidOperationException>(() => new TLE("SAT",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25545  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25704"));
        Assert.Contains("do not belong to the same object", ex.Message);
    }

    [Fact]
    public void Constructor_MalformedCatalogNumber_Throws()
    {
        // 'I' is not part of the Alpha-5 alphabet, and a letter cannot appear after the first column.
        // Checksums are valid here, so the failure can only come from the identifier itself.
        var ex = Assert.Throws<ArgumentException>(() => new TLE("SAT",
            "1 2I544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9059",
            "2 2I544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25708"));
        Assert.Contains("Invalid NORAD catalog number", ex.Message);
    }

    #endregion

    #region OMM interoperability

    [Theory]
    [InlineData(25544)]
    [InlineData(65536)] // regression: (ushort) cast in Omm.ToTle() turned this into 0
    [InlineData(70000)] // regression: became 4464
    [InlineData(99999)]
    [InlineData(275544)] // requires Alpha-5 encoding
    [InlineData(339999)]
    public void OmmToTle_PreservesCatalogNumber(int noradCatalogId)
    {
        var omm = IO.Astrodynamics.CCSDS.OMM.Omm.CreateForTle(
            "TEST SAT", "1998-067A", Epoch.DateTime,
            meanMotion: 15.49309423, eccentricity: 0.0000493, inclination: 51.6423,
            raan: 353.0312, argOfPericenter: 320.8755, meanAnomaly: 39.2360,
            bstar: 0.0001027, meanMotionDot: 0.00016717, meanMotionDDot: 0.0,
            noradCatalogId: noradCatalogId, elementSetNumber: 999, revolutionNumber: 25703);

        var tle = omm.ToTle();

        Assert.Equal(noradCatalogId, tle.NoradCatalogId);
        Assert.Equal(69, tle.Line1.Length);
        Assert.Equal(69, tle.Line2.Length);
    }

    [Theory]
    [InlineData(25544)]
    [InlineData(70000)]
    [InlineData(275544)]
    public void OmmToTleToOmm_RoundTripsCatalogNumber(int noradCatalogId)
    {
        var omm = IO.Astrodynamics.CCSDS.OMM.Omm.CreateForTle(
            "TEST SAT", "1998-067A", Epoch.DateTime,
            meanMotion: 15.49309423, eccentricity: 0.0000493, inclination: 51.6423,
            raan: 353.0312, argOfPericenter: 320.8755, meanAnomaly: 39.2360,
            bstar: 0.0001027, meanMotionDot: 0.00016717, meanMotionDDot: 0.0,
            noradCatalogId: noradCatalogId, elementSetNumber: 999, revolutionNumber: 25703);

        var reconverted = omm.ToTle().ToOmm();

        Assert.Equal(noradCatalogId, reconverted.Data.TleParameters.NoradCatalogId);
    }

    [Theory]
    [InlineData(340000)] // beyond what the TLE field can carry, but legal in an OMM
    [InlineData(999999999)]
    public void OmmToTle_CatalogNumberBeyondTleCapacity_ThrowsInsteadOfTruncating(int noradCatalogId)
    {
        var omm = IO.Astrodynamics.CCSDS.OMM.Omm.CreateForTle(
            "TEST SAT", "1998-067A", Epoch.DateTime,
            meanMotion: 15.49309423, eccentricity: 0.0000493, inclination: 51.6423,
            raan: 353.0312, argOfPericenter: 320.8755, meanAnomaly: 39.2360,
            bstar: 0.0001027, meanMotionDot: 0.00016717, meanMotionDDot: 0.0,
            noradCatalogId: noradCatalogId, elementSetNumber: 999, revolutionNumber: 25703);

        var ex = Assert.Throws<InvalidOperationException>(() => omm.ToTle());
        Assert.Contains(noradCatalogId.ToString(), ex.Message);
    }

    [Fact]
    public void OmmToTle_RevolutionNumberBeyondTleCapacity_ThrowsInsteadOfTruncating()
    {
        var omm = IO.Astrodynamics.CCSDS.OMM.Omm.CreateForTle(
            "TEST SAT", "1998-067A", Epoch.DateTime,
            meanMotion: 15.49309423, eccentricity: 0.0000493, inclination: 51.6423,
            raan: 353.0312, argOfPericenter: 320.8755, meanAnomaly: 39.2360,
            bstar: 0.0001027, meanMotionDot: 0.00016717, meanMotionDDot: 0.0,
            noradCatalogId: 25544, elementSetNumber: 999, revolutionNumber: 123456);

        Assert.Throws<InvalidOperationException>(() => omm.ToTle());
    }

    [Fact]
    public void OmmToTle_RevolutionNumberAboveUshortCeiling_IsPreserved()
    {
        var omm = IO.Astrodynamics.CCSDS.OMM.Omm.CreateForTle(
            "TEST SAT", "1998-067A", Epoch.DateTime,
            meanMotion: 15.49309423, eccentricity: 0.0000493, inclination: 51.6423,
            raan: 353.0312, argOfPericenter: 320.8755, meanAnomaly: 39.2360,
            bstar: 0.0001027, meanMotionDot: 0.00016717, meanMotionDDot: 0.0,
            noradCatalogId: 25544, elementSetNumber: 999, revolutionNumber: 70000);

        var tle = omm.ToTle();

        Assert.Equal(70000, int.Parse(tle.Line2.Substring(63, 5).Trim()));
    }

    #endregion

    #region Configuration / fitting pipeline

    [Theory]
    [InlineData(70000)]
    [InlineData(275544)]
    public void ToTLE_ConfigurationCarriesLargeIdThroughFitting(int noradId)
    {
        var stateVector = new StateVector(
            new IO.Astrodynamics.Math.Vector3(5465479.168061836, -4037598.9299125164, 3.8812307310800365),
            new IO.Astrodynamics.Math.Vector3(2821.2352501830983, 3825.849951628489, 6009.392701926987),
            TestHelpers.EarthAtJ2000, Epoch, Frames.Frame.TEME);

        var config = new IO.Astrodynamics.OrbitalParameters.TLE.Configuration(noradId, "TEST SAT", "98067A", RevolutionsAtEpoch: 70000);

        var tle = stateVector.ToTLE(config);

        Assert.Equal(noradId, tle.NoradCatalogId);
        Assert.Equal(69, tle.Line1.Length);
        Assert.Equal(69, tle.Line2.Length);

        // The fitted TLE must still reproduce the source state
        var refitted = tle.ToStateVector(Epoch).ToFrame(Frames.Frame.TEME).ToStateVector();
        Assert.True((refitted.Position - stateVector.Position).Magnitude() < 10.0);
        Assert.True((refitted.Velocity - stateVector.Velocity).Magnitude() < 0.01);
    }

    #endregion
}
