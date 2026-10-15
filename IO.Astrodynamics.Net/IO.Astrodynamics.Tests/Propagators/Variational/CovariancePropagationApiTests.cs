// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;
using Xunit;
using Xunit.Abstractions;
using Matrix = IO.Astrodynamics.Math.Matrix;

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// The public API of the state transition matrix and of the covariance (D1 to D6): activation, results, exceptions, and
/// a propagation without activation.
/// </summary>
public class CovariancePropagationApiTests : IClassFixture<PartialsTestCases>
{
    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public CovariancePropagationApiTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    private static TimeSystem.Time Start => PartialsTestCases.Epoch;

    private static TimeSystem.Window Window => new(Start, Start.AddHours(1.0));

    private static readonly TimeSpan Step = TimeSpan.FromSeconds(300.0);

    private StateVector Leo => PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);

    private CelestialItem[] Bodies => new CelestialItem[] { _cases.Earth, _cases.Moon, _cases.Sun };

    private static CovariancePropagationOptions Full => new()
    {
        Mode = CovariancePropagationMode.Covariance,
        ProcessNoise = new RtnProcessNoise(1e-12, 1e-11, 1e-12),
        ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.1, reflectivityCoefficientSigma: 0.05)
    };

    [Fact]
    public void Options_Defaults()
    {
        // Act
        var options = new CovariancePropagationOptions();

        // Assert
        Assert.Equal(CovariancePropagationMode.StateTransitionOnly, options.Mode);
        Assert.Null(options.ProcessNoise);
        Assert.Null(options.ConsiderParameters);
        Assert.False(options.IncludeInStepControl);
        Assert.Null(options.GeopotentialPartialsDegree);
    }

    [Fact]
    public void Options_InvalidValues_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CovariancePropagationOptions { Mode = (CovariancePropagationMode)7 });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CovariancePropagationOptions { GeopotentialPartialsDegree = 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RtnProcessNoise(-1e-12, 0.0, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConsiderParameters(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ManeuverExecutionError { ResolutionSigma = 0.0, ShutoffSigma = -1.0, AutopilotSigma = 0.0, PointingSigma = 0.0 });
        Assert.Equal(20, new CovariancePropagationOptions { GeopotentialPartialsDegree = 20 }.GeopotentialPartialsDegree);
    }

    [Fact]
    public void Builder_Covariance_GivesTheValuesOfTheInternalPath_BitForBit()
    {
        // Arrange: the same propagation activated through the builder and through the internal options
        var publicSolution = new CentralBodyPropagatorBuilder(Window, Spacecraft(), new RK78Integrator(), Step)
            .WithPerturbingBodies(Bodies)
            .IncludeAtmosphericDrag()
            .IncludeSolarRadiationPressure()
            .WithCovariancePropagation(Full)
            .Build()
            .Propagate();
        var internalPropagator = new CentralBodyPropagator(Window, Spacecraft(), new RK78Integrator(), Bodies, true,
            true, Step);
        internalPropagator.EnableVariationalEquations(new VariationalOptions(
            ForceParameters.DragCoefficient | ForceParameters.ReflectivityCoefficient, Full.ProcessNoise, false, null,
            true, Full.ConsiderParameters));
        var internalSolution = internalPropagator.Propagate();

        // Act and assert: at a step boundary, inside a step, and at the end
        Assert.Equal(new[] { ForceParameter.DragCoefficient, ForceParameter.ReflectivityCoefficient },
            publicSolution.SensitivityParameters);
        Assert.Equal(publicSolution.SensitivityParameters, publicSolution.ConsiderParameters);
        Assert.Same(Full.ProcessNoise.GetType(), publicSolution.CovariancePropagation.ProcessNoise.GetType());
        foreach (var epoch in new[] { Start, Start.AddSeconds(1234.5), Start.AddHours(1.0) })
        {
            var y = new double[6 * 8];
            var q = new double[21];
            internalSolution.EvaluateVariational(epoch, y, q);
            var p = new double[36];
            internalSolution.EvaluateCovariance(epoch, p);
            var extended = new double[64];
            internalSolution.EvaluateExtendedCovariance(epoch, extended);

            var phi = publicSolution.GetStateTransitionMatrix(epoch);
            var psi = publicSolution.GetParameterSensitivity(epoch);
            var covariance = publicSolution.GetCovariance(epoch);
            var extendedCovariance = publicSolution.GetExtendedCovariance(epoch);
            Assert.Equal((6, 6), (phi.Rows, phi.Columns));
            Assert.Equal((6, 2), (psi.Rows, psi.Columns));
            Assert.Equal((8, 8), (extendedCovariance.Rows, extendedCovariance.Columns));
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    Assert.Equal(y[i * 8 + j], phi.Get(i, j));
                    Assert.Equal(p[i * 6 + j], covariance.Get(i, j));
                }

                for (int j = 0; j < 2; j++)
                {
                    Assert.Equal(y[i * 8 + 6 + j], psi.Get(i, j));
                }
            }

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    Assert.Equal(extended[i * 8 + j], extendedCovariance.Get(i, j));
                }
            }
        }
    }

    [Fact]
    public void AtTheStart_PhiIsTheIdentity_PsiIsZero_AndPIsP0Symmetrized()
    {
        // Arrange
        var spacecraft = Spacecraft();
        var solution = spacecraft.Propagate(Window, Bodies, new RK78Integrator(), true, true, Step, Full);
        var p0 = spacecraft.InitialOrbitalParameters.ToStateVector().Covariance!.Value;

        // Act
        var phi = solution.GetStateTransitionMatrix(Start);
        var psi = solution.GetParameterSensitivity(Start);
        var p = solution.GetCovariance(Start);

        // Assert
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                Assert.Equal(i == j ? 1.0 : 0.0, phi.Get(i, j));
                Assert.Equal(0.5 * (p0.Get(i, j) + p0.Get(j, i)), p.Get(i, j));
            }

            Assert.Equal(0.0, psi.Get(i, 0));
            Assert.Equal(0.0, psi.Get(i, 1));
        }
    }

    [Fact]
    public void Covariance_TheOutputStatesCarryP_AndTheTrajectoryIsUnchanged()
    {
        // Arrange
        var spacecraft = Spacecraft();
        var withCovariance = spacecraft.Propagate(Window, Bodies, new RK78Integrator(), true, true, Step, Full);
        var stored = spacecraft.PropagatedStates;
        var without = Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), true, true, Step);

        // Assert: the same states bit for bit, each with the covariance of its epoch; the spacecraft stores them
        Assert.Equal(without.StateVectors.Count, withCovariance.StateVectors.Count);
        Assert.Equal(withCovariance.StateVectors.Count, stored.Length);
        for (int k = 0; k < without.StateVectors.Count; k++)
        {
            var state = withCovariance.StateVectors[k];
            Assert.Equal(without.StateVectors[k].Position, state.Position);
            Assert.Equal(without.StateVectors[k].Velocity, state.Velocity);
            Assert.Null(without.StateVectors[k].Covariance);
            Assert.Equal(withCovariance.GetCovariance(state.Epoch), state.Covariance!.Value);
            Assert.Same(state, stored[k]);

            var sampled = withCovariance.GetStateVector(state.Epoch);
            Assert.Equal(state.Position, sampled.Position);
            Assert.Equal(state.Velocity, sampled.Velocity);
            Assert.Equal(state.Covariance, sampled.Covariance);
        }
    }

    [Fact]
    public void Covariance_AnOutputGridBeyondTheWindow_EndsOnTheLastState()
    {
        // Arrange: 420 s does not divide the hour, so the output grid ends at 3780 s, past the window
        var step = TimeSpan.FromSeconds(420.0);
        var withCovariance = Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), true, true, step, Full);
        var without = Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), true, true, step);

        // Assert
        Assert.Equal(10, withCovariance.StateVectors.Count);
        Assert.Equal(without.StateVectors[^1].Position, withCovariance.StateVectors[^1].Position);
        Assert.Equal(withCovariance.GetCovariance(withCovariance.StateVectors[^1].Epoch),
            withCovariance.StateVectors[^1].Covariance!.Value);
    }

    [Fact]
    public void StateTransitionOnly_NoCovarianceAnywhere()
    {
        // Arrange
        var spacecraft = Spacecraft();
        var solution = spacecraft.Propagate(Window, Bodies, new RK78Integrator(), false, true, Step,
            new CovariancePropagationOptions());

        // Assert
        Assert.Equal(new[] { ForceParameter.ReflectivityCoefficient }, solution.SensitivityParameters);
        Assert.Empty(solution.ConsiderParameters);
        Assert.All(solution.StateVectors, s => Assert.Null(s.Covariance));
        Assert.Null(solution.GetStateVector(Start.AddSeconds(100.0)).Covariance);
        Assert.Equal(CovariancePropagationMode.StateTransitionOnly, solution.CovariancePropagation.Mode);
        Assert.NotEqual(0.0, solution.GetParameterSensitivity(Start.AddHours(1.0)).Get(0, 0));
        Assert.Contains("did not propagate the covariance",
            Assert.Throws<InvalidOperationException>(() => solution.GetCovariance(Start)).Message);
        Assert.Throws<InvalidOperationException>(() => solution.GetExtendedCovariance(Start));
        Assert.Throws<InvalidOperationException>(() => solution.StateAndCovarianceAt(Start));
        Assert.Throws<InvalidOperationException>(() =>
            Spacecraft().Propagate(Window, Bodies, false, false, Step).StateAndCovarianceAt(Start));
    }

    [Fact]
    public void WithoutParametersOrConsiderParameters_TheirGettersThrow()
    {
        // Arrange: two-body, no drag and no radiation force; covariance without consider parameters
        var solution = Spacecraft().Propagate(Window, new CelestialItem[] { _cases.Earth }, new RK78Integrator(),
            false, false, Step, new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });

        // Assert
        Assert.Empty(solution.SensitivityParameters);
        Assert.Contains("no force parameter",
            Assert.Throws<InvalidOperationException>(() => solution.GetParameterSensitivity(Start)).Message);
        Assert.Contains("no consider parameter",
            Assert.Throws<InvalidOperationException>(() => solution.GetExtendedCovariance(Start)).Message);
        Assert.Equal(6, solution.GetCovariance(Start.AddMinutes(10.0)).Rows);
    }

    [Fact]
    public void OutsideThePropagation_EveryGetterThrows()
    {
        // Arrange
        var solution = Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), true, true, Step, Full);
        var before = Start.AddSeconds(-1.0);
        var after = Start.AddHours(2.0);

        // Assert
        foreach (var epoch in new[] { before, after })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => solution.GetStateTransitionMatrix(epoch));
            Assert.Throws<ArgumentOutOfRangeException>(() => solution.GetParameterSensitivity(epoch));
            Assert.Throws<ArgumentOutOfRangeException>(() => solution.GetCovariance(epoch));
            Assert.Throws<ArgumentOutOfRangeException>(() => solution.GetExtendedCovariance(epoch));
            Assert.Throws<ArgumentOutOfRangeException>(() => solution.GetStateVector(epoch));
        }
    }

    [Fact]
    public void WithoutActivation_EveryGetterThrows_ButTheStateIsGiven()
    {
        // Arrange: RK7(8) and Velocity-Verlet without activation, and a solution built by hand
        var rk78 = Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), false, false, Step);
        var vv = Spacecraft().Propagate(Window, Bodies, false, false, Step);
        var byHand = new PropagationSolution();
        var epoch = Start.AddSeconds(500.0);

        // Assert
        foreach (var solution in new[] { rk78, vv })
        {
            Assert.Null(solution.CovariancePropagation);
            Assert.Empty(solution.SensitivityParameters);
            Assert.Empty(solution.ConsiderParameters);
            Assert.Throws<InvalidOperationException>(() => solution.GetStateTransitionMatrix(epoch));
            Assert.Throws<InvalidOperationException>(() => solution.GetParameterSensitivity(epoch));
            Assert.Throws<InvalidOperationException>(() => solution.GetCovariance(epoch));
            Assert.Throws<InvalidOperationException>(() => solution.GetExtendedCovariance(epoch));
            var state = solution.GetStateVector(epoch);
            Assert.Null(state.Covariance);
            Assert.Equal(solution.InterpolateAt(epoch).position, state.Position);
            Assert.Equal(_cases.Earth.NaifId, state.Observer.NaifId);
            Assert.Equal(Frames.Frame.ICRF, state.Frame);
        }

        Assert.Throws<InvalidOperationException>(() => byHand.GetStateTransitionMatrix(epoch));
        Assert.Throws<InvalidOperationException>(() => byHand.GetStateVector(epoch));
        var segment = new PropagationSegment(Start);
        segment.AddStep(rk78.Segments[0].Steps[0]);
        byHand.AddSegment(segment);
        Assert.Contains("not built by a propagator",
            Assert.Throws<InvalidOperationException>(() => byHand.GetStateVector(Start)).Message);
    }

    [Fact]
    public void Activation_InvalidRequests_Throw()
    {
        // Velocity-Verlet: every entry point
        var vvOptions = new CovariancePropagationOptions();
        Assert.Throws<NotSupportedException>(() => new CentralBodyPropagator(Window, Spacecraft(),
            new VVIntegrator(Step), Bodies, false, false, Step, vvOptions));
        var spacecraft = Spacecraft();
        var forces = new ForceBase[] { new GravitationalAcceleration(_cases.Earth) };
        Assert.Throws<NotSupportedException>(() => new CentralBodyPropagator(Window, spacecraft,
            new VVIntegrator(forces, Step, spacecraft.InitialOrbitalParameters.ToStateVector()), Step, vvOptions));
        Assert.Throws<NotSupportedException>(() =>
            new CentralBodyPropagatorBuilder(Window, Spacecraft(), new VVIntegrator(Step), Step)
                .WithCovariancePropagation(vvOptions).Build());
        Assert.Throws<NotSupportedException>(() =>
            Spacecraft().Propagate(Window, Bodies, new VVIntegrator(Step), false, false, Step, vvOptions));

        // Noise or consider parameters without the covariance
        Assert.Contains("act on the covariance only", Assert.Throws<ArgumentException>(() =>
            Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), true, false, Step,
                new CovariancePropagationOptions { ProcessNoise = new RtnProcessNoise(0.0, 1e-12, 0.0) })).Message);
        Assert.Throws<ArgumentException>(() =>
            Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), true, false, Step,
                new CovariancePropagationOptions { ConsiderParameters = new ConsiderParameters(0.1) }));

        // A consider parameter without its force: Cr without a radiation force
        Assert.Contains("needs its force", Assert.Throws<ArgumentException>(() =>
            Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), true, false, Step,
                Full with { ConsiderParameters = new ConsiderParameters(null, 0.1) })).Message);

        // The covariance without P0
        var withoutP0 = PartialsTestCases.Spacecraft(Leo);
        Assert.Contains("carry none", Assert.Throws<InvalidOperationException>(() =>
            withoutP0.Propagate(Window, Bodies, new RK78Integrator(), true, true, Step, Full)).Message);

        // The builder
        var builder = new CentralBodyPropagatorBuilder(Window, Spacecraft(), new RK78Integrator(), Step);
        Assert.Throws<ArgumentNullException>(() => builder.WithCovariancePropagation(null));
        builder.WithCovariancePropagation(vvOptions);
        Assert.Throws<InvalidOperationException>(() => builder.WithCovariancePropagation(vvOptions));
    }

    [Fact]
    public void Builder_PsiFollowsTheAlbedoAndThermalForces()
    {
        // Arrange: albedo alone is a radiation force, added by Build after the propagator
        var solution = new CentralBodyPropagatorBuilder(Window, Spacecraft(), new RK78Integrator(), Step)
            .IncludeAlbedo(_cases.Earth)
            .WithCovariancePropagation(new CovariancePropagationOptions())
            .Build()
            .Propagate();

        // Assert
        Assert.Equal(new[] { ForceParameter.ReflectivityCoefficient }, solution.SensitivityParameters);
        Assert.NotEqual(0.0, solution.GetParameterSensitivity(Start.AddHours(1.0)).Get(0, 0));
    }

    [Fact]
    public void IntegratorWithItsForces_ThePsiColumnsFollowThem()
    {
        // Arrange
        var spacecraft = Spacecraft();
        var integrator = new RK78Integrator();
        integrator.AddForce(new GravitationalAcceleration(_cases.Earth));
        integrator.AddForce(new AtmosphericDrag(spacecraft, _cases.Earth));
        var propagator = new CentralBodyPropagator(Window, spacecraft, integrator, Step,
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });
        integrator.Initialize(spacecraft.InitialOrbitalParameters.ToStateVector());

        // Act
        var solution = propagator.Propagate();

        // Assert
        Assert.Equal(new[] { ForceParameter.DragCoefficient }, solution.SensitivityParameters);
        Assert.All(solution.StateVectors, s => Assert.NotNull(s.Covariance));
    }

    [Fact]
    public void NullOptions_AreOff()
    {
        // Act
        var solution = Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), false, false, Step,
            (CovariancePropagationOptions)null);
        var reference = Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), false, false, Step);

        // Assert
        Assert.Null(solution.CovariancePropagation);
        Assert.Equal(reference.StateVectors.Select(s => s.Position), solution.StateVectors.Select(s => s.Position));
        Assert.All(solution.StateVectors, s => Assert.Null(s.Covariance));
    }

    [Fact]
    public void BatchTask_CarriesTheOptions()
    {
        // Arrange
        var tasks = new[]
        {
            new PropagationTask(Spacecraft(-2101), Window, Bodies, true, false, Step, () => new RK78Integrator())
                { CovariancePropagation = Full with { ConsiderParameters = new ConsiderParameters(0.1) } },
            new PropagationTask(Spacecraft(-2102), Window, Bodies, true, false, Step, () => new RK78Integrator()),
            new PropagationTask(Spacecraft(-2103), Window, Bodies, true, false, Step)
                { CovariancePropagation = new CovariancePropagationOptions() }
        };

        // Act
        var result = BatchPropagator.Propagate(tasks);

        // Assert
        Assert.Null(result.Results[0].Error);
        Assert.All(result.Results[0].Solution.StateVectors, s => Assert.NotNull(s.Covariance));
        Assert.Null(result.Results[1].Error);
        Assert.Null(result.Results[1].Solution.CovariancePropagation);
        Assert.IsType<NotSupportedException>(result.Results[2].Error);
        Assert.Null(tasks[1].CovariancePropagation);
    }

    [Fact]
    public async Task Scenario_PropagatesTheCovarianceOfEverySpacecraft_AndNamesOneWithoutP0()
    {
        // Arrange
        var scenario = new Scenario("covariance", new Astrodynamics.Mission.Mission("covariance"), Window);
        scenario.AddCelestialItem(_cases.Moon);
        var spacecraft = Spacecraft(-2111);
        scenario.AddSpacecraft(spacecraft);
        var failing = new Scenario("covariance2", new Astrodynamics.Mission.Mission("covariance2"), Window);
        failing.AddSpacecraft(new Spacecraft(-2112, "NOP0", 100.0, 1000.0, new Clock("nop0", 65536), Leo));
        var options = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance };

        // Act
        await scenario.SimulateAsync(false, false, Step, () => new RK78Integrator(), options);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            failing.SimulateAsync(false, false, Step, () => new RK78Integrator(), options));

        // Assert
        Assert.True(scenario.IsSimulated);
        Assert.All(spacecraft.PropagatedStates, s => Assert.NotNull(s.Covariance));
        Assert.Contains("'NOP0'", exception.Message);
        Assert.Contains("carry none", exception.Message);
    }

    [Fact]
    public void ExecutionError_PublicProperty_AddsToTheVelocityCovarianceAtTheBurn()
    {
        // Arrange: the same maneuver with and without its execution error
        var error = new ManeuverExecutionError { ResolutionSigma = 0.01, ShutoffSigma = 0.002, AutopilotSigma = 0.005, PointingSigma = 0.001 };
        var (withError, burnEpoch) = PropagateWithManeuver(-2121, error);
        var (withoutError, _) = PropagateWithManeuver(-2122, null);
        var after = burnEpoch.AddSeconds(1e-3);

        // Act
        var p = withError.GetCovariance(after);
        var reference = withoutError.GetCovariance(after);

        // Assert: the velocity variances grow, the trajectory is the same
        for (int i = 3; i < 6; i++)
        {
            Assert.True(p.Get(i, i) > reference.Get(i, i));
        }

        Assert.Equal(withoutError.StateVectors[^1].Position, withError.StateVectors[^1].Position);
        Assert.Equal(0.01, error.ResolutionSigma);
        Assert.Equal(0.002, error.ShutoffSigma);
        Assert.Equal(0.005, error.AutopilotSigma);
        Assert.Equal(0.001, error.PointingSigma);
    }

    [Fact]
    public void EvaluateVariational_WrongBufferLengths_Throw()
    {
        // Arrange: Cd column and Q carried, so Y holds 42 values and Q 21
        var solution = Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), true, false, Step, Full with
        {
            ConsiderParameters = new ConsiderParameters(0.1)
        });

        // Assert
        Assert.Throws<ArgumentException>(() => solution.EvaluateVariational(Start, new double[36], new double[21]));
        Assert.Throws<ArgumentException>(() => solution.EvaluateVariational(Start, new double[42], new double[0]));
        Assert.Throws<ArgumentException>(() => solution.EvaluateCovariance(Start, new double[35]));
        Assert.Throws<ArgumentException>(() => solution.EvaluateExtendedCovariance(Start, new double[36]));
        solution.EvaluateVariational(Start, new double[42], new double[21]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithoutActivation_AnInitialCovarianceIsNotPropagated_AndNoPartialIsEvaluated(bool rk78)
    {
        // Arrange: the same spacecraft with and without P0
        var withP0 = Propagator(Spacecraft(-2131), rk78, Window);
        var withoutP0 = Propagator(PartialsTestCases.Spacecraft(Leo), rk78, Window);

        // Act
        var solution = withP0.Propagate();
        var reference = withoutP0.Propagate();

        // Assert: no partial evaluated, no covariance on the output states, the same states bit for bit; the initial
        // covariance stays on the initial orbital parameters
        var integrator = (Integrator)withP0.Integrator;
        Assert.All(integrator.Forces, f => Assert.Equal(0, f.PartialsEvaluations));
        Assert.Equal(0, solution.Dynamics?.StageJacobianEvaluations ?? 0);
        Assert.All(solution.StateVectors, s => Assert.Null(s.Covariance));
        Assert.Equal(reference.StateVectors.Select(s => (s.Position, s.Velocity)),
            solution.StateVectors.Select(s => (s.Position, s.Velocity)));
        Assert.NotNull(withP0.Spacecraft.InitialOrbitalParameters.ToStateVector().Covariance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithoutActivation_AnInitialCovarianceAllocatesNothingPerStep(bool rk78)
    {
        // Arrange: a warm-up, then two window lengths, with and without P0
        Propagator(Spacecraft(-2141), rk78, Window).Propagate();
        Propagator(PartialsTestCases.Spacecraft(Leo), rk78, Window).Propagate();

        // Act: the least of three runs, since a shared cache (frame orientations, ephemerides) may be filled once by
        // whichever propagation first reads an epoch, a test of another class included
        long Allocated(Func<Spacecraft> spacecraft, TimeSystem.Window window)
        {
            long least = long.MaxValue;
            for (int run = 0; run < 3; run++)
            {
                var propagator = Propagator(spacecraft(), rk78, window);
                long before = GC.GetAllocatedBytesForCurrentThread();
                propagator.Propagate();
                least = System.Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            }

            return least;
        }

        var longWindow = new TimeSystem.Window(Start, Start.AddHours(3.0));
        long shortDifference = Allocated(() => Spacecraft(-2142), Window) -
                               Allocated(() => PartialsTestCases.Spacecraft(Leo), Window);
        long longDifference = Allocated(() => Spacecraft(-2143), longWindow) -
                              Allocated(() => PartialsTestCases.Spacecraft(Leo), longWindow);
        _output.WriteLine($"{(rk78 ? "RK7(8)" : "VV")}: P0 adds {shortDifference} B over 1 h, {longDifference} B over 3 h");

        // Assert: what P0 adds does not grow with the number of steps
        Assert.Equal(shortDifference, longDifference);
    }

    [Fact]
    public void Covariance_AnOutputStateAllocatesItsResultsOnly()
    {
        // Arrange: a state inside a step, and the same evaluations without the covariance, warmed up
        var solution = Spacecraft().Propagate(Window, Bodies, new RK78Integrator(), true, true, Step, Full);
        var epoch = Start.AddSeconds(1234.5);
        solution.StateAndCovarianceAt(epoch);
        solution.InterpolateAt(epoch);
        var y = new double[48];
        var q = new double[21];
        solution.EvaluateVariational(epoch, y, q);

        // Act
        long Allocated(Action action)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        long withCovariance = Allocated(() => solution.StateAndCovarianceAt(epoch));
        long stateOnly = Allocated(() => solution.InterpolateAt(epoch));
        long variational = Allocated(() => solution.EvaluateVariational(epoch, y, q));
        var leo = Leo;
        Allocated(() => _ = new StateVector(leo.Position, leo.Velocity, leo.Observer, epoch, leo.Frame,
            new Matrix(6, 6, new double[36])));
        long results = Allocated(() =>
            _ = new StateVector(leo.Position, leo.Velocity, leo.Observer, epoch, leo.Frame,
                new Matrix(6, 6, new double[36])));
        _output.WriteLine($"State and covariance {withCovariance} B; state alone {stateOnly} B; Y and Q {variational} B; " +
                          $"the state vector, its 36 values and its matrix {results} B");

        // Assert: the evaluation adds nothing to what the shortened step and its results allocate
        Assert.Equal(stateOnly + results, withCovariance);
        Assert.Equal(stateOnly, variational);
    }

    private CentralBodyPropagator Propagator(Spacecraft spacecraft, bool rk78, TimeSystem.Window window)
    {
        return rk78
            ? new CentralBodyPropagator(window, spacecraft, new RK78Integrator(), Bodies, true, true, Step)
            : new CentralBodyPropagator(window, spacecraft, Bodies, true, true, TimeSpan.FromSeconds(10.0));
    }

    private (PropagationSolution Solution, TimeSystem.Time BurnEpoch) PropagateWithManeuver(int naifId,
        ManeuverExecutionError error)
    {
        var state = CovariancePropagationTests.WithCovariance(Leo);
        var spacecraft = new Spacecraft(naifId, "BURN" + naifId, 1000.0, 3000.0, new Clock("burn" + naifId, 65536),
            state);
        var tank = new FuelTank("tank" + naifId, "model", "sn1", 1000.0, 1000.0);
        var engine = new Engine("engine" + naifId, "model", "sn1", 300.0, 10.0, tank);
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);
        var maneuver = new ApogeeHeightManeuver(_cases.Earth, Start, TimeSpan.Zero, 7000000.0, engine)
        {
            ExecutionError = error
        };
        spacecraft.SetStandbyManeuver(maneuver);
        var solution = spacecraft.Propagate(new TimeSystem.Window(Start, Start.AddHours(2.0)),
            new CelestialItem[] { _cases.Earth }, new RK78Integrator(1e-11, 1e-11), false, false, Step,
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });
        Assert.NotNull(maneuver.ThrustWindow);
        return (solution, solution.Segments[1].BaseEpoch);
    }

    private Spacecraft Spacecraft(int naifId = -1961)
    {
        var state = CovariancePropagationTests.WithCovariance(Leo);
        return new Spacecraft(naifId, "API" + naifId, 100.0, 1000.0, new Clock("api" + naifId, 65536), state, 10.0, 2.2,
            null, 1.5);
    }
}
