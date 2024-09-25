// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body.Spacecraft;

public class SpacecraftFrame : Frame
{
    public int SpacecraftId { get; }
    public string SpacecraftName { get; }

    public SpacecraftFrame(string name, int spacecraftId, string spacecraftName) : base(name, spacecraftId * 1000)
    {
        SpacecraftId = spacecraftId;
        SpacecraftName = spacecraftName;
    }

    public override StateOrientation GetStateOrientationToICRF(Time date)
    {
        return _stateOrientationsToICRF.GetOrAdd(date, _ =>
        {
            if (_stateOrientationsToICRF.IsEmpty)
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
        var data = templateData.Replace("{framename}", Name.ToUpper()).Replace("{frameid}", Id.ToString()).Replace("{spacecraftid}", SpacecraftId.ToString())
            .Replace("{spacecraftname}", SpacecraftName.ToUpper());
        await using var sw = new StreamWriter(outputFile.FullName);
        await sw.WriteAsync(data);
    }
}