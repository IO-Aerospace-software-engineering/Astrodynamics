// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// The parser of a CCSDS date, the date branch of the NDM/XML <c>epochType</c>
/// (<c>ndmxml-4.0.0-common-4.0.xsd</c>), shared by the CDM, OPM and OMM readers (#490).
/// </summary>
/// <remarks>
/// <para>
/// A year, then <c>-MM-DD</c> (calendar) or <c>-DDD</c> (day of year, 1 to 365, 366 in a leap year), then
/// <c>Thh:mm:ss</c>, an optional fraction of any length and an optional <c>Z</c> or <c>±hh:mm</c>. The result is UTC
/// (<see cref="DateTimeKind.Utc"/>), the offset applied. Up to 7 fraction digits are exact, in 100 ns ticks; a longer
/// fraction is rounded to the tick as <c>DateTime.TryParse</c>
/// rounds it, so a calendar date gives the <see cref="DateTime"/> the readers gave before.
/// </para>
/// <para>
/// A text outside this form is rejected (false), so the readers throw their own parse exception (#619, decided by
/// S. Guillet, 2026-11-17): the two forms of CCSDS 508.0-B-1 §6.3.2.6 and 502.0-B-3 (<c>yyyy-mm-ddThh:mm:ss[.d→d][Z]</c>,
/// <c>yyyy-dddThh:mm:ss[.d→d][Z]</c>, "All fields require leading zeros"), with the <c>±hh:mm</c> offset of
/// <c>epochType</c>. Before, <c>DateTime.TryParse</c> read
/// any text it could: a time alone on the day of the read, a bare number (<c>1.5</c>, <c>1,5</c>) as 5 January of the
/// current year, a date alone, a space for the <c>T</c>, RFC 1123. A day of year out of range is rejected, as CSPICE's
/// <c>str2et</c> rejects it, and so is a field out of range (month, day, hour 24, minute or second 60, an offset above
/// 14:00). The digits are ASCII; a year 0000, or a text with other decimal digits, is rejected.
/// </para>
/// </remarks>
internal static class CcsdsEpochParser
{
    /// <summary>
    /// Parses a CCSDS date: the strict branch of the one grammar of the library's time strings
    /// (<see cref="IO.Astrodynamics.TimeSystem.Time.Parse(string)"/>, D3.4 of #668), bit for bit the parser of #619.
    /// </summary>
    /// <param name="value">The text of the date.</param>
    /// <param name="utc">The UTC instant, when the text is a date.</param>
    /// <returns>True when the text is a date.</returns>
    internal static bool TryParse(string value, out DateTime utc) => TimeSystem.TimeGrammar.TryParseCcsds(value, out utc);
}
