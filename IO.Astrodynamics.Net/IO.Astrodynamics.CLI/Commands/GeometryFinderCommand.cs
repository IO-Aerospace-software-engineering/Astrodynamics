using System;
using System.IO;
using System.Threading.Tasks;
using Cocona;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.CLI.Commands;

public class GeometryFinderCommand
{
    public GeometryFinderCommand()
    {
    }

    [Command("find-windows-from-coordinate-constraint", Description = "Find time windows when constraint occurs")]
    public Task CoordinateConstraint(
        [Argument(Description = "Kernels directory path")]
        string kernelsPath,
        [Argument(Description = "Observer identifier (Naif Identifier)")]
        int observerId,
        [Argument(Description = "Target identifier (Naif Identifier)")]
        int targetId,
        [Argument(Description = "CoordinateSystem (RECTANGULAR, LATITUDINAL, RA/DEC, SPHERICAL, CYLINDRICAL, GEODETIC, PLANETOGRAPHIC)")]
        string coordinateSystem,
        [Argument(Description = "Coordinate (X,Y,Z,ALTITUDE,COLATITUDE,DECLINATION,LATITUDE,LONGITUDE,RADIUS,RANGE,RIGHT ASCENSION)")] string coordinate,
        [Argument(Description = "Relational operator used to define search constraint (<,>,=,ABSMIN,ABSMAX,LOCMIN,LOCMAX)")]
        string relationalOperator,
        [Argument(Description = "Value used by operator")]
        double value,
        WindowParameters window,
        [Argument(Description = "Initial step size <d.hh:mm:ss.fff>")]
        TimeSpan step,
        [Argument(Description = "Aberration")] string aberration = "None",
        [Argument(Description = "Adjust value (Used by ABSMIN and ABSMAX only. Return window when value is between ABSMIN and ABSMIN + adjust and ABSMAX - adjust and ABSMAX)")]
        double adjustValue = 0.0,
        [Argument(Description = "Frame")] string frame = "ICRF")
    {
        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));
        if (frame.Equals("icrf", StringComparison.InvariantCultureIgnoreCase))
        {
            frame = "j2000";
        }

        var win = new Window(Helpers.ConvertDateTimeInput(window.Begin), Helpers.ConvertDateTimeInput(window.End));
        var abe = Enum.Parse<Aberration>(aberration, true);
        var coorSys = Enum.Parse<CoordinateSystem>(coordinateSystem, true);
        var coor = Enum.Parse<Coordinate>(coordinate, true);
        var op = Enumeration.GetValueFromDescription<RelationnalOperator>(relationalOperator);

        var res = API.Instance.FindWindowsOnCoordinateConstraint(win, observerId, targetId, new Frame(frame), coorSys, coor, op, value, adjustValue, abe, step);

        foreach (var windowResult in res)
        {
            Console.WriteLine(windowResult);
        }

        return Task.CompletedTask;
    }

    [Command("find-windows-from-distance-constraint", Description = "Find time windows when constraint occurs")]
    public Task DistanceConstraint(
        [Argument(Description = "Kernels directory path")]
        string kernelsPath,
        [Argument(Description = "Observer identifier (Naif Identifier)")]
        int observerId,
        [Argument(Description = "Target identifier (Naif Identifier)")]
        int targetId,
        [Argument(Description = "Relational operator used to define search constraint (<,>,=,ABSMIN,ABSMAX,LOCMIN,LOCMAX)")]
        string relationalOperator,
        [Argument(Description = "Value used by operator")]
        double value,
        WindowParameters window,
        [Argument(Description = "Initial step size <d.hh:mm:ss.fff>")]
        TimeSpan step,
        [Argument(Description = "Aberration")] string aberration = "None"
    )
    {
        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        var win = new Window(Helpers.ConvertDateTimeInput(window.Begin), Helpers.ConvertDateTimeInput(window.End));
        var abe = Enum.Parse<Aberration>(aberration, true);
        var op = Enumeration.GetValueFromDescription<RelationnalOperator>(relationalOperator);

        var res = API.Instance.FindWindowsOnDistanceConstraint(win, observerId, targetId, op, value, abe, step);

        foreach (var windowResult in res)
        {
            Console.WriteLine(windowResult);
        }

        return Task.CompletedTask;
    }

    [Command("find-windows-from-occultation-constraint", Description = "Find time windows when constraint occurs")]
    public Task OccultationConstraint(
        [Argument(Description = "Kernels directory path")]
        string kernelsPath,
        [Argument(Description = "Observer identifier (Naif Identifier)")]
        int observerId,
        [Argument(Description = "Target identifier (Naif Identifier)")]
        int targetId,
        [Argument(Description = "Target shape (Point or Ellipsoid")]
        string targetShape,
        [Argument(Description = "Front body identifier (Naif Identifier)")]
        int frontBodyId,
        [Argument(Description = "Front body shape (Point or Ellipsoid")]
        string frontBodyShape,
        [Argument(Description = "Occultation type (Full or Annular or Partial or Any)")]
        string occultationType,
        WindowParameters window,
        [Argument(Description = "Initial step size <d.hh:mm:ss.fff>")]
        TimeSpan step,
        [Argument(Description = "Aberration")] string aberration = "None"
    )
    {
        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        var win = new Window(Helpers.ConvertDateTimeInput(window.Begin), Helpers.ConvertDateTimeInput(window.End));
        var abe = Enum.Parse<Aberration>(aberration, true);
        var targetShp = Enum.Parse<ShapeType>(targetShape, true);
        var frontShp = Enum.Parse<ShapeType>(frontBodyShape, true);
        var occultation = Enum.Parse<OccultationType>(occultationType, true);

        var res = API.Instance.FindWindowsOnOccultationConstraint(win, observerId, targetId, targetShp, frontBodyId, frontShp, occultation, abe, step);

        foreach (var windowResult in res)
        {
            Console.WriteLine(windowResult);
        }

        return Task.CompletedTask;
    }

    [Command("find-windows-from-FOV-constraint", Description = "Find time windows when a target is in field of view of given instrument")]
    public Task InFieldOfViewConstraint(
        [Argument(Description = "Kernels directory path")]
        string kernelsPath,
        [Argument(Description = "Observer identifier (Naif Identifier)")]
        int observerId,
        [Argument(Description = "instrument identifier (Naif Identifier)")]
        int instrumentId,
        [Argument(Description = "Target identifier (Naif Identifier)")]
        int targetId,
        [Argument(Description = "Target frame")]
        string targetFrame,
        [Argument(Description = "Target shape (Point or Ellipsoid")]
        string targetShape,
        WindowParameters window,
        [Argument(Description = "Initial step size <d.hh:mm:ss.fff>")]
        TimeSpan step,
        [Argument(Description = "Aberration")] string aberration = "None"
    )
    {
        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        var win = new Window(Helpers.ConvertDateTimeInput(window.Begin), Helpers.ConvertDateTimeInput(window.End));
        var abe = Enum.Parse<Aberration>(aberration, true);
        var targetShp = Enum.Parse<ShapeType>(targetShape, true);

        var res = API.Instance.FindWindowsInFieldOfViewConstraint(win, observerId, instrumentId, targetId, new Frame(targetFrame), targetShp, abe, step);

        foreach (var windowResult in res)
        {
            Console.WriteLine(windowResult);
        }

        return Task.CompletedTask;
    }

    [Command("find-windows-from-illumination-constraint", Description = "Find time windows when constraint occurs")]
    public Task IlluminationConstraint(
        [Argument(Description = "Kernels directory path")]
        string kernelsPath,
        [Argument(Description = "Observer identifier (Naif Identifier)")]
        int observerId,
        [Argument(Description = "Target identifier (Naif Identifier)")]
        int targetId,
        [Argument(Description = "Illumination source identifier (Naif Identifier)")]
        int illuminationId,
        [Argument(Description = "Target frame")]
        string fixedFrame,
        PlanetodeticParameters planetodetic,
        [Argument(Description = "Illumination type - Phase or Incidence or Emission")]
        string illuminationType,
        [Argument(Description = "Relational operator (<,>,=,ABSMIN,ABSMAX,LOCMIN,LOCMAX)")]
        string relationalOperator,
        [Argument(Description = "Value")] double value,
        WindowParameters window,
        [Argument(Description = "Initial step size <d.hh:mm:ss.fff>")]
        TimeSpan step,
        [Argument(Description = "Aberration")] string aberration = "None",
        [Argument(Description = "Adjust value (Used by ABSMIN and ABSMAX. Return window when value is between ABSMIN and ABSMIN + adjust and ABSMAX - adjust and ABSMAX)")]
        double adjustValue=0.0
    )
    {
        API.Instance.LoadKernels(new DirectoryInfo(kernelsPath));

        var win = new Window(Helpers.ConvertDateTimeInput(window.Begin), Helpers.ConvertDateTimeInput(window.End));
        var abe = Enum.Parse<Aberration>(aberration, true);
        var coordinates = Helpers.ConvertToPlanetodetic(planetodetic.Planetodetic);
        var illuminationAngle = Enum.Parse<IlluminationAngle>(illuminationType, true);
        var relOperator = Enumeration.GetValueFromDescription<RelationnalOperator>(relationalOperator);
        var res = API.Instance.FindWindowsOnIlluminationConstraint(win, observerId, targetId, new Frame(fixedFrame), coordinates, illuminationAngle, relOperator, value,
            adjustValue, abe, step, illuminationId);

        foreach (var windowResult in res)
        {
            Console.WriteLine(windowResult);
        }

        return Task.CompletedTask;
    }
}