// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.Propagators.Variational;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// A conjunction 2 h after the initial state of a LEO at 400 km with EGM2008 10×10, NRLMSISE-00 drag, the Moon, the Sun and
/// solar radiation pressure; the secondary object is a two-body orbit given at the TCA, 200 m from the protected one
/// (120 m radial, 160 m normal), crossing at 70°.
/// </summary>
public sealed class AvoidanceFallbackCase
{
    internal static readonly TimeSystem.Time Start = new(2024, 3, 1, 0, 0, 0, frame: TimeFrame.TDBFrame);
    internal static readonly TimeSystem.Time Tca = Start.AddHours(2.0);
    internal static readonly ManeuverExecutionError Gates = new()
        { ResolutionSigma = 0.05, ShutoffSigma = 0.01, AutopilotSigma = 0.02, PointingSigma = 0.005 };

    // The options at 1e-13 in RK7(8), their references at 1e-14 (a reference at 1e-13 is off by up to 1.8e-5 m at the TCA,
    // its own integration error); by Velocity-Verlet at 1 s
    internal const double Tolerance = 1e-13;
    internal const double ReferenceTolerance = 1e-14;
    internal static readonly TimeSpan VelocityVerletStep = TimeSpan.FromSeconds(1.0);

    public AvoidanceFallbackCase()
    {
        TestKernels.EnsureSolarSystem();
        Earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new Nrlmsise00Model(SpaceWeather.Nominal));
        Bodies = new CelestialItem[] { Earth, new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start), new CelestialBody(Stars.Sun) };
        PointMassEarth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
        var orbit = new KeplerianElements(6778137.0, 0.001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.4, 1.1, 0.3, Earth,
            Start, Frames.Frame.ICRF).ToStateVector();
        ProtectedStart = ConjunctionCovarianceCase.WithRtnCovariance(orbit, new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 });
        ProtectedStartWithoutCovariance = orbit;

        // Relative to the point-mass Earth, the central body of its propagation on its two-body orbit
        var reference = ReferenceCase.NewSpacecraft(-6700, ProtectedStart);
        Propagate(reference, true);
        var secondary = SecondaryNear(reference.GetEphemeris(Tca, Earth, Frames.Frame.ICRF, Aberration.None).ToStateVector());
        SecondaryAtTca = new StateVector(secondary.Position, secondary.Velocity, PointMassEarth, Tca, secondary.Frame,
            secondary.Covariance);
    }

    internal CelestialBody Earth { get; }

    /// <summary>The Earth without geopotential, for the secondary object propagated on its two-body orbit.</summary>
    internal CelestialBody PointMassEarth { get; }

    /// <summary>The secondary spacecraft propagated in RK7(8) on its two-body orbit over the window.</summary>
    internal PropagationSolution PropagateTwoBody(Spacecraft secondary) =>
        secondary.Propagate(PropagationWindow, new CelestialItem[] { PointMassEarth }, new RK78Integrator(1e-12, 1e-12), false,
            false, TimeSpan.FromSeconds(60.0));

    internal CelestialItem[] Bodies { get; }

    internal StateVector ProtectedStart { get; }

    internal StateVector ProtectedStartWithoutCovariance { get; }

    internal StateVector SecondaryAtTca { get; }

    internal static Window PropagationWindow => new(Start, Start.AddHours(3.0));

    internal static Window ScreeningWindow => new(Start, Start.AddHours(2.5));

    // A state at the TCA 200 m from the given one, its velocity turned by 70° about the radial direction, with a covariance
    internal static StateVector SecondaryNear(StateVector protectedAtTca)
    {
        var radial = protectedAtTca.Position.Normalize();
        var normal = protectedAtTca.Position.Cross(protectedAtTca.Velocity).Normalize();
        double angle = 70.0 * IO.Astrodynamics.Constants.Deg2Rad;
        var v = protectedAtTca.Velocity;
        var velocity = v * System.Math.Cos(angle) + radial.Cross(v) * System.Math.Sin(angle) +
                       radial * (radial * v) * (1.0 - System.Math.Cos(angle));
        return ConjunctionCovarianceCase.WithRtnCovariance(
            new StateVector(protectedAtTca.Position + radial * 120.0 + normal * 160.0, velocity, protectedAtTca.Observer,
                protectedAtTca.Epoch, protectedAtTca.Frame), new[] { 80.0, 300.0, 60.0, 0.3, 0.08, 0.05 });
    }

    internal static Spacecraft WithEngine(int naifId, StateVector start, out Engine engine)
    {
        var spacecraft = ReferenceCase.NewSpacecraft(naifId, start);
        var tank = new FuelTank("tank" + -naifId, "model", "sn1", 100.0, 100.0);
        engine = new Engine("engine" + -naifId, "model", "sn1", 300.0, 10.0, tank);
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        return spacecraft;
    }

    /// <summary>The spacecraft propagated over the window, in RK7(8) or by Velocity-Verlet, with drag and SRP.</summary>
    internal PropagationSolution Propagate(Spacecraft spacecraft, bool rk78, CovariancePropagationOptions options = null,
        double tolerance = Tolerance)
    {
        return rk78
            ? spacecraft.Propagate(PropagationWindow, Bodies, new RK78Integrator(tolerance, tolerance), true, true,
                TimeSpan.FromSeconds(60.0), options)
            : spacecraft.Propagate(PropagationWindow, Bodies, true, true, VelocityVerletStep);
    }
}

