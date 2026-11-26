// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The public probability of collision (R19 of #668, lot L7): <see cref="CollisionProbability.Foster2D"/> and
/// <see cref="CollisionProbability.FrisbeeMax"/> on the seven designed encounters of
/// <c>tools/collision_probability</c> (mpmath), and <see cref="Cdm.ToEncounter"/>, the encounter at the TCA of a CDM
/// from its states as given, its Pc that of <see cref="CollisionProbability.Foster2D"/> bit for bit (decision 110 of
/// #668, decided by S. Guillet, 2026-11-26).
/// </summary>
public partial class ConjunctionAssessmentTests
{
    [Fact]
    public void Foster2DAndFrisbeeMax_DesignedEncounters_AreTheInternalPcAndMatchTheExactPc()
    {
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000TDB;
        foreach (var encounter in CollisionProbabilityReference.Value.RootElement.GetProperty("encounters").EnumerateArray())
        {
            // Arrange: the states in km, as the internal test of the integral, each with its covariance in ICRF
            var (r1, v1, c1, r2, v2, c2, hardBodyRadius) = ReadEncounter(encounter, 1.0);
            var exact = encounter.GetProperty("exact");
            var s1 = new StateVector(r1, v1, earth, tca, Frames.Frame.ICRF, c1 is { } p1 ? BuildStateCovarianceFromPosition(p1) : null);
            var s2 = new StateVector(r2, v2, earth, tca, Frames.Frame.ICRF, c2 is { } p2 ? BuildStateCovarianceFromPosition(p2) : null);
            var combined = c1 is null ? c2 : c2 is null ? c1 : Add(c1.Value, c2.Value);
            var (miss2D, cov2D) = ConjunctionAssessment.ProjectOntoEncounterPlane(r2 - r1, v2 - v1, combined!.Value);

            // Act
            bool maximum = exact.GetProperty("maximum").GetBoolean();
            var pc = maximum
                ? CollisionProbability.FrisbeeMax(s1, s2, hardBodyRadius)
                : CollisionProbability.Foster2D(s1, s2, hardBodyRadius);

            // Assert: the internal integral bit for bit, within its bound of the exact Pc; the other method refuses
            var expected = maximum
                ? ConjunctionAssessment.ComputeSingleCovarianceMaximumCollisionProbability(miss2D, cov2D, hardBodyRadius)
                : ConjunctionAssessment.ComputeFosterCollisionProbability(miss2D, cov2D, hardBodyRadius);
            Assert.Equal(expected, pc);
            Assert.True(RelativeError(pc!.Value, exact.GetProperty("pc")) <= DesignedEncounterPcBound);
            if (maximum)
            {
                Assert.Null(CollisionProbability.Foster2D(s1, s2, hardBodyRadius));
            }
            else
            {
                Assert.Equal("object2", Assert.Throws<ArgumentException>(() => CollisionProbability.FrisbeeMax(s1, s2, hardBodyRadius)).ParamName);
            }
        }
    }

