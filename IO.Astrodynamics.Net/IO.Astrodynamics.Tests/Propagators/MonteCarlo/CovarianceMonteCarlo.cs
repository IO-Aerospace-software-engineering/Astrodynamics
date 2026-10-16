// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.MonteCarlo;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Tests.Propagators.Variational;
using IO.Astrodynamics.Tests.Statistics;
using MathNet.Numerics.LinearAlgebra;

namespace IO.Astrodynamics.Tests.Propagators.MonteCarlo;

/// <summary>
/// F3: a Monte Carlo campaign of <see cref="MonteCarloPropagator"/> on a reference case, read at any epoch, against the
/// covariance propagated by the state transition matrix, and the two statistical tests of the specification.
/// </summary>
/// <remarks>
/// <para>
/// P0 is σ = 100 m and 0.1 m/s on each RTN axis, uncorrelated, turned to the inertial frame by
/// <c>StateVector.RotateCovarianceFromRtn</c>. The runs are those of <see cref="MonteCarloPropagator.Propagate"/>
/// (<see cref="MonteCarloPropagator.PropagateRuns"/>, the same initial states for the same seed), propagated in batches so
/// that only their states at the epochs are kept. P comes from the public path,
/// <see cref="CovariancePropagationOptions"/> in covariance mode and <see cref="PropagationSolution.GetCovariance"/>,
/// with the same integrator, and the nominal state from the same propagation.
/// </para>
/// <para>
/// Element-wise test: under linearity the deviations are Gaussian of covariance P, so (N − 1) S follows the Wishart
/// distribution W(N − 1, P), and the sample covariance S about the sample mean (Bessel's correction) has
/// <c>Var(S_ij) = (P_ij² + P_ii P_jj) / (N − 1)</c>: T. W. Anderson, <i>An Introduction to Multivariate Statistical
/// Analysis</i>, 3rd edition, Wiley, 2003, section 7.3 (to be verified by S. Guillet). Each of the 21 distinct elements is
/// held within 4 standard deviations.
/// </para>
/// <para>
/// Kolmogorov-Smirnov test (<see cref="KolmogorovSmirnov"/>) of the squared Mahalanobis distances
/// <c>d² = δxᵀ P⁻¹ δx</c> against χ²(6), at α = 0.01. δx is the deviation from the nominal propagated state, not from
/// the sample mean, so a shift of the mean (the curvature of the orbit) fails the test. P⁻¹ through the Cholesky factor
/// of the correlation matrix, free of the units of the blocks.
/// </para>
/// </remarks>
internal static class CovarianceMonteCarlo
{
    private const int Batch = 250;

    /// <summary>σ of P0 in position (m) and velocity (m/s), on each RTN axis.</summary>
    internal const double PositionSigma = 100.0;

    internal const double VelocitySigma = 0.1;

    /// <summary>Tolerance of RK7(8) for the runs and for P (decision of S. Guillet, 2026-10-16).</summary>
    internal const double Tolerance = 1e-11;

    /// <summary>The statistics of a campaign at one epoch.</summary>
    /// <remarks>
    /// <c>KsPValueAboutTheMean</c> is the same test with the deviations from the sample mean: it separates a shift of the
    /// mean from a change of shape. <c>KsPValueOfTheSample</c> takes the sample mean and the sample covariance S instead of
    /// P: it fails when the distribution itself is not Gaussian, whatever P.
    /// </remarks>
    internal sealed record EpochResult(TimeSystem.Time Epoch, double WorstZ, int WorstRow, int WorstColumn,
        double KsStatistic, double KsPValue, double KsPValueAboutTheMean, double KsPValueOfTheSample,
        double MeanRadialOffset, double CurvatureOffset,
        double[] Covariance, double[] SampleCovariance)
    {
        /// <summary>Both tests pass: every |z| below 4 and the KS significance at least 0.01.</summary>
        internal bool Passes => WorstZ < 4.0 && KsPValue >= 0.01;
    }

