using System;
using MathNet.Numerics.Differentiation;

namespace IO.Astrodynamics.Math;

/// <summary>
/// Class for evaluating the Jacobian of a function using finite differences. By default, a central 3-point method is used.
/// </summary>
public class Jacobian : NumericalJacobian
{
    /// <summary>
    /// Creates a Jacobian with a three point central difference method
    /// </summary>
    public Jacobian() : this(3, 1)
    {
    }

    /// <inheritdoc />
    public Jacobian(int points, int center) : base(points, center)
    {
    }

    /// <summary>
    /// Evaluates the Jacobian of a multivariate function array f at vector x.
    /// </summary>
    /// <param name="f">Multivariate function array</param>
    /// <param name="x">Vector at which to evaluate Jacobian</param>
    /// <returns></returns>
    public new Matrix Evaluate(Func<double[], double>[] f, double[] x)
    {
        return new Matrix(base.Evaluate(f, x));
    }

    /// <summary>
    /// Evaluates the Jacobian of a multivariate function array f at vector x given a vector of current function values.
    /// </summary>
    /// <param name="f">Multivariate function array handle.</param>
    /// <param name="x">Vector at which to evaluate Jacobian</param>
    /// <param name="currentValues"></param>
    /// <returns></returns>
    public new Matrix Evaluate(Func<double[], double>[] f, double[] x, double[] currentValues)
    {
        if (f == null) throw new ArgumentNullException(nameof(f));
        if (x == null) throw new ArgumentNullException(nameof(x));
        if (currentValues == null) throw new ArgumentNullException(nameof(currentValues));
        return new Matrix(base.Evaluate(f, x, currentValues));
    }
}