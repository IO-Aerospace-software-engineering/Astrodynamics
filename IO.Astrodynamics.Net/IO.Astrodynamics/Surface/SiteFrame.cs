// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
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
/// does (<see cref="GetStateOrientationToICRF"/>).
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
    /// holds the upper-cased name of the site.
    /// </summary>
    /// <param name="outputFile">The frame kernel to write.</param>
    /// <exception cref="InvalidOperationException">The name of the site makes a kernel variable name SPICE cannot read: a
    /// blank, a tab or another whitespace or control character, a quote, a parenthesis, a comma or '=', or more than 32
    /// bytes in UTF-8, so 21 ASCII characters at most for the name (Kernel Required Reading, "Variable Name Rules", #676);
    /// nothing is written.</exception>
    public async Task WriteAsync(FileInfo outputFile)
    {
        Kernels.TextKernelNames.Check("FRAME_" + Name.ToUpper(), $"the site '{Site.Name}'");
        await using var stream = this.GetType().Assembly.GetManifestResourceStream("IO.Astrodynamics.Templates.SiteFrameTemplate.tf");
        using StreamReader sr = new StreamReader(stream ?? throw new InvalidOperationException());
        var templateData = await sr.ReadToEndAsync();
        var data = templateData
            .Replace("{sitename}", Site.Name.ToUpper())
            .Replace("{siteid}", Site.NaifId.ToString())
            .Replace("{sitenametopo}", Name.ToUpper())
            .Replace("{frameid}", Id.ToString())
            .Replace("{fixedframe}", Site.CelestialBody.Frame.Name.ToUpper())
            .Replace("{long}", (-Site.Planetodetic.Longitude).ToString(CultureInfo.InvariantCulture))
            .Replace("{colat}", (-(Constants.PI2 - Site.Planetodetic.Latitude)).ToString(CultureInfo.InvariantCulture));
        await using var sw = new StreamWriter(outputFile.FullName);
        await sw.WriteAsync(data);
    }

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
    /// defines keeps the frame of <see cref="Surface.Site.Planetodetic"/>, computed at each call.
    /// </remarks>
    public override StateOrientation GetStateOrientationToICRF(Time epoch)
    {
        if (Site.IsSpiceBacked)
        {
            return SpiceAPI.KernelDefinesFrame(Id!.Value)
                ? SpiceAPI.Instance.TransformFrame(epoch, this, ICRF)
                : FromPlanetodetic(epoch);
        }

        return _stateOrientationsToICRF.GetOrAdd(epoch, FromPlanetodetic);
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
        Matrix mtx = new Matrix(rotationMatrix);

        var bodyToTopocentric = mtx.ToQuaternion();
        var rotation = celestialBodyFrameToICRF.Rotation * bodyToTopocentric.Conjugate();
        // The site frame is fixed in the body frame: same angular velocity of ICRF relative to it, which comes
        // in body axes (SPICE convention, see Frame.GetStateOrientationToICRF) and goes to topocentric axes.
        var angularVelocity = celestialBodyFrameToICRF.AngularVelocity.Rotate(bodyToTopocentric);
        return new StateOrientation(rotation, angularVelocity, epoch, this);
    }
}
