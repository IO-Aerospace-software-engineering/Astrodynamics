// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Scenario = IO.Astrodynamics.Mission.Scenario;
using MissionOf = IO.Astrodynamics.Mission.Mission;
using Vector3 = IO.Astrodynamics.Math.Vector3;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The force model a propagation builds from its inputs (#674): drag about a central Earth without a model (#12), the
/// central body and its twins (#15), the integrator of a force-building path (#17) and the step (#22), on every entry
/// point and both integrators. The references are structural: a default equals its explicit setting, an unlisted central
/// body equals the run that lists it first, a value-equal twin equals the observer, a rejected input leaves the state.
/// </summary>
public class ForceModelDefectTests
{
    private readonly ITestOutputHelper _output;

    public ForceModelDefectTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    #region Helpers

    private static readonly TimeSystem.Time Epoch = new(2021, 1, 1, 12, 0, 0, TimeScale.TDB);
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);
    private static Window OneHour => new(Epoch, Epoch.AddHours(1.0));
    private static readonly CovariancePropagationOptions Stm = new() { Mode = CovariancePropagationMode.StateTransitionOnly };

    private static int _nextId = -7100;

    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private static IntegratorOptions NewRk78() =>
        new RK78IntegratorOptions { AbsoluteTolerance = 1e-10, RelativeTolerance = 1e-10 };

    // r = 6700 km, A = 10 m², m = 100 kg, Cd 2.2: drag of a few hundred metres per hour about the Earth
    private static Spacecraft NewSpacecraft(ILocalizable observer, Frames.Frame frame = null)
    {
        int id = NextId();
        return new Spacecraft(id, $"FM{-id}", 100.0, new StateVector(new Vector3(6700000.0, 0.0, 0.0), new Vector3(0.0, 7713.0, 1000.0), observer, Epoch,
                frame ?? Frames.Frame.ICRF), 10000.0,
            new Clock($"fmclk{-id}", 65536), 10.0, 2.2);
    }

    // The same orbit relative to the solar system barycenter: SSB mode, whose central body is the Sun
    private static Spacecraft NewSsbSpacecraft(CelestialItem earth)
    {
        var geocentric = new StateVector(new Vector3(6700000.0, 0.0, 0.0), new Vector3(0.0, 7713.0, 1000.0), earth,
            Epoch, Frames.Frame.ICRF);
        var ssbState = geocentric.RelativeTo(Barycenters.SolarSystemBarycenter, Aberration.None).ToStateVector();
        int id = NextId();
        return new Spacecraft(id, $"FM{-id}", 100.0, ssbState, 10000.0, new Clock($"fmclk{-id}", 65536), 10.0, 2.2);
    }

    private static Spacecraft NewMoonSpacecraft()
    {
        var moon = PlanetsAndMoons.MoonBody;
        double r = 1838000.0;
        int id = NextId();
        return new Spacecraft(id, $"FM{-id}", 100.0, new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, System.Math.Sqrt(moon.GM / r), 0.0), moon, Epoch,
                Frames.Frame.ICRF), 10000.0,
            new Clock($"fmclk{-id}", 65536), 10.0, 2.2);
    }

    private static CelestialBody EarthWith(IAtmosphericModel atmosphere = null,
        GeopotentialModelParameters geopotential = null) =>
        new(PlanetsAndMoons.Earth, geopotentialModelParameters: geopotential, atmosphericModel: atmosphere);

    private static GeopotentialModelParameters Egm2008(ushort degree) =>
        new(System.IO.Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree"), degree);

    /// <summary>An Earth whose GM differs from that of the kernels by 1e-3.</summary>
    private sealed class HeavierEarth : CelestialBody
    {
        internal HeavierEarth() : base(399, "EARTH", PlanetsAndMoons.EarthBody.Mass * 1.001,
            PlanetsAndMoons.EarthBody.PolarRadius, PlanetsAndMoons.EarthBody.EquatorialRadius)
        {
        }
    }

    /// <summary>A propagator derived by a user, through the protected constructor of <see cref="PropagatorBase"/>.</summary>
    private sealed class UserPropagator : PropagatorBase
    {
        internal UserPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT)
            : base(window, spacecraft, integrator, deltaT)
        {
        }

        protected override void StorePropagatedStates(StateVector[] outputStates)
        {
        }
    }

    public static readonly string[] AllPaths =
    {
        "RK78 constructor", "RK78 covariance constructor", "VV constructor",
        "Propagate", "Propagate RK78", "Propagate RK78 covariance", "Scenario", "Scenario RK78",
        "Scenario RK78 covariance", "Batch", "Batch RK78"
    };

    public static TheoryData<string> EveryPath()
    {
        var data = new TheoryData<string>();
        foreach (var path in AllPaths) data.Add(path);
        return data;
    }

    /// <summary>
    /// Propagates <paramref name="spacecraft"/> by <paramref name="path"/> and returns its last stored state; a batch task
    /// that failed rethrows its error.
    /// </summary>
    private static StateVector Run(string path, Spacecraft spacecraft, CelestialItem[] bodies, bool drag, bool srp,
        Window? window = null, TimeSpan? step = null)
    {
        var w = window ?? OneHour;
        var dt = step ?? Step;
        switch (path)
        {
            case "RK78 constructor":
            {
                var p = new CentralBodyPropagator(w, spacecraft, new PropagationOptions { Integrator = NewRk78(), OutputStep = dt, ThirdBodies = bodies, AtmosphericDrag = drag, SolarRadiationPressure = srp, CovariancePropagation = CovariancePropagationOptions.None });
                p.Propagate();
                break;
            }
            case "RK78 covariance constructor":
            {
                var p = new CentralBodyPropagator(w, spacecraft, new PropagationOptions { Integrator = NewRk78(), OutputStep = dt, ThirdBodies = bodies, AtmosphericDrag = drag, SolarRadiationPressure = srp, CovariancePropagation = Stm });
                p.Propagate();
                break;
            }
            case "VV constructor":
            {
                var p = new CentralBodyPropagator(w, spacecraft, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = dt }, OutputStep = dt, ThirdBodies = bodies, AtmosphericDrag = drag, SolarRadiationPressure = srp });
                p.Propagate();
                break;
            }
            case "Propagate":
                spacecraft.Propagate(w, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = dt }, OutputStep = dt, ThirdBodies = bodies, AtmosphericDrag = drag, SolarRadiationPressure = srp });
                break;
            case "Propagate RK78":
                spacecraft.Propagate(w, new PropagationOptions { Integrator = NewRk78(), OutputStep = dt, ThirdBodies = bodies, AtmosphericDrag = drag, SolarRadiationPressure = srp, CovariancePropagation = CovariancePropagationOptions.None });
                break;
            case "Propagate RK78 covariance":
                spacecraft.Propagate(w, new PropagationOptions { Integrator = NewRk78(), OutputStep = dt, ThirdBodies = bodies, AtmosphericDrag = drag, SolarRadiationPressure = srp, CovariancePropagation = Stm });
                break;
            case "Scenario":
            case "Scenario RK78":
            case "Scenario RK78 covariance":
            {
                var scenario = new Scenario("FM", new MissionOf("FM"), w);
                scenario.AddSpacecraft(spacecraft);
                foreach (var body in bodies ?? Array.Empty<CelestialItem>()) scenario.AddCelestialItem(body);
                scenario.Simulate(path switch
                {
                    "Scenario" => new PropagationOptions { Integrator = new VVIntegratorOptions { Step = dt }, OutputStep = dt, AtmosphericDrag = drag, SolarRadiationPressure = srp },
                    "Scenario RK78" => new PropagationOptions { Integrator = NewRk78(), OutputStep = dt, AtmosphericDrag = drag, SolarRadiationPressure = srp, CovariancePropagation = CovariancePropagationOptions.None },
                    _ => new PropagationOptions { Integrator = NewRk78(), OutputStep = dt, AtmosphericDrag = drag, SolarRadiationPressure = srp, CovariancePropagation = Stm }
                });
                break;
            }
            case "Batch":
            case "Batch RK78":
            {
                var result = BatchPropagator.Propagate(new[]
                {
                    new PropagationTask(spacecraft, w, new PropagationOptions { Integrator = path == "Batch" ? new VVIntegratorOptions { Step = dt } : NewRk78(), OutputStep = dt, ThirdBodies = bodies, AtmosphericDrag = drag, SolarRadiationPressure = srp, CovariancePropagation = CovariancePropagationOptions.None })
                });
                if (!result.Results[0].Succeeded) throw result.Results[0].Error;
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(path), path, null);
        }

        return spacecraft.PropagatedStates[^1];
    }

    private static void AssertSameState(StateVector expected, StateVector actual, string label)
    {
        Assert.True(expected.Epoch.EqualsExact(actual.Epoch), $"{label}: epoch {expected.Epoch} != {actual.Epoch}");
        Assert.True(expected.Position.X == actual.Position.X && expected.Position.Y == actual.Position.Y &&
                    expected.Position.Z == actual.Position.Z,
            $"{label}: position {expected.Position} != {actual.Position}, {(expected.Position - actual.Position).Magnitude():E3} m");
        Assert.True(expected.Velocity.X == actual.Velocity.X && expected.Velocity.Y == actual.Velocity.Y &&
                    expected.Velocity.Z == actual.Velocity.Z,
            $"{label}: velocity {expected.Velocity} != {actual.Velocity}");
    }

    private static void AssertSameStates(IReadOnlyList<StateVector> expected, IReadOnlyList<StateVector> actual,
        string label)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            AssertSameState(expected[i], actual[i], $"{label}[{i}]");
        }
    }

    #endregion

    #region #12 Drag about a central Earth without a model

    [Theory]
    [MemberData(nameof(EveryPath))]
    public void DragOnAModellessCentralEarth_IsNrlmsise00Nominal_OnEveryPath(string path)
    {
        // Arrange: the same orbit about EarthBody, about an Earth with NRLMSISE-00 at nominal space weather, and
        // without drag
        var explicitEarth = EarthWith(new Nrlmsise00Model(SpaceWeather.Nominal));
        var modelless = NewSpacecraft(PlanetsAndMoons.EarthBody);
        var configured = NewSpacecraft(explicitEarth);
        var withoutDrag = NewSpacecraft(PlanetsAndMoons.EarthBody);

        // Act
        var defaulted = Run(path, modelless,
            new CelestialItem[] { PlanetsAndMoons.EarthBody, Stars.SunBody, PlanetsAndMoons.MoonBody }, true, false);
        var expected = Run(path, configured,
            new CelestialItem[] { explicitEarth, Stars.SunBody, PlanetsAndMoons.MoonBody }, true, false);
        var noDrag = Run(path, withoutDrag,
            new CelestialItem[] { PlanetsAndMoons.EarthBody, Stars.SunBody, PlanetsAndMoons.MoonBody }, false, false);

        // Assert: the default is its explicit setting, bit for bit, and it is drag
        double dragEffect = (defaulted.Position - noDrag.Position).Magnitude();
        _output.WriteLine($"{path}: drag over 1 h {dragEffect:E3} m");
        Assert.True(dragEffect > 10.0, $"{path}: the drag of a model-less Earth moved the orbit by {dragEffect:E3} m");
        AssertSameState(expected, defaulted, path);
    }

    [Fact]
    public void DragOnAModellessEarth_InParallel_EqualsTheSerialRun()
    {
        // Arrange: four spacecraft about EarthBody, and four about Earths each with its own NRLMSISE-00
        var bodies = new CelestialItem[] { PlanetsAndMoons.EarthBody, Stars.SunBody, PlanetsAndMoons.MoonBody };
        var serial = Enumerable.Range(0, 4).Select(_ => NewSpacecraft(PlanetsAndMoons.EarthBody)).ToArray();
        var parallel = Enumerable.Range(0, 4).Select(_ => NewSpacecraft(PlanetsAndMoons.EarthBody)).ToArray();
        var ownModels = Enumerable.Range(0, 4).Select(_ => EarthWith(new Nrlmsise00Model())).ToArray();
        var configured = ownModels.Select(e => NewSpacecraft(e)).ToArray();
        var options = new BatchOptions { MaxDegreeOfParallelism = 4 };

        // Act
        var expected = serial.Select(sc => Run("Propagate RK78", sc, bodies, true, false)).ToArray();
        var watch = Stopwatch.StartNew();
        var result = BatchPropagator.Propagate(parallel
            .Select(sc => new PropagationTask(sc, OneHour, new PropagationOptions { Integrator = NewRk78(), OutputStep = Step, ThirdBodies = bodies, AtmosphericDrag = true, CovariancePropagation = CovariancePropagationOptions.None })).ToArray(), options);
        double defaultMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var reference = BatchPropagator.Propagate(configured.Select((sc, i) => new PropagationTask(sc, OneHour, new PropagationOptions { Integrator = NewRk78(), OutputStep = Step, ThirdBodies = new CelestialItem[] { ownModels[i], Stars.SunBody, PlanetsAndMoons.MoonBody }, AtmosphericDrag = true, CovariancePropagation = CovariancePropagationOptions.None })).ToArray(), options);
        double ownMs = watch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"MEASURE parallel batch of 4, 1 h with drag: default atmosphere {defaultMs:F0} ms, " +
                          $"one NRLMSISE-00 per Earth {ownMs:F0} ms");
        Assert.True(result.AllSucceeded, string.Join("; ", result.Results.Select(r => r.Error?.Message)));
        Assert.True(reference.AllSucceeded);
        for (int i = 0; i < 4; i++)
        {
            AssertSameState(expected[i], parallel[i].PropagatedStates[^1], $"task {i}");
            AssertSameState(configured[i].PropagatedStates[^1], parallel[i].PropagatedStates[^1], $"own model {i}");
        }
    }

    public static TheoryData<string> MoonPaths() => new()
    {
        "Propagate", "Propagate RK78", "VV constructor", "RK78 constructor", "Scenario RK78"
    };

    [Theory]
    [MemberData(nameof(MoonPaths))]
    public void DragAboutAModellessNonEarthCentralBody_Throws_BeforeAnyChange(string path)
    {
        // Arrange: a Moon-centred spacecraft propagated once, without drag
        var spacecraft = NewMoonSpacecraft();
        spacecraft.Propagate(OneHour, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = Step }, OutputStep = Step, ThirdBodies = new CelestialItem[] { PlanetsAndMoons.MoonBody } });
        var before = spacecraft.PropagatedStates;
        int maneuvers = spacecraft.ExecutedManeuvers.Count;
        var atmosphericEarth = EarthWith(new EarthStandardAtmosphere());

        foreach (var bodies in new[]
                 {
                     new CelestialItem[] { PlanetsAndMoons.MoonBody },
                     new CelestialItem[] { PlanetsAndMoons.MoonBody, atmosphericEarth }
                 })
        {
            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => Run(path, spacecraft, bodies, true, false));

            // Assert
            _output.WriteLine(exception.Message);
            Assert.Contains("MOON", exception.Message);
            AssertSameStates(before, spacecraft.PropagatedStates, path);
            Assert.Equal(maneuvers, spacecraft.ExecutedManeuvers.Count);
        }
    }

    public static TheoryData<string> ListedEarthPaths() => new()
    {
        "Propagate", "Propagate RK78", "VV constructor", "RK78 constructor", "Scenario",
        "Scenario RK78", "Batch", "Batch RK78"
    };

    [Theory]
    [MemberData(nameof(ListedEarthPaths))]
    public void DragInSsbMode_FromAListedModellessEarth_IsNrlmsise00Nominal(string path)
    {
        // Arrange: the orbit relative to the barycenter, its central body the Sun, EarthBody listed; the same orbit with
        // an Earth given NRLMSISE-00 at nominal space weather listed; and without drag (amendment of 2026-11-14: an Earth
        // always has at least the base atmosphere)
        var explicitEarth = EarthWith(new Nrlmsise00Model(SpaceWeather.Nominal));
        var window = new Window(Epoch, Epoch.AddMinutes(20.0));
        var step = TimeSpan.FromSeconds(10.0);
        var modelless = NewSsbSpacecraft(PlanetsAndMoons.EarthBody);
        var configured = NewSsbSpacecraft(explicitEarth);
        var withoutDrag = NewSsbSpacecraft(PlanetsAndMoons.EarthBody);

        // Act
        var defaulted = Run(path, modelless, new CelestialItem[] { PlanetsAndMoons.EarthBody, Stars.SunBody }, true,
            false, window, step);
        var expected = Run(path, configured, new CelestialItem[] { explicitEarth, Stars.SunBody }, true, false, window,
            step);
        var noDrag = Run(path, withoutDrag, new CelestialItem[] { PlanetsAndMoons.EarthBody, Stars.SunBody }, false,
            false, window, step);

        // Assert: the listed Earth's default is its explicit setting, bit for bit, and it is drag
        double effect = (defaulted.Position - noDrag.Position).Magnitude();
        _output.WriteLine($"{path}: SSB mode, drag of EarthBody over 20 min {effect:E3} m");
        Assert.True(effect > 1.0, $"{path}: the drag of a listed model-less Earth moved the orbit by {effect:E3} m");
        AssertSameState(expected, defaulted, path);
    }

    [Theory]
    [MemberData(nameof(ListedEarthPaths))]
    public void DragFromAListedModellessNonCentralEarth_IsNrlmsise00Nominal(string path)
    {
        // Arrange: a low Earth orbit propagated about the Moon, given an atmospheric model (US 1976, zero at the Earth's
        // distance), EarthBody listed as a third body; the same with an Earth given NRLMSISE-00 at nominal space weather
        // listed; and without drag
        var moon = new CelestialBody(PlanetsAndMoons.Moon, atmosphericModel: new EarthStandardAtmosphere());
        var explicitEarth = EarthWith(new Nrlmsise00Model(SpaceWeather.Nominal));

        Spacecraft AboutMoon()
        {
            var geocentric = new StateVector(new Vector3(6700000.0, 0.0, 0.0), new Vector3(0.0, 7713.0, 1000.0),
                PlanetsAndMoons.EarthBody, Epoch, Frames.Frame.ICRF);
            int id = NextId();
            return new Spacecraft(id, $"FM{-id}", 100.0, geocentric.RelativeTo(moon, Aberration.None).ToStateVector(), 10000.0,
                new Clock($"fmclk{-id}", 65536), 10.0, 2.2);
        }

        var window = new Window(Epoch, Epoch.AddMinutes(20.0));
        var step = TimeSpan.FromSeconds(10.0);

        // Act
        var defaulted = Run(path, AboutMoon(),
            new CelestialItem[] { moon, PlanetsAndMoons.EarthBody, Stars.SunBody }, true, false, window, step);
        var expected = Run(path, AboutMoon(), new CelestialItem[] { moon, explicitEarth, Stars.SunBody }, true, false,
            window, step);
        var noDrag = Run(path, AboutMoon(), new CelestialItem[] { moon, PlanetsAndMoons.EarthBody, Stars.SunBody },
            false, false, window, step);

        // Assert: the listed Earth adds its default drag, bit for bit its explicit setting
        double effect = (defaulted.Position - noDrag.Position).Magnitude();
        _output.WriteLine($"{path}: about the Moon, drag of EarthBody over 20 min {effect:E3} m");
        Assert.True(effect > 1.0, $"{path}: the drag of a listed model-less Earth moved the orbit by {effect:E3} m");
        AssertSameState(expected, defaulted, path);
    }

    [Fact]
    public void DragInSsbMode_ListedAtmospheres()
    {
        // Arrange: the orbit relative to the barycenter, its central body the Sun; an Earth with US 1976 listed, then the
        // Moon alone, a body other than the Earth without a model
        var earth = EarthWith(new EarthStandardAtmosphere());
        var window = new Window(Epoch, Epoch.AddMinutes(20.0));
        var step = TimeSpan.FromSeconds(10.0);
        var withDrag = NewSsbSpacecraft(earth);
        var withoutDrag = NewSsbSpacecraft(earth);
        var moonOnly = NewSsbSpacecraft(earth);

        // Act
        var dragged = Run("Propagate", withDrag, new CelestialItem[] { earth, Stars.SunBody }, true, false, window, step);
        var free = Run("Propagate", withoutDrag, new CelestialItem[] { earth, Stars.SunBody }, false, false, window, step);
        var exception = Assert.Throws<InvalidOperationException>(() => Run("Propagate", moonOnly,
            new CelestialItem[] { PlanetsAndMoons.MoonBody, Stars.SunBody }, true, false, window, step));

        // Assert: the drag of the listed atmosphere applies; with no listed body that has an atmosphere, the request throws
        double effect = (dragged.Position - free.Position).Magnitude();
        _output.WriteLine($"SSB mode, drag over 20 min {effect:E3} m; {exception.Message}");
        Assert.True(effect > 0.1, $"{effect:E3} m");
        Assert.Empty(moonOnly.PropagatedStates);
    }

    [Fact]
    public void CdmExport_DragAboutAModellessEarth_NamesNrlmsise00()
    {
        // Arrange
        var spacecraft = NewSpacecraft(PlanetsAndMoons.EarthBody);

        // Act
        var solution = spacecraft.Propagate(OneHour, new PropagationOptions { Integrator = NewRk78(), OutputStep = Step, ThirdBodies = new CelestialItem[] { PlanetsAndMoons.EarthBody }, AtmosphericDrag = true, CovariancePropagation = Stm });

        // Assert: the keyword names the model the force used (CCSDS 508.0-B-1, ATMOSPHERIC_MODEL)
        Assert.Equal("NRLMSISE-00", CdmForceModel.From(solution.Dynamics).AtmosphericModel);
    }

    [Fact]
    public void AtmosphericDrag_OnAModellessEarth_EqualsNominalNrlmsise()
    {
        // Arrange
        var explicitEarth = EarthWith(new Nrlmsise00Model(SpaceWeather.Nominal));
        var spacecraft = NewSpacecraft(PlanetsAndMoons.EarthBody);
        var context = new ForceEvaluationContext(100.0, 2.2, 1.0);

        // Act
        var defaulted = new AtmosphericDrag(spacecraft, PlanetsAndMoons.EarthBody);
        var configured = new AtmosphericDrag(spacecraft, explicitEarth);

        // Assert: at 200 to 800 km, the acceleration and its state partials bit for bit
        foreach (double altitude in new[] { 200e3, 350e3, 500e3, 650e3, 800e3 })
        {
            double r = 6378137.0 + altitude;
            var position = new Vector3(r * 0.6, r * 0.64, r * 0.48);
            var velocity = new Vector3(-5000.0, 5500.0, 1000.0);
            var a = defaulted.Apply(new StateVector(position, velocity, PlanetsAndMoons.EarthBody, Epoch, Frames.Frame.ICRF));
            var b = configured.Apply(new StateVector(position, velocity, explicitEarth, Epoch, Frames.Frame.ICRF));
            Assert.True(a == b, $"{altitude / 1000.0} km: {a} != {b}");
            Assert.True(a.Magnitude() > 0.0);

            var dadrA = new double[9];
            var dadvA = new double[9];
            var dadrB = new double[9];
            var dadvB = new double[9];
            defaulted.AccumulateStatePartials(
                new StateVector(position, velocity, PlanetsAndMoons.EarthBody, Epoch, Frames.Frame.ICRF), context, dadrA, dadvA);
            configured.AccumulateStatePartials(new StateVector(position, velocity, explicitEarth, Epoch, Frames.Frame.ICRF),
                context, dadrB, dadvB);
            Assert.Equal(dadrB, dadrA);
            Assert.Equal(dadvB, dadvA);
        }

        // A body without an atmosphere still has no drag
        Assert.Throws<ArgumentException>(() => new AtmosphericDrag(spacecraft, PlanetsAndMoons.MoonBody));
    }

    #endregion

    #region #15 The central body and its twins

    public static TheoryData<string, string, string, string> UnlistedCases()
    {
        var data = new TheoryData<string, string, string, string>();
        foreach (var path in new[] { "RK78 constructor", "VV constructor", "Propagate", "Propagate RK78", "Batch", "Scenario" })
        {
            foreach (var forces in new[] { "drag+SRP", "SRP", "none" })
            {
                data.Add(path, forces, "ICRF", "Sun, Moon");
            }
        }

        data.Add("RK78 constructor", "drag+SRP", "ECLIPJ2000", "Sun, Moon");
        data.Add("VV constructor", "drag+SRP", "ECLIPJ2000", "Sun, Moon");
        data.Add("RK78 constructor", "drag+SRP", "ICRF", "empty");
        data.Add("VV constructor", "drag+SRP", "ICRF", "empty");
        data.Add("RK78 constructor", "none", "ICRF", "null");
        data.Add("Propagate", "drag+SRP", "ICRF", "null");
        data.Add("Propagate", "drag+SRP", "SSB", "Earth, Moon");
        data.Add("VV constructor", "SRP", "SSB", "Earth, Moon");
        data.Add("Batch", "none", "SSB", "Earth, Moon");
        return data;
    }

    [Theory]
    [MemberData(nameof(UnlistedCases))]
    public void UnlistedCentralBody_TakesPart_LikeListedFirst_OnEveryPath(string path, string forces, string frame,
        string list)
    {
        // Arrange: an Earth with US 1976, the observer, listed first or not listed
        var earth = new CelestialBody(PlanetsAndMoons.Earth, Frames.Frame.ICRF, Epoch, atmosphericModel: new EarthStandardAtmosphere());
        bool drag = forces.StartsWith("drag");
        bool srp = forces.Contains("SRP");
        bool ssb = frame == "SSB";
        Spacecraft NewOne() => ssb ? NewSsbSpacecraft(earth) : NewSpacecraft(earth, frame == "ECLIPJ2000" ? Frames.Frame.ECLIPJ2000 : null);
        CelestialItem central = ssb ? Stars.SunBody : earth;
        CelestialItem[] unlisted = list switch
        {
            "empty" => Array.Empty<CelestialItem>(),
            "null" => null,
            "Earth, Moon" => new CelestialItem[] { earth, PlanetsAndMoons.MoonBody },
            _ => new CelestialItem[] { Stars.SunBody, PlanetsAndMoons.MoonBody }
        };
        var listed = new[] { central }.Concat(unlisted ?? Array.Empty<CelestialItem>()).ToArray();
        Window? window = ssb ? new Window(Epoch, Epoch.AddMinutes(20.0)) : null;
        TimeSpan? step = ssb ? TimeSpan.FromSeconds(10.0) : null;

        // Act
        var expected = Run(path, NewOne(), listed, drag, srp, window, step);
        var actual = Run(path, NewOne(), unlisted, drag, srp, window, step);

        // Assert
        AssertSameState(expected, actual, $"{path}, {forces}, {frame}, [{list}]");
    }

    public static TheoryData<string, string> TwinCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var path in new[] { "RK78 constructor", "Propagate", "Propagate RK78", "Batch", "Scenario RK78" })
        {
            foreach (var twin in new[] { "geopotential", "NRLMSISE-00", "space weather", "switch", "GM", "two listed" })
            {
                data.Add(path, twin);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TwinCases))]
    public void ListedTwinWithAnotherModel_Throws_OnEveryPath(string path, string twin)
    {
        // Arrange: the orbit on EarthBody, propagated once; a listed Earth with another model
        var spacecraft = NewSpacecraft(PlanetsAndMoons.EarthBody);
        spacecraft.Propagate(OneHour, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = Step }, OutputStep = Step, ThirdBodies = new CelestialItem[] { PlanetsAndMoons.EarthBody } });
        var before = spacecraft.PropagatedStates;
        var flags = NrlmsiseFlags.CreateStandard();
        flags.Switches[9] = 0;
        CelestialItem[] bodies = twin switch
        {
            "geopotential" => new CelestialItem[] { EarthWith(geopotential: Egm2008(10)), Stars.SunBody },
            "NRLMSISE-00" => new CelestialItem[] { EarthWith(new Nrlmsise00Model()), Stars.SunBody },
            "space weather" => new CelestialItem[]
            {
                EarthWith(new Nrlmsise00Model(new SpaceWeather { F107 = 250.0, F107A = 250.0, Ap = 100.0 })), Stars.SunBody
            },
            "switch" => new CelestialItem[] { EarthWith(new Nrlmsise00Model(flags, SpaceWeather.Nominal)), Stars.SunBody },
            "GM" => new CelestialItem[] { new HeavierEarth(), Stars.SunBody },
            _ => new CelestialItem[] { new CelestialBody(PlanetsAndMoons.Earth), EarthWith(geopotential: Egm2008(4)) }
        };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => Run(path, spacecraft, bodies, true, false));

        // Assert: the message names the bodies, the model that differs and the fix; the spacecraft keeps its states
        _output.WriteLine(exception.Message);
        Assert.Contains("EARTH", exception.Message);
        Assert.Contains(twin switch
        {
            "geopotential" or "two listed" => "gravitational field",
            "GM" => "GM",
            _ => "atmospheric model"
        }, exception.Message);
        AssertSameStates(before, spacecraft.PropagatedStates, path);
    }

    [Fact]
    public void Scenario_AddCelestialItem_OfATwinWithAnotherModel_Throws()
    {
        // Arrange
        var scenario = new Scenario("FM", new MissionOf("FM"), OneHour);
        scenario.AddCelestialItem(PlanetsAndMoons.EarthBody);
        var twin = EarthWith(new EarthStandardAtmosphere(), Egm2008(10));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => scenario.AddCelestialItem(twin));

        // Assert: the first stays; a value-equal twin is ignored, as before
        _output.WriteLine(exception.Message);
        Assert.Equal("celestialItem", exception.ParamName);
        Assert.Contains("EARTH", exception.Message);
        Assert.Same(PlanetsAndMoons.EarthBody, Assert.Single(scenario.CelestialBodies));
        scenario.AddCelestialItem(new CelestialBody(PlanetsAndMoons.Earth));
        Assert.Same(PlanetsAndMoons.EarthBody, Assert.Single(scenario.CelestialBodies));
    }

    public static TheoryData<string, string> EqualTwinCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var path in new[] { "RK78 constructor", "Propagate" })
        {
            foreach (var twin in new[] { "EGM2008 + US 1976", "NRLMSISE-00 nominal", "model-free", "SSB Sun" })
            {
                data.Add(path, twin);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EqualTwinCases))]
    public void ListedTwinWithEqualModels_IsTheObserver_BitForBit(string path, string twin)
    {
        // Arrange: an observer and a separately built twin with value-equal models
        (CelestialItem Observer, CelestialItem Twin) pair = twin switch
        {
            "EGM2008 + US 1976" => (EarthWith(new EarthStandardAtmosphere(), Egm2008(6)),
                EarthWith(new EarthStandardAtmosphere(), Egm2008(6))),
            "NRLMSISE-00 nominal" => (EarthWith(new Nrlmsise00Model(SpaceWeather.Nominal)),
                EarthWith(new Nrlmsise00Model(SpaceWeather.Nominal))),
            "model-free" => (TestHelpers.EarthAtJ2000, TestHelpers.EarthAtJ2000),
            _ => (Stars.SunBody, new CelestialBody(Stars.Sun))
        };
        bool ssb = twin == "SSB Sun";
        var atmosphericEarth = EarthWith(new EarthStandardAtmosphere());
        Spacecraft NewOne() => ssb ? NewSsbSpacecraft(atmosphericEarth) : NewSpacecraft(pair.Observer);
        CelestialItem[] Bodies(CelestialItem central) => ssb
            ? new[] { central, atmosphericEarth }
            : new[] { central, Stars.SunBody };
        Window? window = ssb ? new Window(Epoch, Epoch.AddMinutes(20.0)) : null;
        TimeSpan? step = ssb ? TimeSpan.FromSeconds(10.0) : null;
        string runPath = ssb ? "Propagate" : path;

        // Act
        var expected = Run(runPath, NewOne(), Bodies(pair.Observer), true, true, window, step);
        var actual = Run(runPath, NewOne(), Bodies(pair.Twin), true, true, window, step);

        // Assert
        AssertSameState(expected, actual, $"{path}, {twin}");
    }

    #endregion

    #region #17 An integrator given its forces by the caller

    // The force-building paths that took the caller's integrator were removed in 11.0.0 (R07 of #668): each options entry
    // point builds a new integrator per run, so the reuse of #17 cannot happen; the constructor of an integrator that holds
    // its forces is unaffected

    [Fact]
    public void UserBuiltForceList_IIntegratorConstructor_Unaffected()
    {
        // Arrange: an integrator given its forces by the caller, and the force-building constructor
        var earth = PlanetsAndMoons.EarthBody;
        var user = new RK78Integrator(1e-10, 1e-10);
        user.AddForce(new GravitationalAcceleration(earth));
        var userSpacecraft = NewSpacecraft(earth);
        var builtSpacecraft = NewSpacecraft(earth);
        // The constructor without forces uses the integrator's forces as given and initializes it with its initial state,
        // at construction and at the start of every run (#782): an Initialize by the caller, as here, has no effect
        user.Initialize((StateVector)userSpacecraft.InitialOrbitalParameters);

        // Act
        new CentralBodyPropagator(OneHour, userSpacecraft, user, Step).Propagate();

        Run("RK78 constructor", builtSpacecraft, new CelestialItem[] { earth }, false, false);

        // Assert
        Assert.Single(user.Forces);
        AssertSameState(builtSpacecraft.PropagatedStates[^1], userSpacecraft.PropagatedStates[^1], "user force list");
    }

    #endregion

    #region #22 A step <= 0

    public static TheoryData<string, long> StepCases()
    {
        var data = new TheoryData<string, long>();
        foreach (var entry in new[]
                 {
                     "Propagate", "Propagate integrator", "Propagate covariance", "constructor",
                     "covariance constructor", "IIntegrator constructor", "IIntegrator covariance constructor",
                     "Scenario", "Scenario factory", "Scenario covariance", "Batch", "Monte Carlo", "PropagatorBase"
                 })
        {
            foreach (long ticks in new[] { 0L, TimeSpan.FromSeconds(-60.0).Ticks, -1L })
            {
                data.Add(entry, ticks);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(StepCases))]
    public void NonPositiveStep_Throws_OnEveryPath(string entry, long ticks)
    {
        // Arrange: a spacecraft propagated once
        var step = TimeSpan.FromTicks(ticks);
        var bodies = new CelestialItem[] { PlanetsAndMoons.EarthBody };
        var window = new Window(Epoch, Epoch.AddMinutes(30.0));
        var spacecraft = NewSpacecraft(PlanetsAndMoons.EarthBody);
        spacecraft.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = Step }, OutputStep = Step, ThirdBodies = bodies });
        var before = spacecraft.PropagatedStates;
        var integratorOptions = NewRk78();
        var integrator = new RK78Integrator(1e-10, 1e-10);
        var star = new Star(2, "star2", 1E+30, "spec", 2, 0.3792, Equatorial.FromRightAscensionDeclination(1, 1, Epoch), 0.1, 0.1, 0, 0, 0, 0, Epoch);
        // The options entry points refuse the step when their settings are built (R06 and R07 of #668): the Velocity-Verlet
        // step first, then the output step
        string expectedName = entry switch
        {
            "Propagate" or "Scenario" or "Batch" => "Step",
            "IIntegrator constructor" or "IIntegrator covariance constructor" or "PropagatorBase" => "deltaT",
            _ => "OutputStep"
        };

        Scenario NewScenario()
        {
            var scenario = new Scenario("FM", new MissionOf("FM"), window);
            scenario.AddSpacecraft(spacecraft);
            scenario.AddCelestialItem(PlanetsAndMoons.EarthBody);
            scenario.AddStar(star);
            return scenario;
        }

        void Act()
        {
            switch (entry)
            {
                case "Propagate": spacecraft.Propagate(window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = step }, OutputStep = step, ThirdBodies = bodies }); break;
                case "Propagate integrator": spacecraft.Propagate(window, new PropagationOptions { Integrator = integratorOptions, OutputStep = step, ThirdBodies = bodies, CovariancePropagation = CovariancePropagationOptions.None }); break;
                case "Propagate covariance": spacecraft.Propagate(window, new PropagationOptions { Integrator = integratorOptions, OutputStep = step, ThirdBodies = bodies, CovariancePropagation = Stm }); break;
                case "constructor": new CentralBodyPropagator(window, spacecraft, new PropagationOptions { Integrator = integratorOptions, OutputStep = step, ThirdBodies = bodies, CovariancePropagation = CovariancePropagationOptions.None }).Propagate(); break;
                case "covariance constructor": new CentralBodyPropagator(window, spacecraft, new PropagationOptions { Integrator = integratorOptions, OutputStep = step, ThirdBodies = bodies, CovariancePropagation = Stm }).Propagate(); break;
                case "IIntegrator constructor": new CentralBodyPropagator(window, spacecraft, integrator, step).Propagate(); break;
                case "IIntegrator covariance constructor": new CentralBodyPropagator(window, spacecraft, integrator, step, Stm).Propagate(); break;
                case "Scenario": NewScenario().Simulate(new PropagationOptions { Integrator = new VVIntegratorOptions { Step = step }, OutputStep = step }); break;
                case "Scenario factory": NewScenario().Simulate(new PropagationOptions { Integrator = NewRk78(), OutputStep = step, CovariancePropagation = CovariancePropagationOptions.None }); break;
                case "Scenario covariance": NewScenario().Simulate(new PropagationOptions { Integrator = NewRk78(), OutputStep = step, CovariancePropagation = Stm }); break;
                case "Batch":
                {
                    var result = BatchPropagator.Propagate(new[] { new PropagationTask(spacecraft, window, new PropagationOptions { Integrator = new VVIntegratorOptions { Step = step }, OutputStep = step, ThirdBodies = bodies }) });
                    Assert.False(result.Results[0].Succeeded, "the task with a step <= 0 succeeded");
                    throw result.Results[0].Error;
                }
                case "Monte Carlo":
                {
                    var covTerms = new double[6, 6];
                    for (int i = 0; i < 6; i++) covTerms[i, i] = i < 3 ? 100.0 : 0.01;
                    var cov = new Matrix(covTerms);
                    var template = NewSpacecraft(PlanetsAndMoons.EarthBody);
                    var state = (StateVector)template.InitialOrbitalParameters;
                    int id = NextId();
                    var withCovariance = new Spacecraft(id, $"FM{-id}", 100.0, new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame, cov), 10000.0,
                        new Clock($"fmclk{-id}", 256));
                    MonteCarloPropagator.Propagate(new MonteCarloConfiguration
                    {
                        RunCount = 3, Seed = 1, TemplateSpacecraft = withCovariance, Window = window,
                        Propagation = new PropagationOptions { Integrator = integratorOptions, OutputStep = step, ThirdBodies = bodies }
                    });
                    break;
                }
                case "PropagatorBase": _ = new UserPropagator(window, spacecraft, integrator, step); break;
            }
        }

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(Act);

        // Assert: the step named as the caller passed it, before anything changed
        _output.WriteLine($"{entry}: {exception.Message}");
        Assert.Equal(expectedName, exception.ParamName);
        AssertSameStates(before, spacecraft.PropagatedStates, entry);
        var starStates = (System.Collections.ICollection)typeof(Star)
            .GetField("_propagatedStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(star);
        Assert.Empty(starStates!);
    }

    [Fact]
    public void VVIntegrator_NonPositiveStep_Throws()
    {
        var state = new StateVector(new Vector3(6700000.0, 0, 0), new Vector3(0, 7713.0, 0), PlanetsAndMoons.EarthBody,
            Epoch, Frames.Frame.ICRF);
        Assert.Equal("deltaT", Assert.Throws<ArgumentOutOfRangeException>(() => new VVIntegrator(TimeSpan.Zero)).ParamName);
        Assert.Equal("deltaT", Assert.Throws<ArgumentOutOfRangeException>(() => new VVIntegrator(
            new ForceBase[] { new GravitationalAcceleration(PlanetsAndMoons.EarthBody) }, TimeSpan.FromSeconds(-1.0),
            state)).ParamName);
    }

    #endregion

    #region A central body without GM (R03 of #668)

    // ELARA (507): named by SPICE, its orientation in pck00011.tpc, no BODY507_GM in gm_de440.tpc (KernelTextValues), so the
    // kernel GM of the body is 0. Before 11.0.0 a propagation about it ran without central gravity, silently: a straight line.
    // It is refused at the start of every propagation (structuring choice 10, decided by S. Guillet, 2026-11-19), the
    // constructor of the body still accepting it
    [Theory]
    [MemberData(nameof(EveryPath))]
    public void ACentralBodyWithoutGm_IsRefusedAtTheStartOfEveryPropagation(string path)
    {
        Assert.False(KernelTextValues.HasGravitationalParameter(507));
        var elara = new CelestialBody(507);
        Assert.Equal(0.0, elara.GM);
        var spacecraft = NewSpacecraft(elara);

        var error = Assert.Throws<ArgumentException>(() => Run(path, spacecraft, Array.Empty<CelestialItem>(), false, false));

        Assert.Equal("spacecraft", error.ParamName);
        Assert.Contains("ELARA (507)", error.Message);
        Assert.Contains("BODY507_GM", error.Message);
        Assert.Contains("gm_de440.tpc", error.Message);
        Assert.Empty(spacecraft.PropagatedStates);
    }

    #endregion
}
