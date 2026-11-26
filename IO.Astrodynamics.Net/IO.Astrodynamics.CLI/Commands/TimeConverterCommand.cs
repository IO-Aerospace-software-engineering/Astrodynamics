using System;
using System.Globalization;
using System.Threading.Tasks;
using Cocona;
using IO.Astrodynamics.CLI.Cli;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CLI.Commands;

public class TimeConverterCommand
{
    public TimeConverterCommand()
    {
    }

    /// <summary>
    /// Converts an epoch to a time scale and writes it in a format (D3.7 of #668, decided by S. Guillet, 2026-11-19):
    /// <c>--to tdb|tt|tai|utc|gps|local</c> and <c>--format iso|jd|seconds</c>, TDB and ISO by default; <c>-t</c>,
    /// <c>-u</c>, <c>-l</c>, <c>-j</c>, <c>-e</c> and <c>-d</c> are aliases of <c>--to tdb</c>, <c>--to utc</c>,
    /// <c>--to local</c>, <c>--format jd</c>, <c>--format seconds</c> and <c>--format iso</c>. Local is the calendar of the
    /// machine's zone, written in ISO only. A Julian date is written <c>n JD TAG</c> and seconds <c>s TAG</c>, the tag of
    /// the target scale, so that the command reads its output back as the same instant.
    /// </summary>
    /// <remarks>
    /// Before 11.0.0 one of <c>-t</c>, <c>-u</c>, <c>-l</c> and one of <c>-j</c>, <c>-e</c>, <c>-d</c> were required, an
    /// exclusive or that three flags also passed, and TT, TAI and GPS could not be asked for.
    /// </remarks>
    /// <remarks>An unknown scale or format, two of them, local time with another format than ISO, or an argument after the
    /// epoch (an unquoted tag, which the epoch would otherwise lose) is an input error, exit code 2 (R29 of #668).</remarks>
    [Command("time-converter", Description = "Converts an epoch to a time scale (tdb, tt, tai, utc, gps or local) and a format (iso, jd or seconds from J2000)")]
    public int TimeConverter(
        [Argument("epoch", Description = "Epoch: " + EpochOptions.Forms + "; an epoch with a space is quoted")]
        string epoch,
        [Option('t', Description = "Convert to TDB (alias of --to tdb)")]
        bool toTDB = false,
        [Option('u', Description = "Convert to UTC (alias of --to utc)")]
        bool toUTC = false,
        [Option('l', Description = "Convert to the local time of the machine, ISO only (alias of --to local)")]
        bool toLocal = false,
        [Option('j', Description = "Write the Julian date (alias of --format jd)")]
        bool toJulian = false,
        [Option('e', Description = "Write the seconds from J2000 of the target scale (alias of --format seconds)")]
        bool toSecondsFromJ2000 = false,
        [Option('d', Description = "Write the ISO 8601 calendar (alias of --format iso)")]
        bool toDateTime = false,
        [Option("to", Description = "Target scale: tdb, tt, tai, utc, gps or local (tdb by default)")]
        string to = null,
        [Option("format", Description = "Output format: iso, jd or seconds (iso by default)")]
        string format = null,
        [Option("verbose", Description = "Write the trace of an error after its message")]
        bool verbose = false,
        [Argument("unexpected", Description = "Nothing: an epoch with a space is quoted (\"2024-01-01T00:00:00 TAI\")")]
        string[] unexpected = null)
    {
        return CliContext.Execute(verbose, () => Convert(epoch, toTDB, toUTC, toLocal, toJulian, toSecondsFromJ2000, toDateTime, to, format, unexpected));
    }

    private static void Convert(string epoch, bool toTDB, bool toUTC, bool toLocal, bool toJulian, bool toSecondsFromJ2000, bool toDateTime,
        string to, string format, string[] unexpected)
    {
        if (unexpected is { Length: > 0 })
        {
            // An unquoted tag would be read as a second argument and the epoch taken untagged, as TDB (D3.7 of #668)
            throw new CliInputException(
                $"Unexpected argument(s) '{string.Join(" ", unexpected)}': quote an epoch that holds a space, as \"{epoch} {string.Join(" ", unexpected)}\".");
        }

        string scale = Choose("scale", to, ("tdb", toTDB), ("utc", toUTC), ("local", toLocal)) ?? "tdb";
        string output = Choose("format", format, ("jd", toJulian), ("seconds", toSecondsFromJ2000), ("iso", toDateTime)) ?? "iso";
        if (output is not ("iso" or "jd" or "seconds"))
        {
            throw new CliInputException($"Unknown format '{format}': iso, jd or seconds.");
        }

        var input = CliValues.Epoch(epoch, "epoch");
        string res;
        if (scale == "local")
        {
            if (output != "iso")
            {
                throw new CliInputException("Local time is written in ISO only: give --format iso.");
            }

            // A local time is not a scale: the UTC instant in the zone of the machine
            res = input.ToUtc().DateTime.ToLocalTime().ToString("O", CultureInfo.InvariantCulture);
        }
        else
        {
            var converted = input.ConvertTo(Helpers.ScaleOfTag(scale is "tdb" or "tt" or "tai" or "utc" or "gps"
                ? scale
                : throw new CliInputException($"Unknown scale '{to}': tdb, tt, tai, utc, gps or local.")));
            // Every output names its scale, in a form the command reads back (D3.4 of #668: no implicit scale): n JD TAG and
            // seconds TAG (the names of TimeScale are the tags), the numbers in invariant culture
            string tag = converted.Scale.ToString();
            res = output switch
            {
                "jd" => $"{converted.ToJulianDate().ToString(CultureInfo.InvariantCulture)} JD {tag}",
                "seconds" => $"{converted.TimeSpanFromJ2000().TotalSeconds.ToString(CultureInfo.InvariantCulture)} {tag}",
                _ => converted.ToString()
            };
        }

        CliContext.Out.WriteLine(res);
    }

    // The value of an option and of its aliases, in lower case: null when none is given, the one given otherwise
    private static string Choose(string what, string option, params (string Value, bool Given)[] aliases)
    {
        string chosen = option?.Trim().ToLowerInvariant();
        foreach (var alias in aliases)
        {
            if (!alias.Given)
            {
                continue;
            }

            if (chosen != null && chosen != alias.Value)
            {
                throw new CliInputException($"Two {what}s are asked for: {chosen} and {alias.Value}. Give one.");
            }

            chosen = alias.Value;
        }

        return chosen;
    }
}
