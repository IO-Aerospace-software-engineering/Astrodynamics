// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator.MonteCarlo;

/// <summary>
/// Statistical summary of all Monte Carlo runs at a single propagation epoch.
/// </summary>
public sealed class EpochStatistics
{
    /// <summary>The propagation epoch these statistics correspond to.</summary>
    public Time Epoch { get; }

    /// <summary>Mean position across all successful runs (m, ICRF).</summary>
    public Vector3 MeanPosition { get; }

    /// <summary>Mean velocity across all successful runs (m/s, ICRF).</summary>
    public Vector3 MeanVelocity { get; }

    /// <summary>6×6 Bessel-corrected sample covariance matrix [X, Y, Z, VX, VY, VZ] (m, m/s).</summary>
    public Matrix SampleCovariance { get; }

    /// <summary>Per-component standard deviation for position (m) and velocity (m/s).</summary>
    public StateComponents StandardDeviation { get; }

    /// <summary>Per-component minimum values for position (m) and velocity (m/s) across all runs.</summary>
    public StateComponents Min { get; }

    /// <summary>Per-component maximum values for position (m) and velocity (m/s) across all runs.</summary>
    public StateComponents Max { get; }

    /// <summary>RSS position dispersion percentiles (m).</summary>
    public RssPercentiles RssPositionPercentiles { get; }

    /// <summary>RSS velocity dispersion percentiles (m/s).</summary>
    public RssPercentiles RssVelocityPercentiles { get; }

    internal EpochStatistics(Time epoch, Vector3 meanPosition, Vector3 meanVelocity,
        Matrix sampleCovariance, StateComponents standardDeviation,
        StateComponents min, StateComponents max,
        RssPercentiles rssPositionPercentiles, RssPercentiles rssVelocityPercentiles)
    {
        Epoch = epoch;
        MeanPosition = meanPosition;
        MeanVelocity = meanVelocity;
        SampleCovariance = sampleCovariance;
        StandardDeviation = standardDeviation;
        Min = min;
        Max = max;
        RssPositionPercentiles = rssPositionPercentiles;
        RssVelocityPercentiles = rssVelocityPercentiles;
    }
}
