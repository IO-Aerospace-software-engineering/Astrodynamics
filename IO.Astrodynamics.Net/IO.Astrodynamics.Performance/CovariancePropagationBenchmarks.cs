// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using IO.Astrodynamics;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Performance;

/// <summary>Variational equations propagated with the state.</summary>
public enum VariationalMode
{
    /// <summary>The state alone.</summary>
    None,

    /// <summary>Φ and Ψ (<see cref="CovariancePropagationMode.StateTransitionOnly"/>).</summary>
    StateTransitionOnly,

    /// <summary>Φ, Ψ and P (<see cref="CovariancePropagationMode.Covariance"/>), with the coefficient of the case as a
    /// consider parameter.</summary>
    Covariance
}

/// <summary>
/// The cost of the covariance propagation (G1 of the specification of phase 2, feature 1): RK7(8) at 1e-11 over one day
/// on R2 (LEO 400 km, EGM2008 10×10, Sun, Moon, NRLMSISE-00 drag) and R4 (GEO, EGM2008 70×70, Sun, Moon, the barycentres
/// of Mercury to Neptune, SRP), the reference cases of the tests (<c>ReferenceCases</c>), without the variational
/// equations, with the state transition matrix only and with the covariance, outputs every hour and every minute.
/// Informative: no threshold.
/// </summary>
/// <remarks>
/// One cold propagation per iteration, the propagator built outside the measurement. The spacecraft of the reference
/// cases: 100 kg, 10 m², Cd 2.2, Cr 1.5; P0 with σ = 100 m and 0.1 m/s on each axis.
/// </remarks>
[MarkdownExporterAttribute.GitHub]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 10)]
public class CovariancePropagationBenchmarks
{
    internal static readonly Time Epoch = new(2021, 3, 20, 12, 0, 0);
    internal const string GeopotentialModel = "Data/SolarSystem/EGM2008_to70_TideFree";
    internal const double EarthEquatorialRadius = 6378137.0;
    internal const double Tolerance = 1e-11;

    private CelestialBody _earth = null!;
    private CelestialItem[] _bodies = null!;
    private StateVector _initialState = null!;
    private bool _drag;
    private bool _srp;
    private CentralBodyPropagator _propagator = null!;

    [Params("R2", "R4")] public string Case { get; set; } = "R2";

    [Params(VariationalMode.None, VariationalMode.StateTransitionOnly, VariationalMode.Covariance)]
    public VariationalMode Mode { get; set; }

    [Params(3600, 60)] public int OutputStepSeconds { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
        var sun = new CelestialBody(Stars.Sun);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, Epoch);
        if (Case == "R2")
        {
            _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Epoch,
                new GeopotentialModelParameters(GeopotentialModel, 10), new Nrlmsise00Model(SpaceWeather.Nominal));
            _bodies = new CelestialItem[] { _earth, sun, moon };
            _initialState = State(EarthEquatorialRadius + 400.0e3, 0.0005, 51.6, 20.0, 15.0, 0.0, _earth);
            _drag = true;
        }
        else
        {
            _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Epoch,
                new GeopotentialModelParameters(GeopotentialModel, 70));
            _bodies = new CelestialItem[]
            {
                _earth, sun, moon, Barycenters.MERCURY_BARYCENTER, Barycenters.VENUS_BARYCENTER,
                Barycenters.MARS_BARYCENTER, Barycenters.JUPITER_BARYCENTER, Barycenters.SATURN_BARYCENTER,
                Barycenters.URANUS_BARYCENTER, Barycenters.NEPTUNE_BARYCENTER
            };
            _initialState = State(42164137.0, 0.0002, 0.05, 0.0, 30.0, 0.0, _earth);
            _srp = true;
        }

        _initialState = WithCovariance(_initialState);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        var spacecraft = NewSpacecraft(-1101, _initialState);
        var builder = new CentralBodyPropagatorBuilder(new Window(Epoch, Epoch.AddDays(1.0)), spacecraft,
                new RK78Integrator(Tolerance, Tolerance), TimeSpan.FromSeconds(OutputStepSeconds))
            .WithPerturbingBodies(_bodies);
        if (_drag) builder.IncludeAtmosphericDrag();
        if (_srp) builder.IncludeSolarRadiationPressure();
        if (Mode != VariationalMode.None)
        {
            builder.WithCovariancePropagation(new CovariancePropagationOptions
            {
                Mode = Mode == VariationalMode.Covariance
                    ? CovariancePropagationMode.Covariance
                    : CovariancePropagationMode.StateTransitionOnly,
                ConsiderParameters = Mode == VariationalMode.Covariance
                    ? _drag ? new ConsiderParameters(dragCoefficientSigma: 0.1) : new ConsiderParameters(reflectivityCoefficientSigma: 0.1)
                    : null
            });
        }

        _propagator = builder.Build();
    }

    [Benchmark(Description = "RK7(8) one day")]
    public PropagationSolution Propagate() => _propagator.Propagate();

    /// <summary>The spacecraft of the reference cases: 100 kg, 10 m², Cd 2.2, Cr 1.5.</summary>
    internal static Spacecraft NewSpacecraft(int naifId, StateVector start) =>
        new(naifId, "BENCH" + -naifId, 100.0, 1000.0, new Clock("bench" + -naifId, 65536), start, 10.0, 2.2, null, 1.5);

    internal static StateVector State(double semiMajorAxis, double eccentricity, double inclination, double node,
        double argumentOfPerigee, double meanAnomaly, CelestialBody earth) =>
        new KeplerianElements(semiMajorAxis, eccentricity, inclination * Constants.Deg2Rad, node * Constants.Deg2Rad,
            argumentOfPerigee * Constants.Deg2Rad, meanAnomaly * Constants.Deg2Rad, earth, Epoch, Frame.ICRF).ToStateVector();

    // σ = 100 m and 0.1 m/s on each axis
    private static StateVector WithCovariance(StateVector state)
    {
        var p0 = new Matrix(6, 6);
        for (int i = 0; i < 3; i++)
        {
            p0.Set(i, i, 100.0 * 100.0);
            p0.Set(i + 3, i + 3, 0.1 * 0.1);
        }

        return new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame, p0);
    }
}

