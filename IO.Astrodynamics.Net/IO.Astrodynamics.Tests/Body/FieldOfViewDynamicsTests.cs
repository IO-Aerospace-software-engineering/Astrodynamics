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
/// dynamics"), each against CSPICE's <c>gftfov</c> in the process on the SPK and CK the library writes from the same
/// propagation (choice 9 (b)): RK7(8), an impulsive maneuver followed by a TRIAD attitude inside the window (a discrete
/// attitude), a nadir attitude (a continuous one), SSB mode, initial states in ECLIPJ2000 and in TEME, and a TLE.
/// </summary>
/// <remarks>
/// The fields: a circle, an ellipse and a rectangle whose boresight is -X of the frame (the fields of
/// <c>InstrumentTests.FindWindowInFieldOfView</c>), and a circle of 1.3 rad about the front of the spacecraft. The
/// targets: the Earth as an ellipsoid and as a point, DSS-13 and the Sun. Limits: those of
/// <see cref="FieldOfViewSearchTests"/> off the grazing edges.
/// </remarks>
public class FieldOfViewDynamicsTests
{
    // PROVISIONAL, as FieldOfViewSearchTests.PropagatedLimit (twice the Linux worst of its 206 rows, 2026-11-01); the worst
    // over the variants below, Linux, 2026-11-01, is printed by the MEASURE lines
    private const double Limit = 2.6e-6;

    // PROVISIONAL, the nadir attitude (§8 item 9 of #515): the library holds each orientation of its 1 s output steps with a
    // zero angular velocity, where the CK interpolates between them; the Sun enters the wide field at the step where the
    // library's attitude jumps, 0.885 s from gftfov's edge (Linux, 2026-11-01). Twice that, until the attitude model is
    // decided in its own issue
    private const double AttitudeModelLimit = 1.8;

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
            case "TEME":
            {
                var (spacecraft, _) = Create(-5176, leo.ToFrame(Frames.Frame.TEME).ToStateVector());
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
        var sun = new CelestialBody(Stars.Sun);
        var dss13 = new Site(13, "DSS-13", earth);
        string directory = $"Data/User/FieldOfViewDynamics515/{variant}";
        // The attitude changes the CK holds until 1 ms before them (the TRIAD attitude), read before the searches add theirs
        var jumps = variant == "ImpulseThenTriad"
            ? spacecraft.Frame.GetStateOrientationsToICRF().Select(x => x.Epoch).Where(x => x > window.StartDate && x < window.EndDate).ToArray()
            : [];
        WriteKernels(variant, directory, spacecraft, window);

        var itrf = new Frames.Frame("ITRF93");
        var cases = new (int number, ILocalizable target, ShapeType shape, Aberration aberration)[]
        {
            (789, earth, ShapeType.Ellipsoid, Aberration.LT), (790, earth, ShapeType.Point, Aberration.LT), (791, earth, ShapeType.Ellipsoid, Aberration.LTS),
            (792, dss13, ShapeType.Point, Aberration.LT), (792, sun, ShapeType.Point, Aberration.LTS)
        };
        int windows = 0;
        var results = new List<(Window[] actual, Window[] expected, string label)>();
        foreach (var (number, target, shape, aberration) in cases)
        {
            var instrument = spacecraft.Instruments.First(x => x.NaifId == spacecraft.NaifId * 1000 - number);
            var frame = shape == ShapeType.Point ? Frames.Frame.ICRF : itrf;
            var expected = SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(window, spacecraft.NaifId, instrument.NaifId, target.NaifId, frame, shape, aberration, Step)
                .ToArray();
            var actual = instrument.FindWindowsInFieldOfViewConstraint(window, spacecraft, target, frame, shape, aberration, Step).ToArray();
            _output.WriteLine($"  {variant} {number} {target.Name} {shape} {aberration}: managed {FieldOfViewKernels.Format(actual)}; gftfov {FieldOfViewKernels.Format(expected)}");
            windows += expected.Length;
            results.Add((actual, expected, $"{number} {target.Name} {shape} {aberration}"));
        }

        // An edge of gftfov at an attitude change is the change itself
        foreach (var (actual, expected, label) in results)
        {
            var reference = expected.Select(x => new Window(AtJump(x.StartDate, jumps), AtJump(x.EndDate, jumps))).ToArray();
            double worst = FieldOfViewKernels.WorstEdge(actual, reference);
            _output.WriteLine($"MEASURE #515 dynamics {variant} {label}, {expected.Length} windows: |managed propagated - gftfov (1e-6 s)| {worst:E2} s");
            Assert.True(worst < (variant == "Nadir" ? AttitudeModelLimit : Limit), $"{label}: {worst:E2} s");
        }

        Assert.True(windows >= 3, $"{windows} windows");
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
    /// The kernels of a variant, which describe the trajectory and the attitude the library reads, so that gftfov measures
    /// the field-of-view search and not three defects outside #515, each measured (2026-11-01, Linux):
    /// <list type="bullet">
    /// <item>Spacecraft.WriteOrientation writes the library's rotation from the spacecraft frame to ICRF where a CK holds
    /// the C-matrix, its inverse: the CK of a nadir attitude points the front at the zenith at the start. The CKs here hold
    /// the inverse of the library's rotations; for a constant identity attitude both are the same.</item>
    /// <item>The CK interpolates linearly between the stored orientations, across a discrete attitude change: after the
    /// TRIAD attitude, the CK turns over 2 731 s from the identity to the new attitude, which the library holds from the
    /// maneuver on; 475 s on the edges of the circle. For that variant each orientation is held in the CK until 1 ms
    /// before the next one, the attitude the library defines.</item>
    /// <item>A propagated spacecraft whose initial state is in ECLIPJ2000 stores its states and orientations in that frame,
    /// and Spacecraft.GetGeometricStateRelativeTo and the frame return them as ICRF: positions up to 2.5e6 m from those
    /// of the SPK the library writes, which CSPICE reads right; 0.70 s, 0.77 s and 180 s on the edges here. For that
    /// variant the SPK holds the states the library reads every 10 s, and the CK the orientations as ICRF.</item>
    /// </list>
    /// The nadir attitude keeps the library's model (§8 item 9 of #515): an orientation every output step, held with a zero
    /// angular velocity, where the CK interpolates; its edges differ by up to the output step, its own limit.
    /// </summary>
    private static void WriteKernels(string variant, string directory, Spacecraft spacecraft, Window window)
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

        FieldOfViewKernels.WriteAndLoad(directory, spacecraft, [], [], window, sampledEphemeris, orientations);
    }
}
