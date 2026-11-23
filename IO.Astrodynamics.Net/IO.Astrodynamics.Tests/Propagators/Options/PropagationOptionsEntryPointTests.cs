// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;
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

    private static Spacecraft Template(ILocalizable earth)
    {
        var covariance = IO.Astrodynamics.Math.Matrix.Diagonal(100.0, 100.0, 100.0, 0.01, 0.01, 0.01);
        return new Spacecraft(-1700001, "MCOPT", 100.0, 10000.0, new Clock("mcopt", 256),
            new StateVector(new Vector3(6800000.0, 0, 0), new Vector3(0, 7656.2, 0), earth, Start, Frames.Frame.ICRF,
                covariance));
    }

}
