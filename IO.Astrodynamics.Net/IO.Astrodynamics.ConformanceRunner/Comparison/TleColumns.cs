using System;
using System.Collections.Generic;
using System.Globalization;

namespace IO.Astrodynamics.ConformanceRunner.Comparison;

/// <summary>
/// The two lines of a TLE as computed by a solver: the comparer reads them by the column table of spec.md section 15.2.
/// </summary>
public sealed record TleText(string Line1, string Line2);

/// <summary>
/// The fields of a TLE decoded by the column table of spec.md section 15.2, every number as the exact decimal its
/// columns write, independently of the framework's parser.
/// </summary>
/// <param name="CatalogNumber">The catalog number of line 1, decoded from Alpha-5 when it begins with a letter.</param>
/// <param name="ObjectId">The international designator as <c>YYYY-NNNP</c> (the year by the pivot 57), empty when blank.</param>
/// <param name="EpochDays">The epoch in days of the UTC calendar from 1950-01-01T00:00, 86 400 s a day.</param>
public sealed record TleFields(
    int CatalogNumber,
    string ObjectId,
    string Classification,
    decimal EpochDays,
    decimal MeanMotionDot,
    decimal MeanMotionDdot,
    decimal BStar,
    int EphemerisType,
    int ElementSetNumber,
    decimal Inclination,
    decimal RaOfAscNode,
    decimal Eccentricity,
    decimal ArgOfPericenter,
    decimal MeanAnomaly,
    decimal MeanMotion,
    int RevAtEpoch)
{
    /// <summary>The value of a field compared by its key, by its OMM keyword (<see cref="MetricTable.TleFields"/>).</summary>
    public decimal Value(string field) => field switch
    {
        "EPOCH" => EpochDays,
        "MEAN_MOTION_DOT" => MeanMotionDot,
        "MEAN_MOTION_DDOT" => MeanMotionDdot,
        "BSTAR" => BStar,
        "INCLINATION" => Inclination,
        "RA_OF_ASC_NODE" => RaOfAscNode,
        "ECCENTRICITY" => Eccentricity,
        "ARG_OF_PERICENTER" => ArgOfPericenter,
        "MEAN_ANOMALY" => MeanAnomaly,
        "MEAN_MOTION" => MeanMotion,
        _ => throw new ArgumentException($"{field} is not a field of a TLE compared by its key.", nameof(field))
    };

    /// <summary>The identifiers of a TLE, compared exactly (<see cref="MetricTable.TleIdentifiers"/>).</summary>
    public bool SameIdentifiers(TleFields other) =>
        CatalogNumber == other.CatalogNumber && ObjectId == other.ObjectId && Classification == other.Classification &&
        EphemerisType == other.EphemerisType && ElementSetNumber == other.ElementSetNumber && RevAtEpoch == other.RevAtEpoch;
}

/// <summary>
/// The column table of the TLE format (spec.md section 15.2): columns counted from 1, the assumed decimal points of e,
/// B* and n̈/6, the Alpha-5 catalog number, the two-digit year with the pivot 57 and the checksum (the digits of columns 1
/// to 68, <c>-</c> counting 1, modulo 10).
/// </summary>
public static class TleColumns
{
    /// <summary>The length of a line of a TLE.</summary>
    public const int LineLength = 69;

    // The letters of the leading Alpha-5 character: A = 10 ... Z = 33, without I and O
    private const string Alpha5 = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    private static readonly DateTime CalendarOrigin = new(1950, 1, 1);

    /// <summary>
    /// What makes two lines no TLE of the format: a length other than 69, a wrong checksum, a wrong line number, two
    /// catalog numbers that differ; empty for a TLE whose fields can be decoded.
    /// </summary>
    public static IReadOnlyList<string> Problems(string line1, string line2)
    {
        var problems = new List<string>();
        foreach (var (line, number) in new[] { (line1, '1'), (line2, '2') })
        {
            if (line == null || line.Length != LineLength)
            {
                problems.Add($"line {number} has {line?.Length ?? 0} characters, not {LineLength}");
                continue;
            }

            if (line[0] != number)
            {
                problems.Add($"line {number} begins with '{line[0]}'");
            }

            if (!char.IsAsciiDigit(line[68]) || line[68] - '0' != Checksum(line))
            {
                problems.Add($"the checksum of line {number} is '{line[68]}', not {Checksum(line)}");
            }
        }

        if (problems.Count == 0 && line1.Substring(2, 5) != line2.Substring(2, 5))
        {
            problems.Add($"the catalog numbers of the two lines differ ('{line1.Substring(2, 5)}', '{line2.Substring(2, 5)}')");
        }

        return problems;
    }

