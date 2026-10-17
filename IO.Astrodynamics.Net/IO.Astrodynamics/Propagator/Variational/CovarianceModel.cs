// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Propagator.Forces;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// The propagated covariance from the variational values: P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q (C2), and the extended
/// covariance of the state and the consider parameters, [[P, Ψc Pp], [Pp Ψcᵀ, Pp]] (C4); with an initial
/// state–parameter covariance C0, P = Φ P0 Φᵀ + Φ C0 Ψcᵀ + Ψc C0ᵀ Φᵀ + Ψc Pp Ψcᵀ + Q and the cross block Φ C0 + Ψc Pp.
/// </summary>
/// <remarks>
/// <para>
/// The state deviation is linear in the initial deviation, the consider parameters and the noise:
/// δx(t) = Φ δx0 + Ψ δp + w(t), the three independent, so its covariance is the sum of their covariances through the
/// linear maps, Cov(J x) = J Cov(x) Jᵀ (Tapley, Schutz and Born, <i>Statistical Orbit Determination</i>, 2004,
/// chapter 4). Ψc holds the columns of Ψ of the consider parameters and Pp = diag(σ²) their covariance, the parameters
/// being constant. The extended covariance is that of (δx, δp) = M (δx0, δp) + (w, 0), M = [[Φ, Ψc], [0, I]].
/// </para>
/// <para>
/// The initial deviation and the parameters are independent at the start of a propagation from an initial state
/// (C0 = 0). A propagation that starts from a state already propagated with the same consider parameters, such as an
/// avoidance option from the state at its burn (step 10, E4), starts from the extended covariance
/// [[P0, C0], [C0ᵀ, Pp]] instead, C0 = cov(δx0, δp): the same relation Cov(J x) = J Cov(x) Jᵀ with J = M gives the
/// cross terms Φ C0 Ψcᵀ + Ψc C0ᵀ Φᵀ in P and Φ C0 in the cross block.
/// </para>
/// <para>
/// Each matrix is symmetrized, (P + Pᵀ) / 2. Nothing else is corrected: a published covariance that is not positive
/// semi-definite is a defect, checked by the tests (C6).
/// </para>
/// <para>
/// Not thread-safe: a work buffer is reused. The evaluations of a solution run under its lock.
/// </para>
/// </remarks>
internal sealed class CovarianceModel
{
    private const int N = VariationalEquations.StateDimension;

    private readonly double[] _initialCovariance;
    private readonly int[] _considerColumns;
    private readonly double[] _considerVariances;
    private readonly double[] _work = new double[N * N];

    // The initial state–parameter covariance C0, 6 × the number of consider parameters, row-major; null when zero
    private double[] _initialCross;
    private double[] _crossWork;

    /// <summary>
    /// Create the model.
    /// </summary>
    /// <param name="initialCovariance">P0, 6×6 row-major, symmetric and positive semi-definite, in the frame of the
    /// propagation (m², m²/s, m²/s²).</param>
    /// <param name="options">The options of the variational equations: the columns of Ψ and the consider
    /// parameters.</param>
    /// <exception cref="ArgumentException"><paramref name="initialCovariance"/> does not hold 36 values, or a consider
    /// parameter has no column in Ψ.</exception>
    internal CovarianceModel(ReadOnlySpan<double> initialCovariance, VariationalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (initialCovariance.Length != N * N)
        {
            throw new ArgumentException($"P0 must hold 36 values; it holds {initialCovariance.Length}.",
                nameof(initialCovariance));
        }

        var consider = options.ConsiderParameters?.Parameters ?? ForceParameters.None;
        if ((consider & ~options.Parameters) != 0)
        {
            throw new ArgumentException(
                $"A consider parameter needs its column of Ψ: the parameters are {options.Parameters}, the consider " +
                $"parameters {consider}.", nameof(options));
        }

        _initialCovariance = initialCovariance.ToArray();
        ColumnCount = N + (options.Parameters.HasFlag(ForceParameters.DragCoefficient) ? 1 : 0) +
                      (options.Parameters.HasFlag(ForceParameters.ReflectivityCoefficient) ? 1 : 0);

        // The columns of Ψ: Cd first, then Cr
        var columns = new List<int>();
        var variances = new List<double>();
        int column = N;
        if (options.Parameters.HasFlag(ForceParameters.DragCoefficient))
        {
            if (options.ConsiderParameters?.DragCoefficientSigma is { } sigma)
            {
                columns.Add(column);
                variances.Add(sigma * sigma);
            }

            column++;
        }

        if (options.ConsiderParameters?.ReflectivityCoefficientSigma is { } sigmaCr)
        {
            columns.Add(column);
            variances.Add(sigmaCr * sigmaCr);
        }

        _considerColumns = columns.ToArray();
        _considerVariances = variances.ToArray();
    }

    /// <summary>Number of columns of Y, 6 + p.</summary>
    internal int ColumnCount { get; }

