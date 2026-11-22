// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;

namespace IO.Astrodynamics.Tests.DataProvider;

/// <summary>
/// The body information of the data providers in a domain type (R04 of #668, lot L3): <see cref="CelestialBodyInfo"/>,
/// a public sealed record, replaces the interop struct <c>IO.Astrodynamics.DTO.CelestialBody</c>, now internal, on
/// <see cref="IDataProvider.GetCelestialBodyInfo"/>, <see cref="SpiceAPI.GetCelestialBodyInfo"/> and
/// <see cref="MemoryDataProvider"/>; the state of <see cref="IDataProvider.GetEphemeris"/> is a
/// <see cref="StateVector"/> (R20). The references: the native record of <c>GetCelestialBodyInfoProxy</c>, field for field
/// and bit for bit (the record holds the values SPICE gives, already SI), and the kernel texts of <c>gm_de440.tpc</c> and
/// <c>pck00011.tpc</c> read by the tests.
/// </summary>
public class CelestialBodyInfoTests
{
    private readonly ITestOutputHelper _output;

    public CelestialBodyInfoTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    // Bodies of Data/SolarSystem: barycentres (no frame), the Sun, planets, moons with and without J2, an asteroid, a site
    public static TheoryData<int> Bodies => new() { 0, 3, 5, 10, 199, 299, 301, 399, 401, 499, 599, 606, 799, 899, 2000002, 399013 };

    [Theory]
    [MemberData(nameof(Bodies))]
    public void SpiceRecord_IsTheNativeRecord_BitForBit(int naifId)
    {
        var record = SpiceAPI.Instance.ReadNativeCelestialBodyInfo(naifId);
        var info = SpiceAPI.Instance.GetCelestialBodyInfo(naifId);
        Assert.Equal(record.Id, info.NaifId);
        Assert.Equal(record.Name, info.Name);
        Assert.Equal(record.CenterOfMotionId, info.CenterOfMotionId);
        Assert.Equal(record.BarycenterOfMotionId, info.BarycenterOfMotionId);
        Assert.Equal(BitConverter.DoubleToInt64Bits(record.Radii.X), BitConverter.DoubleToInt64Bits(info.Radii.X));
        Assert.Equal(BitConverter.DoubleToInt64Bits(record.Radii.Y), BitConverter.DoubleToInt64Bits(info.Radii.Y));
        Assert.Equal(BitConverter.DoubleToInt64Bits(record.Radii.Z), BitConverter.DoubleToInt64Bits(info.Radii.Z));
        Assert.Equal(BitConverter.DoubleToInt64Bits(record.GM), BitConverter.DoubleToInt64Bits(info.GM));
        Assert.Equal(record.FrameName, info.FrameName);
        Assert.Equal(record.FrameId, info.FrameId);
        Assert.Equal(BitConverter.DoubleToInt64Bits(record.J2), BitConverter.DoubleToInt64Bits(info.J2));
        Assert.Equal(BitConverter.DoubleToInt64Bits(record.J3), BitConverter.DoubleToInt64Bits(info.J3));
        Assert.Equal(BitConverter.DoubleToInt64Bits(record.J4), BitConverter.DoubleToInt64Bits(info.J4));
        // The provider's record is the same values
        Assert.Equal(info, ((IDataProvider)new SpiceDataProvider()).GetCelestialBodyInfo(naifId));
    }

    [Theory]
    [InlineData(399)]
    [InlineData(301)]
    [InlineData(499)]
    public void SpiceRecord_HoldsTheKernelTexts(int naifId)
    {
        var info = SpiceAPI.Instance.GetCelestialBodyInfo(naifId);
        double gm = KernelTextValues.GravitationalParameter(naifId);
        double[] radii = KernelTextValues.Radii(naifId);
        _output.WriteLine($"MEASURE {naifId}: GM {info.GM:R} (text {gm:R}), radii {info.Radii}");
        Assert.True(KernelTextValues.UlpDistance(gm, info.GM) <= KernelTextValues.SpiceParseUlps);
        Assert.Equal(new Vector3(radii[0], radii[1], radii[2]), info.Radii);
    }

