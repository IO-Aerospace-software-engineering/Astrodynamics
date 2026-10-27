// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Math;

/// <summary>
/// Brent's derivative-free methods on a bracket: the zero of a function that changes sign, and the minimum of a
/// unimodal function.
/// </summary>
/// <remarks>
/// <para>
/// Both are transcriptions of R. P. Brent, <i>Algorithms for Minimization without Derivatives</i>, Prentice-Hall,
/// 1973: the procedure <c>zero</c> of chapter 4 (inverse quadratic interpolation, secant and bisection, the bracket
/// kept at every step) and <c>localmin</c> of chapter 5 (golden section and successive parabolic interpolation), in
/// the form of their Fortran versions <c>zeroin</c> and <c>fmin</c> of Netlib (confirmed by S. Guillet, 2026-10-27).
/// </para>
/// <para>
/// <see cref="FindRoot"/> returns a point within <c>tolerance + 4 ε |x|</c> of a sign change of the function in the
/// bracket (ε the machine epsilon), whatever the function, as bisection does, and converges superlinearly on a smooth
/// function. Both methods are pure: they keep nothing between calls.
/// </para>
/// </remarks>
internal static class Brent
{
    // The machine epsilon of a double, 2^-52
    private const double MachineEpsilon = 2.220446049250313e-16;

    // The square root of the machine epsilon, the relative precision of a minimum
    private static readonly double SqrtMachineEpsilon = System.Math.Sqrt(MachineEpsilon);

    // (3 - √5) / 2, the golden section ratio of localmin
    private static readonly double GoldenSection = 0.5 * (3.0 - System.Math.Sqrt(5.0));

    // Far above what a tolerance of the machine epsilon on any bracket needs: bisection alone halves it at each step
    private const int MaxIterations = 400;

    /// <summary>
    /// A zero of <paramref name="f"/> in [<paramref name="a"/>, <paramref name="b"/>], where it changes sign.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="a">One end of the bracket.</param>
    /// <param name="b">The other end of the bracket.</param>
    /// <param name="fa">f(a).</param>
    /// <param name="fb">f(b), of the sign opposite to <paramref name="fa"/>, or either of them zero.</param>
    /// <param name="tolerance">The absolute tolerance on the zero, positive.</param>
    /// <returns>A point within <paramref name="tolerance"/> + 4 ε |x| of a sign change of f.</returns>
    /// <exception cref="ArgumentException">f(a) and f(b) have the same sign, or the tolerance is not positive.</exception>
    internal static double FindRoot(Func<double, double> f, double a, double b, double fa, double fb, double tolerance)
    {
        if (!(tolerance > 0.0))
        {
            throw new ArgumentException("The tolerance must be positive.", nameof(tolerance));
        }

        if (fa == 0.0)
        {
            return a;
        }

        if (fb == 0.0)
        {
            return b;
        }

        if (fa > 0.0 == fb > 0.0)
        {
            throw new ArgumentException("The function must change sign on the bracket.");
        }

        // zeroin: c is the other end of the bracket [b, c], b the best estimate, a the previous one
        double c = a;
        double fc = fa;
        double d = b - a;
        double e = d;
        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            if (System.Math.Abs(fc) < System.Math.Abs(fb))
            {
                a = b;
                b = c;
                c = a;
                fa = fb;
                fb = fc;
                fc = fa;
            }

            double tol1 = 2.0 * MachineEpsilon * System.Math.Abs(b) + 0.5 * tolerance;
            double xm = 0.5 * (c - b);
            if (System.Math.Abs(xm) <= tol1 || fb == 0.0)
            {
                return b;
            }

            if (System.Math.Abs(e) >= tol1 && System.Math.Abs(fa) > System.Math.Abs(fb))
            {
                // Interpolation: secant when only two points are distinct, inverse quadratic otherwise
                double p;
                double q;
                double s = fb / fa;
                if (a == c)
                {
                    p = 2.0 * xm * s;
                    q = 1.0 - s;
                }
                else
                {
                    q = fa / fc;
                    double r = fb / fc;
                    p = s * (2.0 * xm * q * (q - r) - (b - a) * (r - 1.0));
                    q = (q - 1.0) * (r - 1.0) * (s - 1.0);
                }

                if (p > 0.0)
                {
                    q = -q;
                }

                p = System.Math.Abs(p);
                if (2.0 * p < 3.0 * xm * q - System.Math.Abs(tol1 * q) && p < System.Math.Abs(0.5 * e * q))
                {
                    e = d;
                    d = p / q;
                }
                else
                {
                    d = xm;
                    e = d;
                }
            }
            else
            {
                // Bisection
                d = xm;
                e = d;
            }

            a = b;
            fa = fb;
            b += System.Math.Abs(d) > tol1 ? d : xm > 0.0 ? tol1 : -tol1;
            fb = f(b);
            if (fb > 0.0 == fc > 0.0 && fb != 0.0 && fc != 0.0)
            {
                // The sign change is now between a and b
                c = a;
                fc = fa;
                d = b - a;
                e = d;
            }
        }

