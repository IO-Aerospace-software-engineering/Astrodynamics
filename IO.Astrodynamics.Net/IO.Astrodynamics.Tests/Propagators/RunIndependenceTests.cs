// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Matrix = IO.Astrodynamics.Math.Matrix;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// Independent runs, lot L5a of #668 (R08, decisions 37 to 41, decided by S. Guillet, 2026-11-23): a propagator keeps
/// the initial orbit it was built from and refuses to run once the spacecraft holds another (#783); the re-arm drops the
/// stored states and the attitude record whatever <see cref="Spacecraft.IsPropagated"/> says, so a first run after states
/// and attitudes added by hand is a fresh run; <see cref="TLEPropagator"/> computes every sample first, then re-arms the
/// spacecraft and stores its samples in one block, replacing the stored states, a stop or an SGP4 failure changing
/// nothing; its runs share no array (#785); it refuses a spacecraft with a maneuver plan, and so does
/// <see cref="MonteCarloPropagator"/> for its template.
/// </summary>
/// <remarks>
/// The references are the decided rules: a fresh spacecraft run once by a fresh propagator (decision 22 of lot L4, every
/// run a fresh run), compared bit for bit; SGP4 itself for the samples of <see cref="TLEPropagator"/>.
/// </remarks>
public class RunIndependenceTests
{
    public RunIndependenceTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly TimeSystem.Time Epoch = new(2021, 1, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);
    private static Window OneHour => new(Epoch, Epoch.AddHours(1.0));

    private static int _nextId = -783000;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private static CelestialBody Earth => PlanetsAndMoons.EARTH_BODY;

