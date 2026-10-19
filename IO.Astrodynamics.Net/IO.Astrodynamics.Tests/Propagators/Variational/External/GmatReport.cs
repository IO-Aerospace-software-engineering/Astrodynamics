// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IO.Astrodynamics.Tests.Propagators.Variational.External;

/// <summary>
/// A report written by a script of <c>tools/gmat_reference/generate_scripts.py</c>, converted to SI and to ICRF: at the
/// start and at every output epoch, the state (m, m/s), and, when the script reports them, Φ and P (m², m²/s, m²/s²).
/// </summary>
/// <remarks>
/// <para>
/// The scripts give the initial state in GMAT's EarthICRF, whose axes are those of SPICE J2000 (the Sun of de440s in
/// EarthICRF equals SPICE to 2e-16, measured), and GMAT integrates and reports in EarthMJ2000Eq, which it turns from
/// ICRF by a rotation of 2.1e-7 rad in 2021. Every report is turned to ICRF by the constant rotation R0 of the start,
/// taken from the triads of the initial state in both frames: x = R0 x_MJ2000, Φ = R0 Φ_MJ2000 R0ᵀ and
/// P = R0 P_MJ2000 R0ᵀ, block by block. GMAT's rotation drifts by about 1e-10 rad over a day (measured); that drift moves
/// only the ephemerides of the third bodies in GMAT's integration frame, while turning the reports at each epoch by R(t)
/// would put it on the whole state (G1: 6 mm and 8e-10 on Φ after one day, against 42 µm and 6e-12 with R0).
/// </para>
/// <para>
/// GMAT writes km and km/s; Φ is the same in km and km/s as in m and m/s (Φ_rv in s, Φ_vr in 1/s); every element of P
/// in km², km²/s and km²/s² is 1e-6 of its value in SI.
/// </para>
/// </remarks>
internal sealed class GmatReport
{
    private GmatReport(double[][] states, double[][] phi, double[][] covariance)
    {
        States = states;
        Phi = phi;
        Covariance = covariance;
    }

    /// <summary>The state at the start and at each output epoch, m and m/s, ICRF.</summary>
    internal double[][] States { get; }

    /// <summary>Φ (row-major, ICRF) at the start and at each output epoch; null when not reported.</summary>
    internal double[][] Phi { get; }

    /// <summary>P (row-major, SI, ICRF) at the start and at each output epoch; null when not reported.</summary>
    internal double[][] Covariance { get; }

    /// <summary>
    /// R0, row-major 3×3, such that x_ICRF = R0 x_MJ2000 at the start of <paramref name="gmatCase"/>: from its initial
    /// state, given to GMAT in EarthICRF, and the first state of its nominal report, in EarthMJ2000Eq.
    /// </summary>
    internal static double[] InitialRotation(GmatCase gmatCase)
    {
        var state = gmatCase.Case.InitialState;
        var icrf = new[]
        {
            state.Position.X, state.Position.Y, state.Position.Z, state.Velocity.X, state.Velocity.Y,
            state.Velocity.Z
        };
        return Rotation(icrf, Rows(gmatCase.Name, "nominal")[0].Select(x => x * 1000.0).ToArray());
    }

    /// <summary>
    /// The report <paramref name="variant"/> of <paramref name="caseName"/>, written as blocks of one state line, then
    /// six lines of Φ when <paramref name="withPhi"/>, then six lines of P when <paramref name="withCovariance"/>, all
    /// in EarthMJ2000Eq, turned to ICRF by <paramref name="rotation"/> (<see cref="InitialRotation"/>).
    /// </summary>
    internal static GmatReport Read(string caseName, string variant, bool withPhi, bool withCovariance,
        double[] rotation)
    {
        var rows = Rows(caseName, variant);
        int block = 1 + (withPhi ? 6 : 0) + (withCovariance ? 6 : 0);
        if (rows.Length % block != 0)
        {
            throw new InvalidDataException($"{caseName}/{variant}: {rows.Length} lines, not whole blocks of {block}");
        }

        int count = rows.Length / block;
        var states = new double[count][];
        var phi = withPhi ? new double[count][] : null;
        var covariance = withCovariance ? new double[count][] : null;
        for (int k = 0; k < count; k++)
        {
            int first = k * block;
            states[k] = State(rows[first], rotation);
            if (withPhi)
            {
                phi![k] = Congruence(Matrix(rows, first + 1, 1.0), rotation);
            }

            if (withCovariance)
            {
                covariance![k] = Congruence(Matrix(rows, first + 1 + (withPhi ? 6 : 0), 1.0e6), rotation);
            }
        }

        return new GmatReport(states, phi, covariance);
    }

    /// <summary>A state line of GMAT (km, km/s, EarthMJ2000Eq) in m and m/s in ICRF.</summary>
    internal static double[] State(double[] row, double[] rotation)
    {
        var state = new double[6];
        for (int i = 0; i < 3; i++)
        {
            for (int k = 0; k < 3; k++)
            {
                state[i] += rotation[3 * i + k] * row[k] * 1000.0;
                state[3 + i] += rotation[3 * i + k] * row[3 + k] * 1000.0;
            }
        }

        return state;
    }

