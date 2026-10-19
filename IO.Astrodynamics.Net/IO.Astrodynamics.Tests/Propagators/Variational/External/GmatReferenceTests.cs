// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Propagators.Variational.External;

/// <summary>
/// H2: Φ, Ψ, P and Q of the library against GMAT R2026a, a third-party implementation, on the cases G1 to G6
/// (<see cref="GmatCases"/>), with the model departures kept apart from the computation departures (design of #408).
/// </summary>
/// <remarks>
/// <para>
/// The GMAT outputs are committed in <c>Data/GmatReference</c>, produced by <c>tools/gmat_reference/run_gmat.sh</c>
/// from the scripts that <c>generate_scripts.py</c> writes from <c>cases.json</c>; GMAT is not needed to run the tests.
/// </para>
/// <para>
/// For Φ, at every output epoch (every hour and at the end of the arc), per 3×3 block, relative Frobenius norms:
/// </para>
/// <list type="bullet">
/// <item><description><b>total</b>: Φ of the library against Φ of GMAT (<c>Sat.OrbitSTM</c>);</description></item>
/// <item><description><b>models</b>: the central differences of whole propagations of the library against those of
/// GMAT, with the same steps: no variational equation enters, the departure is that of the models, to the resolution of
/// the two references;</description></item>
/// <item><description><b>GMAT's computation</b>: Φ of GMAT against GMAT's own central differences;</description></item>
/// <item><description><b>the library's computation</b>: Φ against the library's central differences (F1).</description></item>
/// </list>
/// </remarks>
[Trait("Category", "Validation")]
public class GmatReferenceTests : IClassFixture<ReferenceCases>
{
    private const int NaifId = -1984;

    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public GmatReferenceTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    // Bounds of Φ per case: total, models, GMAT's computation, the library's computation, and the state departure in
    // position (m) and velocity (m/s). G1: the threshold of the design (1e-10); the library's computation: the F1
    // thresholds (1e-7 for R1, 1e-4 otherwise); every other bound a record at twice the measurement of 2026-10-19
    public static TheoryData<string, double, double, double, double, double, double> PhiCases => new()
    {
        { "G1", 1e-10, 2e-8, 4e-8, 1e-7, 2e-4, 2e-7 },
        { "G2", 2e-8, 3e-8, 4e-8, 1e-4, 0.2, 2e-4 },
        { "G3", 1e-11, 4e-8, 3e-8, 1e-4, 2e-4, 6e-9 },
        { "G4", 8e-8, 2e-7, 2e-7, 1e-4, 0.6, 4e-4 },
        { "G5", 3e-4, 4e-7, 3e-4, 1e-4, 2e-2, 2e-5 },
        { "G6", 2e-3, 2e-3, 2e-4, 1e-4, 8e3, 9.0 }
    };

