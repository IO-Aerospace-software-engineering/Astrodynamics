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
/// dynamics"), each against CSPICE's <c>gftfov</c> in the process (choice 9 (b)): RK7(8), an impulsive maneuver followed by
/// a TRIAD attitude inside the window (a discrete attitude), a nadir attitude (a continuous one), SSB mode, an initial state
/// in ECLIPJ2000, and a TLE. A TEME initial state is not on the public surface: the propagator rejects it ("must be defined
/// in inertial frame").
/// </summary>
/// <remarks>
/// <para>
/// Two sets of kernels, for each variant. <b>The kernels the library writes</b> (<c>Spacecraft.WriteEphemeris</c> and
/// <c>WriteOrientation</c>), what a user gives CSPICE or another tool: for RK7(8) and SSB mode, whose attitude is the
/// identity of ICRF, the two public paths agree within <see cref="Limit"/>. For the TRIAD attitude, the nadir attitude and
/// the ECLIPJ2000 initial state they do not, because of three defects outside #515, each measured (2026-11-01, Linux) and
/// pinned below with its measured disagreement (PROVISIONAL, until the defects are fixed in their own issues):
/// </para>
/// <list type="bullet">
/// <item>The CK the library writes holds the inverse of the library's attitude: <c>Spacecraft.WriteOrientation</c> passes
/// the rotation from the spacecraft frame to ICRF where a CK holds the C-matrix (the CK of a nadir attitude points the front
/// at the zenith at the start). For an identity attitude both are the same.</item>
/// <item>The CK interpolates linearly between the stored orientations, across a discrete attitude change: after the TRIAD
/// attitude, the CK turns over 2 731 s from the identity to the new attitude, which the library holds from the maneuver
/// on.</item>
/// <item>A propagated spacecraft whose initial state is in ECLIPJ2000 stores its states and orientations in that frame,
/// and <c>Spacecraft.GetGeometricStateRelativeTo</c> and its frame return them as ICRF: positions up to 2.5e6 m from those
/// of the SPK the library writes from the same states, which CSPICE reads right.</item>
/// </list>
/// <para>
/// <b>The kernels of what the library reads</b>, so that <c>gftfov</c> measures the field-of-view search and not those
/// defects: the CK holds the inverse of the library's rotations; for the TRIAD attitude, each orientation is held until
/// 1 ms before the next one, the attitude the library defines; for ECLIPJ2000 and the TLE, the SPK holds the states the
/// library reads every 10 s, and the CK the orientations as ICRF. The nadir attitude keeps the library's model (§8 item 9 of
/// #515): an orientation every output step, held with a zero angular velocity, where the CK interpolates; its edges differ
/// by up to the output step, the limit of each of its rows. A TLE spacecraft that is not propagated has no states the
/// library would write: its SPK is that of its reads.
/// </para>
/// <para>
/// The fields: a circle, an ellipse and a rectangle whose boresight is -X of the frame (the fields of
/// <c>InstrumentTests.FindWindowInFieldOfView</c>), and a circle of 1.3 rad about the front of the spacecraft. The rows of a
/// variant are those with an edge inside the search window on the kernels of what the library reads, their window counts
/// asserted: with a constant attitude, the Sun and the Moon stay where they are in the field.
/// </para>
/// </remarks>
public class FieldOfViewDynamicsTests
{
    // PROVISIONAL, twice the worst value over the rows below, off the nadir attitude, on Linux x64 (2026-11-01), until the
    // three platforms of the CI have printed their MEASURE lines: 1.2e-6 s (SSB 789 EARTH Ellipsoid LT) on the kernels of
    // what the library reads, and on the kernels the library writes for RK7(8) and SSB mode; gftfov at 1e-6 s, the managed
    // search at 1 µs, the times in ticks of 100 ns
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
    private static (Spacecraft spacecraft, Window window) Build(string variant)
    {
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

    /// <summary>
    /// A disagreement of the kernels the library writes with the library's reads, measured on Linux x64 (2026-11-01):
    /// the window counts of gftfov and of the managed search, and, when they are equal, the worst edge, in seconds.
    /// </summary>
    private sealed record Defect(int GftfovWindows, int ManagedWindows, double Worst = 0.0);

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
        // PROVISIONAL, the nadir attitude (§8 item 9 of #515): the library holds each orientation of its 1 s output steps
        // with a zero angular velocity, where the CK interpolates between them, so an edge moves by up to the output step;
        // twice the measured edge of each row (Linux, 2026-11-01: 0.885 s, 0.335 s, 0.550 s), until the attitude model is
        // decided in its own issue. The Earth stays in place in a nadir frame: no edge
        ["Nadir"] =
        [
            new(792, "SUN", ShapeType.Point, Aberration.LTS, 1, 1.8), new(792, "SUN", ShapeType.Ellipsoid, Aberration.LTS, 1, 0.67),
            new(792, "MOON", ShapeType.Point, Aberration.LT, 1, 1.1)
        ],
        ["TLE"] =
        [
            new(789, "EARTH", ShapeType.Ellipsoid, Aberration.LT, 1), new(790, "EARTH", ShapeType.Point, Aberration.LT, 1),
            new(791, "EARTH", ShapeType.Ellipsoid, Aberration.LTS, 1), new(789, "DSS-13", ShapeType.Point, Aberration.LT, 1),
            new(790, "DSS-13", ShapeType.Point, Aberration.LT, 1), new(791, "DSS-13", ShapeType.Point, Aberration.LTS, 1)
        ]
    };