    [Fact]
    public void Record_HasValueEquality_AndOptionalFrameIdAndHarmonics()
    {
        var info = new CelestialBodyInfo(399, "EARTH", 10, 3, new Vector3(6378137.0, 6378137.0, 6356752.0), 3.986004418e14, "ITRF93");
        Assert.Equal(0, info.FrameId);
        Assert.True(double.IsNaN(info.J2) && double.IsNaN(info.J3) && double.IsNaN(info.J4));
        var same = new CelestialBodyInfo(399, "EARTH", 10, 3, new Vector3(6378137.0, 6378137.0, 6356752.0), 3.986004418e14, "ITRF93");
        Assert.Equal(info, same);
        Assert.Equal(info.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(info, info with { J2 = 1.08e-3 });
        Assert.Equal(13000, (info with { FrameId = 13000 }).FrameId);
    }

    [Fact]
    public void MemoryProvider_GivesTheRecordAdded_ByItsNaifId()
    {
        var provider = new MemoryDataProvider();
        var earth = new CelestialBodyInfo(399, "EARTH", 10, 3, new Vector3(6378137.0, 6378137.0, 6356752.0), 3.986004418e14,
            "ITRF93", 13000, 1.08e-3, -2.5e-6, -1.6e-6);
        var moon = new CelestialBodyInfo(301, "MOON", 399, 3, new Vector3(1737400.0, 1737400.0, 1737400.0), 4.9028e12, "MOON_ME");
        provider.AddCelestialBodyInfo(earth, moon);
        Assert.Same(earth, provider.GetCelestialBodyInfo(399));
        Assert.Same(moon, ((IDataProvider)provider).GetCelestialBodyInfo(301));
        var e = Assert.Throws<ArgumentException>(() => provider.GetCelestialBodyInfo(499));
        Assert.Equal("naifId", e.ParamName);
        Assert.Equal("celestialBodyInfos", Assert.Throws<ArgumentNullException>(() => provider.AddCelestialBodyInfo(null)).ParamName);
        Assert.Equal("celestialBodyInfos", Assert.Throws<ArgumentNullException>(() => provider.AddCelestialBodyInfo(earth, null)).ParamName);
    }

    /// <summary>
    /// The state of the provider seam is a <see cref="StateVector"/>, the state <see cref="SpiceAPI.ReadEphemeris(TimeSystem.Time, int, int, Frames.Frame, Aberration)"/>
    /// reads, bit for bit.
    /// </summary>
    [Fact]
    public void SpiceProvider_GetEphemeris_IsAStateVector_OfSpice()
    {
        IDataProvider provider = new SpiceDataProvider();
        var epoch = new TimeSystem.Time(2024, 6, 21, 12, 0, 0, TimeSystem.TimeScale.TDB);
        StateVector state = provider.GetEphemeris(epoch, PlanetsAndMoons.MOON_BODY, PlanetsAndMoons.EARTH_BODY, Frames.Frame.ICRF, Aberration.None);
        var spice = SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, Frames.Frame.ICRF, Aberration.None);
        Assert.Equal(spice.Position, state.Position);
        Assert.Equal(spice.Velocity, state.Velocity);
    }

    /// <summary>The information a natural body read is not reachable by a derived class (private protected).</summary>
    [Fact]
    public void ExtendedInformation_IsNotVisibleToDerivedClasses()
    {
        var field = typeof(CelestialItem).GetField("ExtendedInformation", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.True(field.IsFamilyAndAssembly, "ExtendedInformation is not private protected");
        Assert.Equal(typeof(CelestialBodyInfo), field.FieldType);
    }

    /// <summary>A body built from a provider's record takes its values (SPICE's GM and radii for the Earth).</summary>
    [Fact]
    public void CelestialBody_TakesTheValuesOfItsRecord()
    {
        var info = SpiceAPI.Instance.GetCelestialBodyInfo(399);
        var earth = new CelestialBody(399);
        Assert.Equal(info.GM, earth.GM);
        Assert.Equal(info.Radii, earth.Radii);
        Assert.Equal(info.Name, earth.Name);
        Assert.Equal(info.FrameName, earth.Frame.Name);
        Assert.Equal(info.BarycenterOfMotionId, earth.BarycenterOfMotionId);
    }

    /// <summary>The records of the providers are records of the library, not of an interop namespace.</summary>
    [Fact]
    public void Record_IsAPublicSealedRecordOfTheDataProviderNamespace()
    {
        var type = typeof(CelestialBodyInfo);
        Assert.True(type.IsPublic && type.IsSealed && type.IsClass);
        Assert.Equal("IO.Astrodynamics.DataProvider", type.Namespace);
        Assert.NotNull(type.GetMethod("<Clone>$"));
        Assert.DoesNotContain(type.GetProperties(), p => p.CanWrite && p.SetMethod!.IsPublic &&
                                                          !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Any());
    }
}
