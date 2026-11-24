using System;
using System.Collections.Generic;
using System.Linq;
using Cocona;
using IO.Astrodynamics.CLI.Cli;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CLI.Commands;

/// <summary>
/// The window searches of CSPICE (<c>gfdist</c>, <c>gfposc</c>, <c>gfoclt</c>, <c>gftfov</c>, <c>gfilum</c>) through
/// <see cref="SpiceAPI"/>: the CLI runs the CSPICE searches only, on objects of the kernels (R29 of #668, a known
/// limitation).
/// </summary>
public class GeometryFinderCommand
{
    private const string RelationDescription =
        "Relation: <, >, =, <=, >=, !, ABSMIN, ABSMAX, LOCMIN or LOCMAX, in any case (<= and >= are searched as < and >, whose windows are closed; ! is the window less the instants of =)";

    private const string AdjustDescription =
        "Adjustment of ABSMIN and ABSMAX: the windows where the value is within it of the absolute extremum, in the unit of --value";

    [Command("find-windows-from-coordinate-constraint", Description = "The windows where a coordinate of a target seen from an observer meets a relation (CSPICE gfposc)")]
    public int CoordinateConstraint(
        [Argument("observer", Description = "Observer: " + EphemerisCommand.ObjectDescription)]
        string observer,
        [Argument("target", Description = "Target: " + EphemerisCommand.ObjectDescription)]
        string target,
        [Option("system", ValueName = "system", Description = "Coordinate system: RECTANGULAR, RA/DEC, GEODETIC or PLANETOGRAPHIC, in any case (the member name RaDec read too)")]
        string system,
        [Option("coordinate", ValueName = "coordinate", Description = "Coordinate of the system: X, Y, Z, ALTITUDE, DECLINATION, LATITUDE, LONGITUDE, RADIUS, RANGE or RIGHT ASCENSION, in any case (the member name RightAscension read too)")]
        string coordinate,
        [Option("relation", ValueName = "relation", Description = RelationDescription)]
        string relation,
        [Option("value", ValueName = "value", Description = "Value of the relation: a length in m (or km) for X, Y, Z, ALTITUDE, RADIUS and RANGE, an angle in rad (or deg) otherwise")]
        string value,
        KernelOptions kernels,
        WindowOptions window,
        AberrationOptions aberration,
        TimeScaleOptions timeScale,
        OutputOptions output,
        [Option("adjust", ValueName = "value", Description = AdjustDescription)]
        string adjust = "0",
        [Option("frame", ValueName = "frame", Description = "Frame of the coordinates: ICRF (written J2000) or a frame of the kernels")]
        string frame = "ICRF")
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            var search = Search.Read(window, aberration, timeScale, output);
            var coordinateSystem = CliValues.Enumeration<CoordinateSystem>(Required(system, "--system"), "--system");
            var coordinateValue = CliValues.Enumeration<Coordinate>(Required(coordinate, "--coordinate"), "--coordinate");
            var dimension = coordinateValue is Coordinate.X or Coordinate.Y or Coordinate.Z or Coordinate.Altitude or Coordinate.Radius or Coordinate.Range
                ? Dimension.Length
                : Dimension.Angle;
            var operation = CliValues.Enumeration<RelationnalOperator>(Required(relation, "--relation"), "--relation");
            var number = CliValues.Quantity(Required(value, "--value"), dimension, "--value");
            var adjustment = CliValues.Quantity(adjust, dimension, "--adjust");

            CliKernels.Load(kernels.Kernels);
            var observerId = CliValues.Object(observer, "observer");
            var targetId = CliValues.Object(target, "target");
            var searchFrame = CliValues.Frame(frame, "--frame");

