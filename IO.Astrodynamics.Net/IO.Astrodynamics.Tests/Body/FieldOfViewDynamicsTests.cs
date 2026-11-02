using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Spacecraft = IO.Astrodynamics.Body.Spacecraft.Spacecraft;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The managed field-of-view search of spacecraft propagated in every way the public surface allows (#515, §7 "Observer
/// dynamics"), each against CSPICE's <c>gftfov</c> in the process (choice 9 (b)) on the kernels the library writes
/// (<c>Spacecraft.WriteEphemeris</c> and <c>WriteOrientation</c>), what a user gives CSPICE or another tool: RK7(8), an
/// impulsive maneuver followed by a TRIAD attitude inside the window (a discrete attitude), a nadir attitude (a continuous
/// one), SSB mode, the six inertial initial frames, the path of <c>Scenario.SimulateAsync</c>, and a TLE. A TEME initial
/// state is not on the public surface: the propagator rejects it ("must be defined in inertial frame").
/// </summary>
/// <remarks>
/// <para>
/// Until #537, three defects made the two public paths disagree, and this test pinned their disagreement (CI run
/// 41365060160, 2026-11-02): the CK held the inverse of the library's attitude (#529), interpolated across a discrete
/// change (#534: 971.8 s on an edge of the TRIAD variant), and a spacecraft propagated from ECLIPJ2000 stored its states
/// in that frame, read as ICRF (#535: 232.6 s); the nadir attitude, held between its outputs by the library where the CK
/// interpolates, had limits of 1.8, 0.67 and 1.1 s. The kernels of what the library read stood in for those it writes.
/// Fixed, every variant compares the managed search with <c>gftfov</c> on the kernels the library writes, at the limit of
/// #515, the nadir rows included: the CK repeats the earlier attitude 1 µs before a discrete change, which moves an edge
/// by at most that microsecond and the rounding of its encoded SCLK.
/// </para>
/// <para>
/// A TLE spacecraft is not propagated by <c>Spacecraft.Propagate</c>: its SPK holds the SGP4 states every 10 s, and its
/// CK the identity of ICRF, the attitude of a spacecraft without an attitude maneuver, both written by the test with the
/// raw writers (a read no longer adds to the attitude record, #523).
/// </para>
/// <para>
/// The fields: a circle, an ellipse and a rectangle whose boresight is -X of the frame (the fields of
/// <c>InstrumentTests.FindWindowInFieldOfView</c>), and a circle of 1.3 rad about the front of the spacecraft. The rows of a
/// variant are those with an edge inside the search window, their window counts asserted: with a constant attitude, the
/// Sun and the Moon stay where they are in the field. The variants of #515 run in every CI job; those added by #537 (the
/// other initial frames, the scenario path) are in the Validation category, under the same limit.
/// </para>
/// </remarks>
public class FieldOfViewDynamicsTests
{
    // Twice the worst over Linux, Windows and macOS arm64 (CI run 41365060160, 2026-11-02) over the rows below, off the
    // nadir attitude: 1.2e-6 / 1.2e-6 / 1.2e-6 s (SSB 789 EARTH Ellipsoid LT), every row the same on the three, on the
    // kernels of what the library read before #537, and on the kernels the library writes for RK7(8) and SSB mode; gftfov at
    // 1e-6 s, the managed search at 1 µs, the times in ticks of 100 ns. On the kernels the library writes with #537, the worst
    // over every variant is the same row, 1.2e-6 s (Linux x64, 2026-11-02; the nadir rows 4e-7 s, the TRIAD rows 1.0e-6 s)
    private const double Limit = 2.4e-6;

    private static readonly TimeSpan Step = TimeSpan.FromSeconds(60.0);

    private readonly ITestOutputHelper _output;

    public FieldOfViewDynamicsTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static readonly TimeSystem.Time Start = TimeSystem.Time.CreateUTC(676555130.80).ToTDB();

    private static void AddInstruments(Spacecraft spacecraft)
    {
        int id = spacecraft.NaifId * 1000;
        var y = new Vector3(0.0, IO.Astrodynamics.Constants.PI2, 0.0);
        spacecraft.AddCircularInstrument(id - 789, "CAMERA789", "fov515", 0.75, Vector3.VectorZ, Vector3.VectorY, y);
        spacecraft.AddEllipticalInstrument(id - 790, "ELL790", "fov515", 0.75, 0.3, Vector3.VectorZ, Vector3.VectorY, y);
        spacecraft.AddRectangularInstrument(id - 791, "REC791", "fov515", 0.75, 0.3, Vector3.VectorZ, Vector3.VectorY, y);
        spacecraft.AddCircularInstrument(id - 792, "WIDE792", "fov515", 1.3, Spacecraft.Front, Vector3.VectorZ, Vector3.Zero);
    }

    private static (Spacecraft spacecraft, Engine engine) Create(int naifId, StateVector orbit)
    {
        var spacecraft = new Spacecraft(naifId, $"FOV{-naifId}", 1000.0, 3000.0, new Clock($"FOV{-naifId}_CLK", 65536), orbit);
        var tank = new FuelTank("ft515", "model", "sn515", 1000.0, 1000.0);
        spacecraft.AddFuelTank(tank);
        var engine = new Engine("eng515", "model", "sn515", 300.0, 10.0, tank);
        spacecraft.AddEngine(engine);
        AddInstruments(spacecraft);
        return (spacecraft, engine);
    }

    /// <summary>A propagated spacecraft of the variant and its search window.</summary>
    private static (Spacecraft spacecraft, Window window) Build(string name)
    {
        // A variant "name@FRAME" starts from the same state in another inertial frame (#535): the same trajectory and the same
        // attitude, under other identifiers
        var parts = name.Split('@');
        string variant = parts[0];
        var initialFrame = parts.Length > 1 ? new Frames.Frame(parts[1]) : Frames.Frame.ICRF;
        int offset = parts.Length > 1 ? 100 * (Array.IndexOf(InitialFrames, parts[1]) + 1) : 0;
        StateVector In(StateVector state) => initialFrame == Frames.Frame.ICRF ? state : state.ToFrame(initialFrame).ToStateVector();
        (Spacecraft, Engine) Create(int naifId, StateVector orbit) => FieldOfViewDynamicsTests.Create(naifId - offset, In(orbit));
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start);
        var sun = new CelestialBody(Stars.Sun, Frames.Frame.ICRF, Start);
        var bodies = new CelestialItem[] { earth, moon, sun };
        var propagation = new Window(Start, Start.AddSeconds(3000.0));
        var window = new Window(Start.AddSeconds(1.0), Start.AddSeconds(2999.0));
        var leo = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, Start, Frames.Frame.ICRF);
        var oneSecond = TimeSpan.FromSeconds(1.0);
        switch (variant)
        {
            case "RK78":
            {
                var (spacecraft, _) = Create(-5171, leo);
                spacecraft.Propagate(propagation, bodies, new RK78Integrator(1e-12, 1e-12), false, false, oneSecond);
                return (spacecraft, window);
            }
            case "ImpulseThenTriad":
            {
                // Below the circular velocity: at apogee at the start, at perigee half an orbit later, where the maneuver
                // raises the apogee; the TRIAD attitude follows it, held from then on
                var orbit = new StateVector(leo.Position, new Vector3(0.0, 7600.0, 0.0), earth, Start, Frames.Frame.ICRF);
                var (spacecraft, engine) = Create(-5172, orbit);
                var impulse = new ApogeeHeightManeuver(earth, Start.AddSeconds(100.0), TimeSpan.Zero, 7200000.0, engine);
                impulse.SetNextManeuver(new TriadAttitude(Start.AddSeconds(100.0), TimeSpan.Zero, Spacecraft.Front, earth, Spacecraft.Up, sun, engine));
                spacecraft.SetStandbyManeuver(impulse);
                spacecraft.Propagate(propagation, bodies, false, false, oneSecond);
                Assert.NotNull(impulse.ThrustWindow);
                Assert.InRange((impulse.ThrustWindow.Value.StartDate - Start).TotalSeconds, 100.0, 2900.0);
                return (spacecraft, window);
            }
            case "Nadir":
            {
                var (spacecraft, engine) = Create(-5173, leo);
                spacecraft.SetStandbyManeuver(new NadirAttitude(earth, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, engine));
                spacecraft.Propagate(propagation, bodies, false, false, oneSecond);
                return (spacecraft, window);
            }
            case "SSB":
            {
                var earthFromSsb = earth.GetGeometricStateFromICRF(Start).ToStateVector();
                var orbit = new StateVector(earthFromSsb.Position + leo.Position, earthFromSsb.Velocity + leo.Velocity, Barycenters.SOLAR_SYSTEM_BARYCENTER, Start,
                    Frames.Frame.ICRF);
                var (spacecraft, _) = Create(-5174, orbit);
                spacecraft.Propagate(propagation, bodies, false, false, oneSecond);
                return (spacecraft, window);
            }
            case "ECLIPJ2000":
            {
                var (spacecraft, _) = Create(-5175, leo.ToFrame(Frames.Frame.ECLIPTIC_J2000).ToStateVector());
                spacecraft.Propagate(propagation, bodies, false, false, oneSecond);
                return (spacecraft, window);
            }
            case "NadirScenario":
            {
                // The nadir attitude through Scenario.SimulateAsync
                var (spacecraft, engine) = Create(-5178, leo);
                spacecraft.SetStandbyManeuver(new NadirAttitude(earth, new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero, engine));
                var scenario = new IO.Astrodynamics.Mission.Scenario($"FovDyn{spacecraft.NaifId}", new IO.Astrodynamics.Mission.Mission($"fovdyn{spacecraft.NaifId}"), propagation);
                scenario.AddCelestialItem(moon);
                scenario.AddCelestialItem(earth);
                scenario.AddCelestialItem(sun);
                scenario.AddSpacecraft(spacecraft);
                scenario.SimulateAsync(false, false, oneSecond).GetAwaiter().GetResult();
                return (spacecraft, window);
            }
            case "TLE":
            {
                // Not propagated: the spacecraft reads SGP4 at each epoch, and its SPK holds the SGP4 states every 10 s
                var tle = new TLE("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
                    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
                var spacecraft = new Spacecraft(-5177, "FOV5177", 1000.0, 3000.0, new Clock("FOV5177_CLK", 65536), tle);
                AddInstruments(spacecraft);
                var tleStart = tle.Epoch.ToTDB();
                return (spacecraft, new Window(tleStart.AddSeconds(1.0), tleStart.AddSeconds(2999.0)));
            }
            default:
                throw new ArgumentException(variant);
        }
    }

    /// <summary>A row: a field, a target, its shape, the correction, the windows of gftfov, and the limit of its edges.</summary>
    private sealed record Row(int Number, string Target, ShapeType Shape, Aberration Aberration, int Windows, double Limit = FieldOfViewDynamicsTests.Limit)
    {
        public override string ToString() => $"{Number} {Target} {Shape} {Aberration}";
    }

    private static readonly Row[] ConstantAttitude =
    [
        new(789, "EARTH", ShapeType.Ellipsoid, Aberration.LT, 1), new(790, "EARTH", ShapeType.Point, Aberration.LT, 1),
        new(791, "EARTH", ShapeType.Ellipsoid, Aberration.LTS, 1), new(789, "DSS-13", ShapeType.Point, Aberration.LT, 1),
        new(792, "DSS-13", ShapeType.Point, Aberration.LT, 2), new(792, "EARTH", ShapeType.Ellipsoid, Aberration.LT, 2)
    ];

    private static readonly Dictionary<string, Row[]> Rows = new()
    {
        ["RK78"] = ConstantAttitude,
        ["SSB"] = ConstantAttitude,
        ["ECLIPJ2000"] = ConstantAttitude,
        ["ImpulseThenTriad"] =
        [
            new(789, "EARTH", ShapeType.Ellipsoid, Aberration.LT, 2), new(790, "EARTH", ShapeType.Point, Aberration.LT, 1),
            new(791, "EARTH", ShapeType.Ellipsoid, Aberration.LTS, 2), new(792, "DSS-13", ShapeType.Point, Aberration.LT, 2),
            new(792, "SUN", ShapeType.Point, Aberration.LTS, 1), new(792, "SUN", ShapeType.Ellipsoid, Aberration.LTS, 1)
        ],
        // The nadir attitude: the Earth stays in place in a nadir frame, no edge
        ["Nadir"] =
        [
            new(792, "SUN", ShapeType.Point, Aberration.LTS, 1), new(792, "SUN", ShapeType.Ellipsoid, Aberration.LTS, 1),
            new(792, "MOON", ShapeType.Point, Aberration.LT, 1)
        ],
        ["TLE"] =
        [
            new(789, "EARTH", ShapeType.Ellipsoid, Aberration.LT, 1), new(790, "EARTH", ShapeType.Point, Aberration.LT, 1),
            new(791, "EARTH", ShapeType.Ellipsoid, Aberration.LTS, 1), new(789, "DSS-13", ShapeType.Point, Aberration.LT, 1),
            new(790, "DSS-13", ShapeType.Point, Aberration.LT, 1), new(791, "DSS-13", ShapeType.Point, Aberration.LTS, 1)
        ]
    };

    private static readonly string[] InitialFrames = ["ECLIPJ2000", "B1950", "FK4", "ECLIPB1950", "GALACTIC"];

    [Theory]
    [InlineData("RK78")]
    [InlineData("ImpulseThenTriad")]
    [InlineData("Nadir")]
    [InlineData("SSB")]
    [InlineData("ECLIPJ2000")]
    [InlineData("TLE")]
    public void Variant_MatchesGftfov(string variant) => Check(variant);

    /// <summary>The variants added by #537: the nadir and TRIAD attitudes from the five other inertial frames, RK7(8) and
    /// SSB mode from other frames, the nadir attitude through <c>Scenario.SimulateAsync</c>.</summary>
    [Theory]
    [Trait("Category", "Validation")]
    [InlineData("NadirScenario")]
    [InlineData("Nadir@ECLIPJ2000")]
    [InlineData("Nadir@B1950")]
    [InlineData("Nadir@FK4")]
    [InlineData("Nadir@ECLIPB1950")]
    [InlineData("Nadir@GALACTIC")]
    [InlineData("ImpulseThenTriad@ECLIPJ2000")]
    [InlineData("ImpulseThenTriad@B1950")]
    [InlineData("ImpulseThenTriad@FK4")]
    [InlineData("ImpulseThenTriad@ECLIPB1950")]
    [InlineData("ImpulseThenTriad@GALACTIC")]
    [InlineData("RK78@GALACTIC")]
    [InlineData("SSB@ECLIPJ2000")]
    [InlineData("SSB@GALACTIC")]
    public void VariantFromAnotherFrameOrPath_MatchesGftfov(string variant) => Check(variant);

    private void Check(string variant)
    {
        var (spacecraft, window) = Build(variant);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var targets = new Dictionary<string, ILocalizable>
        {
            ["EARTH"] = earth, ["SUN"] = new CelestialBody(Stars.Sun), ["MOON"] = new CelestialBody(PlanetsAndMoons.MOON), ["DSS-13"] = new Site(13, "DSS-13", earth)
        };
        string directory = $"Data/User/FieldOfViewDynamics515/{variant.Replace('@', '_')}";
        // The kernels the library writes: a read of the spacecraft no longer changes its attitude (#523), so they may be
        // written after or before the searches. The TLE spacecraft is not propagated: its SPK holds the SGP4 states every
        // 10 s, its CK the identity of ICRF, the attitude of a spacecraft without one
        if (variant == "TLE")
        {
            var earthBody = new CelestialBody(PlanetsAndMoons.EARTH);
            var states = new List<StateVector>();
            var orientations = new List<StateOrientation>();
            for (var epoch = window.StartDate.AddSeconds(-60.0); epoch <= window.EndDate.AddSeconds(60.0); epoch = epoch.AddSeconds(10.0))
            {
                var state = spacecraft.GetGeometricStateRelativeTo(epoch, earthBody);
                states.Add(new StateVector(state.Position, state.Velocity, earthBody, epoch, Frames.Frame.ICRF));
                orientations.Add(new StateOrientation(Quaternion.Zero, Vector3.Zero, epoch, Frames.Frame.ICRF));
            }

            FieldOfViewKernels.WriteAndLoad(directory, spacecraft, [], [], window, file => SpiceAPI.Instance.WriteEphemeris(file, spacecraft.NaifId, states.ToArray()), orientations);
        }
        else
        {
            FieldOfViewKernels.WriteAndLoad(directory, spacecraft, [], [], window);
        }

        var rows = Rows[variant.Split('@')[0] == "NadirScenario" ? "Nadir" : variant.Split('@')[0]];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var instrument = spacecraft.Instruments.First(x => x.NaifId == spacecraft.NaifId * 1000 - row.Number);
            var actual = instrument.FindWindowsInFieldOfViewConstraint(window, spacecraft, targets[row.Target], TargetFrame(row), row.Shape, row.Aberration, Step).ToArray();
            var expected = Gftfov(spacecraft, window, row, targets);
            _output.WriteLine($"  {variant} {row}: managed {FieldOfViewKernels.Format(actual)}; gftfov {FieldOfViewKernels.Format(expected)}");
            Assert.Equal(row.Windows, expected.Length);
            Assert.Contains(expected, x => x.StartDate > window.StartDate || x.EndDate < window.EndDate);
            double worst = FieldOfViewKernels.WorstEdge(actual, expected);
            _output.WriteLine($"MEASURE #515 dynamics {variant} {row}, {expected.Length} windows, the kernels the library writes: |managed propagated - gftfov (1e-6 s)| {worst:E2} s");
            Assert.True(worst < row.Limit, $"{row}: {worst:E2} s");
        }
    }

    /// <summary>The body-fixed frame of an ellipsoid target, which gftfov requires; ICRF for a point, which it ignores.</summary>
    private static Frames.Frame TargetFrame(Row row) => row.Shape == ShapeType.Point ? Frames.Frame.ICRF : new Frames.Frame(row.Target == "SUN" ? "IAU_SUN" : "ITRF93");

    private static Window[] Gftfov(Spacecraft spacecraft, Window window, Row row, IReadOnlyDictionary<string, ILocalizable> targets)
    {
        return SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(window, spacecraft.NaifId, spacecraft.NaifId * 1000 - row.Number, targets[row.Target].NaifId,
            TargetFrame(row), row.Shape, row.Aberration, Step).ToArray();
    }
}
