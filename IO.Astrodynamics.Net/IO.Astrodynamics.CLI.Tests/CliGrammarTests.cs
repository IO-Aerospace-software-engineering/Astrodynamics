using System.Globalization;
using IO.Astrodynamics.CLI.Cli;

namespace IO.Astrodynamics.CLI.Tests;

/// <summary>
/// The grammar of the command line (R29 of #668): one value parser, the exit codes (0, 2 for the input, 3 for the kernels,
/// 1 otherwise), the errors on one line of the standard error, nothing but the data on the standard output.
/// </summary>
public class CliGrammarTests
{
    [Theory]
    [InlineData("60s", 60.0)]
    [InlineData("1.5min", 90.0)]
    [InlineData("12h", 43200.0)]
    [InlineData("1d", 86400.0)]
    [InlineData("2D", 172800.0)]
    [InlineData("0.25 s", 0.25)]
    [InlineData("1e-3s", 0.001)]
    [InlineData("00:01:00", 60.0)]
    [InlineData("1.00:00:00", 86400.0)]
    [InlineData("00:00:00.5", 0.5)]
    public void Step_TakesAColonOrAUnit(string text, double seconds)
    {
        Assert.Equal(TimeSystem.Time.SecondsToSpan(seconds), CliValues.Step(text, "--step"));
    }

    /// <summary>A bare number (60 days before 11.0.0), a step of zero or less and an unknown unit are input errors.</summary>
    [Theory]
    [InlineData("60")]
    [InlineData("60m")]
    [InlineData("0s")]
    [InlineData("-1h")]
    [InlineData("00:00:00")]
    [InlineData("-00:01:00")]
    [InlineData("1 hour")]
    [InlineData("")]
    public void Step_Refused(string text)
    {
        Assert.Throws<CliInputException>(() => CliValues.Step(text, "--step"));
    }

    [Theory]
    [InlineData("400000000", "Length", 400000000.0)]
    [InlineData("400000km", "Length", 400000000.0)]
    [InlineData("-1.5e3m", "Length", -1500.0)]
    [InlineData("7.5km/s", "Speed", 7500.0)]
    [InlineData("0.5", "Angle", 0.5)]
    [InlineData("100kg", "Mass", 100.0)]
    [InlineData("2m2", "Area", 2.0)]
    [InlineData(".5", "None", 0.5)]
    public void Quantity_InSiOrWithAUnitOfItsDimension(string text, string dimension, double expected)
    {
        Assert.Equal(expected, CliValues.Quantity(text, Enum.Parse<Dimension>(dimension), "--value"));
    }

    [Fact]
    public void Quantity_InDegrees_IsTheProductByDeg2Rad()
    {
        Assert.Equal(30.0 * Constants.Deg2Rad, CliValues.Quantity("30deg", Dimension.Angle, "--value"));
    }

    /// <summary>
    /// A number of another culture (#814: "6800000,0" was read as 68000000 in an English culture, and "0,5" as 0.5 in a
    /// French one), a thousands separator, a value that is not finite, a unit of another dimension: input errors.
    /// </summary>
    [Theory]
    [InlineData("6800000,0", "Length")]
    [InlineData("0,5", "None")]
    [InlineData("1,000", "None")]
    [InlineData("NaN", "None")]
    [InlineData("Infinity", "Length")]
    [InlineData("1e400", "Length")]
    [InlineData("1e306km", "Length")]
    [InlineData("5deg", "Length")]
    [InlineData("5km", "None")]
    [InlineData("5 parsec", "Length")]
    public void Quantity_Refused(string text, string dimension)
    {
        Assert.Throws<CliInputException>(() => CliValues.Quantity(text, Enum.Parse<Dimension>(dimension), "--value"));
    }

    [Fact]
    public void Quantity_IsReadInInvariantCultureWhateverTheCultureOfTheMachine()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            // A culture of comma decimals and dot thousands, built so that the test runs where only the invariant culture
            // exists (globalization-invariant mode)
            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            comma.NumberFormat.NumberGroupSeparator = ".";
            Assert.Equal(0.5, double.Parse("0,5", comma));
            CultureInfo.CurrentCulture = comma;
            Assert.Equal(6800000.5, CliValues.Quantity("6800000.5", Dimension.Length, "--orbit"));
            Assert.Throws<CliInputException>(() => CliValues.Quantity("6800000,5", Dimension.Length, "--orbit"));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>
    /// Each option is joined to its value and the positional arguments follow <c>--</c>, the command first, so that a
    /// negative number is a value (R29 of #668).
    /// </summary>
    [Fact]
    public void Normalize_KeepsNegativeNumbersAsValues()
    {
        Assert.Equal(new[] { "ephemeris", "--kernels=k", "--begin=0", "--value=-5deg", "--", "-1000", "EARTH" },
            Program.Normalize(["ephemeris", "-1000", "-K", "k", "EARTH", "--begin", "0", "--value", "-5deg"]));
        Assert.Equal(new[] { "propagate", "--drag", "--srp", "--format=text", "--", "S" },
            Program.Normalize(["propagate", "--drag", "S", "--srp", "--format=text"]));
        Assert.Equal(new[] { "time-converter", "-t", "-e", "--", "-1.5e3 UTC" },
            Program.Normalize(["time-converter", "-1.5e3 UTC", "-t", "-e"]));
        Assert.Equal(new[] { "time-converter", "--", "--", "x" },
            Program.Normalize(["time-converter", "--", "--", "x"]));
        Assert.Equal(new[] { "--version" },
            Program.Normalize(["--version"]));
    }

