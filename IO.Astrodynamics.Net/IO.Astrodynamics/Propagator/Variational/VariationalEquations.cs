// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator.Variational;

/// <summary>
/// The variational equations of the propagated state, advanced over one RK7(8) step with the stages of the state: the
/// state transition matrix Φ, the sensitivity Ψ to the force parameters, and the covariance Q of the process noise.
/// </summary>
/// <remarks>
/// <para>
/// With x = (r, v), a = a(r, v, t; p), G = ∂a/∂r, D = ∂a/∂v, A = [[0, I], [G, D]] and B = [0 ; I]:
/// dΦ/dt = A Φ, Φ(t0) = I; dΨ/dt = A Ψ + B ∂a/∂p, Ψ(t0) = 0 (Montenbruck &amp; Gill, Satellite Orbits, 2000, §7.2,
/// to be verified by S. Guillet); dQ/dt = A Q + Q Aᵀ + B Qc Bᵀ, Q(t0) = 0 (Gelb, Applied Optimal Estimation, 1974,
/// chapter 4, and Tapley, Schutz &amp; Born, Statistical Orbit Determination, 2004, §4.9, to be verified by
/// S. Guillet).
/// </para>
/// <para>
/// Y = [Φ | Ψ] is 6×n, n = 6 + p, row-major: rows 0–2 position, rows 3–5 velocity; columns 0–5 Φ, then Ψ (Cd, then Cr).
/// Q is stored as its 21 upper elements, row by row.
/// </para>
/// <para>
/// Discretization: the stages of the state step, x_s, are given. Y_s = Y_0 + h Σ_{j&lt;s} a_sj K_j and
/// K_s = A(x_s) Y_s + [0 | B ∂a/∂p(x_s)], then Y_1 = Y_0 + h Σ b_j K_j. Differentiating the stage equations of the state,
/// x_s = x_0 + h Σ a_sj f(x_j), with respect to x_0 and p gives exactly this recurrence, so Y_1 is the Jacobian of the
/// discrete step map at fixed h, up to the accuracy of the partials. The state stages do not depend on Y, so the state is
/// never touched. Q uses the same stages, with K^Q_s = M_s + M_sᵀ + B Qc_s Bᵀ and M_s = A(x_s) Q_s: symmetric by
/// construction.
/// </para>
/// <para>
/// Not thread-safe: the stage buffers are reused from one step to the next. No allocation per step.
/// </para>
/// </remarks>
internal sealed class VariationalEquations
{
    /// <summary>Dimension of the state, and number of rows of Y.</summary>
    internal const int StateDimension = 6;

    /// <summary>Number of stored elements of Q, its upper triangle.</summary>
    internal const int CovarianceLength = 21;

    private const int Stages = RK78ButcherTableau.Stages;

    private readonly bool _hasDragCoefficient;
    private readonly bool _hasReflectivityCoefficient;
    private readonly IProcessNoiseModel _processNoise;

    // Per stage: G and D (9 each, row-major), ∂a/∂p (3 per parameter), Qc (9)
    private readonly double[] _g = new double[Stages * 9];
    private readonly double[] _d = new double[Stages * 9];
    private readonly double[] _dadp;
    private readonly double[] _qc;

    // Per stage: derivatives of Y and Q
    private readonly double[] _ky;
    private readonly double[] _kq;

    // Scratch
    private readonly double[] _yStage;
    private readonly double[] _qStage = new double[CovarianceLength];
    private readonly double[] _qFull = new double[36];
    private readonly double[] _m = new double[36];
    private readonly double[] _dadCd = new double[3];
    private readonly double[] _dadCr = new double[3];

    private bool _velocityTerms;
    private double _lastStepSize = double.NaN;

