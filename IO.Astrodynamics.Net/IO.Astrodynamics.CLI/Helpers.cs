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
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.CLI;

public class Helpers
{
    internal static ILocalizable CreateLocalizable(int objectId)
    {
        ILocalizable localizableObject = null;

        if (objectId is > 100_000 and < 999_999)
        {
            var id = int.Parse(objectId.ToString().Skip(3).ToString()!);
            localizableObject = new Site(id, $"site{id}", new CelestialBody((int)double.Truncate(objectId * 1E-03)));
        }
        else
        {
            localizableObject = CreateCelestialItem(objectId);
        }

        return localizableObject;
    }

    internal static CelestialItem CreateCelestialItem(int objectId)
    {
        CelestialItem localizableObject = null;
        if (int.IsPositive(objectId))
        {
            localizableObject = new CelestialBody(objectId);
        }
        else
        {
            localizableObject = new Spacecraft(objectId, $"spc{objectId}", 1.0, 1.0, new Clock($"clk{objectId}", 65536), null);
        }

        return localizableObject;
    }

    internal static IOrientable CreateOrientable(int objectId)
    {
        IOrientable celestialItem;
        if (int.IsPositive(objectId))
        {
            celestialItem = new CelestialBody(objectId);
        }
        else
        {
            celestialItem = new Spacecraft(objectId, "spc", 1.0, 1.0, new Clock("clk", 65536), null);
        }

        return celestialItem;
    }

    internal static DateTime ConvertDateTimeInput(string epoch)
    {
        if (string.IsNullOrEmpty(epoch)) throw new ArgumentException("Value cannot be null or empty.", nameof(epoch));
        var isutc = epoch.Contains("utc", StringComparison.InvariantCultureIgnoreCase) || epoch.Contains("z", StringComparison.InvariantCultureIgnoreCase);
        var isjd = epoch.Contains("jd", StringComparison.InvariantCultureIgnoreCase);

        //clean string
        epoch = epoch.Replace("utc", "", StringComparison.InvariantCultureIgnoreCase).Replace("tdb", "", StringComparison.InvariantCultureIgnoreCase).Trim();

        //Input
        DateTime input;
        if (isjd)
        {
            epoch = epoch.Replace("jd", "", StringComparison.InvariantCultureIgnoreCase).Trim();
            double value = double.Parse(epoch, CultureInfo.InvariantCulture);
            if (isutc)
            {
                input = DateTimeExtension.CreateUTCFromJD(value);
            }
            else
            {
                input = DateTimeExtension.CreateTDBFromJD(value);
            }
        }
        else if (!DateTime.TryParse(epoch, out input))
        {
            double value = double.Parse(epoch, CultureInfo.InvariantCulture);
            if (isutc)
            {
                input = DateTimeExtension.CreateUTC(value);
            }
            else
            {
                input = DateTimeExtension.CreateTDB(value);
            }
        }

        return input;
    }

    internal static OrbitalParameters.OrbitalParameters ConvertToOrbitalParameters(string orbitalParametersInput, int centerofMotion, string epoch, string originalFrame,
        bool fromStateVector, bool fromKeplerian, bool fromEquinoctial, bool fromTLE, ushort geopotentialDegrees = 10)
    {
        if (originalFrame.Equals("icrf", StringComparison.InvariantCultureIgnoreCase))
        {
            originalFrame = "j2000";
        }

        var inputFrame = new Frame(originalFrame);
        var inputEpoch = Helpers.ConvertDateTimeInput(epoch);

        GeopotentialModelParameters geopotentialModelParameters = null;
        AtmosphericModel atmosphericModel = null;
        if (centerofMotion == PlanetsAndMoons.EARTH.NaifId)
        {
            Stream sr = typeof(PropagateCommand).Assembly.GetManifestResourceStream("IO.Astrodynamics.CLI.Data.EGM2008_to100_TideFree.txt");
            geopotentialModelParameters = new GeopotentialModelParameters(sr, geopotentialDegrees);
            atmosphericModel = new EarthAtmosphericModel();
        }
        else if (centerofMotion == PlanetsAndMoons.MARS.NaifId)
        {
            atmosphericModel = new MarsAtmosphericModel();
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
            orbitalParameters = TLE.Create("body", arr[0], arr[1]);
        }

        return orbitalParameters;
    }

    internal static OrbitalParameters.OrbitalParameters ConvertToOrbitalParameters(Commands.Parameters.OrbitalParameters orbitalParameters)
    {
        return ConvertToOrbitalParameters(orbitalParameters.OrbitalParametersValues, orbitalParameters.CenterOfMotionId, orbitalParameters.OrbitalParametersEpoch,
            orbitalParameters.Frame, orbitalParameters.FromStateVector, orbitalParameters.FromKeplerian, orbitalParameters.FromEquinoctial, orbitalParameters.FromTLE);
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