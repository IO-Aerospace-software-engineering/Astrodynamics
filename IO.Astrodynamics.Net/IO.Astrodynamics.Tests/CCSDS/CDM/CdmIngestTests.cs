// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.CDM;

/// <summary>
/// The ingest of a CDM (R19 of #668, lot L7): <see cref="Cdm.ToStateVector"/>, the state of an object at TCA with its
/// covariance turned from RTN, and the <see cref="TimeSystem.Time"/> accessors beside the <see cref="DateTime"/> ones.
/// </summary>
public class CdmIngestTests
{
    private static readonly DateTime TcaUtc = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Position1 = new(6_778_137.0, 1000.0, -2000.0);
    private static readonly Vector3 Velocity1 = new(10.0, 6_000.0, 4_300.0);
    private static readonly Vector3 Position2 = new(6_778_000.0, 1500.0, -1800.0);
    private static readonly Vector3 Velocity2 = new(-15.0, 4_300.0, 6_000.0);

    public CdmIngestTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    // A covariance in RTN with correlations, m², m²/s, m²/s²
    private static Matrix CovarianceRtn(double scale) => Matrix.Create(6, 6, (i, j) =>
        i == j ? scale * (i < 3 ? 100.0 * (i + 1) : 1.0e-4 * (i - 2)) : i < 3 && j < 3 ? scale * 20.0 : 0.0);

    private static Cdm Message(CdmReferenceFrame? frame, string? orbitCenter = null, bool covariance = true, bool screeningPeriod = true)
    {
        CdmSegment Segment(CdmObjectRole role, Vector3 position, Vector3 velocity, double scale) => new()
        {
            Metadata = new CdmParticipantMetadata { Object = role, ReferenceFrame = frame, OrbitCenter = orbitCenter },
            Data = new CdmData
            {
                StateVector = new CdmStateVector { PositionMeters = position, VelocityMetersPerSecond = velocity },
                CovarianceMatrix = covariance ? CdmCovarianceMatrix.FromRtnCovariance(CovarianceRtn(scale)) : null,
                OdParameters = new CdmOdParameters { TimeLastObservationStartUtc = TcaUtc.AddDays(-2.0), TimeLastObservationEndUtc = TcaUtc.AddDays(-1.0) }
            }
        };

        return new Cdm(new CdmHeader { CreationDateUtc = TcaUtc.AddHours(-6.0), Originator = "TEST", MessageId = "INGEST" },
            new CdmRelativeMetadataData
            {
                TcaUtc = TcaUtc,
                StartScreenPeriodUtc = screeningPeriod ? TcaUtc.AddDays(-1.0) : null,
                StopScreenPeriodUtc = screeningPeriod ? TcaUtc.AddDays(1.0) : null,
                ScreenEntryTimeUtc = TcaUtc.AddSeconds(-30.0),
                ScreenExitTimeUtc = TcaUtc.AddSeconds(30.0)
            },
            new List<CdmSegment> { Segment(CdmObjectRole.Object1, Position1, Velocity1, 1.0), Segment(CdmObjectRole.Object2, Position2, Velocity2, 4.0) });
    }

    [Theory]
    [InlineData(CdmReferenceFrame.Eme2000)]
    [InlineData(CdmReferenceFrame.Gcrf)]
    public void ToStateVector_InertialFrames_IsTheStateInIcrf_ItsCovarianceTurnedFromRtn(CdmReferenceFrame frame)
    {
        // Act
        var cdm = Message(frame);
        var s1 = cdm.ToStateVector(CdmObjectRole.Object1);
        var s2 = cdm.ToStateVector(CdmObjectRole.Object2);

        // Assert: the state as given in ICRF about the Earth at TCA (UTC), the covariance the RTN covariance turned on that
        // state, as the conformance runner's to_inertial turned it before 11.0.0 (bit for bit)
        var epoch = new TimeSystem.Time(TcaUtc, TimeScale.UTC);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch);
        Assert.Equal(Frames.Frame.ICRF, s1.Frame);
        Assert.Equal(399, s1.Observer.NaifId);
        LabelAssert.Equal(epoch, s1.Epoch);
        Assert.Equal(Position1, s1.Position);
        Assert.Equal(Velocity2, s2.Velocity);
        Assert.Equal(new StateVector(Position1, Velocity1, earth, epoch, Frames.Frame.ICRF).RotateCovarianceFromRtn(CovarianceRtn(1.0)), s1.Covariance);
        Assert.Equal(new StateVector(Position2, Velocity2, earth, epoch, Frames.Frame.ICRF).RotateCovarianceFromRtn(CovarianceRtn(4.0)), s2.Covariance);

