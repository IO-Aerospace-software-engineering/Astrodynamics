using System;
using System.Collections.Generic;
using System.IO;
using IO.Astrodynamics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators;

public class CentralBodyPropagatorBuilderTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");

    public CentralBodyPropagatorBuilderTests()
    {
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
    }

    private static CelestialBody CreateEarth(double albedo = 0.0,
        double thermalEffectiveTemperature = 0.0, double thermalEmissivity = 0.0)
    {
        return new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB, albedo: albedo,
            thermalEffectiveTemperature: thermalEffectiveTemperature, thermalEmissivity: thermalEmissivity);
    }

    private static Spacecraft CreateSpacecraft(CelestialBody earth)
    {
        Clock clk = new Clock("My clock", 256);
        return new Spacecraft(-1001, "MySpacecraft", 100.0, 10000.0, clk,
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0),
                earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));
    }

    #region Fluent API

    [Fact]
    public void FluentMethodsReturnBuilder()
    {
        var earth = CreateEarth(albedo: 0.306);
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var builder = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60));
        var result1 = builder.WithPerturbingBody(Stars.SUN_BODY);
        var result2 = result1.IncludeAtmosphericDrag();
        var result3 = result2.IncludeSolarRadiationPressure();
        var result4 = result3.IncludeAlbedo(earth);

        Assert.Same(builder, result1);
        Assert.Same(builder, result2);
        Assert.Same(builder, result3);
        Assert.Same(builder, result4);
    }

    [Fact]
    public void WithPerturbingBodiesBulkAdd()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var builder = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60));
        var bodies = new CelestialItem[] { Stars.SUN_BODY, new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB) };
        var result = builder.WithPerturbingBodies(bodies);

        Assert.Same(builder, result);

        // Should build successfully with multiple perturbing bodies
        var propagator = builder.Build();
        Assert.NotNull(propagator);
    }

    [Fact]
    public void DuplicatePerturbingBodyThrows()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var builder = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60))
            .WithPerturbingBody(Stars.SUN_BODY);

        Assert.Throws<InvalidOperationException>(() => builder.WithPerturbingBody(Stars.SUN_BODY));
    }

    [Fact]
    public void DuplicateAtmosphericDragThrows()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var builder = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60))
            .IncludeAtmosphericDrag();

        Assert.Throws<InvalidOperationException>(() => builder.IncludeAtmosphericDrag());
    }

    [Fact]
    public void DuplicateSrpThrows()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var builder = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60))
            .IncludeSolarRadiationPressure();

        Assert.Throws<InvalidOperationException>(() => builder.IncludeSolarRadiationPressure());
    }

    [Fact]
    public void DuplicateAlbedoThrows()
    {
        var earth = CreateEarth(albedo: 0.306);
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var builder = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60))
            .IncludeAlbedo(earth);

        Assert.Throws<InvalidOperationException>(() => builder.IncludeAlbedo(earth));
    }

    [Fact]
    public void NullAlbedoBodyThrows()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var builder = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60));

        Assert.Throws<ArgumentNullException>(() => builder.IncludeAlbedo(null));
    }

    [Fact]
    public void IncludeThermalRadiationReturnsBuilder()
    {
        var earth = CreateEarth(thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var builder = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60));
        var result = builder.IncludeThermalRadiation(earth);

        Assert.Same(builder, result);
    }

    [Fact]
    public void DuplicateThermalRadiationThrows()
    {
        var earth = CreateEarth(thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var builder = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60))
            .IncludeThermalRadiation(earth);

        Assert.Throws<InvalidOperationException>(() => builder.IncludeThermalRadiation(earth));
    }

    [Fact]
    public void NullThermalBodyThrows()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var builder = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60));

        Assert.Throws<ArgumentNullException>(() => builder.IncludeThermalRadiation(null));
    }

    #endregion

    #region Build and Propagation

    [Fact]
    public void BuildWithoutAlbedoProducesPropagator()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var propagator = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60))
            .WithPerturbingBody(Stars.SUN_BODY)
            .Build();

        Assert.NotNull(propagator);
    }

    [Fact]
    public void BuildWithAlbedoProducesPropagator()
    {
        var earth = CreateEarth(albedo: 0.306);
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var propagator = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60))
            .WithPerturbingBody(Stars.SUN_BODY)
            .IncludeAlbedo(earth)
            .Build();

        Assert.NotNull(propagator);
    }

    [Fact]
    public void PropagationWithAlbedoDiffersFromWithout()
    {
        var earth = CreateEarth(albedo: 0.306);
        var earthNoAlbedo = CreateEarth();
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromMinutes(90));
        var deltaT = TimeSpan.FromSeconds(60);

        // Without albedo
        var spc1 = CreateSpacecraft(earthNoAlbedo);
        var integrator1 = new RK78Integrator(1e-12, 1e-12, 60.0);
        var prop1 = new CentralBodyPropagatorBuilder(window, spc1, integrator1, deltaT)
            .WithPerturbingBody(Stars.SUN_BODY)
            .Build();
        prop1.Propagate();
        var finalNoAlbedo = spc1.GetEphemeris(window.EndDate, earthNoAlbedo, Frames.Frame.ICRF, Aberration.None).ToStateVector();

        // With albedo
        var spc2 = CreateSpacecraft(earth);
        var integrator2 = new RK78Integrator(1e-12, 1e-12, 60.0);
        var prop2 = new CentralBodyPropagatorBuilder(window, spc2, integrator2, deltaT)
            .WithPerturbingBody(Stars.SUN_BODY)
            .IncludeAlbedo(earth)
            .Build();
        prop2.Propagate();
        var finalWithAlbedo = spc2.GetEphemeris(window.EndDate, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();

        // There should be a measurable position difference (mm to cm scale for 1 orbit)
        var posDiff = (finalWithAlbedo.Position - finalNoAlbedo.Position).Magnitude();
        Assert.True(posDiff > 1e-6, $"Albedo should produce a measurable difference (got {posDiff:E3} m)");
        Assert.True(posDiff < 100.0, $"Albedo effect should be small for one orbit (got {posDiff:E3} m)");
    }

    [Fact]
    public void CommunityForcesWorkThroughBuilder()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var propagator = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60))
            .WithPerturbingBody(Stars.SUN_BODY)
            .WithPerturbingBody(new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB))
            .IncludeSolarRadiationPressure()
            .Build();

        Assert.NotNull(propagator);
        propagator.Propagate();
    }

    [Fact]
    public void BuildWithThermalProducesPropagator()
    {
        var earth = CreateEarth(thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
        var spc = CreateSpacecraft(earth);
        var integrator = new RK78Integrator(1e-10, 1e-10, 60.0);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var propagator = new CentralBodyPropagatorBuilder(window, spc, integrator, TimeSpan.FromSeconds(60))
            .WithPerturbingBody(Stars.SUN_BODY)
            .IncludeThermalRadiation(earth)
            .Build();

        Assert.NotNull(propagator);
    }

    [Fact]
    public void PropagationWithThermalDiffersFromWithout()
    {
        var earth = CreateEarth(thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
        var earthNoThermal = CreateEarth();
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromMinutes(90));
        var deltaT = TimeSpan.FromSeconds(60);

        // Without thermal
        var spc1 = CreateSpacecraft(earthNoThermal);
        var integrator1 = new RK78Integrator(1e-12, 1e-12, 60.0);
        var prop1 = new CentralBodyPropagatorBuilder(window, spc1, integrator1, deltaT)
            .WithPerturbingBody(Stars.SUN_BODY)
            .Build();
        prop1.Propagate();
        var finalNoThermal = spc1.GetEphemeris(window.EndDate, earthNoThermal, Frames.Frame.ICRF, Aberration.None).ToStateVector();

        // With thermal
        var spc2 = CreateSpacecraft(earth);
        var integrator2 = new RK78Integrator(1e-12, 1e-12, 60.0);
        var prop2 = new CentralBodyPropagatorBuilder(window, spc2, integrator2, deltaT)
            .WithPerturbingBody(Stars.SUN_BODY)
            .IncludeThermalRadiation(earth)
            .Build();
        prop2.Propagate();
        var finalWithThermal = spc2.GetEphemeris(window.EndDate, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();

        // There should be a measurable position difference
        var posDiff = (finalWithThermal.Position - finalNoThermal.Position).Magnitude();
        Assert.True(posDiff > 0.05, $"Thermal should produce a measurable difference (got {posDiff:E3} m)");
        Assert.True(posDiff < 0.07, $"Thermal effect should be small for one orbit (got {posDiff:E3} m)");
    }

    #endregion
}
