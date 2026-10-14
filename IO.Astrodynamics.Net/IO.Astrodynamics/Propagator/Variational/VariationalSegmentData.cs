// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// An impulsive maneuver at the start of a segment: its epoch, its ΔV and the covariance of its execution error, in the
/// frame of the propagation.
/// </summary>
/// <param name="Epoch">Epoch of the maneuver.</param>
/// <param name="DeltaV">The ΔV, in m/s.</param>
/// <param name="ExecutionCovariance">The covariance of the execution error of the ΔV, 3×3 row-major, in m²/s²
/// (<see cref="IO.Astrodynamics.Maneuver.ManeuverExecutionError"/>); null when the maneuver has none.</param>
internal readonly record struct ManeuverRecord(Time Epoch, Vector3 DeltaV, double[] ExecutionCovariance = null);

/// <summary>
/// The variational data of one propagation segment: Y = [Φ | Ψ] and Q at the end of each accepted step, relative to the
/// segment start, and the cumulative values at the segment start.
/// </summary>
/// <remarks>
/// <para>
/// The values relative to the segment start begin at Y = [I | 0] and Q = 0, so the values at the start of step k are
/// those at the end of step k − 1, or the identity for k = 0. The cumulative values, from the start of the propagation,
/// are Φ = Φ_seg Φ_entry, Ψ = Φ_seg Ψ_entry + Ψ_seg and Q = Φ_seg Q_entry Φ_segᵀ + Q_seg
/// (<see cref="VariationalEquations.Compose"/>).
/// </para>
/// <para>
/// Q is stored at each step only with process noise. The cumulative Q at the segment start is carried whenever the
/// propagation carries a covariance (<see cref="VariationalEquations.CarriedCovarianceLength"/>), with or without noise: it
/// holds the execution error of the maneuvers before the segment, and without noise Q_seg = 0.
/// </para>
/// <para>
/// The values are stored in flat arrays that grow by doubling, like the list of accepted steps of the segment.
/// </para>
/// </remarks>
internal sealed class VariationalSegmentData
{
    private double[] _y;
    private double[] _q;

    /// <summary>
    /// Create the data of a segment.
    /// </summary>
    /// <param name="yLength">Number of elements of Y.</param>
    /// <param name="hasCovariance">Whether Q is stored at each step: with process noise.</param>
    /// <param name="estimatedStepCount">Initial capacity, in steps.</param>
    /// <param name="entryY">Cumulative Y at the segment start.</param>
    /// <param name="entryQ">Cumulative Q at the segment start: 21 values when Q is stored or carried, empty when there
    /// is none.</param>
    /// <param name="entryManeuver">The maneuver that starts the segment, if any.</param>
    internal VariationalSegmentData(int yLength, bool hasCovariance, int estimatedStepCount,
        ReadOnlySpan<double> entryY, ReadOnlySpan<double> entryQ, ManeuverRecord? entryManeuver)
    {
        if (entryY.Length != yLength)
        {
            throw new ArgumentException($"entryY must hold {yLength} values; it holds {entryY.Length}.",
                nameof(entryY));
        }

        int covarianceLength = hasCovariance ? VariationalEquations.CovarianceLength : 0;
        if ((hasCovariance || entryQ.Length != 0) && entryQ.Length != VariationalEquations.CovarianceLength)
        {
            throw new ArgumentException(
                $"entryQ must hold {VariationalEquations.CovarianceLength} values, or none without Q; it holds " +
                $"{entryQ.Length}.", nameof(entryQ));
        }

        YLength = yLength;
        CovarianceLength = covarianceLength;
        EntryY = entryY.ToArray();
        EntryQ = entryQ.ToArray();
        EntryManeuver = entryManeuver;
        int capacity = System.Math.Max(estimatedStepCount, 1);
        _y = new double[capacity * yLength];
        _q = new double[capacity * covarianceLength];
    }

    /// <summary>Number of elements of Y.</summary>
    internal int YLength { get; }

    /// <summary>Number of elements of Q stored at each step: 21 with process noise, 0 without.</summary>
    internal int CovarianceLength { get; }

    /// <summary>Cumulative Y, from the start of the propagation, at the segment start.</summary>
    internal double[] EntryY { get; }

    /// <summary>Cumulative Q at the segment start, the execution error of the maneuver that starts it included; empty
    /// when the propagation carries no Q.</summary>
    internal double[] EntryQ { get; }

    /// <summary>The maneuver that starts the segment, null for the first segment.</summary>
    internal ManeuverRecord? EntryManeuver { get; }

    /// <summary>Number of accepted steps stored.</summary>
    internal int StepCount { get; private set; }

    /// <summary>
    /// Append Y and Q at the end of the next accepted step, relative to the segment start.
    /// </summary>
    internal void Append(ReadOnlySpan<double> y, ReadOnlySpan<double> q)
    {
        if ((StepCount + 1) * YLength > _y.Length)
        {
            Array.Resize(ref _y, 2 * _y.Length);
            Array.Resize(ref _q, 2 * _q.Length);
        }

        y.CopyTo(_y.AsSpan(StepCount * YLength, YLength));
        q.Slice(0, CovarianceLength).CopyTo(_q.AsSpan(StepCount * CovarianceLength, CovarianceLength));
        StepCount++;
    }

    /// <summary>Y at the end of accepted step <paramref name="step"/>, relative to the segment start.</summary>
    internal ReadOnlySpan<double> StepEndY(int step)
    {
        ValidateStep(step);
        return _y.AsSpan(step * YLength, YLength);
    }

    /// <summary>Q at the end of accepted step <paramref name="step"/>, relative to the segment start.</summary>
    internal ReadOnlySpan<double> StepEndQ(int step)
    {
        ValidateStep(step);
        return _q.AsSpan(step * CovarianceLength, CovarianceLength);
    }

    private void ValidateStep(int step)
    {
        if (step < 0 || step >= StepCount)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, $"The segment holds {StepCount} steps.");
        }
    }
}