    [Theory]
    [MemberData(nameof(PhiCases))]
    public void Phi_AgainstGmat_ModelsAndComputationApart(string name, double totalBound, double modelsBound,
        double gmatBound, double ioBound, double positionBound, double velocityBound)
    {
        // Arrange
        var gmatCase = GmatCases.Get(_cases, name);
        var referenceCase = gmatCase.Case;
        var start = referenceCase.InitialState;
        var epochs = gmatCase.Offsets.Select(t => start.Epoch.AddSeconds(t)).ToArray();
        var rotation = GmatReport.InitialRotation(gmatCase);
        var gmat = GmatReport.Read(name, "nominal", true, false, rotation);
        var gmatDifferences = GmatReport.CentralDifferencePhi(name, referenceCase.PositionStep, referenceCase.VelocityStep,
            rotation);

        // Act
        var solution = referenceCase.Propagate(NaifId, ReferenceCases.Tolerance,
            new VariationalOptions(referenceCase.Parameters));
        var phi = epochs.Select(t => StmMeasures.Phi(StmMeasures.Y(solution, t))).ToArray();
        var states = epochs.Select(t => WholePropagationDifferences.State(solution, t)).ToArray();
        var differences = WholePropagationDifferences.Phi(
            s => referenceCase.PropagateReference(NaifId - 1, s, ReferenceCases.DragCoefficient,
                ReferenceCases.ReflectivityCoefficient, ReferenceCases.ReferenceTolerance),
            start, referenceCase.PositionStep, referenceCase.VelocityStep, epochs);

        // Assert
        Assert.Equal(epochs.Length + 1, gmat.States.Length);
        var total = new double[4];
        var models = new double[4];
        var gmatComputation = new double[4];
        var ioComputation = new double[4];
        double position = 0.0, velocity = 0.0;
        for (int k = 0; k < epochs.Length; k++)
        {
            Max(total, StmMeasures.BlockErrors(phi[k], gmat.Phi[k + 1]));
            Max(models, StmMeasures.BlockErrors(differences[k], gmatDifferences[k]));
            Max(gmatComputation, StmMeasures.BlockErrors(gmat.Phi[k + 1], gmatDifferences[k]));
            Max(ioComputation, StmMeasures.BlockErrors(phi[k], differences[k]));
            position = System.Math.Max(position, Distance(states[k], gmat.States[k + 1], 0));
            velocity = System.Math.Max(velocity, Distance(states[k], gmat.States[k + 1], 3));
        }

        _output.WriteLine($"{name}: worst over {epochs.Length} epochs, blocks rr, rv, vr, vv");
        _output.WriteLine($"{name} total, library against GMAT: {Format(total)}");
        _output.WriteLine($"{name} models, central differences of the library against GMAT's: {Format(models)}");
        _output.WriteLine($"{name} GMAT's computation, its Φ against its central differences: {Format(gmatComputation)}");
        _output.WriteLine($"{name} library's computation, its Φ against its central differences: {Format(ioComputation)}");
        _output.WriteLine($"{name} state departure: position {position:E2} m, velocity {velocity:E2} m/s");
        Assert.True(total.Max() < totalBound, $"{name} total {total.Max():E2}");
        Assert.True(models.Max() < modelsBound, $"{name} models {models.Max():E2}");
        Assert.True(gmatComputation.Max() < gmatBound, $"{name} GMAT's computation {gmatComputation.Max():E2}");
        Assert.True(ioComputation.Max() < ioBound, $"{name} library's computation {ioComputation.Max():E2}");
        Assert.True(position < positionBound, $"{name} position {position:E2} m");
        Assert.True(velocity < velocityBound, $"{name} velocity {velocity:E2} m/s");
    }

    [Theory]
    [MemberData(nameof(PhiCases))]
    public void Phi_AtTheFixedStepOfGmat_IsSettled(string name, double totalBound, double modelsBound,
        double gmatBound, double ioBound, double positionBound, double velocityBound)
    {
        // Arrange
        var gmatCase = GmatCases.Get(_cases, name);
        var rotation = GmatReport.InitialRotation(gmatCase);

        // Act
        var full = GmatReport.Read(name, "nominal", true, false, rotation);
        var half = GmatReport.Read(name, "nominal_half", true, false, rotation);

        // Assert: GMAT's Φ at its fixed step against half the step, worst block over the arc, well below the total
        // departure from the library, which it must not explain (a tenth of its bound)
        double worst = 0.0, position = 0.0;
        for (int k = 1; k < full.Phi.Length; k++)
        {
            worst = System.Math.Max(worst, StmMeasures.WorstBlockError(full.Phi[k], half.Phi[k]));
            position = System.Math.Max(position, Distance(full.States[k], half.States[k], 0));
        }

        _output.WriteLine($"{name} GMAT at {gmatCase.FixedStep} s against {gmatCase.FixedStep / 2.0} s: Φ {worst:E2}, " +
                          $"position {position:E2} m");
        Assert.True(worst < 0.1 * totalBound, $"{name} Φ at the step against half of it: {worst:E2}");
        Assert.True(position < 0.1 * positionBound, $"{name} position at the step against half of it: {position:E2} m");
    }

    // Bounds of Ψ over the arc relative to its largest value: against GMAT's central differences, the central
    // differences of the library against GMAT's (records at twice the measurement), and the library against its own
    // (F1, 1e-4)
    public static TheoryData<string, double, double, double> PsiCases => new()
    {
        { "G5", 2e-5, 2e-5, 1e-4 },
        { "G6", 3e-2, 3e-2, 1e-4 }
    };

