using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Options;

/// <summary>
/// The forces of <see cref="PropagationOptions"/> (R06 of #668): albedo and thermal radiation pressure of the central body,
/// drag and SRP, the central body taking part with its models whether it is listed or not (#674). The tests of the
/// builder of 10.x (removed in 11.0.0, R07 of #668), migrated to the options constructor with the same integrator and
/// forces.
/// </summary>
public class PropagationOptionsForceTests
{
    public PropagationOptionsForceTests()
    {
        TestKernels.EnsureSolarSystem();
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

    // RK7(8) at 1e-10 or 1e-12, a first step of 60 s, the settings of these tests
    private static IntegratorOptions Rk78(double tolerance) => new RK78IntegratorOptions
        { AbsoluteTolerance = tolerance, RelativeTolerance = tolerance, InitialStep = TimeSpan.FromSeconds(60.0) };

    private static PropagationOptions Options(double tolerance, params CelestialItem[] thirdBodies) => new()
    {
        Integrator = Rk78(tolerance), OutputStep = TimeSpan.FromSeconds(60), ThirdBodies = thirdBodies,
        CovariancePropagation = CovariancePropagationOptions.None
    };

    #region Construction and propagation

    [Fact]
    public void WithoutAlbedo_BuildsAPropagator()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var propagator = new CentralBodyPropagator(window, spc, Options(1e-10, Stars.SUN_BODY));

        Assert.NotNull(propagator);
    }

    [Fact]
    public void WithAlbedo_BuildsAPropagator()
    {
        var earth = CreateEarth(albedo: 0.306);
        var spc = CreateSpacecraft(earth);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var propagator = new CentralBodyPropagator(window, spc, Options(1e-10, Stars.SUN_BODY) with { AlbedoRadiationPressure = true });

        Assert.NotNull(propagator);
    }

