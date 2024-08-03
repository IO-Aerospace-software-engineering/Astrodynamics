// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using IO.Astrodynamics.Frames;

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
}