    [Theory]
    [MemberData(nameof(PsiCases))]
    public void Psi_AgainstGmatCentralDifferences(string name, double totalBound, double modelsBound, double ioBound)
    {
        // Arrange
        var gmatCase = GmatCases.Get(_cases, name);
        var referenceCase = gmatCase.Case;
        var start = referenceCase.InitialState;
        var epochs = gmatCase.Offsets.Select(t => start.Epoch.AddSeconds(t)).ToArray();
        bool drag = referenceCase.AtmosphericDrag;
        double coefficient = drag ? ReferenceCases.DragCoefficient : ReferenceCases.ReflectivityCoefficient;
        double relativeStep = drag ? referenceCase.DragCoefficientStep : referenceCase.ReflectivityCoefficientStep;
        var gmatPsi = GmatReport.CentralDifferencePsi(name, drag ? "cd" : "cr", coefficient, relativeStep,
            GmatReport.InitialRotation(gmatCase));

        // Act: Ψ_Cr held with IncludeInStepControl, as in F1
        var solution = referenceCase.Propagate(NaifId, ReferenceCases.Tolerance,
            new VariationalOptions(referenceCase.Parameters, IncludeInStepControl: !drag));
        var psi = epochs.Select(t => StmMeasures.Column(StmMeasures.Y(solution, t), 6)).ToArray();
        var differences = WholePropagationDifferences.Psi(
            v => referenceCase.PropagateReference(NaifId - 1, start, drag ? v : ReferenceCases.DragCoefficient,
                drag ? ReferenceCases.ReflectivityCoefficient : v, ReferenceCases.ReferenceTolerance),
            coefficient, coefficient * relativeStep, epochs);

        // Assert
        var total = WholePropagationDifferences.PsiArcError(psi, gmatPsi);
        var models = WholePropagationDifferences.PsiArcError(differences, gmatPsi);
        var io = WholePropagationDifferences.PsiArcError(psi, differences);
        string label = drag ? "Ψ_Cd" : "Ψ_Cr";
        _output.WriteLine($"{name} {label} over the arc relative to its largest value (position, velocity):");
        _output.WriteLine($"{name} library against GMAT's central differences: {total.Position:E2}, {total.Velocity:E2}");
        _output.WriteLine($"{name} library's central differences against GMAT's: {models.Position:E2}, {models.Velocity:E2}");
        _output.WriteLine($"{name} library against its central differences: {io.Position:E2}, {io.Velocity:E2}");
        Assert.True(System.Math.Max(total.Position, total.Velocity) < totalBound, $"{name} {label} total");
        Assert.True(System.Math.Max(models.Position, models.Velocity) < modelsBound, $"{name} {label} models");
        Assert.True(System.Math.Max(io.Position, io.Velocity) < ioBound, $"{name} {label} library");
    }

    [Fact]
    public void Density_AlongTheDragCase_AgainstGmat()
    {
        // Arrange: GMAT's states and densities (kg/km³) every hour along G6
        var gmatCase = GmatCases.Get(_cases, "G6");
        var referenceCase = gmatCase.Case;
        var start = referenceCase.InitialState;
        var earth = (IO.Astrodynamics.Body.CelestialBody)start.Observer;
        var drag = new AtmosphericDrag(ReferenceCase.NewSpacecraft(NaifId - 3, start), earth);
        var rows = GmatReport.Rows("G6", "density");
        var rotation = GmatReport.InitialRotation(gmatCase);

        // Act: the density of the library at the same states
        double worst = 0.0, mean = 0.0;
        for (int k = 0; k < rows.Length; k++)
        {
            var epoch = start.Epoch.AddSeconds(k == rows.Length - 1 ? referenceCase.Duration.TotalSeconds
                : k * GmatCase.OutputInterval);
            var s = GmatReport.State(rows[k], rotation);
            var state = new IO.Astrodynamics.OrbitalParameters.StateVector(
                new IO.Astrodynamics.Math.Vector3(s[0], s[1], s[2]), new IO.Astrodynamics.Math.Vector3(s[3], s[4], s[5]),
                earth, epoch, start.Frame);
            double density = drag.Density(state.Position, drag.BodyFixedRotation(state), epoch);
            double gmatDensity = rows[k][6] * 1e-9;
            double ratio = density / gmatDensity - 1.0;
            worst = System.Math.Max(worst, System.Math.Abs(ratio));
            mean += ratio / rows.Length;
            _output.WriteLine($"G6 {k} h: library {density:E4} kg/m³, GMAT {gmatDensity:E4} kg/m³, ratio − 1 {ratio:E2}");
        }

        _output.WriteLine($"G6 density, library against GMAT: worst {worst:E2}, mean {mean:E2}");

        // Assert: a record at twice the measurement; above the gate of the design (1e-3), so drag is compared at the
        // B7 states with the density ratio taken out (decision 6)
        Assert.True(worst < 6e-2, $"G6 density {worst:E2}");
    }

    // Bounds of P (relative Frobenius, on the correlation matrix) and of Q (the first-order limit of GMAT's discrete
    // noise against the library, relative Frobenius), at twice the measurement of 2026-10-19; GMAT's P resolves no
    // better than its drift from its own Φ P0 Φᵀ, so the 1e-10 of the design does not hold for P (pending decision)
    public static TheoryData<string, double, double, double> CovarianceCases => new()
    {
        { "G1", 1e-8, 1e-8, 3e-8 },
        { "G2", 5e-8, 3e-6, 5e-8 }
    };

