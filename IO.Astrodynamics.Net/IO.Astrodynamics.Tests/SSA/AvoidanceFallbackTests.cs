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

    // The options and their references at 1e-13 in RK7(8), at 1 s by Velocity-Verlet
    internal const double Tolerance = 1e-13;
    internal static readonly TimeSpan VelocityVerletStep = TimeSpan.FromSeconds(1.0);

    public AvoidanceFallbackCase()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        Earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new Nrlmsise00Model(SpaceWeather.Nominal));
        Bodies = new CelestialItem[] { Earth, new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start), new CelestialBody(Stars.Sun) };
        var orbit = new KeplerianElements(6778137.0, 0.001, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.4, 1.1, 0.3, Earth,
            Start, Frames.Frame.ICRF).ToStateVector();
        ProtectedStart = ConjunctionCovarianceCase.WithRtnCovariance(orbit, new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 });
        ProtectedStartWithoutCovariance = orbit;

        var reference = ReferenceCase.NewSpacecraft(-6700, ProtectedStart);
        Propagate(reference, true);
        SecondaryAtTca = SecondaryNear(reference.GetEphemeris(Tca, Earth, Frames.Frame.ICRF, Aberration.None).ToStateVector());
    }

    internal CelestialBody Earth { get; }

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
    internal PropagationSolution Propagate(Spacecraft spacecraft, bool rk78, CovariancePropagationOptions options = null)
    {
        return rk78
            ? spacecraft.Propagate(PropagationWindow, Bodies, new RK78Integrator(Tolerance, Tolerance), true, true,
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
        var secondaryTrajectory = secondary.Propagate(AvoidanceFallbackCase.PropagationWindow, new CelestialItem[] { _case.Earth },
            new RK78Integrator(1e-12, 1e-12), false, false, TimeSpan.FromSeconds(60.0));
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
            _case.Propagate(reference, true);
            AssertOptionIsItsReference(option, reference, secondary, true);
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
        secondary.Propagate(AvoidanceFallbackCase.PropagationWindow, new CelestialItem[] { _case.Earth },
            new RK78Integrator(1e-12, 1e-12), false, false, TimeSpan.FromSeconds(60.0));
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
            var reference = AvoidanceFallbackCase.WithEngine(referenceId--, start, out var engine);
            reference.SetStandbyManeuver(new FixedImpulse(_case.Earth, AvoidanceFallbackCase.Start,
                option.BurnEpoch, option.DeltaVInertial, engine));
            _case.Propagate(reference, rk78);
            AssertOptionIsItsReference(option, reference, secondary, rk78);
        }
    }

    // The option against the reference spacecraft propagated from the start with its burn, analyzed with the covariance
    // held in RTN
    private void AssertOptionIsItsReference(AvoidanceOption option, Spacecraft reference, Spacecraft secondary, bool rk78)
    {
        var post = option.PostManeuverEncounter;
        var expected = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(reference), secondary,
            new Window(option.BurnEpoch, AvoidanceFallbackCase.ScreeningWindow.EndDate),
            new ConjunctionAnalysisOptions { CovariancePropagation = null });
        var tca = post.EncounterState.Epoch;
        double tcaError = System.Math.Abs((tca - expected.EncounterState.Epoch).TotalSeconds);
        double stateError = (post.ProtectedState.Position -
                             reference.GetEphemeris(tca, _case.Earth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position)
            .Magnitude();
        double missError = System.Math.Abs(post.EncounterState.MissDistanceMeters - expected.EncounterState.MissDistanceMeters);
        double pcError = System.Math.Abs(post.CollisionRisk.ProbabilityOfCollision.Value - expected.CollisionRisk.ProbabilityOfCollision.Value) /
                         expected.CollisionRisk.ProbabilityOfCollision.Value;
        _output.WriteLine($"{(rk78 ? "RK7(8)" : "VV")} option {option.DeltaVInertial}: TCA {tcaError:E2} s, state {stateError:E2} m, " +
                          $"miss {post.EncounterState.MissDistanceMeters:F3} m ({missError:E2} m), Pc {post.CollisionRisk.ProbabilityOfCollision:E4} " +
                          $"({pcError:E2}), fuel {option.FuelCostEstimateKilograms:E4} kg");
        Assert.Equal(AvoidanceTrajectory.Propagated, option.Trajectory);
        Assert.Equal(expected.EncounterState.ProtectedCovariance.Source, post.EncounterState.ProtectedCovariance.Source);
        Assert.Equal(expected.EncounterState.ProtectedCovarianceAge, post.EncounterState.ProtectedCovarianceAge);
        Assert.True(tcaError < 1e-2, $"{tcaError:E2} s");
        Assert.True(stateError < 1.0, $"{stateError:E2} m");
        Assert.True(pcError < 1e-2, $"{pcError:E2}");
    }

    // The covariance at the TCA of the option is the initial covariance held in RTN: its RTN components with the state
    // there are those of the initial covariance with the initial state
    private static void AssertHeldInRtn(StateVector initial, IO.Astrodynamics.Math.Matrix actual, StateVector atTca)
    {
        var expected = initial.RotateCovarianceToRtn(initial.Covariance.Value);
        var rtn = atTca.RotateCovarianceToRtn(actual);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                Assert.Equal(expected.Get(i, j), rtn.Get(i, j), System.Math.Abs(expected.Get(i, j)) * 1e-12 + 1e-15);
            }
        }
    }
}