    // PROVISIONAL, the disagreements on the kernels the library writes, by row, measured on Linux x64 (2026-11-01); a
    // row not listed agrees within its limit. The worst edge is asserted within a factor of two of the measurement
    private static readonly Dictionary<string, Dictionary<string, Defect>> Defects = new()
    {
        // The CK holds the inverse attitude and interpolates across the TRIAD change
        ["ImpulseThenTriad"] = new()
        {
            ["789 EARTH Ellipsoid LT"] = new(1, 2), ["790 EARTH Point LT"] = new(1, 1, 500.2), ["791 EARTH Ellipsoid LTS"] = new(1, 2),
            ["792 DSS-13 Point LT"] = new(2, 2, 971.8), ["792 SUN Point LTS"] = new(1, 1, 268.0), ["792 SUN Ellipsoid LTS"] = new(1, 1, 268.0)
        },
        // The CK holds the inverse attitude: the Sun and the Moon never enter the field of gftfov
        ["Nadir"] = new()
        {
            ["792 SUN Point LTS"] = new(0, 1), ["792 SUN Ellipsoid LTS"] = new(0, 1), ["792 MOON Point LT"] = new(0, 1)
        },
        // The states stored in ECLIPJ2000 are read as ICRF; CSPICE reads the SPK right
        ["ECLIPJ2000"] = new()
        {
            ["789 EARTH Ellipsoid LT"] = new(1, 1, 0.699), ["790 EARTH Point LT"] = new(1, 1, 9.66e-3), ["791 EARTH Ellipsoid LTS"] = new(1, 1, 0.770),
            ["789 DSS-13 Point LT"] = new(1, 1, 215.8), ["792 DSS-13 Point LT"] = new(2, 2, 232.6), ["792 EARTH Ellipsoid LT"] = new(2, 2, 0.322)
        }
    };