    /// <summary>
    /// Create the equations for <paramref name="options"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">The options name a parameter other than Cd and Cr.</exception>
    internal VariationalEquations(VariationalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        const ForceParameters supported = ForceParameters.DragCoefficient | ForceParameters.ReflectivityCoefficient;
        if ((options.Parameters & ~supported) != 0)
        {
            throw new ArgumentException($"Unsupported force parameters: {options.Parameters}.", nameof(options));
        }

        _hasDragCoefficient = (options.Parameters & ForceParameters.DragCoefficient) != 0;
        _hasReflectivityCoefficient = (options.Parameters & ForceParameters.ReflectivityCoefficient) != 0;
        _processNoise = options.ProcessNoise;

        ParameterCount = (_hasDragCoefficient ? 1 : 0) + (_hasReflectivityCoefficient ? 1 : 0);
        ColumnCount = StateDimension + ParameterCount;
        YLength = StateDimension * ColumnCount;

        _dadp = new double[Stages * 3 * ParameterCount];
        _ky = new double[Stages * YLength];
        _yStage = new double[YLength];
        if (_processNoise != null)
        {
            _qc = new double[Stages * 9];
            _kq = new double[Stages * CovarianceLength];
        }
    }

    /// <summary>Number of parameters p, the columns of Ψ.</summary>
    internal int ParameterCount { get; }

    /// <summary>Number of columns n = 6 + p of Y.</summary>
    internal int ColumnCount { get; }

    /// <summary>Number of elements of Y, 6n.</summary>
    internal int YLength { get; }

    /// <summary>Whether Q is integrated.</summary>
    internal bool HasProcessNoise => _processNoise != null;

    /// <summary>Number of stage Jacobian evaluations so far: 13 per step, each summing the partials of all forces.</summary>
    internal long StageJacobianEvaluations { get; private set; }

    /// <summary>
    /// Index of Q(i, j) in the 21 stored elements, for i ≤ j.
    /// </summary>
    internal static int CovarianceIndex(int i, int j)
    {
        return i * (11 - i) / 2 + j;
    }

    /// <summary>
    /// Set <paramref name="y"/> to [I | 0], the value of Y at the start of a segment.
    /// </summary>
    internal void SetIdentity(Span<double> y)
    {
        ValidateLength(y, YLength, nameof(y));
        y.Clear();
        for (int i = 0; i < StateDimension; i++)
        {
            y[i * ColumnCount + i] = 1.0;
        }
    }

    /// <summary>
    /// Advance Y and Q over one step whose 13 stage states are <paramref name="stageStates"/>.
    /// </summary>
    /// <param name="stageStates">The stage states of the step (<see cref="RK78Stepper.StageStates"/>).</param>
    /// <param name="forces">The forces of the propagation.</param>
    /// <param name="context">Mass and coefficients of the segment.</param>
    /// <param name="h">Step size, in seconds.</param>
    /// <param name="y0">Y at the start of the step.</param>
    /// <param name="q0">Q at the start of the step; ignored, and may be empty, without process noise.</param>
    /// <param name="y1">Y at the end of the step.</param>
    /// <param name="q1">Q at the end of the step; ignored, and may be empty, without process noise.</param>
    /// <exception cref="ArgumentException">A buffer has the wrong length, or there are not 13 stage states.</exception>
    internal void Step(IReadOnlyList<StateVector> stageStates, IReadOnlyList<ForceBase> forces,
        in ForceEvaluationContext context, double h, ReadOnlySpan<double> y0, ReadOnlySpan<double> q0,
        Span<double> y1, Span<double> q1)
    {
        ArgumentNullException.ThrowIfNull(stageStates);
        ArgumentNullException.ThrowIfNull(forces);
        if (stageStates.Count != Stages)
        {
            throw new ArgumentException($"A step has {Stages} stage states; {stageStates.Count} were given.",
                nameof(stageStates));
        }

        ValidateLength(y0, YLength, nameof(y0));
        ValidateLength(y1, YLength, nameof(y1));
        if (_processNoise != null)
        {
            ValidateLength(q0, CovarianceLength, nameof(q0));
            ValidateLength(q1, CovarianceLength, nameof(q1));
        }

        EvaluateStageJacobians(stageStates, forces, context);
        AdvanceY(h, y0, y1);
        if (_processNoise != null)
        {
            AdvanceQ(h, q0, q1);
        }

        _lastStepSize = h;
    }

