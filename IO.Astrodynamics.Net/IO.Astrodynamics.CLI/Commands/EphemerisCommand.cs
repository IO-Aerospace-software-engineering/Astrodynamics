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

        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        var window = Helpers.ConvertWindowInput(windowParameters.Begin, windowParameters.End);
        var localizableObject = Helpers.CreateLocalizable(objectId, window.StartDate);

        var observerItem = Helpers.CreateLocalizable(observerId, window.StartDate);

        var ephemeris = localizableObject.GetEphemeris(window, observerItem, new Frame(frame),
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
        // A site has no sub-observer point: before #681 it was cast to a celestial item, null, then dereferenced
        if (Helpers.IsSiteIdentifier(objectId))
        {
            throw new ArgumentException(
                $"sub-point projects a celestial body or a spacecraft given by its NAIF identifier; {objectId} is a site", nameof(objectId));
        }

        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        var epoch = Helpers.ConvertDateTimeInput(epochParameters.Epoch);
        var celestialItem = Helpers.CreateCelestialItem(objectId, epoch);

        var celestialBody = new CelestialBody(celestialBodyId);
        var abe = Enum.Parse<Aberration>(aberration, true);

        var planetocentric = celestialItem.SubObserverPoint(celestialBody, epoch, abe);

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
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        var epoch = Helpers.ConvertDateTimeInput(epochParameters.Epoch);
        var observer = Helpers.CreateLocalizable(observerId, epoch);
        var firstObject = Helpers.CreateLocalizable(firstObjectId, epoch);
        var secondObject = Helpers.CreateLocalizable(secondObjectId, epoch);
        var abe = Enum.Parse<Aberration>(aberration, true);

        var angle = observer.AngularSeparation(epoch, firstObject, secondObject, abe);

        Console.WriteLine(angle);

        return Task.CompletedTask;
    }
}