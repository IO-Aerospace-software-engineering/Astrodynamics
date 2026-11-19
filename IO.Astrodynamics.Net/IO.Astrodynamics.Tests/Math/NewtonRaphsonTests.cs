using System;
using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Math;

public class NewtonRaphsonTests
{
    [Fact]
    public void BoundedNewtonRaphson_ConvergesWithinBounds_ReturnsRoot()
    {
        Func<double, double> f = x => x * x - 4;
        Func<double, double> df = x => 2 * x;
        double root = NewtonRaphson.BoundedNewtonRaphson(f, df, 3, 0, 5, 1e-6, 100);
        Assert.InRange(root, 1.99, 2.01);
    }

    [Fact]
    public void BoundedNewtonRaphson_InitialGuessOutOfBounds_ReturnsBoundedRoot()
    {
        Func<double, double> f = x => x * x - 4;
        Func<double, double> df = x => 2 * x;
        double root = NewtonRaphson.BoundedNewtonRaphson(f, df, 6, 0, 5, 1e-6, 100);
        Assert.InRange(root, 1.99, 2.01);
    }

    [Fact]
    public void BoundedNewtonRaphson_DerivativeTooSmall_ThrowsException()
    {
        Func<double, double> f = x => x * x - 4;
        Func<double, double> df = x => 0;
        Assert.Throws<InvalidOperationException>(() => NewtonRaphson.BoundedNewtonRaphson(f, df, 3, 0, 5, 1e-6, 100));
    }

    [Fact]
    public void BoundedNewtonRaphson_Diverges_ThrowsException()
    {
        Func<double, double> f = x => System.Math.Exp(x) - 1;
        Func<double, double> df = x => System.Math.Exp(x);
        Assert.Throws<InvalidOperationException>(() => NewtonRaphson.BoundedNewtonRaphson(f, df, 10, 0, 5, 1e-6, 10));
    }

    [Fact]
    public void BoundedNewtonRaphson_ConvergesToMinBound_ReturnsMinBound()
    {
        Func<double, double> f = x => x - 0.1;
        Func<double, double> df = x => 1;
        double root = NewtonRaphson.BoundedNewtonRaphson(f, df, -1, 0, 5, 1e-6, 100);
        Assert.Equal(0.1, root, 12);
    }

    [Fact]
    public void BoundedNewtonRaphson_ConvergesToMaxBound_ReturnsMaxBound()
    {
        Func<double, double> f = x => x - 6;
        Func<double, double> df = x => 1;
        double root = NewtonRaphson.BoundedNewtonRaphson(f, df, 7, 0, 5, 1e-6, 100);
        Assert.Equal(5, root);
    }

    [Fact]
    public void SolveVector_ConvergesToCorrectSolution()
    {
        Func<double[], double[]> costFunc = x => new double[] { x[0] * x[0] - 4, x[1] - 2 };
        Func<double[], double[,]> jacobianFunc = x => new double[,] { { 2 * x[0], 0 }, { 0, 1 } };
        double[] initialGuess = new double[] { 3, 0 };
        double[] result = NewtonRaphson.SolveVector(costFunc, jacobianFunc, initialGuess, 1e-6, 100);
        Assert.InRange(result[0], 1.99, 2.01);
        Assert.InRange(result[1], 1.99, 2.01);
    }

    [Fact]
    public void SolveVector_Diverges_ThrowsException()
    {
        Func<double[], double[]> costFunc = x => new double[] { System.Math.Exp(x[0]) - 1, x[1] - 2 };
        Func<double[], double[,]> jacobianFunc = x => new double[,] { { System.Math.Exp(x[0]), 0 }, { 0, 1 } };
        double[] initialGuess = new double[] { 10, 0 };
        Assert.Throws<InvalidOperationException>(() => NewtonRaphson.SolveVector(costFunc, jacobianFunc, initialGuess, 1e-6, 10));
    }

    [Fact]
    public void SolveVector_ZeroJacobian_ThrowsException()
    {
        Func<double[], double[]> costFunc = x => new double[] { x[0] * x[0] - 4, x[1] - 2 };
        Func<double[], double[,]> jacobianFunc = x => new double[,] { { 0, 0 }, { 0, 0 } };
        double[] initialGuess = new double[] { 3, 0 };
        Assert.Throws<InvalidOperationException>(() => NewtonRaphson.SolveVector(costFunc, jacobianFunc, initialGuess, 1e-6, 100));
    }

