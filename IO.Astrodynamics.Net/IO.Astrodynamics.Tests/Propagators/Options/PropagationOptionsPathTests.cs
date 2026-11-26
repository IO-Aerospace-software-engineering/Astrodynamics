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
/// The options entry points of R06 of #668 (lot L4): each default equals its explicit settings (D4.7), the covariance
/// follows the Auto rule, albedo and thermal radiation need their coefficient, the resolved settings are on the solution
/// and repeat the run, and the atmosphere and geopotential options follow D1's fallback rule. The comparisons with the
/// flag entry points of 10.x were run while those existed (lot L4, step 1, bit for bit), and went with them in step 2
/// (R07 of #668, section 7 of the re-baseline).
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
        return new Spacecraft(id, $"OPT{-id}", 500.0, orbit, 3000.0, new Clock($"opt{-id}", 65536), 4.0, 2.2, null, 1.3);
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

    private static CelestialItem[] SunAndMoon() => new CelestialItem[] { Stars.SunBody, PlanetsAndMoons.MoonBody };

    [Fact]
    public void Default_EqualsItsExplicitSettings_Rk78AtItsDefaults_60s_CentralBodyAlone()
    {
        // D4.7: Propagate(window) is the run of new RK78Integrator(), the central body alone, no force, 60 s outputs
        var byDefault = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody));
        var expected = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody));

        var solution = byDefault.Propagate(OneHour);
        var reference = expected.Propagate(OneHour, new PropagationOptions { Integrator = RK78IntegratorOptions.Default, OutputStep = Output, ThirdBodies = Array.Empty<CelestialItem>(), CovariancePropagation = CovariancePropagationOptions.None });

        AssertSameStates(reference.StateVectors, solution.StateVectors);
        AssertSameStates(expected.PropagatedStates, byDefault.PropagatedStates);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AlbedoOrThermal_OnACentralBodyWithoutTheCoefficient_Throws(bool albedo)
    {
        // Section A of the re-baseline: a coefficient of 0, the value of every predefined body, throws
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody));
        var options = albedo
            ? PropagationOptions.Default with { AlbedoRadiationPressure = true }
            : PropagationOptions.Default with { ThermalRadiationPressure = true };
        Assert.Throws<ArgumentException>(() => spacecraft.Propagate(OneHour, options));
        Assert.False(spacecraft.IsPropagated);
    }

    [Fact]
    public void AutoCovariance_PropagatesACovarianceAtTheStart_AsTheExplicitMode_TrajectoryUnchanged()
    {
        // D4.3: under Auto, P0 at the window start is propagated in Covariance mode; the steps follow the state only, so
        // the trajectory is that of the run without covariance, bit for bit. Auto resolves to exactly the options a caller
        // passes to ask for the covariance, so its run and its cost are those of that request (decision 17 of lot L4 of
        // #668, decided by S. Guillet, 2026-11-22)
        var auto = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody, Covariance()));
        var explicitMode = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody, Covariance()));
        var none = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody, Covariance()));

        var solution = auto.Propagate(OneHour);
        var expected = explicitMode.Propagate(OneHour, PropagationOptions.Default with
            { CovariancePropagation = new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance } });
        var withoutCovariance = none.Propagate(OneHour, PropagationOptions.Default with
            { CovariancePropagation = CovariancePropagationOptions.None });

        Assert.Equal(expected.GetCovariance(OneHour.EndDate), solution.GetCovariance(OneHour.EndDate));
        Assert.Equal(new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance },
            solution.Options.CovariancePropagation);
        Assert.Equal(expected.Options, solution.Options);
        Assert.Equal(expected.StateVectors.Select(s => s.Covariance), solution.StateVectors.Select(s => s.Covariance));
        AssertSameStates(explicitMode.PropagatedStates, auto.PropagatedStates);
        AssertSameStates(none.PropagatedStates, auto.PropagatedStates);
        Assert.Same(CovariancePropagationOptions.None, withoutCovariance.Options.CovariancePropagation);
        Assert.Contains("CovariancePropagationOptions.None",
            Assert.Throws<InvalidOperationException>(() => withoutCovariance.GetCovariance(OneHour.EndDate)).Message);
    }

    [Fact]
    public void AutoCovariance_UnderVelocityVerlet_IsNotPropagated_AndGetCovarianceSaysWhy()
    {
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody, Covariance()));
        var solution = spacecraft.Propagate(OneHour, PropagationOptions.Default with { Integrator = VVIntegratorOptions.Default });

        Assert.Same(CovariancePropagationOptions.None, solution.Options.CovariancePropagation);
        var message = Assert.Throws<InvalidOperationException>(() => solution.GetCovariance(OneHour.EndDate)).Message;
        Assert.Contains("Velocity-Verlet", message);
    }

    [Fact]
    public void AutoCovariance_AtAnotherEpoch_IsNotPropagated_AndGetCovarianceSaysWhy()
    {
        var epoch = Start - TimeSpan.FromMinutes(10);
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody, Covariance(), epoch));
        var solution = spacecraft.Propagate(OneHour);

        Assert.Same(CovariancePropagationOptions.None, solution.Options.CovariancePropagation);
        var message = Assert.Throws<InvalidOperationException>(() => solution.GetCovariance(OneHour.EndDate)).Message;
        Assert.Contains("another epoch", message);
    }

    [Fact]
    public void AutoCovariance_Indefinite_Throws_NamingTheRemediationAndNone()
    {
        var indefinite = Matrix.Diagonal(100.0, 100.0, -100.0, 1e-2, 1e-2, 1e-2);
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody, indefinite));

        var message = Assert.Throws<InvalidOperationException>(() => spacecraft.Propagate(OneHour)).Message;
        Assert.Contains("WithRemediatedCovariance", message);
        Assert.Contains("CovariancePropagationOptions.None", message);
        Assert.False(spacecraft.IsPropagated);

        // None turns it off: the run goes
        spacecraft.Propagate(OneHour, PropagationOptions.Default with { CovariancePropagation = CovariancePropagationOptions.None });
        Assert.True(spacecraft.IsPropagated);
    }

    [Fact]
    public void SolutionOptions_AreTheResolvedSettings_AndRepeatTheRun()
    {
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody, Covariance()));
        var options = PropagationOptions.Default with
            { ThirdBodies = new CelestialItem[] { PlanetsAndMoons.EarthBody, Stars.SunBody }, AtmosphericDrag = true };

        var solution = spacecraft.Propagate(OneHour, options);
        var resolved = solution.Options;

        Assert.Same(options.Integrator, resolved.Integrator);
        Assert.Equal(options.OutputStep, resolved.OutputStep);
        Assert.Equal(new CelestialItem[] { Stars.SunBody }, resolved.ThirdBodies);
        Assert.True(resolved.AtmosphericDrag);
        Assert.Null(resolved.Geopotential);
        Assert.IsType<Nrlmsise00Model>(resolved.Atmosphere);
        Assert.Equal(CovariancePropagationMode.Covariance, resolved.CovariancePropagation.Mode);

        // Passed back, they repeat the run
        var again = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody, Covariance()));
        again.Propagate(OneHour, resolved);
        AssertSameStates(spacecraft.PropagatedStates, again.PropagatedStates);
    }

    [Fact]
    public void SolutionOptions_OfAConstructorThatTakesAnIntegrator_AreNull()
    {
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody));
        var integrator = new RK78Integrator();
        integrator.AddForce(new Propagator.Forces.GravitationalAcceleration(PlanetsAndMoons.EarthBody));
        var solution = new CentralBodyPropagator(OneHour, spacecraft, integrator, Output).Propagate();
        Assert.Null(solution.Options);
    }

    [Fact]
    public void CentralBodyPropagatorOptionsConstructor_EqualsSpacecraftPropagate()
    {
        var viaConstructor = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody));
        var viaSpacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody));
        var options = PropagationOptions.Default with { ThirdBodies = SunAndMoon() };

        new CentralBodyPropagator(OneHour, viaConstructor, options).Propagate();
        viaSpacecraft.Propagate(OneHour, options);

        AssertSameStates(viaSpacecraft.PropagatedStates, viaConstructor.PropagatedStates);
    }

    [Fact]
    public void AnInvalidSetting_LeavesThePreviousPropagation()
    {
        var spacecraft = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody));
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
        var earthWithModel = new CelestialBody(PlanetsAndMoons.Earth, Frames.Frame.ICRF, Start, atmosphericModel: standard);
        var viaOption = NewSpacecraft(Leo(PlanetsAndMoons.EarthBody));
        var reference = NewSpacecraft(Leo(earthWithModel));

        var solution = viaOption.Propagate(OneHour, PropagationOptions.Default with { AtmosphericDrag = true, Atmosphere = standard });
        reference.Propagate(OneHour, PropagationOptions.Default with { AtmosphericDrag = true });

        AssertSameStates(reference.PropagatedStates, viaOption.PropagatedStates);
        Assert.Same(standard, solution.Options.Atmosphere);
    }

    [Fact]
    public void AnAtmosphereOption_EqualToTheBodysModel_Runs_AnotherThrows()
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth, Frames.Frame.ICRF, Start,
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
