using System;
using System.Globalization;
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
using IO.Astrodynamics.TimeSystem.Frames;

namespace IO.Astrodynamics.CLI;

public class Helpers
{
    /// <summary>
    /// The degree of the embedded EGM2008 model of the Earth, <c>Data/EGM2008_to100_TideFree.txt</c>, whose last line holds
    /// n = m = 100: a field pads the degrees beyond its file with zeros, so a degree above it is an argument error.
    /// </summary>
    internal const ushort EarthGeopotentialMaximumDegree = 100;

    /// <summary>
    /// The object of a NAIF identifier: a site (its body's identifier times 1000 plus its own), a celestial body, or a
    /// spacecraft (a negative identifier) read from its kernels.
    /// </summary>
    /// <param name="objectId">The NAIF identifier.</param>
    /// <param name="epoch">An epoch the kernels of a spacecraft cover: the kernel-backed spacecraft reads its SPK there.</param>
    internal static ILocalizable CreateLocalizable(int objectId, in Time epoch)
    {
        ILocalizable localizableObject = null;

        if (IsSiteIdentifier(objectId))
        {
            // A site's NAIF identifier is its body's times 1000 plus its own (399013 is DSS-13 on the Earth)
            var id = objectId % 1000;
            localizableObject = new Site(id, $"site{id}", new CelestialBody(objectId / 1000));
        }
        else
        {
            localizableObject = CreateCelestialItem(objectId, epoch);
        }

        return localizableObject;
    }

    /// <summary>
    /// Whether a NAIF identifier is a site's: its body's identifier times 1000 plus its own (399013 is DSS-13 on the Earth).
    /// </summary>
    /// <param name="objectId">The NAIF identifier.</param>
    internal static bool IsSiteIdentifier(int objectId)
    {
        return objectId is > 100_000 and < 999_999;
    }

    /// <summary>
    /// A celestial body, or for a negative identifier the kernel-backed spacecraft, which reads its SPK (#542): a managed
    /// spacecraft has no state, its ephemeris was a NullReferenceException.
    /// </summary>
    /// <param name="objectId">The NAIF identifier.</param>
    /// <param name="epoch">An epoch the SPK of a spacecraft covers, which its constructor reads.</param>
    internal static CelestialItem CreateCelestialItem(int objectId, in Time epoch)
    {
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
    /// times 1000) through its clock kernel, by <c>ckgpav_c</c> with no tolerance (<see cref="SpiceAPI.ReadOrientation"/>),
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
            // The window of the one epoch; the step only bounds the native loop, which runs once
            var read = SpiceAPI.Instance.ReadOrientation(new Window(epoch, epoch), objectId, TimeSpan.Zero, referenceFrame, TimeSpan.FromSeconds(1.0)).Single();
            return new StateOrientation(read.Rotation, read.AngularVelocity, epoch, referenceFrame);
        };
    }

    internal static Time ConvertDateTimeInput(string epoch)
    {
        if (string.IsNullOrEmpty(epoch)) throw new ArgumentException("Value cannot be null or empty.", nameof(epoch));
        var isutc = epoch.Contains("utc", StringComparison.InvariantCultureIgnoreCase) || epoch.Contains("z", StringComparison.InvariantCultureIgnoreCase);
        var isjd = epoch.Contains("jd", StringComparison.InvariantCultureIgnoreCase);

        //clean string
        epoch = epoch.Replace("utc", "", StringComparison.InvariantCultureIgnoreCase).Replace("tdb", "", StringComparison.InvariantCultureIgnoreCase).Trim();

        //Input
        Time input;
        if (isjd)
        {
            epoch = epoch.Replace("jd", "", StringComparison.InvariantCultureIgnoreCase).Trim();
            double value = double.Parse(epoch, CultureInfo.InvariantCulture);
            if (isutc)
            {
                input = Time.CreateFromJD(value, TimeFrame.UTCFrame);
            }
            else
            {
                input = Time.CreateFromJD(value, TimeFrame.TDBFrame);
            }
        }
        else if (double.TryParse(epoch, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleInput))
        {
            if (isutc)
            {
                input = Time.CreateUTC(doubleInput);
            }
            else
            {
                input = Time.CreateTDB(doubleInput);
            }
        }
        else if (isutc)
        {
            // A calendar epoch tagged UTC, in any case and with or without a space, is a UTC calendar (#509): without
            // its tag, removed above, the calendar parses as an untagged epoch, which is TDB (69.18 s off in 2025); one
            // ending in Z already parses as UTC
            var parsed = new Time(epoch);
            input = parsed.Frame is TDBTimeFrame ? new Time(parsed.DateTime, TimeFrame.UTCFrame) : parsed;
        }
        else
        {
            input = new(epoch);
        }

        return input;
    }

    internal static OrbitalParameters.OrbitalParameters ConvertToOrbitalParameters(string orbitalParametersInput, int centerofMotion, string epoch, string originalFrame,
        bool fromStateVector, bool fromKeplerian, bool fromEquinoctial, bool fromTLE, ushort geopotentialDegrees = 10)
    {
        if (originalFrame.Equals("icrf", StringComparison.InvariantCultureIgnoreCase))
        {
            originalFrame = "J2000";
        }

        var inputFrame = new Frame(originalFrame);
        var inputEpoch = Helpers.ConvertDateTimeInput(epoch);

        GeopotentialModelParameters geopotentialModelParameters = null;
        IAtmosphericModel atmosphericModel = null;
        if (centerofMotion == PlanetsAndMoons.EARTH.NaifId)
        {
            Stream sr = typeof(PropagateCommand).Assembly.GetManifestResourceStream("IO.Astrodynamics.CLI.Data.EGM2008_to100_TideFree.txt");
            geopotentialModelParameters = new GeopotentialModelParameters(sr, geopotentialDegrees);
            atmosphericModel = new EarthStandardAtmosphere();
        }
        else if (centerofMotion == PlanetsAndMoons.MARS.NaifId)
        {
            atmosphericModel = new MarsStandardAtmosphere();
        }

        var inputCenterOfMotion = new CelestialBody(centerofMotion, geopotentialModelParameters, atmosphericModel);
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