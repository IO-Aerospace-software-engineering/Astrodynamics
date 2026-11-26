using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CLI.Cli;

/// <summary>The dimension of a value of the command line, which gives the units it may carry (R29 of #668).</summary>
internal enum Dimension
{
    /// <summary>A number without unit.</summary>
    None,

    /// <summary>m (no suffix) or km.</summary>
    Length,

    /// <summary>m/s (no suffix) or km/s.</summary>
    Speed,

    /// <summary>rad (no suffix) or deg.</summary>
    Angle,

    /// <summary>kg (no suffix).</summary>
    Mass,

    /// <summary>m² (no suffix, or m2).</summary>
    Area
}

/// <summary>
/// The one value parser of the command line (R29 of #668): numbers in invariant culture with an optional unit, steps, epochs,
/// enumeration values, objects and frames. Each value it cannot read throws <see cref="CliInputException"/> naming the
/// argument, exit code 2.
/// </summary>
internal static class CliValues
{
    private const string NumberPattern = @"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?";

    private static readonly Regex QuantityRegex = new($@"^\s*(?<number>{NumberPattern})\s*(?<unit>[A-Za-z][A-Za-z0-9/]*)?\s*\z",
        RegexOptions.CultureInvariant);

    private static readonly Regex StepRegex = new($@"^\s*(?<number>{NumberPattern})\s*(?<unit>s|min|h|d)\s*\z",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>The units of each dimension and their factor to SI and radians; the SI unit is also read unwritten.</summary>
    private static readonly Dictionary<Dimension, Dictionary<string, double>> Units = new()
    {
        [Dimension.None] = new Dictionary<string, double>(),
        [Dimension.Length] = new Dictionary<string, double> { ["m"] = 1.0, ["km"] = 1000.0 },
        [Dimension.Speed] = new Dictionary<string, double> { ["m/s"] = 1.0, ["km/s"] = 1000.0 },
        [Dimension.Angle] = new Dictionary<string, double> { ["rad"] = 1.0, ["deg"] = Constants.Deg2Rad },
        [Dimension.Mass] = new Dictionary<string, double> { ["kg"] = 1.0 },
        [Dimension.Area] = new Dictionary<string, double> { ["m2"] = 1.0 }
    };

    /// <summary>
    /// A value of <paramref name="dimension"/>: a number in invariant culture (a dot as decimal separator, no thousands
    /// separator), finite, followed by an optional unit of its dimension; without unit it is in SI and radians. A unit
    /// multiplies the number, so that a value without one is read bit for bit as before 11.0.0.
    /// </summary>
    internal static double Quantity(string text, Dimension dimension, string argument)
    {
        if (text == null) throw new CliInputException($"{argument}: a value is required.");
        var match = QuantityRegex.Match(text);
        if (!match.Success)
        {
            throw new CliInputException(
                $"{argument}: '{text}' is not a number{UnitHint(dimension)}; a number is written in invariant culture, a dot as decimal separator and no thousands separator.");
        }

        var value = double.Parse(match.Groups["number"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!double.IsFinite(value))
        {
            throw new CliInputException($"{argument}: '{text}' is not a finite number.");
        }

        var unit = match.Groups["unit"];
        if (!unit.Success)
        {
            return value;
        }

        if (!Units[dimension].TryGetValue(unit.Value, out var factor))
        {
            throw new CliInputException($"{argument}: '{unit.Value}' is not a unit of this value{UnitHint(dimension)}.");
        }

        var result = value * factor;
        if (!double.IsFinite(result))
        {
            throw new CliInputException($"{argument}: '{text}' is not a finite number.");
        }

        return result;
    }

    /// <summary>
    /// The values of a list, separated by blanks in one argument or given as several, one dimension each:
    /// <paramref name="dimensions"/> gives their number.
    /// </summary>
    internal static double[] Quantities(IEnumerable<string> texts, IReadOnlyList<Dimension> dimensions, string argument)
    {
        var tokens = texts.SelectMany(x => x.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).ToArray();
        if (tokens.Length != dimensions.Count)
        {
            throw new CliInputException($"{argument}: {dimensions.Count} values are expected, {tokens.Length} were given.");
        }

        return tokens.Select((x, i) => Quantity(x, dimensions[i], argument)).ToArray();
    }

    private static string UnitHint(Dimension dimension) => dimension switch
    {
        Dimension.None => " without unit",
        Dimension.Length => ", in m or km (m without unit)",
        Dimension.Speed => ", in m/s or km/s (m/s without unit)",
        Dimension.Angle => ", in rad or deg (rad without unit)",
        Dimension.Mass => ", in kg",
        Dimension.Area => ", in m2",
        _ => string.Empty
    };

    /// <summary>
    /// A step: <c>[d.]hh:mm:ss[.f]</c>, or a number and one of the units s, min, h and d (<c>60s</c>, <c>1.5min</c>,
    /// <c>12h</c>, <c>1d</c>), the nearest tick; above zero. A bare number is refused (R29 of #668): it was a number of
    /// days before 11.0.0.
    /// </summary>
    internal static TimeSpan Step(string text, string argument)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new CliInputException($"{argument}: a step is required.");
        TimeSpan step;
        var match = StepRegex.Match(text);
        if (match.Success)
        {
            var value = double.Parse(match.Groups["number"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            var seconds = match.Groups["unit"].Value.ToLowerInvariant() switch
            {
                "s" => value,
                "min" => value * 60.0,
                "h" => value * 3600.0,
                _ => value * 86400.0
            };
            if (!double.IsFinite(seconds) || System.Math.Abs(seconds) > TimeSpan.MaxValue.TotalSeconds / 2.0)
            {
                throw new CliInputException($"{argument}: '{text}' is not a step the command can take.");
            }

            step = Time.SecondsToSpan(seconds);
        }
        else if (text.Contains(':') && TimeSpan.TryParse(text.Trim(), CultureInfo.InvariantCulture, out step))
        {
        }
        else
        {
            throw new CliInputException(
                $"{argument}: '{text}' is not a step: [d.]hh:mm:ss[.f] or a number followed by s, min, h or d (60s, 12h, 00:01:00); a bare number has no unit.");
        }

        if (step <= TimeSpan.Zero)
        {
            throw new CliInputException($"{argument}: the step must be above zero, '{text}' was given.");
        }

        return step;
    }

    /// <summary>An epoch of the grammar of <see cref="Helpers.ConvertDateTimeInput"/> (D3 of #668).</summary>
    internal static Time Epoch(string text, string argument)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new CliInputException($"{argument}: an epoch is required.");
        try
        {
            return Helpers.ConvertDateTimeInput(text);
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            throw new CliInputException($"{argument}: {e.Message}", e);
        }
    }

    /// <summary>The window of two epochs, the end not before the start.</summary>
    internal static Window Window(string begin, string end)
    {
        var start = Epoch(begin, "--begin");
        var stop = Epoch(end, "--end");
        if (stop < start)
        {
            throw new CliInputException($"--end: the end {stop} is before the start {start}.");
        }

        return new Window(start, stop);
    }

    /// <summary>A member of <typeparamref name="T"/> by its description or its member name, in any case (#652).</summary>
    internal static T Enumeration<T>(string text, string argument) where T : struct, Enum
    {
        try
        {
            return Helpers.ParseEnumeration<T>(text?.Trim() ?? string.Empty, argument);
        }
        catch (ArgumentException e)
        {
            throw new CliInputException(e.Message.Replace($" (Parameter '{argument}')", string.Empty), e);
        }
    }

    /// <summary>
    /// One of <paramref name="values"/>, in any case, as the lower-case value; <paramref name="text"/> null gives null.
    /// </summary>
    internal static string Choice(string text, string argument, params string[] values)
    {
        if (text == null) return null;
        var value = text.Trim().ToLowerInvariant();
        if (!values.Contains(value))
        {
            throw new CliInputException($"{argument}: '{text}' is not one of {string.Join(", ", values)}.");
        }

        return value;
    }

    /// <summary>
    /// The NAIF identifier of an object: an integer (<c>399</c>, <c>-1000</c>), or a name SPICE knows, built in or given by
    /// the loaded kernels (<see cref="SpiceApi.TryGetNaifId"/>, choice 12 of #668). An identifier is not checked here: the
    /// spacecraft a propagation wrote, for instance, has no name.
    /// </summary>
    internal static int Object(string text, string argument)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new CliInputException($"{argument}: an object is required.");
        var trimmed = text.Trim();
        if (int.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var id))
        {
            return id;
        }

        if (SpiceApi.Instance.TryGetNaifId(trimmed, out id))
        {
            return id;
        }

        throw new CliInputException(
            $"{argument}: '{text}' is neither a NAIF identifier nor a name SPICE knows, built in or given by the loaded kernels.");
    }

