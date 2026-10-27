// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The propagators 001 to 003 of the conformance suite against GMAT R2026a with EGM2008 applied to the whole field,
/// its GM in the central term as in its harmonics (decision 17 of #457): the library reproduces them with
/// <see cref="GeopotentialCentralTerm.ModelGravitationalParameter"/> (decision 18 of #457).
/// </summary>
/// <remarks>
/// <para>
/// The GMAT runs are those of the study of the pre-existing conformance cases (variant <c>icrf</c>, 2026-10-27), copied
/// with their scripts and console logs into <c>Data/GmatConformancePropagators</c>; its README gives their provenance.
/// GMAT evaluates EGM2008 from a <c>.cof</c> whose header carries the GM and the radius of the model, which it applies to
/// the central term and to the harmonics, with de440s, the Moon and the Sun (and the planets for 002) as point masses,
/// PrinceDormand78 at 1e-13 and a step of at most 1 s (60 s for 002), in EarthICRF.
/// </para>
/// <para>
/// The library runs as the conformance runner does: RK7(8) at 1e-11, the final state at the UTC end of the day converted
/// to TDB (<see cref="ConformanceCaseAssert"/>). What remains between the two tools is the orientation of the Earth
/// (GMAT's FK5 with its EOP file, the library's ITRF93 of its bpc) and the GMs of the third bodies: the study measured
/// GMAT's own spread over the Earth-orientation data at up to 4.4e-5 km. With the GM of the kernels in the central term
/// (the default, D2-A of #444), the library is 6.8 m to 18.5 m away, so the reference resolves the choice.
/// </para>
/// </remarks>
public class GmatConformancePropagatorTests
{
    private const string DataDirectory = "Data/GmatConformancePropagators";
    private const string GeopotentialModel = "Data/SolarSystem/EGM2008_to70_TideFree";

    private readonly ITestOutputHelper _output;

