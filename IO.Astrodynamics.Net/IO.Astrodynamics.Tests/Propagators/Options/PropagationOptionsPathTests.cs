// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Options;

/// <summary>
/// The options entry points of R06 of #668 (lot L4): each run of <see cref="PropagationOptions"/> is bit for bit the run
/// of the constructor that takes the integrator, the bodies, the flags and the covariance options the settings name
/// (both integrators, fixed-step RK7(8), drag, SRP, albedo and thermal through the builder, the covariance modes,
/// maneuvers, SSB mode, a non-ICRF start), and each default equals its explicit settings (D4.7). The references are the
/// entry points of 10.x, whose results the conformance suite and the validation tests measure.
/// </summary>
public class PropagationOptionsPathTests
{
    private static readonly TimeSystem.Time Start = new(2021, 1, 1, 0, 0, 0, TimeScale.TDB);
    private static readonly Window OneHour = new(Start, Start + TimeSpan.FromHours(1));
    private static readonly TimeSpan Output = TimeSpan.FromSeconds(60);

    public PropagationOptionsPathTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static int _nextId = -1700100;

    private static int NextId() => System.Threading.Interlocked.Decrement(ref _nextId);

    internal static StateVector Leo(ILocalizable earth, Matrix? covariance = null, TimeSystem.Time? epoch = null) =>
        new(new Vector3(6778137.0, 0.0, 0.0), new Vector3(0.0, 5300.0, 5300.0), earth, epoch ?? Start, Frames.Frame.ICRF,
            covariance);

    internal static Spacecraft NewSpacecraft(IO.Astrodynamics.OrbitalParameters.OrbitalParameters orbit)
    {
        int id = NextId();
        return new Spacecraft(id, $"OPT{-id}", 500.0, 3000.0, new Clock($"opt{-id}", 65536), orbit, 4.0, 2.2, null, 1.3);
    }

    internal static Matrix Covariance() =>
        Matrix.Diagonal(100.0, 100.0, 100.0, 1e-2, 1e-2, 1e-2);