    /// <summary>
    /// Runs the campaign of <paramref name="runCount"/> runs on <paramref name="referenceCase"/> over
    /// <paramref name="window"/>, with the coefficients sampled when their σ are given (consider parameters of P), and
    /// returns the statistics at each of <paramref name="epochs"/>.
    /// </summary>
    internal static EpochResult[] Run(ReferenceCase referenceCase, int naifId, TimeSystem.Window window,
        IReadOnlyList<TimeSystem.Time> epochs, int runCount, int seed, double? dragCoefficientSigma = null,
        double? reflectivityCoefficientSigma = null)
    {
        // P0 in RTN, turned to the inertial frame; the template and the configuration of the public campaign
        var start = WithInitialCovariance(referenceCase.InitialState);
        var template = ReferenceCase.NewSpacecraft(naifId, start);
        var config = Configuration(referenceCase, template, window, runCount, seed);

        // P and the nominal state by the public path
        var consider = dragCoefficientSigma.HasValue || reflectivityCoefficientSigma.HasValue
            ? new ConsiderParameters(dragCoefficientSigma, reflectivityCoefficientSigma)
            : null;
        var nominal = ReferenceCase.NewSpacecraft(naifId - 1, start).Propagate(window, referenceCase.NewBodies(),
            new RK78Integrator(Tolerance, Tolerance), referenceCase.AtmosphericDrag,
            referenceCase.SolarRadiationPressure, config.DeltaT,
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance, ConsiderParameters = consider });

        // The runs, in batches, keeping their states at the epochs
        var states = SampledStates(config, start, dragCoefficientSigma, reflectivityCoefficientSigma, epochs);