/// <summary>
/// The avoidance options of an encounter whose protected covariance is not propagated (#402): propagated with the force
/// model of the protected participant's propagation, the covariance held in RTN; the two-body orbit, stated, without one.
/// </summary>
public class AvoidanceFallbackTests : IClassFixture<AvoidanceFallbackCase>
{
    private readonly AvoidanceFallbackCase _case;
    private readonly ITestOutputHelper _output;

    public AvoidanceFallbackTests(AvoidanceFallbackCase @case, ITestOutputHelper output)
    {
        _case = @case;
        _output = output;
    }

    // One lead time and one magnitude: six options
    private static readonly AvoidanceSearchOptions Options = new()
    {
        LeadTimes = new[] { TimeSpan.FromHours(1.0) },
        DeltaVMagnitudesMetersPerSecond = new[] { 0.1 },
        MaxReturnedOptions = 6
    };

    private static readonly ConjunctionAnalysisOptions HeldInRtn = new() { CovariancePropagation = null };

    private Spacecraft Secondary(int naifId) => ReferenceCase.NewSpacecraft(naifId, _case.SecondaryAtTca);

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void EvaluateAvoidance_CovarianceHeldInRtn_EachOptionIsThePropagationWithItsBurn(bool rk78, bool stateTransitionOnly)
    {
        // Arrange: the protected spacecraft with an initial covariance, the analysis with the covariance held in RTN; in
        // RK7(8) its own propagation may carry the state transition matrix, which the fallback does not read
        int id = rk78 ? (stateTransitionOnly ? -6710 : -6720) : -6730;
        var protectedCraft = AvoidanceFallbackCase.WithEngine(id, _case.ProtectedStart, out _);
        _case.Propagate(protectedCraft, rk78,
            stateTransitionOnly ? new CovariancePropagationOptions { Mode = CovariancePropagationMode.StateTransitionOnly } : null);
        var secondary = Secondary(id - 1);
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondary,
            AvoidanceFallbackCase.ScreeningWindow, HeldInRtn);
        Assert.Equal(CovarianceSource.InitialStateHeldInRtn, encounter.EncounterState.ProtectedCovariance.Source);

