// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Tests.Propagators.Variational;

/// <summary>
/// A real number with its gradient with respect to the six components of an initial state: forward-mode automatic
/// differentiation, in which each operation applies the chain rule to the gradient of its operands (Griewank &amp;
/// Walther, Evaluating Derivatives, 2nd ed., SIAM 2008, chapter 3; to be verified by S. Guillet).
/// </summary>
/// <remarks>
/// A constant holds no gradient. The derivatives are exact to the rounding of the operations: no step, no truncation.
/// Test code only: every operation allocates its gradient.
/// </remarks>
internal readonly struct Dual
{
    /// <summary>Number of differentiation directions: the components of the initial position and velocity.</summary>
    internal const int Directions = 6;

    private readonly double[] _gradient;

    private Dual(double value, double[] gradient)
    {
        Value = value;
        _gradient = gradient;
    }

    /// <summary>The value.</summary>
    internal double Value { get; }

    /// <summary>The derivative with respect to direction <paramref name="direction"/>.</summary>
    internal double Derivative(int direction) => _gradient?[direction] ?? 0.0;

    /// <summary>A constant: zero gradient.</summary>
    internal static Dual Constant(double value) => new(value, null);

    /// <summary>An independent variable: unit gradient along <paramref name="direction"/>.</summary>
    internal static Dual Variable(double value, int direction)
    {
        var gradient = new double[Directions];
        gradient[direction] = 1.0;
        return new Dual(value, gradient);
    }

    public static implicit operator Dual(double value) => Constant(value);

    public static Dual operator +(Dual a, Dual b) => new(a.Value + b.Value, Combine(1.0, a, 1.0, b));

    public static Dual operator -(Dual a, Dual b) => new(a.Value - b.Value, Combine(1.0, a, -1.0, b));

    public static Dual operator -(Dual a) => new(-a.Value, Combine(-1.0, a, 0.0, default));

    // d(ab) = b da + a db
    public static Dual operator *(Dual a, Dual b) => new(a.Value * b.Value, Combine(b.Value, a, a.Value, b));

    // d(a/b) = da / b − a db / b²
    public static Dual operator /(Dual a, Dual b)
    {
        double quotient = a.Value / b.Value;
        return new Dual(quotient, Combine(1.0 / b.Value, a, -quotient / b.Value, b));
    }

    // d√a = da / (2√a)
    internal static Dual Sqrt(Dual a)
    {
        double root = System.Math.Sqrt(a.Value);
        return new Dual(root, Combine(0.5 / root, a, 0.0, default));
    }

    // d sin a = cos a da
    internal static Dual Sin(Dual a) => new(System.Math.Sin(a.Value), Combine(System.Math.Cos(a.Value), a, 0.0, default));

    // d cos a = −sin a da
    internal static Dual Cos(Dual a) => new(System.Math.Cos(a.Value), Combine(-System.Math.Sin(a.Value), a, 0.0, default));

    // The gradient of ca a + cb b, null when both operands are constants
    private static double[] Combine(double ca, Dual a, double cb, Dual b)
    {
        if (a._gradient == null && b._gradient == null)
        {
            return null;
        }

        var gradient = new double[Directions];
        for (int i = 0; i < Directions; i++)
        {
            gradient[i] = ca * a.Derivative(i) + cb * b.Derivative(i);
        }

        return gradient;
    }
}
