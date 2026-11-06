using System.Collections.Generic;

namespace IO.Astrodynamics.ConformanceRunner.Models;

/// <summary>The inputs of a <c>ground_station</c> case (spec.md section 14.1).</summary>
public class GroundStationInputs
{
    public string Epoch { get; set; }

    /// <summary>A <see cref="StateVectorOrbit"/> (the two-body target) or a <see cref="BspSourceOrbit"/>.</summary>
    public object Orbit { get; set; }

    /// <summary>μ of a two-body target, km³/s²; null for an SPK target.</summary>
    public double? GmKm3S2 { get; set; }

    public SearchWindow SearchWindow { get; set; }

    /// <summary>The search step, s; informative in the suite.</summary>
    public double StepSizeS { get; set; }

    public LocationInput Location { get; set; }
    public double ElevationMaskDeg { get; set; }

    /// <summary>The light time of the case: <c>none</c>, <c>lt</c>, <c>lt+s</c> or <c>xlt</c>.</summary>
    public string Aberration { get; set; }

    /// <summary>The bodies that may hide the target: none, or the Moon.</summary>
    public List<string> OccultingBodies { get; set; }

    /// <summary>The UTC instants of the samples; empty for an SPK target.</summary>
    public List<string> SampleEpochs { get; set; }
}

/// <summary>An event of an <c>orbital_events</c> case (spec.md section 14.2).</summary>
public class OrbitalEventRequest
{
    public const string Periapsis = "periapsis";
    public const string Apoapsis = "apoapsis";
    public const string AscendingNode = "ascending_node";
    public const string DescendingNode = "descending_node";
    public const string Altitude = "altitude";

    public const string Up = "up";
    public const string Down = "down";
    public const string Any = "any";

    public string Type { get; set; }

    /// <summary>The geodetic altitude of an <c>altitude</c> event, km.</summary>
    public double? ValueKm { get; set; }

    /// <summary>The direction of an <c>altitude</c> event: <c>up</c>, <c>down</c> or <c>any</c>.</summary>
    public string Direction { get; set; }
}

/// <summary>A body axis of an <c>attitude</c> case and the direction it points toward (spec.md section 14.3).</summary>
public class AttitudeAxis
{
    public const string Nadir = "nadir";
    public const string Velocity = "velocity";
    public const string OrbitNormal = "orbit_normal";

    public double[] BodyVector { get; set; }
    public string Direction { get; set; }
}

/// <summary>The inputs of an <c>attitude</c> case (spec.md section 14.3).</summary>
public class AttitudeInputs
{
    public string Epoch { get; set; }
    public object Orbit { get; set; }
    public double GmKm3S2 { get; set; }
    public SearchWindow PropagationWindow { get; set; }
    public double SampleStepS { get; set; }

    /// <summary>The primary axis of a TRIAD law; null for a single axis.</summary>
    public AttitudeAxis Primary { get; set; }

    /// <summary>The secondary axis of a TRIAD law; null for a single axis.</summary>
    public AttitudeAxis Secondary { get; set; }

    /// <summary>The single pointing axis; null for a TRIAD law.</summary>
    public AttitudeAxis Pointing { get; set; }
}
