using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.SSA;

public class ConjunctionAssessmentTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");

    public ConjunctionAssessmentTests()
    {
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
    }

    // ========== TCA Detection Tests ==========

    [Fact]
    public void FindCloseApproaches_MoonSun_RangeRateZeroAtEachTca()
    {
        // Verify each detected TCA has range-rate ≈ 0 and is a local distance minimum
        var earth = CreateEarth();
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB);
        var sun = new CelestialBody(Stars.Sun, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB);

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(60));

        var tcas = ConjunctionAssessment.FindCloseApproaches(
            moon, sun, window, TimeSpan.FromHours(6), 1e-6, earth);

        // Moon-Sun distance oscillates ~2 times per synodic month (~29.5 days)
        Assert.True(tcas.Count >= 2, $"Expected ≥2 TCAs over 60 days, got {tcas.Count}");

        foreach (var tca in tcas)
        {
            Assert.True(window.Intersects(tca), "TCA must be within the screening window");

            // At TCA, range-rate (r_rel · v_rel) should be ≈ 0
            var moonState = moon.GetEphemeris(tca, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var sunState = sun.GetEphemeris(tca, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var relPos = sunState.Position - moonState.Position;
            var relVel = sunState.Velocity - moonState.Velocity;
            double rangeRate = relPos * relVel;
            double rangeRateNormalized = rangeRate / (relPos.Magnitude() * relVel.Magnitude());
            Assert.True(System.Math.Abs(rangeRateNormalized) < 1e-12,
                $"Range-rate at TCA should be ≈ 0, normalized value = {rangeRateNormalized:E6}");

            // Distance at TCA should be a local minimum: d(TCA) < d(TCA ± 1 hour)
            double distAtTca = relPos.Magnitude();
            var moonBefore = moon.GetEphemeris(tca - TimeSpan.FromHours(1), earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var sunBefore = sun.GetEphemeris(tca - TimeSpan.FromHours(1), earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            double distBefore = (sunBefore.Position - moonBefore.Position).Magnitude();

            var moonAfter = moon.GetEphemeris(tca + TimeSpan.FromHours(1), earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            var sunAfter = sun.GetEphemeris(tca + TimeSpan.FromHours(1), earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
            double distAfter = (sunAfter.Position - moonAfter.Position).Magnitude();

            Assert.True(distAtTca <= distBefore,
                $"Distance at TCA ({distAtTca:E6}) should be ≤ distance 1h before ({distBefore:E6})");
            Assert.True(distAtTca <= distAfter,
                $"Distance at TCA ({distAtTca:E6}) should be ≤ distance 1h after ({distAfter:E6})");
        }
    }

    [Fact]
    public void FindCloseApproaches_ConstantRelativeState_ReturnsEmpty()
    {
        var earth = CreateEarth();
        // Un-propagated spacecraft have constant ephemeris → constant range-rate → no zero-crossing
        var protected1 = CreateSpacecraft(-2301, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary1 = CreateSpacecraft(-2302, "S", earth,
            new Vector3(6_800_000.0, 5000.0, 0.0),
            new Vector3(0.0, 7_646.0, 0.0));

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0));

        var tcas = ConjunctionAssessment.FindCloseApproaches(
            protected1, secondary1, window,
            TimeSpan.FromSeconds(5), 1e-9, earth);

        Assert.Empty(tcas);
    }

    [Fact]
    public void FindCloseApproaches_TcaAtWindowStart_IsDetected()
    {
        var earth = CreateEarth();
        var start = TimeSystem.Time.J2000TDB;
        var window = new Window(start, start.AddSeconds(30.0));
        var primary = new LinearMotionLocalizable(-2303, "P", earth, start, Vector3.Zero, Vector3.Zero);
        var secondary = new LinearMotionLocalizable(
            -2304, "S", earth, start,
            new Vector3(0.0, 100.0, 0.0),
            new Vector3(10.0, 0.0, 0.0));

        var tcas = ConjunctionAssessment.FindCloseApproaches(
            primary, secondary, window,
            TimeSpan.FromSeconds(10.0), 1e-9, earth);

        var tca = Assert.Single(tcas);
        Assert.True(System.Math.Abs((tca - start).TotalSeconds) < 1e-6,
            $"Expected TCA at window start, got offset {(tca - start).TotalSeconds:E6}s");
    }

    [Fact]
    public void FindCloseApproaches_CrossingOnlyInFinalSubstep_IsDetected()
    {
        var earth = CreateEarth();
        var start = TimeSystem.Time.J2000TDB;
        var expectedTca = start.AddSeconds(5.0);
        var window = new Window(start, start.AddSeconds(9.0));
        var primary = new LinearMotionLocalizable(-2305, "P", earth, start, Vector3.Zero, Vector3.Zero);
        var secondary = new LinearMotionLocalizable(
            -2306, "S", earth, start,
            new Vector3(-50.0, 100.0, 0.0),
            new Vector3(10.0, 0.0, 0.0));

        var tcas = ConjunctionAssessment.FindCloseApproaches(
            primary, secondary, window,
            TimeSpan.FromSeconds(30.0), 1e-9, earth);

        var tca = Assert.Single(tcas);
        Assert.True(System.Math.Abs((tca - expectedTca).TotalSeconds) < 1e-6,
            $"Expected TCA at {expectedTca}, got offset {(tca - expectedTca).TotalSeconds:E6}s");
    }

    [Fact]
    public void FindCloseApproaches_MultipleTcasInsideSingleSampleInterval_AreDetected()
    {
        var earth = CreateEarth();
        var start = TimeSystem.Time.J2000TDB;
        var window = new Window(start.AddSeconds(0.6), start.AddSeconds(2.4));
        var primary = new LinearMotionLocalizable(-2307, "P", earth, start, Vector3.Zero, Vector3.Zero);
        var secondary = new OscillatingLocalizable(
            -2308,
            "S",
            earth,
            start,
            new Vector3(10.0, 0.0, 0.0),
            Vector3.VectorY,
            amplitudeMeters: 100.0,
            angularFrequencyRadiansPerSecond: System.Math.PI);

        var tcas = ConjunctionAssessment.FindCloseApproaches(
            primary,
            secondary,
            window,
            TimeSpan.FromSeconds(3.0),
            TimeSpan.FromMilliseconds(200.0),
            1e-9,
            earth);

        Assert.Equal(2, tcas.Count);
        Assert.True(System.Math.Abs((tcas[0] - start.AddSeconds(1.0)).TotalSeconds) < 1e-6,
            $"Expected first TCA near t=1s, got {(tcas[0] - start).TotalSeconds:E6}s");
        Assert.True(System.Math.Abs((tcas[1] - start.AddSeconds(2.0)).TotalSeconds) < 1e-6,
            $"Expected second TCA near t=2s, got {(tcas[1] - start).TotalSeconds:E6}s");
    }

    [Fact]
    public void FindCloseApproaches_NoTcaFallback_AnalyzeReturnsValidEncounter()
    {
        var earth = CreateEarth();
        // Co-moving spacecraft → no zero-crossing → Analyze falls back to window midpoint
        var protected1 = CreateSpacecraft(-2201, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary1 = CreateSpacecraft(-2202, "S", earth,
            new Vector3(6_800_000.0, 0.0, 50.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0));
        var profile = new ProtectedSpacecraftProfile(protected1);
        var expectedMidpoint = window.StartDate + TimeSpan.FromSeconds(
            (window.EndDate - window.StartDate).TotalSeconds * 0.5);

        var encounter = ConjunctionAssessment.Analyze(profile, secondary1, window);

        Assert.True(encounter.EncounterState.MissDistanceMeters > 0.0);
        // Fallback should place TCA at window midpoint
        double deltaToMidpoint = System.Math.Abs((encounter.EncounterState.Epoch - expectedMidpoint).TotalSeconds);
        Assert.True(deltaToMidpoint == 0.0, $"TCA should be near midpoint, offset={deltaToMidpoint:F3}s");
    }

    [Fact]
    public void AnalyzeAll_NoTcaFallback_ReturnsSingleMidpointEncounter()
    {
        var earth = CreateEarth();
        var protected1 = CreateSpacecraft(-2203, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary1 = CreateSpacecraft(-2204, "S", earth,
            new Vector3(6_800_000.0, 0.0, 100.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0));
        var expectedMidpoint = window.StartDate + TimeSpan.FromSeconds(
            (window.EndDate - window.StartDate).TotalSeconds * 0.5);
        var profile = new ProtectedSpacecraftProfile(protected1);

        var encounters = ConjunctionAssessment.AnalyzeAll(profile, secondary1, window);

        var encounter = Assert.Single(encounters);
        Assert.Equal(expectedMidpoint, encounter.EncounterState.Epoch);
        Assert.True(encounter.EncounterState.MissDistanceMeters > 0.0);
    }

    [Fact]
    public void AnalyzeAll_MoonSun_ReturnsMultipleEncountersWithDistinctEpochs()
    {
        var earth = CreateEarth();
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB);
        var sun = new CelestialBody(Stars.Sun, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB);
        var profile = new ProtectedSpacecraftProfile(
            CreateSpacecraftFromBody(moon, -5501, "MOON_SC", 1737400.0));
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(60));

        var encounters = ConjunctionAssessment.AnalyzeAll(profile, sun, window,
            new ConjunctionAnalysisOptions { SampleStep = TimeSpan.FromHours(6) });

        Assert.True(encounters.Count >= 2, $"Expected ≥2 encounters over 60 days, got {encounters.Count}");

        // Each encounter must have a distinct epoch and positive miss distance
        for (int i = 0; i < encounters.Count; i++)
        {
            Assert.True(window.Intersects(encounters[i].EncounterState.Epoch));
            Assert.True(encounters[i].EncounterState.MissDistanceMeters > 0.0);
            // Epochs must be distinct (separated by at least 1 day for Moon-Sun geometry)
            for (int j = i + 1; j < encounters.Count; j++)
            {
                double separation = System.Math.Abs(
                    (encounters[i].EncounterState.Epoch - encounters[j].EncounterState.Epoch).TotalDays);
                Assert.True(separation > 1.0,
                    $"Encounters #{i} and #{j} separated by only {separation:F2} days");
            }
        }
    }

    [Fact]
    public void AnalyzeAll_DenseTrajectorySolutions_DetectMultipleTcasWithCoarseSearchSettings()
    {
        var earth = CreateEarth();
        var start = TimeSystem.Time.J2000TDB;
        var window = new Window(start.AddSeconds(0.6), start.AddSeconds(2.4));
        var protectedSpacecraft = CreateSpacecraft(
            -5503,
            "P",
            earth,
            Vector3.Zero,
            Vector3.Zero);
        var secondarySpacecraft = CreateSpacecraft(
            -5504,
            "S",
            earth,
            new Vector3(10.0, 0.0, 0.0),
            new Vector3(0.0, 100.0 * System.Math.PI, 0.0));
        var profile = new ProtectedSpacecraftProfile(protectedSpacecraft);

        var protectedTrajectory = CreatePropagationSolution(
            earth,
            start,
            durationSeconds: 3.0,
            stepSeconds: 0.25,
            stateAtSeconds: _ => (Vector3.Zero, Vector3.Zero, Vector3.Zero));
        var secondaryTrajectory = CreatePropagationSolution(
            earth,
            start,
            durationSeconds: 3.0,
            stepSeconds: 0.25,
            stateAtSeconds: t =>
            {
                double phase = System.Math.PI * t;
                return (
                    new Vector3(10.0, 100.0 * System.Math.Sin(phase), 0.0),
                    new Vector3(0.0, 100.0 * System.Math.PI * System.Math.Cos(phase), 0.0),
                    new Vector3(0.0, -100.0 * System.Math.PI * System.Math.PI * System.Math.Sin(phase), 0.0));
            });

        var encounters = ConjunctionAssessment.AnalyzeAll(
            profile,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromSeconds(160.0),
                MaximumEventSearchStep = TimeSpan.FromSeconds(10.0)
            });

        Assert.Equal(2, encounters.Count);
        Assert.True(System.Math.Abs((encounters[0].EncounterState.Epoch - start.AddSeconds(1.0)).TotalSeconds) < 1e-6,
            $"Expected first dense TCA near t=1s, got {(encounters[0].EncounterState.Epoch - start).TotalSeconds:E6}s");
        Assert.True(System.Math.Abs((encounters[1].EncounterState.Epoch - start.AddSeconds(2.0)).TotalSeconds) < 1e-6,
            $"Expected second dense TCA near t=2s, got {(encounters[1].EncounterState.Epoch - start).TotalSeconds:E6}s");
    }

    [Fact]
    public void AnalyzeAll_DenseSingleAcceptedStep_DetectsMultipleInteriorMinimaWithoutIntermediateKnots()
    {
        var earth = CreateEarth();
        var start = TimeSystem.Time.J2000TDB;
        var window = new Window(start, start.AddSeconds(3.0));
        var protectedSpacecraft = CreateSpacecraft(-5505, "P", earth, Vector3.Zero, Vector3.Zero);
        var secondarySpacecraft = CreateSpacecraft(-5506, "S", earth, new Vector3(10.0, 0.0, 0.0), Vector3.Zero);
        var profile = new ProtectedSpacecraftProfile(protectedSpacecraft);
        const double root1 = 0.75;
        const double root2 = 1.50;
        const double root3 = 2.25;
        const double scale = 80.0;

        var protectedTrajectory = CreatePropagationSolution(
            earth,
            start,
            durationSeconds: 3.0,
            stepSeconds: 3.0,
            stateAtSeconds: _ => (Vector3.Zero, Vector3.Zero, Vector3.Zero));
        var secondaryTrajectory = CreatePropagationSolution(
            earth,
            start,
            durationSeconds: 3.0,
            stepSeconds: 3.0,
            stateAtSeconds: t =>
            {
                double displacement = scale * (t - root1) * (t - root2) * (t - root3);
                double speed = scale * (
                    (t - root2) * (t - root3) +
                    (t - root1) * (t - root3) +
                    (t - root1) * (t - root2));
                double acceleration = scale * (6.0 * t - 2.0 * (root1 + root2 + root3));
                return (
                    new Vector3(10.0, displacement, 0.0),
                    new Vector3(0.0, speed, 0.0),
                    new Vector3(0.0, acceleration, 0.0));
            });

        var encounters = ConjunctionAssessment.AnalyzeAll(
            profile,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaximumEventSearchStep = TimeSpan.FromDays(1.0),
                SecondaryHardBodyRadiusMeters = 0.0,
                LowRelativeSpeedThresholdMetersPerSecond = 0.0
            });

        Assert.Equal(3, encounters.Count);
        Assert.True(System.Math.Abs((encounters[0].EncounterState.Epoch - start.AddSeconds(root1)).TotalSeconds) < 1e-6);
        Assert.True(System.Math.Abs((encounters[1].EncounterState.Epoch - start.AddSeconds(root2)).TotalSeconds) < 1e-6);
        Assert.True(System.Math.Abs((encounters[2].EncounterState.Epoch - start.AddSeconds(root3)).TotalSeconds) < 1e-6);
        Assert.All(encounters, encounter => Assert.Equal(10.0, encounter.EncounterState.MissDistanceMeters, 9));
    }

    [Fact]
    public void Analyze_DenseMonotonicApproach_SelectsWindowBoundaryInsteadOfMidpoint()
    {
        var earth = CreateEarth();
        var start = TimeSystem.Time.J2000TDB;
        var end = start.AddSeconds(2.0);
        var window = new Window(start, end);
        var protectedSpacecraft = CreateSpacecraft(-5507, "P", earth, Vector3.Zero, Vector3.Zero);
        var secondarySpacecraft = CreateSpacecraft(-5508, "S", earth, new Vector3(10.0, 200.0, 0.0), new Vector3(0.0, -100.0, 0.0));
        var profile = new ProtectedSpacecraftProfile(protectedSpacecraft);

        var protectedTrajectory = CreatePropagationSolution(
            earth,
            start,
            durationSeconds: 2.0,
            stepSeconds: 2.0,
            stateAtSeconds: _ => (Vector3.Zero, Vector3.Zero, Vector3.Zero));
        var secondaryTrajectory = CreatePropagationSolution(
            earth,
            start,
            durationSeconds: 2.0,
            stepSeconds: 2.0,
            stateAtSeconds: t => (
                new Vector3(10.0, 200.0 - 100.0 * t, 0.0),
                new Vector3(0.0, -100.0, 0.0),
                Vector3.Zero));

        var encounter = ConjunctionAssessment.Analyze(
            profile,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaximumEventSearchStep = TimeSpan.FromDays(1.0),
                SecondaryHardBodyRadiusMeters = 0.0,
                LowRelativeSpeedThresholdMetersPerSecond = 0.0
            });

        Assert.Equal(end, encounter.EncounterState.Epoch);
        Assert.Equal(10.0, encounter.EncounterState.MissDistanceMeters, 9);
    }

    // ========== Correctness Tests ==========

    [Fact]
    public void Analyze_MissDistanceMatchesIndependentComputation()
    {
        // Validate that reported miss distance matches an independent computation
        // using the same objects at the reported TCA epoch
        var earth = CreateEarth();
        var sun = new CelestialBody(Stars.Sun, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB);

        var moonSc = CreateSpacecraftFromBody(moon, -5601, "MOON_SC", 1737400.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddDays(30));
        var profile = new ProtectedSpacecraftProfile(moonSc);

        var encounter = ConjunctionAssessment.Analyze(profile, sun, window,
            new ConjunctionAnalysisOptions { SampleStep = TimeSpan.FromHours(6) });

        // Independently query the SAME objects (spacecraft + Sun) at TCA epoch
        var scAtTca = moonSc.GetEphemeris(encounter.EncounterState.Epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        var sunAtTca = sun.GetEphemeris(encounter.EncounterState.Epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        double independentDistance = (sunAtTca.Position - scAtTca.Position).Magnitude();

        Assert.True(System.Math.Abs(encounter.EncounterState.MissDistanceMeters - independentDistance) < 1e-06,
            $"Miss distance {encounter.EncounterState.MissDistanceMeters:F1}m should match independent {independentDistance:F1}m");
    }

    [Fact]
    public void RtnRotation_PreservesVectorMagnitude()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-2501, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(spacecraft);
        var secondary = CreateSpacecraft(-2502, "S", earth,
            new Vector3(6_800_000.0, 100.0, 50.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddSeconds(1.0));

        var encounter = ConjunctionAssessment.Analyze(profile, secondary, window,
            new ConjunctionAnalysisOptions { SampleStep = TimeSpan.FromSeconds(0.5) });

        var rtn = encounter.EncounterState.RelativeState.RelativePositionRtn;
        var inertial = encounter.EncounterState.RelativeState.RelativePositionInertial;

        // RTN rotation is orthogonal → must preserve magnitude exactly
        Assert.True(System.Math.Abs(rtn.Magnitude() - inertial.Magnitude()) < 1e-12,
            $"RTN magnitude {rtn.Magnitude():F6} must equal inertial magnitude {inertial.Magnitude():F6}");
        // Miss distance must equal inertial relative position magnitude
        Assert.True(System.Math.Abs(encounter.EncounterState.MissDistanceMeters - inertial.Magnitude()) < 1e-12,
            "MissDistanceMeters must equal |relativePositionInertial|");
    }

    [Fact]
    public void Analyze_IsotropicCovariancesAreSummedAndRemainIsotropicInRtn()
    {
        var earth = CreateEarth();
        var protected1 = CreateSpacecraft(-2503, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary1 = CreateSpacecraft(-2504, "S", earth,
            new Vector3(6_800_000.0, 100.0, 25.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(protected1);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(5.0));

        var encounter = ConjunctionAssessment.Analyze(profile, secondary1, window);
        var combinedCovariance = encounter.EncounterState.CombinedCovarianceRtn;

        for (int i = 0; i < 3; i++)
        {
            Assert.True(System.Math.Abs(combinedCovariance.Get(i, i) - 50.0) < 1e-12,
                $"Position covariance diagonal #{i} should equal 50 m^2, got {combinedCovariance.Get(i, i):E6}");
        }

        for (int i = 3; i < 6; i++)
        {
            Assert.True(System.Math.Abs(combinedCovariance.Get(i, i) - 0.02) < 1e-12,
                $"Velocity covariance diagonal #{i} should equal 0.02 m^2/s^2, got {combinedCovariance.Get(i, i):E6}");
        }

        for (int row = 0; row < combinedCovariance.Rows; row++)
        {
            for (int column = 0; column < combinedCovariance.Columns; column++)
            {
                if (row == column)
                {
                    continue;
                }

                Assert.True(System.Math.Abs(combinedCovariance.Get(row, column)) < 1e-12,
                    $"Off-diagonal covariance ({row},{column}) should remain zero, got {combinedCovariance.Get(row, column):E6}");
            }
        }
    }

    [Fact]
    public void Analyze_CollisionRiskSigmasMatchCombinedRtnCovariance()
    {
        var earth = CreateEarth();
        var protected1 = CreateSpacecraft(-2505, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary1 = CreateSpacecraft(-2506, "S", earth,
            new Vector3(6_800_000.0, 100.0, 50.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(protected1);
        var encounter = ConjunctionAssessment.Analyze(
            profile,
            secondary1,
            new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(5.0)));

        Assert.Equal(System.Math.Sqrt(encounter.EncounterState.CombinedCovarianceRtn.Get(0, 0)), encounter.CollisionRisk.RadialSigmaMeters, 12);
        Assert.Equal(System.Math.Sqrt(encounter.EncounterState.CombinedCovarianceRtn.Get(1, 1)), encounter.CollisionRisk.InTrackSigmaMeters, 12);
        Assert.Equal(System.Math.Sqrt(encounter.EncounterState.CombinedCovarianceRtn.Get(2, 2)), encounter.CollisionRisk.CrossTrackSigmaMeters, 12);
    }

    [Fact]
    public void EncounterPlane_ProjectedMissMagnitudeEqualsInPlaneComponent()
    {
        // Analytical verification: projected miss magnitude = |miss - (miss·v̂)v̂|
        var relPos = new Vector3(100.0, 50.0, 30.0);
        var relVel = new Vector3(1000.0, 2000.0, 500.0);
        var cov = new Matrix(3, 3);
        cov.Set(0, 0, 100.0); cov.Set(1, 1, 200.0); cov.Set(2, 2, 150.0);

        var (miss2D, cov2D) = ConjunctionAssessment.ProjectOntoEncounterPlane(relPos, relVel, cov);

        // Analytical in-plane miss
        var vHat = relVel.Normalize();
        double missAlongV = relPos * vHat;
        var missInPlane = relPos - vHat * missAlongV;
        double expectedMag = missInPlane.Magnitude();
        double actualMag = System.Math.Sqrt(miss2D[0] * miss2D[0] + miss2D[1] * miss2D[1]);

        Assert.True(System.Math.Abs(actualMag - expectedMag) < 1e-12,
            $"Projected miss magnitude {actualMag:F6} must match analytical {expectedMag:F6}");

        // Covariance must be 2x2 symmetric positive semi-definite
        Assert.Equal(2, cov2D.Rows);
        Assert.Equal(2, cov2D.Columns);
        Assert.True(cov2D.Get(0, 0) >= 0.0, "Cov(0,0) must be non-negative");
        Assert.True(cov2D.Get(1, 1) >= 0.0, "Cov(1,1) must be non-negative");
        Assert.True(System.Math.Abs(cov2D.Get(0, 1) - cov2D.Get(1, 0)) < 1e-12,
            "Projected covariance must be symmetric");
    }

    [Fact]
    public void EncounterPlane_DiagonalCovariance_ProjectsCorrectly()
    {
        // With diagonal covariance C=diag(σx², σy², σz²) and encounter plane spanned by e1, e2,
        // the projected covariance C2D(i,j) = Σ_k e_i[k]² σ_k² δ_{ij} when off-diagonal C=0
        // Specifically: C2D(0,0) = e1.x²·σx² + e1.y²·σy² + e1.z²·σz²
        var relPos = new Vector3(0.0, 100.0, 0.0);
        var relVel = new Vector3(1.0, 0.0, 0.0); // velocity along X → encounter plane is Y-Z
        double sx2 = 400.0, sy2 = 900.0, sz2 = 1600.0;
        var cov = new Matrix(3, 3);
        cov.Set(0, 0, sx2); cov.Set(1, 1, sy2); cov.Set(2, 2, sz2);

        var (miss2D, cov2D) = ConjunctionAssessment.ProjectOntoEncounterPlane(relPos, relVel, cov);

        // v̂ = (1,0,0), miss = (0,100,0), miss in plane = (0,100,0)
        // e1 = (0,1,0), e2 = v̂ × e1 = (1,0,0)×(0,1,0) = (0,0,1)
        // miss2D = (100, 0)
        Assert.True(System.Math.Abs(miss2D[0] - 100.0) < 1e-12, $"miss2D[0] should be 100, got {miss2D[0]}");
        Assert.True(System.Math.Abs(miss2D[1]) < 1e-12, $"miss2D[1] should be 0, got {miss2D[1]}");

        // C2D(0,0) = e1·C·e1 = σy² = 900
        // C2D(1,1) = e2·C·e2 = σz² = 1600
        // C2D(0,1) = e1·C·e2 = 0
        Assert.True(System.Math.Abs(cov2D.Get(0, 0) - sy2) < 1e-12,
            $"C2D(0,0) should be σy²={sy2}, got {cov2D.Get(0, 0)}");
        Assert.True(System.Math.Abs(cov2D.Get(1, 1) - sz2) < 1e-12,
            $"C2D(1,1) should be σz²={sz2}, got {cov2D.Get(1, 1)}");
        Assert.True(System.Math.Abs(cov2D.Get(0, 1)) < 1e-12,
            $"C2D(0,1) should be 0, got {cov2D.Get(0, 1)}");
    }

    [Fact]
    public void EncounterPlane_MissParallelToVelocity_ProducesZeroProjectedMiss()
    {
        var relPos = new Vector3(0.0, 250.0, 0.0);
        var relVel = new Vector3(0.0, 10.0, 0.0);
        double sx2 = 25.0, sy2 = 49.0, sz2 = 81.0;
        var cov = new Matrix(3, 3);
        cov.Set(0, 0, sx2);
        cov.Set(1, 1, sy2);
        cov.Set(2, 2, sz2);

        var (miss2D, cov2D) = ConjunctionAssessment.ProjectOntoEncounterPlane(relPos, relVel, cov);

        Assert.True(System.Math.Abs(miss2D[0]) < 1e-12, $"miss2D[0] should be 0, got {miss2D[0]}");
        Assert.True(System.Math.Abs(miss2D[1]) < 1e-12, $"miss2D[1] should be 0, got {miss2D[1]}");
        Assert.True(System.Math.Abs(cov2D.Get(0, 0) - sx2) < 1e-12,
            $"Projected X variance should remain {sx2}, got {cov2D.Get(0, 0)}");
        Assert.True(System.Math.Abs(cov2D.Get(1, 1) - sz2) < 1e-12,
            $"Projected Z variance should remain {sz2}, got {cov2D.Get(1, 1)}");
        Assert.True(System.Math.Abs(cov2D.Get(0, 1)) < 1e-12,
            $"Projected covariance should remain diagonal, got {cov2D.Get(0, 1)}");
    }

    [Fact]
    public void CollisionProbability_ZeroMiss_SymmetricCovariance_MatchesAnalytic()
    {
        // For zero miss and circular covariance: Pc = 1 - exp(-R²/(2σ²))
        double sigma = 100.0;
        double radius = 10.0;
        var miss2D = new[] { 0.0, 0.0 };
        var cov2D = new Matrix(2, 2);
        cov2D.Set(0, 0, sigma * sigma);
        cov2D.Set(1, 1, sigma * sigma);

        var pc = ConjunctionAssessment.ComputeFosterCollisionProbability(miss2D, cov2D, radius);

        Assert.NotNull(pc);
        double expected = 1.0 - System.Math.Exp(-radius * radius / (2.0 * sigma * sigma));
        Assert.True(System.Math.Abs(pc.Value - expected) < 2e-8,
            $"Expected Pc={expected:E6}, got {pc.Value:E6}");
    }

    [Fact]
    public void CollisionProbability_NonZeroMiss_SymmetricCovariance_MatchesIndependentMidpointIntegration()
    {
        double sigma = 50.0;
        double radius = 5.0;
        double missX = 30.0, missY = 40.0; // d = 50m
        var miss2D = new[] { missX, missY };
        var cov2D = new Matrix(2, 2);
        cov2D.Set(0, 0, sigma * sigma);
        cov2D.Set(1, 1, sigma * sigma);

        var pc = ConjunctionAssessment.ComputeFosterCollisionProbability(miss2D, cov2D, radius);
        double expected = IntegrateIsotropicGaussianOverCircleByMidpointRule(miss2D, sigma, radius);

        Assert.NotNull(pc);
        Assert.True(System.Math.Abs(pc.Value - expected) < 5e-6,
            $"Expected Pc={expected:E10}, got {pc.Value:E10}");
    }

    [Fact]
    public void CollisionProbability_LargeMissDistance_NearZero()
    {
        // 100 km miss, 10 m sigma → Pc ≈ 0
        double sigma = 10.0;
        double radius = 10.0;
        var miss2D = new[] { 100_000.0, 0.0 };
        var cov2D = new Matrix(2, 2);
        cov2D.Set(0, 0, sigma * sigma);
        cov2D.Set(1, 1, sigma * sigma);

        var pc = ConjunctionAssessment.ComputeFosterCollisionProbability(miss2D, cov2D, radius);

        Assert.NotNull(pc);
        Assert.True(pc.Value < 1e-15, $"Pc should be near zero for large miss, got {pc.Value:E6}");
    }

    [Fact]
    public void CollisionProbability_NullWhenCovarianceUnavailable()
    {
        var earth = CreateEarth();
        var protectedSpacecraft = CreateSpacecraftWithoutCovariance(-2701, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary = CreateSpacecraftWithoutCovariance(-2702, "S", earth,
            new Vector3(6_800_000.0, 100.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(protectedSpacecraft);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(5.0));

        var encounter = ConjunctionAssessment.Analyze(profile, secondary, window);

        Assert.Null(encounter.CollisionRisk.ProbabilityOfCollision);
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.CovarianceUnavailable));
    }

    [Fact]
    public void CollisionProbability_AsymmetricCovariance_BoundedByCircularCases()
    {
        // For elliptical covariance, Pc should be bounded between circular cases
        // using the larger and smaller eigenvalue
        double sigmaMax = 100.0, sigmaMin = 10.0;
        double radius = 5.0;
        double miss = 20.0;
        var miss2D = new[] { miss, 0.0 };
        var cov2D = new Matrix(2, 2);
        cov2D.Set(0, 0, sigmaMax * sigmaMax);
        cov2D.Set(1, 1, sigmaMin * sigmaMin);

        var pc = ConjunctionAssessment.ComputeFosterCollisionProbability(miss2D, cov2D, radius);

        Assert.NotNull(pc);
        Assert.True(pc.Value > 0.0, "Pc should be positive for nearby miss");
        Assert.True(pc.Value < 1.0, "Pc should be less than 1");

        // Upper bound: Pc with smaller sigma (tighter → higher Pc if miss < sigma)
        // Lower bound: Pc with larger sigma (wider → lower Pc for same miss)
        double pcLargerSigma = (1.0 - System.Math.Exp(-radius * radius / (2.0 * sigmaMax * sigmaMax)))
                               * System.Math.Exp(-miss * miss / (2.0 * sigmaMax * sigmaMax));
        Assert.True(pc.Value >= pcLargerSigma * 0.1,
            $"Pc={pc.Value:E6} should not be orders of magnitude below circular lower bound {pcLargerSigma:E6}");
    }

    [Fact]
    public void CollisionProbability_NearSingularCovariance_IsClippedToFiniteValue()
    {
        // Official CARA tooling clips very small encounter-plane eigenvalues before computing Pc.
        var miss2D = new[] { 10.0, 0.0 };
        var cov2D = new Matrix(2, 2);
        cov2D.Set(0, 0, 100.0);
        cov2D.Set(1, 1, 1e-25); // near-singular

        var pc = ConjunctionAssessment.ComputeFosterCollisionProbability(miss2D, cov2D, 5.0);

        Assert.NotNull(pc);
        Assert.True(pc.Value > 0.0, $"Clipped Pc should remain positive, got {pc:E6}");
        Assert.True(pc.Value < 1.0, $"Pc must remain bounded, got {pc:E6}");
    }

    [Fact]
    public void CollisionProbability_DecreasesWithMissDistance()
    {
        double sigma = 75.0;
        double radius = 10.0;
        var cov2D = new Matrix(2, 2);
        cov2D.Set(0, 0, sigma * sigma);
        cov2D.Set(1, 1, sigma * sigma);

        var near = ConjunctionAssessment.ComputeFosterCollisionProbability(new[] { 0.0, 0.0 }, cov2D, radius);
        var medium = ConjunctionAssessment.ComputeFosterCollisionProbability(new[] { 50.0, 0.0 }, cov2D, radius);
        var far = ConjunctionAssessment.ComputeFosterCollisionProbability(new[] { 150.0, 0.0 }, cov2D, radius);

        Assert.NotNull(near);
        Assert.NotNull(medium);
        Assert.NotNull(far);
        Assert.True(near > medium, $"Pc should decrease with miss distance: near={near:E6}, medium={medium:E6}");
        Assert.True(medium > far, $"Pc should decrease with miss distance: medium={medium:E6}, far={far:E6}");
    }

    [Fact]
    public void CollisionProbability_RotationInvariantForMissAndCovariance()
    {
        double radius = 12.0;
        double theta = System.Math.PI / 3.0;
        double cosTheta = System.Math.Cos(theta);
        double sinTheta = System.Math.Sin(theta);
        var miss2D = new[] { 30.0, -40.0 };
        var cov2D = new Matrix(2, 2);
        cov2D.Set(0, 0, 900.0);
        cov2D.Set(0, 1, 120.0);
        cov2D.Set(1, 0, 120.0);
        cov2D.Set(1, 1, 400.0);

        var baseline = ConjunctionAssessment.ComputeFosterCollisionProbability(miss2D, cov2D, radius);

        var rotation = new Matrix(2, 2);
        rotation.Set(0, 0, cosTheta);
        rotation.Set(0, 1, -sinTheta);
        rotation.Set(1, 0, sinTheta);
        rotation.Set(1, 1, cosTheta);
        var rotatedMiss = rotation * miss2D;
        var rotatedCovariance = rotation * cov2D * rotation.Transpose();

        var rotated = ConjunctionAssessment.ComputeFosterCollisionProbability(rotatedMiss, rotatedCovariance, radius);

        Assert.NotNull(baseline);
        Assert.NotNull(rotated);
        Assert.True(System.Math.Abs(baseline.Value - rotated.Value) < 1e-12,
            $"Pc should be rotation invariant. Baseline={baseline:E12}, rotated={rotated:E12}");
    }

    // ========== Published Collision-Probability Conformance (NASA CARA) ==========
    // These tests isolate the Pc implementation against published reference values.

    [Fact]
    public void CollisionProbability_NasaOmitronReferenceCase_MatchesPublishedValue()
    {
        // NASA CARA Pc2D_Foster_UnitTest.m Omitron Test Case 01.
        var r1 = new Vector3(378.39559, 4305.721887, 5752.767554);
        var v1 = new Vector3(2.360800244, 5.580331936, -4.322349039);
        var cov1 = BuildPositionCovariance(new[,]
        {
            { 44.5757544811362, 81.6751751052616, -67.8687662707124 },
            { 81.6751751052616, 158.4534029561630, -128.6169216448570 },
            { -67.8687662707124, -128.6169216448580, 105.4905425627010 }
        });
        var r2 = new Vector3(374.5180598, 4307.560983, 5751.130418);
        var v2 = new Vector3(-5.388125081, -3.946827739, 3.322820358);
        var cov2 = BuildPositionCovariance(new[,]
        {
            { 2.31067077720423, 1.69905293875632, -1.41701645776610 },
            { 1.69905293875632, 1.24957388457206, -1.04174164279599 },
            { -1.41701645776610, -1.04174164279599, 0.869260558223714 }
        });

        var pc = ComputeFosterProbabilityFromInertialInputs(r1, v1, cov1, r2, v2, cov2, 0.020);

        Assert.NotNull(pc);
        Assert.True(System.Math.Abs(pc.Value - 2.70601573490125e-05) / 2.70601573490125e-05 < 1.0e-5,
            $"Expected NASA Omitron Pc≈2.70601573490125E-05, got {pc.Value:E12}");
    }

    [Fact]
    public void CollisionProbability_NasaAlfanoCase03_MatchesPublishedValue()
    {
        // NASA CARA Pc2D_Foster_UnitTest.m Alfano case 03 expected solution = 1.00351176E-01.
        var r1 = new Vector3(153.951475, 41874.153995, 0.0);
        var v1 = new Vector3(3.066874624, -0.011411025, 0.0);
        var cov1Rtn = BuildPositionCovariance(new[,]
        {
            { 1.988980036134080e+01, -3.524149328959712e+02, 0.0 },
            { -3.524149328959712e+02, 6.496747606851101e+03, 0.0 },
            { 0.0, 0.0, 1.205040573210700e+00 }
        }, scale: 1.0 / 1e6);
        var r2 = new Vector3(153.951973, 41874.156745, 0.002752);
        var v2 = new Vector3(3.066864623, -0.000044999, -0.011356027);
        var cov2Rtn = BuildPositionCovariance(new[,]
        {
            { 1.746930568576392e+01, -3.305057350225742e+02, -1.279563505801461e-15 },
            { -3.305057350225742e+02, 6.542324010830698e+03, -4.449721840993348e-13 },
            { -1.279563505801461e-15, -4.449721840993348e-13, 1.177810899317289e+00 }
        }, scale: 1.0 / 1e6);

        var cov1 = RotateRtnPositionCovarianceToInertial(r1, v1, cov1Rtn);
        var cov2 = RotateRtnPositionCovarianceToInertial(r2, v2, cov2Rtn);

        var pc = ComputeFosterProbabilityFromInertialInputs(r1, v1, cov1, r2, v2, cov2, 0.015);

        Assert.NotNull(pc);
        Assert.True(System.Math.Abs(pc.Value - 1.00351176e-01) / 1.00351176e-01 < 1.0e-5,
            $"Expected NASA Alfano case 03 Pc≈1.00351176E-01, got {pc.Value:E12}");
    }

    // ========== Published Single-Covariance Maximum-Pc Conformance (NASA Frisbee) ==========

    [Fact]
    public void SingleCovarianceMaximumPc_FrisbeeCriticalCovarianceMatchesPublishedMatrix()
    {
        // Frisbee AAS 15-717, Eqs. 11-14.
        var miss2D = new[] { 1000.0, 200.0 };
        var knownCov2D = new Matrix(2, 2);
        knownCov2D.Set(0, 0, 722500.0);
        knownCov2D.Set(1, 1, 2500.0);

        var criticalCovariance = ConjunctionAssessment.BuildMaximumProbabilityEncounterPlaneCovariance(miss2D, knownCov2D);

        Assert.NotNull(criticalCovariance);
        var expected = new Matrix(2, 2);
        expected.Set(0, 0, 261401250.0 / 157.0);
        expected.Set(0, 1, 29593750.0 / 157.0);
        expected.Set(1, 0, 29593750.0 / 157.0);
        expected.Set(1, 1, 6311250.0 / 157.0);
        AssertMatrixApproximatelyEqual(expected, criticalCovariance.Value, 1.0e-6);
    }

    [Theory]
    [InlineData(5.0, 0.000043)]
    [InlineData(10.0, 0.000171)]
    [InlineData(20.0, 0.000685)]
    [InlineData(25.0, 0.001070)]
    public void SingleCovarianceMaximumPc_FrisbeeTable1_MatchesPublishedApproximateOutcomes(double hardBodyRadius, double expectedPc)
    {
        // Frisbee AAS 15-717, Table 1 using the Eq. 11 example geometry.
        var miss2D = new[] { 1000.0, 200.0 };
        var knownCov2D = new Matrix(2, 2);
        knownCov2D.Set(0, 0, 722500.0);
        knownCov2D.Set(1, 1, 2500.0);

        var pc = ConjunctionAssessment.ComputeSingleCovarianceMaximumCollisionProbability(miss2D, knownCov2D, hardBodyRadius);

        Assert.NotNull(pc);
        Assert.True(System.Math.Abs(pc.Value - expectedPc) / expectedPc < 0.03,
            $"Expected NASA Frisbee max Pc≈{expectedPc:E12}, got {pc.Value:E12}");
    }

    // ========== Published Workflow Conformance (NASA CARA Through Analyze/AnalyzeAll) ==========
    // These tests validate that the entry points preserve the published reference geometry,
    // covariance handling, and final Pc values through the full SSA workflow.

    [Fact]
    public void Analyze_NasaOmitronReferenceCase_MatchesPublishedValueThroughEntryPoint()
    {
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000TDB.AddSeconds(10.0);
        var window = new Window(tca.AddSeconds(-10.0), tca.AddSeconds(10.0));
        const double combinedHardBodyRadius = 0.020;

        var r1 = new Vector3(378.39559, 4305.721887, 5752.767554);
        var v1 = new Vector3(2.360800244, 5.580331936, -4.322349039);
        var cov1 = BuildPositionCovariance(new[,]
        {
            { 44.5757544811362, 81.6751751052616, -67.8687662707124 },
            { 81.6751751052616, 158.4534029561630, -128.6169216448570 },
            { -67.8687662707124, -128.6169216448580, 105.4905425627010 }
        });
        var r2 = new Vector3(374.5180598, 4307.560983, 5751.130418);
        var v2 = new Vector3(-5.388125081, -3.946827739, 3.322820358);
        var cov2 = BuildPositionCovariance(new[,]
        {
            { 2.31067077720423, 1.69905293875632, -1.41701645776610 },
            { 1.69905293875632, 1.24957388457206, -1.04174164279599 },
            { -1.41701645776610, -1.04174164279599, 0.869260558223714 }
        });

        var protectedSpacecraft = CreateSpacecraft(
            -5701,
            "OMITRON_P",
            earth,
            r1,
            v1,
            BuildStateCovarianceFromPosition(cov1),
            hardBodyRadius: combinedHardBodyRadius);
        var secondarySpacecraft = CreateSpacecraft(
            -5702,
            "OMITRON_S",
            earth,
            r2,
            v2,
            BuildStateCovarianceFromPosition(cov2),
            hardBodyRadius: 0.0);
        var profile = new ProtectedSpacecraftProfile(protectedSpacecraft);

        var protectedTrajectory = CreatePropagationSolution(
            earth,
            window.StartDate,
            durationSeconds: (window.EndDate - window.StartDate).TotalSeconds,
            stepSeconds: 20.0,
            stateAtSeconds: seconds =>
            {
                double dt = seconds - 10.0;
                return (r1 + v1 * dt, v1, Vector3.Zero);
            });
        var secondaryTrajectory = CreatePropagationSolution(
            earth,
            window.StartDate,
            durationSeconds: (window.EndDate - window.StartDate).TotalSeconds,
            stepSeconds: 20.0,
            stateAtSeconds: seconds =>
            {
                double dt = seconds - 10.0;
                return (r2 + v2 * dt, v2, Vector3.Zero);
            });

        var encounter = ConjunctionAssessment.Analyze(
            profile,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaximumEventSearchStep = TimeSpan.FromDays(1.0),
                SecondaryHardBodyRadiusMeters = 0.0,
                LowRelativeSpeedThresholdMetersPerSecond = 0.0
            });

        Assert.True(System.Math.Abs((encounter.EncounterState.Epoch - tca).TotalSeconds) < 1e-4,
            $"Expected TCA at {tca}, got {encounter.EncounterState.Epoch}");
        double dt = (encounter.EncounterState.Epoch - tca).TotalSeconds;
        AssertEncounterMatchesDirectExpectation(
            encounter,
            r1 + v1 * dt,
            v1,
            cov1,
            r2 + v2 * dt,
            v2,
            cov2,
            expectedCombinedHardBodyRadius: combinedHardBodyRadius,
            expectedPublishedPc: 2.70601573490125e-05,
            publishedPcRelativeTolerance: 1.0e-5);
    }

    [Fact]
    public void AnalyzeAll_NasaAlfanoCase03_MatchesPublishedValueThroughEntryPoint()
    {
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000TDB.AddSeconds(10.0);
        var window = new Window(tca.AddSeconds(-10.0), tca.AddSeconds(10.0));
        const double combinedHardBodyRadius = 0.015;

        var r1 = new Vector3(153.951475, 41874.153995, 0.0);
        var v1 = new Vector3(3.066874624, -0.011411025, 0.0);
        var cov1Rtn = BuildPositionCovariance(new[,]
        {
            { 1.988980036134080e+01, -3.524149328959712e+02, 0.0 },
            { -3.524149328959712e+02, 6.496747606851101e+03, 0.0 },
            { 0.0, 0.0, 1.205040573210700e+00 }
        }, scale: 1.0 / 1e6);
        var r2 = new Vector3(153.951973, 41874.156745, 0.002752);
        var v2 = new Vector3(3.066864623, -0.000044999, -0.011356027);
        var cov2Rtn = BuildPositionCovariance(new[,]
        {
            { 1.746930568576392e+01, -3.305057350225742e+02, -1.279563505801461e-15 },
            { -3.305057350225742e+02, 6.542324010830698e+03, -4.449721840993348e-13 },
            { -1.279563505801461e-15, -4.449721840993348e-13, 1.177810899317289e+00 }
        }, scale: 1.0 / 1e6);
        var cov1 = RotateRtnPositionCovarianceToInertial(r1, v1, cov1Rtn);
        var cov2 = RotateRtnPositionCovarianceToInertial(r2, v2, cov2Rtn);

        var protectedSpacecraft = CreateSpacecraft(
            -5703,
            "ALFANO_P",
            earth,
            r1,
            v1,
            BuildStateCovarianceFromPosition(cov1),
            hardBodyRadius: combinedHardBodyRadius);
        var secondarySpacecraft = CreateSpacecraft(
            -5704,
            "ALFANO_S",
            earth,
            r2,
            v2,
            BuildStateCovarianceFromPosition(cov2),
            hardBodyRadius: 0.0);
        var profile = new ProtectedSpacecraftProfile(protectedSpacecraft);

        var protectedTrajectory = CreatePropagationSolution(
            earth,
            window.StartDate,
            durationSeconds: (window.EndDate - window.StartDate).TotalSeconds,
            stepSeconds: 20.0,
            stateAtSeconds: seconds =>
            {
                double dt = seconds - 10.0;
                return (r1 + v1 * dt, v1, Vector3.Zero);
            });
        var secondaryTrajectory = CreatePropagationSolution(
            earth,
            window.StartDate,
            durationSeconds: (window.EndDate - window.StartDate).TotalSeconds,
            stepSeconds: 20.0,
            stateAtSeconds: seconds =>
            {
                double dt = seconds - 10.0;
                return (r2 + v2 * dt, v2, Vector3.Zero);
            });

        var encounters = ConjunctionAssessment.AnalyzeAll(
            profile,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaximumEventSearchStep = TimeSpan.FromDays(1.0),
                SecondaryHardBodyRadiusMeters = 0.0,
                LowRelativeSpeedThresholdMetersPerSecond = 0.0
            });

        var encounter = Assert.Single(encounters);
        Assert.True(System.Math.Abs((encounter.EncounterState.Epoch - tca).TotalSeconds) < 1e-4,
            $"Expected TCA at {tca}, got {encounter.EncounterState.Epoch}");
        double dt = (encounter.EncounterState.Epoch - tca).TotalSeconds;
        AssertEncounterMatchesDirectExpectation(
            encounter,
            r1 + v1 * dt,
            v1,
            cov1,
            r2 + v2 * dt,
            v2,
            cov2,
            expectedCombinedHardBodyRadius: combinedHardBodyRadius,
            expectedPublishedPc: 1.00351176e-01,
            publishedPcRelativeTolerance: 1.0e-5);
    }

    [Fact]
    public void AnalyzeAll_NasaOmitronReferenceCase_MatchesPublishedValueThroughEntryPoint()
    {
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000TDB.AddSeconds(10.0);
        var window = new Window(tca.AddSeconds(-10.0), tca.AddSeconds(10.0));
        const double combinedHardBodyRadius = 0.020;

        var r1 = new Vector3(378.39559, 4305.721887, 5752.767554);
        var v1 = new Vector3(2.360800244, 5.580331936, -4.322349039);
        var cov1 = BuildPositionCovariance(new[,]
        {
            { 44.5757544811362, 81.6751751052616, -67.8687662707124 },
            { 81.6751751052616, 158.4534029561630, -128.6169216448570 },
            { -67.8687662707124, -128.6169216448580, 105.4905425627010 }
        });
        var r2 = new Vector3(374.5180598, 4307.560983, 5751.130418);
        var v2 = new Vector3(-5.388125081, -3.946827739, 3.322820358);
        var cov2 = BuildPositionCovariance(new[,]
        {
            { 2.31067077720423, 1.69905293875632, -1.41701645776610 },
            { 1.69905293875632, 1.24957388457206, -1.04174164279599 },
            { -1.41701645776610, -1.04174164279599, 0.869260558223714 }
        });
        var profile = new ProtectedSpacecraftProfile(CreateSpacecraft(
            -5705,
            "OMITRON_P_ALL",
            earth,
            r1,
            v1,
            BuildStateCovarianceFromPosition(cov1),
            hardBodyRadius: combinedHardBodyRadius));
        var secondarySpacecraft = CreateSpacecraft(
            -5706,
            "OMITRON_S_ALL",
            earth,
            r2,
            v2,
            BuildStateCovarianceFromPosition(cov2),
            hardBodyRadius: 0.0);
        var protectedTrajectory = CreatePropagationSolution(
            earth,
            window.StartDate,
            durationSeconds: (window.EndDate - window.StartDate).TotalSeconds,
            stepSeconds: 20.0,
            stateAtSeconds: seconds =>
            {
                double dt = seconds - 10.0;
                return (r1 + v1 * dt, v1, Vector3.Zero);
            });
        var secondaryTrajectory = CreatePropagationSolution(
            earth,
            window.StartDate,
            durationSeconds: (window.EndDate - window.StartDate).TotalSeconds,
            stepSeconds: 20.0,
            stateAtSeconds: seconds =>
            {
                double dt = seconds - 10.0;
                return (r2 + v2 * dt, v2, Vector3.Zero);
            });

        var encounter = Assert.Single(ConjunctionAssessment.AnalyzeAll(
            profile,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaximumEventSearchStep = TimeSpan.FromDays(1.0),
                SecondaryHardBodyRadiusMeters = 0.0,
                LowRelativeSpeedThresholdMetersPerSecond = 0.0
            }));

        Assert.True(System.Math.Abs((encounter.EncounterState.Epoch - tca).TotalSeconds) < 1e-4);
        double dt = (encounter.EncounterState.Epoch - tca).TotalSeconds;
        AssertEncounterMatchesDirectExpectation(
            encounter,
            r1 + v1 * dt,
            v1,
            cov1,
            r2 + v2 * dt,
            v2,
            cov2,
            expectedCombinedHardBodyRadius: combinedHardBodyRadius,
            expectedPublishedPc: 2.70601573490125e-05,
            publishedPcRelativeTolerance: 1.0e-5);
    }

    [Fact]
    public void Analyze_NasaAlfanoCase03_MatchesPublishedValueThroughEntryPoint()
    {
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000TDB.AddSeconds(10.0);
        var window = new Window(tca.AddSeconds(-10.0), tca.AddSeconds(10.0));
        const double combinedHardBodyRadius = 0.015;

        var r1 = new Vector3(153.951475, 41874.153995, 0.0);
        var v1 = new Vector3(3.066874624, -0.011411025, 0.0);
        var cov1Rtn = BuildPositionCovariance(new[,]
        {
            { 1.988980036134080e+01, -3.524149328959712e+02, 0.0 },
            { -3.524149328959712e+02, 6.496747606851101e+03, 0.0 },
            { 0.0, 0.0, 1.205040573210700e+00 }
        }, scale: 1.0 / 1e6);
        var r2 = new Vector3(153.951973, 41874.156745, 0.002752);
        var v2 = new Vector3(3.066864623, -0.000044999, -0.011356027);
        var cov2Rtn = BuildPositionCovariance(new[,]
        {
            { 1.746930568576392e+01, -3.305057350225742e+02, -1.279563505801461e-15 },
            { -3.305057350225742e+02, 6.542324010830698e+03, -4.449721840993348e-13 },
            { -1.279563505801461e-15, -4.449721840993348e-13, 1.177810899317289e+00 }
        }, scale: 1.0 / 1e6);
        var cov1 = RotateRtnPositionCovarianceToInertial(r1, v1, cov1Rtn);
        var cov2 = RotateRtnPositionCovarianceToInertial(r2, v2, cov2Rtn);
        var protectedSpacecraft = CreateSpacecraft(
            -5707,
            "ALFANO_P_ANALYZE",
            earth,
            r1,
            v1,
            BuildStateCovarianceFromPosition(cov1),
            hardBodyRadius: combinedHardBodyRadius);
        var secondarySpacecraft = CreateSpacecraft(
            -5708,
            "ALFANO_S_ANALYZE",
            earth,
            r2,
            v2,
            BuildStateCovarianceFromPosition(cov2),
            hardBodyRadius: 0.0);
        var profile = new ProtectedSpacecraftProfile(protectedSpacecraft);
        var protectedTrajectory = CreatePropagationSolution(
            earth,
            window.StartDate,
            durationSeconds: (window.EndDate - window.StartDate).TotalSeconds,
            stepSeconds: 20.0,
            stateAtSeconds: seconds =>
            {
                double dt = seconds - 10.0;
                return (r1 + v1 * dt, v1, Vector3.Zero);
            });
        var secondaryTrajectory = CreatePropagationSolution(
            earth,
            window.StartDate,
            durationSeconds: (window.EndDate - window.StartDate).TotalSeconds,
            stepSeconds: 20.0,
            stateAtSeconds: seconds =>
            {
                double dt = seconds - 10.0;
                return (r2 + v2 * dt, v2, Vector3.Zero);
            });

        var encounter = ConjunctionAssessment.Analyze(
            profile,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaximumEventSearchStep = TimeSpan.FromDays(1.0),
                SecondaryHardBodyRadiusMeters = 0.0,
                LowRelativeSpeedThresholdMetersPerSecond = 0.0
            });

        Assert.True(System.Math.Abs((encounter.EncounterState.Epoch - tca).TotalSeconds) < 1e-4);
        double dt = (encounter.EncounterState.Epoch - tca).TotalSeconds;
        AssertEncounterMatchesDirectExpectation(
            encounter,
            r1 + v1 * dt,
            v1,
            cov1,
            r2 + v2 * dt,
            v2,
            cov2,
            expectedCombinedHardBodyRadius: combinedHardBodyRadius,
            expectedPublishedPc: 1.00351176e-01,
            publishedPcRelativeTolerance: 1.0e-5);
    }

    // ========== Screening Tests ==========

    [Fact]
    public void Screen_EmptyCandidates_ReturnsEmpty()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-2801, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(spacecraft);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0));

        var result = ConjunctionAssessment.Screen(profile, Array.Empty<ILocalizable>(), window);

        Assert.Empty(result);
    }

    [Fact]
    public void Screen_AllBeyondMaxMissDistance_ReturnsEmpty()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-2901, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var far = CreateSpacecraft(-2902, "FAR", earth,
            new Vector3(7_000_000.0, 0.0, 0.0), new Vector3(0.0, 7_546.0, 0.0));
        var profile = new ProtectedSpacecraftProfile(spacecraft);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0));

        var result = ConjunctionAssessment.Screen(profile,
            new ILocalizable[] { far }, window,
            new ScreeningOptions { MaxMissDistanceMeters = 1000.0 });

        Assert.Empty(result);
    }

    [Fact]
    public void Screen_SelfFiltering_ExcludesSameNaifId()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-3001, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(spacecraft);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0));

        var self = CreateSpacecraft(-3001, "SELF", earth,
            new Vector3(6_800_000.0, 10.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));

        var result = ConjunctionAssessment.Screen(profile,
            new ILocalizable[] { self }, window);

        Assert.Empty(result);
    }

    [Fact]
    public void Screen_CloserCandidateRankedFirst()
    {
        var earth = CreateEarth();
        var protectedSpacecraft = CreateSpacecraft(-1201, "PROTECTED", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var closeCandidate = CreateSpacecraft(-1202, "CLOSE", earth,
            new Vector3(6_800_000.0, 100.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var farCandidate = CreateSpacecraft(-1203, "FAR", earth,
            new Vector3(6_800_000.0, 5_000.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(protectedSpacecraft);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0));

        var encounters = ConjunctionAssessment.Screen(
            profile,
            new ILocalizable[] { farCandidate, closeCandidate },
            window,
            new ScreeningOptions
            {
                SampleStep = TimeSpan.FromMinutes(1),
                MaxMissDistanceMeters = 50_000.0,
                MaxResults = 5
            });

        Assert.Equal(2, encounters.Count);
        Assert.Equal("CLOSE", encounters[0].SecondaryObject.Name);
        Assert.True(encounters[0].EncounterState.MissDistanceMeters < encounters[1].EncounterState.MissDistanceMeters);
    }

    [Fact]
    public void Screen_HigherProbabilityCandidateIsRankedFirst()
    {
        var earth = CreateEarth();
        var protectedSpacecraft = CreateSpacecraft(-1204, "PROTECTED", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var lowProbabilityClose = CreateSpacecraft(-1205, "LOW_PC_CLOSE", earth,
            new Vector3(6_800_000.0, 50.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0),
            BuildCovariance(positionVariance: 1.0e-12, velocityVariance: 1.0e-6));
        var highProbabilityFar = CreateSpacecraft(-1206, "HIGH_PC_FAR", earth,
            new Vector3(6_800_000.0, 100.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0),
            BuildCovariance(positionVariance: 10_000.0, velocityVariance: 0.01));
        var profile = new ProtectedSpacecraftProfile(protectedSpacecraft);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0));

        var encounters = ConjunctionAssessment.Screen(
            profile,
            new ILocalizable[] { lowProbabilityClose, highProbabilityFar },
            window,
            new ScreeningOptions
            {
                SampleStep = TimeSpan.FromMinutes(1),
                MaxMissDistanceMeters = 1_000.0,
                MaxResults = 5
            });

        Assert.Equal(2, encounters.Count);
        Assert.Equal("HIGH_PC_FAR", encounters[0].SecondaryObject.Name);
        Assert.True(encounters[0].CollisionRisk.ProbabilityOfCollision > encounters[1].CollisionRisk.ProbabilityOfCollision);
        Assert.True(encounters[0].EncounterState.MissDistanceMeters > encounters[1].EncounterState.MissDistanceMeters);
    }

    // ========== Covariance Quality Tests ==========

    [Fact]
    public void Analyze_BothMissingCovariance_SetsUnavailableFlag()
    {
        var earth = CreateEarth();
        var p = CreateSpacecraftWithoutCovariance(-3101, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var s = CreateSpacecraftWithoutCovariance(-3102, "S", earth,
            new Vector3(6_800_000.0, 100.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(p);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(5.0));

        var encounter = ConjunctionAssessment.Analyze(profile, s, window);

        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.CovarianceUnavailable));
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.MissingProtectedCovariance));
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.MissingSecondaryCovariance));
        Assert.Null(encounter.CollisionRisk.ProbabilityOfCollision);
    }

    [Fact]
    public void Analyze_OneObjectMissingCovariance_UsesSingleCovarianceMaximumPc()
    {
        var earth = CreateEarth();
        var p = CreateSpacecraft(-3201, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var s = CreateSpacecraftWithoutCovariance(-3202, "S", earth,
            new Vector3(6_800_000.0, 100.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(p);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(5.0));

        var encounter = ConjunctionAssessment.Analyze(profile, s, window);

        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.MissingSecondaryCovariance));
        Assert.False(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.CovarianceUnavailable));
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.SingleCovarianceMaximumPcUsed));
        Assert.NotNull(encounter.CollisionRisk.ProbabilityOfCollision);
        Assert.True(encounter.CollisionRisk.ProbabilityOfCollision >= 0.0);
        Assert.True(encounter.CollisionRisk.ProbabilityOfCollision <= 1.0);
    }

    [Fact]
    public void Analyze_OmitronGeometryWithMissingSecondaryCovariance_UsesSingleCovarianceMaximumPcUpperBound()
    {
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000TDB.AddSeconds(10.0);
        var window = new Window(tca.AddSeconds(-10.0), tca.AddSeconds(10.0));
        const double combinedHardBodyRadius = 0.020;

        var r1 = new Vector3(378.39559, 4305.721887, 5752.767554);
        var v1 = new Vector3(2.360800244, 5.580331936, -4.322349039);
        var cov1 = BuildPositionCovariance(new[,]
        {
            { 44.5757544811362, 81.6751751052616, -67.8687662707124 },
            { 81.6751751052616, 158.4534029561630, -128.6169216448570 },
            { -67.8687662707124, -128.6169216448580, 105.4905425627010 }
        });
        var r2 = new Vector3(374.5180598, 4307.560983, 5751.130418);
        var v2 = new Vector3(-5.388125081, -3.946827739, 3.322820358);

        var protectedSpacecraft = CreateSpacecraft(
            -3203,
            "OMITRON_MAXPC_P",
            earth,
            r1,
            v1,
            BuildStateCovarianceFromPosition(cov1),
            hardBodyRadius: combinedHardBodyRadius);
        var secondarySpacecraft = CreateSpacecraftWithoutCovariance(
            -3204,
            "OMITRON_MAXPC_S",
            earth,
            r2,
            v2,
            hardBodyRadius: 0.0);
        var profile = new ProtectedSpacecraftProfile(protectedSpacecraft);
        var protectedTrajectory = CreatePropagationSolution(
            earth,
            window.StartDate,
            durationSeconds: (window.EndDate - window.StartDate).TotalSeconds,
            stepSeconds: 20.0,
            stateAtSeconds: seconds =>
            {
                double dt = seconds - 10.0;
                return (r1 + v1 * dt, v1, Vector3.Zero);
            });
        var secondaryTrajectory = CreatePropagationSolution(
            earth,
            window.StartDate,
            durationSeconds: (window.EndDate - window.StartDate).TotalSeconds,
            stepSeconds: 20.0,
            stateAtSeconds: seconds =>
            {
                double dt = seconds - 10.0;
                return (r2 + v2 * dt, v2, Vector3.Zero);
            });

        var encounter = ConjunctionAssessment.Analyze(
            profile,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaximumEventSearchStep = TimeSpan.FromDays(1.0),
                SecondaryHardBodyRadiusMeters = 0.0,
                LowRelativeSpeedThresholdMetersPerSecond = 0.0
            });

        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.MissingSecondaryCovariance));
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.SingleCovarianceMaximumPcUsed));
        Assert.NotNull(encounter.CollisionRisk.ProbabilityOfCollision);

        double dt = (encounter.EncounterState.Epoch - tca).TotalSeconds;
        var relativePosition = (r2 + v2 * dt) - (r1 + v1 * dt);
        var relativeVelocity = v2 - v1;
        var (miss2D, knownCov2D) = ConjunctionAssessment.ProjectOntoEncounterPlane(relativePosition, relativeVelocity, cov1);
        var maximumPc = ConjunctionAssessment.ComputeSingleCovarianceMaximumCollisionProbability(miss2D, knownCov2D, combinedHardBodyRadius);
        var directKnownOnlyPc = ConjunctionAssessment.ComputeFosterCollisionProbability(miss2D, knownCov2D, combinedHardBodyRadius);

        Assert.NotNull(maximumPc);
        Assert.NotNull(directKnownOnlyPc);
        Assert.True(maximumPc.Value >= directKnownOnlyPc.Value,
            $"Maximum Pc {maximumPc:E12} should bound direct known-only Pc {directKnownOnlyPc:E12}");
        Assert.True(System.Math.Abs(encounter.CollisionRisk.ProbabilityOfCollision.Value - maximumPc.Value) < 1.0e-12,
            $"Entry-point max Pc {encounter.CollisionRisk.ProbabilityOfCollision.Value:E12} should match direct max Pc {maximumPc.Value:E12}");
    }

    [Fact]
    public void Analyze_StaleCovarianceFallback_SetsFlag()
    {
        var earth = CreateEarth();
        var p = CreateSpacecraft(-3301, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var s = CreateSpacecraft(-3302, "S", earth,
            new Vector3(6_800_000.0, 100.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(p);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(5.0));

        var encounter = ConjunctionAssessment.Analyze(profile, s, window);

        // GetEphemeris returns states without covariance, so ResolveCovariance
        // falls back to InitialOrbitalParameters → StaleCovarianceUsed flag
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));
        // Pc should still be computed from the stale covariance
        Assert.NotNull(encounter.CollisionRisk.ProbabilityOfCollision);
    }

    [Fact]
    public void Analyze_TinyProjectedCovariance_IsRemediatedAndFlagged()
    {
        var earth = CreateEarth();
        var tinyCovariance = BuildCovariance(positionVariance: 1.0e-12, velocityVariance: 1.0e-12);
        var protectedSpacecraft = CreateSpacecraft(-3303, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0),
            tinyCovariance);
        var secondary = CreateSpacecraft(-3304, "S", earth,
            new Vector3(6_800_000.0, 10.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0),
            tinyCovariance);
        var encounter = ConjunctionAssessment.Analyze(
            new ProtectedSpacecraftProfile(protectedSpacecraft),
            secondary,
            new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(1.0)));

        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.CovarianceRemediated));
        Assert.NotNull(encounter.CollisionRisk.ProbabilityOfCollision);
        Assert.True(encounter.CollisionRisk.ProjectedCovariance.Get(0, 0) > 0.0);
        Assert.True(encounter.CollisionRisk.ProjectedCovariance.Get(1, 1) > 0.0);
    }

    [Fact]
    public void Analyze_LowRelativeVelocityEncounter_IsFlagged()
    {
        var earth = CreateEarth();
        var protectedSpacecraft = CreateSpacecraft(-3305, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary = CreateSpacecraft(-3306, "S", earth,
            new Vector3(6_800_000.0, 100.0, 0.0),
            new Vector3(0.0, 7_656.1704182967140, 0.0));

        var encounter = ConjunctionAssessment.Analyze(
            new ProtectedSpacecraftProfile(protectedSpacecraft),
            secondary,
            new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(1.0)),
            new ConjunctionAnalysisOptions
            {
                LowRelativeSpeedThresholdMetersPerSecond = 100.0
            });

        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.LowRelativeVelocityEncounter));
        Assert.True(encounter.EncounterState.RelativeState.RelativeVelocityInertial.Magnitude() < 100.0);
    }

    // ========== Avoidance Tests ==========

    [Fact]
    public void EvaluateAvoidance_ImprovesOrMaintainsMissDistance()
    {
        var earth = CreateEarth();
        var protectedSpacecraft = CreateSpacecraft(-1301, "PROTECTED", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var fuelTank = new FuelTank("MAIN", "MONO", "FT-001", 25.0, 25.0);
        protectedSpacecraft.AddFuelTank(fuelTank);
        protectedSpacecraft.AddEngine(new Engine("ENG", "RCS", "ENG-001", 220.0, 0.05, fuelTank));

        var secondarySpacecraft = CreateSpacecraft(-1302, "SECONDARY", earth,
            new Vector3(6_800_000.0, 120.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(
            protectedSpacecraft,
            new ManeuverConstraints
            {
                MaxDeltaVMetersPerSecond = 0.5,
                MinimumLeadTime = TimeSpan.FromMinutes(5),
                MaximumLeadTime = TimeSpan.FromHours(6)
            });

        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(3.0));
        var nominalEncounter = ConjunctionAssessment.Analyze(profile, secondarySpacecraft, window);

        var options = ConjunctionAssessment.EvaluateAvoidance(
            nominalEncounter,
            new AvoidanceSearchOptions
            {
                LeadTimes = new[] { TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30) },
                DeltaVMagnitudesMetersPerSecond = new[] { 0.05, 0.10 },
                AnalysisSampleStep = TimeSpan.FromMinutes(2),
                MaxReturnedOptions = 5
            });

        Assert.NotEmpty(options);
        // Best option should improve or maintain miss distance
        Assert.True(options[0].PostManeuverEncounter.EncounterState.MissDistanceMeters
                    >= nominalEncounter.EncounterState.MissDistanceMeters);
        Assert.True(options[0].FuelCostEstimateKilograms > 0.0,
            "Spacecraft with engine should have non-zero fuel cost");
        // Results must be sorted by ranking score
        Assert.True(options.SequenceEqual(options.OrderBy(option => option.RankingScore)));
    }

    [Fact]
    public void EvaluateAvoidance_NoEngines_ZeroFuelCost()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-3401, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary = CreateSpacecraft(-3402, "S", earth,
            new Vector3(6_800_000.0, 120.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(spacecraft,
            new ManeuverConstraints
            {
                MaxDeltaVMetersPerSecond = 0.5,
                MinimumLeadTime = TimeSpan.FromMinutes(5),
                MaximumLeadTime = TimeSpan.FromHours(6)
            });
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(3.0));
        var encounter = ConjunctionAssessment.Analyze(profile, secondary, window);

        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter,
            new AvoidanceSearchOptions
            {
                LeadTimes = new[] { TimeSpan.FromMinutes(30) },
                DeltaVMagnitudesMetersPerSecond = new[] { 0.1 },
                MaxReturnedOptions = 3
            });

        Assert.NotEmpty(avoidance);
        Assert.All(avoidance, opt => Assert.Equal(0.0, opt.FuelCostEstimateKilograms));
    }

    [Fact]
    public void EvaluateAvoidance_LeadTimeOutsideConstraints_ReturnsEmpty()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-3501, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary = CreateSpacecraft(-3502, "S", earth,
            new Vector3(6_800_000.0, 120.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(spacecraft,
            new ManeuverConstraints
            {
                MaxDeltaVMetersPerSecond = 0.5,
                MinimumLeadTime = TimeSpan.FromHours(1),
                MaximumLeadTime = TimeSpan.FromHours(2)
            });
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(3.0));
        var encounter = ConjunctionAssessment.Analyze(profile, secondary, window);

        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter,
            new AvoidanceSearchOptions
            {
                LeadTimes = new[] { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30) },
                DeltaVMagnitudesMetersPerSecond = new[] { 0.1 },
                MaxReturnedOptions = 10
            });

        Assert.Empty(avoidance);
    }

    [Fact]
    public void EvaluateAvoidance_RankedByDeltaVWhenPcMeetsThreshold()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-3601, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary = CreateSpacecraft(-3602, "S", earth,
            new Vector3(6_800_000.0, 100.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(spacecraft,
            new ManeuverConstraints
            {
                MaxDeltaVMetersPerSecond = 1.0,
                MinimumLeadTime = TimeSpan.FromMinutes(5),
                MaximumLeadTime = TimeSpan.FromHours(6)
            });
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddHours(3.0));
        var encounter = ConjunctionAssessment.Analyze(profile, secondary, window);

        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter,
            new AvoidanceSearchOptions
            {
                LeadTimes = new[] { TimeSpan.FromMinutes(30) },
                DeltaVMagnitudesMetersPerSecond = new[] { 0.05, 0.25, 0.50 },
                PcThreshold = 1.0, // very lenient → all meet threshold
                MaxReturnedOptions = 20
            });

        Assert.NotEmpty(avoidance);
        // All ranking scores < 1e9 means Pc met threshold; tiebreaker is delta-V
        Assert.All(avoidance, opt => Assert.True(opt.RankingScore < 1e9,
            $"RankingScore {opt.RankingScore} should be < 1e9 when PcThreshold=1.0"));
        Assert.True(avoidance.SequenceEqual(avoidance.OrderBy(o => o.RankingScore)));
    }

    [Fact]
    public void EvaluateAvoidance_FiltersInvalidLeadTimesAndDeltaV()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-3603, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary = CreateSpacecraft(-3604, "S", earth,
            new Vector3(6_800_000.0, 0.0, 120.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var profile = new ProtectedSpacecraftProfile(spacecraft,
            new ManeuverConstraints
            {
                MaxDeltaVMetersPerSecond = 0.2,
                MinimumLeadTime = TimeSpan.FromMinutes(1),
                MaximumLeadTime = TimeSpan.FromMinutes(10)
            });
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB.AddMinutes(10.0));
        var encounter = ConjunctionAssessment.Analyze(profile, secondary, window);

        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter,
            new AvoidanceSearchOptions
            {
                LeadTimes = new[]
                {
                    TimeSpan.FromSeconds(30.0),
                    TimeSpan.FromMinutes(2.0),
                    TimeSpan.FromMinutes(6.0)
                },
                DeltaVMagnitudesMetersPerSecond = new[] { 0.0, 0.1, 0.25 },
                MaxReturnedOptions = 20
            });

        Assert.Equal(6, avoidance.Count);
        Assert.All(avoidance, option =>
        {
            Assert.True(window.Intersects(option.BurnEpoch), "Burn epoch must stay inside the screening window");
            Assert.True(System.Math.Abs(option.DeltaVInertial.Magnitude() - 0.1) < 1e-12,
                $"Only the valid 0.1 m/s delta-V should remain, got {option.DeltaVInertial.Magnitude():E6}");
            Assert.True(System.Math.Abs((encounter.EncounterState.Epoch - option.BurnEpoch).TotalSeconds - 120.0) < 1e-9,
                $"Only the valid 2-minute lead time should remain, got {(encounter.EncounterState.Epoch - option.BurnEpoch).TotalSeconds:F6}s");
        });
    }

    // ========== Helpers ==========

    private static CelestialBody CreateEarth()
    {
        return new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB);
    }

    private static Spacecraft CreateSpacecraft(int naifId, string name, CelestialBody earth, Vector3 position, Vector3 velocity, double hardBodyRadius = 8.0)
    {
        return CreateSpacecraft(naifId, name, earth, position, velocity, BuildCovariance(positionVariance: 25.0, velocityVariance: 0.01), hardBodyRadius);
    }

    private static Spacecraft CreateSpacecraft(int naifId, string name, CelestialBody earth, Vector3 position, Vector3 velocity, Matrix covariance, double hardBodyRadius = 8.0)
    {
        var orbit = new StateVector(position, velocity, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF, covariance);
        return new Spacecraft(naifId, name, 120.0, 150.0, new Clock($"{name}_CLK", 256), orbit, sectionalArea: 2.0, dragCoeff: 2.2, cosparId: $"2026-001{name[0]}", solarRadiationCoeff: 1.2, hardBodyRadius: hardBodyRadius);
    }

    private static Spacecraft CreateSpacecraftWithoutCovariance(int naifId, string name, CelestialBody earth, Vector3 position, Vector3 velocity, double hardBodyRadius = 8.0)
    {
        var orbit = new StateVector(position, velocity, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        return new Spacecraft(naifId, name, 120.0, 150.0, new Clock($"{name}_CLK", 256), orbit, sectionalArea: 2.0, dragCoeff: 2.2, cosparId: $"2026-001{name[0]}", solarRadiationCoeff: 1.2, hardBodyRadius: hardBodyRadius);
    }

    private static Spacecraft CreateSpacecraftFromBody(CelestialBody body, int naifId, string name, double hardBodyRadius = 0.0)
    {
        var earth = CreateEarth();
        var state = body.GetEphemeris(TimeSystem.Time.J2000TDB, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        return new Spacecraft(naifId, name, 1000.0, 2000.0, new Clock($"{name}_CLK", 256), state, sectionalArea: 10.0, dragCoeff: 2.2, cosparId: $"2026-001{name[0]}", solarRadiationCoeff: 1.2, hardBodyRadius: hardBodyRadius);
    }

    private static Matrix BuildCovariance(double positionVariance, double velocityVariance)
    {
        var covariance = new Matrix(6, 6);
        covariance.Set(0, 0, positionVariance);
        covariance.Set(1, 1, positionVariance);
        covariance.Set(2, 2, positionVariance);
        covariance.Set(3, 3, velocityVariance);
        covariance.Set(4, 4, velocityVariance);
        covariance.Set(5, 5, velocityVariance);
        return covariance;
    }

    private static Matrix BuildPositionCovariance(double[,] values, double scale = 1.0)
    {
        var matrix = new Matrix(3, 3);
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                matrix.Set(row, column, values[row, column] * scale);
            }
        }

        return matrix;
    }

    private static Matrix BuildStateCovarianceFromPosition(Matrix positionCovariance)
    {
        var covariance = new Matrix(6, 6);
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                covariance.Set(row, column, positionCovariance.Get(row, column));
            }
        }

        return covariance;
    }

    private static Matrix RotateRtnPositionCovarianceToInertial(Vector3 position, Vector3 velocity, Matrix covarianceRtn)
    {
        var rotation = CreateRtnRotation(position, velocity);
        return rotation.Transpose() * covarianceRtn * rotation;
    }

    private static void AssertEncounterMatchesDirectExpectation(
        EncounterCase encounter,
        Vector3 primaryPositionAtEpoch,
        Vector3 primaryVelocityAtEpoch,
        Matrix primaryPositionCovariance,
        Vector3 secondaryPositionAtEpoch,
        Vector3 secondaryVelocityAtEpoch,
        Matrix secondaryPositionCovariance,
        double expectedCombinedHardBodyRadius,
        double expectedPublishedPc,
        double publishedPcRelativeTolerance)
    {
        var expectedRelativePosition = secondaryPositionAtEpoch - primaryPositionAtEpoch;
        var expectedRelativeVelocity = secondaryVelocityAtEpoch - primaryVelocityAtEpoch;
        double expectedMissDistance = expectedRelativePosition.Magnitude();
        var rtnRotation = CreateRtnRotation(primaryPositionAtEpoch, primaryVelocityAtEpoch);
        var expectedRelativePositionRtn = RotateVector(rtnRotation, expectedRelativePosition);
        var expectedRelativeVelocityRtn = RotateVector(rtnRotation, expectedRelativeVelocity);

        var combinedPositionCovariance = Add(primaryPositionCovariance, secondaryPositionCovariance);
        var combinedStateCovariance = Add(
            BuildStateCovarianceFromPosition(primaryPositionCovariance),
            BuildStateCovarianceFromPosition(secondaryPositionCovariance));
        var rot6X6 = Matrix.CreateBlockDiagonal(rtnRotation, rtnRotation);
        var expectedCombinedCovarianceRtn = rot6X6 * combinedStateCovariance * rot6X6.Transpose();
        var (_, expectedProjectedCovariance) = ConjunctionAssessment.ProjectOntoEncounterPlane(
            expectedRelativePosition,
            expectedRelativeVelocity,
            combinedPositionCovariance);
        var expectedPc = ComputeFosterProbabilityFromInertialInputs(
            primaryPositionAtEpoch,
            primaryVelocityAtEpoch,
            primaryPositionCovariance,
            secondaryPositionAtEpoch,
            secondaryVelocityAtEpoch,
            secondaryPositionCovariance,
            expectedCombinedHardBodyRadius);

        Assert.Equal(EncounterQualityFlags.StaleCovarianceUsed, encounter.EncounterState.QualityFlags);
        Assert.Equal(expectedCombinedHardBodyRadius, encounter.CollisionRisk.CombinedHardBodyRadiusMeters, 12);
        Assert.NotNull(encounter.CollisionRisk.ProbabilityOfCollision);
        Assert.NotNull(expectedPc);
        Assert.True(System.Math.Abs(encounter.CollisionRisk.ProbabilityOfCollision.Value - expectedPc.Value) < 1.0e-12,
            $"Entry-point Pc {encounter.CollisionRisk.ProbabilityOfCollision.Value:E12} should match direct Pc {expectedPc.Value:E12}");
        Assert.True(System.Math.Abs(encounter.CollisionRisk.ProbabilityOfCollision.Value - expectedPublishedPc) / expectedPublishedPc < publishedPcRelativeTolerance,
            $"Expected published Pc≈{expectedPublishedPc:E12}, got {encounter.CollisionRisk.ProbabilityOfCollision.Value:E12}");

        Assert.Equal(expectedMissDistance, encounter.EncounterState.MissDistanceMeters, 9);
        AssertVectorApproximatelyEqual(expectedRelativePosition, encounter.EncounterState.RelativeState.RelativePositionInertial, 1.0e-8);
        AssertVectorApproximatelyEqual(expectedRelativeVelocity, encounter.EncounterState.RelativeState.RelativeVelocityInertial, 1.0e-11);
        AssertVectorApproximatelyEqual(expectedRelativePositionRtn, encounter.EncounterState.RelativeState.RelativePositionRtn, 1.0e-8);
        AssertVectorApproximatelyEqual(expectedRelativeVelocityRtn, encounter.EncounterState.RelativeState.RelativeVelocityRtn, 1.0e-11);

        AssertMatrixApproximatelyEqual(expectedCombinedCovarianceRtn, encounter.EncounterState.CombinedCovarianceRtn, 1.0e-9);
        AssertMatrixApproximatelyEqual(expectedProjectedCovariance, encounter.CollisionRisk.ProjectedCovariance, 1.0e-10);
        Assert.Equal(System.Math.Sqrt(expectedCombinedCovarianceRtn.Get(0, 0)), encounter.CollisionRisk.RadialSigmaMeters, 9);
        Assert.Equal(System.Math.Sqrt(expectedCombinedCovarianceRtn.Get(1, 1)), encounter.CollisionRisk.InTrackSigmaMeters, 9);
        Assert.Equal(System.Math.Sqrt(expectedCombinedCovarianceRtn.Get(2, 2)), encounter.CollisionRisk.CrossTrackSigmaMeters, 9);
    }

    private static void AssertVectorApproximatelyEqual(Vector3 expected, Vector3 actual, double tolerance)
    {
        Assert.True(System.Math.Abs(expected.X - actual.X) <= tolerance,
            $"Expected X={expected.X:E12}, got {actual.X:E12}");
        Assert.True(System.Math.Abs(expected.Y - actual.Y) <= tolerance,
            $"Expected Y={expected.Y:E12}, got {actual.Y:E12}");
        Assert.True(System.Math.Abs(expected.Z - actual.Z) <= tolerance,
            $"Expected Z={expected.Z:E12}, got {actual.Z:E12}");
    }

    private static void AssertMatrixApproximatelyEqual(Matrix expected, Matrix actual, double tolerance)
    {
        Assert.Equal(expected.Rows, actual.Rows);
        Assert.Equal(expected.Columns, actual.Columns);
        for (int row = 0; row < expected.Rows; row++)
        {
            for (int column = 0; column < expected.Columns; column++)
            {
                Assert.True(System.Math.Abs(expected.Get(row, column) - actual.Get(row, column)) <= tolerance,
                    $"Matrix mismatch at ({row},{column}): expected {expected.Get(row, column):E12}, got {actual.Get(row, column):E12}");
            }
        }
    }

    private static Vector3 RotateVector(Matrix rotation, Vector3 vector)
    {
        var rotated = rotation * new[] { vector.X, vector.Y, vector.Z };
        return new Vector3(rotated[0], rotated[1], rotated[2]);
    }

    private static Matrix CreateRtnRotation(Vector3 position, Vector3 velocity)
    {
        var radial = position.Normalize();
        var crossTrack = position.Cross(velocity).Normalize();
        var inTrack = crossTrack.Cross(radial).Normalize();
        var rotation = new Matrix(3, 3);
        rotation.Set(0, 0, radial.X); rotation.Set(0, 1, radial.Y); rotation.Set(0, 2, radial.Z);
        rotation.Set(1, 0, inTrack.X); rotation.Set(1, 1, inTrack.Y); rotation.Set(1, 2, inTrack.Z);
        rotation.Set(2, 0, crossTrack.X); rotation.Set(2, 1, crossTrack.Y); rotation.Set(2, 2, crossTrack.Z);
        return rotation;
    }

    private static double? ComputeFosterProbabilityFromInertialInputs(
        Vector3 primaryPosition,
        Vector3 primaryVelocity,
        Matrix primaryCovariance,
        Vector3 secondaryPosition,
        Vector3 secondaryVelocity,
        Matrix secondaryCovariance,
        double combinedHardBodyRadius)
    {
        var relativePosition = secondaryPosition - primaryPosition;
        var relativeVelocity = secondaryVelocity - primaryVelocity;
        var combinedCovariance = Add(primaryCovariance, secondaryCovariance);
        var (miss2D, cov2D) = ConjunctionAssessment.ProjectOntoEncounterPlane(relativePosition, relativeVelocity, combinedCovariance);
        return ConjunctionAssessment.ComputeFosterCollisionProbability(miss2D, cov2D, combinedHardBodyRadius);
    }

    private static PropagationSolution CreatePropagationSolution(
        CelestialBody observer,
        TimeSystem.Time baseEpoch,
        double durationSeconds,
        double stepSeconds,
        Func<double, (Vector3 position, Vector3 velocity, Vector3 acceleration)> stateAtSeconds)
    {
        var solution = new PropagationSolution();
        var segment = new PropagationSegment(baseEpoch, (int)System.Math.Ceiling(durationSeconds / stepSeconds));
        var outputStates = new List<StateVector>();

        var (initialPosition, initialVelocity, _) = stateAtSeconds(0.0);
        outputStates.Add(new StateVector(initialPosition, initialVelocity, observer, baseEpoch, Frames.Frame.ICRF));

        for (double t = 0.0; t < durationSeconds - 1e-12; t += stepSeconds)
        {
            double nextTime = System.Math.Min(t + stepSeconds, durationSeconds);
            var (startPosition, startVelocity, startAcceleration) = stateAtSeconds(t);
            var (endPosition, endVelocity, endAcceleration) = stateAtSeconds(nextTime);
            segment.AddStep(new AcceptedStep(
                t,
                nextTime - t,
                startPosition,
                startVelocity,
                endPosition,
                endVelocity,
                startAcceleration,
                endAcceleration));

            outputStates.Add(new StateVector(
                endPosition,
                endVelocity,
                observer,
                baseEpoch.AddSeconds(nextTime),
                Frames.Frame.ICRF));
        }

        solution.AddSegment(segment);
        solution.SetOutputStates(outputStates.ToArray());
        return solution;
    }

    private static double IntegrateIsotropicGaussianOverCircleByMidpointRule(double[] miss2D, double sigma, double radius, int cellsPerAxis = 800)
    {
        double sigma2 = sigma * sigma;
        double normalizer = 1.0 / (2.0 * System.Math.PI * sigma2);
        double lowerX = miss2D[0] - radius;
        double lowerY = miss2D[1] - radius;
        double step = (2.0 * radius) / cellsPerAxis;
        double radiusSquared = radius * radius;
        double sum = 0.0;

        for (int i = 0; i < cellsPerAxis; i++)
        {
            double x = lowerX + (i + 0.5) * step;
            for (int j = 0; j < cellsPerAxis; j++)
            {
                double y = lowerY + (j + 0.5) * step;
                double dx = x - miss2D[0];
                double dy = y - miss2D[1];
                if (dx * dx + dy * dy > radiusSquared)
                {
                    continue;
                }

                sum += normalizer * System.Math.Exp(-(x * x + y * y) / (2.0 * sigma2));
            }
        }

        return sum * step * step;
    }

    private static Matrix Add(Matrix left, Matrix right)
    {
        var result = new Matrix(left.Rows, left.Columns);
        for (int row = 0; row < left.Rows; row++)
        {
            for (int column = 0; column < left.Columns; column++)
            {
                result.Set(row, column, left.Get(row, column) + right.Get(row, column));
            }
        }

        return result;
    }

    private sealed class LinearMotionLocalizable : ILocalizable
    {
        private readonly CelestialBody _observer;
        private readonly TimeSystem.Time _epoch0;
        private readonly Vector3 _position0;
        private readonly Vector3 _velocity;

        public LinearMotionLocalizable(int naifId, string name, CelestialBody observer, TimeSystem.Time epoch0, Vector3 position0, Vector3 velocity)
        {
            NaifId = naifId;
            Name = name;
            _observer = observer;
            _epoch0 = epoch0;
            _position0 = position0;
            _velocity = velocity;
            InitialOrbitalParameters = BuildState(epoch0);
        }

        public int NaifId { get; }

        public string Name { get; }

        public IO.Astrodynamics.OrbitalParameters.OrbitalParameters InitialOrbitalParameters { get; }

        public bool IsSpiceBacked => false;

        public double GM => 0.0;

        public double Mass => 0.0;

        public IEnumerable<IO.Astrodynamics.OrbitalParameters.OrbitalParameters> GetEphemeris(
            in Window searchWindow,
            ILocalizable observer,
            Frames.Frame frame,
            Aberration aberration,
            in TimeSpan stepSize)
        {
            return new[] { GetEphemeris(searchWindow.StartDate, observer, frame, aberration) };
        }

        public StateVector GetGeometricStateRelativeTo(in TimeSystem.Time epoch, CelestialItem referenceBody)
        {
            return BuildState(epoch);
        }

        public IO.Astrodynamics.OrbitalParameters.OrbitalParameters GetEphemeris(in TimeSystem.Time epoch, ILocalizable observer, Frames.Frame frame, Aberration aberration)
        {
            return BuildState(epoch);
        }

        public double AngularSeparation(in TimeSystem.Time epoch, ILocalizable target1, ILocalizable target2, Aberration aberration)
        {
            throw new NotSupportedException();
        }

        public IEnumerable<ILocalizable> GetCentersOfMotion()
        {
            yield return _observer;
        }

        public IEnumerable<Window> FindWindowsOnDistanceConstraint(
            in Window searchWindow,
            ILocalizable observer,
            RelationnalOperator relationalOperator,
            double value,
            Aberration aberration,
            in TimeSpan stepSize)
        {
            return Array.Empty<Window>();
        }

        public IEnumerable<Window> FindWindowsOnOccultationConstraint(
            in Window searchWindow,
            ILocalizable observer,
            ShapeType targetShape,
            INaifObject frontBody,
            ShapeType frontShape,
            OccultationType occultationType,
            Aberration aberration,
            in TimeSpan stepSize)
        {
            return Array.Empty<Window>();
        }

        public IEnumerable<Window> FindWindowsOnCoordinateConstraint(
            in Window searchWindow,
            ILocalizable observer,
            Frames.Frame frame,
            CoordinateSystem coordinateSystem,
            Coordinate coordinate,
            RelationnalOperator relationalOperator,
            double value,
            double adjustValue,
            Aberration aberration,
            in TimeSpan stepSize)
        {
            return Array.Empty<Window>();
        }

        private StateVector BuildState(TimeSystem.Time epoch)
        {
            double dtSeconds = (epoch - _epoch0).TotalSeconds;
            return new StateVector(
                _position0 + _velocity * dtSeconds,
                _velocity,
                _observer,
                epoch,
                Frames.Frame.ICRF);
        }
    }

    private sealed class OscillatingLocalizable : ILocalizable
    {
        private readonly CelestialBody _observer;
        private readonly TimeSystem.Time _epoch0;
        private readonly Vector3 _basePosition;
        private readonly Vector3 _axis;
        private readonly double _amplitudeMeters;
        private readonly double _angularFrequencyRadiansPerSecond;

        public OscillatingLocalizable(
            int naifId,
            string name,
            CelestialBody observer,
            TimeSystem.Time epoch0,
            Vector3 basePosition,
            Vector3 axis,
            double amplitudeMeters,
            double angularFrequencyRadiansPerSecond)
        {
            NaifId = naifId;
            Name = name;
            _observer = observer;
            _epoch0 = epoch0;
            _basePosition = basePosition;
            _axis = axis.Normalize();
            _amplitudeMeters = amplitudeMeters;
            _angularFrequencyRadiansPerSecond = angularFrequencyRadiansPerSecond;
            InitialOrbitalParameters = BuildState(epoch0);
        }

        public int NaifId { get; }

        public string Name { get; }

        public IO.Astrodynamics.OrbitalParameters.OrbitalParameters InitialOrbitalParameters { get; }

        public bool IsSpiceBacked => false;

        public double GM => 0.0;

        public double Mass => 0.0;

        public IEnumerable<IO.Astrodynamics.OrbitalParameters.OrbitalParameters> GetEphemeris(
            in Window searchWindow,
            ILocalizable observer,
            Frames.Frame frame,
            Aberration aberration,
            in TimeSpan stepSize)
        {
            return new[] { GetEphemeris(searchWindow.StartDate, observer, frame, aberration) };
        }

        public StateVector GetGeometricStateRelativeTo(in TimeSystem.Time epoch, CelestialItem referenceBody)
        {
            return BuildState(epoch);
        }

        public IO.Astrodynamics.OrbitalParameters.OrbitalParameters GetEphemeris(in TimeSystem.Time epoch, ILocalizable observer, Frames.Frame frame, Aberration aberration)
        {
            return BuildState(epoch);
        }

        public double AngularSeparation(in TimeSystem.Time epoch, ILocalizable target1, ILocalizable target2, Aberration aberration)
        {
            throw new NotSupportedException();
        }

        public IEnumerable<ILocalizable> GetCentersOfMotion()
        {
            yield return _observer;
        }

        public IEnumerable<Window> FindWindowsOnDistanceConstraint(
            in Window searchWindow,
            ILocalizable observer,
            RelationnalOperator relationalOperator,
            double value,
            Aberration aberration,
            in TimeSpan stepSize)
        {
            return Array.Empty<Window>();
        }

        public IEnumerable<Window> FindWindowsOnOccultationConstraint(
            in Window searchWindow,
            ILocalizable observer,
            ShapeType targetShape,
            INaifObject frontBody,
            ShapeType frontShape,
            OccultationType occultationType,
            Aberration aberration,
            in TimeSpan stepSize)
        {
            return Array.Empty<Window>();
        }

        public IEnumerable<Window> FindWindowsOnCoordinateConstraint(
            in Window searchWindow,
            ILocalizable observer,
            Frames.Frame frame,
            CoordinateSystem coordinateSystem,
            Coordinate coordinate,
            RelationnalOperator relationalOperator,
            double value,
            double adjustValue,
            Aberration aberration,
            in TimeSpan stepSize)
        {
            return Array.Empty<Window>();
        }

        private StateVector BuildState(TimeSystem.Time epoch)
        {
            double dtSeconds = (epoch - _epoch0).TotalSeconds;
            double phase = _angularFrequencyRadiansPerSecond * dtSeconds;
            double displacement = _amplitudeMeters * System.Math.Sin(phase);
            double speed = _amplitudeMeters * _angularFrequencyRadiansPerSecond * System.Math.Cos(phase);
            return new StateVector(
                _basePosition + _axis * displacement,
                _axis * speed,
                _observer,
                epoch,
                Frames.Frame.ICRF);
        }
    }
}
