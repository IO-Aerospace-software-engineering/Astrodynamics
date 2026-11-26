using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.CCSDS.Common.Enums;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The catalogue object of the conjunction analysis and the CDM export (R19 of #668, lot L7).
/// </summary>
public class TrackedObjectTests
{
    // The ISS set of the TLE tests, and the same set with the largest Alpha-5 number (TLENoradIdTests)
    private static readonly Tle Iss = new("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
        "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

    private static readonly Tle Z9999 = new("Z9999", "1 Z9999U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9050",
        "2 Z9999  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25709");

    public TrackedObjectTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static TimeSystem.Time Tca => AvoidanceFallbackCase.Tca;

    private static CelestialBody Earth => PlanetsAndMoons.EarthBody;

    // A protected state at TCA with its RTN covariance, and the state of a secondary crossing it 200 m away (the avoidance
    // tests' geometry)
    private static StateVector ProtectedAtTca() => ConjunctionCovarianceCase.WithRtnCovariance(
        new StateVector(new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 5_300.0, 5_300.0), Earth, Tca, Frames.Frame.ICRF),
        new[] { 50.0, 200.0, 40.0, 0.2, 0.05, 0.04 });

    private static Window Window => new(Tca.AddMinutes(-30.0), Tca.AddMinutes(30.0));

    [Fact]
    public void FromTle_DefaultIdentity_MkspkIdAndTheFieldsOfTheTle()
    {
        // Act
        var tracked = TrackedObject.FromTle(Z9999);

        // Assert: −100000 − NORAD (mkspk's convention, confirmed by S. Guillet, 2026-11-13), Alpha-5 decoded
        Assert.Equal(-439999, tracked.NaifId);
        Assert.Equal(339999, tracked.NoradCatalogId);
        Assert.Equal("1998-067A", tracked.CosparId);
        Assert.Equal(ObjectType.Unknown, tracked.ObjectType);
        Assert.Equal(0.0, tracked.HardBodyRadius);
        Assert.Equal("Z9999", tracked.Name);
        Assert.Same(Z9999, tracked.InitialOrbitalParameters);
        Assert.False(tracked.IsSpiceBacked);
        Assert.Equal(399, tracked.CenterOfMotionId);
    }

    [Fact]
    public void FromTle_GivenValues_Kept()
    {
        var tracked = TrackedObject.FromTle(Iss, 1.5, -7001, ObjectType.Debris, "Fragment");

        Assert.Equal(-7001, tracked.NaifId);
        Assert.Equal(1.5, tracked.HardBodyRadius);
        Assert.Equal(ObjectType.Debris, tracked.ObjectType);
        Assert.Equal("Fragment", tracked.Name);
        Assert.Equal(25544, tracked.NoradCatalogId);
    }

    [Fact]
    public void FromTle_Ephemeris_BitForBitTheSpacecraftOfTheSameTle()
    {
        // Arrange: the former workaround, a spacecraft wrapping the TLE
        var tracked = TrackedObject.FromTle(Iss);
        var wrapper = new Spacecraft(-7002, "Wrapper", 100.0, Iss);
        var epoch = Iss.Epoch.AddHours(3.0);

        foreach (var aberration in new[] { Aberration.None, Aberration.LT })
        {
            // Act
            var fromTracked = tracked.GetEphemeris(epoch, Earth, Frames.Frame.ICRF, aberration);
            var fromWrapper = wrapper.GetEphemeris(epoch, Earth, Frames.Frame.ICRF, aberration);
            var moonFromTracked = PlanetsAndMoons.MoonBody.GetEphemeris(epoch, tracked, Frames.Frame.ICRF, aberration);
            var moonFromWrapper = PlanetsAndMoons.MoonBody.GetEphemeris(epoch, wrapper, Frames.Frame.ICRF, aberration);

            // Assert: the object as target and as observer (its reference body the Earth of the TLE)
            Assert.Equal(fromWrapper.Position, fromTracked.Position);
            Assert.Equal(fromWrapper.Velocity, fromTracked.Velocity);
            Assert.Equal(moonFromWrapper.Position, moonFromTracked.Position);
            Assert.Equal(moonFromWrapper.Velocity, moonFromTracked.Velocity);
        }
    }

    [Fact]
    public void FromOmm_TheObjectOfTheTleOfTheOmm()
    {
        // Arrange
        var omm = Iss.ToOmm();

        // Act
        var tracked = TrackedObject.FromOmm(omm, 2.0);
        var expected = TrackedObject.FromTle(omm.ToTle(), 2.0);

        // Assert
        Assert.Equal(expected.NaifId, tracked.NaifId);
        Assert.Equal(-125544, tracked.NaifId);
        Assert.Equal(expected.CosparId, tracked.CosparId);
        Assert.Equal(2.0, tracked.HardBodyRadius);
        var epoch = Iss.Epoch.AddHours(1.0);
        Assert.Equal(expected.GetEphemeris(epoch, Earth).Position, tracked.GetEphemeris(epoch, Earth).Position);
    }

    [Fact]
    public void FromState_TwoBodyMotionAndIdentity()
    {
        // Arrange
        var state = ProtectedAtTca();

        // Act
        var tracked = TrackedObject.FromState(state, "Debris", 0.5, noradCatalogId: 90001, cosparId: "2020-001C",
            objectType: ObjectType.Debris);

        // Assert
        Assert.Equal(-190001, tracked.NaifId);
        Assert.Equal(90001, tracked.NoradCatalogId);
        Assert.Equal("2020-001C", tracked.CosparId);
        var epoch = Tca.AddMinutes(20.0);
        var expected = state.ToStateVector(epoch);
        var read = tracked.GetEphemeris(epoch, Earth);
        Assert.Equal(expected.Position, read.Position);
        Assert.Equal(expected.Velocity, read.Velocity);
    }

    [Fact]
    public void FromState_WithoutNoradOrNaifId_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => TrackedObject.FromState(ProtectedAtTca(), "Debris"));
        Assert.Equal("naifId", exception.ParamName);
        Assert.Equal(-7003, TrackedObject.FromState(ProtectedAtTca(), "Debris", naifId: -7003).NaifId);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Factories_InvalidHardBodyRadius_Throw(double radius)
    {
        Assert.Equal("hardBodyRadiusMeters",
            Assert.Throws<ArgumentOutOfRangeException>(() => TrackedObject.FromTle(Iss, radius)).ParamName);
        Assert.Equal("hardBodyRadiusMeters",
            Assert.Throws<ArgumentOutOfRangeException>(() => TrackedObject.FromState(ProtectedAtTca(), "D", radius, -7004)).ParamName);
    }

    [Fact]
    public void Factories_InvalidArguments_Throw()
    {
        Assert.Equal("naifId", Assert.Throws<ArgumentOutOfRangeException>(() => TrackedObject.FromTle(Iss, naifId: 0)).ParamName);
        Assert.Equal("noradCatalogId",
            Assert.Throws<ArgumentOutOfRangeException>(() => TrackedObject.FromState(ProtectedAtTca(), "D", noradCatalogId: -1)).ParamName);
        Assert.Throws<ArgumentNullException>(() => TrackedObject.FromTle(null));
        Assert.Throws<ArgumentNullException>(() => TrackedObject.FromOmm(null));
        Assert.Throws<ArgumentNullException>(() => TrackedObject.FromState(null, "D", naifId: -7005));
        Assert.Throws<ArgumentException>(() => TrackedObject.FromState(ProtectedAtTca(), "", naifId: -7005));
    }

    [Fact]
    public void Analyze_TrackedSecondary_ItsRadiusAndTrajectory()
    {
        // Arrange
        var protectedCraft = new Spacecraft(-7010, "P7010", 100.0, ProtectedAtTca(), hardBodyRadius: 2.0);
        var debris = TrackedObject.FromState(AvoidanceFallbackCase.SecondaryNear(ProtectedAtTca()), "Debris", 3.0,
            noradCatalogId: 90002);

        // Act
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, debris, Window);

        // Assert: the radius of the object, its two-body orbit and its covariance
        Assert.Equal(5.0, encounter.CollisionRisk.CombinedHardBodyRadiusMeters);
        Assert.Equal(TrajectorySource.TwoBody, encounter.EncounterState.SecondaryTrajectory);
        Assert.Equal(CovarianceSource.InitialStateHeldInRtn, encounter.EncounterState.SecondaryCovariance.Source);
        Assert.True(encounter.MissDistanceMeters < 300.0);

        // An option that differs from the radius of the object throws, as for a spacecraft
        Assert.Throws<ArgumentException>(() => ConjunctionAssessment.Analyze(protectedCraft, debris, Window,
            ConjunctionAnalysisOptions.Default with { SecondaryHardBodyRadiusMeters = 4.0 }));
    }

    [Fact]
    public void Analyze_TrackedTleSecondary_Sgp4()
    {
        // Arrange
        var protectedCraft = new Spacecraft(-7011, "P7011", 100.0,
            AvoidanceFallbackCase.SecondaryNear(Iss.ToOsculating(Iss.Epoch.AddHours(1.0)).ToFrame(Frames.Frame.ICRF).ToStateVector()));
        var debris = TrackedObject.FromTle(Iss);

        // Act
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, debris,
            new Window(Iss.Epoch.AddMinutes(30.0), Iss.Epoch.AddMinutes(90.0)));

        // Assert
        Assert.Equal(TrajectorySource.Sgp4, encounter.EncounterState.SecondaryTrajectory);
        Assert.Equal(5.0, encounter.CollisionRisk.CombinedHardBodyRadiusMeters);
    }

    [Fact]
    public void ToCdm_TrackedSecondary_ItsCatalogueIdentity()
    {
        // Arrange
        var protectedCraft = new Spacecraft(-7012, "P7012", 100.0, ProtectedAtTca(), hardBodyRadius: 2.0, cosparId: "2026-001A");
        var debris = TrackedObject.FromState(AvoidanceFallbackCase.SecondaryNear(ProtectedAtTca()), "Debris", 1.0,
            noradCatalogId: 90003, cosparId: "1999-025ABC", objectType: ObjectType.Debris);
        var anonymous = TrackedObject.FromState(AvoidanceFallbackCase.SecondaryNear(ProtectedAtTca()), "Anonymous", 1.0,
            noradCatalogId: 90004);
        var options = new CdmExportOptions { Originator = "IO.Astrodynamics.Tests", MessageFor = "Desk", MessageId = "CDM-TRACKED" };

        // Act
        var cdm = ConjunctionAssessment.Analyze(protectedCraft, debris, Window).ToCdm(options);
        var anonymousCdm = ConjunctionAssessment.Analyze(protectedCraft, anonymous, Window).ToCdm(options);

        // Assert: the NORAD number in the SATCAT, the international designator, the type of the object, MANEUVERABLE N/A
        var metadata = cdm.Segments[1].Metadata;
        Assert.Equal("90003", metadata.ObjectDesignator);
        Assert.Equal("SATCAT", metadata.CatalogName);
        Assert.Equal("1999-025ABC", metadata.InternationalDesignator);
        Assert.Equal(ObjectType.Debris, metadata.ObjectType);
        Assert.Equal(CdmManeuverable.NotApplicable, metadata.Maneuverable);
        Assert.Equal("Debris", metadata.ObjectName);
        Assert.True(cdm.Validate().IsValid);
        var unknown = anonymousCdm.Segments[1].Metadata;
        Assert.Equal("UNKNOWN", unknown.InternationalDesignator);
        Assert.Equal(ObjectType.Unknown, unknown.ObjectType);

        // The protected spacecraft keeps the NAIF catalogue
        Assert.Equal("NAIF", cdm.Segments[0].Metadata.CatalogName);
        Assert.Equal("7012", cdm.Segments[0].Metadata.ObjectDesignator);
    }

    [Fact]
    public void ToCdm_TrackedSecondaryWithoutNorad_TheNaifCatalogue()
    {
        var protectedCraft = new Spacecraft(-7013, "P7013", 100.0, ProtectedAtTca(), cosparId: "2026-001A");
        var debris = TrackedObject.FromState(AvoidanceFallbackCase.SecondaryNear(ProtectedAtTca()), "Debris", naifId: -7014);

        var metadata = ConjunctionAssessment.Analyze(protectedCraft, debris, Window).ToCdm().Segments[1].Metadata;

        Assert.Equal("7014", metadata.ObjectDesignator);
        Assert.Equal("NAIF", metadata.CatalogName);
        Assert.Equal("UNKNOWN", metadata.InternationalDesignator);
    }

    [Fact]
    public void Screen_TrackedCandidates_TheirOwnRadius()
    {
        var protectedCraft = new Spacecraft(-7015, "P7015", 100.0, ProtectedAtTca(), hardBodyRadius: 2.0);
        var debris = TrackedObject.FromState(AvoidanceFallbackCase.SecondaryNear(ProtectedAtTca()), "Debris", 3.0, naifId: -7016);

        var encounters = ConjunctionAssessment.Screen(protectedCraft, new ILocalizable[] { debris }, Window,
            new ScreeningOptions { Analysis = ConjunctionAnalysisOptions.Default with { SecondaryHardBodyRadiusMeters = 7.0 } });

        Assert.Equal(5.0, encounters.Single().CollisionRisk.CombinedHardBodyRadiusMeters);
    }
}
