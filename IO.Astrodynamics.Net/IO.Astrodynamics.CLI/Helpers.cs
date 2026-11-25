using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
namespace IO.Astrodynamics.CLI;

public class Helpers
{
    /// <summary>
    /// The degree of the embedded EGM2008 model of the Earth, <c>Data/EGM2008_to100_TideFree.txt</c>, whose last line holds
    /// n = m = 100, complete from degree 2: a degree above it is an argument error, checked by the command before anything
    /// is loaded (the field also rejects a degree above its file, #689).
    /// </summary>
    internal const ushort EarthGeopotentialMaximumDegree = 100;

    /// <summary>
    /// The degree of the Earth geopotential when <c>propagate -n</c> is not given, and of the conversions of
    /// <c>orbital-parameters-converter</c>, which read the GM only.
    /// </summary>
    internal const ushort EarthGeopotentialDefaultDegree = 10;

    /// <summary>
    /// The object of a NAIF identifier: a site by the library's rule (<see cref="SpiceAPI.IsSiteOfNamedBody"/>, its body's
    /// identifier times 1000 plus its own, the body a planet or a moon without the Lagrange points that SPICE names), built
    /// from the kernels under its SPICE name as <see cref="SpiceAPI.ReadEphemeris(Time, int, int, Frame, Aberration)"/>
    /// builds its observer; an object of the kernels read by its identifier for one of that form whose body SPICE does not
    /// name, NOTO (398989) and NEW NORCIA (398990) (#750: before, the site of a body 398, which threw); a celestial item; or
    /// a spacecraft (a negative identifier) read from its kernels (<see cref="CreateCelestialItem"/>).
    /// </summary>
    /// <param name="objectId">The NAIF identifier.</param>
    /// <param name="epoch">An epoch the kernels of a spacecraft cover: the kernel-backed spacecraft reads its SPK there.</param>
    /// <remarks>
    /// Before 11.0.0 the CLI kept its own rule, 100001 to 999998, and built the site <c>site&lt;id&gt;</c> of any of them:
    /// an x000, a body of at most 100 or a Lagrange point failed with another error than the library's
    /// (<c>ArgumentOutOfRangeException</c> for 399000), and a site was not named as SPICE names it (DSS-13 for 399013).
    /// A site that no loaded kernel names now fails as the library fails (<see cref="ArgumentException"/>, "has no name in
    /// SPICE"), where it was read as <c>site&lt;id&gt;</c>: the SPK <c>Site.WriteEphemeris</c> writes names nothing, and the
    /// frame kernel of <c>Site.WriteFrame</c> names the site.
    /// </remarks>
    internal static ILocalizable CreateLocalizable(int objectId, in Time epoch)
    {
        return SpiceAPI.IsSiteOfNamedBody(objectId) ? SpiceAPI.Instance.CreateKernelSite(objectId) : CreateCelestialItem(objectId, epoch);
    }

    /// <summary>
    /// A celestial body, or for a negative identifier the kernel-backed spacecraft, which reads its SPK (#542): a managed
    /// spacecraft has no state, its ephemeris was a NullReferenceException. An identifier of the form of a site
    /// (<see cref="SpiceAPI.IsSiteId"/>) that reaches it, one whose body SPICE does not name (NOTO, NEW NORCIA), is the
    /// object of the kernels read by its identifier (#750: before, a <see cref="CelestialBody"/>, which has no frame for it).
    /// </summary>
    /// <param name="objectId">The NAIF identifier.</param>
    /// <param name="epoch">An epoch the SPK of a spacecraft covers, which its constructor reads.</param>
    internal static CelestialItem CreateCelestialItem(int objectId, in Time epoch)
    {
        if (SpiceAPI.IsSiteId(objectId))
        {
            return SpiceAPI.CreateKernelObject(objectId);
        }

        if (int.IsPositive(objectId))
        {
            return new CelestialBody(objectId);
        }

        return new Spacecraft(objectId, $"spc{objectId}", epoch);
    }