        // Without a covariance, the state alone
        Assert.Null(Message(frame, covariance: false).ToStateVector(CdmObjectRole.Object1).Covariance);
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void ToStateVector_Itrf_IsTheStateInItrf93_ItsCovarianceTurnedFromRtnOnTheInertialState()
    {
        // Act
        var s1 = Message(CdmReferenceFrame.Itrf).ToStateVector(CdmObjectRole.Object1);

        // Assert: the state of the CDM in ITRF93; its covariance M P Mᵀ, P the RTN covariance turned on the inertial state
        // and M the state transformation of SPICE from J2000 to ITRF93 at TCA (sxform_c), the inertial state from it too
        var epoch = new TimeSystem.Time(TcaUtc, TimeScale.UTC);
        Assert.Equal(Frames.Frame.ITRF93, s1.Frame);
        Assert.Equal(Position1, s1.Position);
        Assert.Equal(Velocity1, s1.Velocity);
        double et = epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds;
        var toItrf = Body.CspiceDirect.Sxform("J2000", "ITRF93", et);
        var fromItrf = Body.CspiceDirect.Sxform("ITRF93", "J2000", et);
        var m = Matrix.Create(6, 6, (i, j) => toItrf[6 * i + j]);
        double[] itrf = [Position1.X, Position1.Y, Position1.Z, Velocity1.X, Velocity1.Y, Velocity1.Z];
        var j2000 = new double[6];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
                j2000[i] += fromItrf[6 * i + j] * itrf[j];
        }

