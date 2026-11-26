// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OMM;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS;

/// <summary>
/// <c>Omm.GetEpoch</c>, <c>Opm.GetEpoch</c> and <c>Omm.ToMeanElements</c> (R22 of #668, lot L7): the epoch of a message in
/// its <c>TIME_SYSTEM</c>, and its mean elements in SI units about the centre <c>CENTER_NAME</c> names, in its
/// <c>REF_FRAME</c>, bit for bit the internal <c>KeplerianElements.FromOMM</c> (public before 11.0.0) on the same values.
/// </summary>
public class OmmMeanElementsAndEpochTests
{
    public OmmMeanElementsAndEpochTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly DateTime Epoch = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Omm OmmWith(string centerName, string referenceFrame, string timeSystem, MeanElements elements) =>
        new(CcsdsHeader.CreateDefault(), new OmmMetadata("SAT", "2024-001A", centerName, referenceFrame, timeSystem, "SGP4"),
            new OmmData(elements));

    private static MeanElements WithMeanMotion() => MeanElements.CreateWithMeanMotion(Epoch, 15.5, 0.0007, 51.64, 120.0, 80.0, 30.0);

    [Fact]
    public void GetEpoch_IsTheEpochInItsTimeSystem()
    {
        // iss.omm: EPOCH 2026-01-13T05:00:55.752768 under TIME_SYSTEM UTC
        var iss = Omm.Load(NdmDocuments.IssOmmPath);
        LabelAssert.Equal(new IO.Astrodynamics.TimeSystem.Time(new DateTime(2026, 1, 13, 5, 0, 55).AddTicks(7527680), TimeScale.UTC), iss.GetEpoch());

        foreach (var (system, scale) in new[] { ("TDB", TimeScale.TDB), ("TT", TimeScale.TT), ("TAI", TimeScale.TAI), ("GPS", TimeScale.GPS) })
        {
            var expected = new IO.Astrodynamics.TimeSystem.Time(DateTime.SpecifyKind(Epoch, DateTimeKind.Unspecified), scale);
            LabelAssert.Equal(expected, OmmWith("EARTH", "TEME", system, WithMeanMotion()).GetEpoch());
            var opm = Opm.CreateMinimal("SAT", "2024-001A", "EARTH", "ICRF", system,
                new OpmStateVector(Epoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0));
            LabelAssert.Equal(expected, opm.GetEpoch());
        }
    }

    [Fact]
    public void GetEpoch_TimeSystemTimeLacks_Throws()
    {
        // UT1 loads (a warning of the validator) but no Time holds it
        Assert.Throws<InvalidOperationException>(() => OmmWith("EARTH", "TEME", "UT1", WithMeanMotion()).GetEpoch());
        var opm = Opm.CreateMinimal("SAT", "2024-001A", "EARTH", "ICRF", "UT1", new OpmStateVector(Epoch, 6778.137, 0.0, 0.0, 0.0, 7.5, 0.0));
        Assert.Throws<InvalidOperationException>(() => opm.GetEpoch());
    }

    [Fact]
    public void ToMeanElements_MeanMotion_IsFromOmmBitForBit()
    {
        var iss = Omm.Load(NdmDocuments.IssOmmPath);
        var epoch = iss.GetEpoch();
        var m = iss.Data.MeanElements;
        var expected = KeplerianElements.FromOMM(m.MeanMotion!.Value, m.Eccentricity, m.Inclination,
            m.RightAscensionOfAscendingNode, m.ArgumentOfPericenter, m.MeanAnomaly,
            new CelestialBody(PlanetsAndMoons.Earth, Frames.Frame.ICRF, epoch), epoch, Frames.Frame.TEME);

        var actual = iss.ToMeanElements();

        Assert.Equal(OrbitalElementsType.Mean, actual.ElementsType);
        Assert.Equal(expected, actual);
        Assert.Equal(399, actual.Observer.NaifId);
        Assert.Equal(Frames.Frame.TEME, actual.Frame);
        Assert.Equal(expected.MeanMotion(), actual.MeanMotion());
        Assert.Equal(expected.SemiMajorAxis(), actual.SemiMajorAxis());
        LabelAssert.Equal(epoch, actual.Epoch);
    }

    [Fact]
    public void ToMeanElements_SemiMajorAxis_IsInSiUnits()
    {
        var omm = OmmWith("EARTH", "ICRF", "TDB", MeanElements.CreateWithSemiMajorAxis(Epoch, 6778.137, 0.0007, 51.64, 120.0, 80.0, 30.0));
        var epoch = omm.GetEpoch();

        var actual = omm.ToMeanElements();

        var d = IO.Astrodynamics.Constants.Deg2Rad;
        var expected = new KeplerianElements(6778137.0, 0.0007, 51.64 * d, 120.0 * d, 80.0 * d, 30.0 * d,
            new CelestialBody(PlanetsAndMoons.Earth, Frames.Frame.ICRF, epoch), epoch, Frames.Frame.ICRF, OrbitalElementsType.Mean);
        Assert.Equal(expected, actual);
        Assert.Equal(6778137.0, actual.SemiMajorAxis());
    }

    [Fact]
    public void ToMeanElements_TheCentreCenterNameNames()
    {
        // The rules of Opm.ToStateVector (#679): the centre CENTER_NAME names, a contradicting observer and an unknown name
        // without an observer throwing ArgumentException (observer)
        var moon = OmmWith("MOON", "ICRF", "UTC", WithMeanMotion());
        Assert.Equal(301, moon.ToMeanElements().Observer.NaifId);
        var ssb = OmmWith("SOLAR SYSTEM BARYCENTER", "ICRF", "UTC", WithMeanMotion());
        Assert.Equal(0, ssb.ToMeanElements().Observer.NaifId);

        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        Assert.Equal("observer", Assert.Throws<ArgumentException>(() => moon.ToMeanElements(earth)).ParamName);
        var unknown = OmmWith("NOT A BODY", "ICRF", "UTC", WithMeanMotion());
        Assert.Equal("observer", Assert.Throws<ArgumentException>(() => unknown.ToMeanElements()).ParamName);
        Assert.Same(earth, unknown.ToMeanElements(earth).Observer);
    }

    // FromOMM, whose angles are degrees and mean motion rev/day, is internal; the bool constructor of MeanElements is removed
    [Fact]
    public void FromOmmAndTheMeanMotionConstructor_AreNotPublic()
    {
        Assert.Null(typeof(KeplerianElements).GetMethod("FromOMM", BindingFlags.Public | BindingFlags.Static));
        Assert.DoesNotContain(typeof(MeanElements).GetConstructors(), c => c.GetParameters().Any(p => p.ParameterType == typeof(bool)));
    }
}