    /// <summary>
    /// The reader of the orientation of an object relative to <paramref name="referenceFrame"/>, at an epoch: that of a
    /// celestial body, or, for a negative identifier, the CK of the spacecraft (#542).
    /// </summary>
    /// <remarks>
    /// A spacecraft is read by its NAIF identifier alone, as the CLI names it: the CK of its structure (the identifier
    /// times 1000) through its clock kernel, with no tolerance (<see cref="SpiceAPI.ReadOrientation"/>: the CK segment of
    /// highest priority, <c>ckgp_c</c>'s, with <c>ckgpav_c</c>'s rate when it holds one, #724),
    /// the C-matrix from <paramref name="referenceFrame"/> to the spacecraft with the angular velocity of the spacecraft
    /// relative to <paramref name="referenceFrame"/>, in it: the orientation <see cref="Spacecraft.GetOrientation"/>
    /// gives. A kernel-backed spacecraft would need the name its frame kernel gives it, which the identifier does not
    /// tell, and an SPK; the managed spacecraft built before had an empty attitude record, the identity, whatever its CK.
    /// An epoch the CK does not cover throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    internal static Func<Time, StateOrientation> CreateOrientationReader(int objectId, Frame referenceFrame)
    {
        if (int.IsPositive(objectId))
        {
            var celestialBody = new CelestialBody(objectId);
            return epoch => celestialBody.GetOrientation(referenceFrame, epoch);
        }

        return epoch =>
        {
            // The window of the one epoch: one record, read in a buffer of one (#563); the step is that of an empty grid
            var read = SpiceAPI.Instance.ReadOrientation(new Window(epoch, epoch), objectId, TimeSpan.Zero, referenceFrame, TimeSpan.FromSeconds(1.0)).Single();
            return new StateOrientation(read.Rotation, read.AngularVelocity, epoch, referenceFrame);
        };
    }

