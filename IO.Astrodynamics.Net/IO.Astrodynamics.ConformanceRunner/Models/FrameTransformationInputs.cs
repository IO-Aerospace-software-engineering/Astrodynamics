using System.Collections.Generic;

namespace IO.Astrodynamics.ConformanceRunner.Models;

/// <summary>
/// The inputs of a <c>frame_transformation</c> case (spec.md section 11.3).
/// </summary>
public class FrameTransformationInputs
{
    /// <summary>Tagged epoch (spec.md section 3).</summary>
    public string Epoch { get; set; }

    /// <summary>The frame of the state, a name of spec.md section 2.1.</summary>
    public string FromFrame { get; set; }

    /// <summary>The frame of the outputs, a name of spec.md section 2.1.</summary>
    public string ToFrame { get; set; }

    /// <summary>The centre of the state: <c>Earth</c> or <c>Moon</c>.</summary>
    public string FromCenter { get; set; }

    /// <summary>The centre of the outputs: <c>Earth</c> or <c>Moon</c>.</summary>
    public string ToCenter { get; set; }

    public double[] PositionKm { get; set; }

    public double[] VelocityKmS { get; set; }

    /// <summary>The files of the suite's <c>data/</c> the golden depends on, possibly none.</summary>
    public List<string> Kernels { get; set; }

    /// <summary>The ellipsoid of the geodetic outputs, or null when the case has none.</summary>
    public EllipsoidInputs Ellipsoid { get; set; }
}

/// <summary>
/// An ellipsoid of revolution: equatorial radius and flattening.
/// </summary>
public class EllipsoidInputs
{
    public double EquatorialRadiusKm { get; set; }
    public double Flattening { get; set; }
}
