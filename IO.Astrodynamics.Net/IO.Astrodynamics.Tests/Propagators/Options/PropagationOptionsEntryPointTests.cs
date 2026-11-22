// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Options;

/// <summary>
/// The options entry points beyond <see cref="Spacecraft.Propagate(Window, PropagationOptions)"/> (R06 of #668, lot L4,
/// resolution C2): <see cref="Scenario.Simulate(PropagationOptions)"/> (the scenario's bodies when
/// <see cref="PropagationOptions.ThirdBodies"/> is null, a conflict throwing), <see cref="PropagationTask"/> through
/// <see cref="BatchPropagator"/>, and <see cref="MonteCarloConfiguration.Propagation"/>, each bit for bit the entry point
/// of 10.x with the integrator, bodies and flags its settings name.
/// </summary>
public class PropagationOptionsEntryPointTests
{
    private static readonly TimeSystem.Time Start = new(2021, 1, 1, 0, 0, 0, TimeScale.TDB);
    private static readonly Window OneHour = new(Start, Start + TimeSpan.FromHours(1));
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60);

    public PropagationOptionsEntryPointTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static Spacecraft NewSpacecraft() =>
        PropagationOptionsPathTests.NewSpacecraft(PropagationOptionsPathTests.Leo(PlanetsAndMoons.EARTH_BODY));

    private static Scenario NewScenario(Spacecraft spacecraft, params CelestialItem[] bodies)
    {
        var scenario = new Scenario($"OPT{-spacecraft.NaifId}", new IO.Astrodynamics.Mission.Mission($"mOPT{-spacecraft.NaifId}"), OneHour);
        scenario.AddSpacecraft(spacecraft);
        foreach (var body in bodies)
        {
            scenario.AddCelestialItem(body);
        }

        return scenario;
    }

    [Fact]
    public async Task Simulate_EqualsSimulateAsyncOfItsIntegratorAndFlags_WithTheScenariosBodies()
    {
        var viaOptions = NewSpacecraft();
        var reference = NewSpacecraft();
        var options = PropagationOptions.Default with { SolarRadiationPressure = true };

        var summary = NewScenario(viaOptions, PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY)
            .Simulate(options);
        await NewScenario(reference, PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY)
            .SimulateAsync(false, true, Output, () => new RK78Integrator());

        Assert.NotNull(summary);
        PropagationOptionsPathTests.AssertSameStates(reference.PropagatedStates, viaOptions.PropagatedStates);
    }

    [Fact]
    public void Simulate_ThirdBodiesEqualToTheScenarios_TheCentralBodyAside_Run()
    {
        var viaList = NewSpacecraft();
        var viaScenario = NewSpacecraft();
        NewScenario(viaList, PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY).Simulate(PropagationOptions.Default with
            { ThirdBodies = new CelestialItem[] { Stars.SUN_BODY } });
        NewScenario(viaScenario, PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY).Simulate();
        PropagationOptionsPathTests.AssertSameStates(viaScenario.PropagatedStates, viaList.PropagatedStates);
    }

    [Fact]
    public void Simulate_ThirdBodiesThatDifferFromTheScenarios_Throw_BeforeAnyPropagation()
    {
        var spacecraft = NewSpacecraft();
        var scenario = NewScenario(spacecraft, Stars.SUN_BODY);
        var exception = Assert.Throws<ArgumentException>(() => scenario.Simulate(PropagationOptions.Default with
            { ThirdBodies = new CelestialItem[] { PlanetsAndMoons.MOON_BODY } }));
        Assert.Equal("options", exception.ParamName);
        Assert.False(spacecraft.IsPropagated);
        Assert.False(scenario.IsSimulated);
    }

    [Fact]
    public void Simulate_AScenarioWithoutBodies_TakesTheList()
    {
        var viaScenario = NewSpacecraft();
        var viaSpacecraft = NewSpacecraft();
        var options = PropagationOptions.Default with { ThirdBodies = new CelestialItem[] { Stars.SUN_BODY } };
        NewScenario(viaScenario).Simulate(options);
        viaSpacecraft.Propagate(OneHour, options);
        PropagationOptionsPathTests.AssertSameStates(viaSpacecraft.PropagatedStates, viaScenario.PropagatedStates);
    }

    [Fact]
    public void BatchTasksOfOptions_EqualSpacecraftPropagate()
    {
        var options = PropagationOptions.Default with { ThirdBodies = new CelestialItem[] { Stars.SUN_BODY } };
        var tasks = Enumerable.Range(0, 3).Select(_ => new PropagationTask(NewSpacecraft(), OneHour, options)).ToArray();
        var reference = NewSpacecraft();
        reference.Propagate(OneHour, options);

        var result = BatchPropagator.Propagate(tasks);

        Assert.All(result.Results, r => Assert.True(r.Succeeded, r.Error?.ToString()));
        foreach (var task in tasks)
        {
            PropagationOptionsPathTests.AssertSameStates(reference.PropagatedStates, task.Spacecraft.PropagatedStates);
        }
    }

    [Fact]
    public void ABatchTaskOfOptions_WithTheCovariancePropertyOfTheFlagsConstructor_Fails()
    {
        var task = new PropagationTask(NewSpacecraft(), OneHour)
            { CovariancePropagation = new Propagator.Variational.CovariancePropagationOptions() };
        var result = BatchPropagator.Propagate(new[] { task });
        Assert.IsType<ArgumentException>(result.Results[0].Error);
    }

    private static Spacecraft Template(ILocalizable earth)
    {
        var covariance = IO.Astrodynamics.Math.Matrix.Diagonal(100.0, 100.0, 100.0, 0.01, 0.01, 0.01);
        return new Spacecraft(-1700001, "MCOPT", 100.0, 10000.0, new Clock("mcopt", 256),
            new StateVector(new Vector3(6800000.0, 0, 0), new Vector3(0, 7656.2, 0), earth, Start, Frames.Frame.ICRF,
                covariance));
    }

    [Fact]
    public void MonteCarloPropagation_EqualsTheFlagsConfiguration_OfItsIntegratorAndBodies()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var viaOptions = MonteCarloPropagator.Propagate(new MonteCarloConfiguration
        {
            RunCount = 4, Seed = 7, TemplateSpacecraft = Template(earth), Window = OneHour,
            Propagation = PropagationOptions.Default with
                { ThirdBodies = new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY } }
        });
        var reference = MonteCarloPropagator.Propagate(new MonteCarloConfiguration
        {
            RunCount = 4, Seed = 7, TemplateSpacecraft = Template(earth), Window = OneHour,
            CelestialBodiesFactory = () => new CelestialItem[] { earth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY },
            DeltaT = Output, IntegratorFactory = () => new RK78Integrator()
        });

        Assert.Equal(4, viaOptions.SuccessCount);
        Assert.Equal(reference.EpochStatistics.Count, viaOptions.EpochStatistics.Count);
        for (int i = 0; i < reference.EpochStatistics.Count; i++)
        {
            Assert.Equal(reference.EpochStatistics[i].MeanPosition, viaOptions.EpochStatistics[i].MeanPosition);
            Assert.Equal(reference.EpochStatistics[i].MeanVelocity, viaOptions.EpochStatistics[i].MeanVelocity);
        }
    }

    [Fact]
    public void MonteCarloPropagation_WithAMemberOfTheFlagsConfiguration_Throws()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var config = new MonteCarloConfiguration
        {
            RunCount = 2, Seed = 7, TemplateSpacecraft = Template(earth), Window = OneHour,
            Propagation = PropagationOptions.Default, DeltaT = Output
        };
        Assert.Throws<ArgumentException>(() => MonteCarloPropagator.Propagate(config));
    }
}
