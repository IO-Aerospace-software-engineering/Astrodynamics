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

public class SiteFrame : Frame
{
    public Site Site { get; }

    internal SiteFrame(string name, Site site) : base(name, 1000000 + site.NaifId)
    {
        Site = site;
    }

    public async Task WriteAsync(FileInfo outputFile)
    {
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

    public override StateOrientation GetStateOrientationToICRF(Time epoch)
    {
        return _stateOrientationsToICRF.GetOrAdd(epoch, _ =>
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

            var rotation = celestialBodyFrameToICRF.Rotation * mtx.ToQuaternion().Conjugate();
            var angularVelocity = celestialBodyFrameToICRF.AngularVelocity.Rotate(rotation.Conjugate());
            return new StateOrientation(rotation, angularVelocity, epoch, this);
        });
    }
}