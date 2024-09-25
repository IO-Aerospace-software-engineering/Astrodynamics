// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body.Spacecraft;

public class EllipticalInstrument : Instrument
{
    public double CrossAngle { get; }

    internal EllipticalInstrument(Spacecraft spacecraft, int naifId, string name, string model, double fieldOfView, double crossAngle, Vector3 boresight, Vector3 refVector,
        Vector3 orientation) : base(spacecraft, naifId, name, model, fieldOfView, InstrumentShape.Elliptical, boresight, refVector, orientation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(crossAngle);
        CrossAngle = crossAngle;
    }

    public override async Task WriteKernelAsync(FileInfo outputFile)
    {
        await using var stream = this.GetType().Assembly.GetManifestResourceStream("IO.Astrodynamics.Templates.InstrumentKernelEllipticalTemplate.ti");
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
            .Replace("{angle}", FieldOfView.ToString(CultureInfo.InvariantCulture))
            .Replace("{cangle}", CrossAngle.ToString(CultureInfo.InvariantCulture));
        await using var sw = new StreamWriter(outputFile.FullName);
        await sw.WriteAsync(data);
    }

    public override bool IsInFOV(Time date, ILocalizable target, Aberration aberration)
    {
        var cameraPostion = Spacecraft.GetEphemeris(date, new Barycenter(0), Frame.ICRF, aberration).ToStateVector();
        var objectPosition = target.GetEphemeris(date, new Barycenter(0), Frame.ICRF, aberration).ToStateVector();
        // Calculate the vector from the camera to the object
        Vector3 toObject = objectPosition.Position - cameraPostion.Position;

        // Project the vector onto the camera's view direction
        double z = toObject * GetBoresightInICRFFrame(date);

        // Check if the object is in front of the camera
        if (z <= 0)
            return false;

        // Calculate horizontal and vertical angles
        Vector3 projectedOntoXY = new Vector3(toObject.X, toObject.Y, 0);
        double azimuth = System.Math.Atan2(projectedOntoXY.Magnitude(), z); // Horizontal angle

        Vector3 projectedOntoYZ = new Vector3(0, toObject.Y, toObject.Z);
        double elevation = System.Math.Atan2(projectedOntoYZ.Magnitude(), z); // Vertical angle

        // Check if the object is within the camera's horizontal and vertical FOV
        if (System.Math.Pow(azimuth / (FieldOfView), 2) + System.Math.Pow(elevation / (CrossAngle), 2) <= 1)
        {
            return true;
        }

        return false;
    }
}