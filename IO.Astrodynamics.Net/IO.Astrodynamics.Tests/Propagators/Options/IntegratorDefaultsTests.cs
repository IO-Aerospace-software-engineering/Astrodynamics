// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Options;

/// <summary>
/// R07 of #668 with structuring choice 5 (lot L4, decided by S. Guillet, 2026-11-19, as amended by section 7 of the
/// re-baseline: no forwarder, no error-level trap): the members that propagated with no integrator (Velocity-Verlet at
/// the output step, 974 km off Kepler after a LEO day at 60 s) are removed with the flag overloads, the builder, the
/// flags <see cref="PropagationTask"/> and the factories of <see cref="MonteCarloConfiguration"/>; the options entry
/// points default to RK7(8) at <see cref="RK78IntegratorOptions.Default"/>, which is the integrator of
/// <c>new RK78Integrator()</c>, and <see cref="VVIntegratorOptions.Default"/> steps 1 s (D4.7: each default equals its
/// explicit settings, bit for bit).
/// </summary>
/// <remarks>
/// The explicit reference is the constructor of an integrator that holds its forces, the one path that takes an
/// integrator instance: <c>new RK78Integrator()</c> (or <c>new VVIntegrator(1 s)</c>) with the central body's point-mass
/// gravity, outputs every 60 s.
/// </remarks>
public class IntegratorDefaultsTests
{
    private static readonly TimeSystem.Time Start = new(2024, 6, 21, 12, 0, 0, TimeScale.TDB);
    private static readonly Window ThreeHours = new(Start, Start.AddHours(3.0));
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60.0);

    public IntegratorDefaultsTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static int _nextId = -1792000;

    private static int NextId() => System.Threading.Interlocked.Decrement(ref _nextId);

    private static StateVector Leo(Matrix? covariance = null) =>
        new(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7700.0, 0.0), PlanetsAndMoons.EARTH_BODY, Start,
            Frames.Frame.ICRF, covariance);

    private static Spacecraft NewSpacecraft(IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit)
    {
        int id = NextId();
        return new Spacecraft(id, $"DEF{-id}", 500.0, 3000.0, new Clock($"def{-id}", 65536), orbit);
    }

    // The explicit settings: the integrator instance with its forces, the central body's point-mass gravity
    private static StateVector[] Explicit(Integrator integrator)
    {
        var spacecraft = NewSpacecraft(Leo());
        integrator.AddForce(new GravitationalAcceleration(PlanetsAndMoons.EARTH_BODY));
        new CentralBodyPropagator(ThreeHours, spacecraft, integrator, Output).Propagate();
        return spacecraft.PropagatedStates;
    }

    private static void AssertSame(StateVector[] expected, StateVector[] actual) =>
        PropagationOptionsPathTests.AssertSameStates(expected, actual);

    /// <summary>
    /// Every options entry point, given no settings, is the run of <c>new RK78Integrator()</c> about the central body
    /// alone every 60 s, bit for bit.
    /// </summary>
    [Theory]
    [InlineData("Spacecraft.Propagate")]
    [InlineData("CentralBodyPropagator")]
    [InlineData("Scenario.Simulate")]
    [InlineData("PropagationTask")]
    public void EveryOptionsEntryPoint_DefaultsToRk78AtItsDefaults(string path)
    {
        // Arrange
        var expected = Explicit(new RK78Integrator());
        var spacecraft = NewSpacecraft(Leo());

        // Act
        switch (path)
        {
            case "Spacecraft.Propagate":
                spacecraft.Propagate(ThreeHours);
                break;
            case "CentralBodyPropagator":
                new CentralBodyPropagator(ThreeHours, spacecraft).Propagate();
                break;
            case "Scenario.Simulate":
            {
                var scenario = new Scenario("def", new IO.Astrodynamics.Mission.Mission("def"), ThreeHours);
                scenario.AddSpacecraft(spacecraft);
                scenario.Simulate();
                break;
            }
            default:
                BatchPropagator.Propagate(new[] { new PropagationTask(spacecraft, ThreeHours) });
                break;
        }

        // Assert
        AssertSame(expected, spacecraft.PropagatedStates);
    }

    /// <summary>
    /// <see cref="VVIntegratorOptions.Default"/> is Velocity-Verlet at 1 s, the run of <c>new VVIntegrator(1 s)</c>, bit
    /// for bit.
    /// </summary>
    [Fact]
    public void VvDefault_IsVelocityVerletAtOneSecond()
    {
        var expected = Explicit(new VVIntegrator(TimeSpan.FromSeconds(1.0)));
        var spacecraft = NewSpacecraft(Leo());

        spacecraft.Propagate(ThreeHours, new PropagationOptions { Integrator = VVIntegratorOptions.Default });

        AssertSame(expected, spacecraft.PropagatedStates);
    }

    /// <summary>
    /// A Monte Carlo configuration without <see cref="MonteCarloConfiguration.Propagation"/> runs
    /// <see cref="PropagationOptions.Default"/>: the statistics of the explicit settings, bit for bit.
    /// </summary>
    [Fact]
    public void MonteCarlo_WithoutPropagation_RunsTheDefaults()
    {
        var template = NewSpacecraft(Leo(Matrix.Diagonal(100.0, 100.0, 100.0, 1e-2, 1e-2, 1e-2)));
        var byDefault = MonteCarloPropagator.Propagate(new MonteCarloConfiguration
        {
            RunCount = 3, Seed = 11, TemplateSpacecraft = template, Window = ThreeHours
        });
        var explicitSettings = MonteCarloPropagator.Propagate(new MonteCarloConfiguration
        {
            RunCount = 3, Seed = 11, TemplateSpacecraft = template, Window = ThreeHours,
            Propagation = new PropagationOptions
            {
                Integrator = new RK78IntegratorOptions
                {
                    AbsoluteTolerance = 1e-9, RelativeTolerance = 1e-9, InitialStep = TimeSpan.FromSeconds(60.0),
                    MinStep = TimeSpan.FromTicks(10), MaxStep = TimeSpan.FromDays(1.0), SafetyFactor = 0.9,
                    MinFactor = 0.2, MaxFactor = 5.0
                },
                OutputStep = Output, ThirdBodies = Array.Empty<CelestialItem>()
            }
        });

        Assert.Equal(3, byDefault.SuccessCount);
        Assert.Equal(explicitSettings.EpochStatistics.Count, byDefault.EpochStatistics.Count);
        for (int i = 0; i < byDefault.EpochStatistics.Count; i++)
        {
            Assert.True(explicitSettings.EpochStatistics[i].MeanPosition == byDefault.EpochStatistics[i].MeanPosition &&
                        explicitSettings.EpochStatistics[i].MeanVelocity == byDefault.EpochStatistics[i].MeanVelocity,
                $"epoch {i}");
        }
    }

    /// <summary>
    /// The members with no integrator, the flag overloads and the builder are removed outright (section 7 of the
    /// re-baseline of #668): each propagation entry point takes <see cref="PropagationOptions"/>, or an integrator that
    /// holds its forces.
    /// </summary>
    [Fact]
    public void TheMembersWithoutOptions_AreRemoved()
    {
        var library = typeof(PropagationOptions).Assembly;
        Assert.Null(library.GetType("IO.Astrodynamics.Propagator.CentralBodyPropagatorBuilder"));

        // Spacecraft: Propagate(window, options), no async overload
        var propagate = typeof(Spacecraft).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name is "Propagate" or "PropagateAsync").ToArray();
        var only = Assert.Single(propagate);
        Assert.Equal(new[] { typeof(Window), typeof(PropagationOptions) },
            only.GetParameters().Select(p => p.ParameterType).ToArray());

        // Scenario: Simulate(options), no flags overload
        Assert.DoesNotContain(typeof(Scenario).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            m => m.Name == "SimulateAsync" || m.GetParameters().Any(p => p.ParameterType == typeof(bool)));

        // CentralBodyPropagator: options, or an integrator that holds its forces
        foreach (var constructor in typeof(CentralBodyPropagator).GetConstructors())
        {
            var types = constructor.GetParameters().Select(p => p.ParameterType).ToArray();
            Assert.DoesNotContain(typeof(bool), types);
            Assert.True(types.Contains(typeof(PropagationOptions)) || types.Contains(typeof(IIntegrator)),
                constructor.ToString());
        }

        // PropagationTask: (spacecraft, window, options) alone
        var task = Assert.Single(typeof(PropagationTask).GetConstructors(), c => !c.GetParameters()
            .Any(p => p.ParameterType == typeof(PropagationTask)));
        Assert.Equal(new[] { typeof(Spacecraft), typeof(Window), typeof(PropagationOptions) },
            task.GetParameters().Select(p => p.ParameterType).ToArray());
        foreach (var name in new[]
                 {
                     "CelestialBodies", "IncludeAtmosphericDrag", "IncludeSolarRadiationPressure", "DeltaT",
                     "IntegratorFactory", "CovariancePropagation"
                 })
        {
            Assert.Null(typeof(PropagationTask).GetProperty(name));
        }

        // MonteCarloConfiguration: Propagation alone
        foreach (var name in new[]
                 {
                     "CelestialBodiesFactory", "IncludeAtmosphericDrag", "IncludeSolarRadiationPressure", "DeltaT",
                     "IntegratorFactory"
                 })
        {
            Assert.Null(typeof(MonteCarloConfiguration).GetProperty(name));
        }
    }
}
