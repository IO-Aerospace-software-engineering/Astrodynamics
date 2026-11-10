using System.Collections.Generic;

namespace IO.Astrodynamics.ConformanceRunner.Models;

/// <summary>
/// An object of a <c>conjunction</c> case (spec.md section 16.1): propagated from its state at the epoch, or read from
/// the SPKs of the case.
/// </summary>
public class ConjunctionObject
{
    public string Name { get; set; }

    /// <summary>The NAIF id of an object read from the SPKs of the case; null for a propagated object.</summary>
    public int? NaifId { get; set; }

    /// <summary>The position at the epoch in ICRF, km; null for a kernel object.</summary>
    public double[] PositionKm { get; set; }

    /// <summary>The velocity at the epoch in ICRF, km/s; null for a kernel object.</summary>
    public double[] VelocityKmS { get; set; }

    /// <summary>The spacecraft of the object as a propagator case gives it (spec.md section 12.1), or null.</summary>
    public Dictionary<object, object> Spacecraft { get; set; }

    /// <summary>The 21 numbers of the initial 6x6 covariance in the object's RTN at the epoch (km, km/s; the order of
    /// CCSDS 508.0), or null.</summary>
    public double[] CovarianceRtn { get; set; }

    /// <summary>Whether the object is read from the SPKs of the case.</summary>
    public bool FromKernel => NaifId.HasValue;
}

/// <summary>The inputs of a <c>conjunction</c> case (spec.md section 16.1).</summary>
public class ConjunctionInputs
{
    /// <summary>The TCA of least miss in the window.</summary>
    public const string Closest = "closest";

    /// <summary>Every TCA in the window, in time order.</summary>
    public const string AllMinima = "all_minima";

    public string Epoch { get; set; }

    /// <summary>Object 1, then object 2.</summary>
    public List<ConjunctionObject> Objects { get; set; }

    public SearchWindow AnalysisWindow { get; set; }

    public string Report { get; set; }

    /// <summary>The combined hard-body radius, km, where the case reports the Pc; null otherwise.</summary>
    public double? HardBodyRadiusKm { get; set; }
}

/// <summary>An object of a <c>collision_probability</c> case (spec.md section 16.2): its state at TCA.</summary>
public class EncounterObject
{
    /// <summary>The position at TCA in ICRF, km.</summary>
    public double[] PositionKm { get; set; }

    /// <summary>The velocity at TCA in ICRF, km/s.</summary>
    public double[] VelocityKmS { get; set; }

    /// <summary>
    /// The lower triangle of the covariance in the object's RTN, in the order of CCSDS 508.0: 6 numbers (3x3 position,
    /// km², <c>collision_probability</c>) or 21 (6x6, km, km/s, <c>cdm</c> export); null when absent.
    /// </summary>
    public double[] CovarianceRtn { get; set; }
}

/// <summary>The inputs of a <c>collision_probability</c> case (spec.md section 16.2).</summary>
public class CollisionProbabilityInputs
{
    /// <summary>The TCA, informative.</summary>
    public string Tca { get; set; }

    /// <summary>The two objects; at least one has a covariance.</summary>
    public List<EncounterObject> Objects { get; set; }

    public double HardBodyRadiusKm { get; set; }
}

/// <summary>The inputs of a <c>cdm</c> case (spec.md section 16.3).</summary>
public class CdmInputs
{
    /// <summary>Decode the fields of a CDM.</summary>
    public const string Decode = "decode";

    /// <summary>Decode a CDM and turn each covariance to inertial axes.</summary>
    public const string ToInertial = "to_inertial";

    /// <summary>Write the CDM of an encounter, read it back.</summary>
    public const string Export = "export";

    public string Operation { get; set; }

    /// <summary>The CDM next to <c>inputs.yaml</c> (decode, to_inertial).</summary>
    public string CdmFile { get; set; }

    /// <summary>The encounter at an exact TCA (export).</summary>
    public string Tca { get; set; }

    /// <summary>The two objects at TCA, each with its 6x6 RTN covariance (export).</summary>
    public List<EncounterObject> Objects { get; set; }

    /// <summary>The combined hard-body radius, km (export).</summary>
    public double? HardBodyRadiusKm { get; set; }
}