    /// <summary>
    /// The size of the initial perturbation each column of Y stands for, for <see cref="ScaledError"/>: |r0| for the
    /// position columns, |v0| for the velocity columns, |p| for a parameter (1 when p = 0).
    /// </summary>
    /// <param name="position">Position at the start of the segment.</param>
    /// <param name="velocity">Velocity at the start of the segment.</param>
    /// <param name="context">Coefficients of the segment.</param>
    /// <param name="scales">The n scales.</param>
    internal void ColumnScales(in Vector3 position, in Vector3 velocity, in ForceEvaluationContext context,
        Span<double> scales)
    {
        ValidateLength(scales, ColumnCount, nameof(scales));
        double r = position.Magnitude();
        double v = velocity.Magnitude();
        for (int c = 0; c < 3; c++)
        {
            scales[c] = r;
            scales[c + 3] = v;
        }

        int column = StateDimension;
        if (_hasDragCoefficient)
        {
            scales[column++] = ParameterScale(context.DragCoefficient);
        }

        if (_hasReflectivityCoefficient)
        {
            scales[column] = ParameterScale(context.ReflectivityCoefficient);
        }
    }

    /// <summary>
    /// The error of Y over the last <see cref="Step"/>, for the step-size control: each column of the embedded error
    /// estimate, of Y_0 and of Y_1 is multiplied by its scale, which makes it a position and velocity error, then goes
    /// through the component norm of the state (<see cref="RK78Integrator.ScaledComponentError"/>), infinity norm.
    /// </summary>
    /// <param name="y0">Y at the start of the step.</param>
    /// <param name="y1">Y at the end of the step.</param>
    /// <param name="columnScales">The n scales, from <see cref="ColumnScales"/>.</param>
    /// <param name="absoluteTolerance">Absolute tolerance of the state, in m and m/s.</param>
    /// <param name="relativeTolerance">Relative tolerance of the state.</param>
    /// <returns>The normalized error; the step is acceptable for Y when it is at most 1.</returns>
    /// <exception cref="InvalidOperationException">No step has been computed.</exception>
    internal double ScaledError(ReadOnlySpan<double> y0, ReadOnlySpan<double> y1, ReadOnlySpan<double> columnScales,
        double absoluteTolerance, double relativeTolerance)
    {
        if (double.IsNaN(_lastStepSize))
        {
            throw new InvalidOperationException("The error of Y needs a step first.");
        }

        ValidateLength(y0, YLength, nameof(y0));
        ValidateLength(y1, YLength, nameof(y1));
        ValidateLength(columnScales, ColumnCount, nameof(columnScales));

        var e = RK78ButcherTableau.E;
        double maxError = 0.0;
        for (int index = 0; index < YLength; index++)
        {
            double error = 0.0;
            for (int j = 0; j < Stages; j++)
            {
                if (e[j] != 0.0)
                {
                    error += _ky[j * YLength + index] * (_lastStepSize * e[j]);
                }
            }

            double scale = columnScales[index % ColumnCount];
            maxError = System.Math.Max(maxError, RK78Integrator.ScaledComponentError(y0[index] * scale,
                y1[index] * scale, error * scale, absoluteTolerance, relativeTolerance));
        }

        return maxError;
    }

    /// <summary>
    /// The cumulative values from the values relative to a segment start and the cumulative values at that start:
    /// Φ = Φ_seg Φ_entry, Ψ = Φ_seg Ψ_entry + Ψ_seg, Q = Φ_seg Q_entry Φ_segᵀ + Q_seg.
    /// </summary>
    /// <remarks>
    /// The variational equations are linear, so their solution from the start of the propagation is the solution from
    /// the segment start applied to the entry values: x(t) − x̄(t) = Φ_seg (x(te) − x̄(te)) + Ψ_seg δp, with
    /// x(te) − x̄(te) = Φ_entry δx0 + Ψ_entry δp, and the noise accumulated before te propagates through Φ_seg while the
    /// noise after te is independent of it. The outputs must not overlap the inputs.
    /// </remarks>
    /// <param name="columns">Number of columns n of Y.</param>
    /// <param name="segmentY">Y relative to the segment start.</param>
    /// <param name="segmentQ">Q relative to the segment start; empty without Q.</param>
    /// <param name="entryY">Cumulative Y at the segment start.</param>
    /// <param name="entryQ">Cumulative Q at the segment start; empty without Q.</param>
    /// <param name="y">Cumulative Y.</param>
    /// <param name="q">Cumulative Q; empty without Q.</param>
    internal static void Compose(int columns, ReadOnlySpan<double> segmentY, ReadOnlySpan<double> segmentQ,
        ReadOnlySpan<double> entryY, ReadOnlySpan<double> entryQ, Span<double> y, Span<double> q)
    {
        for (int i = 0; i < StateDimension; i++)
        {
            for (int c = 0; c < columns; c++)
            {
                double sum = 0.0;
                for (int k = 0; k < StateDimension; k++)
                {
                    sum += segmentY[i * columns + k] * entryY[k * columns + c];
                }

                if (c >= StateDimension)
                {
                    sum += segmentY[i * columns + c];
                }

                y[i * columns + c] = sum;
            }
        }

        if (q.Length == 0)
        {
            return;
        }

        // T = Φ_seg Q_entry, then Q = T Φ_segᵀ + Q_seg on the upper triangle
        Span<double> t = stackalloc double[36];
        for (int i = 0; i < StateDimension; i++)
        {
            for (int j = 0; j < StateDimension; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < StateDimension; k++)
                {
                    sum += segmentY[i * columns + k] *
                           entryQ[CovarianceIndex(System.Math.Min(k, j), System.Math.Max(k, j))];
                }

                t[6 * i + j] = sum;
            }
        }