    private static TLE Iss() => new("ISS",
        "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
        "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

    private static StateVector LeoState(Frames.Frame frame = null) =>
        new(new Vector3(6700000.0, 0.0, 0.0), new Vector3(0.0, 7713.0, 1000.0), Earth, Epoch, frame ?? Frames.Frame.ICRF);

    private static Spacecraft NewSpacecraft(IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit)
    {
        int id = NextId();
        return new Spacecraft(id, $"RI{-id}", 100.0, 10000.0, new Clock($"riclk{-id}", 65536), orbit, 10.0, 2.2);
    }

    private static Spacecraft Leo(Frames.Frame frame = null) => NewSpacecraft(LeoState(frame));

    // A tank, an engine and an apogee raise at the first apoapsis after the start: a plan
    private static void AddPlan(Spacecraft spacecraft, in TimeSystem.Time minimumEpoch)
    {
        int id = spacecraft.NaifId;
        var tank = new FuelTank($"ft{-id}", "model", "sn", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine($"eng{-id}", "model", "sn", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        spacecraft.SetStandbyManeuver(new ApogeeHeightManeuver(Earth, minimumEpoch, TimeSpan.Zero, 9000000.0, engine));
    }

    private static PropagationOptions Options(string kind) => new()
    {
        Integrator = kind == "VV"
            ? new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) }
            : new RK78IntegratorOptions { AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10 },
        OutputStep = Step,
        ThirdBodies = [Earth],
        CovariancePropagation = CovariancePropagationOptions.None
    };

    // A propagator built from options, or from an integrator holding the forces of the caller
    private static CentralBodyPropagator Build(Spacecraft spacecraft, string kind, string constructor, in Window window)
    {
        if (constructor == "Options")
        {
            return new CentralBodyPropagator(window, spacecraft, Options(kind));
        }

        IIntegrator integrator;
        if (kind == "VV")
        {
            var vv = new VVIntegrator(TimeSpan.FromSeconds(10.0));
            vv.AddForce(new GravitationalAcceleration(Earth));
            integrator = vv;
        }
        else
        {
            var rk = new RK78Integrator(1e-10, 1e-10);
            rk.AddForce(new GravitationalAcceleration(Earth));
            integrator = rk;
        }

        return new CentralBodyPropagator(window, spacecraft, integrator, Step);
    }

    // What a run leaves on a spacecraft, compared bit for bit
    private sealed record Left(StateVector[] States, int Arcs, StateOrientation[] Attitudes, bool Propagated,
        PropagationSolution LastPropagation, Maneuver.Maneuver[] Executed, double Fuel);

    private static Left Take(Spacecraft spacecraft) => new(spacecraft.PropagatedStates.ToArray(),
        spacecraft.PropagatedArcs.Count, spacecraft.Frame.GetStateOrientationsToICRF().ToArray(), spacecraft.IsPropagated,
        spacecraft.LastPropagation, spacecraft.ExecutedManeuvers.ToArray(), spacecraft.FuelTanks.Sum(t => t.Quantity));

    private static void AssertSameStates(IReadOnlyList<StateVector> expected, IReadOnlyList<StateVector> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].Epoch.EqualsExact(actual[i].Epoch), $"epoch {i}");
            Assert.Equal(expected[i].Position, actual[i].Position);
            Assert.Equal(expected[i].Velocity, actual[i].Velocity);
        }
    }

    // The rotation angle between two attitudes, exactly 0 for the same quaternion
    private static double Angle(Quaternion a, Quaternion b)
    {
        var q = a * b.Conjugate();
        return 2.0 * System.Math.Atan2(q.VectorPart.Magnitude(), System.Math.Abs(q.W));
    }

    private static void AssertSameAttitudes(IReadOnlyList<StateOrientation> expected, IReadOnlyList<StateOrientation> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].Epoch.EqualsExact(actual[i].Epoch), $"attitude epoch {i}");
            double angle = Angle(expected[i].Rotation, actual[i].Rotation);
            Assert.True(angle == 0.0, $"attitude {i} off by {angle:E3} rad");
        }
    }

    private static void AssertUnchanged(Left before, Spacecraft spacecraft)
    {
        var after = Take(spacecraft);
        AssertSameStates(before.States, after.States);
        Assert.Equal(before.Arcs, after.Arcs);
        AssertSameAttitudes(before.Attitudes, after.Attitudes);
        Assert.Equal(before.Propagated, after.Propagated);
        Assert.Same(before.LastPropagation, after.LastPropagation);
        Assert.Equal(before.Executed, after.Executed);
        Assert.Equal(before.Fuel, after.Fuel);
    }

    #region #783: a propagator keeps the orbit it was built from (decision 37)

    [Theory]
    [InlineData("RK", "Options")]
    [InlineData("VV", "Options")]
    [InlineData("RK", "Integrator")]
    [InlineData("VV", "Integrator")]
    public void APropagatorReusedAfterSetInitialOrbitalParameters_Throws_AndChangesNothing(string kind, string constructor)
    {
        // Arrange: a run, then another initial orbit on the spacecraft
        var spacecraft = Leo();
        var propagator = Build(spacecraft, kind, constructor, OneHour);
        propagator.Propagate();
        var before = Take(spacecraft);
        Assert.True(before.Propagated);
        spacecraft.SetInitialOrbitalParameters(new StateVector(new Vector3(7500000.0, 0.0, 0.0),
            new Vector3(0.0, 7290.0, 0.0), Earth, Epoch, Frames.Frame.ICRF));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => propagator.Propagate());

        // Assert: refused before anything changes, the message naming the remedy
        Assert.Contains("initial orbit", exception.Message);
        Assert.Contains("new propagator", exception.Message);
        AssertUnchanged(before, spacecraft);
    }

    [Theory]
    [InlineData("ICRF")]
    [InlineData("ECLIPJ2000")]
    [InlineData("SSB")]
    [InlineData("TLE")]
    public void AnEqualButDistinctInitialOrbit_Throws_OnEveryKindOfOrbit(string orbit)
    {
        // Arrange: the orbit is compared by reference, so an equal orbit given anew is another orbit (decision 37)
        Spacecraft spacecraft;
        Func<IO.Astrodynamics.OrbitalParameters.OrbitalParameters> again;
        Window window = OneHour;
        switch (orbit)
        {
            case "ECLIPJ2000":
                spacecraft = Leo(Frames.Frame.ECLIPTIC_J2000);
                again = () => LeoState(Frames.Frame.ECLIPTIC_J2000);
                break;
            case "SSB":
                var heliocentric = new StateVector(new Vector3(1.5e11, 0.0, 0.0), new Vector3(0.0, 29780.0, 1000.0),
                    Stars.SUN_BODY, Epoch, Frames.Frame.ICRF);
                again = () => heliocentric.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None).ToStateVector();
                spacecraft = NewSpacecraft(again());
                break;
            case "TLE":
                spacecraft = NewSpacecraft(Iss());
                again = Iss;
                window = new Window(Iss().Epoch, Iss().Epoch.AddHours(1.0));
                break;
            default:
                spacecraft = Leo();
                again = () => LeoState();
                break;
        }

        var propagator = new CentralBodyPropagator(window, spacecraft, new PropagationOptions
        {
            OutputStep = Step, ThirdBodies = orbit == "SSB" ? [Stars.SUN_BODY, Earth] : [Earth],
            CovariancePropagation = CovariancePropagationOptions.None
        });
        spacecraft.SetInitialOrbitalParameters(again());

        // Act and assert
        Assert.Throws<InvalidOperationException>(() => propagator.Propagate());
        Assert.False(spacecraft.IsPropagated);
        Assert.Empty(spacecraft.PropagatedStates);
    }

    [Fact]
    public void TheSameInitialOrbitSetAgain_Runs_BitForBitAFreshRun()
    {
        // Arrange: the reference held by the spacecraft is unchanged, so the propagator runs
        var spacecraft = Leo();
        var propagator = Build(spacecraft, "RK", "Options", OneHour);
        spacecraft.SetInitialOrbitalParameters(spacecraft.InitialOrbitalParameters);
        var fresh = Leo();

        // Act
        var run = propagator.Propagate();
        var reference = Build(fresh, "RK", "Options", OneHour).Propagate();

        // Assert
        AssertSameStates(reference.StateVectors, run.StateVectors);
    }

    [Fact]
    public void APropagatorBuiltAfterSetInitialOrbitalParameters_RunsFromTheNewOrbit()
    {
        // Arrange: the remedy the message names
        var spacecraft = Leo();
        Build(spacecraft, "RK", "Options", OneHour).Propagate();
        var orbit = new StateVector(new Vector3(7500000.0, 0.0, 0.0), new Vector3(0.0, 7290.0, 0.0), Earth, Epoch,
            Frames.Frame.ICRF);
        spacecraft.SetInitialOrbitalParameters(orbit);

        // Act
        var solution = Build(spacecraft, "RK", "Options", OneHour).Propagate();

        // Assert
        Assert.Equal(orbit.Position, solution.StateVectors[0].Position);
        Assert.Equal(orbit.Velocity, solution.StateVectors[0].Velocity);
    }

    [Fact]
    public void ATLEPropagatorReusedAfterSetInitialOrbitalParameters_Throws_AndChangesNothing()
    {
        // Arrange
        var tle = Iss();
        var spacecraft = NewSpacecraft(tle);
        var propagator = new TLEPropagator(new Window(tle.Epoch, tle.Epoch.AddHours(1.0)), spacecraft, Step);
        propagator.Propagate();
        var before = Take(spacecraft);
        spacecraft.SetInitialOrbitalParameters(Iss());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => propagator.Propagate());

        // Assert
        Assert.Contains("initial orbit", exception.Message);
        Assert.Contains("new propagator", exception.Message);
        AssertUnchanged(before, spacecraft);
    }

    #endregion

    #region The re-arm on every path, whatever IsPropagated says (decision 38)

    private static readonly Quaternion QuarterTurn = new(Vector3.VectorZ, System.Math.PI / 2.0);

    // Three attitudes added by hand, a quarter turn about Z, before, inside and after the window, and one state an hour
    // before it
    private static void AddByHand(Spacecraft spacecraft, in Window window)
    {
        var before = window.StartDate.AddHours(-1.0);
        spacecraft.AddPropagatedStates(spacecraft.InitialOrbitalParameters.ToStateVector(before));
        foreach (var epoch in new[] { before, window.StartDate.AddSeconds(1830.5), window.EndDate.AddHours(1.0) })
        {
            spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(QuarterTurn, Vector3.Zero, epoch,
                spacecraft.Frame));
        }
    }

    private static PropagationSolution Run(Spacecraft spacecraft, string kind, string path, in Window window)
    {
        switch (path)
        {
            case "Propagate":
                return spacecraft.Propagate(window, Options(kind));
            case "Propagator":
                return new CentralBodyPropagator(window, spacecraft, Options(kind)).Propagate();
            case "Batch":
                var result = BatchPropagator.Propagate([new PropagationTask(spacecraft, window, Options(kind))]).Results[0];
                Assert.True(result.Succeeded, result.Error?.ToString());
                return result.Solution;
            case "Scenario":
                int id = NextId();
                var scenario = new Scenario($"RIS{-id}", new IO.Astrodynamics.Mission.Mission($"RIM{-id}"), window);
                scenario.AddSpacecraft(spacecraft);
                scenario.Simulate(Options(kind));
                return spacecraft.LastPropagation;
            default:
                throw new ArgumentOutOfRangeException(nameof(path));
        }
    }

    [Theory]
    [InlineData("RK", "Propagate")]
    [InlineData("VV", "Propagate")]
    [InlineData("RK", "Propagator")]
    [InlineData("VV", "Propagator")]
    [InlineData("RK", "Batch")]
    [InlineData("VV", "Batch")]
    [InlineData("RK", "Scenario")]
    [InlineData("VV", "Scenario")]
    public void AFirstRunAfterStatesAndAttitudesAddedByHand_IsAFreshRun(string kind, string path)
    {
        // Arrange: the reference, a fresh spacecraft run once; the other, never propagated, holds a state and three
        // attitudes added by hand, which the re-arm kept before 11.0.0 (IsPropagated false): its record held 4 entries
        // against 2, a quarter turn off from 12:30:30.5 to the end of the window (gap analysis of L5, #668)
        var reference = Leo();
        Run(reference, kind, path, OneHour);
        var spacecraft = Leo();
        AddByHand(spacecraft, OneHour);
        Assert.False(spacecraft.IsPropagated);

        // Act
        Run(spacecraft, kind, path, OneHour);

        // Assert
        AssertSameStates(reference.PropagatedStates, spacecraft.PropagatedStates);
        AssertSameAttitudes(reference.Frame.GetStateOrientationsToICRF().ToArray(),
            spacecraft.Frame.GetStateOrientationsToICRF().ToArray());
    }

    [Fact]
    public void ANumericalRunOfATLESpacecraftSampledByTLEPropagator_IsAFreshRun()
    {
        // Arrange: a TLE spacecraft sampled by TLEPropagator, then given an attitude by hand 600 s before the window
        var tle = Iss();
        var window = new Window(tle.Epoch, tle.Epoch.AddHours(1.0));
        var reference = NewSpacecraft(Iss());
        reference.Propagate(window, Options("RK"));
        var spacecraft = NewSpacecraft(tle);
        new TLEPropagator(window, spacecraft, Step).Propagate();
        spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(QuarterTurn, Vector3.Zero,
            window.StartDate.AddSeconds(-600.0), spacecraft.Frame));

        // Act
        spacecraft.Propagate(window, Options("RK"));

        // Assert
        AssertSameStates(reference.PropagatedStates, spacecraft.PropagatedStates);
        AssertSameAttitudes(reference.Frame.GetStateOrientationsToICRF().ToArray(),
            spacecraft.Frame.GetStateOrientationsToICRF().ToArray());
    }

    #endregion

    #region TLEPropagator: compute, then re-arm and store in one block (decisions 38 and 39)

    // The samples of a TLEPropagator run as the spacecraft stores them: SGP4 at each sample, rotated to ICRF
    private static StateVector[] StoredSamples(PropagationSolution solution) => solution.StateVectors
        .Select(sv => sv.ToFrame(Frames.Frame.ICRF).ToStateVector()).ToArray();

    [Fact]
    public void ATLEPropagatorRunAfterANumericalRunWithABurn_ReArmsTheSpacecraft()
    {
        // Arrange: a numerical run with a burn, then the plan cleared (a TLEPropagator refuses a plan, decision 40)
        var tle = Iss();
        var spacecraft = NewSpacecraft(tle);
        AddPlan(spacecraft, tle.Epoch.ToTDB());
        spacecraft.Propagate(new Window(tle.Epoch, tle.Epoch.AddHours(2.0)), Options("RK"));
        Assert.Single(spacecraft.ExecutedManeuvers);
        Assert.True(spacecraft.FuelTanks.Single().Quantity < 1000.0);
        Assert.NotEmpty(spacecraft.Frame.GetStateOrientationsToICRF());
        Assert.True(spacecraft.IsPropagated);
        spacecraft.SetStandbyManeuver(null);

        // Act
        var solution = new TLEPropagator(new Window(tle.Epoch.AddHours(3.0), tle.Epoch.AddHours(4.0)), spacecraft, Step)
            .Propagate();

        // Assert: the full re-arm, then the samples alone, in one arc
        Assert.False(spacecraft.IsPropagated);
        Assert.Null(spacecraft.LastPropagation);
        Assert.Null(spacecraft.LastForceModel);
        Assert.Empty(spacecraft.ExecutedManeuvers);
        Assert.Equal(1000.0, spacecraft.FuelTanks.Single().Quantity);
        Assert.Empty(spacecraft.Frame.GetStateOrientationsToICRF());
        Assert.Equal(1, spacecraft.PropagatedArcs.Count);
        AssertSameStates(StoredSamples(solution), spacecraft.PropagatedStates);
        Assert.Null(spacecraft.InterpolatedStatesWindow);
    }

    [Fact]
    public void ASecondTLEPropagatorRun_ReplacesTheStatesOfTheFirst()
    {
        // Arrange: before 11.0.0 the second run added its 61 samples to the 61 of the first, in one arc of 122 states
        var tle = Iss();
        var spacecraft = NewSpacecraft(tle);
        new TLEPropagator(new Window(tle.Epoch, tle.Epoch.AddHours(1.0)), spacecraft, Step).Propagate();

        // Act
        var second = new TLEPropagator(new Window(tle.Epoch.AddHours(3.0), tle.Epoch.AddHours(4.0)), spacecraft, Step)
            .Propagate();

        // Assert
        Assert.Equal(1, spacecraft.PropagatedArcs.Count);
        AssertSameStates(StoredSamples(second), spacecraft.PropagatedStates);
    }

    [Fact]
    public void AStoppedTLEPropagatorRun_LeavesANumericallyPropagatedSpacecraftAsItWas()
    {
        // Arrange: a numerical run, then a TLEPropagator run stopped from inside its tenth SGP4 sample
        var tle = new CancellingTle("ISS",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var spacecraft = NewSpacecraft(tle);
        spacecraft.Propagate(new Window(tle.Epoch, tle.Epoch.AddHours(1.0)), Options("RK"));
        var before = Take(spacecraft);
        Assert.True(before.Propagated);
        var propagator = new TLEPropagator(new Window(tle.Epoch.AddHours(1.0), tle.Epoch.AddHours(2.0)), spacecraft, Step);
        using var cts = new CancellationTokenSource();
        tle.CancelAt(cts, 10);

        // Act
        var exception = Assert.Throws<OperationCanceledException>(() => propagator.Propagate(cts.Token));

        // Assert: nothing changed, the numerical run's states, attitudes and solution kept
        Assert.Equal(cts.Token, exception.CancellationToken);
        AssertUnchanged(before, spacecraft);
        Assert.NotNull(spacecraft.LastForceModel);
    }

    [Fact]
    public void TheSolutionsOfTwoRunsOfOneTLEPropagator_ShareNoState()
    {
        // Arrange: before 11.0.0 both solutions held the same array of the propagator, its first state built once (#785)
        var tle = Iss();
        var spacecraft = NewSpacecraft(tle);
        var propagator = new TLEPropagator(new Window(tle.Epoch, tle.Epoch.AddHours(1.0)), spacecraft, Step);

        // Act
        var first = propagator.Propagate();
        var second = propagator.Propagate();

        // Assert: the same values, in states of their own
        Assert.NotSame(first.StateVectors, second.StateVectors);
        Assert.Equal(first.StateVectors.Count, second.StateVectors.Count);
        for (int i = 0; i < first.StateVectors.Count; i++)
        {
            Assert.NotSame(first.StateVectors[i], second.StateVectors[i]);
        }

        AssertSameStates(first.StateVectors, second.StateVectors);
        for (int i = 0; i < first.StateVectors.Count; i++)
        {
            var sgp4 = tle.ToStateVector(first.StateVectors[i].Epoch);
            Assert.Equal(sgp4.Position, first.StateVectors[i].Position);
            Assert.Equal(sgp4.Velocity, first.StateVectors[i].Velocity);
        }
    }

    // A TLE that cancels a token from inside its SGP4 call of a given rank, once armed
    private sealed class CancellingTle(string name, string line1, string line2) : TLE(name, line1, line2)
    {
        private CancellationTokenSource _source;
        private int _rank;
        private int _calls;

        internal void CancelAt(CancellationTokenSource source, int rank)
        {
            _source = source;
            _rank = rank;
            _calls = 0;
        }

        public override StateVector ToStateVector(TimeSystem.Time date)
        {
            var state = base.ToStateVector(date);
            if (_source != null && ++_calls == _rank)
            {
                _source.Cancel();
            }

            return state;
        }
    }

    #endregion

    #region A maneuver plan where it cannot apply (decisions 40 and 41)

    [Fact]
    public void TLEPropagator_ASpacecraftWithAManeuverPlan_ThrowsArgumentException()
    {
        // Arrange
        var tle = Iss();
        var spacecraft = NewSpacecraft(tle);
        AddPlan(spacecraft, tle.Epoch.ToTDB());

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new TLEPropagator(new Window(tle.Epoch, tle.Epoch.AddHours(1.0)), spacecraft, Step));

        // Assert
        Assert.Equal("spacecraft", exception.ParamName);
        Assert.Contains("maneuver", exception.Message);
        Assert.Empty(spacecraft.PropagatedStates);
    }

    [Fact]
    public void TLEPropagator_APlanSetAfterConstruction_ThrowsInvalidOperationException_AndChangesNothing()
    {
        // Arrange
        var tle = Iss();
        var spacecraft = NewSpacecraft(tle);
        var propagator = new TLEPropagator(new Window(tle.Epoch, tle.Epoch.AddHours(1.0)), spacecraft, Step);
        propagator.Propagate();
        AddPlan(spacecraft, tle.Epoch.ToTDB());
        var before = Take(spacecraft);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => propagator.Propagate());

        // Assert
        Assert.Contains("maneuver", exception.Message);
        AssertUnchanged(before, spacecraft);
    }

    [Fact]
    public void MonteCarlo_ATemplateWithAManeuverPlan_ThrowsArgumentException_NamingConfig()
    {
        // Arrange: a template the campaign could sample, with a plan, which its runs would drop
        var template = NewSpacecraft(new StateVector(new Vector3(6700000.0, 0.0, 0.0), new Vector3(0.0, 7713.0, 1000.0),
            Earth, Epoch, Frames.Frame.ICRF, Matrix.Diagonal(100.0, 100.0, 100.0, 1e-2, 1e-2, 1e-2)));
        AddPlan(template, Epoch);
        var config = new MonteCarloConfiguration
        {
            RunCount = 2, Seed = 42, TemplateSpacecraft = template, Window = OneHour,
            Propagation = Options("VV"), BaseNaifId = NextId() - 100000
        };

        // Act and assert: both entries
        var exception = Assert.Throws<ArgumentException>(() => MonteCarloPropagator.Propagate(config));
        Assert.Equal("config", exception.ParamName);
        Assert.Contains("maneuver", exception.Message);
        exception = Assert.Throws<ArgumentException>(() => MonteCarloPropagator.Propagate(config, progress: null));
        Assert.Equal("config", exception.ParamName);
    }

    #endregion
}
