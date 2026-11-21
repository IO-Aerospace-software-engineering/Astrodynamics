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
    /// The kernels path of a command, as <see cref="SpiceAPI.LoadKernels"/> takes it: a file (a kernel or a meta-kernel) when
    /// one exists at the path, a directory otherwise (#673: every command passed a directory, from which a file loaded
    /// nothing). A path that does not exist loads nothing, as before.
    /// </summary>
    internal static FileSystemInfo KernelsPath(string path) => File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);

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
    /// frame kernel of <c>Site.WriteFrameAsync</c> names the site.
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
    /// <c>JD 2458850.0 TDB</c>), read by <see cref="Time.TryParse(string, TimeScale, out Time)"/> with TDB for an untagged
    /// string, as before.
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

        if (Time.TryParse(epoch, TimeScale.TDB, out var time))
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

    internal static OrbitalParameters.OrbitalParameters ConvertToOrbitalParameters(string orbitalParametersInput, int centerofMotion, string epoch, string originalFrame,
        bool fromStateVector, bool fromKeplerian, bool fromEquinoctial, bool fromTLE, ushort geopotentialDegrees = EarthGeopotentialDefaultDegree)
    {
        var inputFrame = ResolveFrame(originalFrame);
        var inputEpoch = Helpers.ConvertDateTimeInput(epoch);

        var inputCenterOfMotion = CreateCenterOfMotion(centerofMotion, geopotentialDegrees);
        //Generate original orbital parameters
        OrbitalParameters.OrbitalParameters orbitalParameters = null;
        if (fromStateVector)
        {
            var arr = orbitalParametersInput.Split(' ').Select(double.Parse).ToArray();
            orbitalParameters = new StateVector(new Vector3(arr[0], arr[1], arr[2]), new Vector3(arr[3], arr[4], arr[5]), inputCenterOfMotion, inputEpoch, inputFrame);
        }
        else if (fromKeplerian)
        {
            var arr = orbitalParametersInput.Split(' ').Select(double.Parse).ToArray();
            orbitalParameters = new KeplerianElements(arr[0], arr[1], arr[2], arr[3], arr[4], arr[5], inputCenterOfMotion, inputEpoch, inputFrame);
        }
        else if (fromEquinoctial)
        {
            var arr = orbitalParametersInput.Split(' ').Select(double.Parse).ToArray();
            orbitalParameters = new EquinoctialElements(arr[0], arr[1], arr[2], arr[3], arr[4], arr[5], inputCenterOfMotion, inputEpoch, inputFrame);
        }
        else if (fromTLE)
        {
            var arr = orbitalParametersInput.Split(',').ToArray();
            orbitalParameters = new TLE("body", arr[0], arr[1]);
        }

        return orbitalParameters;
    }

    /// <summary>
    /// The centre of motion of the CLI: the Earth with the embedded EGM2008 at <paramref name="geopotentialDegrees"/> and
    /// the U.S. Standard Atmosphere 1976, Mars with its standard atmosphere, any other body as the kernels define it.
    /// </summary>
    internal static CelestialBody CreateCenterOfMotion(int centerOfMotion, ushort geopotentialDegrees = EarthGeopotentialDefaultDegree)
    {
        GeopotentialModelParameters geopotentialModelParameters = null;
        IAtmosphericModel atmosphericModel = null;
        if (centerOfMotion == PlanetsAndMoons.EARTH.NaifId)
        {
            Stream sr = typeof(PropagateCommand).Assembly.GetManifestResourceStream("IO.Astrodynamics.CLI.Data.EGM2008_to100_TideFree.txt");
            geopotentialModelParameters = new GeopotentialModelParameters(sr, geopotentialDegrees);
            atmosphericModel = new EarthStandardAtmosphere();
        }
        else if (centerOfMotion == PlanetsAndMoons.MARS.NaifId)
        {
            atmosphericModel = new MarsStandardAtmosphere();
        }

        return new CelestialBody(centerOfMotion, geopotentialModelParameters, atmosphericModel);
    }

    /// <summary>
    /// The initial orbit of a propagation from a TLE (#675): its SGP4 state at the TDB start of the window, in TEME, on
    /// the Earth of the CLI, which carries its geopotential, the embedded EGM2008 to <paramref name="geopotentialDegrees"/>
    /// (the degree of <c>propagate -n</c>, #681), and atmosphere (the propagation rotates it once to ICRF).
    /// </summary>
    /// <exception cref="ArgumentException">The centre of motion is not the Earth: a TLE describes an Earth orbit.</exception>
    internal static StateVector TleStartState(TLE tle, int centerOfMotion, in Window window, ushort geopotentialDegrees)
    {
        if (centerOfMotion != PlanetsAndMoons.EARTH.NaifId)
        {
            throw new ArgumentException(
                $"A TLE describes an Earth orbit: its centre of motion is the Earth (399), not {centerOfMotion}.", nameof(centerOfMotion));
        }

        var start = window.StartDate.ToTDB();
        var sgp4 = tle.ToStateVector(start);
        return new StateVector(sgp4.Position, sgp4.Velocity, CreateCenterOfMotion(centerOfMotion, geopotentialDegrees), start, Frame.TEME);
    }

    /// <summary>
    /// The orbital parameters of the command's arguments, about a centre whose geopotential, for the Earth, is the embedded
    /// EGM2008 model to <paramref name="geopotentialDegrees"/> (#681: the degree of <c>propagate -n</c>, which no code read).
    /// </summary>
    internal static OrbitalParameters.OrbitalParameters ConvertToOrbitalParameters(Commands.Parameters.OrbitalParameters orbitalParameters,
        ushort geopotentialDegrees)
    {
        return ConvertToOrbitalParameters(orbitalParameters.OrbitalParametersValues, orbitalParameters.CenterOfMotionId, orbitalParameters.OrbitalParametersEpoch,
            orbitalParameters.Frame, orbitalParameters.FromStateVector, orbitalParameters.FromKeplerian, orbitalParameters.FromEquinoctial, orbitalParameters.FromTLE,
            geopotentialDegrees);
    }

    internal static Planetodetic ConvertToPlanetodetic(string value)
    {
        double[] coordinates = value.Split(' ').Select(double.Parse).ToArray();
        return new Planetodetic(coordinates[0], coordinates[1], coordinates[2]);
    }

    internal static Window ConvertWindowInput(string begin, string end)
    {
        return new Window(ConvertDateTimeInput(begin), ConvertDateTimeInput(end));
    }

    internal static Window ConvertWindowInput(EpochParameters begin, EpochParameters end)
    {
        return ConvertWindowInput(begin.Epoch, end.Epoch);
    }

    internal static Window ConvertWindowInput(WindowParameters windowParameters)
    {
        return ConvertWindowInput(windowParameters.Begin, windowParameters.End);
    }
}