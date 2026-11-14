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

    private static readonly TimeSystem.Time Epoch = new(2021, 1, 1, 12, 0, 0);
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);
    private static Window OneHour => new(Epoch, Epoch.AddHours(1.0));
    private static readonly CovariancePropagationOptions Stm = new() { Mode = CovariancePropagationMode.StateTransitionOnly };

    private static int _nextId = -7100;

    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private static RK78Integrator NewRk78() => new(1e-10, 1e-10);

    // r = 6700 km, A = 10 m², m = 100 kg, Cd 2.2: drag of a few hundred metres per hour about the Earth
    private static Spacecraft NewSpacecraft(ILocalizable observer, Frames.Frame frame = null)
    {
        int id = NextId();
        return new Spacecraft(id, $"FM{-id}", 100.0, 10000.0, new Clock($"fmclk{-id}", 65536),
            new StateVector(new Vector3(6700000.0, 0.0, 0.0), new Vector3(0.0, 7713.0, 1000.0), observer, Epoch,
                frame ?? Frames.Frame.ICRF), 10.0, 2.2);
    }

    // The same orbit relative to the solar system barycenter: SSB mode, whose central body is the Sun
    private static Spacecraft NewSsbSpacecraft(CelestialItem earth)
    {
        var geocentric = new StateVector(new Vector3(6700000.0, 0.0, 0.0), new Vector3(0.0, 7713.0, 1000.0), earth,
            Epoch, Frames.Frame.ICRF);
        var ssbState = geocentric.RelativeTo(Barycenters.SOLAR_SYSTEM_BARYCENTER, Aberration.None).ToStateVector();
        int id = NextId();
        return new Spacecraft(id, $"FM{-id}", 100.0, 10000.0, new Clock($"fmclk{-id}", 65536), ssbState, 10.0, 2.2);
    }

    private static Spacecraft NewMoonSpacecraft()
    {
        var moon = PlanetsAndMoons.MOON_BODY;
        double r = 1838000.0;
        int id = NextId();
        return new Spacecraft(id, $"FM{-id}", 100.0, 10000.0, new Clock($"fmclk{-id}", 65536),
            new StateVector(new Vector3(r, 0.0, 0.0), new Vector3(0.0, System.Math.Sqrt(moon.GM / r), 0.0), moon, Epoch,
                Frames.Frame.ICRF), 10.0, 2.2);
    }

    private static CelestialBody EarthWith(IAtmosphericModel atmosphere = null,
        GeopotentialModelParameters geopotential = null) =>
        new(PlanetsAndMoons.EARTH, geopotentialModelParameters: geopotential, atmosphericModel: atmosphere);

    private static GeopotentialModelParameters Egm2008(ushort degree) =>
        new(System.IO.Path.Combine(Constants.SolarSystemKernelPath.ToString(), "EGM2008_to70_TideFree"), degree);

    /// <summary>An Earth whose GM differs from that of the kernels by 1e-3.</summary>
    private sealed class HeavierEarth : CelestialBody
    {
        internal HeavierEarth() : base(399, "EARTH", PlanetsAndMoons.EARTH_BODY.Mass * 1.001,
            PlanetsAndMoons.EARTH_BODY.PolarRadius, PlanetsAndMoons.EARTH_BODY.EquatorialRadius)
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
        "RK78 constructor", "RK78 covariance constructor", "VV constructor", "VV default constructor", "builder",
        "Propagate", "Propagate RK78", "Propagate RK78 covariance", "PropagateAsync", "Scenario", "Scenario RK78",
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
                using var p = new CentralBodyPropagator(w, spacecraft, NewRk78(), bodies, drag, srp, dt);
                p.Propagate();
                break;
            }
            case "RK78 covariance constructor":
            {
                using var p = new CentralBodyPropagator(w, spacecraft, NewRk78(), bodies, drag, srp, dt, Stm);
                p.Propagate();
                break;
            }
            case "VV constructor":
            {
                using var p = new CentralBodyPropagator(w, spacecraft, new VVIntegrator(dt), bodies, drag, srp, dt);
                p.Propagate();
                break;
            }
            case "VV default constructor":
            {
                using var p = new CentralBodyPropagator(w, spacecraft, bodies, drag, srp, dt);
                p.Propagate();
                break;
            }
            case "builder":
            {
                var builder = new CentralBodyPropagatorBuilder(w, spacecraft, NewRk78(), dt)
                    .WithPerturbingBodies(bodies ?? Array.Empty<CelestialItem>());
                if (drag) builder.IncludeAtmosphericDrag();
                if (srp) builder.IncludeSolarRadiationPressure();
                using var p = builder.Build();
                p.Propagate();
                break;
            }
            case "Propagate":
                spacecraft.Propagate(w, bodies, drag, srp, dt);
                break;
            case "Propagate RK78":
                spacecraft.Propagate(w, bodies, NewRk78(), drag, srp, dt);
                break;
            case "Propagate RK78 covariance":
                spacecraft.Propagate(w, bodies, NewRk78(), drag, srp, dt, Stm);
                break;
            case "PropagateAsync":
                spacecraft.PropagateAsync(w, bodies, drag, srp, dt).GetAwaiter().GetResult();
                break;
            case "Scenario":
            case "Scenario RK78":
            case "Scenario RK78 covariance":
            {
                var scenario = new Scenario("FM", new MissionOf("FM"), w);
                scenario.AddSpacecraft(spacecraft);
                foreach (var body in bodies ?? Array.Empty<CelestialItem>()) scenario.AddCelestialItem(body);
                var task = path switch
                {
                    "Scenario" => scenario.SimulateAsync(drag, srp, dt),
                    "Scenario RK78" => scenario.SimulateAsync(drag, srp, dt, NewRk78),
                    _ => scenario.SimulateAsync(drag, srp, dt, NewRk78, Stm)
                };
                task.GetAwaiter().GetResult();
                break;
            }
            case "Batch":
            case "Batch RK78":
            {
                var result = BatchPropagator.Propagate(new[]
                {
                    new PropagationTask(spacecraft, w, bodies, drag, srp, dt, path == "Batch" ? null : NewRk78)
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
        Assert.True(expected.Epoch == actual.Epoch, $"{label}: epoch {expected.Epoch} != {actual.Epoch}");
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
        // Arrange: the same orbit about EARTH_BODY, about an Earth with NRLMSISE-00 at nominal space weather, and
        // without drag
        var explicitEarth = EarthWith(new Nrlmsise00Model(SpaceWeather.Nominal));
        var modelless = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);
        var configured = NewSpacecraft(explicitEarth);
        var withoutDrag = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);

        // Act
        var defaulted = Run(path, modelless,
            new CelestialItem[] { PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }, true, false);
        var expected = Run(path, configured,
            new CelestialItem[] { explicitEarth, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }, true, false);
        var noDrag = Run(path, withoutDrag,
            new CelestialItem[] { PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }, false, false);

        // Assert: the default is its explicit setting, bit for bit, and it is drag
        double dragEffect = (defaulted.Position - noDrag.Position).Magnitude();
        _output.WriteLine($"{path}: drag over 1 h {dragEffect:E3} m");
        Assert.True(dragEffect > 10.0, $"{path}: the drag of a model-less Earth moved the orbit by {dragEffect:E3} m");
        AssertSameState(expected, defaulted, path);
    }

    [Fact]
    public void DragOnAModellessEarth_InParallel_EqualsTheSerialRun()
    {
        // Arrange: four spacecraft about EARTH_BODY, and four about Earths each with its own NRLMSISE-00
        var bodies = new CelestialItem[] { PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY };
        var serial = Enumerable.Range(0, 4).Select(_ => NewSpacecraft(PlanetsAndMoons.EARTH_BODY)).ToArray();
        var parallel = Enumerable.Range(0, 4).Select(_ => NewSpacecraft(PlanetsAndMoons.EARTH_BODY)).ToArray();
        var ownModels = Enumerable.Range(0, 4).Select(_ => EarthWith(new Nrlmsise00Model())).ToArray();
        var configured = ownModels.Select(e => NewSpacecraft(e)).ToArray();
        var options = new BatchOptions { MaxDegreeOfParallelism = 4 };

        // Act
        var expected = serial.Select(sc => Run("Propagate RK78", sc, bodies, true, false)).ToArray();
        var watch = Stopwatch.StartNew();
        var result = BatchPropagator.Propagate(parallel
            .Select(sc => new PropagationTask(sc, OneHour, bodies, true, false, Step, NewRk78)).ToArray(), options);
        double defaultMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var reference = BatchPropagator.Propagate(configured.Select((sc, i) => new PropagationTask(sc, OneHour,
            new CelestialItem[] { ownModels[i], Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }, true, false, Step,
            NewRk78)).ToArray(), options);
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
        "Propagate", "Propagate RK78", "VV default constructor", "RK78 constructor", "builder", "Scenario RK78"
    };

    [Theory]
    [MemberData(nameof(MoonPaths))]
    public void DragAboutAModellessNonEarthCentralBody_Throws_BeforeAnyChange(string path)
    {
        // Arrange: a Moon-centred spacecraft propagated once, without drag
        var spacecraft = NewMoonSpacecraft();
        spacecraft.Propagate(OneHour, new CelestialItem[] { PlanetsAndMoons.MOON_BODY }, false, false, Step);
        var before = spacecraft.PropagatedStates;
        int maneuvers = spacecraft.ExecutedManeuvers.Count;
        var atmosphericEarth = EarthWith(new EarthStandardAtmosphere());

        foreach (var bodies in new[]
                 {
                     new CelestialItem[] { PlanetsAndMoons.MOON_BODY },
                     new CelestialItem[] { PlanetsAndMoons.MOON_BODY, atmosphericEarth }
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
        "Propagate", "Propagate RK78", "VV default constructor", "RK78 constructor", "builder", "Scenario",
        "Scenario RK78", "Batch", "Batch RK78"
    };

    [Theory]
    [MemberData(nameof(ListedEarthPaths))]
    public void DragInSsbMode_FromAListedModellessEarth_IsNrlmsise00Nominal(string path)
    {
        // Arrange: the orbit relative to the barycenter, its central body the Sun, EARTH_BODY listed; the same orbit with
        // an Earth given NRLMSISE-00 at nominal space weather listed; and without drag (amendment of 2026-11-14: an Earth
        // always has at least the base atmosphere)
        var explicitEarth = EarthWith(new Nrlmsise00Model(SpaceWeather.Nominal));
        var window = new Window(Epoch, Epoch.AddMinutes(20.0));
        var step = TimeSpan.FromSeconds(10.0);
        var modelless = NewSsbSpacecraft(PlanetsAndMoons.EARTH_BODY);
        var configured = NewSsbSpacecraft(explicitEarth);
        var withoutDrag = NewSsbSpacecraft(PlanetsAndMoons.EARTH_BODY);

        // Act
        var defaulted = Run(path, modelless, new CelestialItem[] { PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY }, true,
            false, window, step);
        var expected = Run(path, configured, new CelestialItem[] { explicitEarth, Stars.SUN_BODY }, true, false, window,
            step);
        var noDrag = Run(path, withoutDrag, new CelestialItem[] { PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY }, false,
            false, window, step);

        // Assert: the listed Earth's default is its explicit setting, bit for bit, and it is drag
        double effect = (defaulted.Position - noDrag.Position).Magnitude();
        _output.WriteLine($"{path}: SSB mode, drag of EARTH_BODY over 20 min {effect:E3} m");
        Assert.True(effect > 1.0, $"{path}: the drag of a listed model-less Earth moved the orbit by {effect:E3} m");
        AssertSameState(expected, defaulted, path);
    }

    [Theory]
    [MemberData(nameof(ListedEarthPaths))]
    public void DragFromAListedModellessNonCentralEarth_IsNrlmsise00Nominal(string path)
    {
        // Arrange: a low Earth orbit propagated about the Moon, given an atmospheric model (US 1976, zero at the Earth's
        // distance), EARTH_BODY listed as a third body; the same with an Earth given NRLMSISE-00 at nominal space weather
        // listed; and without drag
        var moon = new CelestialBody(PlanetsAndMoons.MOON, atmosphericModel: new EarthStandardAtmosphere());
        var explicitEarth = EarthWith(new Nrlmsise00Model(SpaceWeather.Nominal));

        Spacecraft AboutMoon()
        {
            var geocentric = new StateVector(new Vector3(6700000.0, 0.0, 0.0), new Vector3(0.0, 7713.0, 1000.0),
                PlanetsAndMoons.EARTH_BODY, Epoch, Frames.Frame.ICRF);
            int id = NextId();
            return new Spacecraft(id, $"FM{-id}", 100.0, 10000.0, new Clock($"fmclk{-id}", 65536),
                geocentric.RelativeTo(moon, Aberration.None).ToStateVector(), 10.0, 2.2);
        }

        var window = new Window(Epoch, Epoch.AddMinutes(20.0));
        var step = TimeSpan.FromSeconds(10.0);

        // Act
        var defaulted = Run(path, AboutMoon(),
            new CelestialItem[] { moon, PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY }, true, false, window, step);
        var expected = Run(path, AboutMoon(), new CelestialItem[] { moon, explicitEarth, Stars.SUN_BODY }, true, false,
            window, step);
        var noDrag = Run(path, AboutMoon(), new CelestialItem[] { moon, PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY },
            false, false, window, step);

        // Assert: the listed Earth adds its default drag, bit for bit its explicit setting
        double effect = (defaulted.Position - noDrag.Position).Magnitude();
        _output.WriteLine($"{path}: about the Moon, drag of EARTH_BODY over 20 min {effect:E3} m");
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
        var dragged = Run("Propagate", withDrag, new CelestialItem[] { earth, Stars.SUN_BODY }, true, false, window, step);
        var free = Run("Propagate", withoutDrag, new CelestialItem[] { earth, Stars.SUN_BODY }, false, false, window, step);
        var exception = Assert.Throws<InvalidOperationException>(() => Run("Propagate", moonOnly,
            new CelestialItem[] { PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }, true, false, window, step));

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
        var spacecraft = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);

        // Act
        var solution = spacecraft.Propagate(OneHour, new CelestialItem[] { PlanetsAndMoons.EARTH_BODY }, NewRk78(), true,
            false, Step, Stm);

        // Assert: the keyword names the model the force used (CCSDS 508.0-B-1, ATMOSPHERIC_MODEL)
        Assert.Equal("NRLMSISE-00", CdmForceModel.From(solution.Dynamics).AtmosphericModel);
    }

    [Fact]
    public void AtmosphericDrag_OnAModellessEarth_EqualsNominalNrlmsise()
    {
        // Arrange
        var explicitEarth = EarthWith(new Nrlmsise00Model(SpaceWeather.Nominal));
        var spacecraft = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);
        var context = new ForceEvaluationContext(100.0, 2.2, 1.0);

        // Act
        var defaulted = new AtmosphericDrag(spacecraft, PlanetsAndMoons.EARTH_BODY);
        var configured = new AtmosphericDrag(spacecraft, explicitEarth);

        // Assert: at 200 to 800 km, the acceleration and its state partials bit for bit
        foreach (double altitude in new[] { 200e3, 350e3, 500e3, 650e3, 800e3 })
        {
            double r = 6378137.0 + altitude;
            var position = new Vector3(r * 0.6, r * 0.64, r * 0.48);
            var velocity = new Vector3(-5000.0, 5500.0, 1000.0);
            var a = defaulted.Apply(new StateVector(position, velocity, PlanetsAndMoons.EARTH_BODY, Epoch, Frames.Frame.ICRF));
            var b = configured.Apply(new StateVector(position, velocity, explicitEarth, Epoch, Frames.Frame.ICRF));
            Assert.True(a == b, $"{altitude / 1000.0} km: {a} != {b}");
            Assert.True(a.Magnitude() > 0.0);

            var dadrA = new double[9];
            var dadvA = new double[9];
            var dadrB = new double[9];
            var dadvB = new double[9];
            defaulted.AccumulateStatePartials(
                new StateVector(position, velocity, PlanetsAndMoons.EARTH_BODY, Epoch, Frames.Frame.ICRF), context, dadrA, dadvA);
            configured.AccumulateStatePartials(new StateVector(position, velocity, explicitEarth, Epoch, Frames.Frame.ICRF),
                context, dadrB, dadvB);
            Assert.Equal(dadrB, dadrA);
            Assert.Equal(dadvB, dadvA);
        }

        // A body without an atmosphere still has no drag
        Assert.Throws<ArgumentException>(() => new AtmosphericDrag(spacecraft, PlanetsAndMoons.MOON_BODY));
    }

    #endregion

    #region #15 The central body and its twins

    public static TheoryData<string, string, string, string> UnlistedCases()
    {
        var data = new TheoryData<string, string, string, string>();
        foreach (var path in new[] { "RK78 constructor", "VV default constructor", "Propagate", "Propagate RK78", "Batch", "Scenario" })
        {
            foreach (var forces in new[] { "drag+SRP", "SRP", "none" })
            {
                data.Add(path, forces, "ICRF", "Sun, Moon");
            }
        }

        data.Add("RK78 constructor", "drag+SRP", "ECLIPJ2000", "Sun, Moon");
        data.Add("VV default constructor", "drag+SRP", "ECLIPJ2000", "Sun, Moon");
        data.Add("RK78 constructor", "drag+SRP", "ICRF", "empty");
        data.Add("VV default constructor", "drag+SRP", "ICRF", "empty");
        data.Add("RK78 constructor", "none", "ICRF", "null");
        data.Add("Propagate", "drag+SRP", "ICRF", "null");
        data.Add("Propagate", "drag+SRP", "SSB", "Earth, Moon");
        data.Add("VV default constructor", "SRP", "SSB", "Earth, Moon");
        data.Add("Batch", "none", "SSB", "Earth, Moon");
        return data;
    }

    [Theory]
    [MemberData(nameof(UnlistedCases))]
    public void UnlistedCentralBody_TakesPart_LikeListedFirst_OnEveryPath(string path, string forces, string frame,
        string list)
    {
        // Arrange: an Earth with US 1976, the observer, listed first or not listed
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch, atmosphericModel: new EarthStandardAtmosphere());
        bool drag = forces.StartsWith("drag");
        bool srp = forces.Contains("SRP");
        bool ssb = frame == "SSB";
        Spacecraft NewOne() => ssb ? NewSsbSpacecraft(earth) : NewSpacecraft(earth, frame == "ECLIPJ2000" ? Frames.Frame.ECLIPTIC_J2000 : null);
        CelestialItem central = ssb ? Stars.SUN_BODY : earth;
        CelestialItem[] unlisted = list switch
        {
            "empty" => Array.Empty<CelestialItem>(),
            "null" => null,
            "Earth, Moon" => new CelestialItem[] { earth, PlanetsAndMoons.MOON_BODY },
            _ => new CelestialItem[] { Stars.SUN_BODY, PlanetsAndMoons.MOON_BODY }
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
        foreach (var path in new[] { "RK78 constructor", "builder", "Propagate", "Propagate RK78", "Batch", "Scenario RK78" })
        {
            foreach (var twin in new[] { "geopotential", "NRLMSISE-00", "space weather", "switch", "GM", "two listed" })
            {
                // The builder rejects a second body of one NAIF identifier when it is given (WithPerturbingBody)
                if (path == "builder" && twin == "two listed") continue;
                data.Add(path, twin);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TwinCases))]
    public void ListedTwinWithAnotherModel_Throws_OnEveryPath(string path, string twin)
    {
        // Arrange: the orbit on EARTH_BODY, propagated once; a listed Earth with another model
        var spacecraft = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);
        spacecraft.Propagate(OneHour, new CelestialItem[] { PlanetsAndMoons.EARTH_BODY }, false, false, Step);
        var before = spacecraft.PropagatedStates;
        var flags = NrlmsiseFlags.CreateStandard();
        flags.Switches[9] = 0;
        CelestialItem[] bodies = twin switch
        {
            "geopotential" => new CelestialItem[] { EarthWith(geopotential: Egm2008(10)), Stars.SUN_BODY },
            "NRLMSISE-00" => new CelestialItem[] { EarthWith(new Nrlmsise00Model()), Stars.SUN_BODY },
            "space weather" => new CelestialItem[]
            {
                EarthWith(new Nrlmsise00Model(new SpaceWeather { F107 = 250.0, F107A = 250.0, Ap = 100.0 })), Stars.SUN_BODY
            },
            "switch" => new CelestialItem[] { EarthWith(new Nrlmsise00Model(flags, SpaceWeather.Nominal)), Stars.SUN_BODY },
            "GM" => new CelestialItem[] { new HeavierEarth(), Stars.SUN_BODY },
            _ => new CelestialItem[] { new CelestialBody(PlanetsAndMoons.EARTH), EarthWith(geopotential: Egm2008(4)) }
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
        scenario.AddCelestialItem(PlanetsAndMoons.EARTH_BODY);
        var twin = EarthWith(new EarthStandardAtmosphere(), Egm2008(10));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => scenario.AddCelestialItem(twin));

        // Assert: the first stays; a value-equal twin is ignored, as before
        _output.WriteLine(exception.Message);
        Assert.Equal("celestialItem", exception.ParamName);
        Assert.Contains("EARTH", exception.Message);
        Assert.Same(PlanetsAndMoons.EARTH_BODY, Assert.Single(scenario.CelestialBodies));
        scenario.AddCelestialItem(new CelestialBody(PlanetsAndMoons.EARTH));
        Assert.Same(PlanetsAndMoons.EARTH_BODY, Assert.Single(scenario.CelestialBodies));
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
            _ => (Stars.SUN_BODY, new CelestialBody(Stars.Sun))
        };
        bool ssb = twin == "SSB Sun";
        var atmosphericEarth = EarthWith(new EarthStandardAtmosphere());
        Spacecraft NewOne() => ssb ? NewSsbSpacecraft(atmosphericEarth) : NewSpacecraft(pair.Observer);
        CelestialItem[] Bodies(CelestialItem central) => ssb
            ? new[] { central, atmosphericEarth }
            : new[] { central, Stars.SUN_BODY };
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

    #region #17 An integrator reused by a force-building path

    public static TheoryData<string, string> ReuseCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var integrator in new[] { "RK78", "VV" })
        {
            foreach (var path in new[] { "Propagate", "constructor", "builder twice", "builder pre-loaded" })
            {
                data.Add(integrator, path);
            }
        }

        data.Add("RK78", "Propagate covariance");
        data.Add("RK78", "covariance constructor");
        return data;
    }

    [Theory]
    [MemberData(nameof(ReuseCases))]
    public void ReusedIntegrator_Throws_OnEveryForceBuildingPath(string kind, string path)
    {
        // Arrange
        Integrator NewIntegrator() => kind == "RK78" ? NewRk78() : new VVIntegrator(Step);
        var bodies = new CelestialItem[] { PlanetsAndMoons.EARTH_BODY, Stars.SUN_BODY };
        var integrator = NewIntegrator();
        var spacecraft = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);
        var fresh = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);
        CentralBodyPropagatorBuilder builder = null;

        void Use(Integrator given, Spacecraft sc)
        {
            switch (path)
            {
                case "Propagate":
                    sc.Propagate(OneHour, bodies, given, false, true, Step);
                    break;
                case "Propagate covariance":
                    sc.Propagate(OneHour, bodies, given, false, true, Step, Stm);
                    break;
                case "constructor":
                {
                    using var p = new CentralBodyPropagator(OneHour, sc, given, bodies, false, true, Step);
                    p.Propagate();
                    break;
                }
                case "covariance constructor":
                {
                    using var p = new CentralBodyPropagator(OneHour, sc, given, bodies, false, true, Step, Stm);
                    p.Propagate();
                    break;
                }
                default:
                {
                    if (ReferenceEquals(given, integrator) && builder != null)
                    {
                        // The second Build of the same builder
                        using var again = builder.Build();
                        again.Propagate();
                        break;
                    }

                    var b = new CentralBodyPropagatorBuilder(OneHour, sc, given, Step).WithPerturbingBody(Stars.SUN_BODY)
                        .IncludeSolarRadiationPressure();
                    if (ReferenceEquals(given, integrator)) builder = b;
                    using var p = b.Build();
                    p.Propagate();
                    break;
                }
            }
        }

        // Act: a first use, and a run with a fresh instance
        if (path == "builder pre-loaded")
        {
            integrator.AddForce(new GravitationalAcceleration(PlanetsAndMoons.EARTH_BODY));
            builder = new CentralBodyPropagatorBuilder(OneHour, spacecraft, integrator, Step);
        }
        else
        {
            Use(integrator, spacecraft);
        }

        Use(NewIntegrator(), fresh);
        var firstStates = spacecraft.PropagatedStates;
        int forces = integrator.Forces.Count;

        // Assert: the second use throws, before anything changes
        var exception = Assert.Throws<ArgumentException>(() => Use(integrator, spacecraft));
        _output.WriteLine(exception.Message);
        Assert.Equal("integrator", exception.ParamName);
        Assert.Equal(forces, integrator.Forces.Count);
        AssertSameStates(firstStates, spacecraft.PropagatedStates, "after the rejected use");
        if (path != "builder pre-loaded")
        {
            AssertSameStates(fresh.PropagatedStates, spacecraft.PropagatedStates, "first use against a fresh instance");
        }
    }

    [Fact]
    public void SharedIntegratorFactory_InBatchAndMonteCarlo_OneRunOnly()
    {
        var bodies = new CelestialItem[] { PlanetsAndMoons.EARTH_BODY };
        var window = new Window(Epoch, Epoch.AddMinutes(30.0));
        var reference = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);
        reference.Propagate(window, bodies, new VVIntegrator(Step), false, false, Step);
        int referenceForces = 1;

        for (int repetition = 0; repetition < 20; repetition++)
        {
            // Arrange: eight tasks whose factory returns one instance
            var shared = new VVIntegrator(Step);
            var spacecraft = Enumerable.Range(0, 8).Select(_ => NewSpacecraft(PlanetsAndMoons.EARTH_BODY)).ToArray();
            var tasks = spacecraft.Select(sc => new PropagationTask(sc, window, bodies, false, false, Step, () => shared))
                .ToArray();

            // Act
            var result = BatchPropagator.Propagate(tasks, new BatchOptions { MaxDegreeOfParallelism = 4 });

            // Assert: one task runs, as with a fresh instance; the others are rejected
            var succeeded = result.Results.Where(r => r.Succeeded).ToArray();
            Assert.Single(succeeded);
            AssertSameState(reference.PropagatedStates[^1], succeeded[0].Spacecraft.PropagatedStates[^1],
                $"repetition {repetition}");
            foreach (var failed in result.Results.Where(r => !r.Succeeded))
            {
                var error = Assert.IsType<ArgumentException>(failed.Error);
                Assert.Equal("integrator", error.ParamName);
            }

            Assert.Equal(referenceForces, shared.Forces.Count);
        }

        // Monte Carlo: a factory returning one instance gives one run and RunCount − 1 failures
        var cov = new Matrix(6, 6);
        for (int i = 0; i < 3; i++)
        {
            cov.Set(i, i, 100.0);
            cov.Set(i + 3, i + 3, 0.01);
        }

        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        int id = NextId();
        var template = new Spacecraft(id, $"FM{-id}", 100.0, 10000.0, new Clock($"fmclk{-id}", 256),
            new StateVector(new Vector3(6800000.0, 0, 0), new Vector3(0, System.Math.Sqrt(earth.GM / 6800000.0), 0), earth,
                Epoch, Frames.Frame.ICRF, cov));
        var single = new VVIntegrator(Step);
        var campaign = MonteCarloPropagator.Propagate(new MonteCarloConfiguration
        {
            RunCount = 5, Seed = 42, TemplateSpacecraft = template, Window = window,
            CelestialBodiesFactory = () => new CelestialItem[] { new CelestialBody(PlanetsAndMoons.EARTH) },
            DeltaT = Step, IntegratorFactory = () => single
        });
        Assert.Equal(4, campaign.FailureCount);
    }

    [Fact]
    public void UserBuiltForceList_IIntegratorConstructor_Unaffected()
    {
        // Arrange: an integrator given its forces by the caller, and the force-building constructor
        var earth = PlanetsAndMoons.EARTH_BODY;
        var user = NewRk78();
        user.AddForce(new GravitationalAcceleration(earth));
        var userSpacecraft = NewSpacecraft(earth);
        var builtSpacecraft = NewSpacecraft(earth);
        // The constructor without forces uses the integrator as given: its caller initializes it
        user.Initialize((StateVector)userSpacecraft.InitialOrbitalParameters);

        // Act
        using (var p = new CentralBodyPropagator(OneHour, userSpacecraft, user, Step))
        {
            p.Propagate();
        }

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
                     "Propagate", "Propagate integrator", "Propagate covariance", "PropagateAsync", "constructor",
                     "covariance constructor", "IIntegrator constructor", "IIntegrator covariance constructor",
                     "VV default constructor", "builder", "Scenario", "Scenario factory", "Scenario covariance", "Batch",
                     "Monte Carlo", "PropagatorBase"
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
        var bodies = new CelestialItem[] { PlanetsAndMoons.EARTH_BODY };
        var window = new Window(Epoch, Epoch.AddMinutes(30.0));
        var spacecraft = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);
        spacecraft.Propagate(window, bodies, false, false, Step);
        var before = spacecraft.PropagatedStates;
        var integrator = NewRk78();
        var star = new Star(2, "star2", 1E+30, "spec", 2, 0.3792, new Equatorial(1, 1, Epoch), 0.1, 0.1, 0, 0, 0, 0, Epoch);
        string expectedName = entry switch
        {
            "Propagate" or "Propagate integrator" or "Propagate covariance" or "PropagateAsync" or "Scenario"
                or "Scenario factory" or "Scenario covariance" or "Batch" => "propagatorStepSize",
            "Monte Carlo" => "DeltaT",
            _ => "deltaT"
        };

        Scenario NewScenario()
        {
            var scenario = new Scenario("FM", new MissionOf("FM"), window);
            scenario.AddSpacecraft(spacecraft);
            scenario.AddCelestialItem(PlanetsAndMoons.EARTH_BODY);
            scenario.AddStar(star);
            return scenario;
        }

        void Act()
        {
            switch (entry)
            {
                case "Propagate": spacecraft.Propagate(window, bodies, false, false, step); break;
                case "Propagate integrator": spacecraft.Propagate(window, bodies, integrator, false, false, step); break;
                case "Propagate covariance": spacecraft.Propagate(window, bodies, integrator, false, false, step, Stm); break;
                case "PropagateAsync": _ = spacecraft.PropagateAsync(window, bodies, false, false, step); break;
                case "constructor": new CentralBodyPropagator(window, spacecraft, integrator, bodies, false, false, step).Propagate(); break;
                case "covariance constructor": new CentralBodyPropagator(window, spacecraft, integrator, bodies, false, false, step, Stm).Propagate(); break;
                case "IIntegrator constructor": new CentralBodyPropagator(window, spacecraft, integrator, step).Propagate(); break;
                case "IIntegrator covariance constructor": new CentralBodyPropagator(window, spacecraft, integrator, step, Stm).Propagate(); break;
                case "VV default constructor": new CentralBodyPropagator(window, spacecraft, bodies, false, false, step).Propagate(); break;
                case "builder": new CentralBodyPropagatorBuilder(window, spacecraft, integrator, step).Build().Propagate(); break;
                case "Scenario": NewScenario().SimulateAsync(false, false, step).GetAwaiter().GetResult(); break;
                case "Scenario factory": NewScenario().SimulateAsync(false, false, step, NewRk78).GetAwaiter().GetResult(); break;
                case "Scenario covariance": NewScenario().SimulateAsync(false, false, step, NewRk78, Stm).GetAwaiter().GetResult(); break;
                case "Batch":
                {
                    var result = BatchPropagator.Propagate(new[] { new PropagationTask(spacecraft, window, bodies, false, false, step) });
                    Assert.False(result.Results[0].Succeeded, "the task with a step <= 0 succeeded");
                    throw result.Results[0].Error;
                }
                case "Monte Carlo":
                {
                    var cov = new Matrix(6, 6);
                    for (int i = 0; i < 6; i++) cov.Set(i, i, i < 3 ? 100.0 : 0.01);
                    var template = NewSpacecraft(PlanetsAndMoons.EARTH_BODY);
                    var state = (StateVector)template.InitialOrbitalParameters;
                    int id = NextId();
                    var withCovariance = new Spacecraft(id, $"FM{-id}", 100.0, 10000.0, new Clock($"fmclk{-id}", 256),
                        new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame, cov));
                    MonteCarloPropagator.Propagate(new MonteCarloConfiguration
                    {
                        RunCount = 3, Seed = 1, TemplateSpacecraft = withCovariance, Window = window,
                        CelestialBodiesFactory = () => bodies, DeltaT = step
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

        // The integrator given with the rejected step is not consumed
        spacecraft.Propagate(window, bodies, integrator, false, false, Step);
    }

    [Fact]
    public void VVIntegrator_NonPositiveStep_Throws()
    {
        var state = new StateVector(new Vector3(6700000.0, 0, 0), new Vector3(0, 7713.0, 0), PlanetsAndMoons.EARTH_BODY,
            Epoch, Frames.Frame.ICRF);
        Assert.Equal("deltaT", Assert.Throws<ArgumentOutOfRangeException>(() => new VVIntegrator(TimeSpan.Zero)).ParamName);
        Assert.Equal("deltaT", Assert.Throws<ArgumentOutOfRangeException>(() => new VVIntegrator(
            new ForceBase[] { new GravitationalAcceleration(PlanetsAndMoons.EARTH_BODY) }, TimeSpan.FromSeconds(-1.0),
            state)).ParamName);
    }

    #endregion
}
