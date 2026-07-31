// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;

namespace IO.Astrodynamics.OrbitalParameters.TLE;

/// <summary>
/// Encodes and decodes the 5-character NORAD catalog number field of a TLE.
/// </summary>
/// <remarks>
/// <para>
/// The catalog number field spans 5 fixed-width columns on both TLE lines (columns 3-7).
/// Numbers up to <see cref="MaxNumericValue"/> are written as plain zero-padded digits.
/// </para>
/// <para>
/// Above that, the Alpha-5 convention defined by the 18th Space Defense Squadron replaces the
/// leading digit with a letter carrying the value 10 to 33, extending the field to
/// <see cref="MaxValue"/> without changing the column layout. The letters 'I' and 'O' are excluded
/// because they are visually ambiguous with the digits '1' and '0'.
/// For example, <c>T5544</c> denotes catalog number 275544.
/// </para>
/// <para>
/// Alpha-5 is a stopgap: the catalog will eventually exceed 339999 and only the CCSDS OMM
/// format (9-digit NORAD_CAT_ID) can carry the full range. See <see cref="CCSDS.OMM.Omm"/>.
/// </para>
/// </remarks>
public static class NoradCatalogNumber
{
    /// <summary>
    /// The alphabet used by the Alpha-5 convention: 'A' (10) through 'Z' (33), excluding 'I' and 'O'.
    /// </summary>
    private const string Alpha5Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>
    /// The value carried by the first Alpha-5 letter ('A').
    /// </summary>
    private const int Alpha5Offset = 10;

    /// <summary>
    /// The number of characters occupied by the catalog number field on a TLE line.
    /// </summary>
    public const int FieldLength = 5;

    /// <summary>
    /// The largest catalog number representable with plain digits only (99999).
    /// </summary>
    public const int MaxNumericValue = 99999;

    /// <summary>
    /// The largest catalog number representable in a TLE, using the Alpha-5 convention (339999, i.e. "Z9999").
    /// </summary>
    public const int MaxValue = 339999;

    /// <summary>
    /// Determines whether a value can be written into the 5-character catalog number field.
    /// </summary>
    /// <param name="number">The catalog number.</param>
    /// <returns><see langword="true"/> if the value is within [0, <see cref="MaxValue"/>].</returns>
    public static bool IsValid(int number) => number >= 0 && number <= MaxValue;

    /// <summary>
    /// Formats a catalog number into the 5-character TLE field, using Alpha-5 above <see cref="MaxNumericValue"/>.
    /// </summary>
    /// <param name="number">The catalog number, between 0 and <see cref="MaxValue"/>.</param>
    /// <returns>A 5-character string, e.g. <c>"25544"</c> or <c>"T5544"</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the number is negative or exceeds <see cref="MaxValue"/>.</exception>
    public static string Format(int number)
    {
        if (!IsValid(number))
        {
            throw new ArgumentOutOfRangeException(nameof(number),
                $"NORAD catalog number must be between 0 and {MaxValue} to fit the {FieldLength}-character TLE field " +
                $"(values above {MaxNumericValue} use the Alpha-5 convention). Use a CCSDS OMM to carry larger identifiers.");
        }

        if (number <= MaxNumericValue)
        {
            return number.ToString("00000", CultureInfo.InvariantCulture);
        }

        int letterValue = System.Math.DivRem(number, 10000, out int remainder);
        return string.Concat(
            Alpha5Letters[letterValue - Alpha5Offset].ToString(),
            remainder.ToString("0000", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Parses the catalog number field of a TLE line, accepting both plain digits and Alpha-5.
    /// </summary>
    /// <param name="field">The field content. Leading and trailing spaces are tolerated.</param>
    /// <returns>The decoded catalog number.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="field"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when the field is not a valid catalog number.</exception>
    public static int Parse(string field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (!TryParse(field, out int number))
        {
            throw new FormatException($"'{field}' is not a valid NORAD catalog number field.");
        }

        return number;
    }

    /// <summary>
    /// Attempts to parse the catalog number field of a TLE line, accepting both plain digits and Alpha-5.
    /// </summary>
    /// <param name="field">The field content. Leading and trailing spaces are tolerated. May be <see langword="null"/>.</param>
    /// <param name="number">The decoded catalog number, or 0 when parsing fails.</param>
    /// <returns><see langword="true"/> when the field was successfully decoded.</returns>
    public static bool TryParse(string field, out int number)
    {
        number = 0;

        if (string.IsNullOrWhiteSpace(field))
        {
            return false;
        }

        var trimmed = field.Trim();
        if (trimmed.Length > FieldLength)
        {
            return false;
        }

        char first = trimmed[0];

        // Plain numeric form: every character must be a digit (no embedded spaces or signs).
        if (char.IsAsciiDigit(first))
        {
            foreach (char c in trimmed)
            {
                if (!char.IsAsciiDigit(c))
                {
                    return false;
                }
            }

            return int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        // Alpha-5 form: a letter followed by exactly 4 digits. The letter cannot be padded away,
        // so the trimmed field must occupy the full width.
        if (trimmed.Length != FieldLength)
        {
            return false;
        }

        int letterIndex = Alpha5Letters.IndexOf(char.ToUpperInvariant(first));
        if (letterIndex < 0)
        {
            return false;
        }

        var digits = trimmed.AsSpan(1);
        foreach (char c in digits)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int remainder))
        {
            return false;
        }

        number = (letterIndex + Alpha5Offset) * 10000 + remainder;
        return true;
    }
}