    [Fact]
    public void SolveVector_ConvergesWithHighTolerance_ReturnsApproximateSolution()
    {
        Func<double[], double[]> costFunc = x => new double[] { x[0] * x[0] - 4, x[1] - 2 };
        Func<double[], double[,]> jacobianFunc = x => new double[,] { { 2 * x[0], 0 }, { 0, 1 } };
        double[] initialGuess = new double[] { 3, 0 };
        double[] result = NewtonRaphson.SolveVector(costFunc, jacobianFunc, initialGuess, 1e-1, 100);
        Assert.InRange(result[0], 1.8, 2.2);
        Assert.InRange(result[1], 1.8, 2.2);
    }

    [Fact]
    public void SolveVector_NormBelowTolerance_ReturnsInitialGuess()
    {
        Func<double[], double[]> costFunc = x => new double[] { 0.00001, 0.00001 };
        Func<double[], double[,]> jacobianFunc = x => new double[,] { { 1, 0 }, { 0, 1 } };
        double[] initialGuess = new double[] { 1, 1 };
        double[] result = NewtonRaphson.SolveVector(costFunc, jacobianFunc, initialGuess, 1e-3, 100);
        Assert.Equal(initialGuess, result);
    }

    [Fact]
    public void Solve_ConvergesToRootForQuadraticFunction()
    {
        Func<double, double> function = x => x * x - 4;
        Func<double, double> derivative = x => 2 * x;
        double root = NewtonRaphson.Solve(function, derivative, 3, 1e-6, 100);
        Assert.InRange(root, 1.99, 2.01);
    }

    [Fact]
    public void Solve_DivergesForNonConvergingFunction_ThrowsException()
    {
        Func<double, double> function = x => System.Math.Exp(x) - 1;
        Func<double, double> derivative = x => System.Math.Exp(x);
        Assert.Throws<InvalidOperationException>(() => NewtonRaphson.Solve(function, derivative, 10, 1e-6, 10));
    }

    [Fact]
    public void Solve_ZeroDerivative_ThrowsException()
    {
        Func<double, double> function = x => x * x - 4;
        Func<double, double> derivative = x => 0;
        Assert.Throws<InvalidOperationException>(() => NewtonRaphson.Solve(function, derivative, 3, 1e-6, 100));
    }

    [Fact]
    public void Solve_ConvergesWithHighTolerance_ReturnsApproximateRoot()
    {
        Func<double, double> function = x => x * x - 4;
        Func<double, double> derivative = x => 2 * x;
        double root = NewtonRaphson.Solve(function, derivative, 3, 1e-1, 100);
        Assert.InRange(root, 1.8, 2.2);
    }

    [Fact]
    public void Solve_InitialGuessIsRoot_ReturnsInitialGuess()
    {
        Func<double, double> function = x => x * x - 4;
        Func<double, double> derivative = x => 2 * x;
        double root = NewtonRaphson.Solve(function, derivative, 2, 1e-6, 100);
        Assert.Equal(2, root);
    }

    // R03 of #668: the solver throws InvalidOperationException, not System.Exception, for each of its three failures
    [Fact]
    public void Solve_DerivativeTooSmall_ThrowsInvalidOperationException()
    {
        var error = Assert.Throws<InvalidOperationException>(() => NewtonRaphson.Solve(x => x * x - 4, _ => 0.0, 3.0));
        Assert.Contains("Derivative too small", error.Message);
    }

    [Fact]
    public void Solve_Diverges_ThrowsInvalidOperationException()
    {
        // A step of -1.8e308 / 1e-11 from 0: an infinite estimate
        var error = Assert.Throws<InvalidOperationException>(() => NewtonRaphson.Solve(_ => double.MaxValue, _ => 1e-11, 0.0));
        Assert.Contains("diverged", error.Message);
    }

    [Fact]
    public void Solve_DoesNotConverge_ThrowsInvalidOperationException()
    {
        // x^2 + 1 has no real root: the estimates wander without converging
        var error = Assert.Throws<InvalidOperationException>(() => NewtonRaphson.Solve(x => x * x + 1.0, x => 2.0 * x, 0.5, 1e-12, 20));
        Assert.Contains("did not converge", error.Message);
    }
}