    [Theory]
    [MemberData(nameof(CovarianceCases))]
    public void CovarianceAndProcessNoise_AgainstGmat(string name, double pBound, double pCorrelationBound,
        double qBound)
    {
        // Arrange
        var gmatCase = GmatCases.Get(_cases, name);
        var referenceCase = gmatCase.Case;
        var rotation = GmatReport.InitialRotation(gmatCase);
        var gmat = GmatReport.Read(name, "cov", true, true, rotation);
        // GMAT takes P0 in MJ2000Eq axes only: the scripts give it the P0 of cases.json there, so the library starts
        // from the same P0 turned to ICRF, R0 P0 R0ᵀ
        var start = WithRotatedCovariance(GmatCases.WithCovariance(referenceCase.InitialState), rotation);
        var epochs = gmatCase.Offsets.Select(t => start.Epoch.AddSeconds(t)).ToArray();

        // Act
        var solution = Propagate(referenceCase, start, null);
        // GMAT rejects a zero sigma: R and N at 1e-13 km/s^1.5, a density of 1e-20 m²/s³, the same here
        var noisy = Propagate(referenceCase, start, new RtnProcessNoise(1e-20, GmatCases.TangentialNoise, 1e-20));

        // Assert: P
        double pRelative = 0.0, pCorrelation = 0.0, drift = 0.0;
        var p0 = Elements(start.Covariance!.Value);
        for (int k = 0; k < epochs.Length; k++)
        {
            var p = Elements(solution.GetCovariance(epochs[k]));
            pRelative = System.Math.Max(pRelative, RelativeFrobenius(p, gmat.Covariance[k + 1]));
            pCorrelation = System.Math.Max(pCorrelation, CorrelationError(p, gmat.Covariance[k + 1]));
            // GMAT's computation: its P against its own Φ P0 Φᵀ, which it propagates step by step
            drift = System.Math.Max(drift, CorrelationError(gmat.Covariance[k + 1], Propagated(gmat.Phi[k + 1], p0)));
        }

        _output.WriteLine($"{name} P against GMAT over {epochs.Length} epochs: relative Frobenius {pRelative:E2}, " +
                          $"on the correlation matrix {pCorrelation:E2}");
        _output.WriteLine($"{name} GMAT's computation, its P against its own Φ P0 Φᵀ, on the correlation matrix: " +
                          $"{drift:E2}");
        Assert.True(pRelative < pBound, $"{name} P {pRelative:E2}");
        Assert.True(pCorrelation < pCorrelationBound, $"{name} P on the correlation matrix {pCorrelation:E2}");

        // Q = P with the noise − P without, in both tools, at the end of the arc
        var end = epochs[^1];
        var q = Subtract(Elements(noisy.GetCovariance(end)), Elements(solution.GetCovariance(end)));
        var qGmat = new List<double[]>();
        foreach (double update in GmatCases.NoiseSteps)
        {
            var noisyGmat = GmatReport.Read(name, $"snc_{GmatCases.Format(update)}", true, true, rotation);
            qGmat.Add(Subtract(noisyGmat.Covariance[^1], Congruence(noisyGmat.Phi[^1], GmatCases.NoiseInitialDiagonal)));
            _output.WriteLine($"{name} Q at the end, GMAT's discrete noise every {update} s against the library: " +
                              $"relative Frobenius {RelativeFrobenius(qGmat[^1], q):E2}, on the correlation matrix " +
                              $"{CorrelationError(qGmat[^1], q):E2}");
        }

        // First order in the interval: each halving halves the departure, and the limit 2 Q(Δt/2) − Q(Δt) from each
        // pair of halving steps is the library's Q
        for (int i = 0; i + 1 < qGmat.Count; i++)
        {
            var limit = qGmat[i + 1].Zip(qGmat[i], (half, full) => 2.0 * half - full).ToArray();
            double ratio = RelativeFrobenius(qGmat[i], q) / RelativeFrobenius(qGmat[i + 1], q);
            double error = RelativeFrobenius(limit, q);
            _output.WriteLine($"{name} Q, first-order limit of GMAT from {GmatCases.NoiseSteps[i]} and " +
                              $"{GmatCases.NoiseSteps[i + 1]} s against the library: {error:E2} (departure halved: " +
                              $"ratio {ratio:F3})");
            Assert.InRange(ratio, 1.9, 2.1);
            Assert.True(error < qBound, $"{name} Q limit {error:E2}");
        }

        var tight = Propagate(referenceCase, start, new RtnProcessNoise(1e-20, GmatCases.TangentialNoise, 1e-20), 1e-14);
        var plain = Propagate(referenceCase, start, null, 1e-14);
        var qTight = Subtract(Elements(tight.GetCovariance(end)), Elements(plain.GetCovariance(end)));
        _output.WriteLine($"{name} Q of the library at 1e-13 against 1e-14: {RelativeFrobenius(q, qTight):E2}");
    }

