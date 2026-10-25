namespace IO.Astrodynamics.ConformanceRunner.Models;

/// <summary>
/// The inputs of a <c>time_conversion</c> case (spec.md section 11.2).
/// </summary>
public class TimeConversionInputs
{
    /// <summary>Tagged epoch (spec.md section 3), in UTC, TAI, TT or TDB.</summary>
    public string Epoch { get; set; }
}
