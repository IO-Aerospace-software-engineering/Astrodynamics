using System;

namespace IO.Astrodynamics.OrbitalParameters;

public record OrbitDetermination
{
    public OrbitalParameters OrbitalParameters { get; }
    public double Reliability { get; }
    public double GeometricReliability { get; }
    public double TemporalReliability { get; }
    public double HeliocentricQualityReliability { get; }

    public OrbitDetermination(OrbitalParameters orbitalParameters, double reliability, double geometricReliability, double temporalReliability, double heliocentricReliability)
    {
        if (orbitalParameters == null) throw new ArgumentNullException(nameof(orbitalParameters));
        ArgumentOutOfRangeException.ThrowIfNegative(reliability);
        if (reliability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(reliability), "Reliability must be between 0 and 1.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(heliocentricReliability);
        if (heliocentricReliability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(heliocentricReliability), "Heliocentric Quality Reliability must be between 0 and 1.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(temporalReliability);
        if (temporalReliability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(temporalReliability), "Temporal Reliability must be between 0 and 1.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(geometricReliability);
        if (geometricReliability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(geometricReliability), "Geometric Reliability must be between 0 and 1.");
        }

        OrbitalParameters = orbitalParameters;
        Reliability = reliability;
        GeometricReliability = geometricReliability;
        TemporalReliability = temporalReliability;
        HeliocentricQualityReliability = heliocentricReliability;
    }

    override public string ToString()
    {
        return $"Reliability : {Reliability * 100}%\n" +
               $"Geometric Reliability : {GeometricReliability * 100}%\n" +
               $"Temporal Reliability : {TemporalReliability * 100}%\n" +
               $"Heliocentric Quality Reliability : {HeliocentricQualityReliability * 100}%";
    }
}