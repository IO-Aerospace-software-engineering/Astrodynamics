// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using System.Collections.Generic;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Propagator.MonteCarlo;

internal static class StatisticsAggregator
{
    public static IReadOnlyList<EpochStatistics> Aggregate(IReadOnlyList<PropagationSolution> solutions)
    {
        if (solutions == null || solutions.Count < 2)
            throw new ArgumentException("At least 2 solutions are required for statistics.", nameof(solutions));

        int epochCount = solutions[0].StateVectors.Count;
        for (int i = 1; i < solutions.Count; i++)
        {
            if (solutions[i].StateVectors.Count != epochCount)
                throw new ArgumentException(
                    $"All solutions must have the same number of state vectors. " +
                    $"Solution 0 has {epochCount}, solution {i} has {solutions[i].StateVectors.Count}.");
        }

        int n = solutions.Count;
        var results = new EpochStatistics[epochCount];

        for (int k = 0; k < epochCount; k++)
        {
            var epoch = solutions[0].StateVectors[k].Epoch;

            // Compute means
            double mx = 0, my = 0, mz = 0, mvx = 0, mvy = 0, mvz = 0;
            for (int i = 0; i < n; i++)
            {
                var sv = solutions[i].StateVectors[k];
                mx += sv.Position.X;
                my += sv.Position.Y;
                mz += sv.Position.Z;
                mvx += sv.Velocity.X;
                mvy += sv.Velocity.Y;
                mvz += sv.Velocity.Z;
            }

            mx /= n; my /= n; mz /= n;
            mvx /= n; mvy /= n; mvz /= n;

            // Compute sample covariance (Bessel-corrected), min, max
            var cov = new Matrix(6, 6);
            var min = new double[] { double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue };
            var max = new double[] { double.MinValue, double.MinValue, double.MinValue, double.MinValue, double.MinValue, double.MinValue };
            var rssPos = new double[n];
            var rssVel = new double[n];
            double[] means = { mx, my, mz, mvx, mvy, mvz };

            for (int i = 0; i < n; i++)
            {
                var sv = solutions[i].StateVectors[k];
                double[] vals = { sv.Position.X, sv.Position.Y, sv.Position.Z, sv.Velocity.X, sv.Velocity.Y, sv.Velocity.Z };
                double[] diff = new double[6];

                for (int j = 0; j < 6; j++)
                {
                    diff[j] = vals[j] - means[j];
                    if (vals[j] < min[j]) min[j] = vals[j];
                    if (vals[j] > max[j]) max[j] = vals[j];
                }

                for (int j = 0; j < 6; j++)
                    for (int l = 0; l < 6; l++)
                        cov.Set(j, l, cov.Get(j, l) + diff[j] * diff[l]);

                rssPos[i] = System.Math.Sqrt(diff[0] * diff[0] + diff[1] * diff[1] + diff[2] * diff[2]);
                rssVel[i] = System.Math.Sqrt(diff[3] * diff[3] + diff[4] * diff[4] + diff[5] * diff[5]);
            }

            // Bessel correction
            for (int j = 0; j < 6; j++)
                for (int l = 0; l < 6; l++)
                    cov.Set(j, l, cov.Get(j, l) / (n - 1));

            // Standard deviation
            var stdDev = new StateComponents(
                new Vector3(System.Math.Sqrt(cov.Get(0, 0)), System.Math.Sqrt(cov.Get(1, 1)), System.Math.Sqrt(cov.Get(2, 2))),
                new Vector3(System.Math.Sqrt(cov.Get(3, 3)), System.Math.Sqrt(cov.Get(4, 4)), System.Math.Sqrt(cov.Get(5, 5))));

            var minComponents = new StateComponents(
                new Vector3(min[0], min[1], min[2]),
                new Vector3(min[3], min[4], min[5]));

            var maxComponents = new StateComponents(
                new Vector3(max[0], max[1], max[2]),
                new Vector3(max[3], max[4], max[5]));

            // RSS percentiles (50th, 95th, 99th) using nearest-rank
            Array.Sort(rssPos);
            Array.Sort(rssVel);
            var posPercentiles = new RssPercentiles(
                NearestRankPercentile(rssPos, 0.50),
                NearestRankPercentile(rssPos, 0.95),
                NearestRankPercentile(rssPos, 0.99));
            var velPercentiles = new RssPercentiles(
                NearestRankPercentile(rssVel, 0.50),
                NearestRankPercentile(rssVel, 0.95),
                NearestRankPercentile(rssVel, 0.99));

            results[k] = new EpochStatistics(epoch,
                new Vector3(mx, my, mz), new Vector3(mvx, mvy, mvz),
                cov, stdDev, minComponents, maxComponents, posPercentiles, velPercentiles);
        }

        return results;
    }

    private static double NearestRankPercentile(double[] sorted, double p)
    {
        int index = (int)System.Math.Ceiling(p * sorted.Length) - 1;
        if (index < 0) index = 0;
        if (index >= sorted.Length) index = sorted.Length - 1;
        return sorted[index];
    }
}