    /// <summary>
    /// R, row-major 3×3, such that x_ICRF = R x_MJ2000: T_ICRF T_MJ2000ᵀ, T the orthonormal triad (r̂, ĥ × r̂, ĥ) of the
    /// same state in each frame.
    /// </summary>
    internal static double[] Rotation(double[] icrf, double[] mj2000)
    {
        var a = Triad(icrf);
        var b = Triad(mj2000);
        var rotation = new double[9];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                for (int k = 0; k < 3; k++)
                {
                    rotation[3 * i + j] += a[3 * i + k] * b[3 * j + k];
                }
            }
        }

        return rotation;
    }

    /// <summary>The numbers of each line of the report <paramref name="variant"/> of <paramref name="caseName"/>, as GMAT
    /// wrote them.</summary>
    internal static double[][] Rows(string caseName, string variant)
    {
        return File.ReadLines(Path.Combine(GmatCasesFileTests.DataDirectory, caseName, variant + ".txt"))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray())
            .ToArray();
    }

    /// <summary>
    /// Φ from GMAT's own central differences of whole propagations at each output epoch, the start excluded: the
    /// reports <c>cd_&lt;k&gt;p</c> and <c>cd_&lt;k&gt;m</c> of the 6 components, perturbed in ICRF by
    /// ±<paramref name="positionStep"/> (m) and ±<paramref name="velocityStep"/> (m/s).
    /// </summary>
    internal static double[][] CentralDifferencePhi(string caseName, double positionStep, double velocityStep,
        double[] rotation)
    {
        string[] components = { "x", "y", "z", "vx", "vy", "vz" };
        double[][] phi = null;
        for (int j = 0; j < 6; j++)
        {
            var plus = Read(caseName, "cd_" + components[j] + "p", false, false, rotation).States;
            var minus = Read(caseName, "cd_" + components[j] + "m", false, false, rotation).States;
            phi ??= Enumerable.Range(0, plus.Length - 1).Select(_ => new double[36]).ToArray();
            double step = j < 3 ? positionStep : velocityStep;
            for (int k = 1; k < plus.Length; k++)
            {
                for (int i = 0; i < 6; i++)
                {
                    phi[k - 1][6 * i + j] = (plus[k][i] - minus[k][i]) / (2.0 * step);
                }
            }
        }

        return phi;
    }

    /// <summary>
    /// Ψ of the coefficient <paramref name="name"/> (<c>cd</c> or <c>cr</c>) from GMAT's own central differences at each
    /// output epoch, the start excluded: the coefficient multiplied by 1 ± <paramref name="relativeStep"/>.
    /// </summary>
    internal static double[][] CentralDifferencePsi(string caseName, string name, double coefficient, double relativeStep,
        double[] rotation)
    {
        var plus = Read(caseName, "cd_" + name + "p", false, false, rotation).States;
        var minus = Read(caseName, "cd_" + name + "m", false, false, rotation).States;
        double step = coefficient * relativeStep;
        return Enumerable.Range(1, plus.Length - 1)
            .Select(k => Enumerable.Range(0, 6).Select(i => (plus[k][i] - minus[k][i]) / (2.0 * step)).ToArray())
            .ToArray();
    }

    // The columns r̂, ĥ × r̂, ĥ, row-major
    private static double[] Triad(double[] state)
    {
        var r = new IO.Astrodynamics.Math.Vector3(state[0], state[1], state[2]);
        var v = new IO.Astrodynamics.Math.Vector3(state[3], state[4], state[5]);
        var e1 = r.Normalize();
        var e3 = r.Cross(v).Normalize();
        var e2 = e3.Cross(e1);
        return new[] { e1.X, e2.X, e3.X, e1.Y, e2.Y, e3.Y, e1.Z, e2.Z, e3.Z };
    }

    // R M Rᵀ, applied to each 3×3 block of the 6×6 M
    private static double[] Congruence(double[] matrix, double[] rotation)
    {
        var result = new double[36];
        for (int bi = 0; bi < 6; bi += 3)
        {
            for (int bj = 0; bj < 6; bj += 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        double sum = 0.0;
                        for (int k = 0; k < 3; k++)
                        {
                            for (int m = 0; m < 3; m++)
                            {
                                sum += rotation[3 * i + k] * matrix[6 * (bi + k) + bj + m] * rotation[3 * j + m];
                            }
                        }

                        result[6 * (bi + i) + bj + j] = sum;
                    }
                }
            }
        }

        return result;
    }

    private static double[] Matrix(double[][] rows, int first, double scale)
    {
        var matrix = new double[36];
        for (int i = 0; i < 6; i++)
        {
            if (rows[first + i].Length != 6)
            {
                throw new InvalidDataException($"A row of a matrix holds {rows[first + i].Length} numbers, not 6");
            }

            for (int j = 0; j < 6; j++)
            {
                matrix[6 * i + j] = rows[first + i][j] * scale;
            }
        }

        return matrix;
    }
}
