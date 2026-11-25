// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;
using Quaternion = IO.Astrodynamics.DTO.Quaternion;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;

namespace IO.Astrodynamics.Surface;

/// <summary>
/// The topocentric frame of a site, <c>NAME_TOPO</c> of id 1000000 + NaifId: X toward the north, Y toward the west, Z
/// toward the zenith. For a site from coordinates, the frame of those coordinates; for a site from the kernels, the frame
/// the loaded kernels define for its id (DSS-13_TOPO for DSS-13, #690), or the frame of its J2000 coordinates when none
/// does (<see cref="GetStateOrientationToICRF"/>). An ephemeris read in the frame of a site from coordinates, or of a site
/// from the kernels whose frame id no loaded kernel defines, is read in J2000 and rotated by that orientation (#682, third
/// review of #690); in a kernel frame, SPICE reads it by the kernel's name. <see cref="SpiceAPI.TransformFrame(Time, Frame, Frame)"/>
/// with such a frame composes that orientation through ICRF, as for TEME (fourth round of #668; before, it threw).
/// </summary>
public class SiteFrame : Frame
{
    public Site Site { get; }

    internal SiteFrame(string name, Site site) : base(name, 1000000 + site.NaifId)
    {
        Site = site;
    }

    /// <summary>
    /// Writes the frame kernel of the topocentric frame of the site, whose kernel variable <c>FRAME_&lt;SITE&gt;_TOPO</c>
    /// holds the upper-cased name of the site: a TK frame of the frame of its body, the frame
    /// <see cref="GetStateOrientationToICRF"/> gives.
    /// </summary>
    /// <remarks>
    /// For a site built from coordinates, or from the kernels when no loaded kernel defines its frame id, the frame of
    /// <see cref="Surface.Site.Planetodetic"/>, as angles. For a site from the kernels whose frame id, 1000000 + NaifId, the
    /// loaded kernels define, that kernel frame (#690, second review): its name in the kernels (DSS-13_TOPO for DSS-13,
    /// whatever the name of the site) and its rotation to the frame of the body read from SPICE (<c>pxform_c</c>), written
    /// as the matrix of a TK frame (<c>TKFRAME_&lt;id&gt;_MATRIX</c>, the rotation from the frame to the frame of the body,
    /// in column order), so that the file defines the frame the kernels define, to the rounding of the matrix; loaded
    /// beside those kernels it assigns their frame again. Before 11.0.0 it wrote the frame of
    /// <see cref="Surface.Site.Planetodetic"/> under the id and, for a site named as its kernel frame, the name of the
    /// kernel frame: 7.76e-8 rad from DSS-13_TOPO.
    /// </remarks>
    /// <param name="outputFile">The frame kernel to write.</param>
    /// <exception cref="InvalidOperationException">The name of the site, or of its kernel frame, makes a kernel variable
    /// name SPICE cannot read: a blank, a tab or another whitespace or control character, a quote, a parenthesis, a comma or
    /// '=', or more than 32 bytes in UTF-8, so 21 ASCII characters at most for the name (Kernel Required Reading, "Variable
    /// Name Rules", #676); or the kernel frame of a site from the kernels is not fixed in the frame of its body, which a TK
    /// frame of that body cannot hold. Nothing is written.</exception>
    public async Task WriteAsync(FileInfo outputFile)
    {
        var kernelFrameName = Site.IsSpiceBacked ? SpiceAPI.KernelFrameNameOf(Id!.Value) : null;
        var frameName = kernelFrameName ?? Name;
        Kernels.TextKernelNames.Check("FRAME_" + frameName.ToUpper(), $"the site '{Site.Name}'");
        string matrix = kernelFrameName != null ? KernelFrameMatrix(kernelFrameName) : null;
        var template = matrix != null ? "IO.Astrodynamics.Templates.SiteKernelFrameTemplate.tf" : "IO.Astrodynamics.Templates.SiteFrameTemplate.tf";
        await using var stream = this.GetType().Assembly.GetManifestResourceStream(template);
        using StreamReader sr = new StreamReader(stream ?? throw new InvalidOperationException());
        var templateData = await sr.ReadToEndAsync();
        var data = templateData
            .Replace("{sitename}", Site.Name.ToUpper())
            .Replace("{siteid}", Site.NaifId.ToString(CultureInfo.InvariantCulture))
            .Replace("{sitenametopo}", frameName.ToUpper())
            .Replace("{frameid}", Id!.Value.ToString(CultureInfo.InvariantCulture))
            .Replace("{fixedframe}", Site.CelestialBody.Frame.Name.ToUpper())
            .Replace("{matrix}", matrix)
            .Replace("{long}", (-Site.Planetodetic.Longitude).ToString(CultureInfo.InvariantCulture))
            .Replace("{colat}", (-(Constants.HalfPi - Site.Planetodetic.Latitude)).ToString(CultureInfo.InvariantCulture));
        await using var sw = new StreamWriter(outputFile.FullName);
        await sw.WriteAsync(data);
    }

