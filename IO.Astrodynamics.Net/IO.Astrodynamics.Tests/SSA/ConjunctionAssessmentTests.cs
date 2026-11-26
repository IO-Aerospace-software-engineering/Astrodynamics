using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Ccsds;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Ssa;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.SSA;

public partial class ConjunctionAssessmentTests
{
    private readonly ITestOutputHelper _output;

    public ConjunctionAssessmentTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    // ========== TCA Detection Tests ==========

    [Fact]
    public void FindCloseApproaches_MoonSun_RangeRateZeroAtEachTca()
    {
        // Verify each detected TCA has range-rate ≈ 0 and is a local distance minimum
        var earth = CreateEarth();
        var moon = new CelestialBody(PlanetsAndMoons.Moon, Frames.Frame.ICRF, TimeSystem.Time.J2000Tdb);
        var sun = new CelestialBody(Stars.Sun, Frames.Frame.ICRF, TimeSystem.Time.J2000Tdb);

        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddDays(60));

        var tcas = ConjunctionAssessment.FindCloseApproaches(
            moon, sun, window, TimeSpan.FromHours(6), 1e-6, earth);

        // Moon-Sun distance oscillates ~2 times per synodic month (~29.5 days)
        Assert.True(tcas.Count >= 2, $"Expected ≥2 TCAs over 60 days, got {tcas.Count}");