            search.Write(SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(search.Window, observerId, targetId, searchFrame, coordinateSystem,
                coordinateValue, operation, number, adjustment, search.Aberration, search.Step));
        });
    }

    [Command("find-windows-from-distance-constraint", Description = "The windows where the distance of a target from an observer meets a relation (CSPICE gfdist)")]
    public int DistanceConstraint(
        [Argument("observer", Description = "Observer: " + EphemerisCommand.ObjectDescription)]
        string observer,
        [Argument("target", Description = "Target: " + EphemerisCommand.ObjectDescription)]
        string target,
        [Option("relation", ValueName = "relation", Description = RelationDescription)]
        string relation,
        [Option("value", ValueName = "distance", Description = "Distance of the relation, in m (or km)")]
        string value,
        KernelOptions kernels,
        WindowOptions window,
        AberrationOptions aberration,
        TimeScaleOptions timeScale,
        OutputOptions output)
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            var search = Search.Read(window, aberration, timeScale, output);
            var operation = CliValues.Enumeration<RelationnalOperator>(Required(relation, "--relation"), "--relation");
            var distance = CliValues.Quantity(Required(value, "--value"), Dimension.Length, "--value");

            CliKernels.Load(kernels.Kernels);
            var observerId = CliValues.Object(observer, "observer");
            var targetId = CliValues.Object(target, "target");

            search.Write(SpiceAPI.Instance.FindWindowsOnDistanceConstraint(search.Window, observerId, targetId, operation, distance,
                search.Aberration, search.Step));
        });
    }

    [Command("find-windows-from-occultation-constraint", Description = "The windows where a front body occults a target seen from an observer (CSPICE gfoclt)")]
    public int OccultationConstraint(
        [Argument("observer", Description = "Observer: " + EphemerisCommand.ObjectDescription)]
        string observer,
        [Argument("target", Description = "Target occulted: " + EphemerisCommand.ObjectDescription)]
        string target,
        [Argument("front", Description = "Front body, which occults the target: " + EphemerisCommand.ObjectDescription)]
        string front,
        [Option("target-shape", ValueName = "point|ellipsoid", Description = "Shape of the target: point or ellipsoid")]
        string targetShape,
        [Option("front-shape", ValueName = "point|ellipsoid", Description = "Shape of the front body: point or ellipsoid")]
        string frontShape,
        [Option("occultation", ValueName = "full|annular|partial|any", Description = "Occultation: full, annular, partial or any")]
        string occultation,
        KernelOptions kernels,
        WindowOptions window,
        AberrationOptions aberration,
        TimeScaleOptions timeScale,
        OutputOptions output)
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            var search = Search.Read(window, aberration, timeScale, output);
            var targetShapeType = CliValues.Enumeration<ShapeType>(Required(targetShape, "--target-shape"), "--target-shape");
            var frontShapeType = CliValues.Enumeration<ShapeType>(Required(frontShape, "--front-shape"), "--front-shape");
            var occultationType = CliValues.Enumeration<OccultationType>(Required(occultation, "--occultation"), "--occultation");

            CliKernels.Load(kernels.Kernels);
            var observerId = CliValues.Object(observer, "observer");
            var targetId = CliValues.Object(target, "target");
            var frontId = CliValues.Object(front, "front");

            search.Write(SpiceAPI.Instance.FindWindowsOnOccultationConstraint(search.Window, observerId, targetId, targetShapeType, frontId,
                frontShapeType, occultationType, search.Aberration, search.Step));
        });
    }

    [Command("find-windows-from-fov-constraint",
        Description = "The windows where a target is in the field of view of an instrument of a spacecraft (CSPICE gftfov)")]
    public int InFieldOfViewConstraint(
        [Argument("observer", Description = "Spacecraft that carries the instrument: " + EphemerisCommand.ObjectDescription)]
        string observer,
        [Argument("instrument", Description = "Instrument: " + EphemerisCommand.ObjectDescription)]
        string instrument,
        [Argument("target", Description = "Target: " + EphemerisCommand.ObjectDescription)]
        string target,
        [Option("target-frame", ValueName = "frame", Description = "Body-fixed frame of the target (ITRF93, IAU_MOON, ...), which orients its ellipsoid")]
        string targetFrame,
        [Option("target-shape", ValueName = "point|ellipsoid", Description = "Shape of the target: point or ellipsoid")]
        string targetShape,
        KernelOptions kernels,
        WindowOptions window,
        AberrationOptions aberration,
        TimeScaleOptions timeScale,
        OutputOptions output)
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            var search = Search.Read(window, aberration, timeScale, output);
            var shape = CliValues.Enumeration<ShapeType>(Required(targetShape, "--target-shape"), "--target-shape");

            CliKernels.Load(kernels.Kernels);
            var observerId = CliValues.Object(observer, "observer");
            var instrumentId = CliValues.Object(instrument, "instrument");
            var targetId = CliValues.Object(target, "target");
            var frame = CliValues.Frame(Required(targetFrame, "--target-frame"), "--target-frame");

            search.Write(SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(search.Window, observerId, instrumentId, targetId, frame, shape,
                search.Aberration, search.Step));
        });
    }

    [Command("find-windows-from-illumination-constraint", Description = "The windows where an illumination angle at a site of a target body, seen from an observer, meets a relation (CSPICE gfilum)")]
    public int IlluminationConstraint(
        [Argument("observer", Description = "Observer: " + EphemerisCommand.ObjectDescription)]
        string observer,
        [Argument("target", Description = "Target body of the site: " + EphemerisCommand.ObjectDescription)]
        string target,
        [Argument("source", Description = "Illumination source: " + EphemerisCommand.ObjectDescription)]
        string source,
        [Option("fixed-frame", ValueName = "frame", Description = "Body-fixed frame of the target (ITRF93, IAU_MOON, ...)")]
        string fixedFrame,
        [Option("site", ValueName = "\"longitude latitude altitude\"", Description = "Planetodetic coordinates of the site: longitude and latitude in rad (or deg), altitude in m (or km)")]
        string site,
        [Option("angle", ValueName = "phase|incidence|emission", Description = "Illumination angle: phase, incidence or emission")]
        string angle,
        [Option("relation", ValueName = "relation", Description = RelationDescription)]
        string relation,
        [Option("value", ValueName = "angle", Description = "Angle of the relation, in rad (or deg)")]
        string value,
        KernelOptions kernels,
        WindowOptions window,
        AberrationOptions aberration,
        TimeScaleOptions timeScale,
        OutputOptions output,
        [Option("adjust", ValueName = "angle", Description = AdjustDescription)]
        string adjust = "0")
    {
        return CliContext.Execute(output.Verbose, () =>
        {
            var search = Search.Read(window, aberration, timeScale, output);
            var coordinates = CliValues.Quantities([Required(site, "--site")], [Dimension.Angle, Dimension.Angle, Dimension.Length], "--site");
            var illuminationAngle = CliValues.Enumeration<IlluminationAngle>(Required(angle, "--angle"), "--angle");
            var operation = CliValues.Enumeration<RelationnalOperator>(Required(relation, "--relation"), "--relation");
            var number = CliValues.Quantity(Required(value, "--value"), Dimension.Angle, "--value");
            var adjustment = CliValues.Quantity(adjust, Dimension.Angle, "--adjust");

            CliKernels.Load(kernels.Kernels);
            var observerId = CliValues.Object(observer, "observer");
            var targetId = CliValues.Object(target, "target");
            var sourceId = CliValues.Object(source, "source");
            var frame = CliValues.Frame(Required(fixedFrame, "--fixed-frame"), "--fixed-frame");

            search.Write(SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(search.Window, observerId, targetId, frame,
                new Planetodetic(coordinates[0], coordinates[1], coordinates[2]), illuminationAngle, operation, number, adjustment,
                search.Aberration, search.Step, sourceId));
        });
    }

    private static string Required(string text, string argument) =>
        text ?? throw new CliInputException($"{argument} is required.");

    /// <summary>The window, step, aberration, scale and format of a search, and the writer of its windows.</summary>
    private sealed class Search
    {
        internal Window Window { get; private init; }
        internal TimeSpan Step { get; private init; }
        internal Aberration Aberration { get; private init; }
        private TimeScale Scale { get; init; }
        private OutputFormat Format { get; init; }

        internal static Search Read(WindowOptions window, AberrationOptions aberration, TimeScaleOptions timeScale, OutputOptions output)
        {
            var format = CliOutput.Format(output.Format, tabular: true);
            var searchWindow = CliValues.Window(window.Begin, window.End);
            return new Search
            {
                Format = format,
                Window = searchWindow,
                Step = CliValues.Step(window.Step, "--step"),
                Aberration = CliValues.Enumeration<Aberration>(aberration.Aberration, "--aberration"),
                Scale = CliValues.TimeScale(timeScale.TimeScale, searchWindow.StartDate.Scale)
            };
        }

        /// <summary>The windows found, each end in the scale of the output.</summary>
        internal void Write(IEnumerable<Window> windows)
        {
            var found = windows.Select(x => CliTime.Relabel(x, Scale)).ToArray();
            if (Format == OutputFormat.Csv)
            {
                CliOutput.WriteCsv(CliContext.Out, CliOutput.WindowHeader, found.Select(CliOutput.Window));
                return;
            }

            foreach (var window in found)
            {
                CliContext.Out.WriteLine(window.ToString());
            }
        }
    }
}
