using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Tests.OrbitalParameters;
using Xunit.Abstractions;

namespace IO.Astrodynamics.CLI.Tests;

public class OrbitalParametersTests
{
    private readonly ITestOutputHelper _output;

    public OrbitalParametersTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // The input of the state vector conversions, relative to the Sun, at J2000 TDB, in ICRF
    private static OrbitalParameters.StateVector InputState()
    {
        return new OrbitalParameters.StateVector(new Math.Vector3(-26499033677.42509, 132757417338.33946, 57556718470.53819),
            new Math.Vector3(-29794.26007, -5018.05231, -2175.39380), new Body.CelestialBody(10), TimeSystem.Time.J2000TDB,
            Frames.Frame.ICRF);
    }

    [Fact]
    public void SvToSv()
    {
        using (Configuration.Lock())
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data",
                new Commands.Parameters.OrbitalParameters
                {
                    OrbitalParametersValues = "-26499033677.42509 132757417338.33946 57556718470.53819 -29794.26007 -5018.05231 -2175.39380", CenterOfMotionId = 10,
                    OrbitalParametersEpoch = "0.0",
                    Frame = "ICRF", FromStateVector = true, FromEquinoctial = false, FromKeplerian = false, FromTLE = false
                }, true, false, false, new EpochParameters { Epoch = "0.0" }, "ICRF");
            var res = sb.ToString();