        foreach (var tca in tcas)
        {
            Assert.True(window.Contains(tca), "TCA must be within the screening window");

            // At TCA, range-rate (r_rel · v_rel) should be ≈ 0
            var moonState = moon.GetEphemeris(tca, earth, Frames.Frame.ICRF, Aberration.None);
            var sunState = sun.GetEphemeris(tca, earth, Frames.Frame.ICRF, Aberration.None);
            var relPos = sunState.Position - moonState.Position;
            var relVel = sunState.Velocity - moonState.Velocity;
            double rangeRate = relPos.Dot(relVel);
            double rangeRateNormalized = rangeRate / (relPos.Magnitude() * relVel.Magnitude());
            Assert.True(System.Math.Abs(rangeRateNormalized) < 1e-12,
                $"Range-rate at TCA should be ≈ 0, normalized value = {rangeRateNormalized:E6}");

            // Distance at TCA should be a local minimum: d(TCA) < d(TCA ± 1 hour)
            double distAtTca = relPos.Magnitude();
            var moonBefore = moon.GetEphemeris(tca - TimeSpan.FromHours(1), earth, Frames.Frame.ICRF, Aberration.None);
            var sunBefore = sun.GetEphemeris(tca - TimeSpan.FromHours(1), earth, Frames.Frame.ICRF, Aberration.None);
            double distBefore = (sunBefore.Position - moonBefore.Position).Magnitude();

            var moonAfter = moon.GetEphemeris(tca + TimeSpan.FromHours(1), earth, Frames.Frame.ICRF, Aberration.None);
            var sunAfter = sun.GetEphemeris(tca + TimeSpan.FromHours(1), earth, Frames.Frame.ICRF, Aberration.None);
            double distAfter = (sunAfter.Position - moonAfter.Position).Magnitude();

            Assert.True(distAtTca <= distBefore,
                $"Distance at TCA ({distAtTca:E6}) should be ≤ distance 1h before ({distBefore:E6})");
            Assert.True(distAtTca <= distAfter,
                $"Distance at TCA ({distAtTca:E6}) should be ≤ distance 1h after ({distAfter:E6})");
        }
    }

    [Fact]
    public void FindCloseApproaches_RangeDecreasingThroughoutTheWindow_ReturnsEmpty()
    {
        // Two equatorial orbits 5 km apart; unpropagated, each follows its Keplerian orbit. Over the first 5 minutes
        // the range decreases without a minimum (the closest approach comes at 449 s), as the closed-form
        // two-body solution shows below: no range-rate zero crossing.
        var earth = CreateEarth();
        var (protectedPosition, protectedVelocity) = (new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var (secondaryPosition, secondaryVelocity) = (new Vector3(6_800_000.0, 5000.0, 0.0), new Vector3(0.0, 7_646.0, 0.0));
        var protected1 = CreateSpacecraft(-2301, "P", earth, protectedPosition, protectedVelocity);
        var secondary1 = CreateSpacecraft(-2302, "S", earth, secondaryPosition, secondaryVelocity);
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(5.0));
        for (double t = 5.0; t <= 300.0; t += 5.0)
        {
            Assert.True(ClosedFormRange(earth, protectedPosition, protectedVelocity, secondaryPosition, secondaryVelocity, t) <
                        ClosedFormRange(earth, protectedPosition, protectedVelocity, secondaryPosition, secondaryVelocity, t - 5.0));
        }

        var tcas = ConjunctionAssessment.FindCloseApproaches(
            protected1, secondary1, window,
            TimeSpan.FromSeconds(5), 1e-9, earth);

        Assert.Empty(tcas);
    }

    [Fact]
    public void FindCloseApproaches_EquatorialOrbits_FindsTheTcaOfTheClosedForm()
    {
        // The same two equatorial orbits over 10 minutes: one closest approach, at the minimum of the closed-form
        // two-body range. Before #368, the argument of periapsis of an equatorial orbit was taken in [0, π] only, and
        // the secondary started 3 609 km away from its given state.
        var earth = CreateEarth();
        var (protectedPosition, protectedVelocity) = (new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var (secondaryPosition, secondaryVelocity) = (new Vector3(6_800_000.0, 5000.0, 0.0), new Vector3(0.0, 7_646.0, 0.0));
        var protected1 = CreateSpacecraft(-2391, "P", earth, protectedPosition, protectedVelocity);
        var secondary1 = CreateSpacecraft(-2392, "S", earth, secondaryPosition, secondaryVelocity);
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(10.0));

        // Golden-section search of the closed-form range minimum
        double lo = 400.0, hi = 500.0;
        double g = (System.Math.Sqrt(5.0) - 1.0) / 2.0;
        while (hi - lo > 1e-9)
        {
            double a = hi - g * (hi - lo), b = lo + g * (hi - lo);
            if (ClosedFormRange(earth, protectedPosition, protectedVelocity, secondaryPosition, secondaryVelocity, a) <
                ClosedFormRange(earth, protectedPosition, protectedVelocity, secondaryPosition, secondaryVelocity, b))
                hi = b;
            else
                lo = a;
        }

        var tcas = ConjunctionAssessment.FindCloseApproaches(
            protected1, secondary1, window,
            TimeSpan.FromSeconds(5), 1e-9, earth);

        var tca = Assert.Single(tcas);
        double error = (tca - TimeSystem.Time.J2000Tdb).TotalSeconds - 0.5 * (lo + hi);
        // Measured 2.5e-5 s from the minimum at 449.2 s (Linux, .NET 10, 2026-10-11)
        Assert.True(System.Math.Abs(error) < 1e-4, $"TCA {error:E2} s from the closed form");
    }

    // Range between two Keplerian orbits t seconds after J2000, from the closed-form two-body solution of the tests
    private static double ClosedFormRange(CelestialBody earth, Vector3 r1, Vector3 v1, Vector3 r2, Vector3 v2, double t)
    {
        var (x1, _) = IO.Astrodynamics.Tests.Propagators.Variational.KeplerianStm.Propagate(earth.GM, r1, v1, t);
        var (x2, _) = IO.Astrodynamics.Tests.Propagators.Variational.KeplerianStm.Propagate(earth.GM, r2, v2, t);
        return new Vector3(x1[0] - x2[0], x1[1] - x2[1], x1[2] - x2[2]).Magnitude();
    }

    [Fact]
    public void FindCloseApproaches_TcaAtWindowStart_IsDetected()
    {
        var earth = CreateEarth();
        var start = TimeSystem.Time.J2000Tdb;
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

    // The sampled search (two ILocalizable objects) bisects each change of sign of the range rate between its samples and
    // returns the tick nearer the zero (#624): a secondary at (100 m, v (t − t*), 0) from a still primary, v = 7.6 km/s, the
    // zero t* f ticks after the tick k from the start, in a window of 10 s sampled every second. Before, the lower tick of
    // the last bracket: up to a tick before the zero, a zero exactly on a tick one tick early
    [Theory]
    [InlineData(52_345_678L, 0.0)] // exactly on a tick: that tick
    [InlineData(52_345_678L, 0.25)]
    [InlineData(52_345_678L, 0.5 - 1.0e-3)]
    [InlineData(52_345_678L, 0.5 + 1.0e-3)]
    [InlineData(52_345_678L, 0.75)]
    [InlineData(50_000_000L, 0.0)] // exactly on a sample epoch: that epoch
    [InlineData(0L, 0.25)] // in the first tick, nearer the start: the start
    [InlineData(0L, 0.75)] // nearer the tick after it: that tick
    [InlineData(99_999_999L, 0.25)] // in the last tick, nearer the tick before the end: that tick
    [InlineData(99_999_999L, 0.75)] // nearer the end: the end, the sampled search applies no window-end rule (#621)
    [InlineData(100_000_000L, 0.0)] // exactly at the end: the end, as a zero exactly at the start is the start
    public void FindCloseApproaches_SampledSearch_ReturnsTheTickNearerTheZero(long zeroTicks, double fraction)
    {
        var earth = CreateEarth();
        var start = TimeSystem.Time.J2000Tdb;
        var window = new Window(start, start.AddSeconds(10.0));
        const double v = 7600.0;
        double zeroSeconds = (zeroTicks + fraction) / TimeSpan.TicksPerSecond;
        var primary = new LinearMotionLocalizable(-2341, "P", earth, start, Vector3.Zero, Vector3.Zero);
        var secondary = new LinearMotionLocalizable(-2342, "S", earth, start, new Vector3(100.0, -v * zeroSeconds, 0.0),
            new Vector3(0.0, v, 0.0));

        var tcas = ConjunctionAssessment.FindCloseApproaches(primary, secondary, window, TimeSpan.FromSeconds(1.0), 1e-9,
            earth);

        LabelAssert.Equal(start.AddTicks(zeroTicks + (fraction > 0.5 ? 1 : 0)), Assert.Single(tcas));
    }

    [Fact]
    public void FindCloseApproaches_CrossingOnlyInFinalSubstep_IsDetected()
    {
        var earth = CreateEarth();
        var start = TimeSystem.Time.J2000Tdb;
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
        var start = TimeSystem.Time.J2000Tdb;
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
    public void FindCloseApproaches_NoTca_AnalyzeReturnsTheClosestWindowEnd()
    {
        var earth = CreateEarth();
        // Co-moving spacecraft → no zero-crossing → Analyze returns the closest window end, flagged (#621: the window
        // midpoint before 11.0.0)
        var protected1 = CreateSpacecraft(-2201, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary1 = CreateSpacecraft(-2202, "S", earth,
            new Vector3(6_800_000.0, 0.0, 50.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(10.0));

        var encounter = ConjunctionAssessment.Analyze(protected1, secondary1, window);

        // The reference: the distance of the two-body states at each end
        double Distance(TimeSystem.Time epoch) => (secondary1.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).Position - protected1.GetEphemeris(epoch, earth, Frames.Frame.ICRF, Aberration.None).Position).Magnitude();
        var expected = Distance(window.EndDate) < Distance(window.StartDate) ? window.EndDate : window.StartDate;
        LabelAssert.Equal(expected, encounter.EncounterState.Epoch);
        Assert.Equal(Distance(expected), encounter.EncounterState.MissDistanceMeters, 9);
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.TcaAtWindowEnd));
    }

    [Fact]
    public void AnalyzeAll_NoTca_ReturnsNothing()
    {
        var earth = CreateEarth();
        var protected1 = CreateSpacecraft(-2203, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary1 = CreateSpacecraft(-2204, "S", earth,
            new Vector3(6_800_000.0, 0.0, 100.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(10.0));

        // No close approach: an empty list, as on two propagations (#621: a single midpoint encounter before 11.0.0)
        Assert.Empty(ConjunctionAssessment.AnalyzeAll(protected1, secondary1, window));
    }

    [Fact]
    public void AnalyzeAll_MoonSun_ReturnsMultipleEncountersWithDistinctEpochs()
    {
        var earth = CreateEarth();
        var moon = new CelestialBody(PlanetsAndMoons.Moon, Frames.Frame.ICRF, TimeSystem.Time.J2000Tdb);
        var sun = new CelestialBody(Stars.Sun, Frames.Frame.ICRF, TimeSystem.Time.J2000Tdb);
        var protectedCraft = CreateSpacecraftFromBody(moon, -5501, "MOON_SC", 1737400.0);
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddDays(60));

        var encounters = ConjunctionAssessment.AnalyzeAll(protectedCraft, sun, window,
            new ConjunctionAnalysisOptions { SampleStep = TimeSpan.FromHours(6) });

        Assert.True(encounters.Count >= 2, $"Expected ≥2 encounters over 60 days, got {encounters.Count}");

        // Each encounter must have a distinct epoch and positive miss distance
        for (int i = 0; i < encounters.Count; i++)
        {
            Assert.True(window.Contains(encounters[i].EncounterState.Epoch));
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
        var start = TimeSystem.Time.J2000Tdb;
        var window = new Window(start.AddSeconds(0.6), start.AddSeconds(2.4));
        var protectedSpacecraft = CreateSpacecraft(
            -5503,
            "P",
            earth,
            UniformOffsetPosition,
            UniformOffsetVelocity);
        var secondarySpacecraft = CreateSpacecraft(
            -5504,
            "S",
            earth,
            new Vector3(10.0, 0.0, 0.0) + UniformOffsetPosition,
            new Vector3(0.0, 100.0 * System.Math.PI, 0.0) + UniformOffsetVelocity);

        var protectedTrajectory = CreatePropagationSolution(
            earth,
            start,
            durationSeconds: 3.0,
            stepSeconds: 0.25,
            stateAtSeconds: t => WithUniformOffset(t, (Vector3.Zero, Vector3.Zero, Vector3.Zero)));
        var secondaryTrajectory = CreatePropagationSolution(
            earth,
            start,
            durationSeconds: 3.0,
            stepSeconds: 0.25,
            stateAtSeconds: t =>
            {
                double phase = System.Math.PI * t;
                return WithUniformOffset(t, (
                    new Vector3(10.0, 100.0 * System.Math.Sin(phase), 0.0),
                    new Vector3(0.0, 100.0 * System.Math.PI * System.Math.Cos(phase), 0.0),
                    new Vector3(0.0, -100.0 * System.Math.PI * System.Math.PI * System.Math.Sin(phase), 0.0)));
            });

        var encounters = ConjunctionAssessment.AnalyzeAll(
            protectedSpacecraft,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromSeconds(160.0),
                MaxEventSearchStep = TimeSpan.FromSeconds(10.0)
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
        var start = TimeSystem.Time.J2000Tdb;
        var window = new Window(start, start.AddSeconds(3.0));
        var protectedSpacecraft = CreateSpacecraft(-5505, "P", earth, UniformOffsetPosition, UniformOffsetVelocity);
        // The option gives the secondary its radius of 0 m, which the spacecraft then carries (#683: a differing option throws)
        var secondarySpacecraft = CreateSpacecraft(-5506, "S", earth, new Vector3(10.0, 0.0, 0.0) + UniformOffsetPosition, UniformOffsetVelocity,
            hardBodyRadius: 0.0);
        const double root1 = 0.75;
        const double root2 = 1.50;
        const double root3 = 2.25;
        const double scale = 80.0;

        var protectedTrajectory = CreatePropagationSolution(
            earth,
            start,
            durationSeconds: 3.0,
            stepSeconds: 3.0,
            stateAtSeconds: t => WithUniformOffset(t, (Vector3.Zero, Vector3.Zero, Vector3.Zero)));
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
                return WithUniformOffset(t, (
                    new Vector3(10.0, displacement, 0.0),
                    new Vector3(0.0, speed, 0.0),
                    new Vector3(0.0, acceleration, 0.0)));
            });

        var encounters = ConjunctionAssessment.AnalyzeAll(
            protectedSpacecraft,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaxEventSearchStep = TimeSpan.FromDays(1.0),
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
        var start = TimeSystem.Time.J2000Tdb;
        var end = start.AddSeconds(2.0);
        var window = new Window(start, end);
        var protectedSpacecraft = CreateSpacecraft(-5507, "P", earth, UniformOffsetPosition, UniformOffsetVelocity);
        // The option gives the secondary its radius of 0 m, which the spacecraft then carries (#683: a differing option throws)
        var secondarySpacecraft = CreateSpacecraft(-5508, "S", earth, new Vector3(10.0, 200.0, 0.0) + UniformOffsetPosition,
            new Vector3(0.0, -100.0, 0.0) + UniformOffsetVelocity, hardBodyRadius: 0.0);

        var protectedTrajectory = CreatePropagationSolution(
            earth,
            start,
            durationSeconds: 2.0,
            stepSeconds: 2.0,
            stateAtSeconds: t => WithUniformOffset(t, (Vector3.Zero, Vector3.Zero, Vector3.Zero)));
        var secondaryTrajectory = CreatePropagationSolution(
            earth,
            start,
            durationSeconds: 2.0,
            stepSeconds: 2.0,
            stateAtSeconds: t => WithUniformOffset(t, (
                new Vector3(10.0, 200.0 - 100.0 * t, 0.0),
                new Vector3(0.0, -100.0, 0.0),
                Vector3.Zero)));

        var encounter = ConjunctionAssessment.Analyze(
            protectedSpacecraft,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaxEventSearchStep = TimeSpan.FromDays(1.0),
                SecondaryHardBodyRadiusMeters = 0.0,
                LowRelativeSpeedThresholdMetersPerSecond = 0.0
            });

        LabelAssert.Equal(end, encounter.EncounterState.Epoch);
        Assert.Equal(10.0, encounter.EncounterState.MissDistanceMeters, 9);
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.TcaAtWindowEnd));
    }

    // #687: the protected spacecraft of the dense tests was at rest at the centre (r = 0, v = 0), a state with no RTN
    // frame, which the analysis needs at TCA for the relative state and the covariance. Both participants now move by the
    // same uniform motion r0 + v0 t, r0 × v0 ≠ 0, chosen so that the relative state is the former one bit for bit, and
    // with it the closed forms and the decisions at the tick: r0 = (1, 0, 0) m adds exactly to the secondary's x of 10 or
    // 50 m, its y is untouched, and v0 = (0, 0, 1e-3) m/s gives both participants the same z
    private static readonly Vector3 UniformOffsetPosition = new(1.0, 0.0, 0.0);
    private static readonly Vector3 UniformOffsetVelocity = new(0.0, 0.0, 1e-3);

    private static (Vector3 position, Vector3 velocity, Vector3 acceleration) WithUniformOffset(double t,
        (Vector3 position, Vector3 velocity, Vector3 acceleration) state) =>
        (state.position + UniformOffsetPosition + UniformOffsetVelocity * t, state.velocity + UniformOffsetVelocity, state.acceleration);

    // A rectilinear pass in a 2 s window: the secondary at (x, y0 + vy t) m from the protected spacecraft, one accepted
    // step; closest, x m, where y = 0; both with the uniform motion above
    private static (Spacecraft, PropagationSolution, Spacecraft, PropagationSolution, Window)
        RectilinearPass(int naifId, double y0, double vy, double x = 10.0)
    {
        var earth = CreateEarth();
        var start = TimeSystem.Time.J2000Tdb;
        var protectedSpacecraft = CreateSpacecraft(naifId, "P", earth, UniformOffsetPosition, UniformOffsetVelocity);
        // RectilinearPassOptions give the secondary its radius of 0 m, which the spacecraft then carries (#683)
        var secondarySpacecraft = CreateSpacecraft(naifId - 1, "S", earth, new Vector3(x, y0, 0.0) + UniformOffsetPosition,
            new Vector3(0.0, vy, 0.0) + UniformOffsetVelocity, hardBodyRadius: 0.0);
        var protectedTrajectory = CreatePropagationSolution(earth, start, durationSeconds: 2.0, stepSeconds: 2.0,
            stateAtSeconds: t => WithUniformOffset(t, (Vector3.Zero, Vector3.Zero, Vector3.Zero)));
        var secondaryTrajectory = CreatePropagationSolution(earth, start, durationSeconds: 2.0, stepSeconds: 2.0,
            stateAtSeconds: t => WithUniformOffset(t, (new Vector3(x, y0 + vy * t, 0.0), new Vector3(0.0, vy, 0.0), Vector3.Zero)));
        return (protectedSpacecraft, protectedTrajectory, secondarySpacecraft,
            secondaryTrajectory, new Window(start, start.AddSeconds(2.0)));
    }

    private static readonly ConjunctionAnalysisOptions RectilinearPassOptions = new()
    {
        SampleStep = TimeSpan.FromDays(1.0),
        MaxEventSearchStep = TimeSpan.FromDays(1.0),
        SecondaryHardBodyRadiusMeters = 0.0,
        LowRelativeSpeedThresholdMetersPerSecond = 0.0
    };

    [Theory]
    [InlineData(200.0, -100.0)] // closest at the window end, closing before it
    [InlineData(0.0, 100.0)] // closest at the window start, receding after it
    public void DenseRangeRateZeroAtAWindowEnd_AnalyzeReturnsThatEndItself_AnalyzeAllNothing(double y0, double vy)
    {
        // The closed form: y = 0 at t = −y0/vy, a window end; the range rate is zero there and the distance 10 m. A window
        // end is never a close approach (design of lot 6 of #457, §2.1: "A window end is never a TCA"; §8.3 row 7:
        // AnalyzeAll lists the interior minima only), so AnalyzeAll lists nothing; Analyze keeps its end fallback, the end
        // itself, not the tick before it (the lower tick that the bisection returned before #624)
        var (protectedCraft, protectedTrajectory, secondary, secondaryTrajectory, window) = RectilinearPass(-5524, y0, vy);
        var expected = window.StartDate.AddSeconds(-y0 / vy);

        var encounter = ConjunctionAssessment.Analyze(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions);
        var encounters = ConjunctionAssessment.AnalyzeAll(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions);

        LabelAssert.Equal(expected, encounter.EncounterState.Epoch);
        Assert.Equal(10.0, encounter.EncounterState.MissDistanceMeters, 9);
        Assert.Empty(encounters);
    }

    // The slow pass of the round-2 review of lot 6 of #457: 0.113 m/s, 50 m, closest at m = 1.00000005 s, half-way between
    // two ticks (100 ns). Over these windows (ends in ticks from 1 s) the squared distance moves by at most
    // (0.113 m/s × 8 ms)² = 8.2e-7 m², within the 1e-6 m² flatness tolerance of the seeds: before, no seed survived,
    // AnalyzeAll listed nothing and Analyze returned the midpoint (1 ms off on the first window)
    [Theory]
    [InlineData(-50_000, 30_000)] // [m − 5 ms, m + 3 ms]
    [InlineData(-10_000, 10_000)] // [m − 1 ms, m + 1 ms]
    [InlineData(-80_000, 80_000)] // [m − 8 ms, m + 8 ms]
    [InlineData(-1, 2)] // one tick inside on each side of m
    [InlineData(-3, 3)]
    [InlineData(0, 3)] // m in the first tick, half-way, which the bisection may round onto the start: the tick after it
    [InlineData(-3, 1)] // m in the last tick
    public void Dense_SlowPassFlatOverTheWindow_ListsTheMinimumOfTheRangeRate(long startTicks, long endTicks)
    {
        const double vy = 0.113;
        const double m = 1.00000005;
        var (protectedCraft, protectedTrajectory, secondary, secondaryTrajectory, fullWindow) =
            RectilinearPass(-5528, -vy * m, vy, x: 50.0);
        var oneSecond = fullWindow.StartDate.AddTicks(TimeSpan.TicksPerSecond);
        var window = new Window(oneSecond.AddTicks(startTicks), oneSecond.AddTicks(endTicks));

        var encounters = ConjunctionAssessment.AnalyzeAll(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions);
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions);

        // The closed form: the TCA within one tick of m, strictly inside the window, 50 m
        var listed = Assert.Single(encounters);
        foreach (var state in new[] { listed.EncounterState, encounter.EncounterState })
        {
            double seconds = (state.Epoch - fullWindow.StartDate).TotalSeconds;
            Assert.True(System.Math.Abs(seconds - m) <= 1.0e-7, $"TCA {seconds:F9} s, closed form {m:F9} s");
            Assert.True(state.Epoch > window.StartDate && state.Epoch < window.EndDate);
            Assert.Equal(50.0, state.MissDistanceMeters, 9);
        }

        LabelAssert.Equal(listed.EncounterState.Epoch, encounter.EncounterState.Epoch);
    }

    // The tick returned on the dense path (#624): the refinement returns the tick nearer the zero of the range rate, and a
    // zero within the first or last tick of the window that it rounds onto a window end is the tick next to that end
    // inside the window, a window end being no close approach (#621): a zero in the first tick is the tick after the start,
    // a zero in the last tick the tick before the end, whichever is nearer. Rectilinear passes closest at m = 1 s + f
    // ticks, the window [1 s + a ticks, 1 s + b ticks]; before #624, the lower tick of the last bracket, so m − f ticks
    // inside the window and the tick after the start in the first tick
    [Theory]
    // A pass at 100 m/s, 10 m, in windows of 1 s or more: the seed of the Hermite refined on the range rate
    [InlineData(100.0, 10.0, 0.8, -10_000_000L, 1L, 0L)] // 0.2 tick before the end: the tick before it, not the end
    [InlineData(100.0, 10.0, 0.6, -10_000_000L, 1L, 0L)] // 0.4 tick before the end: the tick before it
    [InlineData(100.0, 10.0, 0.2, -10_000_000L, 1L, 0L)] // 0.8 tick before the end: the tick before it, the nearer
    [InlineData(100.0, 10.0, 0.2, 0L, 10_000_000L, 1L)] // 0.2 tick after the start: the tick after it, not the start
    [InlineData(100.0, 10.0, 0.8, 0L, 10_000_000L, 1L)] // 0.8 tick after the start: the tick after it, the nearer
    [InlineData(100.0, 10.0, 0.8, -10_000_000L, 10_000_000L, 1L)] // inside: the nearer tick, after m − f ticks
    [InlineData(100.0, 10.0, 0.6, -10_000_000L, 10_000_000L, 1L)]
    [InlineData(100.0, 10.0, 0.2, -10_000_000L, 10_000_000L, 0L)]
    // The slow pass of 0.113 m/s, 50 m, in windows of a few ticks: no seed inside, the range rate bisected over the window
    [InlineData(0.113, 50.0, 0.8, -3L, 1L, 0L)]
    [InlineData(0.113, 50.0, 0.2, -3L, 1L, 0L)]
    [InlineData(0.113, 50.0, 0.2, 0L, 3L, 1L)]
    [InlineData(0.113, 50.0, 0.8, 0L, 3L, 1L)]
    [InlineData(0.113, 50.0, 0.8, -3L, 3L, 1L)]
    [InlineData(0.113, 50.0, 0.2, -3L, 3L, 0L)]
    public void Dense_TheTickNearerTheZero_TheTickInsideAtAWindowEnd(double vy, double x, double fraction,
        long startTicks, long endTicks, long expectedTicks)
    {
        double m = 1.0 + fraction * 1.0e-7;
        var (protectedCraft, protectedTrajectory, secondary, secondaryTrajectory, fullWindow) =
            RectilinearPass(-5536, -vy * m, vy, x);
        var oneSecond = fullWindow.StartDate.AddTicks(TimeSpan.TicksPerSecond);
        var window = new Window(oneSecond.AddTicks(startTicks), oneSecond.AddTicks(endTicks));

        var encounters = ConjunctionAssessment.AnalyzeAll(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions);
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions);

        // The closed form: closest at m, x m away; the TCA the expected tick, strictly inside the window
        var expected = oneSecond.AddTicks(expectedTicks);
        double offset = (expectedTicks - fraction) * 1.0e-7;
        foreach (var state in new[] { Assert.Single(encounters).EncounterState, encounter.EncounterState })
        {
            LabelAssert.Equal(expected, state.Epoch);
            Assert.True(state.Epoch > window.StartDate && state.Epoch < window.EndDate);
            Assert.Equal(System.Math.Sqrt(x * x + System.Math.Pow(vy * offset, 2.0)), state.MissDistanceMeters, 9);
        }
    }

    [Fact]
    public void Dense_SlowPassInAWindowOneTickWide_AnalyzeReturnsTheCloserEnd_AnalyzeAllNothing()
    {
        // m = 1.00000003 s inside [1 s, 1 s + 1 tick]: no epoch inside resolves the minimum, a window end is never listed;
        // Analyze returns the closer end, 1 s, never the midpoint
        const double vy = 0.113;
        const double m = 1.00000003;
        var (protectedCraft, protectedTrajectory, secondary, secondaryTrajectory, fullWindow) =
            RectilinearPass(-5532, -vy * m, vy, x: 50.0);
        var oneSecond = fullWindow.StartDate.AddTicks(TimeSpan.TicksPerSecond);
        var window = new Window(oneSecond, oneSecond.AddTicks(1));

        Assert.Empty(ConjunctionAssessment.AnalyzeAll(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions));
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions);
        LabelAssert.Equal(oneSecond, encounter.EncounterState.Epoch);
        Assert.Equal(System.Math.Sqrt(2500.0 + System.Math.Pow(vy * 3.0e-8, 2.0)),
            encounter.EncounterState.MissDistanceMeters, 9);
    }

    [Fact]
    public void Dense_WindowOfZeroLength_AnalyzeReturnsItsEpoch_AnalyzeAllNothing()
    {
        // [1 s, 1 s] on the pass closest at 1 s: no epoch inside, so AnalyzeAll lists nothing; Analyze returns that epoch
        var (protectedCraft, protectedTrajectory, secondary, secondaryTrajectory, fullWindow) = RectilinearPass(-5534, -100.0, 100.0);
        var oneSecond = fullWindow.StartDate.AddTicks(TimeSpan.TicksPerSecond);
        var window = new Window(oneSecond, oneSecond);

        Assert.Empty(ConjunctionAssessment.AnalyzeAll(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions));
        var encounter = ConjunctionAssessment.Analyze(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions);
        LabelAssert.Equal(oneSecond, encounter.EncounterState.Epoch);
        Assert.Equal(10.0, encounter.EncounterState.MissDistanceMeters, 9);
    }

    [Theory]
    [InlineData(-5.0e-3, -1.0e-3, 1.0e-3)] // still closing at the end: the end
    [InlineData(1.0e-3, 5.0e-3, 1.0e-3)] // receding from the start: the start
    public void Dense_SlowPassFlatAndMonotonic_AnalyzeReturnsTheEndWhereTheDistanceGrowsIntoIt(double startOffset,
        double endOffset, double expectedFromMinimum)
    {
        // Flat over the window and no minimum inside: Analyze returns the end the distance decreases toward, never the
        // midpoint; AnalyzeAll lists nothing
        const double vy = 0.113;
        const double m = 1.0;
        var (protectedCraft, protectedTrajectory, secondary, secondaryTrajectory, fullWindow) =
            RectilinearPass(-5530, -vy * m, vy, x: 50.0);
        var oneSecond = fullWindow.StartDate.AddTicks(TimeSpan.TicksPerSecond);
        var window = new Window(oneSecond.AddTicks((long)System.Math.Round(startOffset * TimeSpan.TicksPerSecond)),
            oneSecond.AddTicks((long)System.Math.Round(endOffset * TimeSpan.TicksPerSecond)));

        var encounter = ConjunctionAssessment.Analyze(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions);

        var expected = startOffset < 0.0 ? window.EndDate : window.StartDate;
        LabelAssert.Equal(expected, encounter.EncounterState.Epoch);
        Assert.Equal(System.Math.Sqrt(2500.0 + System.Math.Pow(vy * expectedFromMinimum, 2.0)),
            encounter.EncounterState.MissDistanceMeters, 9);
        Assert.Empty(ConjunctionAssessment.AnalyzeAll(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions));
    }

    // #687: a protected spacecraft at rest at the centre (r × v = 0) has no RTN frame at TCA, which the relative state and
    // the covariance of the encounter need: Analyze and AnalyzeAll throw, where they rotated them by a matrix with zero
    // rows (CCSDS 508.0-B-1: N = (r × v) / |r × v|)
    [Fact]
    public void Analyze_RectilinearProtectedStateAtTca_Throws()
    {
        var earth = CreateEarth();
        var start = TimeSystem.Time.J2000Tdb;
        var protectedSpacecraft = CreateSpacecraft(-5540, "P", earth, Vector3.Zero, Vector3.Zero);
        var secondarySpacecraft = CreateSpacecraft(-5541, "S", earth, new Vector3(10.0, -100.0, 0.0), new Vector3(0.0, 100.0, 0.0),
            hardBodyRadius: 0.0);
        var protectedTrajectory = CreatePropagationSolution(earth, start, durationSeconds: 2.0, stepSeconds: 2.0,
            stateAtSeconds: _ => (Vector3.Zero, Vector3.Zero, Vector3.Zero));
        var secondaryTrajectory = CreatePropagationSolution(earth, start, durationSeconds: 2.0, stepSeconds: 2.0,
            stateAtSeconds: t => (new Vector3(10.0, -100.0 + 100.0 * t, 0.0), new Vector3(0.0, 100.0, 0.0), Vector3.Zero));
        var window = new Window(start, start.AddSeconds(2.0));

        Assert.Throws<InvalidOperationException>(() => ConjunctionAssessment.Analyze(protectedSpacecraft, protectedTrajectory, secondarySpacecraft,
            secondaryTrajectory, window, RectilinearPassOptions));
        Assert.Throws<InvalidOperationException>(() => ConjunctionAssessment.AnalyzeAll(protectedSpacecraft, protectedTrajectory, secondarySpacecraft,
            secondaryTrajectory, window, RectilinearPassOptions));
    }

    [Theory]
    [InlineData(250.0, -100.0, 2.0)] // still closing at the end: the end, where the distance grows into the window
    [InlineData(50.0, 100.0, 0.0)] // receding from the start: the start
    public void Analyze_DenseMonotonicWindow_ReturnsTheEndWhereTheDistanceGrowsIntoIt_AndAnalyzeAllNothing(double y0,
        double vy, double expectedSeconds)
    {
        var (protectedCraft, protectedTrajectory, secondary, secondaryTrajectory, window) = RectilinearPass(-5526, y0, vy);

        var encounter = ConjunctionAssessment.Analyze(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions);
        var encounters = ConjunctionAssessment.AnalyzeAll(protectedCraft, protectedTrajectory, secondary, secondaryTrajectory,
            window, RectilinearPassOptions);

        LabelAssert.Equal(window.StartDate.AddSeconds(expectedSeconds), encounter.EncounterState.Epoch);
        Assert.Equal(System.Math.Sqrt(100.0 + System.Math.Pow(y0 + vy * expectedSeconds, 2.0)),
            encounter.EncounterState.MissDistanceMeters, 9);
        Assert.Empty(encounters);
    }

    // ========== Correctness Tests ==========

    [Fact]
    public void Analyze_MissDistanceMatchesIndependentComputation()
    {
        // Validate that reported miss distance matches an independent computation
        // using the same objects at the reported TCA epoch
        var earth = CreateEarth();
        var sun = new CelestialBody(Stars.Sun, Frames.Frame.ICRF, TimeSystem.Time.J2000Tdb);
        var moon = new CelestialBody(PlanetsAndMoons.Moon, Frames.Frame.ICRF, TimeSystem.Time.J2000Tdb);

        var moonSc = CreateSpacecraftFromBody(moon, -5601, "MOON_SC", 1737400.0);
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddDays(30));

        var encounter = ConjunctionAssessment.Analyze(moonSc, sun, window,
            new ConjunctionAnalysisOptions { SampleStep = TimeSpan.FromHours(6) });

        // Independently query the SAME objects (spacecraft + Sun) at TCA epoch
        var scAtTca = moonSc.GetEphemeris(encounter.EncounterState.Epoch, earth, Frames.Frame.ICRF, Aberration.None);
        var sunAtTca = sun.GetEphemeris(encounter.EncounterState.Epoch, earth, Frames.Frame.ICRF, Aberration.None);
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
        var secondary = CreateSpacecraft(-2502, "S", earth,
            new Vector3(6_800_000.0, 100.0, 50.0),
            new Vector3(0.0, 7_656.2204182967143, 0.0));
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddSeconds(1.0));

        var encounter = ConjunctionAssessment.Analyze(spacecraft, secondary, window,
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
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(5.0));

        var encounter = ConjunctionAssessment.Analyze(protected1, secondary1, window);
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
        var encounter = ConjunctionAssessment.Analyze(
            protected1,
            secondary1,
            new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(5.0)));

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
        var covTerms = new double[3, 3];
        covTerms[0, 0] = 100.0; covTerms[1, 1] = 200.0; covTerms[2, 2] = 150.0;
        var cov = new Matrix(covTerms);

        var (miss2D, cov2D) = ConjunctionAssessment.ProjectOntoEncounterPlane(relPos, relVel, cov);

        // Analytical in-plane miss
        var vHat = relVel.Normalize();
        double missAlongV = relPos.Dot(vHat);
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
        var covTerms = new double[3, 3];
        covTerms[0, 0] = sx2; covTerms[1, 1] = sy2; covTerms[2, 2] = sz2;
        var cov = new Matrix(covTerms);

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
        var covTerms = new double[3, 3];
        covTerms[0, 0] = sx2;
        covTerms[1, 1] = sy2;
        covTerms[2, 2] = sz2;
        var cov = new Matrix(covTerms);

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
        var cov2DTerms = new double[2, 2];
        cov2DTerms[0, 0] = sigma * sigma;
        cov2DTerms[1, 1] = sigma * sigma;
        var cov2D = new Matrix(cov2DTerms);

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
        var cov2DTerms = new double[2, 2];
        cov2DTerms[0, 0] = sigma * sigma;
        cov2DTerms[1, 1] = sigma * sigma;
        var cov2D = new Matrix(cov2DTerms);

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
        var cov2DTerms = new double[2, 2];
        cov2DTerms[0, 0] = sigma * sigma;
        cov2DTerms[1, 1] = sigma * sigma;
        var cov2D = new Matrix(cov2DTerms);

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
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(5.0));

        var encounter = ConjunctionAssessment.Analyze(protectedSpacecraft, secondary, window);

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
        var cov2DTerms = new double[2, 2];
        cov2DTerms[0, 0] = sigmaMax * sigmaMax;
        cov2DTerms[1, 1] = sigmaMin * sigmaMin;
        var cov2D = new Matrix(cov2DTerms);

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
        var cov2DTerms = new double[2, 2];
        cov2DTerms[0, 0] = 100.0;
        cov2DTerms[1, 1] = 1e-25; // near-singular
        var cov2D = new Matrix(cov2DTerms);

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
        var cov2DTerms = new double[2, 2];
        cov2DTerms[0, 0] = sigma * sigma;
        cov2DTerms[1, 1] = sigma * sigma;
        var cov2D = new Matrix(cov2DTerms);

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
        var cov2DTerms = new double[2, 2];
        cov2DTerms[0, 0] = 900.0;
        cov2DTerms[0, 1] = 120.0;
        cov2DTerms[1, 0] = 120.0;
        cov2DTerms[1, 1] = 400.0;
        var cov2D = new Matrix(cov2DTerms);

        var baseline = ConjunctionAssessment.ComputeFosterCollisionProbability(miss2D, cov2D, radius);

        var rotationTerms = new double[2, 2];
        rotationTerms[0, 0] = cosTheta;
        rotationTerms[0, 1] = -sinTheta;
        rotationTerms[1, 0] = sinTheta;
        rotationTerms[1, 1] = cosTheta;
        var rotation = new Matrix(rotationTerms);
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
        // NASA CARA Pc2D_Foster_UnitTest.m Omitron Test Case 01 (published 2.70601573490125e-05, itself 2.9e-6 from the
        // exact Pc of its inputs)
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
        double error = System.Math.Abs(pc.Value - OmitronCase01ExactPc) / OmitronCase01ExactPc;
        _output.WriteLine($"MEASURE #611 CARA direct worst {error:E2} (Omitron case 01)");
        Assert.True(error <= CaraDirectPcBound, $"Omitron case 01: relative error {error:E2} to the exact Pc");
    }

    [Fact]
    public void CollisionProbability_NasaAlfanoCase03_MatchesPublishedValue()
    {
        // NASA CARA Pc2D_Foster_UnitTest.m Alfano case 03 (published 1.00351176E-01, 2.3e-6 from the exact Pc)
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
        double error = System.Math.Abs(pc.Value - AlfanoCase03ExactPc) / AlfanoCase03ExactPc;
        _output.WriteLine($"MEASURE #611 CARA direct worst {error:E2} (Alfano case 03)");
        Assert.True(error <= CaraDirectPcBound, $"Alfano case 03: relative error {error:E2} to the exact Pc");
    }

    // ========== Published Single-Covariance Maximum-Pc Conformance (NASA Frisbee) ==========

    [Fact]
    public void SingleCovarianceMaximumPc_FrisbeeCriticalCovarianceMatchesPublishedMatrix()
    {
        // Frisbee AAS 15-717, Eqs. 11-14.
        var miss2D = new[] { 1000.0, 200.0 };
        var knownCov2DTerms = new double[2, 2];
        knownCov2DTerms[0, 0] = 722500.0;
        knownCov2DTerms[1, 1] = 2500.0;
        var knownCov2D = new Matrix(knownCov2DTerms);

        var criticalCovariance = ConjunctionAssessment.BuildMaximumProbabilityEncounterPlaneCovariance(miss2D, knownCov2D);

        Assert.NotNull(criticalCovariance);
        var expectedTerms = new double[2, 2];
        expectedTerms[0, 0] = 261401250.0 / 157.0;
        expectedTerms[0, 1] = 29593750.0 / 157.0;
        expectedTerms[1, 0] = 29593750.0 / 157.0;
        expectedTerms[1, 1] = 6311250.0 / 157.0;
        var expected = new Matrix(expectedTerms);
        AssertMatrixApproximatelyEqual(expected, criticalCovariance.Value, 1.0e-6);
    }

    [Theory]
    [InlineData(5.0, 4.278120354404266e-05)]
    [InlineData(10.0, 1.710716927885487e-04)]
    [InlineData(20.0, 6.834377891732130e-04)]
    [InlineData(25.0, 1.066878220462202e-03)]
    public void SingleCovarianceMaximumPc_FrisbeeTable1_MatchesPublishedApproximateOutcomes(double hardBodyRadius, double expectedPc)
    {
        // Frisbee AAS 15-717, Table 1 using the Eq. 11 example geometry (published to two or three digits); the expected
        // values are the exact maximum Pc of tools/collision_probability (rows "Frisbee AAS 15-717 table 1")
        var miss2D = new[] { 1000.0, 200.0 };
        var knownCov2DTerms = new double[2, 2];
        knownCov2DTerms[0, 0] = 722500.0;
        knownCov2DTerms[1, 1] = 2500.0;
        var knownCov2D = new Matrix(knownCov2DTerms);

        var pc = ConjunctionAssessment.ComputeSingleCovarianceMaximumCollisionProbability(miss2D, knownCov2D, hardBodyRadius);

        Assert.NotNull(pc);
        double error = System.Math.Abs(pc.Value - expectedPc) / expectedPc;
        _output.WriteLine($"MEASURE #611 CARA direct worst {error:E2} (Frisbee table 1, R {hardBodyRadius})");
        Assert.True(error <= CaraDirectPcBound, $"Frisbee max Pc, R {hardBodyRadius}: relative error {error:E2} to the exact Pc");
    }

    // ========== Published Workflow Conformance (NASA CARA Through Analyze/AnalyzeAll) ==========
    // These tests validate that the entry points preserve the published reference geometry,
    // covariance handling, and final Pc values through the full SSA workflow.

    [Fact]
    public void Analyze_NasaOmitronReferenceCase_MatchesPublishedValueThroughEntryPoint()
    {
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000Tdb.AddSeconds(10.0);
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
            protectedSpacecraft,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaxEventSearchStep = TimeSpan.FromDays(1.0),
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
            expectedPublishedPc: OmitronCase01ExactPc,
            publishedPcRelativeTolerance: CaraEntryPointPcBound);
    }

    [Fact]
    public void AnalyzeAll_NasaAlfanoCase03_MatchesPublishedValueThroughEntryPoint()
    {
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000Tdb.AddSeconds(10.0);
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
            protectedSpacecraft,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaxEventSearchStep = TimeSpan.FromDays(1.0),
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
            expectedPublishedPc: AlfanoCase03ExactPc,
            publishedPcRelativeTolerance: CaraEntryPointPcBound);
    }

    [Fact]
    public void AnalyzeAll_NasaOmitronReferenceCase_MatchesPublishedValueThroughEntryPoint()
    {
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000Tdb.AddSeconds(10.0);
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
        var protectedCraft = CreateSpacecraft(
            -5705,
            "OMITRON_P_ALL",
            earth,
            r1,
            v1,
            BuildStateCovarianceFromPosition(cov1),
            hardBodyRadius: combinedHardBodyRadius);
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
            protectedCraft,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaxEventSearchStep = TimeSpan.FromDays(1.0),
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
            expectedPublishedPc: OmitronCase01ExactPc,
            publishedPcRelativeTolerance: CaraEntryPointPcBound);
    }

    [Fact]
    public void Analyze_NasaAlfanoCase03_MatchesPublishedValueThroughEntryPoint()
    {
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000Tdb.AddSeconds(10.0);
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
            protectedSpacecraft,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaxEventSearchStep = TimeSpan.FromDays(1.0),
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
            expectedPublishedPc: AlfanoCase03ExactPc,
            publishedPcRelativeTolerance: CaraEntryPointPcBound);
    }

    // ========== Screening Tests ==========

    [Fact]
    public void Screen_EmptyCandidates_ReturnsEmpty()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-2801, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(10.0));

        var result = ConjunctionAssessment.Screen(spacecraft, Array.Empty<ILocalizable>(), window);

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
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(10.0));

        var result = ConjunctionAssessment.Screen(spacecraft,
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
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(10.0));

        var self = CreateSpacecraft(-3001, "SELF", earth,
            new Vector3(6_800_000.0, 10.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));

        var result = ConjunctionAssessment.Screen(spacecraft,
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
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(10.0));

        var encounters = ConjunctionAssessment.Screen(
            protectedSpacecraft,
            new ILocalizable[] { farCandidate, closeCandidate },
            window,
            new ScreeningOptions
            {
                Analysis = ConjunctionAnalysisOptions.Default with
                {
                    CovariancePropagation = IO.Astrodynamics.Propagator.Variational.CovariancePropagationOptions.None
                },
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
        // Two crossing encounters at 300 s, built with the closed-form two-body solution: at the encounter each
        // secondary is radially offset from the protected spacecraft, 50 m and 100 m, on an orbit inclined by ±5°, so
        // that it crosses at about 670 m/s. The close one has a tiny covariance (Pc ~ 0 at 50 m with the protected
        // σ of 5 m), the far one a large one (σ 100 m at 100 m): the far one has the higher Pc and is ranked first.
        var earth = CreateEarth();
        const double tca = 300.0;
        double speed = 7_656.2204182967143;
        var protectedAtTca = (new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, speed, 0.0));
        double tilt = 5.0 * IO.Astrodynamics.Constants.Deg2Rad;
        var closeAtTca = (new Vector3(6_800_050.0, 0.0, 0.0), new Vector3(0.0, speed * System.Math.Cos(tilt), speed * System.Math.Sin(tilt)));
        var farAtTca = (new Vector3(6_800_100.0, 0.0, 0.0), new Vector3(0.0, speed * System.Math.Cos(tilt), -speed * System.Math.Sin(tilt)));
        var protectedSpacecraft = CreateSpacecraft(-1204, "PROTECTED", earth, Before(earth, protectedAtTca, tca).Position,
            Before(earth, protectedAtTca, tca).Velocity);
        var lowProbabilityClose = CreateSpacecraft(-1205, "LOW_PC_CLOSE", earth, Before(earth, closeAtTca, tca).Position,
            Before(earth, closeAtTca, tca).Velocity, BuildCovariance(positionVariance: 1.0e-12, velocityVariance: 1.0e-6));
        var highProbabilityFar = CreateSpacecraft(-1206, "HIGH_PC_FAR", earth, Before(earth, farAtTca, tca).Position,
            Before(earth, farAtTca, tca).Velocity, BuildCovariance(positionVariance: 10_000.0, velocityVariance: 0.01));
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(10.0));

        var encounters = ConjunctionAssessment.Screen(
            protectedSpacecraft,
            new ILocalizable[] { lowProbabilityClose, highProbabilityFar },
            window,
            new ScreeningOptions
            {
                Analysis = ConjunctionAnalysisOptions.Default with
                {
                    CovariancePropagation = IO.Astrodynamics.Propagator.Variational.CovariancePropagationOptions.None
                },
                MaxMissDistanceMeters = 1_000.0,
                MaxResults = 5
            });

        Assert.Equal(2, encounters.Count);
        Assert.Equal("HIGH_PC_FAR", encounters[0].SecondaryObject.Name);
        Assert.True(encounters[0].CollisionRisk.ProbabilityOfCollision > encounters[1].CollisionRisk.ProbabilityOfCollision);
        Assert.True(encounters[0].EncounterState.MissDistanceMeters > encounters[1].EncounterState.MissDistanceMeters);
    }

    // The state dt seconds before the given one, from the closed-form two-body solution of the tests
    private static (Vector3 Position, Vector3 Velocity) Before(CelestialBody earth, (Vector3 Position, Vector3 Velocity) state, double dt)
    {
        var (x, _) = IO.Astrodynamics.Tests.Propagators.Variational.KeplerianStm.Propagate(earth.GM, state.Position, state.Velocity, -dt);
        return (new Vector3(x[0], x[1], x[2]), new Vector3(x[3], x[4], x[5]));
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
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(5.0));

        var encounter = ConjunctionAssessment.Analyze(p, s, window);

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
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(5.0));

        var encounter = ConjunctionAssessment.Analyze(p, s, window);

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
        var tca = TimeSystem.Time.J2000Tdb.AddSeconds(10.0);
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
            protectedSpacecraft,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaxEventSearchStep = TimeSpan.FromDays(1.0),
                SecondaryHardBodyRadiusMeters = 0.0,
                LowRelativeSpeedThresholdMetersPerSecond = 0.0
            });

        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.MissingSecondaryCovariance));
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.SingleCovarianceMaximumPcUsed));
        Assert.NotNull(encounter.CollisionRisk.ProbabilityOfCollision);

        double dt = (encounter.EncounterState.Epoch - tca).TotalSeconds;
        var relativePosition = (r2 + v2 * dt) - (r1 + v1 * dt);
        var relativeVelocity = v2 - v1;
        // The protected covariance is held fixed in RTN from the initial state to the found TCA.
        var heldCov1 = HoldPositionCovarianceInRtn(cov1,
            Assert.IsType<StateVector>(protectedSpacecraft.InitialOrbitalParameters), r1 + v1 * dt, v1);
        var (miss2D, knownCov2D) = ConjunctionAssessment.ProjectOntoEncounterPlane(relativePosition, relativeVelocity, heldCov1);
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
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(5.0));

        var encounter = ConjunctionAssessment.Analyze(p, s, window);

        // GetEphemeris returns states without covariance, so ResolveCovariance
        // falls back to InitialOrbitalParameters → StaleCovarianceUsed flag
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));
        // Pc should still be computed from the stale covariance
        Assert.NotNull(encounter.CollisionRisk.ProbabilityOfCollision);
    }

    // ========== Covariance Held In RTN And Stale Covariance Guard ==========

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    public void EncounterCovariance_InitialCovarianceIsHeldInRtnUpToTheEncounter(double orbitFraction)
    {
        // Circular orbit of 6,778 km. At the initial epoch the covariance is elongated along-track:
        // sigma R = 1 m, sigma T = 100 m, sigma N = 2 m. Half an orbit later the along-track axis is merely reversed,
        // so a covariance left in the inertial frame would still look right; a quarter or three quarters of an orbit
        // later it would point radially. Held in RTN, it stays aligned with the velocity at every fraction.
        var earth = CreateEarth();
        const double radius = 6_778_000.0;
        double speed = System.Math.Sqrt(earth.GM / radius);
        double period = 2.0 * System.Math.PI * radius / speed;

        var initialPosition = new Vector3(radius, 0.0, 0.0);
        var initialVelocity = new Vector3(0.0, speed, 0.0);
        var covarianceRtnTerms = new double[6, 6];
        double[] variances = { 1.0, 1.0e4, 4.0, 1.0e-6, 1.0e-4, 4.0e-6 };
        for (int i = 0; i < 6; i++)
        {
            covarianceRtnTerms[i, i] = variances[i];
        }
        var covarianceRtn = new Matrix(covarianceRtnTerms);

        var initialOrientation = new StateVector(initialPosition, initialVelocity, earth, TimeSystem.Time.J2000Tdb, Frames.Frame.ICRF);
        var spacecraft = CreateSpacecraft(-3401, "RTN_HOLD", earth, initialPosition, initialVelocity,
            initialOrientation.RotateCovarianceFromRtn(covarianceRtn));

        double angle = 2.0 * System.Math.PI * orbitFraction;
        var encounterVelocity = new Vector3(-speed * System.Math.Sin(angle), speed * System.Math.Cos(angle), 0.0);
        var encounterState = new StateVector(
            new Vector3(radius * System.Math.Cos(angle), radius * System.Math.Sin(angle), 0.0),
            encounterVelocity,
            earth,
            TimeSystem.Time.J2000Tdb.AddSeconds(period * orbitFraction),
            Frames.Frame.ICRF);

        var resolved = EncounterCovariance.Resolve(spacecraft, encounterState);

        Assert.True(resolved.FromInitialState);
        Assert.NotNull(resolved.CovarianceInertial);
        Assert.Equal(period * orbitFraction, resolved.Age!.Value.TotalSeconds, 1.0e-6);
        AssertMatrixApproximatelyEqual(covarianceRtn, encounterState.RotateCovarianceToRtn(resolved.CovarianceInertial!.Value), 1.0e-9);

        // The largest position variance (100 m squared) lies along the velocity at the encounter.
        var along = encounterVelocity.Normalize();
        double[] direction = { along.X, along.Y, along.Z };
        double varianceAlongVelocity = 0.0;
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                varianceAlongVelocity += direction[i] * resolved.CovarianceInertial.Value.Get(i, j) * direction[j];
            }
        }

        Assert.Equal(1.0e4, varianceAlongVelocity, 6);
    }

    [Fact]
    public void Analyze_CovarianceHoursOldAtTca_IsFlaggedAndCdmExportRequiresOptIn()
    {
        // Omitron geometry (NASA CARA), with the TCA three hours after the initial states that carry the covariances.
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000Tdb.AddHours(3.0);
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

        var protectedSpacecraft = CreateSpacecraft(-5711, "STALE_P", earth, r1, v1, BuildStateCovarianceFromPosition(cov1),
            hardBodyRadius: combinedHardBodyRadius);
        var secondarySpacecraft = CreateSpacecraft(-5712, "STALE_S", earth, r2, v2, BuildStateCovarianceFromPosition(cov2),
            hardBodyRadius: 0.0);
        var protectedTrajectory = CreatePropagationSolution(earth, window.StartDate, 20.0, 20.0,
            seconds => (r1 + v1 * (seconds - 10.0), v1, Vector3.Zero));
        var secondaryTrajectory = CreatePropagationSolution(earth, window.StartDate, 20.0, 20.0,
            seconds => (r2 + v2 * (seconds - 10.0), v2, Vector3.Zero));

        var encounter = ConjunctionAssessment.Analyze(
            protectedSpacecraft,
            protectedTrajectory,
            secondarySpacecraft,
            secondaryTrajectory,
            window,
            new ConjunctionAnalysisOptions
            {
                SampleStep = TimeSpan.FromDays(1.0),
                MaxEventSearchStep = TimeSpan.FromDays(1.0),
                SecondaryHardBodyRadiusMeters = 0.0,
                LowRelativeSpeedThresholdMetersPerSecond = 0.0
            });

        Assert.Equal(EncounterQualityFlags.StaleCovarianceUsed, encounter.EncounterState.QualityFlags);
        Assert.Equal(10800.0, encounter.EncounterState.ProtectedCovariance.Age!.Value.TotalSeconds, 1.0e-3);
        Assert.Equal(10800.0, encounter.EncounterState.SecondaryCovariance.Age!.Value.TotalSeconds, 1.0e-3);
        // The initial states carry the TCA geometry, so holding the covariances in RTN leaves them unchanged:
        // the exact Pc is still reproduced.
        double staleError = System.Math.Abs(encounter.CollisionRisk.ProbabilityOfCollision!.Value - OmitronCase01ExactPc) / OmitronCase01ExactPc;
        _output.WriteLine($"MEASURE #611 CARA entry point worst {staleError:E2} (Omitron case 01, 3 h old covariances)");
        Assert.True(staleError <= CaraEntryPointPcBound, $"Omitron case 01, 3 h old covariances: relative error {staleError:E2} to the exact Pc");

        var creationDate = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        var refused = Assert.Throws<InvalidOperationException>(() => encounter.ToCdm(new CdmExportOptions { CreationDateUtc = creationDate }));
        Assert.Contains("AllowStaleCovariance", refused.Message);
        Assert.Contains("10800.0 s", refused.Message);

        var cdm = encounter.ToCdm(new CdmExportOptions { CreationDateUtc = creationDate, AllowStaleCovariance = true });
        foreach (var segment in cdm.Segments)
        {
            Assert.Contains(segment.Data!.Comments, comment => comment.Contains("10800.0 s") && comment.Contains("RTN"));
            Assert.Contains(segment.Metadata!.Comments, comment => comment.Contains("10800.0 s"));
        }
    }

    [Fact]
    public void Analyze_CovarianceWithinStaleThreshold_IsNotFlagged()
    {
        // Same geometry as above with a 60 s threshold and the TCA 45 s after the initial states.
        var earth = CreateEarth();
        var tca = TimeSystem.Time.J2000Tdb.AddSeconds(45.0);
        var window = new Window(tca.AddSeconds(-10.0), tca.AddSeconds(10.0));
        var r1 = new Vector3(6_800_000.0, 0.0, 0.0);
        var v1 = new Vector3(0.0, 7_656.2204182967143, 0.0);
        var r2 = new Vector3(6_800_000.0, 20.0, 5.0);
        var v2 = new Vector3(0.0, 0.0, 7_656.2204182967143);
        var protectedSpacecraft = CreateSpacecraft(-5721, "FRESH_P", earth, r1, v1);
        var secondarySpacecraft = CreateSpacecraft(-5722, "FRESH_S", earth, r2, v2);
        var protectedTrajectory = CreatePropagationSolution(earth, window.StartDate, 20.0, 20.0,
            seconds => (r1 + v1 * (seconds - 10.0), v1, Vector3.Zero));
        var secondaryTrajectory = CreatePropagationSolution(earth, window.StartDate, 20.0, 20.0,
            seconds => (r2 + v2 * (seconds - 10.0), v2, Vector3.Zero));

        var options = new ConjunctionAnalysisOptions
        {
            SampleStep = TimeSpan.FromDays(1.0),
            MaxEventSearchStep = TimeSpan.FromDays(1.0),
            LowRelativeSpeedThresholdMetersPerSecond = 0.0
        };
        var encounter = ConjunctionAssessment.Analyze(protectedSpacecraft, protectedTrajectory,
            secondarySpacecraft, secondaryTrajectory, window, options);

        Assert.Equal(TimeSpan.FromSeconds(60), options.StaleCovarianceThreshold);
        Assert.False(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));
        Assert.Equal(45.0, encounter.EncounterState.ProtectedCovariance.Age!.Value.TotalSeconds, 1.0e-2);

        // A tighter threshold flags the same encounter.
        var strict = ConjunctionAssessment.Analyze(protectedSpacecraft, protectedTrajectory,
            secondarySpacecraft, secondaryTrajectory, window, options with { StaleCovarianceThreshold = TimeSpan.FromSeconds(30) });
        Assert.True(strict.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));
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
            protectedSpacecraft,
            secondary,
            new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(1.0)));

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
            protectedSpacecraft,
            secondary,
            new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(1.0)),
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
        var fuelTank = new FuelTank("MAIN", 25.0, 25.0, "MONO", "FT-001");
        protectedSpacecraft.AddFuelTank(fuelTank);
        protectedSpacecraft.AddEngine(new Engine("ENG", 220.0, 0.05, fuelTank, "RCS", "ENG-001"));

        var secondarySpacecraft = CreateSpacecraft(-1302, "SECONDARY", earth,
            new Vector3(6_800_000.0, 120.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddHours(3.0));
        var nominalEncounter = ConjunctionAssessment.Analyze(protectedSpacecraft, secondarySpacecraft, window);

        var options = ConjunctionAssessment.EvaluateAvoidance(
            nominalEncounter,
            new AvoidanceSearchOptions
            {
                LeadTimes = new[] { TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30) },
                DeltaVMagnitudesMetersPerSecond = new[] { 0.05, 0.10 },
                Analysis = ConjunctionAnalysisOptions.Default with { SampleStep = TimeSpan.FromMinutes(2) },
                MaxReturnedOptions = 5
            }).Options;

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
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddHours(3.0));
        var encounter = ConjunctionAssessment.Analyze(spacecraft, secondary, window);

        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter,
            new AvoidanceSearchOptions
            {
                LeadTimes = new[] { TimeSpan.FromMinutes(30) },
                DeltaVMagnitudesMetersPerSecond = new[] { 0.1 },
                MaxReturnedOptions = 3
            }).Options;

        Assert.NotEmpty(avoidance);
        Assert.All(avoidance, opt => Assert.Equal(0.0, opt.FuelCostEstimateKilograms));
    }

    [Fact]
    public void EvaluateAvoidance_EveryLeadTimeOfTheGrid_IsEvaluatedOrSkippedWithItsReason()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-3501, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary = CreateSpacecraft(-3502, "S", earth,
            new Vector3(6_800_000.0, 120.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddHours(3.0));
        var encounter = ConjunctionAssessment.Analyze(spacecraft, secondary, window);

        var study = ConjunctionAssessment.EvaluateAvoidance(encounter,
            new AvoidanceSearchOptions
            {
                LeadTimes = new[] { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30) },
                DeltaVMagnitudesMetersPerSecond = new[] { 0.1 },
                MaxReturnedOptions = 20
            });

        // The grid is the constraint (R17 of #668): each lead time gives its six options or is listed as skipped, with its
        // reason (before 11.0.0, ManeuverConstraints of 1 h to 2 h dropped both leads without a word)
        Assert.Equal(12, study.Options.Count + 6 * study.Skipped.Count);
        Assert.All(study.Skipped, skipped => Assert.Equal(AvoidanceSkipReason.BeforeWindow, skipped.Reason));
        Assert.All(study.Options, option => Assert.True(window.Contains(option.BurnEpoch)));
    }

    [Fact]
    public void EvaluateAvoidance_RankedByDeltaVWhenPcMeetsThreshold()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-3601, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary = CreateSpacecraft(-3602, "S", earth,
            new Vector3(6_800_000.0, 100.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddHours(3.0));
        var encounter = ConjunctionAssessment.Analyze(spacecraft, secondary, window);

        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter,
            new AvoidanceSearchOptions
            {
                LeadTimes = new[] { TimeSpan.FromMinutes(30) },
                DeltaVMagnitudesMetersPerSecond = new[] { 0.05, 0.25, 0.50 },
                PcThreshold = 1.0, // very lenient → all meet threshold
                MaxReturnedOptions = 20
            }).Options;

        Assert.NotEmpty(avoidance);
        // All ranking scores < 1e9 means Pc met threshold; tiebreaker is delta-V
        Assert.All(avoidance, opt => Assert.True(opt.RankingScore < 1e9,
            $"RankingScore {opt.RankingScore} should be < 1e9 when PcThreshold=1.0"));
        Assert.True(avoidance.SequenceEqual(avoidance.OrderBy(o => o.RankingScore)));
    }

    [Fact]
    public void EvaluateAvoidance_EveryValueOfTheGrid_TheBurnsBeforeTheWindowSkipped()
    {
        var earth = CreateEarth();
        var spacecraft = CreateSpacecraft(-3603, "P", earth,
            new Vector3(6_800_000.0, 0.0, 0.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var secondary = CreateSpacecraft(-3604, "S", earth,
            new Vector3(6_800_000.0, 0.0, 120.0), new Vector3(0.0, 7_656.2204182967143, 0.0));
        var window = new Window(TimeSystem.Time.J2000Tdb, TimeSystem.Time.J2000Tdb.AddMinutes(10.0));
        var encounter = ConjunctionAssessment.Analyze(spacecraft, secondary, window);

        // No close approach: the TCA is the closest window end, the end here (#621: the midpoint before 11.0.0), so that a
        // lead time of 12 min puts the burn before the window
        LabelAssert.Equal(window.EndDate, encounter.EncounterState.Epoch);
        var study = ConjunctionAssessment.EvaluateAvoidance(encounter,
            new AvoidanceSearchOptions
            {
                LeadTimes = new[]
                {
                    TimeSpan.FromSeconds(30.0),
                    TimeSpan.FromMinutes(2.0),
                    TimeSpan.FromMinutes(12.0)
                },
                DeltaVMagnitudesMetersPerSecond = new[] { 0.1, 0.25 },
                MaxReturnedOptions = 30
            });

        // Every value of the grid (before 11.0.0, ManeuverConstraints of 1 min to 20 min and 0.2 m/s dropped the lead of
        // 30 s and the 0.25 m/s, and a magnitude of 0 was dropped: it throws at init now), but the burn before the window
        Assert.Equal(2 * 2 * 6, study.Options.Count);
        Assert.All(study.Options, option =>
            Assert.True(window.Contains(option.BurnEpoch), "Burn epoch must stay inside the screening window"));
        Assert.Equal(new[] { 30.0, 120.0 }, study.Options
            .Select(option => System.Math.Round((encounter.EncounterState.Epoch - option.BurnEpoch).TotalSeconds, 6))
            .Distinct().OrderBy(lead => lead));
        Assert.Equal(new[] { 0.1, 0.25 }, study.Options
            .Select(option => System.Math.Round(option.DeltaVInertial.Magnitude(), 12)).Distinct().OrderBy(m => m));
        var skipped = Assert.Single(study.Skipped);
        Assert.Equal(TimeSpan.FromMinutes(12.0), skipped.LeadTime);
        Assert.Equal(AvoidanceSkipReason.BeforeWindow, skipped.Reason);
    }

    // ========== Helpers ==========

    private static CelestialBody CreateEarth()
    {
        return new CelestialBody(PlanetsAndMoons.Earth, Frames.Frame.ICRF, TimeSystem.Time.J2000Tdb);
    }

    private static Spacecraft CreateSpacecraft(int naifId, string name, CelestialBody earth, Vector3 position, Vector3 velocity, double hardBodyRadius = 8.0)
    {
        return CreateSpacecraft(naifId, name, earth, position, velocity, BuildCovariance(positionVariance: 25.0, velocityVariance: 0.01), hardBodyRadius);
    }

    private static Spacecraft CreateSpacecraft(int naifId, string name, CelestialBody earth, Vector3 position, Vector3 velocity, Matrix covariance, double hardBodyRadius = 8.0)
    {
        var orbit = new StateVector(position, velocity, earth, TimeSystem.Time.J2000Tdb, Frames.Frame.ICRF, covariance);
        return new Spacecraft(naifId, name, 120.0, orbit, 150.0, new Clock($"{name}_CLK", 256), sectionalArea: 2.0, dragCoefficient: 2.2, cosparId: $"2026-001{name[0]}", solarRadiationCoefficient: 1.2, hardBodyRadius: hardBodyRadius);
    }

    private static Spacecraft CreateSpacecraftWithoutCovariance(int naifId, string name, CelestialBody earth, Vector3 position, Vector3 velocity, double hardBodyRadius = 8.0)
    {
        var orbit = new StateVector(position, velocity, earth, TimeSystem.Time.J2000Tdb, Frames.Frame.ICRF);
        return new Spacecraft(naifId, name, 120.0, orbit, 150.0, new Clock($"{name}_CLK", 256), sectionalArea: 2.0, dragCoefficient: 2.2, cosparId: $"2026-001{name[0]}", solarRadiationCoefficient: 1.2, hardBodyRadius: hardBodyRadius);
    }

    private static Spacecraft CreateSpacecraftFromBody(CelestialBody body, int naifId, string name, double hardBodyRadius = 0.0)
    {
        var earth = CreateEarth();
        var state = body.GetEphemeris(TimeSystem.Time.J2000Tdb, earth, Frames.Frame.ICRF, Aberration.None);
        return new Spacecraft(naifId, name, 1000.0, state, 2000.0, new Clock($"{name}_CLK", 256), sectionalArea: 10.0, dragCoefficient: 2.2, cosparId: $"2026-001{name[0]}", solarRadiationCoefficient: 1.2, hardBodyRadius: hardBodyRadius);
    }

    private static Matrix BuildCovariance(double positionVariance, double velocityVariance)
    {
        var covarianceTerms = new double[6, 6];
        covarianceTerms[0, 0] = positionVariance;
        covarianceTerms[1, 1] = positionVariance;
        covarianceTerms[2, 2] = positionVariance;
        covarianceTerms[3, 3] = velocityVariance;
        covarianceTerms[4, 4] = velocityVariance;
        covarianceTerms[5, 5] = velocityVariance;
        var covariance = new Matrix(covarianceTerms);
        return covariance;
    }

    private static Matrix BuildPositionCovariance(double[,] values, double scale = 1.0)
    {
        var matrixTerms = new double[3, 3];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                matrixTerms[row, column] = values[row, column] * scale;
            }
        }
        var matrix = new Matrix(matrixTerms);

        return matrix;
    }

    private static Matrix BuildStateCovarianceFromPosition(Matrix positionCovariance)
    {
        var covarianceTerms = new double[6, 6];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                covarianceTerms[row, column] = positionCovariance.Get(row, column);
            }
        }
        var covariance = new Matrix(covarianceTerms);

        return covariance;
    }

    private static Matrix RotateRtnPositionCovarianceToInertial(Vector3 position, Vector3 velocity, Matrix covarianceRtn)
    {
        var rotation = CreateRtnRotation(position, velocity);
        return rotation.Transpose() * covarianceRtn * rotation;
    }

    private void AssertEncounterMatchesDirectExpectation(
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
        // The covariances are attached to the initial states and held fixed in RTN up to the encounter epoch.
        // The fixtures put the TCA geometry on the initial states, so this transport is a rotation of about
        // 1e-7 rad (the found TCA is within 1e-4 s of the nominal one); it is applied here with the test's own
        // RTN construction so that the direct and entry-point Pc can be compared to rounding level.
        var protectedInitial = Assert.IsType<StateVector>(encounter.ProtectedSpacecraft.InitialOrbitalParameters);
        var secondaryInitial = Assert.IsType<StateVector>(encounter.SecondaryObject.InitialOrbitalParameters);
        primaryPositionCovariance = HoldPositionCovarianceInRtn(primaryPositionCovariance, protectedInitial,
            primaryPositionAtEpoch, primaryVelocityAtEpoch);
        secondaryPositionCovariance = HoldPositionCovarianceInRtn(secondaryPositionCovariance, secondaryInitial,
            secondaryPositionAtEpoch, secondaryVelocityAtEpoch);

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

        // The covariances are 10 s old at TCA, below the 60 s stale threshold: no quality flag.
        Assert.Equal(EncounterQualityFlags.None, encounter.EncounterState.QualityFlags);
        Assert.NotNull(encounter.EncounterState.ProtectedCovariance.Age);
        Assert.NotNull(encounter.EncounterState.SecondaryCovariance.Age);
        Assert.Equal(10.0, encounter.EncounterState.ProtectedCovariance.Age!.Value.TotalSeconds, 1.0e-3);
        Assert.Equal(10.0, encounter.EncounterState.SecondaryCovariance.Age!.Value.TotalSeconds, 1.0e-3);
        Assert.Equal(expectedCombinedHardBodyRadius, encounter.CollisionRisk.CombinedHardBodyRadiusMeters, 12);
        Assert.NotNull(encounter.CollisionRisk.ProbabilityOfCollision);
        Assert.NotNull(expectedPc);
        Assert.True(System.Math.Abs(encounter.CollisionRisk.ProbabilityOfCollision.Value - expectedPc.Value) < 1.0e-12,
            $"Entry-point Pc {encounter.CollisionRisk.ProbabilityOfCollision.Value:E12} should match direct Pc {expectedPc.Value:E12}");
        double publishedError = System.Math.Abs(encounter.CollisionRisk.ProbabilityOfCollision.Value - expectedPublishedPc) / expectedPublishedPc;
        _output.WriteLine($"MEASURE #611 CARA entry point worst {publishedError:E2} (expected Pc {expectedPublishedPc:R})");
        Assert.True(publishedError <= publishedPcRelativeTolerance,
            $"Relative error {publishedError:E2} to the expected Pc {expectedPublishedPc:E12}");

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

    /// <summary>
    /// Holds a 3x3 position covariance fixed in RTN from the initial state to the given epoch state:
    /// C_epoch = R_epoch^T R_initial C R_initial^T R_epoch.
    /// </summary>
    private static Matrix HoldPositionCovarianceInRtn(Matrix positionCovariance, StateVector initialState, Vector3 positionAtEpoch,
        Vector3 velocityAtEpoch)
    {
        var initialRotation = CreateRtnRotation(initialState.Position, initialState.Velocity);
        var epochRotation = CreateRtnRotation(positionAtEpoch, velocityAtEpoch);
        var covarianceRtn = initialRotation * positionCovariance * initialRotation.Transpose();
        return epochRotation.Transpose() * covarianceRtn * epochRotation;
    }

    private static Matrix CreateRtnRotation(Vector3 position, Vector3 velocity)
    {
        var radial = position.Normalize();
        var crossTrack = position.Cross(velocity).Normalize();
        var inTrack = crossTrack.Cross(radial).Normalize();
        var rotationTerms = new double[3, 3];
        rotationTerms[0, 0] = radial.X; rotationTerms[0, 1] = radial.Y; rotationTerms[0, 2] = radial.Z;
        rotationTerms[1, 0] = inTrack.X; rotationTerms[1, 1] = inTrack.Y; rotationTerms[1, 2] = inTrack.Z;
        rotationTerms[2, 0] = crossTrack.X; rotationTerms[2, 1] = crossTrack.Y; rotationTerms[2, 2] = crossTrack.Z;
        var rotation = new Matrix(rotationTerms);
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
        var resultTerms = new double[left.Rows, left.Columns];
        for (int row = 0; row < left.Rows; row++)
        {
            for (int column = 0; column < left.Columns; column++)
            {
                resultTerms[row, column] = left.Get(row, column) + right.Get(row, column);
            }
        }
        var result = new Matrix(resultTerms);

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

        public IEnumerable<StateVector> GetEphemeris(
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

        public StateVector GetEphemeris(in TimeSystem.Time epoch, ILocalizable observer, Frames.Frame frame, Aberration aberration)
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

        public IEnumerable<StateVector> GetEphemeris(
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

        public StateVector GetEphemeris(in TimeSystem.Time epoch, ILocalizable observer, Frames.Frame frame, Aberration aberration)
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
