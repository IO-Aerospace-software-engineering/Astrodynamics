using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.ConformanceRunner.Models;

/// <summary>Two lines of a TLE and its name (spec.md section 15.2).</summary>
public class TleInput
{
    /// <summary>The name of the object, informative.</summary>
    public string Name { get; set; }

    public string Line1 { get; set; }

    public string Line2 { get; set; }
}

/// <summary>The inputs of an <c>sgp4</c> case (spec.md section 15.1).</summary>
public class Sgp4Inputs
{
    /// <summary>The gravity model of SGP4, <c>wgs72old</c>.</summary>
    public const string Wgs72Old = "wgs72old";

    /// <summary>The mode of SGP4, <c>afspc</c>.</summary>
    public const string Afspc = "afspc";

    public TleInput Tle { get; set; }

    public string GravityModel { get; set; }

    public string Opsmode { get; set; }

    /// <summary>The samples as minutes since the TLE epoch, or null when the case gives UTC instants.</summary>
    public List<double> TsinceMin { get; set; }

    /// <summary>The samples as UTC instants (spec.md section 3), or null when the case gives tsince.</summary>
    public List<string> UtcEpochs { get; set; }

    /// <summary>The frames of the states: <c>TEME</c>, and <c>ICRF</c> with UTC instants.</summary>
    public List<string> OutputFrames { get; set; }
}

/// <summary>The inputs of a <c>tle_conversion</c> case (spec.md section 15.2).</summary>
public class TleConversionInputs
{
    /// <summary>A TLE read into its fields.</summary>
    public const string TleToOmm = "tle_to_omm";

    /// <summary>OMM mean elements written as a TLE.</summary>
    public const string OmmToTle = "omm_to_tle";

    public string Operation { get; set; }

    /// <summary>The TLEs of <c>tle_to_omm</c>, in their order.</summary>
    public List<TleInput> Tles { get; set; }

    /// <summary>The OMM items of <c>omm_to_tle</c>, in their order.</summary>
    public List<OmmInput> Omms { get; set; }
}

/// <summary>The OMM mean elements of an <c>omm_to_tle</c> item (spec.md section 15.2), in CCSDS units.</summary>
public class OmmInput
{
    public string ObjectName { get; set; }
    public string ObjectId { get; set; }

    /// <summary>The scale of <see cref="Epoch"/>: UTC, TT, TAI or TDB.</summary>
    public string TimeSystem { get; set; }

    /// <summary>The epoch in the CCSDS form, without a tag, in <see cref="TimeSystem"/>.</summary>
    public DateTime Epoch { get; set; }

    public double MeanMotion { get; set; }
    public double Eccentricity { get; set; }
    public double Inclination { get; set; }
    public double RaOfAscNode { get; set; }
    public double ArgOfPericenter { get; set; }
    public double MeanAnomaly { get; set; }
    public int EphemerisType { get; set; }
    public string ClassificationType { get; set; }
    public int NoradCatId { get; set; }
    public int ElementSetNo { get; set; }
    public int RevAtEpoch { get; set; }
    public double BStar { get; set; }
    public double MeanMotionDot { get; set; }
    public double MeanMotionDdot { get; set; }
}