        for (int i = 0; i < StateDimension; i++)
        {
            for (int j = i; j < StateDimension; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < StateDimension; k++)
                {
                    sum += t[6 * i + k] * segmentY[j * columns + k];
                }

                q[CovarianceIndex(i, j)] = sum + segmentQ[CovarianceIndex(i, j)];
            }
        }
    }

    private static double ParameterScale(double parameter)
    {
        return parameter != 0.0 ? System.Math.Abs(parameter) : 1.0;
    }

    private void EvaluateStageJacobians(IReadOnlyList<StateVector> stageStates, IReadOnlyList<ForceBase> forces,
        in ForceEvaluationContext context)
    {
        _velocityTerms = false;
        for (int f = 0; f < forces.Count; f++)
        {
            _velocityTerms |= forces[f].DependsOnVelocity;
        }

        Array.Clear(_g);
        Array.Clear(_d);
        Array.Clear(_dadp);
        if (_qc != null)
        {
            Array.Clear(_qc);
        }

        int parameterStride = 3 * ParameterCount;
        for (int s = 0; s < Stages; s++)
        {
            var state = stageStates[s];
            var g = _g.AsSpan(s * 9, 9);
            var d = _d.AsSpan(s * 9, 9);
            for (int f = 0; f < forces.Count; f++)
            {
                forces[f].AccumulateStatePartials(state, context, g, d);
            }

            if (ParameterCount > 0)
            {
                Array.Clear(_dadCd);
                Array.Clear(_dadCr);
                for (int f = 0; f < forces.Count; f++)
                {
                    forces[f].AccumulateParameterPartials(state, context, _dadCd, _dadCr);
                }

                var dadp = _dadp.AsSpan(s * parameterStride, parameterStride);
                int offset = 0;
                if (_hasDragCoefficient)
                {
                    _dadCd.CopyTo(dadp.Slice(offset, 3));
                    offset += 3;
                }

                if (_hasReflectivityCoefficient)
                {
                    _dadCr.CopyTo(dadp.Slice(offset, 3));
                }
            }

            _processNoise?.Accumulate(state, _qc.AsSpan(s * 9, 9));
        }

        StageJacobianEvaluations += Stages;
    }

    private void AdvanceY(double h, ReadOnlySpan<double> y0, Span<double> y1)
    {
        var a = RK78ButcherTableau.A;
        var b = RK78ButcherTableau.B;
        var yStage = _yStage.AsSpan();

        for (int s = 0; s < Stages; s++)
        {
            y0.CopyTo(yStage);
            var aS = a[s];
            for (int j = 0; j < s; j++)
            {
                if (aS[j] != 0.0)
                {
                    AddScaled(yStage, _ky.AsSpan(j * YLength, YLength), h * aS[j]);
                }
            }

            YDerivative(s, yStage, _ky.AsSpan(s * YLength, YLength));
        }

        y0.CopyTo(y1);
        for (int j = 0; j < Stages; j++)
        {
            if (b[j] != 0.0)
            {
                AddScaled(y1, _ky.AsSpan(j * YLength, YLength), h * b[j]);
            }
        }
    }

    // K = A(x_s) Y + [0 | B ∂a/∂p(x_s)], with A Y = [Y_v ; G Y_r + D Y_v]
    private void YDerivative(int stage, ReadOnlySpan<double> y, Span<double> k)
    {
        int n = ColumnCount;
        var g = _g.AsSpan(stage * 9, 9);
        var d = _d.AsSpan(stage * 9, 9);
        var dadp = _dadp.AsSpan(stage * 3 * ParameterCount, 3 * ParameterCount);

        for (int c = 0; c < n; c++)
        {
            for (int i = 0; i < 3; i++)
            {
                k[i * n + c] = y[(i + 3) * n + c];

                double sum = g[3 * i] * y[c] + g[3 * i + 1] * y[n + c] + g[3 * i + 2] * y[2 * n + c];
                if (_velocityTerms)
                {
                    sum += d[3 * i] * y[3 * n + c] + d[3 * i + 1] * y[4 * n + c] + d[3 * i + 2] * y[5 * n + c];
                }

                if (c >= StateDimension)
                {
                    sum += dadp[3 * (c - StateDimension) + i];
                }

                k[(i + 3) * n + c] = sum;
            }
        }
    }

    private void AdvanceQ(double h, ReadOnlySpan<double> q0, Span<double> q1)
    {
        var a = RK78ButcherTableau.A;
        var b = RK78ButcherTableau.B;
        var qStage = _qStage.AsSpan();

        for (int s = 0; s < Stages; s++)
        {
            q0.CopyTo(qStage);
            var aS = a[s];
            for (int j = 0; j < s; j++)
            {
                if (aS[j] != 0.0)
                {
                    AddScaled(qStage, _kq.AsSpan(j * CovarianceLength, CovarianceLength), h * aS[j]);
                }
            }

            QDerivative(s, qStage, _kq.AsSpan(s * CovarianceLength, CovarianceLength));
        }

        q0.CopyTo(q1);
        for (int j = 0; j < Stages; j++)
        {
            if (b[j] != 0.0)
            {
                AddScaled(q1, _kq.AsSpan(j * CovarianceLength, CovarianceLength), h * b[j]);
            }
        }
    }

    // K^Q = M + Mᵀ + B Qc Bᵀ with M = A(x_s) Q = [Q_v ; G Q_r + D Q_v] (Q_r, Q_v: rows 0–2 and 3–5 of Q)
    private void QDerivative(int stage, ReadOnlySpan<double> q, Span<double> k)
    {
        var full = _qFull.AsSpan();
        for (int i = 0; i < StateDimension; i++)
        {
            for (int j = i; j < StateDimension; j++)
            {
                double value = q[CovarianceIndex(i, j)];
                full[6 * i + j] = value;
                full[6 * j + i] = value;
            }
        }

        var g = _g.AsSpan(stage * 9, 9);
        var d = _d.AsSpan(stage * 9, 9);
        var m = _m.AsSpan();
        for (int c = 0; c < StateDimension; c++)
        {
            for (int i = 0; i < 3; i++)
            {
                m[6 * i + c] = full[6 * (i + 3) + c];

                double sum = g[3 * i] * full[c] + g[3 * i + 1] * full[6 + c] + g[3 * i + 2] * full[12 + c];
                if (_velocityTerms)
                {
                    sum += d[3 * i] * full[18 + c] + d[3 * i + 1] * full[24 + c] + d[3 * i + 2] * full[30 + c];
                }

                m[6 * (i + 3) + c] = sum;
            }
        }

        var qc = _qc.AsSpan(stage * 9, 9);
        for (int i = 0; i < StateDimension; i++)
        {
            for (int j = i; j < StateDimension; j++)
            {
                double value = m[6 * i + j] + m[6 * j + i];
                if (i >= 3)
                {
                    value += qc[3 * (i - 3) + (j - 3)];
                }

                k[CovarianceIndex(i, j)] = value;
            }
        }
    }

    private static void AddScaled(Span<double> target, ReadOnlySpan<double> source, double factor)
    {
        for (int i = 0; i < target.Length; i++)
        {
            target[i] += source[i] * factor;
        }
    }

    private static void ValidateLength(ReadOnlySpan<double> buffer, int expected, string name)
    {
        if (buffer.Length != expected)
        {
            throw new ArgumentException($"{name} must hold {expected} values; it holds {buffer.Length}.", name);
        }
    }
}