    [Fact]
    public void HelpAndVersion_ExitWith0_OnTheStandardOutput()
    {
        using (Configuration.Lock())
        {
            var help = CliRun.Run("--help");
            Assert.Equal(0, help.ExitCode);
            Assert.StartsWith("Usage: astro [command]", help.Out);
            Assert.Equal(string.Empty, help.Error);

            var commandHelp = CliRun.Run("ephemeris", "--help");
            Assert.Equal(0, commandHelp.ExitCode);
            Assert.StartsWith("Usage: astro ephemeris ", commandHelp.Out);

            var version = CliRun.Run("--version");
            Assert.Equal(0, version.ExitCode);
            Assert.Equal($"astro {typeof(Program).Assembly.GetName().Version!.ToString(3)}{Environment.NewLine}", version.Out);

            // No banner: the first line of a command is its data (before 11.0.0, "IO.Astrodynamics.CLI v11.0.0 experimental"
            // on every run)
            Assert.Equal($"2000-01-01T12:00:00.0000000 TDB{Environment.NewLine}", CliRun.Ok("time-converter", "0"));
        }
    }

    /// <summary>
    /// What the parser rejects (an unknown command or option, a missing argument, an option without its value) is an
    /// input error, exit code 2 (before 11.0.0: 129 for an unknown option, 1 for a missing argument).
    /// </summary>
    [Theory]
    [InlineData("ephemerides")]
    [InlineData("ephemeris", "301", "399", "-K", "Data", "--begin", "0", "--end", "60", "--step", "60s", "--bogus", "3")]
    [InlineData("ephemeris", "301", "-K", "Data", "--begin", "0", "--end", "60", "--step", "60s")]
    [InlineData("ephemeris", "301", "399", "-K", "Data", "--begin", "0", "--end", "60")]
    [InlineData("time-converter")]
    public void WhatTheParserRejects_IsAnInputError(params string[] args)
    {
        using (Configuration.Lock())
        {
            var result = CliRun.Run(args);
            Assert.Equal(2, result.ExitCode);
            Assert.Equal(string.Empty, result.Out);
            Assert.NotEqual(string.Empty, result.Error);
        }
    }

    /// <summary>
    /// --help among the arguments of an error is no help (review of L8): an unknown command, or --help taken as the value
    /// of an option, exits 2; an option given last without its value is an input error, not the value "--".
    /// </summary>
    [Theory]
    [InlineData("body-information", "--help")]
    [InlineData("ephemeris", "399", "301", "--frame", "--help")]
    [InlineData("celestial-body-info", "399", "-K")]
    [InlineData("celestial-body-info", "399", "-K", "Data", "--format")]
    [InlineData("time-converter", "0", "--to")]
    public void HelpInAnError_AndAnOptionWithoutItsValue_AreInputErrors(params string[] args)
    {
        using (Configuration.Lock())
        {
            var result = CliRun.Run(args);
            Assert.Equal(2, result.ExitCode);
            Assert.StartsWith("Error: ", result.Error);
        }
    }

    /// <summary>
    /// The progress line of propagate --verbose reaches the standard error before the run ends, not after its output
    /// (second review of L8: the buffered errors delivered it last).
    /// </summary>
    [Fact]
    public void VerboseProgress_PrecedesTheOutput()
    {
        using (Configuration.Lock())
        {
            var writer = new OrderWriter();
            var code = Program.Run(["propagate", "PV", "-K", "Data", "--output", "PV", "--from", "state", "--orbit", "6800km 0 0 0 7.6km/s 0",
                "--center", "EARTH", "--epoch", "0", "--frame", "ICRF", "--begin", "0", "--end", "60", "--verbose"], writer, writer);
            Assert.Equal(0, code);
            Assert.StartsWith("Propagation in progress.", writer.ToString());
            Assert.Contains("NAIF id : -1000", writer.ToString());
        }
    }

    // One writer for the output and the error, which keeps the order of their lines
    private sealed class OrderWriter : StringWriter
    {
    }

    /// <summary>The aliases of time-converter bundled behind one minus sign read as each of them.</summary>
    [Fact]
    public void BundledAliases_AreFlags()
    {
        using (Configuration.Lock())
        {
            Assert.Equal(CliRun.Ok("time-converter", "0", "-t", "-j"), CliRun.Ok("time-converter", "-tj", "0"));
            Assert.Equal(new[] { "time-converter", "-ue", "--", "0" }, Program.Normalize(["time-converter", "-ue", "0"]));
        }
    }

    /// <summary>An error is one line on the standard error; --verbose adds its trace.</summary>
    [Fact]
    public void AnError_IsOneLine_AndVerboseAddsItsTrace()
    {
        using (Configuration.Lock())
        {
            string[] args = ["ephemeris", "301", "399", "-K", "Data", "--begin", "2160-01-01T00:00:00", "--end", "2160-01-01T00:01:00", "--step", "60s"];
            var error = CliRun.Fails(1, args);
            Assert.Single(error.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
            Assert.StartsWith("Error: ", error);

            var verbose = CliRun.Fails(1, [.. args, "--verbose"]);
            Assert.StartsWith(error, verbose);
            Assert.Contains("   at ", verbose);
        }
    }

    [Fact]
    public void Csv_QuotesAFieldWithACommaOrAQuote()
    {
        var writer = new StringWriter();
        CliOutput.WriteCsv(writer, [new CliOutput.Field("a", null), new CliOutput.Field("b", null)],
            [[new CliOutput.Field("a", "x,y"), new CliOutput.Field("b", "say \"z\"")]]);
        Assert.Equal($"a,b{Environment.NewLine}\"x,y\",\"say \"\"z\"\"\"{Environment.NewLine}", writer.ToString());
    }
}
