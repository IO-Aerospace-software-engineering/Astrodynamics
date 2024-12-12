using System;

namespace IO.Astrodynamics.OrbitalParameters;

public record OrbitDetermination
{
    public OrbitalParameters OrbitalParameters { get; }
    public double Reliability { get; }
    public double GeometricReliability { get; }
    public double TemporalReliability { get; }
    public double CenterOfMotionQualityReliability { get; }

    public OrbitDetermination(OrbitalParameters orbitalParameters, double reliability, double geometricReliability, double temporalReliability, double centerOfMotionReliability)
    {
        if (orbitalParameters == null) throw new ArgumentNullException(nameof(orbitalParameters));
        ArgumentOutOfRangeException.ThrowIfNegative(reliability);
        if (reliability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(reliability), "Reliability must be between 0 and 1.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(centerOfMotionReliability);
        if (centerOfMotionReliability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(centerOfMotionReliability), "Center of motion quality Reliability must be between 0 and 1.");
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
        CenterOfMotionQualityReliability = centerOfMotionReliability;
    }

    public override string ToString()
    {
        return $"Reliability : {Reliability * 100}%\n" +
               $"Geometric reliability : {GeometricReliability * 100}%\n" +
               $"Temporal reliability : {TemporalReliability * 100}%\n" +
               $"Center of motion reliability : {CenterOfMotionQualityReliability * 100}%";
    }
}