    internal static void AssertSameStates(IReadOnlyList<StateVector> expected, IReadOnlyList<StateVector> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].Position == actual[i].Position && expected[i].Velocity == actual[i].Velocity,
                $"state {i}: {expected[i].Position} != {actual[i].Position}, " +
                $"{(expected[i].Position - actual[i].Position).Magnitude():E3} m");
            Assert.True(expected[i].Epoch.EqualsExact(actual[i].Epoch), $"epoch {i}");
        }
    }

    private static CelestialItem[] SunAndMoon() => new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY };

    [Fact]
    public void Default_EqualsItsExplicitSettings_Rk78AtItsDefaults_60s_CentralBodyAlone()
    {
        // D4.7: Propagate(window) is the run of new RK78Integrator(), the central body alone, no force, 60 s outputs
        var byDefault = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY));
        var expected = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY));

        var solution = byDefault.Propagate(OneHour);
        var reference = expected.Propagate(OneHour, Array.Empty<CelestialItem>(), new RK78Integrator(), false, false,
            Output, null);

        AssertSameStates(reference.StateVectors, solution.StateVectors);
        AssertSameStates(expected.PropagatedStates, byDefault.PropagatedStates);
    }

    public static IEnumerable<object[]> ForceModels()
    {
        foreach (var integrator in new[] { "rk78", "rk78fixed", "vv" })
        {
            yield return new object[] { integrator, false, false };
            yield return new object[] { integrator, true, true };
        }
    }

    private static (IntegratorOptions Options, Func<Integrator> Instance) Integrators(string name) => name switch
    {
        "rk78" => (new RK78IntegratorOptions { AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-11, InitialStep = TimeSpan.FromSeconds(30) },
            () => new RK78Integrator(1e-10, 1e-11, 30.0)),
        "rk78fixed" => (new RK78IntegratorOptions { FixedStep = TimeSpan.FromSeconds(20) }, () => new RK78Integrator(20.0)),
        _ => (new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10) }, () => new VVIntegrator(TimeSpan.FromSeconds(10)))
    };

    [Theory]
    [MemberData(nameof(ForceModels))]
    public void Options_EqualTheConstructorOfTheirIntegratorBodiesAndFlags(string integrator, bool drag, bool srp)
    {
        var (integratorOptions, instance) = Integrators(integrator);
        var options = new PropagationOptions
        {
            Integrator = integratorOptions, OutputStep = Output, ThirdBodies = SunAndMoon(), AtmosphericDrag = drag,
            SolarRadiationPressure = srp
        };
        var viaOptions = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY));
        var reference = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY));

        viaOptions.Propagate(OneHour, options);
        reference.Propagate(OneHour, SunAndMoon(), instance(), drag, srp, Output);

        AssertSameStates(reference.PropagatedStates, viaOptions.PropagatedStates);
    }

    [Fact]
    public void AlbedoAndThermal_EqualTheBuilderOfTheCentralBody()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start, albedo: 0.3,
            thermalEffectiveTemperature: 254.0, thermalEmissivity: 0.95);
        var viaOptions = NewSpacecraft(Leo(earth));
        var reference = NewSpacecraft(Leo(earth));

        viaOptions.Propagate(OneHour, PropagationOptions.Default with
        {
            ThirdBodies = new CelestialItem[] { Stars.SUN_BODY }, SolarRadiationPressure = true,
            AlbedoRadiationPressure = true, ThermalRadiationPressure = true
        });
        new CentralBodyPropagatorBuilder(OneHour, reference, new RK78Integrator(), Output)
            .WithPerturbingBody(Stars.SUN_BODY).IncludeSolarRadiationPressure().IncludeAlbedo(earth)
            .IncludeThermalRadiation(earth).Build().Propagate();

        AssertSameStates(reference.PropagatedStates, viaOptions.PropagatedStates);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AlbedoOrThermal_OnACentralBodyWithoutTheCoefficient_Throws(bool albedo)
    {
        // Section A of the re-baseline: a coefficient of 0, the value of every predefined body, throws
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY));
        var options = albedo
            ? PropagationOptions.Default with { AlbedoRadiationPressure = true }
            : PropagationOptions.Default with { ThermalRadiationPressure = true };
        Assert.Throws<ArgumentException>(() => spacecraft.Propagate(OneHour, options));
        Assert.False(spacecraft.IsPropagated);
    }

    [Fact]
    public void ExplicitCovariance_EqualsTheConstructorOfTheCovarianceOptions()
    {
        var covariance = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance };
        var viaOptions = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY, Covariance()));
        var reference = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY, Covariance()));

        var solution = viaOptions.Propagate(OneHour, PropagationOptions.Default with { CovariancePropagation = covariance });
        var expected = reference.Propagate(OneHour, Array.Empty<CelestialItem>(), new RK78Integrator(), false, false, Output,
            covariance);

        AssertSameStates(reference.PropagatedStates, viaOptions.PropagatedStates);
        Assert.Equal(expected.GetCovariance(OneHour.EndDate), solution.GetCovariance(OneHour.EndDate));
    }

    [Fact]
    public void AutoCovariance_PropagatesACovarianceAtTheStart_AsTheExplicitMode_TrajectoryUnchanged()
    {
        // D4.3: under Auto, P0 at the window start is propagated in Covariance mode; the steps follow the state only, so
        // the trajectory is that of the run without covariance, bit for bit
        var auto = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY, Covariance()));
        var explicitMode = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY, Covariance()));
        var none = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY, Covariance()));

        var solution = auto.Propagate(OneHour);
        var expected = explicitMode.Propagate(OneHour, PropagationOptions.Default with
            { CovariancePropagation = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance } });
        var withoutCovariance = none.Propagate(OneHour, PropagationOptions.Default with
            { CovariancePropagation = CovariancePropagationOptions.None });

        Assert.Equal(expected.GetCovariance(OneHour.EndDate), solution.GetCovariance(OneHour.EndDate));
        Assert.Equal(CovariancePropagationMode.Covariance, solution.Options.CovariancePropagation.Mode);
        AssertSameStates(none.PropagatedStates, auto.PropagatedStates);
        Assert.Same(CovariancePropagationOptions.None, withoutCovariance.Options.CovariancePropagation);
        Assert.Contains("CovariancePropagationOptions.None",
            Assert.Throws<InvalidOperationException>(() => withoutCovariance.GetCovariance(OneHour.EndDate)).Message);
    }

    [Fact]
    public void AutoCovariance_UnderVelocityVerlet_IsNotPropagated_AndGetCovarianceSaysWhy()
    {
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY, Covariance()));
        var solution = spacecraft.Propagate(OneHour, PropagationOptions.Default with { Integrator = VVIntegratorOptions.Default });

        Assert.Same(CovariancePropagationOptions.None, solution.Options.CovariancePropagation);
        var message = Assert.Throws<InvalidOperationException>(() => solution.GetCovariance(OneHour.EndDate)).Message;
        Assert.Contains("Velocity-Verlet", message);
    }

    [Fact]
    public void AutoCovariance_AtAnotherEpoch_IsNotPropagated_AndGetCovarianceSaysWhy()
    {
        var epoch = Start - TimeSpan.FromMinutes(10);
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY, Covariance(), epoch));
        var solution = spacecraft.Propagate(OneHour);

        Assert.Same(CovariancePropagationOptions.None, solution.Options.CovariancePropagation);
        var message = Assert.Throws<InvalidOperationException>(() => solution.GetCovariance(OneHour.EndDate)).Message;
        Assert.Contains("another epoch", message);
    }

    [Fact]
    public void AutoCovariance_Indefinite_Throws_NamingTheRemediationAndNone()
    {
        var indefinite = Matrix.Diagonal(100.0, 100.0, -100.0, 1e-2, 1e-2, 1e-2);
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY, indefinite));

        var message = Assert.Throws<InvalidOperationException>(() => spacecraft.Propagate(OneHour)).Message;
        Assert.Contains("WithRemediatedCovariance", message);
        Assert.Contains("CovariancePropagationOptions.None", message);
        Assert.False(spacecraft.IsPropagated);

        // None turns it off: the run goes
        spacecraft.Propagate(OneHour, PropagationOptions.Default with { CovariancePropagation = CovariancePropagationOptions.None });
        Assert.True(spacecraft.IsPropagated);
    }

    [Fact]
    public void Maneuvers_EqualTheConstructorPath()
    {
        static (Spacecraft, Engine) WithEngine()
        {
            var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY));
            var tank = new FuelTank($"ft{spacecraft.NaifId}", "model", "sn", 1000.0, 1000.0);
            spacecraft.AddFuelTank(tank);
            var engine = new Engine($"eng{spacecraft.NaifId}", "model", "sn", 300.0, 10.0, tank);
            spacecraft.AddEngine(engine);
            spacecraft.SetStandbyManeuver(new ApogeeHeightManeuver(PlanetsAndMoons.EARTH_BODY, Start, TimeSpan.Zero,
                7378137.0, engine));
            return (spacecraft, engine);
        }

        var window = new Window(Start, Start + TimeSpan.FromHours(3));
        var (viaOptions, _) = WithEngine();
        var (reference, _) = WithEngine();

        viaOptions.Propagate(window, PropagationOptions.Default with { ThirdBodies = SunAndMoon() });
        reference.Propagate(window, SunAndMoon(), new RK78Integrator(), false, false, Output);

        Assert.NotEmpty(reference.ExecutedManeuvers);
        Assert.Equal(reference.ExecutedManeuvers.Count, viaOptions.ExecutedManeuvers.Count);
        AssertSameStates(reference.PropagatedStates, viaOptions.PropagatedStates);
    }

    [Fact]
    public void SsbMode_AndANonIcrfStart_EqualTheConstructorPath()
    {
        var earth = new CelestialBody(399);
        var geocentric = Leo(earth);
        var ssb = geocentric.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None).ToStateVector();
        var bodies = new CelestialItem[] { earth, new CelestialBody(301), new CelestialBody(10) };
        foreach (var start in new[] { ssb, geocentric.ToFrame(Frames.Frame.ECLIPTIC_J2000).ToStateVector() })
        {
            var viaOptions = NewSpacecraft(start);
            var reference = NewSpacecraft(start);
            var window = new Window(Start, Start + TimeSpan.FromMinutes(20));

            viaOptions.Propagate(window, PropagationOptions.Default with { ThirdBodies = bodies, SolarRadiationPressure = true });
            reference.Propagate(window, bodies, new RK78Integrator(), false, true, Output);

            AssertSameStates(reference.PropagatedStates, viaOptions.PropagatedStates);
        }
    }

    [Fact]
    public void SolutionOptions_AreTheResolvedSettings_AndRepeatTheRun()
    {
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY, Covariance()));
        var options = PropagationOptions.Default with
            { ThirdBodies = new CelestialItem[] { PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY }, AtmosphericDrag = true };

        var solution = spacecraft.Propagate(OneHour, options);
        var resolved = solution.Options;

        Assert.Same(options.Integrator, resolved.Integrator);
        Assert.Equal(options.OutputStep, resolved.OutputStep);
        Assert.Equal(new CelestialItem[] { Stars.SUN_BODY }, resolved.ThirdBodies);
        Assert.True(resolved.AtmosphericDrag);
        Assert.Null(resolved.Geopotential);
        Assert.IsType<Nrlmsise00Model>(resolved.Atmosphere);
        Assert.Equal(CovariancePropagationMode.Covariance, resolved.CovariancePropagation.Mode);

        // Passed back, they repeat the run
        var again = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY, Covariance()));
        again.Propagate(OneHour, resolved);
        AssertSameStates(spacecraft.PropagatedStates, again.PropagatedStates);
    }

    [Fact]
    public void SolutionOptions_OfAConstructorThatTakesAnIntegrator_AreNull()
    {
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY));
        var solution = spacecraft.Propagate(OneHour, Array.Empty<CelestialItem>(), new RK78Integrator(), false, false, Output);
        Assert.Null(solution.Options);
    }

    [Fact]
    public void CentralBodyPropagatorOptionsConstructor_EqualsSpacecraftPropagate()
    {
        var viaConstructor = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY));
        var viaSpacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY));
        var options = PropagationOptions.Default with { ThirdBodies = SunAndMoon() };

        new CentralBodyPropagator(OneHour, viaConstructor, options).Propagate();
        viaSpacecraft.Propagate(OneHour, options);

        AssertSameStates(viaSpacecraft.PropagatedStates, viaConstructor.PropagatedStates);
    }

    [Fact]
    public void AnInvalidSetting_LeavesThePreviousPropagation()
    {
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY));
        spacecraft.Propagate(OneHour);
        var before = spacecraft.PropagatedStates;

        Assert.Throws<ArgumentException>(() => spacecraft.Propagate(OneHour, PropagationOptions.Default with
            { Integrator = new RK78IntegratorOptions { FixedStep = TimeSpan.FromSeconds(10), MaxFactor = 4.0 } }));
        AssertSameStates(before, spacecraft.PropagatedStates);
    }

    [Fact]
    public void AnAtmosphereOption_FillsAModellessEarth_AsAnEarthBuiltWithIt()
    {
        // D1.6: a body without a model counts as not given (R12): the option replaces the NRLMSISE-00 of a model-less Earth
        var standard = new EarthStandardAtmosphere();
        var earthWithModel = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start, atmosphericModel: standard);
        var viaOption = NewSpacecraft(Leo(PlanetsAndMoons.EARTH_BODY));
        var reference = NewSpacecraft(Leo(earthWithModel));

        var solution = viaOption.Propagate(OneHour, PropagationOptions.Default with { AtmosphericDrag = true, Atmosphere = standard });
        reference.Propagate(OneHour, PropagationOptions.Default with { AtmosphericDrag = true });

        AssertSameStates(reference.PropagatedStates, viaOption.PropagatedStates);
        Assert.Same(standard, solution.Options.Atmosphere);
    }

    [Fact]
    public void AnAtmosphereOption_EqualToTheBodysModel_Runs_AnotherThrows()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
            atmosphericModel: new Nrlmsise00Model(SpaceWeather.Nominal));
        var equal = NewSpacecraft(Leo(earth));
        var reference = NewSpacecraft(Leo(earth));
        equal.Propagate(OneHour, PropagationOptions.Default with { AtmosphericDrag = true, Atmosphere = new Nrlmsise00Model(SpaceWeather.Nominal) });
        reference.Propagate(OneHour, PropagationOptions.Default with { AtmosphericDrag = true });
        AssertSameStates(reference.PropagatedStates, equal.PropagatedStates);

        var other = NewSpacecraft(Leo(earth));
        var exception = Assert.Throws<ArgumentException>(() => other.Propagate(OneHour, PropagationOptions.Default with
            { AtmosphericDrag = true, Atmosphere = new EarthStandardAtmosphere() }));
        Assert.Equal("options", exception.ParamName);
        Assert.Contains("Nrlmsise00Model", exception.Message);
        Assert.Contains("EarthStandardAtmosphere", exception.Message);
    }
}