    [Fact]
    public void Foster2DAndFrisbeeMax_RefuseTheirArgumentsOutsideTheirContract()
    {
        // Arrange
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000TDB;
        var covariance = BuildStateCovarianceFromPosition(BuildPositionCovariance(new[,] { { 100.0, 0.0, 0.0 }, { 0.0, 400.0, 0.0 }, { 0.0, 0.0, 50.0 } }));
        var s1 = new StateVector(new Vector3(7.0e6, 0.0, 0.0), new Vector3(0.0, 7.5e3, 0.0), earth, tca, Frames.Frame.ICRF, covariance);
        var s2 = new StateVector(new Vector3(7.0e6, 10.0, 0.0), new Vector3(0.0, 0.0, 7.5e3), earth, tca, Frames.Frame.ICRF, covariance);
        var bare = new StateVector(s2.Position, s2.Velocity, earth, tca, Frames.Frame.ICRF);
        var bare1 = new StateVector(s1.Position, s1.Velocity, earth, tca, Frames.Frame.ICRF);

        // Assert: the radius finite and above 0
        foreach (var radius in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
        {
            Assert.Equal("combinedHardBodyRadiusMeters", Assert.Throws<ArgumentOutOfRangeException>(() => CollisionProbability.Foster2D(s1, s2, radius)).ParamName);
            Assert.Equal("combinedHardBodyRadiusMeters", Assert.Throws<ArgumentOutOfRangeException>(() => CollisionProbability.FrisbeeMax(s1, bare, radius)).ParamName);
        }

        // Two epochs, two frames, two observers
        var later = new StateVector(s2.Position, s2.Velocity, earth, tca.AddSeconds(1.0), Frames.Frame.ICRF, covariance);
        var ecliptic = s2.ToFrame(Frames.Frame.ECLIPTIC_J2000);
        var moon = new StateVector(s2.Position, s2.Velocity, new CelestialBody(SolarSystemObjects.PlanetsAndMoons.MOON), tca, Frames.Frame.ICRF, covariance);
        foreach (var other in new[] { later, ecliptic, moon })
        {
            Assert.Equal("object2", Assert.Throws<ArgumentException>(() => CollisionProbability.Foster2D(s1, other, 10.0)).ParamName);
        }

        // A covariance missing: no Foster Pc; none: no maximum Pc; null states refused
        Assert.Null(CollisionProbability.Foster2D(s1, bare, 10.0));
        Assert.Null(CollisionProbability.FrisbeeMax(bare1, bare, 10.0));
        Assert.NotNull(CollisionProbability.FrisbeeMax(s1, bare, 10.0));
        Assert.Throws<ArgumentNullException>(() => CollisionProbability.Foster2D(null, s2, 10.0));

        // A pair in one other inertial frame is the pair in ICRF
        Assert.Equal(CollisionProbability.Foster2D(s1, s2, 10.0)!.Value,
            CollisionProbability.Foster2D(s1.ToFrame(Frames.Frame.ECLIPTIC_J2000), s2.ToFrame(Frames.Frame.ECLIPTIC_J2000), 10.0)!.Value, 12);
    }

    [Fact]
    public void ToEncounter_DesignedEncounters_IsTheEncounterAtTheTcaOfTheCdm_ItsPcFoster2D()
    {
        foreach (var encounter in CollisionProbabilityReference.Value.RootElement.GetProperty("encounters").EnumerateArray())
        {
            // Arrange: the CDM of the encounter, its states in EME2000 (m), its covariances in RTN (the lower triangle of
            // the reference, km²), the screening period around TCA
            var exact = encounter.GetProperty("exact");
            var cdm = DesignedEncounterCdm(encounter);
            double hardBodyRadius = encounter.GetProperty("hbr").GetDouble() * 1000.0;

            // Act
            var result = cdm.ToEncounter(hardBodyRadius);

            // Assert: at the TCA of the CDM, its states as given, the Pc that of Foster2D or FrisbeeMax bit for bit
            var s1 = cdm.ToStateVector(CdmObjectRole.Object1);
            var s2 = cdm.ToStateVector(CdmObjectRole.Object2);
            LabelAssert.Equal(cdm.RelativeMetadataData.Tca, result.Tca);
            Assert.Equal(s1.Position, result.ProtectedState.Position);
            Assert.Equal(s2.Velocity, result.SecondaryState.Velocity);
            Assert.Equal((s2.Position - s1.Position).Magnitude(), result.MissDistanceMeters);
            bool maximum = exact.GetProperty("maximum").GetBoolean();
            Assert.Equal(maximum ? CollisionProbability.FrisbeeMax(s1, s2, hardBodyRadius) : CollisionProbability.Foster2D(s1, s2, hardBodyRadius),
                result.ProbabilityOfCollision);
            Assert.True(RelativeError(result.ProbabilityOfCollision!.Value, exact.GetProperty("pc")) <= DesignedEncounterAnalyzePcBound);
            Assert.Equal(maximum, result.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.SingleCovarianceMaximumPcUsed));
            Assert.Equal(exact.GetProperty("remediated").GetBoolean(), result.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.CovarianceRemediated));
            Assert.Equal(hardBodyRadius, result.CollisionRisk.CombinedHardBodyRadiusMeters);
            Assert.Equal(CloseApproachSearch.None, result.Search);
            Assert.Equal(TrajectorySource.TwoBody, result.EncounterState.ProtectedTrajectory);
            LabelAssert.Equal(new Window(cdm.RelativeMetadataData.StartScreenPeriod!.Value, cdm.RelativeMetadataData.StopScreenPeriod!.Value),
                result.ScreeningWindow);
        }
    }

    [Fact]
    public void ToEncounter_TheAvoidanceFallsBackOnTheTwoBodyOrbit_AndTheRadiusIsChecked()
    {
        // Arrange
        var cdm = DesignedEncounterCdm(CollisionProbabilityReference.Value.RootElement.GetProperty("encounters")[0]);

        // Act
        var encounter = cdm.ToEncounter(15.0);
        var study = ConjunctionAssessment.EvaluateAvoidance(encounter, InputRuleAvoidance);

        // Assert: the options of the two-body orbit, a lead of 15 min inside the screening period
        Assert.Equal(6, study.Options.Count);
        Assert.All(study.Options, o => Assert.Equal(TrajectorySource.TwoBody, o.Trajectory));
        foreach (var radius in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
        {
            Assert.Equal("combinedHardBodyRadiusMeters", Assert.Throws<ArgumentOutOfRangeException>(() => cdm.ToEncounter(radius)).ParamName);
        }
    }

    // A CDM of a designed encounter of tools/collision_probability: the states in EME2000 (m, m/s), each covariance in
    // its RTN (the reference's lower triangle R, TR, TT, NR, NT, NN in km², a 6x6 position block in m²), TCA 2026-01-01
    // 00:00:00 UTC, the screening period one hour about it
    private static Cdm DesignedEncounterCdm(JsonElement encounter)
    {
        var tca = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        CdmSegment Segment(CdmObjectRole role, string r, string v, string covariance)
        {
            Vector3 Read(string name) => new Vector3(encounter.GetProperty(name)[0].GetDouble(), encounter.GetProperty(name)[1].GetDouble(),
                encounter.GetProperty(name)[2].GetDouble()) * 1000.0;
            CdmCovarianceMatrix matrix = null;
            if (encounter.GetProperty(covariance).ValueKind != JsonValueKind.Null)
            {
                var l = encounter.GetProperty(covariance).EnumerateArray().Select(x => x.GetDouble() * 1.0e6).ToArray();
                var position = new[,] { { l[0], l[1], l[3] }, { l[1], l[2], l[4] }, { l[3], l[4], l[5] } };
                matrix = CdmCovarianceMatrix.FromRtnCovariance(Matrix.Create(6, 6, (i, j) => i < 3 && j < 3 ? position[i, j] : 0.0));
            }

            return new CdmSegment
            {
                Metadata = new CdmParticipantMetadata { Object = role, ObjectName = role.ToString(), ReferenceFrame = CdmReferenceFrame.Eme2000 },
                Data = new CdmData
                {
                    StateVector = new CdmStateVector { PositionMeters = Read(r), VelocityMetersPerSecond = Read(v) },
                    CovarianceMatrix = matrix
                }
            };
        }

        return new Cdm(new CdmHeader { CreationDateUtc = tca, Originator = "TEST", MessageId = "PC" },
            new CdmRelativeMetadataData
            {
                TcaUtc = tca,
                StartScreenPeriodUtc = tca.AddHours(-1.0),
                StopScreenPeriodUtc = tca.AddHours(1.0)
            },
            new List<CdmSegment> { Segment(CdmObjectRole.Object1, "r1", "v1", "cov1_rtn"), Segment(CdmObjectRole.Object2, "r2", "v2", "cov2_rtn") });
    }
}