    /// <summary>
    /// The epoch of a command argument (D3 of #668, decided by S. Guillet, 2026-11-19): seconds from J2000 followed by an
    /// optional scale tag (<c>631152000.0</c>, <c>631152000.0 UTC</c>, <c>757339200 TT</c>; TDB without a tag), the CLI's
    /// own form, or any string of the grammar of <see cref="Time.Parse(string)"/> (an ISO 8601 date or a CCSDS day of year
    /// with an optional time, then <c>Z</c>, an offset or a tag; a Julian date <c>2458850.0 JD UTC</c> or
    /// <c>JD 2458850.0 TDB</c>), read by <see cref="Time.TryParse(string, out Time)"/>, and an untagged string by
    /// <see cref="Time.TryParse(string, TimeScale, out Time)"/> with TDB, as before.
    /// </summary>
    /// <remarks>
    /// Before 11.0.0 the tag of seconds was UTC or TDB only (<c>757339200 TT</c> threw), a string held UTC when it held a
    /// Z or the letters UTC anywhere, an offset was the machine's local time, and a Julian date went through a double (now
    /// its digits to the nearest tick, at most one tick away).
    /// </remarks>
    /// <exception cref="ArgumentException">The epoch is null or empty.</exception>
    /// <exception cref="FormatException">The epoch is of neither form; the message lists the forms.</exception>
    internal static Time ConvertDateTimeInput(string epoch)
    {
        if (string.IsNullOrWhiteSpace(epoch)) throw new ArgumentException("Value cannot be null or empty.", nameof(epoch));

        var seconds = SecondsFromJ2000.Match(epoch);
        if (seconds.Success &&
            double.TryParse(seconds.Groups["seconds"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            var tag = seconds.Groups["tag"];
            return Time.Create(value, tag.Success ? ScaleOfTag(tag.Value) : TimeScale.TDB);
        }

        // A tagged string names its scale; an untagged one is TDB. Parse(s, scale) refuses a tag of another scale (D3.4 of
        // #668), so the tagged reading comes first
        if (Time.TryParse(epoch, out var time) || Time.TryParse(epoch, TimeScale.TDB, out time))
        {
            return time;
        }

        throw new FormatException(
            $"'{epoch}' is not an epoch: seconds from J2000 with an optional scale tag (631152000.0 UTC), an ISO 8601 date or a day of year with an optional time then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-172T12:00:00 TT), or a Julian date (2458850.0 JD UTC); the scale tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given.");
    }

    /// <summary>
    /// The scale a tag names, in any case; TT and TDT are TT.
    /// </summary>
    internal static TimeScale ScaleOfTag(string tag) => tag.ToUpperInvariant() switch
    {
        "UTC" => TimeScale.UTC,
        "TT" or "TDT" => TimeScale.TT,
        "TAI" => TimeScale.TAI,
        "GPS" => TimeScale.GPS,
        "TDB" => TimeScale.TDB,
        _ => throw new ArgumentException($"Unknown time scale '{tag}': UTC, TT, TDT, TDB, TAI or GPS.", nameof(tag))
    };

    /// <summary>
    /// Seconds from J2000, then an optional scale tag, in any case.
    /// </summary>
    private static readonly Regex SecondsFromJ2000 = new(
        @"^\s*(?<seconds>[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)\s*(?<tag>UTC|TT|TDT|TDB|TAI|GPS)?\s*\z",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// The member of <typeparamref name="T"/> a command argument names, by its description (the value its help lists, e.g.
    /// <c>RA/DEC</c>, <c>RIGHT ASCENSION</c>) or by its member name (<c>RaDec</c>, <c>RightAscension</c>), in any case (#652:
    /// the coordinate system and the coordinate were read by member name only, so the values the help showed were rejected).
    /// </summary>
    /// <exception cref="ArgumentException">The value names no member: the message lists the values the command reads, and
    /// <see cref="ArgumentException.ParamName"/> is <paramref name="parameterName"/>.</exception>
    internal static T ParseEnumeration<T>(string value, string parameterName) where T : struct, Enum
    {
        foreach (var member in Enum.GetValues<T>())
        {
            if (member.GetDescription().Equals(value, StringComparison.InvariantCultureIgnoreCase) ||
                member.ToString().Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return member;
            }
        }

        throw new ArgumentException(
            $"Unknown {parameterName} '{value}': the command reads {string.Join(", ", Enum.GetValues<T>().Select(x => x.GetDescription()))}, in any case, or the member names {string.Join(", ", Enum.GetValues<T>())}.",
            parameterName);
    }

    /// <summary>
    /// The frame a command argument names, the one rule of every command (#657): ICRF or J2000, in any case, is
    /// <see cref="Frame.ICRF"/>, whose name is J2000, so that every path labels it J2000; any other name is
    /// <c>new Frame(name)</c>, which the library routes by its name (TEME, GCRF, CIRS, TIRS: the managed frames, #656).
    /// Before 11.0.0 <c>ephemeris</c>, <c>orientation</c> and <c>geometry-finder</c> rewrote ICRF as <c>j2000</c>, which
    /// the SPICE path printed as given and a site observer as J2000.
    /// </summary>
    /// <exception cref="ArgumentException">The name is null, empty or blank.</exception>
    internal static Frame ResolveFrame(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A frame name is required.", nameof(name));
        }

        return name.Equals("ICRF", StringComparison.OrdinalIgnoreCase) || name.Equals(Frame.ICRF.Name, StringComparison.OrdinalIgnoreCase)
            ? Frame.ICRF
            : new Frame(name);
    }

    /// <summary>The embedded EGM2008 model of the Earth to <paramref name="degree"/>, read from a fresh stream.</summary>
    internal static GeopotentialModelParameters EarthGeopotential(ushort degree) =>
        new(typeof(PropagateCommand).Assembly.GetManifestResourceStream("IO.Astrodynamics.CLI.Data.EGM2008_to100_TideFree.txt"), degree);
}