    public GmatConformancePropagatorTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    /// <summary>
    /// The case, the integrator (Velocity-Verlet when true, RK7(8) otherwise), and the bounds of the largest position
    /// component (m) and of the largest velocity component (m/s) with the GM of the model in the central term.
    /// </summary>
    /// <remarks>
    /// Provisional bounds, twice the Linux measurement rounded up at the second significant digit (Linux x64, .NET 10,
    /// 2026-10-27): RK7(8) 001 8.834e-2 m and 1.015e-4 m/s, 002 5.951e-2 m and 4.287e-6 m/s, 003 1.562e-1 m and
    /// 1.728e-4 m/s; Velocity-Verlet 002 3.371e-1 m and 2.397e-5 m/s. To be set again from the MEASURE lines of the CI
    /// on Linux, Windows and macOS arm64. Of the RK7(8) figures, the integration at 1e-11 is about 5 mm (1e-13: 9.36 cm,
    /// 5.95 cm and 16.3 cm), the rest the models of the two tools. Velocity-Verlet only for 002: in LEO its error at
    /// 1 s, about 300 m a day, hides the central term.
    /// </remarks>
    public static TheoryData<string, bool, double, double> Cases => new()
    {
        { "001", false, 0.18, 2.1e-4 },
        { "002", false, 0.12, 8.6e-6 },
        { "003", false, 0.32, 3.5e-4 },
        { "002", true, 0.68, 4.8e-5 }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void WithTheModelCentralTerm_ReproducesGmat(string name, bool verlet, double positionBound,
        double velocityBound)
    {
        // Arrange
        var gmatCase = Case(name);
        var (gmatInitial, gmatFinal) = ReadReport(gmatCase.Report);

        // Act
        var withModel = Propagate(gmatCase, GeopotentialCentralTerm.ModelGravitationalParameter, verlet);
        var withBody = Propagate(gmatCase, GeopotentialCentralTerm.BodyGravitationalParameter, verlet);

        // Assert
        var initial = new[]
        {
            gmatCase.Position.X, gmatCase.Position.Y, gmatCase.Position.Z, gmatCase.Velocity.X, gmatCase.Velocity.Y,
            gmatCase.Velocity.Z
        };
        var (initialPosition, initialVelocity) = LargestComponents(initial, gmatInitial);
        var (position, velocity) = LargestComponents(withModel, gmatFinal);
        var (bodyPosition, bodyVelocity) = LargestComponents(withBody, gmatFinal);
        string integrator = verlet ? "Velocity-Verlet 1 s" : "RK7(8) 1e-11";
        _output.WriteLine($"MEASURE central term with the model GM (decision 18 of #457), propagator {name}, " +
                          $"{integrator} against GMAT R2026a: largest component {position:E3} m, {velocity:E3} m/s " +
                          $"(bounds {positionBound:E2} m, {velocityBound:E2} m/s); the GM of the body: " +
                          $"{bodyPosition:E3} m, {bodyVelocity:E3} m/s; the initial state of the report " +
                          $"{initialPosition:E2} m, {initialVelocity:E2} m/s from the input");
        Assert.True(initialPosition < 1e-6 && initialVelocity < 1e-9, "The report is not the run of this case");
        Assert.True(position < positionBound, $"position {position:E3} m");
        Assert.True(velocity < velocityBound, $"velocity {velocity:E3} m/s");
        // The reference resolves the choice: the GM of the body in the central term, the default, is more than ten
        // times farther from GMAT (measured 6.8 m to 18.5 m)
        Assert.True(bodyPosition > 10.0 * position, $"the GM of the body: {bodyPosition:E3} m");
    }

    private sealed record GmatCase(string Report, TimeSystem.Time Epoch, ushort Degree, Vector3 Position, Vector3 Velocity,
        Func<CelestialItem[]> ThirdBodies);

    private static GmatCase Case(string name)
    {
        // The initial states of the scripts, in EarthICRF; for 003, given to GMAT as Keplerian elements, the Cartesian
        // state GMAT reports at the start
        return name switch
        {
            "001" => new GmatCase(Path.Combine(DataDirectory, "propagator_001", "propagator_24h_geo10_001_output.txt"),
                new TimeSystem.Time(2025, 8, 25, 11, 55, 44, frame: TimeFrame.UTCFrame), 10,
                new Vector3(5442162.5926801835, -4068949.8468206248, -13456.851447751518),
                new Vector3(2858.1975428173836, 3809.7859312745794, 6002.1266931226886),
                () => new CelestialItem[] { PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }),
            "002" => new GmatCase(
                Path.Combine(DataDirectory, "propagator_002", "propagator_24h_geo70_full_002_output.txt"),
                new TimeSystem.Time(2026, 2, 9, 10, 22, 58, millisecond: 958, frame: TimeFrame.UTCFrame), 70,
                new Vector3(19283848.018390324, 37944390.563573960, -328553.515505210),
                new Vector3(-2727.809889171343, 1386.957738048448, 52.987319351738),
                () => new CelestialItem[]
                {
                    PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY, Barycenters.MERCURY_BARYCENTER,
                    Barycenters.VENUS_BARYCENTER, Barycenters.MARS_BARYCENTER, Barycenters.JUPITER_BARYCENTER,
                    Barycenters.SATURN_BARYCENTER, Barycenters.URANUS_BARYCENTER, Barycenters.NEPTUNE_BARYCENTER,
                    Barycenters.PLUTO_BARYCENTER
                }),
            "003" => new GmatCase(Path.Combine(DataDirectory, "propagator_003", "propagator_24h_sso_003_output.txt"),
                new TimeSystem.Time(2025, 6, 1, 10, 30, 0, frame: TimeFrame.UTCFrame), 10,
                new Vector3(972520.62166446251, -260586.11519760552, 6999012.2132351244),
                new Vector3(-1944.1954872554523, -7255.8363382834763, 4.5349106380634854e-13),
                () => new CelestialItem[] { PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY }),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    /// <summary>
    /// The final state (m, m/s, ICRF) of <paramref name="gmatCase"/> propagated over one day, EGM2008 with its own
    /// constants and <paramref name="centralTerm"/>, at the UTC end of the window converted to TDB: by RK7(8) at 1e-11
    /// as the conformance runner, or by the default Velocity-Verlet at a fixed step of 1 s.
    /// </summary>
    private static double[] Propagate(GmatCase gmatCase, GeopotentialCentralTerm centralTerm, bool verlet)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, gmatCase.Epoch,
            new GeopotentialModelParameters(GeopotentialModel, gmatCase.Degree) { CentralTerm = centralTerm });
        var orbit = new StateVector(gmatCase.Position, gmatCase.Velocity, earth, gmatCase.Epoch, Frames.Frame.ICRF);
        var spacecraft = new Spacecraft(-1001, "GMAT_CONFORMANCE", 1000.0, 10000.0, new Clock("clock", 256), orbit);
        var window = new Window(gmatCase.Epoch, gmatCase.Epoch.AddDays(1));
        var propagator = verlet
            ? new CentralBodyPropagator(window, spacecraft, gmatCase.ThirdBodies(), false, false,
                TimeSpan.FromSeconds(1.0))
            : new CentralBodyPropagator(window, spacecraft, new RK78Integrator(absoluteTolerance: 1e-11,
                    relativeTolerance: 1e-11, initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0),
                gmatCase.ThirdBodies(), false, false, TimeSpan.FromSeconds(10.0));
        var (position, velocity) = propagator.Propagate().InterpolateAt(window.EndDate.ToTDB());
        return new[] { position.X, position.Y, position.Z, velocity.X, velocity.Y, velocity.Z };
    }

    /// <summary>
    /// The initial and the final state of a GMAT report (km, km/s, EarthICRF), in m and m/s: the lines that start with a
    /// date, <c>dd Mon yyyy hh:mm:ss.fff</c>, then the six components.
    /// </summary>
    internal static (double[] Initial, double[] Final) ReadReport(string path)
    {
        var rows = File.ReadLines(path)
            .Where(line => line.Length > 0 && char.IsDigit(line[0]))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Skip(4).Select(x => double.Parse(x, CultureInfo.InvariantCulture) * 1000.0).ToArray())
            .ToArray();
        if (rows.Length != 2 || rows.Any(row => row.Length != 6))
        {
            throw new InvalidDataException($"{path}: expected two states of six components");
        }

        return (rows[0], rows[1]);
    }

    private static (double Position, double Velocity) LargestComponents(double[] actual, double[] expected)
    {
        double position = 0.0, velocity = 0.0;
        for (int i = 0; i < 3; i++)
        {
            position = System.Math.Max(position, System.Math.Abs(actual[i] - expected[i]));
            velocity = System.Math.Max(velocity, System.Math.Abs(actual[3 + i] - expected[3 + i]));
        }

        return (position, velocity);
    }
}