    // The rotation from the kernel frame of the site to the frame of its body, as SPICE reads it, in column order: column i
    // the axis i of the kernel frame in the frame of the body. Read at J2000 TDB and 6 h later, half a turn of the Earth:
    // a frame that is not fixed in the frame of the body is rejected (a TK frame of the body is the same at both)
    private string KernelFrameMatrix(string kernelFrameName)
    {
        var bodyFrame = Site.CelestialBody.Frame;
        var atJ2000 = SpiceAPI.Instance.TransformFrame(Time.J2000TDB, this, bodyFrame).Rotation;
        var later = SpiceAPI.Instance.TransformFrame(Time.J2000TDB.AddSeconds(6.0 * 3600.0), this, bodyFrame).Rotation;
        var difference = atJ2000.Conjugate() * later;
        double angle = 2.0 * System.Math.Atan2(difference.VectorPart.Magnitude(), System.Math.Abs(difference.W));
        if (angle > KernelFrameFixedTolerance)
        {
            throw new InvalidOperationException(
                $"The frame {kernelFrameName} the loaded kernels define for the site '{Site.Name}' is not fixed in {bodyFrame.Name} " +
                $"(it turns by {angle.ToString("E3", CultureInfo.InvariantCulture)} rad relative to it in 6 h): a TK frame of {bodyFrame.Name} " +
                "cannot hold it. Use the frame kernel that defines it.");
        }

        // One column per line: CSPICE reads the first 132 characters of a line of a text kernel (the nine components on one
        // line, 240 characters, gave 6)
        var columns = new[] { Vector3.VectorX, Vector3.VectorY, Vector3.VectorZ }.Select(axis => axis.Rotate(atJ2000));
        return string.Join(Environment.NewLine + "                               ",
            columns.Select(c => string.Join(" ", new[] { c.X, c.Y, c.Z }.Select(x => x.ToString("R", CultureInfo.InvariantCulture)))));
    }

    // A TK chain gives the same rotation at every epoch, to the bit; a frame that moves relative to the body by less than
    // this in 6 h is taken as fixed in it
    private const double KernelFrameFixedTolerance = 1e-12;

    /// <summary>
    /// The orientation of the topocentric frame of the site relative to ICRF: X toward the north, Y toward the west and Z
    /// toward the zenith.
    /// </summary>
    /// <remarks>
    /// The frame of a site from the kernels (<see cref="Surface.Site.IsSpiceBacked"/>) whose id, 1000000 + NaifId, the
    /// loaded kernels define is that kernel frame (DSS-13_TOPO of earth_topo_201023.tf for DSS-13), read from SPICE at each
    /// call and not stored, as the frame of a kernel-backed spacecraft (#690): the same frame as the one SPICE reads by
    /// name, on every path. Before 11.0.0 it was built, as the frame of a site from coordinates still is, from
    /// <see cref="Surface.Site.Planetodetic"/>, the position of the site at J2000 TDB on the ellipsoid of the body, which
    /// for DSS-13 departs from DSS-13_TOPO (the coordinates of the station at 2020-10-23 on the WGS84 spheroid) by 7.76e-8
    /// rad, most of it the plate motion between the two epochs. A site from the kernels whose frame no loaded kernel
    /// defines keeps the frame of <see cref="Surface.Site.Planetodetic"/>, computed at each call, and an ephemeris read in
    /// it rotates by that frame, as for a site from coordinates (third review of #690; before, it reached SPICE by the
    /// site's own name and threw SPICE(UNKNOWNFRAME)).
    /// </remarks>
    public override StateOrientation GetStateOrientationToICRF(Time epoch)
    {
        if (Site.IsSpiceBacked)
        {
            return SpiceAPI.KernelDefinesFrame(Id!.Value)
                ? SpiceAPI.Instance.TransformFrame(epoch, this, ICRF, null, null)
                : FromPlanetodetic(epoch);
        }

        return LastOrientation(epoch, FromPlanetodetic);
    }

    // The frame of the planetodetic coordinates of the site: north, west and zenith, fixed in the frame of its body
    private StateOrientation FromPlanetodetic(Time epoch)
    {
        var celestialBodyFrameToICRF = Site.CelestialBody.Frame.ToFrame(ICRF, epoch);
        double[,] rotationMatrix = new double[3, 3];
        rotationMatrix[0, 0] = -System.Math.Sin(Site.Planetodetic.Latitude) * System.Math.Cos(Site.Planetodetic.Longitude);
        rotationMatrix[0, 1] = -System.Math.Sin(Site.Planetodetic.Latitude) * System.Math.Sin(Site.Planetodetic.Longitude);
        rotationMatrix[0, 2] = System.Math.Cos(Site.Planetodetic.Latitude);
        rotationMatrix[1, 0] = System.Math.Sin(Site.Planetodetic.Longitude);
        rotationMatrix[1, 1] = -System.Math.Cos(Site.Planetodetic.Longitude);
        rotationMatrix[1, 2] = 0.0;
        rotationMatrix[2, 0] = System.Math.Cos(Site.Planetodetic.Latitude) * System.Math.Cos(Site.Planetodetic.Longitude);
        rotationMatrix[2, 1] = System.Math.Cos(Site.Planetodetic.Latitude) * System.Math.Sin(Site.Planetodetic.Longitude);
        rotationMatrix[2, 2] = System.Math.Sin(Site.Planetodetic.Latitude);
        Matrix mtx = Matrix.Wrap(rotationMatrix);

        var bodyToTopocentric = mtx.ToQuaternion();
        var rotation = celestialBodyFrameToICRF.Rotation * bodyToTopocentric.Conjugate();
        // The site frame is fixed in the body frame: same angular velocity of ICRF relative to it, which comes
        // in body axes (SPICE convention, see Frame.GetStateOrientationToICRF) and goes to topocentric axes.
        var angularVelocity = celestialBodyFrameToICRF.AngularVelocity.Rotate(bodyToTopocentric);
        return new StateOrientation(rotation, angularVelocity, epoch, this);
    }
}