        var inertialCovariance = new StateVector(new Vector3(j2000[0], j2000[1], j2000[2]), new Vector3(j2000[3], j2000[4], j2000[5]),
            s1.Observer, epoch, Frames.Frame.ICRF).RotateCovarianceFromRtn(CovarianceRtn(1.0));
        var expected = m * inertialCovariance * m.Transpose();
        var actual = s1.Covariance!.Value;
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double scale = System.Math.Sqrt(expected[i, i] * expected[j, j]);
                Assert.True(System.Math.Abs(actual[i, j] - expected[i, j]) <= 1.0e-12 * scale, $"({i}, {j}): {actual[i, j]:R} against {expected[i, j]:R}");
            }
        }
    }

    [Fact]
    public void ToStateVector_TheOrbitCenter_IsTheObserver_AnUnknownOneThrows()
    {
        Assert.Equal(399, Message(CdmReferenceFrame.Eme2000, "earth ").ToStateVector(CdmObjectRole.Object1).Observer.NaifId);
        Assert.Equal(301, Message(CdmReferenceFrame.Eme2000, "MOON").ToStateVector(CdmObjectRole.Object2).Observer.NaifId);
        Assert.Contains("ORBIT_CENTER", Assert.Throws<InvalidOperationException>(() =>
            Message(CdmReferenceFrame.Eme2000, "DSS-14").ToStateVector(CdmObjectRole.Object1)).Message);
        Assert.Contains("ORBIT_CENTER", Assert.Throws<InvalidOperationException>(() =>
            Message(CdmReferenceFrame.Itrf, "MOON").ToStateVector(CdmObjectRole.Object1)).Message);
        Assert.Contains("REF_FRAME", Assert.Throws<InvalidOperationException>(() =>
            Message(null).ToStateVector(CdmObjectRole.Object1)).Message);
        Assert.Equal("role", Assert.Throws<ArgumentOutOfRangeException>(() =>
            Message(CdmReferenceFrame.Eme2000).ToStateVector((CdmObjectRole)2)).ParamName);
    }

    [Fact]
    public void ToStateVector_ASegmentWithoutState_Throws()
    {
        var cdm = Message(CdmReferenceFrame.Eme2000);
        var bare = new Cdm(cdm.Header, cdm.RelativeMetadataData,
            new List<CdmSegment> { cdm.Segments[0] with { Data = null }, cdm.Segments[1] });

        Assert.Throws<InvalidOperationException>(() => bare.ToStateVector(CdmObjectRole.Object1));
        Assert.Throws<InvalidOperationException>(() => bare.ToEncounter(10.0));
    }

    /// <summary>
    /// A CDM without <c>START_SCREEN_PERIOD</c> and <c>STOP_SCREEN_PERIOD</c> (both optional in CCSDS 508.0-B-1) gives an
    /// encounter whose screening window is the TCA alone, so <see cref="ConjunctionAssessment.EvaluateAvoidance"/> gives
    /// no option and lists every lead time in <see cref="AvoidanceStudy.Skipped"/> as before the window (the review of
    /// lot L7 of #668: the docs claimed an avoidance had its burns within the window in every case).
    /// </summary>
    [Fact]
    public void ToEncounter_WithoutScreeningPeriod_TheWindowIsTheTca_AnAvoidanceSkipsEveryLeadTime()
    {
        var encounter = Message(CdmReferenceFrame.Eme2000, screeningPeriod: false).ToEncounter(10.0);

        Assert.Equal(TimeSpan.Zero, encounter.ScreeningWindow.Length);
        Assert.Equal(encounter.Tca, encounter.ScreeningWindow.StartDate);
        var study = ConjunctionAssessment.EvaluateAvoidance(encounter);
        Assert.Empty(study.Options);
        Assert.Equal(AvoidanceSearchOptions.Default.LeadTimes.Count, study.Skipped.Count);
        Assert.All(study.Skipped, x => Assert.Equal(AvoidanceSkipReason.BeforeWindow, x.Reason));
    }

    [Fact]
    public void TimeAccessors_AreTheUtcDates()
    {
        var cdm = Message(CdmReferenceFrame.Eme2000);
        var relative = cdm.RelativeMetadataData;
        var od = cdm.Segments[0].Data!.OdParameters!;

        LabelAssert.Equal(new TimeSystem.Time(TcaUtc.AddHours(-6.0), TimeScale.UTC), cdm.Header.CreationDate);
        LabelAssert.Equal(new TimeSystem.Time(TcaUtc, TimeScale.UTC), relative.Tca);
        LabelAssert.Equal(new TimeSystem.Time(TcaUtc.AddDays(-1.0), TimeScale.UTC), relative.StartScreenPeriod);
        LabelAssert.Equal(new TimeSystem.Time(TcaUtc.AddDays(1.0), TimeScale.UTC), relative.StopScreenPeriod);
        LabelAssert.Equal(new TimeSystem.Time(TcaUtc.AddSeconds(-30.0), TimeScale.UTC), relative.ScreenEntryTime);
        LabelAssert.Equal(new TimeSystem.Time(TcaUtc.AddSeconds(30.0), TimeScale.UTC), relative.ScreenExitTime);
        LabelAssert.Equal(new TimeSystem.Time(TcaUtc.AddDays(-2.0), TimeScale.UTC), od.TimeLastObservationStart);
        LabelAssert.Equal(new TimeSystem.Time(TcaUtc.AddDays(-1.0), TimeScale.UTC), od.TimeLastObservationEnd);

        // An unspecified kind is UTC, as the writer takes it (#617); an absent date is null
        var unspecified = relative with { TcaUtc = DateTime.SpecifyKind(TcaUtc, DateTimeKind.Unspecified), StartScreenPeriodUtc = null };
        LabelAssert.Equal(new TimeSystem.Time(TcaUtc, TimeScale.UTC), unspecified.Tca);
        Assert.Null(unspecified.StartScreenPeriod);
    }
}
