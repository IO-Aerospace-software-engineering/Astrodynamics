using System;
using System.Globalization;
using System.Threading.Tasks;
using Cocona;
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
    /// machine's zone, written in ISO only.
    /// </summary>
    /// <remarks>
    /// Before 11.0.0 one of <c>-t</c>, <c>-u</c>, <c>-l</c> and one of <c>-j</c>, <c>-e</c>, <c>-d</c> were required, an
    /// exclusive or that three flags also passed, and TT, TAI and GPS could not be asked for.
    /// </remarks>
    /// <exception cref="ArgumentException">A scale or a format is unknown, two of them are asked for, local time with
    /// another format than ISO, or an argument follows the epoch (an unquoted tag, which the epoch would otherwise lose).</exception>
    [Command("time-converter", Description = "Convert an epoch to a time scale (tdb, tt, tai, utc, gps or local) and a format (iso, jd or seconds from J2000)")]
    public Task TimeConverter(
        EpochParameters epochParameters,
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
        [Argument(Description = "Nothing: an epoch with a space is quoted (\"2024-01-01T00:00:00 TAI\")")]
        string[] unexpected = null)
    {
        if (unexpected is { Length: > 0 })
        {
            // An unquoted tag would be read as a second argument and the epoch taken untagged, as TDB (D3.7 of #668)
            throw new ArgumentException(
                $"Unexpected argument(s) '{string.Join(" ", unexpected)}': quote an epoch that holds a space, as \"{epochParameters.Epoch} {string.Join(" ", unexpected)}\".",
                nameof(unexpected));
        }

        string scale = Choose("scale", to, ("tdb", toTDB), ("utc", toUTC), ("local", toLocal)) ?? "tdb";
        string output = Choose("format", format, ("jd", toJulian), ("seconds", toSecondsFromJ2000), ("iso", toDateTime)) ?? "iso";
        if (output is not ("iso" or "jd" or "seconds"))
        {
            throw new ArgumentException($"Unknown format '{format}': iso, jd or seconds.", nameof(format));
        }

        var input = Helpers.ConvertDateTimeInput(epochParameters.Epoch);
        string res;
        if (scale == "local")
        {
            if (output != "iso")
            {
                throw new ArgumentException("Local time is written in ISO only: give --format iso.", nameof(format));
            }

            // A local time is not a scale: the UTC instant in the zone of the machine
            res = input.ToUTC().DateTime.ToLocalTime().ToString("O", CultureInfo.InvariantCulture);
        }
        else
        {
            var converted = input.ConvertTo(Helpers.ScaleOfTag(scale is "tdb" or "tt" or "tai" or "utc" or "gps"
                ? scale
                : throw new ArgumentException($"Unknown scale '{to}': tdb, tt, tai, utc, gps or local.", nameof(to))));
            res = output switch
            {
                "jd" => $"{converted.ToJulianDate()} JD",
                "seconds" => $"{converted.TimeSpanFromJ2000().TotalSeconds}",
                _ => converted.ToString()
            };
        }

        Console.WriteLine(res);
        return Task.CompletedTask;
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
                throw new ArgumentException($"Two {what}s are asked for: {chosen} and {alias.Value}. Give one.");
            }

            chosen = alias.Value;
        }

        return chosen;
    }
}
