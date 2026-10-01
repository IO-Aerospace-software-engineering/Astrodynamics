// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.Propagator.MonteCarlo;

internal static class StateSampler
{
    public static StateVector[] Sample(StateVector nominal, int count, int seed)
    {
        if (nominal == null)
            throw new ArgumentNullException(nameof(nominal));
        if (!nominal.Covariance.HasValue)
            throw new InvalidOperationException("Nominal state vector must have a covariance matrix.");
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");

        var L = nominal.Covariance.Value.Cholesky();
        var rng = new Random(seed);
        var samples = new StateVector[count];

        for (int i = 0; i < count; i++)
        {
            var z = GenerateStandardNormals(rng);
            var delta = L.Multiply(z);

            var perturbedPosition = new Vector3(
                nominal.Position.X + delta[0],
                nominal.Position.Y + delta[1],
                nominal.Position.Z + delta[2]);

            var perturbedVelocity = new Vector3(
                nominal.Velocity.X + delta[3],
                nominal.Velocity.Y + delta[4],
                nominal.Velocity.Z + delta[5]);

            samples[i] = new StateVector(perturbedPosition, perturbedVelocity,
                nominal.Observer, nominal.Epoch, nominal.Frame);
        }

        return samples;
    }

    private static double[] GenerateStandardNormals(Random rng)
    {
        var normals = new double[6];
        for (int i = 0; i < 6; i += 2)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            double r = System.Math.Sqrt(-2.0 * System.Math.Log(u1));
            double theta = 2.0 * System.Math.PI * u2;
            normals[i] = r * System.Math.Cos(theta);
            normals[i + 1] = r * System.Math.Sin(theta);
        }

        return normals;
    }
}