        throw new InvalidOperationException($"Brent's zero search did not converge in {MaxIterations} iterations.");
    }

    /// <summary>
    /// A local minimum of <paramref name="f"/> in the open interval (<paramref name="a"/>, <paramref name="b"/>), the
    /// minimum when f is unimodal there. The ends of the interval are never evaluated.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="a">The lower end of the interval.</param>
    /// <param name="b">The upper end of the interval, greater than <paramref name="a"/>.</param>
    /// <param name="tolerance">The absolute tolerance on the abscissa of the minimum, positive; the method adds
    /// √ε |x| to it.</param>
    /// <param name="minimum">The value of f at the returned abscissa.</param>
    /// <param name="stopBelow">The search stops at the first point where f is lower than this value, which it
    /// returns: the caller wants to know whether f goes below it, not where its minimum is.</param>
    /// <returns>The abscissa of the minimum, or of the first point below <paramref name="stopBelow"/>.</returns>
    internal static double Minimize(Func<double, double> f, double a, double b, double tolerance, out double minimum,
        double stopBelow = double.NegativeInfinity)
    {
        if (!(b > a))
        {
            throw new ArgumentException("The interval must not be empty.");
        }

        if (!(tolerance > 0.0))
        {
            throw new ArgumentException("The tolerance must be positive.", nameof(tolerance));
        }

        // localmin: x the best point so far, w the second best, v the previous value of w, u the last evaluated
        double v = a + GoldenSection * (b - a);
        double w = v;
        double x = v;
        double d = 0.0;
        double e = 0.0;
        double fx = f(x);
        double fv = fx;
        double fw = fx;
        if (fx < stopBelow)
        {
            minimum = fx;
            return x;
        }

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            double xm = 0.5 * (a + b);
            double tol1 = SqrtMachineEpsilon * System.Math.Abs(x) + tolerance / 3.0;
            double tol2 = 2.0 * tol1;
            if (System.Math.Abs(x - xm) <= tol2 - 0.5 * (b - a))
            {
                break;
            }

            bool golden = true;
            if (System.Math.Abs(e) > tol1)
            {
                // Parabola through x, v and w
                double r = (x - w) * (fx - fv);
                double q = (x - v) * (fx - fw);
                double p = (x - v) * q - (x - w) * r;
                q = 2.0 * (q - r);
                if (q > 0.0)
                {
                    p = -p;
                }

                q = System.Math.Abs(q);
                r = e;
                e = d;
                if (System.Math.Abs(p) < System.Math.Abs(0.5 * q * r) && p > q * (a - x) && p < q * (b - x))
                {
                    d = p / q;
                    double trial = x + d;
                    if (trial - a < tol2 || b - trial < tol2)
                    {
                        d = xm - x >= 0.0 ? tol1 : -tol1;
                    }

                    golden = false;
                }
            }

            if (golden)
            {
                e = x >= xm ? a - x : b - x;
                d = GoldenSection * e;
            }

            double u = System.Math.Abs(d) >= tol1 ? x + d : x + (d >= 0.0 ? tol1 : -tol1);
            double fu = f(u);
            if (fu < stopBelow)
            {
                minimum = fu;
                return u;
            }

            if (fu <= fx)
            {
                if (u >= x)
                {
                    a = x;
                }
                else
                {
                    b = x;
                }

                v = w;
                fv = fw;
                w = x;
                fw = fx;
                x = u;
                fx = fu;
            }
            else
            {
                if (u < x)
                {
                    a = u;
                }
                else
                {
                    b = u;
                }

                if (fu <= fw || w == x)
                {
                    v = w;
                    fv = fw;
                    w = u;
                    fw = fu;
                }
                else if (fu <= fv || v == x || v == w)
                {
                    v = u;
                    fv = fu;
                }
            }
        }

        minimum = fx;
        return x;
    }
}
