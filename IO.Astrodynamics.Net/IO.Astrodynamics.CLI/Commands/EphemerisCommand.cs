using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cocona;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Frames;

namespace IO.Astrodynamics.CLI.Commands;

public class EphemerisCommand
{
    public EphemerisCommand()
    {
    }

    [Command("ephemeris", Description = "Compute ephemeris of given object")]
    public Task Ephemeris(
        [Argument(Description = "Kernels directory path")]
        string kernelsPath,
        [Argument(Description = "Object identifier (Naif Identifier)")]
        int objectId,
        [Argument(Description = "Observer identifier (Naif Identifier)")]
        int observerId,
        WindowParameters windowParameters,
        [Argument(Description = "Step size <d.hh:mm:ss.fff>")]
        TimeSpan step,
        [Argument(Description = "Frame")] string frame = "ICRF",
        [Argument(Description = "Aberration")] string aberration = "None",
        [Option('s', Description = "Display result as state vector")]
        bool toStateVector = true,
        [Option('k', Description = "Display result as Keplerian elements")]
        bool toKeplerian = false,
        [Option('q', Description = "Display result as equinoctial elements")]
        bool toEquinoctial = false)
    {
        if (frame.Equals("icrf", StringComparison.InvariantCultureIgnoreCase))
        {
            frame = "j2000";
        }

        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        var localizableObject = Helpers.CreateLocalizable(objectId);

        var observerItem = Helpers.CreateLocalizable(observerId);

        var ephemeris = localizableObject.GetEphemeris(Helpers.ConvertWindowInput(windowParameters.Begin, windowParameters.End), observerItem, new Frame(frame),
            Enum.Parse<Aberration>(aberration, true), step);


        if (toKeplerian)
        {
            ephemeris = ephemeris.Select(x => x.ToKeplerianElements());
        }
        else if (toEquinoctial)
        {
            ephemeris = ephemeris.Select(x => x.ToEquinoctial());
        }
        else
        {
            ephemeris = ephemeris.Select(x => x.ToStateVector());
        }

        foreach (var eph in ephemeris)
        {
            Console.WriteLine(eph.ToString());
        }

        return Task.CompletedTask;
    }

    [Command("sub-point", Description = "Compute sub observer point of given object in planetodetic, planetocentric or planetocentric cartesian coordinates")]
    public Task SubPoint(
        [Argument(Description = "Kernels directory path")]
        string kernelsPath,
        [Argument(Description = "Object identifier (Naif Identifier)")]
        int objectId,
        [Argument(Description = "Identifier of the celestial body onto which the object is projected (Naif Identifier)")]
        int celestialBodyId,
        EpochParameters epochParameters,
        [Argument(Description = "Aberration")] string aberration = "None",
        [Option(shortName: 'd', Description = "Display result as planetodetic coordinates")]
        bool planetodetic = false,
        [Option(shortName: 'c', Description = "Display result as planetocentric cartesian coordinates")]
        bool cartesian = false)
    {
        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        var localizableObject = Helpers.CreateLocalizable(objectId) as CelestialItem;

        var celestialBody = new CelestialBody(celestialBodyId);
        var abe = Enum.Parse<Aberration>(aberration, true);

        var planetocentric = localizableObject!.SubObserverPoint(celestialBody, Helpers.ConvertDateTimeInput(epochParameters.Epoch), abe);

        if (cartesian)
        {
            Console.WriteLine(planetocentric.ToCartesianCoordinates().Normalize() *
                              planetocentric.RadiusFromPlanetocentricLatitude(celestialBody.EquatorialRadius, celestialBody.Flattening));
            return Task.CompletedTask;
        }

        if (planetodetic)
        {
            Console.WriteLine(planetocentric.ToPlanetodetic(celestialBody.Flattening, celestialBody.EquatorialRadius));
            return Task.CompletedTask;
        }

        Console.WriteLine(planetocentric);
        return Task.CompletedTask;
    }

    [Command("angular-separation", Description = "Compute angular separation between given objects")]
    public Task AngularSeparation(
        [Argument(Description = "Kernels directory path")]
        string kernelsPath,
        [Argument(Description = "Observer identifier (Naif Identifier)")]
        int observerId,
        [Argument(Description = "First object identifier (Naif Identifier)")]
        int firstObjectId,
        [Argument(Description = "Second object identifier (Naif Identifier)")]
        int secondObjectId,
        EpochParameters epochParameters,
        [Argument(Description = "Aberration")] string aberration = "None"
    )
    {
        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        var observer = Helpers.CreateLocalizable(observerId);
        var firstObject = Helpers.CreateLocalizable(firstObjectId);
        var secondObject = Helpers.CreateLocalizable(secondObjectId);
        var abe = Enum.Parse<Aberration>(aberration, true);

        var angle = observer.AngularSeparation(Helpers.ConvertDateTimeInput(epochParameters.Epoch), firstObject, secondObject, abe);

        Console.WriteLine(angle);

        return Task.CompletedTask;
    }
}