        // Act
        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter, Options);

        // Assert
        AssertOptionsAreTheirReferences(avoidance, rk78, secondary, id - 2, _case.ProtectedStart);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EvaluateAvoidance_TrajectoryOverload_EachOptionIsThePropagationWithItsBurn(bool rk78)
    {
        // Arrange: the solutions passed to the analysis, both propagated by the same integrator
        int id = rk78 ? -6740 : -6750;
        var protectedCraft = AvoidanceFallbackCase.WithEngine(id, _case.ProtectedStart, out _);
        var protectedTrajectory = _case.Propagate(protectedCraft, rk78);
        var secondary = Secondary(id - 1);
        var secondaryTrajectory = _case.PropagateTwoBody(secondary);
        var encounter = ConjunctionAssessment.AnalyzeAll(new ProtectedSpacecraftProfile(protectedCraft), protectedTrajectory,
                secondary, secondaryTrajectory, AvoidanceFallbackCase.ScreeningWindow, HeldInRtn)
            .OrderBy(e => e.EncounterState.MissDistanceMeters).First();

        // Act: the protected spacecraft propagated again afterwards, with another window: the options follow the
        // trajectory passed to the analysis, not the last propagation of the spacecraft
        _case.Propagate(protectedCraft, !rk78);
        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter, Options);

        // Assert
        Assert.Same(protectedTrajectory, encounter.ProtectedTrajectory);
        AssertOptionsAreTheirReferences(avoidance, rk78, secondary, id - 2, _case.ProtectedStart);
    }

    [Fact]
    public void EvaluateAvoidance_RK78WithoutInitialCovariance_PropagatesTheOptionsWithoutCovariance()
    {
        // Arrange
        var protectedCraft = AvoidanceFallbackCase.WithEngine(-6760, _case.ProtectedStartWithoutCovariance, out _);
        _case.Propagate(protectedCraft, true);
        var secondary = Secondary(-6761);
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondary,
            AvoidanceFallbackCase.ScreeningWindow);
        Assert.Equal(CovarianceSource.None, encounter.EncounterState.ProtectedCovariance.Source);

        // Act
        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter, Options);

        // Assert: propagated, without a protected covariance (the single-covariance maximum Pc)
        AssertOptionsAreTheirReferences(avoidance, true, secondary, -6762, _case.ProtectedStartWithoutCovariance);
        Assert.All(avoidance, o => Assert.Equal(CovarianceSource.None,
            o.PostManeuverEncounter.EncounterState.ProtectedCovariance.Source));
    }

    [Fact]
    public void EvaluateAvoidance_ManeuversBeforeAndAfterTheBurn_TheOptionStartsAfterTheFirstAndLeavesOutTheSecond()
    {
        // Arrange: a burn 15 min after the start spends 9 % of the mass; another, 1.5 h after the start, between the burns
        // of the options (1 h before the TCA) and the TCA
        var protectedCraft = AvoidanceFallbackCase.WithEngine(-6770, _case.ProtectedStart, out var engine);
        var first = new FixedImpulse(_case.Earth, AvoidanceFallbackCase.Start,
            AvoidanceFallbackCase.Start.AddMinutes(15.0), _case.ProtectedStart.Velocity.Normalize() * 0.02, engine);
        first.SetNextManeuver(new FixedImpulse(_case.Earth, AvoidanceFallbackCase.Start,
            AvoidanceFallbackCase.Start.AddHours(1.5), _case.ProtectedStart.Velocity.Normalize() * 300.0, engine));
        protectedCraft.SetStandbyManeuver(first);
        _case.Propagate(protectedCraft, true);
        var secondary = Secondary(-6771);
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondary,
            new Window(AvoidanceFallbackCase.Start.AddMinutes(30.0), AvoidanceFallbackCase.ScreeningWindow.EndDate), HeldInRtn);

        // Act
        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter, Options with { LeadTimes = new[] { TimeSpan.FromMinutes(40.0) } });

        // Assert: each option against the spacecraft propagated from the start with the first burn and the burn of the
        // option, its mass after the first burn less its fuel; the second burn is not part of the option (open loop)
        Assert.NotEmpty(avoidance);
        foreach (var option in avoidance.Take(2))
        {
            var reference = AvoidanceFallbackCase.WithEngine(-6772, _case.ProtectedStart, out var referenceEngine);
            var referenceFirst = new FixedImpulse(_case.Earth, AvoidanceFallbackCase.Start,
                AvoidanceFallbackCase.Start.AddMinutes(15.0), _case.ProtectedStart.Velocity.Normalize() * 0.02, referenceEngine);
            referenceFirst.SetNextManeuver(new FixedImpulse(_case.Earth, AvoidanceFallbackCase.Start,
                option.BurnEpoch, option.DeltaVInertial, referenceEngine));
            reference.SetStandbyManeuver(referenceFirst);
            var referenceSolution = _case.Propagate(reference, true, tolerance: AvoidanceFallbackCase.ReferenceTolerance);
            AssertOptionIsItsReference(option, reference, referenceSolution, secondary, true);
        }
    }

    [Fact]
    public void EvaluateAvoidance_NotPropagated_TheTwoBodyOrbitWithTheInitialCovarianceHeldInRtn()
    {
        // Arrange: the protected spacecraft given by its initial state, not propagated: its ephemeris is the two-body orbit
        var protectedOrbit = _case.ProtectedStart;
        var protectedCraft = ReferenceCase.NewSpacecraft(-6780,
            ConjunctionCovarianceCase.WithRtnCovariance(protectedOrbit, new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 }));
        var secondary = ReferenceCase.NewSpacecraft(-6781,
            AvoidanceFallbackCase.SecondaryNear(protectedOrbit.ToStateVector(AvoidanceFallbackCase.Tca)));
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondary,
            AvoidanceFallbackCase.ScreeningWindow);

        // Act
        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter, Options);

        // Assert: the two-body orbit of the state after the burn, stated; the initial covariance held in RTN from the
        // initial state to the TCA of the option (before #402, none), aged from the initial state
        Assert.Equal(6, avoidance.Count);
        foreach (var option in avoidance)
        {
            var post = option.PostManeuverEncounter;
            var tca = post.EncounterState.Epoch;
            var burnState = protectedOrbit.ToStateVector(option.BurnEpoch);
            var expected = new StateVector(burnState.Position, burnState.Velocity + option.DeltaVInertial, burnState.Observer,
                option.BurnEpoch, burnState.Frame).ToStateVector(tca);
            Assert.Equal(AvoidanceTrajectory.TwoBody, option.Trajectory);
            Assert.True((post.ProtectedState.Position - expected.Position).Magnitude() < 1e-6);
            Assert.Equal(CovarianceSource.InitialStateHeldInRtn, post.EncounterState.ProtectedCovariance.Source);
            Assert.Equal((tca - AvoidanceFallbackCase.Start).Duration(), post.EncounterState.ProtectedCovarianceAge);
            Assert.True(post.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));
            AssertHeldInRtn(protectedOrbit, post.ProtectedCovariance.CovarianceInertial.Value, post.ProtectedState);
        }
    }

    [Fact]
    public void EvaluateAvoidance_Tle_TheTwoBodyOrbitStated()
    {
        // Arrange: a spacecraft given by a TLE, not propagated (SGP4), without covariance
        var tle = new TLE("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var protectedCraft = new Spacecraft(-6790, "TLE6790", 100.0, 1000.0, new Clock("tle6790", 65536), tle, 10.0, 2.2, null, 1.5);
        var tca = tle.Epoch.AddHours(2.0);
        var secondary = ReferenceCase.NewSpacecraft(-6791,
            AvoidanceFallbackCase.SecondaryNear(protectedCraft.GetEphemeris(tca, tle.Observer, Frames.Frame.ICRF, Aberration.None).ToStateVector()));
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondary,
            new Window(tle.Epoch, tle.Epoch.AddHours(2.5)));

        // Act
        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter, Options);

        // Assert
        Assert.Equal(6, avoidance.Count);
        Assert.All(avoidance, o =>
        {
            Assert.Equal(AvoidanceTrajectory.TwoBody, o.Trajectory);
            Assert.Equal(CovarianceSource.None, o.PostManeuverEncounter.EncounterState.ProtectedCovariance.Source);
        });
    }

    [Fact]
    public void EvaluateAvoidance_Fallback_IgnoresTheExecutionErrorAndKeepsThePropagatedSecondaryCovariance()
    {
        // Arrange: the protected spacecraft by Velocity-Verlet (covariance held in RTN), the secondary in RK7(8) with its
        // initial covariance (propagated at the encounter, the default options)
        var protectedCraft = AvoidanceFallbackCase.WithEngine(-6800, _case.ProtectedStart, out _);
        _case.Propagate(protectedCraft, false);
        var secondaryAtStart = _case.SecondaryAtTca.ToStateVector(AvoidanceFallbackCase.Start);
        var secondary = ReferenceCase.NewSpacecraft(-6801, new StateVector(secondaryAtStart.Position, secondaryAtStart.Velocity,
            secondaryAtStart.Observer, secondaryAtStart.Epoch, secondaryAtStart.Frame, _case.ProtectedStart.Covariance));
        _case.PropagateTwoBody(secondary);
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondary,
            AvoidanceFallbackCase.ScreeningWindow);
        Assert.Equal(CovarianceSource.InitialStateHeldInRtn, encounter.EncounterState.ProtectedCovariance.Source);
        Assert.Equal(CovarianceSource.Propagated, encounter.EncounterState.SecondaryCovariance.Source);

        // Act
        var without = ConjunctionAssessment.EvaluateAvoidance(encounter, Options);
        var with = ConjunctionAssessment.EvaluateAvoidance(encounter, Options with { ExecutionError = AvoidanceFallbackCase.Gates });

        // Assert: the same options and probabilities; the secondary keeps its propagated covariance
        Assert.Equal(without.Select(o => o.PostManeuverEncounter.CollisionRisk.ProbabilityOfCollision),
            with.Select(o => o.PostManeuverEncounter.CollisionRisk.ProbabilityOfCollision));
        Assert.All(with, o =>
        {
            Assert.Equal(AvoidanceTrajectory.Propagated, o.Trajectory);
            Assert.Equal(CovarianceSource.InitialStateHeldInRtn, o.PostManeuverEncounter.EncounterState.ProtectedCovariance.Source);
            Assert.Equal(CovarianceSource.Propagated, o.PostManeuverEncounter.EncounterState.SecondaryCovariance.Source);
        });
    }

    public static System.Collections.Generic.IEnumerable<object[]> InheritanceRows()
    {
        foreach (var model in new[] { "TwoBody", "VelocityVerlet", "Rk78" })
        {
            foreach (var entry in new[] { "Analyze", "AnalyzeAll", "AnalyzeDense", "Screen" })
            {
                // Neither participant propagated: no trajectory to pass
                if (model != "TwoBody" || entry != "AnalyzeDense")
                    yield return new object[] { model, entry };
            }
        }
    }

    // The settings of the encounter: a stale threshold of a day, a low-speed threshold of 100 km/s (above the 8.8 km/s of
    // the crossing at 70°, so that only the inherited threshold raises the flag), the secondary without a radius of its
    // own given 2 m (the protected spacecraft has none)
    private const double InheritedLowSpeedThreshold = 1.0e5;

    private static readonly ConjunctionAnalysisOptions InheritedSettings = HeldInRtn with
    {
        StaleCovarianceThreshold = TimeSpan.FromDays(1.0),
        SecondaryHardBodyRadiusMeters = 2.0,
        LowRelativeSpeedThresholdMetersPerSecond = InheritedLowSpeedThreshold
    };

    // The encounter of the protected spacecraft (two-body, or propagated by Velocity-Verlet or in RK7(8)) analyzed by the
    // entry point with the settings above (Screen: its radius and low-speed threshold, the stale threshold its 60 s)
    private (EncounterCase Encounter, TimeSpan StaleThreshold) InheritanceEncounter(string model, string entry, int id)
    {
        Spacecraft protectedCraft;
        Spacecraft secondary;
        PropagationSolution protectedTrajectory = null;
        if (model == "TwoBody")
        {
            protectedCraft = ReferenceCase.NewSpacecraft(id, _case.ProtectedStart);
            secondary = ReferenceCase.NewSpacecraft(id - 1,
                AvoidanceFallbackCase.SecondaryNear(_case.ProtectedStart.ToStateVector(AvoidanceFallbackCase.Tca)));
        }
        else
        {
            protectedCraft = AvoidanceFallbackCase.WithEngine(id, _case.ProtectedStart, out _);
            protectedTrajectory = _case.Propagate(protectedCraft, model == "Rk78");
            secondary = Secondary(id - 1);
        }

        var profile = new ProtectedSpacecraftProfile(protectedCraft);
        var window = AvoidanceFallbackCase.ScreeningWindow;
        var encounter = entry switch
        {
            "Analyze" => ConjunctionAssessment.Analyze(profile, secondary, window, InheritedSettings),
            "AnalyzeAll" => ConjunctionAssessment.AnalyzeAll(profile, secondary, window, InheritedSettings)
                .OrderBy(e => e.EncounterState.MissDistanceMeters).First(),
            "AnalyzeDense" => ConjunctionAssessment.Analyze(profile, protectedTrajectory, secondary,
                _case.PropagateTwoBody(secondary), window, InheritedSettings),
            _ => Assert.Single(ConjunctionAssessment.Screen(profile, new ILocalizable[] { secondary }, window,
                new ScreeningOptions
                {
                    DefaultSecondaryHardBodyRadiusMeters = 2.0,
                    LowRelativeSpeedThresholdMetersPerSecond = InheritedLowSpeedThreshold,
                    MaxMissDistanceMeters = double.MaxValue
                }))
        };
        return (encounter, entry == "Screen" ? TimeSpan.FromSeconds(60.0) : InheritedSettings.StaleCovarianceThreshold);
    }

    [Theory]
    [MemberData(nameof(InheritanceRows))]
    public void EvaluateAvoidance_InheritsTheEncounterSettings(string model, string entry)
    {
        // Arrange
        int id = -6900 - 100 * Array.IndexOf(new[] { "TwoBody", "VelocityVerlet", "Rk78" }, model) -
                 10 * Array.IndexOf(new[] { "Analyze", "AnalyzeAll", "AnalyzeDense", "Screen" }, entry);
        var (encounter, staleThreshold) = InheritanceEncounter(model, entry, id);
        Assert.Equal(2.0, encounter.CollisionRisk.CombinedHardBodyRadiusMeters);

        // Act
        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter, Options);

        // Assert: each option re-analyzed with the settings of the encounter (#683: the radius of the avoidance options, 5 m
        // for the secondary, their stale threshold of 60 s and their low-speed threshold of 1 m/s): its radius, the stale
        // flag by the rule of the encounter's threshold on the sources and ages the option reports, the low-speed flag by
        // the rule of the encounter's threshold on the relative speed of the option (below 100 km/s: raised)
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.LowRelativeVelocityEncounter));
        Assert.Equal(6, avoidance.Count);
        int stale = 0;
        foreach (var option in avoidance)
        {
            var post = option.PostManeuverEncounter;
            var state = post.EncounterState;
            bool expectedStale = IsStale(state.ProtectedCovariance, staleThreshold) || IsStale(state.SecondaryCovariance, staleThreshold);
            stale += expectedStale ? 1 : 0;
            Assert.Equal(encounter.CollisionRisk.CombinedHardBodyRadiusMeters, post.CollisionRisk.CombinedHardBodyRadiusMeters);
            Assert.Equal(expectedStale, state.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));
            Assert.True(state.RelativeState.RelativeVelocityInertial.Magnitude() < InheritedLowSpeedThreshold);
            Assert.True(state.QualityFlags.HasFlag(EncounterQualityFlags.LowRelativeVelocityEncounter));
        }

        // The protected covariance is held in RTN from the initial state, aged 1 h to 2.5 h at the TCA of the options:
        // stale against Screen's 60 s, not against a day
        Assert.Equal(entry == "Screen" ? 6 : 0, stale);
    }

    private static bool IsStale(ParticipantCovariance covariance, TimeSpan threshold) =>
        covariance.Source == CovarianceSource.InitialStateHeldInRtn && covariance.Age > threshold;

    [Theory]
    [InlineData("TwoBody")]
    [InlineData("Rk78")]
    public void EvaluateAvoidance_NullMirrors_EqualTheEncounterValuesGivenExplicitly(string model)
    {
        // Arrange: the encounter analyzed with settings other than the defaults of the avoidance
        int id = model == "TwoBody" ? -6990 : -6995;
        var settings = InheritedSettings with
        {
            SampleStep = TimeSpan.FromMinutes(2.0),
            MaximumEventSearchStep = TimeSpan.FromSeconds(30.0),
            LowRelativeSpeedThresholdMetersPerSecond = 0.5
        };
        Spacecraft protectedCraft;
        Spacecraft secondary;
        if (model == "TwoBody")
        {
            protectedCraft = ReferenceCase.NewSpacecraft(id, _case.ProtectedStart);
            secondary = ReferenceCase.NewSpacecraft(id - 1,
                AvoidanceFallbackCase.SecondaryNear(_case.ProtectedStart.ToStateVector(AvoidanceFallbackCase.Tca)));
        }
        else
        {
            protectedCraft = AvoidanceFallbackCase.WithEngine(id, _case.ProtectedStart, out _);
            _case.Propagate(protectedCraft, true);
            secondary = Secondary(id - 1);
        }

        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondary,
            AvoidanceFallbackCase.ScreeningWindow, settings);

        // Act: the mirrors left null, and given the values of the encounter
        var inherited = ConjunctionAssessment.EvaluateAvoidance(encounter, Options);
        var explicitly = ConjunctionAssessment.EvaluateAvoidance(encounter, Options with
        {
            AnalysisSampleStep = settings.SampleStep,
            MaximumEventSearchStep = settings.MaximumEventSearchStep,
            SecondaryHardBodyRadiusMeters = 2.0,
            LowRelativeSpeedThresholdMetersPerSecond = settings.LowRelativeSpeedThresholdMetersPerSecond
        });

        // Assert: the same options, bit for bit (#683: the defaults of the avoidance replaced the settings of the encounter)
        Assert.Equal(6, inherited.Count);
        Assert.Equal(explicitly.Count, inherited.Count);
        for (int i = 0; i < inherited.Count; i++)
        {
            var a = inherited[i];
            var b = explicitly[i];
            Assert.Equal(b.BurnEpoch, a.BurnEpoch);
            Assert.Equal(b.DeltaVInertial, a.DeltaVInertial);
            Assert.Equal(b.PostManeuverEncounter.EncounterState.Epoch, a.PostManeuverEncounter.EncounterState.Epoch);
            Assert.Equal(b.PostManeuverEncounter.EncounterState.MissDistanceMeters, a.PostManeuverEncounter.EncounterState.MissDistanceMeters);
            Assert.Equal(b.PostManeuverEncounter.CollisionRisk.ProbabilityOfCollision, a.PostManeuverEncounter.CollisionRisk.ProbabilityOfCollision);
            Assert.Equal(b.PostManeuverEncounter.CollisionRisk.CombinedHardBodyRadiusMeters,
                a.PostManeuverEncounter.CollisionRisk.CombinedHardBodyRadiusMeters);
            Assert.Equal(b.PostManeuverEncounter.EncounterState.QualityFlags, a.PostManeuverEncounter.EncounterState.QualityFlags);
        }
    }

    [Theory]
    [InlineData(32.0, 600.0)]
    [InlineData(60.0, 100.0)]
    public void EvaluateAvoidance_TwoBody_SearchesWithTheEncounterStepsAndTolerance(double sampleStepMinutes,
        double maximumEventSearchStepSeconds)
    {
        // Arrange: the pair on its two-body orbits (the sampled search on a uniform grid from the window start), the
        // encounter analyzed with a bisection tolerance of 10 s and a sample step and a maximum search step that are not
        // the defaults (1 min, 60 s): the first row the sample step binding the search step (32 min / 16 = 120 s), the
        // second the maximum search step (100 s)
        int id = sampleStepMinutes == 32.0 ? -6980 : -6985;
        var settings = InheritedSettings with
        {
            SampleStep = TimeSpan.FromMinutes(sampleStepMinutes),
            MaximumEventSearchStep = TimeSpan.FromSeconds(maximumEventSearchStepSeconds),
            BisectionToleranceSeconds = 10.0
        };
        var protectedCraft = ReferenceCase.NewSpacecraft(id, _case.ProtectedStart);
        var secondary = ReferenceCase.NewSpacecraft(id - 1,
            AvoidanceFallbackCase.SecondaryNear(_case.ProtectedStart.ToStateVector(AvoidanceFallbackCase.Tca)));
        var window = AvoidanceFallbackCase.ScreeningWindow;

        // Every close approach of the window, the one of the crossing 2 h after the start the encounter (a TCA up to
        // 3.75 s off at 8.8 km/s leaves its miss above that of other minima of the window, which Analyze would return)
        var encounters = ConjunctionAssessment.AnalyzeAll(new ProtectedSpacecraftProfile(protectedCraft), secondary, window, settings);
        var encounter = encounters.OrderBy(e => System.Math.Abs((e.EncounterState.Epoch - AvoidanceFallbackCase.Tca).TotalSeconds)).First();
        Assert.True(System.Math.Abs((encounter.EncounterState.Epoch - AvoidanceFallbackCase.Tca).TotalSeconds) < 60.0);

        // The closed form of the search (docs/reference/conjunction-assessment.md, options): a step of
        // min(max(min(S/16, M), 4 tol), S) from the start of the window, each bracket halved in ticks until it is narrower
        // than the tolerance, its middle returned: a TCA (j + 1/2) w after the window start for an integer j, w the width
        // of the last bracket (7.5 s for the first row, 6.25 s for the second). The defaults of the avoidance before #683
        // give a step of 40 s and w = 5 s (a TCA never on either lattice), a tolerance of 1 ns the tick of the zero
        double step = System.Math.Min(System.Math.Max(System.Math.Min(sampleStepMinutes * 60.0 / 16.0,
            maximumEventSearchStepSeconds), 4.0 * settings.BisectionToleranceSeconds), sampleStepMinutes * 60.0);
        long width = TimeSpan.FromSeconds(step).Ticks;
        while (TimeSpan.FromTicks(width).TotalSeconds >= settings.BisectionToleranceSeconds)
        {
            Assert.Equal(0L, width % 2);
            width /= 2;
        }

        void AssertOnTheLattice(TimeSystem.Time tca, TimeSystem.Time start, string what)
        {
            long offset = (tca - start).Ticks;
            _output.WriteLine($"{what}: TCA {offset * 1e-7:F7} s after the start of its window, last bracket {width * 1e-7} s");
            Assert.Equal(width / 2, offset % width);
        }

        foreach (var approach in encounters)
        {
            AssertOnTheLattice(approach.EncounterState.Epoch, window.StartDate, "encounter");
        }

        // Act
        var avoidance = ConjunctionAssessment.EvaluateAvoidance(encounter, Options);

        // Assert: each option searched on the window from its burn with the steps and the tolerance of the encounter
        // (#683: the avoidance re-analyzed with a sample step of 1 min, a maximum search step of 60 s and a tolerance of
        // 1 ns)
        Assert.Equal(6, avoidance.Count);
        foreach (var option in avoidance)
        {
            Assert.Equal(AvoidanceTrajectory.TwoBody, option.Trajectory);
            AssertOnTheLattice(option.PostManeuverEncounter.EncounterState.Epoch, option.BurnEpoch, "option");
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EvaluateAvoidance_ConcurrentStudiesOfOneEncounter_GiveTheSequentialResults(bool rk78)
    {
        // Arrange
        int id = rk78 ? -6810 : -6820;
        var protectedCraft = AvoidanceFallbackCase.WithEngine(id, _case.ProtectedStart, out _);
        _case.Propagate(protectedCraft, rk78);
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), Secondary(id - 1),
            AvoidanceFallbackCase.ScreeningWindow, HeldInRtn);
        var sequential = ConjunctionAssessment.EvaluateAvoidance(encounter, Options);

        // Act: three studies at once, sharing the forces of the protected trajectory under its lock
        var concurrent = Task.WhenAll(Enumerable.Range(0, 3)
            .Select(_ => Task.Run(() => ConjunctionAssessment.EvaluateAvoidance(encounter, Options)))).GetAwaiter().GetResult();

        // Assert: bit for bit
        foreach (var study in concurrent)
        {
            Assert.Equal(sequential.Select(o => o.PostManeuverEncounter.ProtectedState.Position),
                study.Select(o => o.PostManeuverEncounter.ProtectedState.Position));
            Assert.Equal(sequential.Select(o => o.PostManeuverEncounter.CollisionRisk.ProbabilityOfCollision),
                study.Select(o => o.PostManeuverEncounter.CollisionRisk.ProbabilityOfCollision));
        }
    }

    [Fact]
    public void Propagate_VelocityVerlet_KeepsTheForceModelWithoutTheSteps()
    {
        // Arrange
        var spacecraft = AvoidanceFallbackCase.WithEngine(-6830, _case.ProtectedStart, out _);

        // Act
        var solution = _case.Propagate(spacecraft, false);

        // Assert: the force model is kept, the solution is not (#401, H3 B2); an RK7(8) propagation then keeps both, the
        // force model with the lock of its dynamics
        Assert.Null(spacecraft.LastPropagation);
        Assert.Same(solution.ForceModel, spacecraft.LastForceModel);
        Assert.Equal(AvoidanceFallbackCase.Start, spacecraft.LastForceModel.Start);
        Assert.Equal(AvoidanceFallbackCase.PropagationWindow.EndDate, spacecraft.LastForceModel.End);
        Assert.Equal(spacecraft.GetTotalMass(), spacecraft.LastForceModel.ContextAt(AvoidanceFallbackCase.Tca).TotalMass);
        _case.Propagate(spacecraft, true);
        Assert.Same(spacecraft.LastPropagation.ForceModel, spacecraft.LastForceModel);
        Assert.Same(spacecraft.LastPropagation.Dynamics.SyncRoot, spacecraft.LastForceModel.SyncRoot);
    }

    private void AssertOptionsAreTheirReferences(System.Collections.Generic.IReadOnlyList<AvoidanceOption> avoidance, bool rk78,
        Spacecraft secondary, int referenceId, StateVector start)
    {
        Assert.Equal(6, avoidance.Count);
        foreach (var option in avoidance.Take(2))
        {
            var (reference, referenceSolution) = rk78
                ? BurnPropagatedFromTheStart(option, start, referenceId--)
                : BurnPropagatedInTwoParts(option, start, referenceId--);
            AssertOptionIsItsReference(option, reference, referenceSolution, secondary, rk78);
        }
    }

    // In RK7(8), the spacecraft propagated from the start with the burn of the option, located by the event detection
    private (Spacecraft, PropagationSolution) BurnPropagatedFromTheStart(AvoidanceOption option, StateVector start, int naifId)
    {
        var reference = AvoidanceFallbackCase.WithEngine(naifId, start, out var engine);
        reference.SetStandbyManeuver(new FixedImpulse(_case.Earth, AvoidanceFallbackCase.Start, option.BurnEpoch,
            option.DeltaVInertial, engine));
        return (reference, _case.Propagate(reference, true, tolerance: AvoidanceFallbackCase.ReferenceTolerance));
    }

    // By Velocity-Verlet, which executes a burn only at the end of a step while the burn of an option is off the grid of the
    // steps: the spacecraft propagated from the start to the burn (the last step shortened to end there), then a new
    // spacecraft, with its mass less the fuel of the option, from the state after the burn, each with forces of its own
    private (Spacecraft, PropagationSolution) BurnPropagatedInTwoParts(AvoidanceOption option, StateVector start, int naifId)
    {
        var before = AvoidanceFallbackCase.WithEngine(naifId, start, out _);
        before.Propagate(new Window(AvoidanceFallbackCase.Start, option.BurnEpoch), _case.Bodies, true, true,
            AvoidanceFallbackCase.VelocityVerletStep);
        var atBurn = before.GetEphemeris(option.BurnEpoch, _case.Earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        var afterBurn = new StateVector(atBurn.Position, atBurn.Velocity + option.DeltaVInertial, _case.Earth, option.BurnEpoch,
            Frames.Frame.ICRF);

        // With the initial covariance held in RTN, as the option carries it
        if (start.Covariance.HasValue)
        {
            afterBurn = new StateVector(afterBurn.Position, afterBurn.Velocity, afterBurn.Observer, afterBurn.Epoch, afterBurn.Frame,
                afterBurn.RotateCovarianceFromRtn(start.RotateCovarianceToRtn(start.Covariance.Value)));
        }
        var after = ReferenceCase.NewSpacecraft(naifId - 50, afterBurn);
        var tank = new FuelTank("tank" + -naifId, "model", "sn1", 100.0, 100.0 - option.FuelCostEstimateKilograms);
        after.AddFuelTank(tank);
        after.AddEngine(new Engine("engine" + -naifId, "model", "sn1", 300.0, 10.0, tank));
        var solution = after.Propagate(new Window(option.BurnEpoch, AvoidanceFallbackCase.PropagationWindow.EndDate),
            _case.Bodies, true, true, AvoidanceFallbackCase.VelocityVerletStep);
        return (after, solution);
    }

    // The option against the reference spacecraft propagated from the start with its burn, analyzed with the covariance
    // held in RTN: the state on the steps of the reference, the TCA, the miss distance and the probability of its analysis
    private void AssertOptionIsItsReference(AvoidanceOption option, Spacecraft reference, PropagationSolution referenceSolution,
        Spacecraft secondary, bool rk78)
    {
        var post = option.PostManeuverEncounter;
        var expected = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(reference), secondary,
            new Window(option.BurnEpoch, AvoidanceFallbackCase.ScreeningWindow.EndDate),
            new ConjunctionAnalysisOptions { CovariancePropagation = null });
        var tca = post.EncounterState.Epoch;
        double tcaError = System.Math.Abs((tca - expected.EncounterState.Epoch).TotalSeconds);
        // The state on the steps of the reference; its analysis samples its ephemeris, interpolated between its outputs
        double stateError = (post.ProtectedState.Position - referenceSolution.InterpolateAt(tca).position).Magnitude();
        double missError = System.Math.Abs(post.EncounterState.MissDistanceMeters - expected.EncounterState.MissDistanceMeters);
        double pcError = System.Math.Abs(post.CollisionRisk.ProbabilityOfCollision.Value - expected.CollisionRisk.ProbabilityOfCollision.Value) /
                         expected.CollisionRisk.ProbabilityOfCollision.Value;
        _output.WriteLine($"{(rk78 ? "RK7(8)" : "VV")} option {option.DeltaVInertial}: TCA {tcaError:E2} s, state {stateError:E2} m, " +
                          $"miss {post.EncounterState.MissDistanceMeters:F3} m ({missError:E2} m), Pc {post.CollisionRisk.ProbabilityOfCollision:E4} " +
                          $"({pcError:E2}), fuel {option.FuelCostEstimateKilograms:E4} kg");
        Assert.Equal(AvoidanceTrajectory.Propagated, option.Trajectory);
        Assert.Equal(expected.EncounterState.ProtectedCovariance.Source, post.EncounterState.ProtectedCovariance.Source);

        if (post.EncounterState.ProtectedCovarianceAge.HasValue)
        {
            Assert.Equal((tca - AvoidanceFallbackCase.Start).Duration(), post.EncounterState.ProtectedCovarianceAge);
        }

        // Twice the worst departure over Linux, macOS arm64 and Windows, rounded up at the second significant digit
        // (decision of S. Guillet, 2026-10-21; the integration error of the option at 1e-13 depends on the platform, its
        // steps parting differently with the floating point of arm64). Measured by the CI of every platform:
        // - RK7(8), the option at 1e-13 against its reference at 1e-14 (a reference at 1e-13 is off by up to 1.8e-5 m, its
        //   own integration error): state 2.24e-5 / 4.14e-5 / 2.42e-5 m, miss distance 1.90e-5 / 3.37e-5 / 1.99e-5 m, Pc
        //   1.24e-7 / 3.18e-7 / 1.87e-7 relative (Linux / macOS arm64 / Windows);
        // - Velocity-Verlet at 1 s, against the reference in two parts: state 1.86e-4 m, miss distance 1.52e-4 m, Pc
        //   1.53e-6 on the three platforms, the burn state of a trajectory passed to the analysis being its cubic Hermite
        //   interpolation (9e-8 m at most from the ephemeris of the spacecraft);
        // - the TCA 0 on the three platforms.
        // Before: RK7(8) 5e-5 m, 4e-5 m and 3e-7, set on Linux alone (and first 1e-5 m, 1e-5 m and 1e-7 against references at
        // 1e-13, which measured how close the steps of two propagations at one tolerance fall); Velocity-Verlet 4e-4 m,
        // 3e-4 m and 3e-6
        var (stateThreshold, missThreshold, pcThreshold) = rk78 ? (8.3e-5, 6.8e-5, 6.4e-7) : (3.8e-4, 3.1e-4, 3.1e-6);
        Assert.True(tcaError < 1e-5, $"{tcaError:E2} s");
        Assert.True(stateError < stateThreshold, $"{stateError:E2} m");
        Assert.True(missError < missThreshold, $"{missError:E2} m");
        Assert.True(pcError < pcThreshold, $"{pcError:E2}");
    }

    // The covariance at the TCA of the option is the initial covariance held in RTN: its RTN components with the state
    // there are those of the initial covariance with the initial state
    private static void AssertHeldInRtn(StateVector initial, IO.Astrodynamics.Math.Matrix actual, StateVector atTca)
    {
        var expected = initial.RotateCovarianceToRtn(initial.Covariance.Value);
        var rtn = atTca.RotateCovarianceToRtn(actual);
        double largest = 0.0;
        double difference = 0.0;
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                largest = System.Math.Max(largest, System.Math.Abs(expected.Get(i, j)));
                difference = System.Math.Max(difference, System.Math.Abs(rtn.Get(i, j) - expected.Get(i, j)));
            }
        }

        // Two rotations of a 6×6 matrix whose entries span 1e-3 to 9e4: rounding, relative to the largest entry
        Assert.True(difference < 1e-12 * largest, $"{difference / largest:E2}");
    }
}