    /// <summary>
    /// The frame of a name (<see cref="Helpers.ResolveFrame"/>: ICRF or J2000 is <see cref="Frame.ICRF"/>), one the library
    /// holds or SPICE knows from its built-in frames and the loaded kernels.
    /// </summary>
    internal static Frame Frame(string text, string argument)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new CliInputException($"{argument}: a frame is required.");
        var frame = Helpers.ResolveFrame(text.Trim());
        if (!SpiceApi.KnowsFrame(frame))
        {
            throw new CliInputException($"{argument}: '{text}' is not a frame of the library or of the loaded kernels.");
        }

        return frame;
    }

    /// <summary>
    /// Refuses the positional arguments a command does not take (an unquoted value split by the shell, a frame or an
    /// aberration in the positions of 10.x), which the parser would otherwise drop.
    /// </summary>
    internal static void NoMore(string[] unexpected)
    {
        if (unexpected is { Length: > 0 })
        {
            throw new CliInputException(
                $"Unexpected argument(s) '{string.Join(" ", unexpected)}': quote a value that holds spaces, and give everything but the objects of the command as options.");
        }
    }

    /// <summary>The scale of a <c>--time-scale</c>, or <paramref name="input"/> when none is given.</summary>
    internal static TimeScale TimeScale(string text, TimeScale input)
    {
        return Choice(text, "--time-scale", "tdb", "tt", "tai", "utc", "gps") switch
        {
            null => input,
            var x => Helpers.ScaleOfTag(x)
        };
    }
}