    private static PropagationSolution Propagate(ReferenceCase referenceCase, IO.Astrodynamics.OrbitalParameters.StateVector start,
        RtnProcessNoise noise, double tolerance = ReferenceCases.Tolerance)
    {
        var spacecraft = ReferenceCase.NewSpacecraft(NaifId - 2, start);
        return spacecraft.PropagateWithVariationalEquations(referenceCase.Window, referenceCase.Bodies,
            new RK78Integrator(tolerance, tolerance), referenceCase.AtmosphericDrag,
            referenceCase.SolarRadiationPressure, TimeSpan.FromHours(1.0),
            new VariationalOptions(ForceParameters.None, noise, PropagateCovariance: true));
    }

    // The state with its covariance P0 replaced by R0 P0 R0ᵀ, R0 applied to each 3×3 block
    private static IO.Astrodynamics.OrbitalParameters.StateVector WithRotatedCovariance(
        IO.Astrodynamics.OrbitalParameters.StateVector state, double[] rotation)
    {
        var p0 = state.Covariance!.Value;
        var b = new IO.Astrodynamics.Math.Matrix(6, 6);
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                b.Set(i, j, rotation[3 * i + j]);
                b.Set(3 + i, 3 + j, rotation[3 * i + j]);
            }
        }

        var rotated = b * p0 * b.Transpose();
        var symmetric = new IO.Astrodynamics.Math.Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                symmetric.Set(i, j, 0.5 * (rotated.Get(i, j) + rotated.Get(j, i)));
            }
        }

        return new IO.Astrodynamics.OrbitalParameters.StateVector(state.Position, state.Velocity, state.Observer,
            state.Epoch, state.Frame, symmetric);
    }

    private static double[] Elements(IO.Astrodynamics.Math.Matrix matrix)
    {
        var values = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                values[6 * i + j] = matrix.Get(i, j);
            }
        }

        return values;
    }

    private static double[] Subtract(double[] a, double[] b) => a.Zip(b, (x, y) => x - y).ToArray();

    // Φ P Φᵀ, P row-major 6×6
    private static double[] Propagated(double[] phi, double[] p)
    {
        var result = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < 6; k++)
                {
                    for (int m = 0; m < 6; m++)
                    {
                        sum += phi[6 * i + k] * p[6 * k + m] * phi[6 * j + m];
                    }
                }

                result[6 * i + j] = sum;
            }
        }

        return result;
    }

    // Φ diag(d) Φᵀ
    private static double[] Congruence(double[] phi, double[] diagonal)
    {
        var result = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < 6; k++)
                {
                    sum += phi[6 * i + k] * diagonal[k] * phi[6 * j + k];
                }

                result[6 * i + j] = sum;
            }
        }

        return result;
    }

    private static double RelativeFrobenius(double[] actual, double[] expected)
    {
        double difference = 0.0, norm = 0.0;
        for (int i = 0; i < actual.Length; i++)
        {
            difference += (actual[i] - expected[i]) * (actual[i] - expected[i]);
            norm += expected[i] * expected[i];
        }

        return System.Math.Sqrt(difference / norm);
    }

    // max |ΔP_ij| / √(P_ii P_jj), with the diagonal of the expected matrix: the error on the correlation matrix,
    // independent of the units of the blocks (C6, #387)
    private static double CorrelationError(double[] actual, double[] expected)
    {
        double worst = 0.0;
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                double scale = System.Math.Sqrt(expected[7 * i] * expected[7 * j]);
                worst = System.Math.Max(worst, System.Math.Abs(actual[6 * i + j] - expected[6 * i + j]) / scale);
            }
        }

        return worst;
    }

    private static double Distance(double[] a, double[] b, int offset)
    {
        double sum = 0.0;
        for (int i = offset; i < offset + 3; i++)
        {
            sum += (a[i] - b[i]) * (a[i] - b[i]);
        }

        return System.Math.Sqrt(sum);
    }

    private static void Max(double[] worst, double[] errors)
    {
        for (int i = 0; i < worst.Length; i++)
        {
            worst[i] = System.Math.Max(worst[i], errors[i]);
        }
    }

    private static string Format(IEnumerable<double> values) => string.Join(", ", values.Select(v => v.ToString("E2")));
}
