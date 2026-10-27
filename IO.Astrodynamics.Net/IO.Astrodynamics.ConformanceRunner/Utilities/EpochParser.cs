using System;
using System.Globalization;
using System.Text.RegularExpressions;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

/// <summary>
/// The tagged epoch of spec.md section 3, used by the conversion categories: <c>YYYY-MM-DDThh:mm:ss[.fff] TAG</c>, with
/// 0 to 3 decimals, exactly one space and a tag among UTC, TAI, TT and TDB. Second 60 is never written.
/// </summary>
/// <remarks>
/// The epoch is first matched against the pattern of <c>tagged_epoch</c> in <c>schemas/v1/case.schema.json</c>, so that
/// the parser rejects whatever the schema rejects, then read by <see cref="DateTime.ParseExact(string, string[], IFormatProvider, DateTimeStyles)"/>
/// with the invariant culture, which also rejects a date the pattern lets through (30 February). TT is the framework's
/// TDT.
/// </remarks>
public static class EpochParser
{
    /// <summary>
    /// The pattern of <c>tagged_epoch</c> in the case schema, copied as it is; a test compares it with the schema of the
    /// lot-1 fixture.
    /// </summary>
    public const string Pattern =
        @"^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\.[0-9]{1,3})? (UTC|TAI|TT|TDB)$";

    private static readonly Regex Expression = new(Pattern, RegexOptions.CultureInvariant);

    private static readonly string[] Formats =
    {
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss'.'f",
        "yyyy-MM-dd'T'HH:mm:ss'.'ff",
        "yyyy-MM-dd'T'HH:mm:ss'.'fff"
    };

    /// <summary>
    /// The instant of a tagged epoch, in the time frame of its tag.
    /// </summary>
    /// <exception cref="FormatException">The epoch is not a tagged epoch of spec.md section 3.</exception>
    public static Time Parse(string epoch)
    {
        var (calendar, scale) = Split(epoch);
        DateTime dateTime;
        try
        {
            dateTime = DateTime.ParseExact(calendar, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None);
        }
        catch (FormatException ex)
        {
            throw new FormatException($"The epoch '{epoch}' is not a date of the calendar: {ex.Message}", ex);
        }

        return new Time(dateTime, Frame(scale));
    }

    /// <summary>
    /// An instant of a golden (spec.md section 3): a tagged epoch, or, in the goldens of the earlier categories before
    /// conformance-tests#25, an ISO 8601 UTC instant ending in <c>Z</c> (<c>2026-03-20T00:18:37.494Z</c>), with up to 7
    /// decimals.
    /// </summary>
    /// <exception cref="FormatException">The instant is neither form.</exception>
    public static Time ParseInstant(string instant)
    {
        if (instant != null && instant.EndsWith('Z'))
        {
            var dateTime = DateTime.ParseExact(instant[..^1], ZuluFormats, CultureInfo.InvariantCulture, DateTimeStyles.None);
            return new Time(dateTime, TimeFrame.UTCFrame);
        }

        return Parse(instant);
    }

    private static readonly string[] ZuluFormats =
    {
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss'.'FFFFFFF"
    };

    /// <summary>
    /// The tag of a tagged epoch: UTC, TAI, TT or TDB.
    /// </summary>
    /// <exception cref="FormatException">The epoch is not a tagged epoch of spec.md section 3.</exception>
    public static string Scale(string epoch)
    {
        return Split(epoch).Scale;
    }

    private static (string Calendar, string Scale) Split(string epoch)
    {
        // In .NET, $ also matches before a final newline, which the schema's ECMA-262 pattern does not: the match must
        // cover the whole string
        var match = epoch == null ? Match.Empty : Expression.Match(epoch);
        if (!match.Success || match.Length != epoch.Length)
        {
            throw new FormatException(
                $"The epoch '{epoch}' is not a tagged epoch of spec.md section 3: YYYY-MM-DDThh:mm:ss with 0 to 3 decimals, one space and UTC, TAI, TT or TDB.");
        }

        int space = epoch.IndexOf(' ');
        return (epoch[..space], epoch[(space + 1)..]);
    }

    private static ITimeFrame Frame(string scale)
    {
        // The pattern admits no other tag than these four
        return scale switch
        {
            "UTC" => TimeFrame.UTCFrame,
            "TAI" => TimeFrame.TAIFrame,
            "TT" => TimeFrame.TDTFrame,
            _ => TimeFrame.TDBFrame
        };
    }
}