        return epochs.Select((t, k) => Statistics(t, states[k], nominal)).ToArray();
    }

    /// <summary>The configuration of the public campaign on <paramref name="referenceCase"/>.</summary>
    internal static MonteCarloConfiguration Configuration(ReferenceCase referenceCase,
        IO.Astrodynamics.Body.Spacecraft.Spacecraft template, TimeSystem.Window window, int runCount, int seed)
    {
        return new MonteCarloConfiguration
        {
            RunCount = runCount,
            Seed = seed,
            TemplateSpacecraft = template,
            Window = window,
            CelestialBodiesFactory = referenceCase.NewBodies,
            IncludeAtmosphericDrag = referenceCase.AtmosphericDrag,
            IncludeSolarRadiationPressure = referenceCase.SolarRadiationPressure,
            DeltaT = TimeSpan.FromHours(6.0),
            IntegratorFactory = () => new RK78Integrator(Tolerance, Tolerance)
        };
    }

    /// <summary><paramref name="state"/> with P0: σ = 100 m and 0.1 m/s on each RTN axis.</summary>
    internal static StateVector WithInitialCovariance(StateVector state)
    {
        var rtn = new IO.Astrodynamics.Math.Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            double sigma = i < 3 ? PositionSigma : VelocitySigma;
            rtn.Set(i, i, sigma * sigma);
        }

        return new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame,
            state.RotateCovarianceFromRtn(rtn));
    }

    /// <summary>The sample covariance with Bessel's correction, row-major, of the states (6 components each).</summary>
    internal static double[] SampleCovariance(IReadOnlyList<double[]> states)
    {
        int n = states.Count;
        var mean = new double[6];
        foreach (var x in states)
        {
            for (int i = 0; i < 6; i++)
            {
                mean[i] += x[i];
            }
        }

        for (int i = 0; i < 6; i++)
        {
            mean[i] /= n;
        }

        var covariance = new double[36];
        foreach (var x in states)
        {
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    covariance[6 * i + j] += (x[i] - mean[i]) * (x[j] - mean[j]);
                }
            }
        }

        for (int i = 0; i < 36; i++)
        {
            covariance[i] /= n - 1;
        }

        return covariance;
    }

    private static double[][][] SampledStates(MonteCarloConfiguration config, StateVector start,
        double? dragCoefficientSigma, double? reflectivityCoefficientSigma, IReadOnlyList<TimeSystem.Time> epochs)
    {
        StateVector[] initialStates;
        (double DragCoefficient, double ReflectivityCoefficient)[] coefficients = null;
        if (dragCoefficientSigma.HasValue || reflectivityCoefficientSigma.HasValue)
        {
            (initialStates, coefficients) = StateSampler.SampleWithCoefficients(start, ReferenceCases.DragCoefficient,
                dragCoefficientSigma ?? 0.0, ReferenceCases.ReflectivityCoefficient, reflectivityCoefficientSigma ?? 0.0,
                config.RunCount, config.Seed);
        }
        else
        {
            initialStates = StateSampler.Sample(start, config.RunCount, config.Seed);
        }

        var states = epochs.Select(_ => new double[config.RunCount][]).ToArray();
        for (int first = 0; first < config.RunCount; first += Batch)
        {
            int count = System.Math.Min(Batch, config.RunCount - first);
            var batch = MonteCarloPropagator.PropagateRuns(config, start, initialStates[first..(first + count)], first,
                coefficients?[first..(first + count)]);
            for (int r = 0; r < count; r++)
            {
                var result = batch.Results[r];
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Run {first + r} failed", result.Error);
                }

                for (int k = 0; k < epochs.Count; k++)
                {
                    states[k][first + r] = WholePropagationDifferences.State(result.Solution, epochs[k]);
                }
            }
        }

        return states;
    }

    // Squared Mahalanobis distances from center by the covariance, through the Cholesky factor of its correlation matrix
    private static double[] Distances(double[][] samples, double[] center, double[] covariance)
    {
        var scale = Enumerable.Range(0, 6).Select(i => 1.0 / System.Math.Sqrt(covariance[7 * i])).ToArray();
        var correlation = Matrix<double>.Build.Dense(6, 6, (i, j) => scale[i] * covariance[6 * i + j] * scale[j]);
        var cholesky = correlation.Cholesky();
        return samples.Select(x =>
        {
            var d = Vector<double>.Build.Dense(6, i => scale[i] * (x[i] - center[i]));
            return d.DotProduct(cholesky.Solve(d));
        }).ToArray();
    }

    private static EpochResult Statistics(TimeSystem.Time epoch, double[][] samples, PropagationSolution nominal)
    {
        int n = samples.Length;
        var p = nominal.GetCovariance(epoch);
        var covariance = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                covariance[6 * i + j] = p.Get(i, j);
            }
        }

        // Element-wise, within 4 standard deviations of the Wishart estimator
        var s = SampleCovariance(samples);
        double worst = 0.0;
        int worstRow = 0, worstColumn = 0;
        for (int i = 0; i < 6; i++)
        {
            for (int j = i; j < 6; j++)
            {
                double pij = covariance[6 * i + j];
                double sigma = System.Math.Sqrt((pij * pij + covariance[7 * i] * covariance[7 * j]) / (n - 1));
                double z = System.Math.Abs(s[6 * i + j] - pij) / sigma;
                if (z > worst)
                {
                    (worst, worstRow, worstColumn) = (z, i, j);
                }
            }
        }

        // Squared Mahalanobis distances from the nominal state, through the correlation matrix
        var x0 = WholePropagationDifferences.State(nominal, epoch);
        double statistic = KolmogorovSmirnov.Statistic(Distances(samples, x0, covariance),
            KolmogorovSmirnov.ChiSquared6Cdf);
        var mean = Enumerable.Range(0, 6).Select(i => samples.Average(x => x[i])).ToArray();
        double aboutTheMean = KolmogorovSmirnov.PValue(
            KolmogorovSmirnov.Statistic(Distances(samples, mean, covariance), KolmogorovSmirnov.ChiSquared6Cdf), n);
        double ofTheSample = KolmogorovSmirnov.PValue(
            KolmogorovSmirnov.Statistic(Distances(samples, mean, s), KolmogorovSmirnov.ChiSquared6Cdf), n);

        // The shape of the departure: the mean radial offset of the samples in the RTN axes of the nominal state, and
        // the offset the curvature of the orbit gives to an along-track spread, −E[δT²] / (2 r)
        var state = nominal.GetStateVector(epoch);
        double radius = state.Position.Magnitude();
        double radial = 0.0, alongTrack = 0.0;
        foreach (var x in samples)
        {
            var delta = state.ToRtn(new IO.Astrodynamics.Math.Vector3(x[0] - x0[0], x[1] - x0[1], x[2] - x0[2]));
            radial += delta.X;
            alongTrack += delta.Y * delta.Y;
        }

        return new EpochResult(epoch, worst, worstRow, worstColumn, statistic, KolmogorovSmirnov.PValue(statistic, n),
            aboutTheMean, ofTheSample, radial / n, -alongTrack / n / (2.0 * radius), covariance, s);
    }
}