    /// <summary>
    /// Set the initial state–parameter covariance C0 = cov(δx0, δp), 6 × the number of consider parameters, row-major, the
    /// consider parameters in the order of the extended covariance (Cd, then Cr); in the frame of P0.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="cross"/> does not hold 6 × the number of consider parameters
    /// values, or a value is not finite.</exception>
    internal void SetInitialCrossCovariance(ReadOnlySpan<double> cross)
    {
        if (cross.Length != N * ConsiderCount)
        {
            throw new ArgumentException(
                $"C0 holds {N * ConsiderCount} values (6 × {ConsiderCount} consider parameters); got {cross.Length}.",
                nameof(cross));
        }

        foreach (var value in cross)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentException("C0 must hold finite values.", nameof(cross));
            }
        }

        _initialCross = cross.ToArray();
        _crossWork = new double[N * ConsiderCount];
    }

    /// <summary>Number of consider parameters.</summary>
    internal int ConsiderCount => _considerColumns.Length;

    /// <summary>Dimension of the extended covariance, 6 + the number of consider parameters.</summary>
    internal int ExtendedDimension => N + ConsiderCount;

    /// <summary>
    /// P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q, symmetrized.
    /// </summary>
    /// <param name="y">The cumulative Y = [Φ | Ψ], 6 × (6 + p).</param>
    /// <param name="q">The cumulative Q, its 21 upper elements.</param>
    /// <param name="covariance">P, 6×6 row-major.</param>
    internal void Covariance(ReadOnlySpan<double> y, ReadOnlySpan<double> q, Span<double> covariance)
    {
        ValidateLengths(y, q);
        if (covariance.Length != N * N)
        {
            throw new ArgumentException($"P holds 36 values; got {covariance.Length}.", nameof(covariance));
        }

        Compute(y, q, covariance, N);
    }

    /// <summary>
    /// The extended covariance [[P, Ψc Pp], [Pp Ψcᵀ, Pp]], m = 6 + the number of consider parameters.
    /// </summary>
    /// <param name="y">The cumulative Y = [Φ | Ψ], 6 × (6 + p).</param>
    /// <param name="q">The cumulative Q, its 21 upper elements.</param>
    /// <param name="extended">The extended covariance, m×m row-major.</param>
    internal void ExtendedCovariance(ReadOnlySpan<double> y, ReadOnlySpan<double> q, Span<double> extended)
    {
        ValidateLengths(y, q);
        int m = ExtendedDimension;
        if (extended.Length != m * m)
        {
            throw new ArgumentException($"The extended covariance holds {m * m} values; got {extended.Length}.",
                nameof(extended));
        }

        Compute(y, q, extended, m);
        int n = ColumnCount;
        for (int c = 0; c < ConsiderCount; c++)
        {
            int column = _considerColumns[c];
            double variance = _considerVariances[c];
            for (int i = 0; i < N; i++)
            {
                // Ψc Pp, and Φ C0 with an initial cross covariance
                double value = y[i * n + column] * variance;
                if (_initialCross != null)
                {
                    value += _crossWork[i * ConsiderCount + c];
                }

                extended[i * m + N + c] = value;
                extended[(N + c) * m + i] = value;
            }

            for (int d = 0; d < ConsiderCount; d++)
            {
                extended[(N + c) * m + N + d] = c == d ? variance : 0.0;
            }
        }
    }

    // P into the upper-left 6×6 block of a matrix of stride m
    private void Compute(ReadOnlySpan<double> y, ReadOnlySpan<double> q, Span<double> p, int m)
    {
        int n = ColumnCount;

        // T = Φ P0
        var t = _work.AsSpan();
        for (int i = 0; i < N; i++)
        {
            for (int j = 0; j < N; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < N; k++)
                {
                    sum += y[i * n + k] * _initialCovariance[N * k + j];
                }

                t[N * i + j] = sum;
            }
        }

        // With an initial cross covariance, Φ C0, kept for the cross block of the extended covariance
        if (_initialCross != null)
        {
            for (int i = 0; i < N; i++)
            {
                for (int c = 0; c < ConsiderCount; c++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < N; k++)
                    {
                        sum += y[i * n + k] * _initialCross[k * ConsiderCount + c];
                    }

                    _crossWork[i * ConsiderCount + c] = sum;
                }
            }
        }

        // P = T Φᵀ + Σ σ² ψ ψᵀ + Q, on the upper triangle, then mirrored: the symmetrization of the specification
        for (int i = 0; i < N; i++)
        {
            for (int j = i; j < N; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < N; k++)
                {
                    sum += t[N * i + k] * y[j * n + k];
                }

                double symmetric = sum;
                if (j > i)
                {
                    double lower = 0.0;
                    for (int k = 0; k < N; k++)
                    {
                        lower += t[N * j + k] * y[i * n + k];
                    }

                    symmetric = 0.5 * (sum + lower);
                }

                for (int c = 0; c < _considerColumns.Length; c++)
                {
                    int column = _considerColumns[c];
                    symmetric += _considerVariances[c] * y[i * n + column] * y[j * n + column];
                    if (_initialCross != null)
                    {
                        // Φ C0 Ψcᵀ + Ψc C0ᵀ Φᵀ, symmetric by construction
                        symmetric += _crossWork[i * ConsiderCount + c] * y[j * n + column] +
                                     y[i * n + column] * _crossWork[j * ConsiderCount + c];
                    }
                }

                symmetric += q[VariationalEquations.CovarianceIndex(i, j)];
                p[i * m + j] = symmetric;
                p[j * m + i] = symmetric;
            }
        }
    }

    private void ValidateLengths(ReadOnlySpan<double> y, ReadOnlySpan<double> q)
    {
        if (y.Length != N * ColumnCount)
        {
            throw new ArgumentException($"Y holds {N * ColumnCount} values; got {y.Length}.", nameof(y));
        }

        if (q.Length != VariationalEquations.CovarianceLength)
        {
            throw new ArgumentException($"Q holds {VariationalEquations.CovarianceLength} values; got {q.Length}.",
                nameof(q));
        }
    }
}
