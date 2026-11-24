using System.Collections.Generic;
using System.Linq;
using Cocona;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CLI.Cli;
using IO.Astrodynamics.CLI.Commands.Parameters;

namespace IO.Astrodynamics.CLI.Commands;

public class EphemerisCommand
{
    internal const string ObjectDescription = "a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13)";

    [Command("ephemeris", Description = "The states of a target relative to an observer over a window, as state vectors, Keplerian or equinoctial elements")]
    public int Ephemeris(
        [Argument("target", Description = "Target: " + ObjectDescription)]
        string target,
        [Argument("observer", Description = "Observer: " + ObjectDescription)]
        string observer,
        KernelOptions kernels,
        WindowOptions window,
        AberrationOptions aberration,
        TimeScaleOptions timeScale,
        OutputOptions output,
        [Option("frame", ValueName = "frame", Description = "Frame of the states: ICRF (written J2000) or a frame of the library or of the loaded kernels")]
        string frame = "ICRF",
        [Option("as", ValueName = "state|keplerian|equinoctial", Description = "Kind of the states written: state, keplerian or equinoctial")]
        string @as = "state")
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            var format = CliOutput.Format(output.Format, tabular: true);
            var kind = CliValues.Choice(@as, "--as", "state", "keplerian", "equinoctial");
            var searchWindow = CliValues.Window(window.Begin, window.End);
            var step = CliValues.Step(window.Step, "--step");
            var correction = CliValues.Enumeration<Aberration>(aberration.Aberration, "--aberration");
            var scale = CliValues.TimeScale(timeScale.TimeScale, searchWindow.StartDate.Scale);

            CliKernels.Load(kernels.Kernels);
            var targetObject = Helpers.CreateLocalizable(CliValues.Object(target, "target"), searchWindow.StartDate);
            var observerObject = Helpers.CreateLocalizable(CliValues.Object(observer, "observer"), searchWindow.StartDate);
            var outputFrame = CliValues.Frame(frame, "--frame");

            IEnumerable<OrbitalParameters.OrbitalParameters> ephemeris = targetObject
                .GetEphemeris(searchWindow, observerObject, outputFrame, correction, step)
                .Select(x => CliTime.Relabel(x, scale))
                .Select(x => kind switch
                {
                    "keplerian" => x.ToKeplerianElements(),
                    "equinoctial" => x.ToEquinoctial(),
                    _ => x
                });

            var orbits = ephemeris.ToArray();
            if (format == OutputFormat.Csv)
            {
                CliOutput.WriteCsv(CliContext.Out, kind switch
                {
                    "keplerian" => CliOutput.KeplerianHeader,
                    "equinoctial" => CliOutput.EquinoctialHeader,
                    _ => CliOutput.StateHeader
                }, orbits.Select(CliOutput.Orbit));
                return;
            }

            foreach (var orbit in orbits)
            {
                CliContext.Out.WriteLine(orbit.ToString());
            }
        });
    }

    [Command("sub-point", Description = "The sub-observer point of an object on a celestial body, in planetocentric, planetodetic or planetocentric cartesian coordinates")]
    public int SubPoint(
        [Argument("object", Description = "Object projected, a celestial body or a spacecraft: " + ObjectDescription)]
        string @object,
        [Argument("body", Description = "Celestial body onto which the object is projected: " + ObjectDescription)]
        string body,
        KernelOptions kernels,
        EpochOptions epoch,
        AberrationOptions aberration,
        OutputOptions output,
        [Option("as", ValueName = "planetocentric|planetodetic|cartesian", Description = "Coordinates written: planetocentric, planetodetic, or the planetocentric cartesian position on the surface")]
        string @as = "planetocentric")
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            var format = CliOutput.Format(output.Format, tabular: false);
            var kind = CliValues.Choice(@as, "--as", "planetocentric", "planetodetic", "cartesian");
            var at = CliValues.Epoch(epoch.Epoch, "--epoch");
            var correction = CliValues.Enumeration<Aberration>(aberration.Aberration, "--aberration");

            CliKernels.Load(kernels.Kernels);
            var objectId = CliValues.Object(@object, "object");

            // A site has no sub-observer point (#681). A site by the library's rule (SpiceAPI.IsSiteOfNamedBody); NOTO and
            // NEW NORCIA, of no body SPICE names, are objects of the kernels and projected (#750)
            if (SpiceAPI.IsSiteOfNamedBody(objectId))
            {
                throw new CliInputException(
                    $"object: sub-point projects a celestial body or a spacecraft; {@object} is a site.");
            }

            var celestialItem = Helpers.CreateCelestialItem(objectId, at);
            var celestialBody = new CelestialBody(CliValues.Object(body, "body"));
            var planetocentric = celestialItem.SubObserverPoint(celestialBody, at, correction);

            switch (kind)
            {
                case "cartesian":
                    var position = planetocentric.ToCartesianCoordinates().Normalize() * planetocentric.RadiusFromPlanetocentricLatitude(celestialBody);
                    Write(format, position.ToString(), CliOutput.Cartesian(position));
                    break;
                case "planetodetic":
                    var planetodetic = planetocentric.ToPlanetodetic(celestialBody);
                    Write(format, planetodetic.ToString(), CliOutput.Planetodetic(planetodetic));
                    break;
                default:
                    Write(format, planetocentric.ToString(), CliOutput.Planetocentric(planetocentric));
                    break;
            }
        });
    }

    [Command("angular-separation", Description = "The angle between two objects seen from an observer, in rad")]
    public int AngularSeparation(
        [Argument("observer", Description = "Observer: " + ObjectDescription)]
        string observer,
        [Argument("first", Description = "First object: " + ObjectDescription)]
        string first,
        [Argument("second", Description = "Second object: " + ObjectDescription)]
        string second,
        KernelOptions kernels,
        EpochOptions epoch,
        AberrationOptions aberration,
        OutputOptions output)
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            var format = CliOutput.Format(output.Format, tabular: false);
            var at = CliValues.Epoch(epoch.Epoch, "--epoch");
            var correction = CliValues.Enumeration<Aberration>(aberration.Aberration, "--aberration");

            CliKernels.Load(kernels.Kernels);
            var observerObject = Helpers.CreateLocalizable(CliValues.Object(observer, "observer"), at);
            var firstObject = Helpers.CreateLocalizable(CliValues.Object(first, "first"), at);
            var secondObject = Helpers.CreateLocalizable(CliValues.Object(second, "second"), at);

            var angle = observerObject.AngularSeparation(at, firstObject, secondObject, correction);
            Write(format, CliOutput.Real(angle), [new CliOutput.Field("angle_rad", angle)]);
        });
    }

    /// <summary>Writes a record: its text, or its fields in json.</summary>
    internal static void Write(OutputFormat format, string text, IReadOnlyList<CliOutput.Field> fields)
    {
        if (format == OutputFormat.Json)
        {
            CliOutput.WriteJson(CliContext.Out, fields);
        }
        else
        {
            CliContext.Out.WriteLine(text);
        }
    }
}
