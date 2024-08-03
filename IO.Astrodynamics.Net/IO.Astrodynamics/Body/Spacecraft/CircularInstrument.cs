// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Body.Spacecraft;

public class CircularInstrument : Instrument
{
    internal CircularInstrument(Spacecraft spacecraft, int naifId, string name, string model, double fieldOfView, Vector3 boresight, Vector3 refVector, Vector3 orientation) : base(spacecraft, naifId, name, model, fieldOfView, InstrumentShape.Circular, boresight, refVector, orientation)
    {
    }

    public override async Task WriteKernelAsync(FileInfo outputFile)
    {
        await using var stream = this.GetType().Assembly.GetManifestResourceStream("IO.Astrodynamics.Templates.InstrumentKernelCircularTemplate.ti");
        using StreamReader sr = new StreamReader(stream ?? throw new InvalidOperationException());
        var templateData = await sr.ReadToEndAsync();
        var data = templateData
            .Replace("{instrumentid}", NaifId.ToString())
            .Replace("{framename}", Spacecraft.Name.ToUpper() + "_" + Name.ToUpper())
            .Replace("{spacecraftid}", Spacecraft.NaifId.ToString())
            .Replace("{bx}", Boresight.X.ToString(CultureInfo.InvariantCulture))
            .Replace("{by}", Boresight.Y.ToString(CultureInfo.InvariantCulture))
            .Replace("{bz}", Boresight.Z.ToString(CultureInfo.InvariantCulture))
            .Replace("{rx}", RefVector.X.ToString(CultureInfo.InvariantCulture))
            .Replace("{ry}", RefVector.Y.ToString(CultureInfo.InvariantCulture))
            .Replace("{rz}", RefVector.Z.ToString(CultureInfo.InvariantCulture))
            .Replace("{angle}", FieldOfView.ToString(CultureInfo.InvariantCulture));
        await using var sw = new StreamWriter(outputFile.FullName);
        await sw.WriteAsync(data);
    }
}