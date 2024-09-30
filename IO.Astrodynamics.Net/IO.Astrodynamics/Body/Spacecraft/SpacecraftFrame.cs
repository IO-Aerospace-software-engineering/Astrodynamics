// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body.Spacecraft;

public class SpacecraftFrame : Frame
{
    private readonly IDataProvider _dataProvider;
    public Spacecraft Spacecraft { get; }

    public SpacecraftFrame(Spacecraft spacecraft) : base(spacecraft.Name + "_FRAME", spacecraft.NaifId * 1000)
    {
        Spacecraft = spacecraft;
        _dataProvider = Configuration.Instance.DataProvider;
    }

    public override StateOrientation GetStateOrientationToICRF(Time date)
    {
        return _stateOrientationsToICRF.GetOrAdd(date, _ =>
        {
            if (_stateOrientationsToICRF.Count == 0)
            {
                return new StateOrientation(Quaternion.Zero, Vector3.Zero, date, ICRF);
            }

            var latestKnown = _stateOrientationsToICRF.OrderBy(x => x.Key).LastOrDefault(x => x.Key < date);
            return latestKnown.Value is null ? _stateOrientationsToICRF.OrderBy(x => x.Key).First().Value : latestKnown.Value.AtDate(date);
        });
    }

    public async Task WriteAsync(FileInfo outputFile)
    {
        await using var stream = this.GetType().Assembly.GetManifestResourceStream("IO.Astrodynamics.Templates.FrameTemplate.tf");
        using StreamReader sr = new StreamReader(stream ?? throw new InvalidOperationException());
        var templateData = await sr.ReadToEndAsync();
        var data = templateData.Replace("{framename}", Name.ToUpper()).Replace("{frameid}", Id.ToString()).Replace("{spacecraftid}", Spacecraft.NaifId.ToString())
            .Replace("{spacecraftname}", Spacecraft.Name.ToUpper());
        await using var sw = new StreamWriter(outputFile.FullName);
        await sw.WriteAsync(data);
    }

    public void WriteOrientation(FileInfo outputFile, Spacecraft spacecraft)
    {
        API.Instance.WriteOrientation(outputFile, spacecraft, _stateOrientationsToICRF.Values.ToArray());
    }
}