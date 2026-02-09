using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace IO.Astrodynamics.ConformanceRunner.Models;

public class CaseInput
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; }

    [YamlMember(Alias = "schema_version")]
    public string SchemaVersion { get; set; }

    [YamlMember(Alias = "version")]
    public string Version { get; set; }

    [YamlMember(Alias = "category")]
    public string Category { get; set; }

    [YamlMember(Alias = "created")]
    public string Created { get; set; }

    [YamlMember(Alias = "description")]
    public string Description { get; set; }

    [YamlMember(Alias = "metadata")]
    public CaseMetadata Metadata { get; set; }

    [YamlMember(Alias = "inputs")]
    public Dictionary<string, object> Inputs { get; set; }

    [YamlMember(Alias = "tolerances_override")]
    public Dictionary<string, TolerancePair> TolerancesOverride { get; set; }
}

public class CaseMetadata
{
    [YamlMember(Alias = "ephemeris_kernel")]
    public string EphemerisKernel { get; set; }

    [YamlMember(Alias = "reference_frame")]
    public string ReferenceFrame { get; set; }

    [YamlMember(Alias = "time_scale")]
    public string TimeScale { get; set; }
}

public class KeplerianOrbit
{
    public string Type { get; set; }
    public double AKm { get; set; }
    public double E { get; set; }
    public double IDeg { get; set; }
    public double RaanDeg { get; set; }
    public double ArgpDeg { get; set; }
    public double MaDeg { get; set; }
}

public class StateVectorOrbit
{
    public string Type { get; set; }
    public double[] PositionKm { get; set; }
    public double[] VelocityKmS { get; set; }
}

public class FieldOfView
{
    public double HalfAngleDeg { get; set; }
    public double[] AxisBody { get; set; }
}

public class EclipseInputs
{
    public string Epoch { get; set; }
    public object Orbit { get; set; }
    public SearchWindow SearchWindow { get; set; }
    public string OccultingBody { get; set; }
    public string LightSource { get; set; }
}

public class TriadInputs
{
    public string Epoch { get; set; }
    public object Orbit { get; set; }
    public string PrimaryTarget { get; set; }
    public string SecondaryTarget { get; set; }
    public double[] PrimaryBodyVector { get; set; }
    public double[] SecondaryBodyVector { get; set; }
    public FieldOfView FieldOfView { get; set; }
}

public class SearchWindow
{
    public string Start { get; set; }
    public string End { get; set; }
}
