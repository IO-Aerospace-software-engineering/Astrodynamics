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
            samples[i] = Perturbed(nominal, L, GenerateStandardNormals(rng, 6));
        }

        return samples;
    }

    /// <summary>
    /// Draws <paramref name="count"/> states and drag and reflectivity coefficients jointly from the block-diagonal
    /// covariance of the state covariance of <paramref name="nominal"/> and of the variances of the coefficients
    /// (independent of the state), by the same Cholesky factor and Box-Muller transform as <see cref="Sample"/>; eight
    /// standard normals per draw, so its stream differs from that of <see cref="Sample"/>.
    /// </summary>
    internal static (StateVector[] States, (double DragCoefficient, double ReflectivityCoefficient)[] Coefficients)
        SampleWithCoefficients(StateVector nominal, double dragCoefficient, double dragCoefficientSigma,
            double reflectivityCoefficient, double reflectivityCoefficientSigma, int count, int seed)
    {
        if (nominal == null)
            throw new ArgumentNullException(nameof(nominal));
        if (!nominal.Covariance.HasValue)
            throw new InvalidOperationException("Nominal state vector must have a covariance matrix.");
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");
        ArgumentOutOfRangeException.ThrowIfNegative(dragCoefficientSigma);
        ArgumentOutOfRangeException.ThrowIfNegative(reflectivityCoefficientSigma);

        var L = nominal.Covariance.Value.Cholesky();
        var rng = new Random(seed);
        var states = new StateVector[count];
        var coefficients = new (double, double)[count];
        for (int i = 0; i < count; i++)
        {
            var z = GenerateStandardNormals(rng, 8);
            states[i] = Perturbed(nominal, L, z[..6]);
            coefficients[i] = (dragCoefficient + dragCoefficientSigma * z[6],
                reflectivityCoefficient + reflectivityCoefficientSigma * z[7]);
        }

        return (states, coefficients);
    }

    private static StateVector Perturbed(StateVector nominal, Matrix L, double[] z)
    {
        var delta = L.Multiply(z);

        var perturbedPosition = new Vector3(
            nominal.Position.X + delta[0],
            nominal.Position.Y + delta[1],
            nominal.Position.Z + delta[2]);

        var perturbedVelocity = new Vector3(
            nominal.Velocity.X + delta[3],
            nominal.Velocity.Y + delta[4],
            nominal.Velocity.Z + delta[5]);

        return new StateVector(perturbedPosition, perturbedVelocity, nominal.Observer, nominal.Epoch, nominal.Frame);
    }

    private static double[] GenerateStandardNormals(Random rng, int count)
    {
        var normals = new double[count];
        for (int i = 0; i < count; i += 2)
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