/// <summary>
/// The cost of the covariance propagated to TCA in <see cref="ConjunctionAssessment.Analyze(ProtectedSpacecraftProfile, ILocalizable, Window, ConjunctionAnalysisOptions)"/>:
/// two participants on a sun-synchronous orbit at 700 km in the force model of R2 (EGM2008 10×10, Sun, Moon,
/// NRLMSISE-00 drag) crossing 23 h after the start, each
/// propagated once over one day in RK7(8) at 1e-11 without the variational equations, with an RTN covariance at the start;
/// the analysis over 2 h around the crossing, with the covariance propagated to TCA (the default: the replay of the
/// variational equations along the steps of both participants) and held in RTN. The geometry of
/// <c>ConjunctionCovarianceCostTests</c>. Informative: no threshold.
/// </summary>
[MarkdownExporterAttribute.GitHub]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 10)]
public class ConjunctionCovarianceBenchmarks
{
    private ProtectedSpacecraftProfile _protected = null!;
    private Spacecraft _secondary = null!;
    private Window _window;

    [GlobalSetup]
    public void GlobalSetup()
    {
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data"));
        var epoch = CovariancePropagationBenchmarks.Epoch;
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
            new GeopotentialModelParameters(CovariancePropagationBenchmarks.GeopotentialModel, 10),
            new Nrlmsise00Model(SpaceWeather.Nominal));
        var bodies = new CelestialItem[]
            { earth, new CelestialBody(Stars.Sun), new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, epoch) };
        var tca = epoch.AddHours(23.0);
        var (protectedOrbit, secondaryOrbit) = Pair(earth, epoch, tca);
        var sigma = new[] { 100.0, 100.0, 100.0, 0.1, 0.1, 0.1 };
        var protectedCraft = CovariancePropagationBenchmarks.NewSpacecraft(-1201, WithRtnCovariance(protectedOrbit, sigma));
        _secondary = CovariancePropagationBenchmarks.NewSpacecraft(-1202, WithRtnCovariance(secondaryOrbit, sigma));
        var propagation = new Window(epoch, epoch.AddDays(1.0));
        foreach (var craft in new[] { protectedCraft, _secondary })
        {
            craft.Propagate(propagation, bodies, new RK78Integrator(CovariancePropagationBenchmarks.Tolerance,
                CovariancePropagationBenchmarks.Tolerance), true, false, TimeSpan.FromSeconds(60.0), null);
        }

        _protected = new ProtectedSpacecraftProfile(protectedCraft);
        _window = new Window(epoch.AddHours(22.0), epoch.AddHours(24.0));
    }

    [Benchmark(Baseline = true, Description = "Analyze, covariance held in RTN")]
    public EncounterCase HeldInRtn() => ConjunctionAssessment.Analyze(_protected, _secondary, _window,
        new ConjunctionAnalysisOptions { CovariancePropagation = null });

    [Benchmark(Description = "Analyze, covariance propagated to TCA")]
    public EncounterCase Propagated() => ConjunctionAssessment.Analyze(_protected, _secondary, _window);

    // The protected object, a LEO at 700 km inclined by 98.2°, and a secondary object passing, in the two-body problem,
    // 200 m from it at tca (120 m radial, 160 m normal) with its velocity turned by 70° about the radial direction: the
    // pair of ConjunctionCovarianceCase in the tests
    private static (StateVector Protected, StateVector Secondary) Pair(CelestialBody earth, Time start, Time tca)
    {
        var protectedOrbit = new KeplerianElements(7078137.0, 0.001, 98.2 * Constants.Deg2Rad, 0.4, 1.1, 0.3, earth,
            start, Frame.ICRF).ToStateVector();
        var protectedAtTca = protectedOrbit.ToStateVector(tca);
        var radial = protectedAtTca.Position.Normalize();
        var normal = protectedAtTca.Position.Cross(protectedAtTca.Velocity).Normalize();
        var position = protectedAtTca.Position + radial * 120.0 + normal * 160.0;
        double angle = 70.0 * Constants.Deg2Rad;
        var v = protectedAtTca.Velocity;
        var velocity = v * System.Math.Cos(angle) + radial.Cross(v) * System.Math.Sin(angle) +
                       radial * (radial * v) * (1.0 - System.Math.Cos(angle));
        return (protectedOrbit, new StateVector(position, velocity, earth, tca, Frame.ICRF).ToStateVector(start));
    }

    private static StateVector WithRtnCovariance(StateVector state, double[] sigma)
    {
        var rtn = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            rtn.Set(i, i, sigma[i] * sigma[i]);
        }

        rtn.Set(1, 3, -0.9 * sigma[1] * sigma[3]);
        rtn.Set(3, 1, -0.9 * sigma[1] * sigma[3]);
        return new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame,
            state.RotateCovarianceFromRtn(rtn));
    }
}