    [Fact]
    public void TheCentralBody_TakesPart_SoThatDragApplies()
    {
        // Arrange: an Earth with an atmosphere, the observer of the spacecraft, not listed in the first run
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB, null,
            new Nrlmsise00Model(SpaceWeather.Nominal));
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromMinutes(90));

        CentralBodyPropagator Build(bool listEarth, bool drag)
        {
            var bodies = listEarth ? new CelestialItem[] { earth, Stars.SUN_BODY } : new CelestialItem[] { Stars.SUN_BODY };
            return new CentralBodyPropagator(window, CreateSpacecraft(earth), Options(1e-12, bodies) with { AtmosphericDrag = drag });
        }

        var unlisted = Build(false, true);
        var listed = Build(true, true);
        var withoutDrag = Build(false, false);

        // Act
        var unlistedStates = unlisted.Propagate().StateVectors;
        var listedStates = listed.Propagate().StateVectors;
        var withoutDragStates = withoutDrag.Propagate().StateVectors;

        // Assert: the drag force is there, the propagation is the one with the Earth listed, and drag acts
        Assert.Single(((Integrator)unlisted.Integrator).Forces.OfType<AtmosphericDrag>());
        Assert.Equal(listedStates.Select(s => (s.Position, s.Velocity)), unlistedStates.Select(s => (s.Position, s.Velocity)));
        Assert.NotEqual(withoutDragStates[^1].Position, unlistedStates[^1].Position);
    }

    [Fact]
    public void TheCentralBody_TakesPart_AsAnOccultingBodyOfSrp()
    {
        // Arrange: SRP with the Sun only listed, and with the Earth listed too; one orbit crosses the Earth's shadow
        var earth = CreateEarth();
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromMinutes(90));

        StateVector Final(bool listEarth)
        {
            var bodies = listEarth ? new CelestialItem[] { earth, Stars.SUN_BODY } : new CelestialItem[] { Stars.SUN_BODY };
            return new CentralBodyPropagator(window, CreateSpacecraft(earth),
                Options(1e-12, bodies) with { SolarRadiationPressure = true }).Propagate().StateVectors[^1];
        }

        // Act
        var unlisted = Final(false);
        var listed = Final(true);

        // Assert
        Assert.Equal(listed.Position, unlisted.Position);
        Assert.Equal(listed.Velocity, unlisted.Velocity);
    }

    [Fact]
    public void PropagationWithAlbedoDiffersFromWithout()
    {
        var earth = CreateEarth(albedo: 0.306);
        var earthNoAlbedo = CreateEarth();
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromMinutes(90));

        // Without albedo
        var spc1 = CreateSpacecraft(earthNoAlbedo);
        new CentralBodyPropagator(window, spc1, Options(1e-12, Stars.SUN_BODY)).Propagate();
        var finalNoAlbedo = spc1.GetEphemeris(window.EndDate, earthNoAlbedo, Frames.Frame.ICRF, Aberration.None);

        // With albedo
        var spc2 = CreateSpacecraft(earth);
        new CentralBodyPropagator(window, spc2, Options(1e-12, Stars.SUN_BODY) with { AlbedoRadiationPressure = true }).Propagate();
        var finalWithAlbedo = spc2.GetEphemeris(window.EndDate, earth, Frames.Frame.ICRF, Aberration.None);

        // There should be a measurable position difference (mm to cm scale for 1 orbit)
        var posDiff = (finalWithAlbedo.Position - finalNoAlbedo.Position).Magnitude();
        Assert.True(posDiff > 1e-6, $"Albedo should produce a measurable difference (got {posDiff:E3} m)");
        Assert.True(posDiff < 100.0, $"Albedo effect should be small for one orbit (got {posDiff:E3} m)");
    }

    [Fact]
    public void ThirdBodiesAndSrp_Propagate()
    {
        var earth = CreateEarth();
        var spc = CreateSpacecraft(earth);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var propagator = new CentralBodyPropagator(window, spc,
            Options(1e-10, Stars.SUN_BODY, new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB))
                with { SolarRadiationPressure = true });

        Assert.NotNull(propagator);
        propagator.Propagate();
    }

    [Fact]
    public void WithThermal_BuildsAPropagator()
    {
        var earth = CreateEarth(thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
        var spc = CreateSpacecraft(earth);
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        var propagator = new CentralBodyPropagator(window, spc, Options(1e-10, Stars.SUN_BODY) with { ThermalRadiationPressure = true });

        Assert.NotNull(propagator);
    }

    [Fact]
    public void PropagationWithThermalDiffersFromWithout()
    {
        var earth = CreateEarth(thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
        var earthNoThermal = CreateEarth();
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromMinutes(90));

        // Without thermal
        var spc1 = CreateSpacecraft(earthNoThermal);
        new CentralBodyPropagator(window, spc1, Options(1e-12, Stars.SUN_BODY)).Propagate();
        var finalNoThermal = spc1.GetEphemeris(window.EndDate, earthNoThermal, Frames.Frame.ICRF, Aberration.None);

        // With thermal
        var spc2 = CreateSpacecraft(earth);
        new CentralBodyPropagator(window, spc2, Options(1e-12, Stars.SUN_BODY) with { ThermalRadiationPressure = true }).Propagate();
        var finalWithThermal = spc2.GetEphemeris(window.EndDate, earth, Frames.Frame.ICRF, Aberration.None);

        // There should be a measurable position difference
        var posDiff = (finalWithThermal.Position - finalNoThermal.Position).Magnitude();
        Assert.True(posDiff > 0.05, $"Thermal should produce a measurable difference (got {posDiff:E3} m)");
        Assert.True(posDiff < 0.07, $"Thermal effect should be small for one orbit (got {posDiff:E3} m)");
    }

    #endregion

    #region The central body and its twins (#674)

    [Fact]
    public void ATwinOfTheObserverWithAnotherModel_Throws()
    {
        // Arrange: the orbit on a model-less Earth, an Earth with a geopotential given as a third body
        var earth = CreateEarth();
        var twin = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, TimeSystem.Time.J2000TDB,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new CentralBodyPropagator(window, CreateSpacecraft(earth), Options(1e-10, twin)));

        // Assert
        Assert.Contains("gravitational field", exception.Message);
    }

    [Fact]
    public void AValueEqualTwinOfTheObserver_IsTheRunWithoutIt()
    {
        // Arrange: a separately built Earth with the same models as the observer
        var earth = CreateEarth();
        var window = new Window(TimeSystem.Time.J2000TDB, TimeSystem.Time.J2000TDB + TimeSpan.FromHours(1));
        StateVector Run(params CelestialItem[] bodies)
        {
            var spc = CreateSpacecraft(earth);
            new CentralBodyPropagator(window, spc,
                Options(1e-10, bodies) with { AtmosphericDrag = true, SolarRadiationPressure = true }).Propagate();
            return spc.PropagatedStates[^1];
        }

        // Act
        var withoutIt = Run(Stars.SUN_BODY);
        var withIt = Run(CreateEarth(), Stars.SUN_BODY);

        // Assert
        Assert.Equal(withoutIt.Position, withIt.Position);
        Assert.Equal(withoutIt.Velocity, withIt.Velocity);
    }

    #endregion
}
