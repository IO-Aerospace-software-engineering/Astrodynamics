// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// Reference derivatives for the tests of the force partials: Ridders' method, a Richardson extrapolation of central
/// differences over a decreasing sequence of steps, which also estimates its own error.
/// </summary>
/// <remarks>
/// References: C. J. F. Ridders, "Accurate computation of F'(x) and F'(x)F''(x)", Advances in Engineering Software
/// 4(2), 75-76 (1982); Press, Teukolsky, Vetterling and Flannery, Numerical Recipes, 3rd edition (2007), section 5.7,
/// routine <c>dfridr</c>, with its constants (step contraction 1.4, table size 10, stop when the error grows by a factor
/// of 2). Both confirmed by S. Guillet (2026-10-07). The helper is checked against closed-form derivatives in
/// <see cref="RiddersDerivativeTests"/>.
/// </remarks>
internal static class RiddersDerivative
{
    private const double Contraction = 1.4;
    private const double Safe = 2.0;
    private const int TableSize = 10;

    /// <summary>
    /// Derivative of <paramref name="function"/> at <paramref name="x"/>.
    /// </summary>
    /// <param name="function">The function.</param>
    /// <param name="x">The point.</param>
    /// <param name="initialStep">
    /// The largest step, over which the function should change appreciably: the extrapolation goes down from it.
    /// </param>
    /// <returns>The derivative and the estimate of its absolute error.</returns>
    internal static (double Derivative, double Error) Derivative(Func<double, double> function, double x,
        double initialStep)
    {
        var table = new double[TableSize, TableSize];
        double contraction2 = Contraction * Contraction;
        double step = initialStep;
        table[0, 0] = (function(x + step) - function(x - step)) / (2.0 * step);
        double error = double.MaxValue;
        double derivative = table[0, 0];
        for (int i = 1; i < TableSize; i++)
        {
            step /= Contraction;
            table[0, i] = (function(x + step) - function(x - step)) / (2.0 * step);
            double factor = contraction2;
            for (int j = 1; j <= i; j++)
            {
                table[j, i] = (table[j - 1, i] * factor - table[j - 1, i - 1]) / (factor - 1.0);
                factor *= contraction2;
                double candidateError = System.Math.Max(System.Math.Abs(table[j, i] - table[j - 1, i]),
                    System.Math.Abs(table[j, i] - table[j - 1, i - 1]));
                if (candidateError <= error)
                {
                    error = candidateError;
                    derivative = table[j, i];
                }
            }

            if (System.Math.Abs(table[i, i] - table[i - 1, i - 1]) >= Safe * error)
            {
                break;
            }
        }

        return (derivative, error);
    }

    /// <summary>
    /// ∂a/∂r and ∂a/∂v of a force at a state, row-major, by <see cref="Derivative"/> on each component of each column,
    /// with the relative error estimates ‖error‖_F / ‖J‖_F of the two blocks.
    /// </summary>
    /// <param name="force">The force.</param>
    /// <param name="state">The state.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="positionStep">Initial step on the position, in m.</param>
    /// <param name="velocityStep">Initial step on the velocity, in m/s.</param>
    /// <returns>The two 3×3 blocks and their estimated relative errors.</returns>
    internal static (double[] Dadr, double[] Dadv, double DadrError, double DadvError) StatePartials(ForceBase force,
        StateVector state, ForceEvaluationContext context, double positionStep, double velocityStep)
    {
        var dadr = new double[9];
        var dadv = new double[9];
        var dadrError = new double[9];
        var dadvError = new double[9];
        for (int j = 0; j < 3; j++)
        {
            int column = j;
            for (int i = 0; i < 3; i++)
            {
                int row = i;
                (dadr[3 * i + j], dadrError[3 * i + j]) = Derivative(
                    t => Component(force.Apply(WithPosition(state, column, t), context), row),
                    Component(state.Position, column), positionStep);
                (dadv[3 * i + j], dadvError[3 * i + j]) = Derivative(
                    t => Component(force.Apply(WithVelocity(state, column, t), context), row),
                    Component(state.Velocity, column), velocityStep);
            }
        }

        return (dadr, dadv, RelativeNorm(dadrError, dadr), RelativeNorm(dadvError, dadv));
    }

    private static double RelativeNorm(double[] error, double[] matrix)
    {
        double e = 0.0;
        double m = 0.0;
        for (int k = 0; k < matrix.Length; k++)
        {
            e += error[k] * error[k];
            m += matrix[k] * matrix[k];
        }

        return m == 0.0 ? (e == 0.0 ? 0.0 : double.PositiveInfinity) : System.Math.Sqrt(e / m);
    }

    /// <summary>
    /// ‖actual − expected‖_F / ‖expected‖_F.
    /// </summary>
    internal static double RelativeFrobeniusError(ReadOnlySpan<double> actual, ReadOnlySpan<double> expected)
    {
        double difference = 0.0;
        double reference = 0.0;
        for (int k = 0; k < expected.Length; k++)
        {
            double d = actual[k] - expected[k];
            difference += d * d;
            reference += expected[k] * expected[k];
        }

        return System.Math.Sqrt(difference / reference);
    }

    internal static double Component(in Vector3 vector, int index)
    {
        return index switch
        {
            0 => vector.X,
            1 => vector.Y,
            _ => vector.Z
        };
    }

    private static Vector3 With(in Vector3 vector, int index, double value)
    {
        return index switch
        {
            0 => new Vector3(value, vector.Y, vector.Z),
            1 => new Vector3(vector.X, value, vector.Z),
            _ => new Vector3(vector.X, vector.Y, value)
        };
    }

    private static StateVector WithPosition(StateVector state, int index, double value)
    {
        return new StateVector(With(state.Position, index, value), state.Velocity, state.Observer, state.Epoch,
            state.Frame);
    }

    private static StateVector WithVelocity(StateVector state, int index, double value)
    {
        return new StateVector(state.Position, With(state.Velocity, index, value), state.Observer, state.Epoch,
            state.Frame);
    }
}