            // The same epoch and frame: the input itself, to rounding (the Keplerian round trip moved it by 0.72 m, #368)
            OutputAssert.EqualApprox(
                $"{InputState()}{Environment.NewLine}",
                res, 1e-14);
        }
    }
    
    [Fact]
    public void SvToSvToDifferentFrame()
    {
        using (Configuration.Lock())
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data",
                new Commands.Parameters.OrbitalParameters
                {
                    OrbitalParametersValues = "-26499033677.42509 132757417338.33946 57556718470.53819 -29794.26007 -5018.05231 -2175.39380", CenterOfMotionId = 10,
                    OrbitalParametersEpoch = "0.0",
                    Frame = "ICRF", FromStateVector = true, FromEquinoctial = false, FromKeplerian = false, FromTLE = false
                }, true, false, false, new EpochParameters { Epoch = "0.0" }, "ECLIPJ2000");
            var res = sb.ToString();

            // The input rotated to the ecliptic, without the Keplerian round trip. Per number, relative: the rounding of
            // the round trip, 1e-15 of the 1.4e11 m vector, is 6e-12 of its small ecliptic z (6e5 m); SvToSv checks the
            // round trip itself at 1e-14
            OutputAssert.EqualApprox(
                $"{InputState().ToFrame(new Frames.Frame("ECLIPJ2000")).ToStateVector()}{Environment.NewLine}",
                res, 1e-10);
        }
    }

    [Fact]
    public void SvToKeplerian()
    {
        using (Configuration.Lock())
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data",
                new Commands.Parameters.OrbitalParameters
                {
                    OrbitalParametersValues = "-26499033677.42509 132757417338.33946 57556718470.53819 -29794.26007 -5018.05231 -2175.39380", CenterOfMotionId = 10,
                    OrbitalParametersEpoch = "0.0",
                    Frame = "ICRF", FromStateVector = true, FromEquinoctial = false, FromKeplerian = false, FromTLE = false
                },
                false, true, false, new EpochParameters());
            var res = sb.ToString();

            // Against SPICE OSCELT
            OutputAssert.EqualApprox(
                $"{SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(InputState())}{Environment.NewLine}",
                res, 1e-14);
        }
    }

    [Fact]
    public void KeplerianToEquinocial()
    {
        using (Configuration.Lock())
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data", new Commands.Parameters.OrbitalParameters
            {
                OrbitalParametersValues = "13560000.0 0.5 0.17453292519943295 0.26179938779914941 0.52359877559829882 0.78539816339744828", CenterOfMotionId = 399,
                OrbitalParametersEpoch = "0.0",
                Frame = "ICRF", FromStateVector = false, FromKeplerian = true, FromEquinoctial = false, FromTLE = false
            }, false, false, true, new EpochParameters());
            var res = sb.ToString();
            // Numbers compared within tolerance so the same expected value holds across
            // architectures (last-digit round-trip formatting differs on x64 vs arm64).
            OutputAssert.EqualApprox(
                $"Epoch : 2000-01-01T12:00:00.0000000 TDB P : 10170000 F : 0.3535533905932738 G : 0.35355339059327373 H : 0.08450755960720442 K 0.022643732351075387 L0 : 2.589226534411255 Frame : J2000{Environment.NewLine}",
                res);
        }
    }

    [Fact]
    public void KeplerianToSv()
    {
        using (Configuration.Lock())
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data", new Commands.Parameters.OrbitalParameters
            {
                OrbitalParametersValues = "6800803.5449581668 0.001353139738203394 0.90267066832323262 0.56855938608714662 1.8545420365902201 0.7925932793200029",
                CenterOfMotionId = 399, OrbitalParametersEpoch = "0.0",
                Frame = "ICRF", FromStateVector = false, FromKeplerian = true, FromEquinoctial = false, FromTLE = false
            }, true, false, false, new EpochParameters());
            var res = sb.ToString();

            // Numbers compared within tolerance so the same expected value holds across
            // architectures (last-digit round-trip formatting differs on x64 vs arm64).
            OutputAssert.EqualApprox(
                $"Epoch : 2000-01-01T12:00:00.0000000 TDB Position : X : -6116559.469556894 Y : -1546174.6986767196 Z: 2521950.1574303135 Velocity : X : -807.8383054672163 Y : -5477.64627908088 Z: -5297.633404926932 Frame : J2000{Environment.NewLine}",
                res);
        }
    }

    [Fact]
    public void KeplerianToKeplerian()
    {
        using (Configuration.Lock())
        {
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);
            command.Converter("Data", new Commands.Parameters.OrbitalParameters
            {
                OrbitalParametersValues = "6800803.5449581668 0.001353139738203394 0.90267066832323262 0.56855938608714662 1.8545420365902201 0.7925932793200029",
                CenterOfMotionId = 399, OrbitalParametersEpoch = "0.0", Frame = "ICRF", FromStateVector = false, FromKeplerian = true, FromEquinoctial = false,
                FromTLE = false
            }, false, true, false, new EpochParameters{Epoch = "3600.0"});
            var res = sb.ToString();

            Assert.Equal(
                $"Epoch : 2000-01-01T13:00:00.0000000 TDB A : 6800803.544958167, Ecc. : 0.001353139738203394, Inc. : 0.9026706683232326, AN : 0.5685593860871466, AOP : 1.85454203659022, M : 4.845168091449668, Frame : J2000{Environment.NewLine}",
                res);
        }
    }

    public static TheoryData<string, double> EquinoctialInputs => new()
    {
        { "circular, i 30, node 40 (suite 155)", 0.0 }, { "circular, i 30, node 40 (suite 155)", 600.0 },
        { "equatorial, e 0.05, lop 120 (suite 156)", 0.0 }, { "equatorial, e 0.05, lop 120 (suite 156)", 600.0 },
        { "suite 151, generic", 0.0 }, { "suite 151, generic", 600.0 }
    };

    [Theory]
    [MemberData(nameof(EquinoctialInputs))]
    public void EquinoctialToSv_CircularAndEquatorial(string name, double dt)
    {
        using (Configuration.Lock())
        {
            // Arrange: --from-equinoctial with the elements of the conformance cases 155 (circular), 156 (equatorial)
            // and 151, at J2000 TDB. Before #452, the state of 155 was 4.79e6 m off, that of 156 1.32e7 m
            var reference = EquinoctialReference.Load();
            var c = reference.Cli.Single(x => x.Name == name);
            var command = new OrbitalParametersConverterCommand();
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            Console.SetOut(sw);

            // Act: to a state vector at the epoch of the elements and 600 s later, through AtEpoch (K)
            command.Converter("Data", new Commands.Parameters.OrbitalParameters
            {
                OrbitalParametersValues = c.CliValues, CenterOfMotionId = 399, OrbitalParametersEpoch = "0.0",
                Frame = "ICRF", FromStateVector = false, FromKeplerian = false, FromEquinoctial = true, FromTLE = false
            }, true, false, false, new EpochParameters { Epoch = dt.ToString("R", CultureInfo.InvariantCulture) },
                "ICRF");
            var res = sb.ToString();

            // Assert against the 60-digit state, with the GM of the kernels of these tests (gm_de431)
            var earth = new Body.CelestialBody(399);
            Assert.Equal(reference.Gm["gm_de431"], earth.GM);
            Assert.Contains("Frame : J2000", res);
            var state = ParseState(res, earth, TimeSystem.Time.J2000TDB.AddSeconds(dt));
            var (position, velocity) = c.At(dt).RelativeError(state);
            _output.WriteLine($"MEASURE #452 EquinoctialToSv_CircularAndEquatorial, {name}, dt {dt} s: position " +
                              $"{position:E2}, velocity {velocity:E2} (relative)");
            Assert.True(position < EquinoctialPosition, $"position {position:E2}");
            Assert.True(velocity < EquinoctialVelocity, $"velocity {velocity:E2}");
        }
    }

    // The bounds of EquinoctialToSv_CircularAndEquatorial, relative: twice the worst over Linux, Windows and macOS
    // arm64 (CI run 40280932636, 2026-10-26), and measured again after #453: 4.37e-16 and 4.09e-16 (suite 156, 600 s;
    // Linux)
    private const double EquinoctialPosition = 8.8e-16;
    private const double EquinoctialVelocity = 8.2e-16;

    private static readonly Regex StateRx = new(
        @"Position : X : (\S+) Y : (\S+) Z: (\S+) Velocity : X : (\S+) Y : (\S+) Z: (\S+) Frame");

    /// <summary>The state vector that the converter prints, every number written to round trip.</summary>
    private static OrbitalParameters.StateVector ParseState(string output, Body.CelestialBody observer,
        TimeSystem.Time epoch)
    {
        var match = StateRx.Match(output);
        Assert.True(match.Success, output);
        double Number(int group) => double.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
        return new OrbitalParameters.StateVector(new Math.Vector3(Number(1), Number(2), Number(3)),
            new Math.Vector3(Number(4), Number(5), Number(6)), observer, epoch, Frames.Frame.ICRF);
    }

    [Fact]
    public async Task Exceptions()
    {
        var command = new OrbitalParametersConverterCommand();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            command.Converter("Data", new Commands.Parameters.OrbitalParameters
            {
                OrbitalParametersValues = "6800000.0 0.0 0.0 0.0 7000.0 0.0", CenterOfMotionId = 10, OrbitalParametersEpoch = "0.0", Frame = "ICRF",
                FromStateVector = true,
                FromKeplerian = true, FromEquinoctial = false, FromTLE = false
            }, true, false, false, new EpochParameters()));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            command.Converter("Data", new Commands.Parameters.OrbitalParameters
            {
                OrbitalParametersValues = "6800000.0 0.0 0.0 0.0 7000.0 0.0", CenterOfMotionId = 10, OrbitalParametersEpoch = "0.0", Frame = "ICRF",
                FromStateVector = false,
                FromKeplerian = false, FromEquinoctial = false, FromTLE = false
            }, true, false, false, new EpochParameters()));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            command.Converter("Data", new Commands.Parameters.OrbitalParameters
            {
                OrbitalParametersValues = "6800000.0 0.0 0.0 0.0 7000.0 0.0", CenterOfMotionId = 10, OrbitalParametersEpoch = "0.0",
                Frame = "ICRF",
                FromStateVector = true,
                FromKeplerian = false, FromEquinoctial = false, FromTLE = false
            }, false, false, false, new EpochParameters()));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            command.Converter("Data", new Commands.Parameters.OrbitalParameters
            {
                OrbitalParametersValues = "6800000.0 0.0 0.0 0.0 7000.0 0.0", CenterOfMotionId = 10, OrbitalParametersEpoch = "0.0",
                Frame = "ICRF",
                FromStateVector = true,
                FromKeplerian = false, FromEquinoctial = false, FromTLE = false
            }, true, true, false, new EpochParameters()));
    }
}