    [Theory]
    [InlineData("RK78")]
    [InlineData("ImpulseThenTriad")]
    [InlineData("Nadir")]
    [InlineData("SSB")]
    [InlineData("ECLIPJ2000")]
    [InlineData("TLE")]
    public void Variant_MatchesGftfov(string variant)
    {
        var (spacecraft, window) = Build(variant);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var targets = new Dictionary<string, ILocalizable>
        {
            ["EARTH"] = earth, ["SUN"] = new CelestialBody(Stars.Sun), ["MOON"] = new CelestialBody(PlanetsAndMoons.MOON), ["DSS-13"] = new Site(13, "DSS-13", earth)
        };
        string directory = $"Data/User/FieldOfViewDynamics515/{variant}";
        // Both sets of kernels are built before the searches, which add orientations to the spacecraft's frame; the
        // attitude changes the CK of the reads holds until 1 ms before them (the TRIAD attitude)
        var jumps = variant == "ImpulseThenTriad"
            ? spacecraft.Frame.GetStateOrientationsToICRF().Select(x => x.Epoch).Where(x => x > window.StartDate && x < window.EndDate).ToArray()
            : [];
        var writeReads = PrepareReadKernels(variant, directory + "/Read", spacecraft, window);
        var rows = Rows[variant];

        // The kernels the library writes, loaded first: those of the reads, loaded after, take precedence
        var libraryWindows = new Window[rows.Length][];
        if (variant != "TLE")
        {
            FieldOfViewKernels.WriteAndLoad(directory + "/Library", spacecraft, [], [], window);
            for (int i = 0; i < rows.Length; i++)
            {
                libraryWindows[i] = Gftfov(spacecraft, window, rows[i], targets);
            }
        }

        writeReads();
        Defects.TryGetValue(variant, out var defects);
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var instrument = spacecraft.Instruments.First(x => x.NaifId == spacecraft.NaifId * 1000 - row.Number);
            var actual = instrument.FindWindowsInFieldOfViewConstraint(window, spacecraft, targets[row.Target], TargetFrame(row), row.Shape, row.Aberration, Step).ToArray();
            var expected = Gftfov(spacecraft, window, row, targets);
            _output.WriteLine($"  {variant} {row}: managed {FieldOfViewKernels.Format(actual)}; gftfov {FieldOfViewKernels.Format(expected)}");

            // An edge of gftfov at an attitude change is the change itself
            var reference = expected.Select(x => new Window(AtJump(x.StartDate, jumps), AtJump(x.EndDate, jumps))).ToArray();
            Assert.Equal(row.Windows, expected.Length);
            Assert.Contains(expected, x => x.StartDate > window.StartDate || x.EndDate < window.EndDate);
            double worst = FieldOfViewKernels.WorstEdge(actual, reference);
            _output.WriteLine($"MEASURE #515 dynamics {variant} {row}, {expected.Length} windows: |managed propagated - gftfov (1e-6 s)| {worst:E2} s");
            Assert.True(worst < row.Limit, $"{row}: {worst:E2} s");

            if (libraryWindows[i] is not { } library)
            {
                continue;
            }

            string key = row.ToString();
            if (defects != null && defects.TryGetValue(key, out var defect))
            {
                Assert.Equal(defect.GftfovWindows, library.Length);
                Assert.Equal(defect.ManagedWindows, actual.Length);
                if (library.Length == actual.Length)
                {
                    double off = FieldOfViewKernels.WorstEdge(actual, library);
                    _output.WriteLine($"MEASURE #515 dynamics {variant} {row}, the kernels the library writes (a defect outside #515): |managed propagated - gftfov| {off:E3} s");
                    Assert.InRange(off, defect.Worst / 2.0, defect.Worst * 2.0);
                }
                else
                {
                    _output.WriteLine($"MEASURE #515 dynamics {variant} {row}, the kernels the library writes (a defect outside #515): gftfov {library.Length} windows, managed {actual.Length}");
                }
            }
            else
            {
                double off = FieldOfViewKernels.WorstEdge(actual, library);
                _output.WriteLine($"MEASURE #515 dynamics {variant} {row}, the kernels the library writes: |managed propagated - gftfov (1e-6 s)| {off:E2} s");
                Assert.True(off < row.Limit, $"{row}, the kernels the library writes: {off:E2} s");
            }
        }
    }

    /// <summary>The body-fixed frame of an ellipsoid target, which gftfov requires; ICRF for a point, which it ignores.</summary>
    private static Frames.Frame TargetFrame(Row row) => row.Shape == ShapeType.Point ? Frames.Frame.ICRF : new Frames.Frame(row.Target == "SUN" ? "IAU_SUN" : "ITRF93");

    private static Window[] Gftfov(Spacecraft spacecraft, Window window, Row row, IReadOnlyDictionary<string, ILocalizable> targets)
    {
        return SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(window, spacecraft.NaifId, spacecraft.NaifId * 1000 - row.Number, targets[row.Target].NaifId,
            TargetFrame(row), row.Shape, row.Aberration, Step).ToArray();
    }

    /// <summary>An edge of gftfov within the 1 ms before an attitude change the CK holds is the change itself.</summary>
    private static TimeSystem.Time AtJump(TimeSystem.Time edge, IReadOnlyList<TimeSystem.Time> jumps)
    {
        foreach (var jump in jumps)
        {
            double before = (jump - edge).TotalSeconds;
            if (before >= 0.0 && before <= 1.0e-3)
            {
                return jump;
            }
        }

        return edge;
    }

    /// <summary>
    /// The writing of the kernels of what the library reads (the remarks of the class), from what it holds before any
    /// search.
    /// </summary>
    private static Action PrepareReadKernels(string variant, string directory, Spacecraft spacecraft, Window window)
    {
        var stored = spacecraft.Frame.GetStateOrientationsToICRF().OrderBy(x => x.Epoch).ToArray();
        Action<FileInfo> sampledEphemeris = null;
        if (variant is "TLE" or "ECLIPJ2000")
        {
            // The states the library reads, every 10 s; and, for the TLE, which is not propagated, its frame's orientations at
            // the same epochs (that of ICRF, without attitude)
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);
            var states = new List<StateVector>();
            for (var epoch = window.StartDate.AddSeconds(-60.0); epoch <= window.EndDate.AddSeconds(60.0); epoch = epoch.AddSeconds(10.0))
            {
                var state = spacecraft.GetGeometricStateRelativeTo(epoch, earth);
                states.Add(new StateVector(state.Position, state.Velocity, earth, epoch, Frames.Frame.ICRF));
                if (variant == "TLE")
                {
                    spacecraft.Frame.GetStateOrientationToICRF(epoch);
                }
            }

            sampledEphemeris = file => SpiceAPI.Instance.WriteEphemeris(file, spacecraft.NaifId, states.ToArray());
            stored = spacecraft.Frame.GetStateOrientationsToICRF().OrderBy(x => x.Epoch).ToArray();
        }

        var orientations = new List<StateOrientation>();
        for (int i = 0; i < stored.Length; i++)
        {
            var frame = variant == "ECLIPJ2000" ? Frames.Frame.ICRF : stored[i].ReferenceFrame;
            if (variant == "ImpulseThenTriad" && i > 0)
            {
                orientations.Add(new StateOrientation(stored[i - 1].Rotation.Conjugate(), stored[i - 1].AngularVelocity, stored[i].Epoch.AddSeconds(-1e-3), frame));
            }

            orientations.Add(new StateOrientation(stored[i].Rotation.Conjugate(), stored[i].AngularVelocity, stored[i].Epoch, frame));
        }

        return () => FieldOfViewKernels.WriteAndLoad(directory, spacecraft, [], [], window, sampledEphemeris, orientations);
    }
}