    /// <summary>The checksum of a line: the digits of columns 1 to 68, <c>-</c> counting 1, modulo 10.</summary>
    public static int Checksum(string line)
    {
        int sum = 0;
        for (int i = 0; i < LineLength - 1 && i < line.Length; i++)
        {
            sum += char.IsAsciiDigit(line[i]) ? line[i] - '0' : line[i] == '-' ? 1 : 0;
        }

        return sum % 10;
    }

    /// <summary>The fields of a TLE, read from their columns.</summary>
    /// <exception cref="FormatException">The lines are not a TLE of the format (<see cref="Problems"/>) or a field
    /// cannot be read from its columns.</exception>
    public static TleFields Decode(string line1, string line2)
    {
        var problems = Problems(line1, line2);
        if (problems.Count > 0)
        {
            throw new FormatException($"Not a TLE of spec.md section 15.2: {string.Join("; ", problems)}.");
        }

        return new TleFields(
            CatalogNumber(Columns(line1, 3, 7)),
            ObjectId(Columns(line1, 10, 17)),
            Columns(line1, 8, 8),
            EpochDays(Columns(line1, 19, 32)),
            Number(Columns(line1, 34, 43)),
            Exponent(Columns(line1, 45, 52)),
            Exponent(Columns(line1, 54, 61)),
            Integer(Columns(line1, 63, 63)),
            Integer(Columns(line1, 65, 68)),
            Number(Columns(line2, 9, 16)),
            Number(Columns(line2, 18, 25)),
            Number("0." + Columns(line2, 27, 33)),
            Number(Columns(line2, 35, 42)),
            Number(Columns(line2, 44, 51)),
            Number(Columns(line2, 53, 63)),
            Integer(Columns(line2, 64, 68)));
    }

    // Columns first to last of a line, counted from 1
    private static string Columns(string line, int first, int last) => line.Substring(first - 1, last - first + 1);

    private static int CatalogNumber(string field)
    {
        int alpha = Alpha5.IndexOf(field[0]);
        return alpha >= 0 ? (alpha + 10) * 10000 + Integer(field[1..]) : Integer(field);
    }

    // YYNNNP left-justified, read as YYYY-NNNP with the pivot 57
    private static string ObjectId(string field)
    {
        field = field.Trim();
        if (field.Length == 0)
        {
            return string.Empty;
        }

        if (field.Length < 6)
        {
            throw new FormatException($"The international designator '{field}' is not YYNNNP.");
        }

        return $"{Year(field[..2])}-{field[2..5]}{field[5..]}";
    }

    private static int Year(string twoDigits)
    {
        int year = int.Parse(twoDigits, NumberStyles.None, CultureInfo.InvariantCulture);
        return year < 57 ? 2000 + year : 1900 + year;
    }

    // YYDDD.DDDDDDDD: day 1.0 is January 1, 00:00 UTC
    private static decimal EpochDays(string field)
    {
        decimal dayOfYear = Number(field[2..]);
        return (new DateTime(Year(field[..2]), 1, 1) - CalendarOrigin).Days + dayOfYear - 1m;
    }

    // ±MMMMM±E: a sign, a 5-digit mantissa after an assumed decimal point, a signed one-digit exponent
    private static decimal Exponent(string field)
    {
        if (field[0] is not (' ' or '+' or '-') || field[6] is not ('+' or '-') || !char.IsAsciiDigit(field[7]))
        {
            throw new FormatException($"The field '{field}' is not ±MMMMM±E.");
        }

        decimal value = Number("0." + field[1..6]);
        int exponent = field[7] - '0';
        for (int i = 0; i < exponent; i++)
        {
            value = field[6] == '-' ? value / 10m : value * 10m;
        }

        return field[0] == '-' ? -value : value;
    }

    private static decimal Number(string field)
    {
        return decimal.Parse(field.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    // A right-justified integer; blank is 0
    private static int Integer(string field)
    {
        field = field.Trim();
        return field.Length == 0 ? 0 : int.Parse(field, NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
