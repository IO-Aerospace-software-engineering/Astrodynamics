// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;

namespace IO.Astrodynamics.Body;

/// <summary>
/// Computes gravitational acceleration including spherical harmonic (geopotential) perturbations.
/// <para>
/// <b>Thread safety:</b> This class is <b>not</b> thread-safe. Internal buffers (Legendre tables,
/// trig arrays, and the Cunningham tables of the partials when the state transition matrix is integrated) are
/// reused across calls for performance. Create a separate instance per thread when propagating multiple spacecraft
/// concurrently.
/// </para>
/// </summary>
public class GeopotentialGravitationalField : GravitationalField
{
    /// <summary>
    /// Reader for the geopotential model.
    /// </summary>
    private readonly GeopotentialModelReader _geopotentialModelReader;

    /// <summary>
    /// Maximum degree for the geopotential model.
    /// </summary>
    public int MaxDegree { get; }

    // Pre-allocated buffers for performance
    private readonly double[][] _P;
    private readonly double[][] _dP;
    private readonly double[] _cosMLambda;
    private readonly double[] _sinMLambda;

    // 2D indexed coefficients for O(1) access: _coefficients[n][m]
    private readonly GeopotentialCoefficient[][] _coefficients;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopotentialGravitationalField"/> class.
    /// </summary>
    /// <param name="geopotentialModelFile">The stream reader for the geopotential model file.</param>
    /// <param name="maxDegrees">The maximum degrees for the geopotential model. Default is 70.</param>
    /// <remarks>
    /// The harmonics of this field are evaluated with the GM and the equatorial radius of the body they are evaluated
    /// for; a <see cref="CelestialBody"/> built with <see cref="GeopotentialModelParameters"/> gives its field the
    /// constants of the model instead.
    /// </remarks>
    public GeopotentialGravitationalField(StreamReader geopotentialModelFile, ushort maxDegrees = 70)
        : this(geopotentialModelFile, maxDegrees, null, null, 1.0, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopotentialGravitationalField"/> class whose harmonics are
    /// evaluated with the constants of the model.
    /// </summary>
    /// <param name="geopotentialModelFile">The stream reader for the geopotential model file.</param>
    /// <param name="maxDegrees">The maximum degrees for the geopotential model.</param>
    /// <param name="gravitationalParameter">The GM to which the coefficients are normalized, in m³/s², or null for
    /// that of the body.</param>
    /// <param name="referenceRadius">The reference radius of the coefficients, in m, or null for the equatorial radius
    /// of the body.</param>
    /// <param name="centralGravitationalParameter">The GM of the central term, that of the body (its kernels).</param>
    /// <param name="modelName">The name of the model, for the CDM, or null when it is not known.</param>
    internal GeopotentialGravitationalField(StreamReader geopotentialModelFile, ushort maxDegrees,
        double? gravitationalParameter, double? referenceRadius, double centralGravitationalParameter,
        string modelName)
    {
        GravitationalParameter = gravitationalParameter;
        ReferenceRadius = referenceRadius;
        ModelName = modelName;

        // k = GM_model / GM_kernel, once: exactly 1 without a GM of the model, so that every product of the
        // acceleration is then the one of a field evaluated with the GM of the body
        _harmonicsScale = gravitationalParameter.HasValue
            ? gravitationalParameter.Value / centralGravitationalParameter
            : 1.0;

        MaxDegree = maxDegrees;
        _geopotentialModelReader = new GeopotentialModelReader(geopotentialModelFile);

        // Allocate Legendre buffers
        (_P, _dP) = LegendreFunctions.AllocateLegendreTable(MaxDegree);

        // Allocate trig buffers
        _cosMLambda = new double[MaxDegree + 1];
        _sinMLambda = new double[MaxDegree + 1];

        // Build 2D coefficient array for O(1) indexed access
        _coefficients = new GeopotentialCoefficient[MaxDegree + 1][];
        for (int n = 0; n <= MaxDegree; n++)
        {
            _coefficients[n] = new GeopotentialCoefficient[n + 1];
            for (int m = 0; m <= n; m++)
            {
                try
                {
                    _coefficients[n][m] = _geopotentialModelReader.ReadCoefficient((ushort)n, (ushort)m);
                }
                catch (KeyNotFoundException)
                {
                    // Coefficient not available in model (e.g. n=0,1 absent from EGM2008) — use zeros
                    _coefficients[n][m] = new GeopotentialCoefficient((ushort)n, (ushort)m, 0.0, 0.0, 0.0, 0.0);
                }
            }
        }
    }

    /// <summary>
    /// The GM to which the coefficients are normalized, with which the harmonics are evaluated, in m³/s²; null when
    /// they take the GM of the body. The central term always takes the GM of the body.
    /// </summary>
    public double? GravitationalParameter { get; }

    /// <summary>
    /// The reference radius of the coefficients, with which the harmonics are evaluated, in m; null when they take the
    /// equatorial radius of the body.
    /// </summary>
    public double? ReferenceRadius { get; }

    /// <summary>
    /// The name of the model when the field is evaluated with its known constants (EGM2008 for the Earth by default),
    /// or null; the <c>GRAVITY_MODEL</c> of the CDM.
    /// </summary>
    internal string ModelName { get; }

    // GM_model / GM_kernel, the factor of the harmonics in the acceleration, 1 without a GM of the model
    private readonly double _harmonicsScale;

    /// <summary>
    /// The GM and the reference radius of the harmonics for a body: the constants of the model, or those of the body
    /// when the model has none. Read by the acceleration and its partials alike.
    /// </summary>
    private (double GravitationalParameter, double ReferenceRadius) HarmonicsConstants(CelestialBody observer)
    {
        return (GravitationalParameter ?? observer.GM, ReferenceRadius ?? observer.EquatorialRadius);
    }

    /// <summary>
    /// Backward-compatible property for code that references MaxDegrees as double.
    /// </summary>
    public double MaxDegrees => MaxDegree;

    /// <summary>
    /// Largest degree of the analytic partials.
    /// </summary>
    /// <remarks>
    /// The partials use the unnormalized coefficients and Cunningham functions of the reference, whose range grows
    /// with the degree: at the equatorial radius, V_(N+2,N+2) is about (2N + 3)!!, and the normalization factor of
    /// C_NN about 1 / √((2N)!). At degree 120 they are near 1e238 and 1e-233, so more than ten orders of magnitude
    /// remain on each side of the range of a double for states below the surface and for small coefficients
    /// (<c>GeopotentialPartialsTests.AtTheGuardDegree_TheTablesStayWithinTheirMeasuredBounds</c>). Above it, the
    /// partials throw instead of returning infinities or NaN; the degree of the partials can be reduced below it.
    /// </remarks>
    internal const int MaxPartialsDegree = 120;

    // Tables of the analytic partials, allocated at the first call so that a propagation without them allocates nothing:
    // the Cunningham functions V_nm and W_nm for n up to the degree + 2, row n at index n * _partialsStride, and the
    // unnormalized coefficients
    private int _partialsStride;
    private double[] _v;
    private double[] _w;
    private double[][] _unnormalizedC;
    private double[][] _unnormalizedS;

    /// <summary>
    /// The degree of the partials for a requested degree: the requested one, at most the degree of the field, or the
    /// degree of the field when none is requested.
    /// </summary>
    /// <param name="requestedDegree">The requested degree, or null for the degree of the field.</param>
    /// <returns>The degree of the partials.</returns>
    /// <exception cref="NotSupportedException">The degree of the partials is above <see cref="MaxPartialsDegree"/>.
    /// </exception>
    internal int PartialsDegree(int? requestedDegree)
    {
        int degree = requestedDegree.HasValue ? System.Math.Min(requestedDegree.Value, MaxDegree) : MaxDegree;
        if (degree > MaxPartialsDegree)
        {
            throw new NotSupportedException(
                $"The analytic partials of the geopotential are limited to degree {MaxPartialsDegree}, beyond which " +
                $"their unnormalized terms leave the range of a double; this field has degree {MaxDegree}. Reduce the " +
                "degree of the partials (GeopotentialPartialsDegree).");
        }

        return degree;
    }

    /// <summary>
    /// Adds the analytic ∂a/∂r of <see cref="ComputeGravitationalAcceleration"/> to <paramref name="dadr"/>: the point
    /// mass, plus the harmonics of degree 2 to <see cref="PartialsDegree"/> by the Cunningham recursion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The harmonics are differentiated in the body-fixed frame, as the acceleration, with the Cunningham functions V_nm
    /// and W_nm extended to degree n + 2: O. Montenbruck, E. Gill, Satellite Orbits, Springer (2000), chapter 3
    /// (recursions of V_nm and W_nm, acceleration) and chapter 7 (partials of the acceleration); confirmed by S.
    /// Guillet (2026-10-12). The block is brought back to the frame of the state by Rᵀ H R, with R the
    /// rotation of the acceleration (<see cref="Frames.Frame.RotationTo"/>, the frame of the state to the body-fixed
    /// frame, at the epoch of the state). It is symmetric by construction: the upper triangle is computed and mirrored.
    /// </para>
    /// <para>
    /// The tables are kept by the instance and reused, and the rotation allocates nothing in the propagation's case (the
    /// orientation cache active on the thread), so this method allocates nothing there; it is not thread-safe, as the
    /// acceleration.
    /// </para>
    /// </remarks>
    /// <param name="stateVector">The state, relative to the attracting body.</param>
    /// <param name="dadr">3×3 row-major block the partials are added to, in 1/s², in the frame of the state.</param>
    /// <param name="partialsDegree">The requested degree of the harmonics, or null for the degree of the field; see
    /// <see cref="PartialsDegree"/>.</param>
    /// <returns>True.</returns>
    /// <exception cref="NotSupportedException">The degree of the partials is above <see cref="MaxPartialsDegree"/>.
    /// </exception>
    internal override bool TryAccumulatePositionPartials(StateVector stateVector, Span<double> dadr,
        int? partialsDegree = null)
    {
        int degree = PartialsDegree(partialsDegree);
        var observer = (CelestialBody)stateVector.Observer;
        var position = stateVector.Position;

        // R maps the frame of the state to the body-fixed frame, as in ComputeGravitationalAcceleration; its columns are
        // the images of the unit vectors
        Vector3 column0 = Vector3.VectorX, column1 = Vector3.VectorY, column2 = Vector3.VectorZ;
        var fixedPosition = position;
        if (stateVector.Frame != observer.Frame)
        {
            var rotation = stateVector.Frame.RotationTo(observer.Frame, stateVector.Epoch);
            column0 = column0.Rotate(rotation);
            column1 = column1.Rotate(rotation);
            column2 = column2.Rotate(rotation);
            fixedPosition = position.Rotate(rotation);
        }

        Span<double> harmonics = stackalloc double[6];
        var (harmonicsGm, referenceRadius) = HarmonicsConstants(observer);
        HarmonicPartials(fixedPosition, harmonicsGm, referenceRadius, degree, harmonics);
        AddRotatedBack(harmonics, column0, column1, column2, dadr);
        PointMassPartials.Accumulate(position, observer.GM, dadr);
        return true;
    }

    /// <summary>
    /// The second derivatives of the harmonic potential of degree 2 to <paramref name="degree"/> at a body-fixed
    /// position: U_xx, U_yy, U_zz, U_xy, U_xz, U_yz, in 1/s².
    /// </summary>
    /// <remarks>
    /// <para>
    /// With E_nm = V_nm + i W_nm and Montenbruck and Gill's potential U = (GM / R) Σ (C_nm V_nm + S_nm W_nm), the
    /// derivatives of the Cunningham functions are linear combinations of the functions of degree n + 1
    /// (Montenbruck and Gill, chapter 3, the relations behind their acceleration of the geopotential):
    /// (∂x + i ∂y) E_nm = −E_(n+1,m+1) / R, (∂x − i ∂y) E_nm = (n − m + 2)(n − m + 1) E_(n+1,m−1) / R and
    /// ∂z E_nm = −(n − m + 1) E_(n+1,m) / R. Applied twice, they give the second derivatives from the functions of
    /// degree n + 2 and orders m − 2 to m + 2, the form of Montenbruck and Gill, chapter 7. For m = 0 and m = 1, the
    /// orders below zero come from E_n0 being real: (∂x − i ∂y) E_n0 is the conjugate of (∂x + i ∂y) E_n0.
    /// Compared term by term, without copying code, with CNES PATRIUS 4.16 (Apache 2.0),
    /// <c>GravityToolbox.computeDAccDPos</c> and its cases m = 0, m = 1 and m ≥ 2: the same combinations.
    /// </para>
    /// <para>
    /// Laplace's equation, U_xx + U_yy + U_zz = 0, holds term by term; the tests check it.
    /// </para>
    /// </remarks>
    /// <param name="position">Body-fixed position, in m.</param>
    /// <param name="gm">Gravitational parameter of the body, in m³/s².</param>
    /// <param name="equatorialRadius">Reference radius of the coefficients, in m.</param>
    /// <param name="degree">Highest degree of the harmonics.</param>
    /// <param name="partials">The six second derivatives, in 1/s².</param>
    private void HarmonicPartials(in Vector3 position, double gm, double equatorialRadius, int degree,
        Span<double> partials)
    {
        EnsurePartialsTables();
        CunninghamFunctions(position, equatorialRadius, degree + 2);

        int stride = _partialsStride;
        double xx = 0.0, yy = 0.0, zz = 0.0, xy = 0.0, xz = 0.0, yz = 0.0;

        // From the highest degree down, so that the small terms are summed first
        for (int n = degree; n >= 2; n--)
        {
            int row = (n + 2) * stride;
            var cRow = _unnormalizedC[n];
            var sRow = _unnormalizedS[n];
            for (int m = 0; m <= n; m++)
            {
                double c = cRow[m];
                double s = sRow[m];
                if (c == 0.0 && s == 0.0)
                {
                    continue;
                }

                double k1 = n - m + 1;
                double k2 = (n - m + 2) * k1;

                // A = E_(n+2,m+2), B = E_(n+2,m), F = E_(n+2,m+1)
                double aRe = _v[row + m + 2], aIm = _w[row + m + 2];
                double bRe = _v[row + m], bIm = _w[row + m];
                double fRe = k1 * _v[row + m + 1], fIm = k1 * _w[row + m + 1];

                // (∂x − i ∂y)² E_nm and ∂z (∂x − i ∂y) E_nm
                double dRe, dIm, gRe, gIm;
                if (m >= 2)
                {
                    double k4 = (n - m + 4) * (n - m + 3) * k2;
                    dRe = k4 * _v[row + m - 2];
                    dIm = k4 * _w[row + m - 2];
                    double k3 = -(n - m + 3) * k2;
                    gRe = k3 * _v[row + m - 1];
                    gIm = k3 * _w[row + m - 1];
                }
                else if (m == 1)
                {
                    // (∂x − i ∂y) E_n1 = (n + 1) n E_(n+1,0), real, so (∂x − i ∂y)² E_n1 = −(n + 1) n E_(n+2,1)*
                    double k = -(n + 1.0) * n;
                    dRe = k * _v[row + 1];
                    dIm = -k * _w[row + 1];
                    double k3 = -(n + 2.0) * k2;
                    gRe = k3 * _v[row];
                    gIm = k3 * _w[row];
                }
                else
                {
                    // E_n0 is real: the conjugates of (∂x + i ∂y)² E_n0 and ∂z (∂x + i ∂y) E_n0
                    dRe = aRe;
                    dIm = -aIm;
                    gRe = fRe;
                    gIm = -fIm;
                }

                // Re[(C − i S) X] and Re[(C − i S) X / i]
                double aC = c * aRe + s * aIm, aI = c * aIm - s * aRe;
                double bC = c * bRe + s * bIm;
                double dC = c * dRe + s * dIm, dI = c * dIm - s * dRe;
                double fC = c * fRe + s * fIm, fI = c * fIm - s * fRe;
                double gC = c * gRe + s * gIm, gI = c * gIm - s * gRe;

                // ∂x² = (∂₊² + 2 ∂₊∂₋ + ∂₋²) / 4 with ∂₊∂₋ E_nm = −(n − m + 2)(n − m + 1) E_(n+2,m), and so on
                xx += 0.25 * (aC - 2.0 * k2 * bC + dC);
                yy -= 0.25 * (aC + 2.0 * k2 * bC + dC);
                zz += k2 * bC;
                xy += 0.25 * (aI - dI);
                xz += 0.5 * (fC + gC);
                yz += 0.5 * (fI - gI);
            }
        }

        double scale = gm / (equatorialRadius * equatorialRadius * equatorialRadius);
        partials[0] = scale * xx;
        partials[1] = scale * yy;
        partials[2] = scale * zz;
        partials[3] = scale * xy;
        partials[4] = scale * xz;
        partials[5] = scale * yz;
    }

    /// <summary>
    /// The Cunningham functions V_nm and W_nm, for 0 ≤ m ≤ n ≤ <paramref name="highestDegree"/>, at a body-fixed
    /// position, into the tables.
    /// </summary>
    /// <remarks>
    /// Montenbruck and Gill, chapter 3: V_00 = R / r, W_00 = 0;
    /// V_mm = (2m − 1) (x R / r² V_(m−1,m−1) − y R / r² W_(m−1,m−1)),
    /// W_mm = (2m − 1) (x R / r² W_(m−1,m−1) + y R / r² V_(m−1,m−1));
    /// V_nm = ((2n − 1) z R / r² V_(n−1,m) − (n + m − 1) R² / r² V_(n−2,m)) / (n − m), and the same for W, with
    /// V_(m−1,m) = W_(m−1,m) = 0. Confirmed by S. Guillet (2026-10-12).
    /// </remarks>
    private void CunninghamFunctions(in Vector3 position, double equatorialRadius, int highestDegree)
    {
        int stride = _partialsStride;
        double r2 = position.X * position.X + position.Y * position.Y + position.Z * position.Z;
        double x0 = equatorialRadius * position.X / r2;
        double y0 = equatorialRadius * position.Y / r2;
        double z0 = equatorialRadius * position.Z / r2;
        double rho = equatorialRadius * equatorialRadius / r2;

        _v[0] = equatorialRadius / System.Math.Sqrt(r2);
        _w[0] = 0.0;
        for (int m = 0; m <= highestDegree; m++)
        {
            int diagonal = m * stride + m;
            if (m > 0)
            {
                int previous = (m - 1) * stride + m - 1;
                double factor = 2.0 * m - 1.0;
                _v[diagonal] = factor * (x0 * _v[previous] - y0 * _w[previous]);
                _w[diagonal] = factor * (x0 * _w[previous] + y0 * _v[previous]);
            }

            if (m + 1 <= highestDegree)
            {
                double factor = (2.0 * m + 1.0) * z0;
                _v[diagonal + stride] = factor * _v[diagonal];
                _w[diagonal + stride] = factor * _w[diagonal];
            }

            for (int n = m + 2; n <= highestDegree; n++)
            {
                int index = n * stride + m;
                double a = (2.0 * n - 1.0) * z0;
                double b = (n + m - 1.0) * rho;
                double inverse = 1.0 / (n - m);
                _v[index] = (a * _v[index - stride] - b * _v[index - 2 * stride]) * inverse;
                _w[index] = (a * _w[index - stride] - b * _w[index - 2 * stride]) * inverse;
            }
        }
    }

    /// <summary>
    /// Allocates the tables of the partials and the unnormalized coefficients, at the first call.
    /// </summary>
    /// <remarks>
    /// C_nm = N_nm C̄_nm and S_nm = N_nm S̄_nm, with the geodesy normalization of the field,
    /// N_nm = √((2 − δ_0m)(2n + 1)(n − m)! / (n + m)!), the convention of <see cref="LegendreFunctions"/>. The
    /// factorial ratio is a running product, square-rooted before it could overflow.
    /// </remarks>
    private void EnsurePartialsTables()
    {
        if (_v != null)
        {
            return;
        }

        int degree = System.Math.Min(MaxDegree, MaxPartialsDegree);
        _partialsStride = degree + 3;
        _v = new double[_partialsStride * _partialsStride];
        _w = new double[_partialsStride * _partialsStride];
        _unnormalizedC = new double[degree + 1][];
        _unnormalizedS = new double[degree + 1][];
        for (int n = 0; n <= degree; n++)
        {
            _unnormalizedC[n] = new double[n + 1];
            _unnormalizedS[n] = new double[n + 1];
            for (int m = 0; m <= n; m++)
            {
                double factor = NormalizationFactor(n, m);
                _unnormalizedC[n][m] = factor * _coefficients[n][m].C;
                _unnormalizedS[n][m] = factor * _coefficients[n][m].S;
            }
        }
    }

    /// <summary>
    /// The largest |V_nm| or |W_nm| of the last partials, for the measurement of the guard degree.
    /// </summary>
    internal double LargestCunninghamFunction()
    {
        double largest = 0.0;
        for (int k = 0; k < _v.Length; k++)
        {
            largest = System.Math.Max(largest, System.Math.Max(System.Math.Abs(_v[k]), System.Math.Abs(_w[k])));
        }

        return largest;
    }

    /// <summary>
    /// N_nm = √((2 − δ_0m)(2n + 1)(n − m)! / (n + m)!), the factor from the normalized to the unnormalized coefficients.
    /// </summary>
    internal static double NormalizationFactor(int n, int m)
    {
        double factor = System.Math.Sqrt((m == 0 ? 1.0 : 2.0) * (2 * n + 1));
        double product = 1.0;
        for (int k = n - m + 1; k <= n + m; k++)
        {
            product *= k;
            if (product > 1e250)
            {
                factor /= System.Math.Sqrt(product);
                product = 1.0;
            }
        }

        return factor / System.Math.Sqrt(product);
    }

    /// <summary>
    /// Adds Rᵀ H R to <paramref name="dadr"/>, for the symmetric H of <paramref name="harmonics"/> (xx, yy, zz, xy, xz,
    /// yz) and the rotation R of columns <paramref name="c0"/>, <paramref name="c1"/>, <paramref name="c2"/>; the upper
    /// triangle is computed and mirrored, so the block added is exactly symmetric.
    /// </summary>
    private static void AddRotatedBack(ReadOnlySpan<double> harmonics, in Vector3 c0, in Vector3 c1, in Vector3 c2,
        Span<double> dadr)
    {
        // (Rᵀ H R)_ab = c_aᵀ H c_b
        var h0 = new Vector3(harmonics[0], harmonics[3], harmonics[4]);
        var h1 = new Vector3(harmonics[3], harmonics[1], harmonics[5]);
        var h2 = new Vector3(harmonics[4], harmonics[5], harmonics[2]);
        var hc0 = new Vector3(h0 * c0, h1 * c0, h2 * c0);
        var hc1 = new Vector3(h0 * c1, h1 * c1, h2 * c1);
        var hc2 = new Vector3(h0 * c2, h1 * c2, h2 * c2);

        double e00 = c0 * hc0, e01 = c0 * hc1, e02 = c0 * hc2;
        double e11 = c1 * hc1, e12 = c1 * hc2, e22 = c2 * hc2;
        dadr[0] += e00;
        dadr[1] += e01;
        dadr[2] += e02;
        dadr[3] += e01;
        dadr[4] += e11;
        dadr[5] += e12;
        dadr[6] += e02;
        dadr[7] += e12;
        dadr[8] += e22;
    }

    /// <summary>
    /// Computes the full 3D gravitational acceleration including geopotential harmonics
    /// using the Montenbruck &amp; Gill spherical coordinate gradient formulation.
    /// </summary>
    /// <param name="stateVector">The state vector for which to compute the gravitational acceleration.</param>
    /// <returns>The gravitational acceleration as a <see cref="Vector3"/>.</returns>
    public override Vector3 ComputeGravitationalAcceleration(StateVector stateVector)
    {
        var observer = (CelestialBody)stateVector.Observer;
        var fixedFrame = observer.Frame;

        // The position in the body-fixed frame, as OrbitalParameters.ToFrame gives it (the same rotation, and the
        // position itself when the frames are the same), without allocating: see Frame.RotationTo
        var position = stateVector.Frame == fixedFrame
            ? stateVector.Position
            : stateVector.Position.Rotate(stateVector.Frame.RotationTo(fixedFrame, stateVector.Epoch));

        double x = position.X;
        double y = position.Y;
        double z = position.Z;
        double r = position.Magnitude();

        // Geocentric latitude φ and longitude λ (fixes Bug 4: no Math.Abs)
        double rhoXY = System.Math.Sqrt(x * x + y * y);
        double sinPhi = z / r;
        double cosPhi = rhoXY / r;
        double lambda = System.Math.Atan2(y, x);

        // The central term with the GM of the body; the harmonics with the constants of the model, their GM as
        // k GM, k = GM_model / GM_body (Pavlis et al., 2012, for EGM2008)
        double GM = observer.GM;
        double Re = HarmonicsConstants(observer).ReferenceRadius;
        double k = _harmonicsScale;

        // Compute Legendre table with derivatives
        LegendreFunctions.ComputeGeodesyNormalizedLegendreTable(MaxDegree, sinPhi, cosPhi, _P, _dP);

        // Pre-compute cos(m·λ) and sin(m·λ) via trig recurrence
        double cosLambda = System.Math.Cos(lambda);
        double sinLambda = System.Math.Sin(lambda);
        _cosMLambda[0] = 1.0;
        _sinMLambda[0] = 0.0;
        if (MaxDegree >= 1)
        {
            _cosMLambda[1] = cosLambda;
            _sinMLambda[1] = sinLambda;
        }

        for (int m = 2; m <= MaxDegree; m++)
        {
            _cosMLambda[m] = 2.0 * cosLambda * _cosMLambda[m - 1] - _cosMLambda[m - 2];
            _sinMLambda[m] = 2.0 * cosLambda * _sinMLambda[m - 1] - _sinMLambda[m - 2];
        }

        // Sum the three gradient components:
        //   dV/dr   (radial) — includes (n+1) factor
        //   dV/dφ   (latitude)
        //   dV/dλ   (longitude)
        double dVdr = 0.0;
        double dVdPhi = 0.0;
        double dVdLambda = 0.0;

        double reOverR = Re / r;
        double reOverRn = reOverR; // Will be (Re/r)^n for n starting at 1; we init for n=2 below

        for (int n = 2; n <= MaxDegree; n++)
        {
            reOverRn = System.Math.Pow(reOverR, n);

            for (int m = 0; m <= n; m++)
            {
                var coeff = _coefficients[n][m];
                double Cnm = coeff.C;
                double Snm = coeff.S;

                double cosmL = _cosMLambda[m];
                double sinmL = _sinMLambda[m];

                double CcosSplussin = Cnm * cosmL + Snm * sinmL;
                double ScosmCsin = Snm * cosmL - Cnm * sinmL;

                double Pnm = _P[n][m];
                double dPnm = _dP[n][m];

                // Radial: -(n+1) * (Re/r)^n * P̄_nm * (C̄·cos(mλ) + S̄·sin(mλ))
                dVdr += (n + 1) * reOverRn * Pnm * CcosSplussin;

                // Latitude: (Re/r)^n * dP̄_nm/dφ * (C̄·cos(mλ) + S̄·sin(mλ))
                dVdPhi += reOverRn * dPnm * CcosSplussin;

                // Longitude: m * (Re/r)^n * P̄_nm * (S̄·cos(mλ) - C̄·sin(mλ))
                dVdLambda += m * reOverRn * Pnm * ScosmCsin;
            }
        }

        // Convert to acceleration in spherical coordinates:
        //   a_r = -(GM/r²) * [1 + k dVdr_sum]  (central body + harmonics)
        //   a_φ = (GM/r²) * k dVdPhi_sum
        //   a_λ = (GM/r²) * k dVdLambda_sum / cosφ
        double GMoverR2 = GM / (r * r);
        double ar = -GMoverR2 * (1.0 + k * dVdr);
        double aPhi = GMoverR2 * (k * dVdPhi);

        const double cosPhiEpsilon = 1.0e-15;
        double aLambda = System.Math.Abs(cosPhi) > cosPhiEpsilon
            ? GMoverR2 * (k * dVdLambda) / cosPhi
            : 0.0;

        // Transform spherical (r, φ, λ) → body-fixed Cartesian (x, y, z)
        double cosL = cosLambda;
        double sinL = sinLambda;

        double ax = ar * cosPhi * cosL - aPhi * sinPhi * cosL - aLambda * sinL;
        double ay = ar * cosPhi * sinL - aPhi * sinPhi * sinL + aLambda * cosL;
        double az = ar * sinPhi + aPhi * cosPhi;

        var bodyFixedAcceleration = new Vector3(ax, ay, az);

        // Rotate from body-fixed to original inertial frame
        return bodyFixedAcceleration.Rotate(fixedFrame.RotationTo(stateVector.Frame, stateVector.Epoch));
    }
}
